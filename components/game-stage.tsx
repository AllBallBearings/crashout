'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { Camera, Gauge, HelpCircle, RotateCcw } from 'lucide-react';
import * as THREE from 'three';

import { Button } from '@/components/ui/button';

type Controls = {
  accelerate: boolean;
  brake: boolean;
  left: boolean;
  right: boolean;
};

type TrafficCar = {
  mesh: THREE.Group;
  lane: number;
  direction: 1 | -1;
  baseSpeed: number;
  speed: number;
  crashed: boolean;
  hit: boolean;
  velocity: THREE.Vector3;
  angularVelocity: THREE.Vector3;
};

type GameRuntime = {
  reset: () => void;
};

type GamePhase = 'ready' | 'approach' | 'crash' | 'aftermath' | 'result';
type CameraMode = 'near' | 'chase' | 'overhead';

type VehicleContact = {
  normal: THREE.Vector3;
  depth: number;
};

const TRAFFIC_COLORS = [0x35b8c6, 0xf0b03d, 0xd84c4a, 0x6f7fd7, 0xe6e2d3, 0x6cab63];
const VEHICLE_HALF_WIDTH = 0.7;
const VEHICLE_HALF_HEIGHT = 0.42;
const VEHICLE_HALF_LENGTH = 1.08;
const CAR_REST_Y = 0.42;
const WHEEL_RADIUS = 0.23;
const WHEELBASE = 2.1;

function forwardFromHeading(heading: number) {
  // The modeled front of every car points down its local -Z axis.
  return new THREE.Vector3(-Math.sin(heading), 0, -Math.cos(heading));
}

function vehicleAxes(object: THREE.Object3D) {
  const heading = object.rotation.y;
  return [
    new THREE.Vector2(Math.cos(heading), -Math.sin(heading)),
    new THREE.Vector2(Math.sin(heading), Math.cos(heading)),
  ] as const;
}

function vehicleVerticalRadius(object: THREE.Object3D) {
  const rotation = new THREE.Matrix4().makeRotationFromQuaternion(object.quaternion);
  const elements = rotation.elements;
  return (
    VEHICLE_HALF_WIDTH * Math.abs(elements[1]) +
    VEHICLE_HALF_HEIGHT * Math.abs(elements[5]) +
    VEHICLE_HALF_LENGTH * Math.abs(elements[9])
  );
}

function resolveVehicleOverlap(
  a: THREE.Object3D,
  b: THREE.Object3D,
  aShare = 0.5,
): VehicleContact | null {
  const aAxes = vehicleAxes(a);
  const bAxes = vehicleAxes(b);
  const centerDelta = new THREE.Vector2(b.position.x - a.position.x, b.position.z - a.position.z);
  let smallestDepth = Number.POSITIVE_INFINITY;
  const collisionNormal = new THREE.Vector3();

  for (const axis of [...aAxes, ...bAxes]) {
    const aRadius =
      VEHICLE_HALF_WIDTH * Math.abs(axis.dot(aAxes[0])) +
      VEHICLE_HALF_LENGTH * Math.abs(axis.dot(aAxes[1]));
    const bRadius =
      VEHICLE_HALF_WIDTH * Math.abs(axis.dot(bAxes[0])) +
      VEHICLE_HALF_LENGTH * Math.abs(axis.dot(bAxes[1]));
    const signedDistance = centerDelta.dot(axis);
    const depth = aRadius + bRadius - Math.abs(signedDistance);

    if (depth <= 0) return null;
    if (depth < smallestDepth) {
      smallestDepth = depth;
      const direction = signedDistance < 0 ? -1 : 1;
      collisionNormal.set(axis.x * direction, 0, axis.y * direction);
    }
  }

  const verticalDistance = b.position.y - a.position.y;
  const verticalDepth = vehicleVerticalRadius(a) + vehicleVerticalRadius(b) - Math.abs(verticalDistance);
  if (verticalDepth <= 0) return null;
  if (verticalDepth < smallestDepth) {
    smallestDepth = verticalDepth;
    collisionNormal.set(0, verticalDistance < 0 ? -1 : 1, 0);
  }

  const bShare = 1 - aShare;
  a.position.x -= collisionNormal.x * smallestDepth * aShare;
  a.position.y -= collisionNormal.y * smallestDepth * aShare;
  a.position.z -= collisionNormal.z * smallestDepth * aShare;
  b.position.x += collisionNormal.x * smallestDepth * bShare;
  b.position.y += collisionNormal.y * smallestDepth * bShare;
  b.position.z += collisionNormal.z * smallestDepth * bShare;

  return { normal: collisionNormal, depth: smallestDepth };
}

function integrateCrashBody(
  object: THREE.Object3D,
  velocity: THREE.Vector3,
  angularVelocity: THREE.Vector3,
  dt: number,
) {
  velocity.y -= 9.8 * dt;
  object.position.addScaledVector(velocity, dt);

  const angularSpeed = angularVelocity.length();
  if (angularSpeed > 0.0001) {
    const deltaRotation = new THREE.Quaternion().setFromAxisAngle(
      angularVelocity.clone().normalize(),
      angularSpeed * dt,
    );
    object.quaternion.premultiply(deltaRotation).normalize();
  }

  const groundHeight = vehicleVerticalRadius(object);
  if (object.position.y < groundHeight) {
    object.position.y = groundHeight;
    if (velocity.y < -0.45) velocity.y *= -0.18;
    else velocity.y = 0;

    const horizontalSpeed = Math.hypot(velocity.x, velocity.z);
    if (horizontalSpeed > 0) {
      const stoppedSpeed = Math.max(0, horizontalSpeed - 8.5 * dt);
      const frictionScale = stoppedSpeed / horizontalSpeed;
      velocity.x *= frictionScale;
      velocity.z *= frictionScale;
    }
    angularVelocity.multiplyScalar(Math.pow(0.86, dt * 60));
    if (Math.hypot(velocity.x, velocity.z) < 0.06) {
      velocity.x = 0;
      velocity.z = 0;
    }
    if (angularVelocity.lengthSq() < 0.0025) angularVelocity.set(0, 0, 0);
  } else {
    velocity.x *= Math.pow(0.991, dt * 60);
    velocity.z *= Math.pow(0.991, dt * 60);
    angularVelocity.multiplyScalar(Math.pow(0.992, dt * 60));
  }
}

function money(value: number) {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
    maximumFractionDigits: 0,
  }).format(value);
}

function makeCar(color: number, player = false) {
  const group = new THREE.Group();
  const bodyMaterial = new THREE.MeshStandardMaterial({
    color,
    roughness: 0.28,
    metalness: 0.56,
  });
  const darkMaterial = new THREE.MeshStandardMaterial({
    color: 0x111820,
    roughness: 0.18,
    metalness: 0.35,
  });
  const lightMaterial = new THREE.MeshStandardMaterial({
    color: player ? 0xffd36d : 0xf4f0d9,
    emissive: player ? 0xff8b2e : 0xd9ecff,
    emissiveIntensity: 1.8,
  });

  const body = new THREE.Mesh(new THREE.BoxGeometry(1.15, 0.42, 2.1), bodyMaterial);
  body.position.y = 0;
  body.castShadow = true;
  body.receiveShadow = true;
  group.add(body);

  const cabin = new THREE.Mesh(new THREE.BoxGeometry(0.88, 0.38, 0.95), darkMaterial);
  cabin.position.set(0, 0.35, 0.08);
  cabin.castShadow = true;
  group.add(cabin);

  const lightGeometry = new THREE.BoxGeometry(0.28, 0.12, 0.05);
  for (const x of [-0.36, 0.36]) {
    const light = new THREE.Mesh(lightGeometry, lightMaterial);
    light.position.set(x, 0.05, -1.07);
    group.add(light);
  }

  const wheelGeometry = new THREE.CylinderGeometry(0.23, 0.23, 0.16, 12);
  const wheelMaterial = new THREE.MeshStandardMaterial({ color: 0x090b0d, roughness: 0.9 });
  const frontWheelPivots: THREE.Group[] = [];
  const wheelSpinPivots: THREE.Group[] = [];
  for (const x of [-0.62, 0.62]) {
    for (const z of [-0.68, 0.68]) {
      const steeringPivot = new THREE.Group();
      steeringPivot.position.set(x, -0.15, z);
      group.add(steeringPivot);

      const spinPivot = new THREE.Group();
      steeringPivot.add(spinPivot);

      const wheel = new THREE.Mesh(wheelGeometry, wheelMaterial);
      wheel.rotation.z = Math.PI / 2;
      spinPivot.add(wheel);
      wheelSpinPivots.push(spinPivot);
      if (z < 0) frontWheelPivots.push(steeringPivot);
    }
  }
  group.userData.frontWheelPivots = frontWheelPivots;
  group.userData.wheelSpinPivots = wheelSpinPivots;

  if (player) {
    const glow = new THREE.PointLight(0xff8a2d, 5, 5, 2);
    glow.position.set(0, 0.27, -1.2);
    group.add(glow);
  }
  return group;
}

function addRoad(scene: THREE.Scene) {
  const asphalt = new THREE.MeshStandardMaterial({ color: 0x20252a, roughness: 0.94 });
  const concrete = new THREE.MeshStandardMaterial({ color: 0x59605f, roughness: 0.9 });
  const paint = new THREE.MeshStandardMaterial({
    color: 0xe9d99b,
    emissive: 0x66551f,
    emissiveIntensity: 0.24,
    roughness: 0.7,
  });

  const ground = new THREE.Mesh(new THREE.BoxGeometry(46, 0.5, 46), concrete);
  ground.position.y = -0.34;
  ground.receiveShadow = true;
  scene.add(ground);

  const northSouth = new THREE.Mesh(new THREE.BoxGeometry(9, 0.06, 46), asphalt);
  northSouth.position.y = -0.03;
  northSouth.receiveShadow = true;
  scene.add(northSouth);

  const eastWest = new THREE.Mesh(new THREE.BoxGeometry(46, 0.06, 9), asphalt);
  eastWest.position.y = -0.02;
  eastWest.receiveShadow = true;
  scene.add(eastWest);

  const stripeGeometry = new THREE.BoxGeometry(0.13, 0.035, 1.6);
  for (let z = -21; z <= 21; z += 3.2) {
    if (Math.abs(z) < 5.2) continue;
    const stripe = new THREE.Mesh(stripeGeometry, paint);
    stripe.position.set(0, 0.03, z);
    scene.add(stripe);
  }

  const crossStripeGeometry = new THREE.BoxGeometry(1.6, 0.035, 0.13);
  for (let x = -21; x <= 21; x += 3.2) {
    if (Math.abs(x) < 5.2) continue;
    const stripe = new THREE.Mesh(crossStripeGeometry, paint);
    stripe.position.set(x, 0.04, 0);
    scene.add(stripe);
  }

  const curbMaterial = new THREE.MeshStandardMaterial({ color: 0xa9aca6, roughness: 0.86 });
  const cornerPositions: Array<[number, number]> = [
    [-8.2, -8.2],
    [8.2, -8.2],
    [-8.2, 8.2],
    [8.2, 8.2],
  ];

  cornerPositions.forEach(([x, z], index) => {
    const sidewalk = new THREE.Mesh(new THREE.BoxGeometry(7, 0.22, 7), curbMaterial);
    sidewalk.position.set(x, 0.08, z);
    sidewalk.receiveShadow = true;
    scene.add(sidewalk);

    const height = 3.6 + (index % 3) * 1.1;
    const building = new THREE.Mesh(
      new THREE.BoxGeometry(4.7, height, 4.7),
      new THREE.MeshStandardMaterial({
        color: [0x313941, 0x4a3a37, 0x303b35, 0x3f3b4b][index],
        roughness: 0.78,
        metalness: 0.08,
      }),
    );
    building.position.set(x, height / 2 + 0.2, z);
    building.castShadow = true;
    building.receiveShadow = true;
    scene.add(building);

    const sign = new THREE.Mesh(
      new THREE.BoxGeometry(1.7, 0.18, 0.08),
      new THREE.MeshStandardMaterial({
        color: index % 2 ? 0xff9145 : 0x49cad2,
        emissive: index % 2 ? 0xff612e : 0x2fa8b5,
        emissiveIntensity: 1.4,
      }),
    );
    sign.position.set(x, 2.1, z + (z < 0 ? 2.4 : -2.4));
    scene.add(sign);
  });
}

export function GameStage() {
  const mountRef = useRef<HTMLDivElement>(null);
  const controlsRef = useRef<Controls>({
    accelerate: false,
    brake: false,
    left: false,
    right: false,
  });
  const runtimeRef = useRef<GameRuntime | null>(null);
  const phaseRef = useRef<GamePhase>('ready');
  const cameraModeRef = useRef<CameraMode>('near');
  const helpOpenRef = useRef(false);
  const [score, setScore] = useState(0);
  const [speed, setSpeed] = useState(0);
  const [chain, setChain] = useState(0);
  const [status, setStatus] = useState('READY');
  const [phase, setPhase] = useState<GamePhase>('ready');
  const [cameraMode, setCameraMode] = useState<CameraMode>('near');
  const [helpOpen, setHelpOpen] = useState(false);

  const setControl = useCallback((key: keyof Controls, active: boolean) => {
    controlsRef.current[key] = active;
    if (key === 'accelerate' && active && phaseRef.current === 'ready') {
      phaseRef.current = 'approach';
      setPhase('approach');
      setStatus('LAUNCH');
    }
  }, []);

  const resetGame = useCallback(() => {
    runtimeRef.current?.reset();
    controlsRef.current = { accelerate: false, brake: false, left: false, right: false };
    phaseRef.current = 'ready';
    setPhase('ready');
    setScore(0);
    setSpeed(0);
    setChain(0);
    setStatus('READY');
  }, []);

  const toggleCamera = useCallback(() => {
    const nextMode: CameraMode =
      cameraModeRef.current === 'near'
        ? 'chase'
        : cameraModeRef.current === 'chase'
          ? 'overhead'
          : 'near';
    cameraModeRef.current = nextMode;
    setCameraMode(nextMode);
  }, []);

  const openHelp = useCallback(() => {
    helpOpenRef.current = true;
    controlsRef.current = { accelerate: false, brake: false, left: false, right: false };
    setHelpOpen(true);
  }, []);

  const closeHelp = useCallback(() => {
    helpOpenRef.current = false;
    setHelpOpen(false);
  }, []);

  useEffect(() => {
    const mount = mountRef.current;
    if (!mount) return;

    const scene = new THREE.Scene();
    scene.background = new THREE.Color(0x0a0e13);
    scene.fog = new THREE.FogExp2(0x0a0e13, 0.024);

    const camera = new THREE.PerspectiveCamera(56, 1, 0.1, 100);
    camera.position.set(0, 6, 24);

    const renderer = new THREE.WebGLRenderer({ antialias: true, powerPreference: 'high-performance' });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 1.8));
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    renderer.outputColorSpace = THREE.SRGBColorSpace;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.08;
    mount.appendChild(renderer.domElement);

    scene.add(new THREE.HemisphereLight(0xb9d7ef, 0x3a251a, 1.65));
    const sun = new THREE.DirectionalLight(0xffe0b7, 3.5);
    sun.position.set(-9, 18, 11);
    sun.castShadow = true;
    sun.shadow.mapSize.set(1536, 1536);
    sun.shadow.camera.left = -24;
    sun.shadow.camera.right = 24;
    sun.shadow.camera.top = 24;
    sun.shadow.camera.bottom = -24;
    scene.add(sun);

    addRoad(scene);

    const player = makeCar(0xff6a2b, true);
    player.position.set(0, CAR_REST_Y, 17);
    scene.add(player);
    const playerFrontWheels = player.userData.frontWheelPivots as THREE.Group[];
    const playerWheelSpinPivots = player.userData.wheelSpinPivots as THREE.Group[];

    const trafficSeed = [
      { x: -22, lane: -2.15, direction: 1 as const, speed: 6.5 },
      { x: -14, lane: -2.15, direction: 1 as const, speed: 5.8 },
      { x: -6, lane: -2.15, direction: 1 as const, speed: 7.2 },
      { x: 3, lane: -2.15, direction: 1 as const, speed: 6.1 },
      { x: 12, lane: -2.15, direction: 1 as const, speed: 6.8 },
      { x: 21, lane: -2.15, direction: 1 as const, speed: 5.6 },
      { x: 22, lane: 2.15, direction: -1 as const, speed: 6.2 },
      { x: 14, lane: 2.15, direction: -1 as const, speed: 7.0 },
      { x: 6, lane: 2.15, direction: -1 as const, speed: 5.5 },
      { x: -3, lane: 2.15, direction: -1 as const, speed: 6.7 },
      { x: -12, lane: 2.15, direction: -1 as const, speed: 5.9 },
      { x: -21, lane: 2.15, direction: -1 as const, speed: 6.4 },
    ];

    const traffic: TrafficCar[] = trafficSeed.map((seed, index) => {
      const mesh = makeCar(TRAFFIC_COLORS[index % TRAFFIC_COLORS.length]);
      mesh.position.set(seed.x, CAR_REST_Y, seed.lane);
      mesh.rotation.y = seed.direction === 1 ? -Math.PI / 2 : Math.PI / 2;
      scene.add(mesh);
      return {
        mesh,
        lane: seed.lane,
        direction: seed.direction,
        baseSpeed: seed.speed,
        speed: seed.speed,
        crashed: false,
        hit: false,
        velocity: new THREE.Vector3(),
        angularVelocity: new THREE.Vector3(),
      };
    });

    let playerSpeed = 0;
    let playerHeading = 0;
    let scoreValue = 0;
    let chainValue = 0;
    let lastTime = performance.now();
    let animationFrame = 0;
    let shake = 0;
    let lastHudUpdate = 0;
    let playerCrashed = false;
    let steeringAngle = 0;
    let crashStartedAt = 0;
    const playerVelocity = new THREE.Vector3();
    const playerAngularVelocity = new THREE.Vector3();
    const cinematicTarget = player.position.clone();

    const reset = () => {
      player.position.set(0, CAR_REST_Y, 17);
      player.rotation.set(0, 0, 0);
      playerSpeed = 0;
      playerHeading = 0;
      playerCrashed = false;
      steeringAngle = 0;
      crashStartedAt = 0;
      playerVelocity.set(0, 0, 0);
      playerAngularVelocity.set(0, 0, 0);
      playerFrontWheels.forEach((wheel) => {
        wheel.rotation.y = 0;
      });
      playerWheelSpinPivots.forEach((wheel) => {
        wheel.rotation.x = 0;
      });
      cinematicTarget.copy(player.position);
      scoreValue = 0;
      chainValue = 0;
      traffic.forEach((car, index) => {
        const seed = trafficSeed[index];
        car.mesh.position.set(seed.x, CAR_REST_Y, seed.lane);
        car.mesh.rotation.set(0, seed.direction === 1 ? -Math.PI / 2 : Math.PI / 2, 0);
        car.speed = car.baseSpeed;
        car.crashed = false;
        car.hit = false;
        car.velocity.set(0, 0, 0);
        car.angularVelocity.set(0, 0, 0);
        (car.mesh.userData.wheelSpinPivots as THREE.Group[]).forEach((wheel) => {
          wheel.rotation.x = 0;
        });
      });
    };
    runtimeRef.current = { reset };

    const crashCar = (car: TrafficCar, impact: number, impulse: THREE.Vector3) => {
      car.crashed = true;
      car.hit = true;
      car.velocity.copy(impulse);
      const variation = Math.sin(car.mesh.position.x * 1.73 + car.mesh.position.z * 2.11);
      car.velocity.y += THREE.MathUtils.clamp(0.45 + impact * (0.055 + Math.abs(variation) * 0.025), 0.7, 3.2);
      car.angularVelocity.set(
        (0.7 + impact * 0.045) * (variation < 0 ? -1 : 1),
        (car.direction * 0.5 + variation * 0.35) * Math.min(impact * 0.075, 1.8),
        (0.55 + impact * 0.04) * (Math.cos(car.mesh.position.x * 0.91) < 0 ? -1 : 1),
      );
      chainValue += 1;
      scoreValue += Math.round(impact * (1100 + chainValue * 240));
      setScore(scoreValue);
      setChain(chainValue);
      setStatus(chainValue > 1 ? `CHAIN ×${chainValue}` : 'IMPACT');
      shake = Math.min(0.75, 0.22 + impact * 0.025);
    };

    const handleResize = () => {
      const width = mount.clientWidth;
      const height = mount.clientHeight;
      renderer.setSize(width, height, false);
      camera.aspect = Math.max(width / Math.max(height, 1), 0.5);
      camera.updateProjectionMatrix();
    };
    handleResize();
    const resizeObserver = new ResizeObserver(handleResize);
    resizeObserver.observe(mount);

    const onKeyDown = (event: KeyboardEvent) => {
      if (['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', ' '].includes(event.key)) {
        event.preventDefault();
      }
      if (event.key === 'Escape') {
        closeHelp();
        return;
      }
      if (helpOpenRef.current) return;
      if (event.key === 'ArrowUp' || event.key.toLowerCase() === 'w') setControl('accelerate', true);
      if (event.key === 'ArrowDown' || event.key.toLowerCase() === 's') setControl('brake', true);
      if (event.key === 'ArrowLeft' || event.key.toLowerCase() === 'a') setControl('left', true);
      if (event.key === 'ArrowRight' || event.key.toLowerCase() === 'd') setControl('right', true);
      if (event.key.toLowerCase() === 'c') toggleCamera();
      if (event.key.toLowerCase() === 'r') resetGame();
      if ((event.key === 'Enter' || event.key === ' ') && phaseRef.current === 'result') resetGame();
    };
    const onKeyUp = (event: KeyboardEvent) => {
      if (event.key === 'ArrowUp' || event.key.toLowerCase() === 'w') setControl('accelerate', false);
      if (event.key === 'ArrowDown' || event.key.toLowerCase() === 's') setControl('brake', false);
      if (event.key === 'ArrowLeft' || event.key.toLowerCase() === 'a') setControl('left', false);
      if (event.key === 'ArrowRight' || event.key.toLowerCase() === 'd') setControl('right', false);
    };
    window.addEventListener('keydown', onKeyDown, { passive: false });
    window.addEventListener('keyup', onKeyUp);

    const clock = (now: number) => {
      const dt = Math.min((now - lastTime) / 1000, 0.05);
      lastTime = now;
      const controls = controlsRef.current;
      const simulationActive = phaseRef.current !== 'result';
      let forward = forwardFromHeading(playerHeading);

      if (simulationActive && !playerCrashed) {
        if (controls.accelerate) playerSpeed = Math.min(playerSpeed + 14.5 * dt, 21);
        if (controls.brake) playerSpeed = Math.max(playerSpeed - 22 * dt, 0);
        if (!controls.accelerate && !controls.brake) playerSpeed *= Math.pow(0.982, dt * 60);
        if (playerSpeed < 0.025) playerSpeed = 0;

        const steerInput = (controls.left ? 1 : 0) - (controls.right ? 1 : 0);
        const targetSteeringAngle = steerInput * THREE.MathUtils.degToRad(28);
        steeringAngle = THREE.MathUtils.damp(steeringAngle, targetSteeringAngle, 8.5, dt);
        if (playerSpeed > 0.35) {
          playerHeading += (playerSpeed / WHEELBASE) * Math.tan(steeringAngle) * 0.28 * dt;
        }
        playerFrontWheels.forEach((wheel) => {
          wheel.rotation.y = steeringAngle;
        });
        playerWheelSpinPivots.forEach((wheel) => {
          wheel.rotation.x -= (playerSpeed * dt) / WHEEL_RADIUS;
        });

        forward = forwardFromHeading(playerHeading);
        player.position.addScaledVector(forward, playerSpeed * dt);
        player.position.x = THREE.MathUtils.clamp(player.position.x, -4.75, 4.75);
        player.position.z = THREE.MathUtils.clamp(player.position.z, -21, 19);
        player.rotation.y = playerHeading;
      } else if (simulationActive && playerCrashed) {
        integrateCrashBody(player, playerVelocity, playerAngularVelocity, dt);
        playerHeading = player.rotation.y;
        forward = forwardFromHeading(playerHeading);
      }

      if (simulationActive) {
        for (const car of traffic) {
          if (car.crashed) continue;

          let nearestGap = 48;
          let leaderSpeed = car.baseSpeed;
          for (const other of traffic) {
            if (other === car || other.crashed || other.lane !== car.lane || other.direction !== car.direction) continue;
            let forwardGap = (other.mesh.position.x - car.mesh.position.x) * car.direction;
            if (forwardGap <= 0) forwardGap += 48;
            if (forwardGap < nearestGap) {
              nearestGap = forwardGap;
              leaderSpeed = other.speed;
            }
          }

          const spacingFactor = THREE.MathUtils.smoothstep(nearestGap, 2.6, 7.2);
          const targetSpeed = Math.min(car.baseSpeed, leaderSpeed + Math.max(nearestGap - 3.2, 0) * 0.65) * spacingFactor;
          car.speed = THREE.MathUtils.damp(car.speed, targetSpeed, nearestGap < 5 ? 9 : 2.2, dt);
        }

        for (const car of traffic) {
          if (!car.crashed) {
            car.mesh.position.x += car.direction * car.speed * dt;
            const wheelSpinPivots = car.mesh.userData.wheelSpinPivots as THREE.Group[];
            wheelSpinPivots.forEach((wheel) => {
              wheel.rotation.x -= (car.speed * dt) / WHEEL_RADIUS;
            });
            if (car.mesh.position.x > 24) {
              car.mesh.position.x = -24;
              car.hit = false;
            } else if (car.mesh.position.x < -24) {
              car.mesh.position.x = 24;
              car.hit = false;
            }
          } else {
            integrateCrashBody(car.mesh, car.velocity, car.angularVelocity, dt);
          }

          const contact = resolveVehicleOverlap(player, car.mesh, car.crashed ? 0.5 : 0.68);
          const playerImpactSpeed = playerCrashed ? playerVelocity.length() : playerSpeed;
          if (contact && !car.hit && playerImpactSpeed > 2.1) {
            const impact = playerImpactSpeed + car.speed * 0.72;
            const impulse = playerCrashed
              ? playerVelocity.clone().multiplyScalar(0.64)
              : forward.clone().multiplyScalar(playerSpeed * 0.62);
            impulse.x += car.direction * car.speed * 0.38;
            crashCar(car, impact, impulse);

            if (!playerCrashed) {
              playerCrashed = true;
              playerVelocity.copy(forward).multiplyScalar(playerSpeed * 0.56);
              playerVelocity.x -= contact.normal.x * car.speed * 0.18;
              playerVelocity.z -= contact.normal.z * car.speed * 0.18;
              playerVelocity.y = THREE.MathUtils.clamp(impact * 0.045, 0.65, 2.2);
              const rollImpulse = Math.min(impact * 0.055, 1.65);
              playerAngularVelocity.set(
                contact.normal.z * rollImpulse,
                (contact.normal.x * 0.5 + car.direction * 0.3) * rollImpulse,
                -contact.normal.x * rollImpulse,
              );
              playerSpeed = 0;
              crashStartedAt = now;
              phaseRef.current = 'crash';
              setPhase('crash');
              setStatus('INITIAL IMPACT');
              controlsRef.current = { accelerate: false, brake: false, left: false, right: false };
            } else {
              playerVelocity.multiplyScalar(0.72);
            }
          } else if (contact && playerCrashed && car.crashed) {
            const normal = contact.normal;
            const closingSpeed = playerVelocity.clone().sub(car.velocity).dot(normal);
            if (closingSpeed > 0) {
              playerVelocity.addScaledVector(normal, -closingSpeed * 0.58);
              car.velocity.addScaledVector(normal, closingSpeed * 0.58);
              playerAngularVelocity.add(new THREE.Vector3(normal.z, normal.x * 0.35, -normal.x).multiplyScalar(closingSpeed * 0.045));
            }
          }
        }

        for (let i = 0; i < traffic.length; i += 1) {
          const a = traffic[i];
          for (let j = i + 1; j < traffic.length; j += 1) {
            const b = traffic[j];
            const contact = resolveVehicleOverlap(a.mesh, b.mesh);
            if (!contact) continue;
            if (!a.crashed && !b.crashed) {
              a.mesh.position.y = CAR_REST_Y;
              b.mesh.position.y = CAR_REST_Y;
              continue;
            }

            if (a.crashed && !b.hit) {
              const impact = Math.max(a.velocity.length(), 2.5) + b.speed;
              const impulse = a.velocity.clone().multiplyScalar(0.58);
              impulse.x += b.direction * b.speed * 0.42;
              crashCar(b, impact, impulse);
            } else if (b.crashed && !a.hit) {
              const impact = Math.max(b.velocity.length(), 2.5) + a.speed;
              const impulse = b.velocity.clone().multiplyScalar(0.58);
              impulse.x += a.direction * a.speed * 0.42;
              crashCar(a, impact, impulse);
            } else if (a.crashed && b.crashed) {
              const normal = contact.normal;
              const closingSpeed = a.velocity.clone().sub(b.velocity).dot(normal);
              if (closingSpeed > 0) {
                a.velocity.addScaledVector(normal, -closingSpeed * 0.52);
                b.velocity.addScaledVector(normal, closingSpeed * 0.52);
                a.angularVelocity.add(new THREE.Vector3(normal.z, normal.x * 0.3, -normal.x).multiplyScalar(closingSpeed * 0.04));
                b.angularVelocity.add(new THREE.Vector3(-normal.z, -normal.x * 0.3, normal.x).multiplyScalar(closingSpeed * 0.04));
              }
            }
          }
        }

        if (playerCrashed) {
          const elapsed = (now - crashStartedAt) / 1000;
          if (phaseRef.current === 'crash' && elapsed > 1.1) {
            phaseRef.current = 'aftermath';
            setPhase('aftermath');
            setStatus('PILEUP IN MOTION');
          }

          const wreckMotion =
            playerVelocity.length() + playerAngularVelocity.length() * 0.35 +
            traffic.reduce(
              (total, car) => total + (car.crashed ? car.velocity.length() + car.angularVelocity.length() * 0.35 : 0),
              0,
            );
          if (elapsed > 3.5 && (wreckMotion < 1.5 || elapsed > 10)) {
            phaseRef.current = 'result';
            setPhase('result');
            setStatus('RUN COMPLETE');
            setSpeed(0);
            controlsRef.current = { accelerate: false, brake: false, left: false, right: false };
          }
        } else if (phaseRef.current === 'approach' && player.position.z < -12) {
          phaseRef.current = 'result';
          setPhase('result');
          setStatus('MISSED THE JUNCTION');
          setSpeed(0);
          controlsRef.current = { accelerate: false, brake: false, left: false, right: false };
        }
      }

      let desiredCamera: THREE.Vector3;
      if (!playerCrashed) {
        const cameraModeValue = cameraModeRef.current;
        const cameraDistance = cameraModeValue === 'near' ? 3.5 : cameraModeValue === 'chase' ? 6.2 : 5.2;
        const cameraHeight = cameraModeValue === 'near' ? 1.65 : cameraModeValue === 'chase' ? 3.2 : 8.5;
        desiredCamera = player.position.clone().addScaledVector(forward, -cameraDistance);
        desiredCamera.y += cameraHeight;
        camera.position.copy(desiredCamera);
        camera.lookAt(player.position.x, player.position.y, player.position.z);
      } else {
        const crashCenter = player.position.clone();
        let crashCount = 1;
        for (const car of traffic) {
          if (!car.crashed) continue;
          crashCenter.add(car.mesh.position);
          crashCount += 1;
        }
        crashCenter.multiplyScalar(1 / crashCount);
        cinematicTarget.lerp(crashCenter, 1 - Math.pow(0.018, dt));
        const cameraModeValue = cameraModeRef.current;
        const crashOffset =
          cameraModeValue === 'near'
            ? new THREE.Vector3(6.8, 5.4, 7.8)
            : cameraModeValue === 'chase'
              ? new THREE.Vector3(10.5, 11.5, 12.5)
              : new THREE.Vector3(0, 18.5, 5.5);
        desiredCamera = cinematicTarget.clone().add(crashOffset);
        camera.position.lerp(desiredCamera, 1 - Math.pow(0.045, dt));
        camera.lookAt(cinematicTarget.x, cinematicTarget.y + 0.35, cinematicTarget.z);
      }
      if (shake > 0.005) {
        camera.position.x += (Math.random() - 0.5) * shake * 0.55;
        camera.position.y += (Math.random() - 0.5) * shake * 0.28;
        shake *= Math.pow(0.88, dt * 60);
      }
      renderer.render(scene, camera);

      if (now - lastHudUpdate > 80) {
        const displayedSpeed = phaseRef.current === 'result' ? 0 : playerCrashed ? playerVelocity.length() : playerSpeed;
        setSpeed(Math.round(displayedSpeed * 6.2));
        if (phaseRef.current === 'ready') setStatus('READY');
        if (phaseRef.current === 'approach' && chainValue === 0) {
          setStatus(player.position.z < 7 ? 'COMMIT' : 'BUILD SPEED');
        }
        lastHudUpdate = now;
      }
      animationFrame = requestAnimationFrame(clock);
    };
    animationFrame = requestAnimationFrame(clock);

    return () => {
      cancelAnimationFrame(animationFrame);
      resizeObserver.disconnect();
      window.removeEventListener('keydown', onKeyDown);
      window.removeEventListener('keyup', onKeyUp);
      runtimeRef.current = null;
      renderer.dispose();
      scene.traverse((object) => {
        if (object instanceof THREE.Mesh) {
          object.geometry.dispose();
          const materials = Array.isArray(object.material) ? object.material : [object.material];
          materials.forEach((material) => material.dispose());
        }
      });
      renderer.domElement.remove();
    };
  }, [closeHelp, resetGame, setControl, toggleCamera]);

  return (
    <main className="relative h-[100svh] w-screen overflow-hidden bg-[#07090d] text-[#f6f3e9]">
      <div ref={mountRef} className="absolute inset-0" aria-label="Playable miniature city intersection" />
      <div className="scanlines pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_50%_45%,transparent_28%,rgb(4_6_10/58%)_100%)]" />

      <header className="pointer-events-none absolute inset-x-0 top-0 z-10 flex items-start justify-between p-4 sm:p-6">
        <div className="hud-glass pointer-events-auto flex items-center gap-3 rounded-xl px-3 py-2.5 sm:px-4">
          <div className="grid h-8 w-8 place-items-center rounded-lg bg-[#f77a2d] text-sm font-black italic text-[#111318] shadow-[0_0_25px_rgb(247_122_45/40%)]">
            C!
          </div>
          <div>
            <p className="font-[var(--font-display)] text-base font-black uppercase leading-none tracking-[0.12em] sm:text-lg">
              Crashout
            </p>
            <p className="mt-1 font-mono text-[9px] uppercase tracking-[0.22em] text-white/45 sm:text-[10px]">
              Junction 01 · Run 001
            </p>
          </div>
        </div>

        <div className="hud-glass pointer-events-auto flex items-center gap-1 rounded-xl p-1.5">
          <Button type="button" variant="ghost" size="lg" onClick={toggleCamera} aria-label={`Camera: ${cameraMode}. Change camera`} className="h-9 gap-2 px-2.5 font-mono text-[9px] font-bold uppercase tracking-[0.13em] text-white/65 hover:bg-white/10 hover:text-white">
            <Camera className="h-4 w-4" />
            <span className="hidden sm:inline">{cameraMode}</span>
          </Button>
          <Button type="button" variant="ghost" size="lg" aria-label="Open controls help" onClick={openHelp} className="h-9 gap-2 px-2.5 font-mono text-[9px] font-bold uppercase tracking-[0.13em] text-white/65 hover:bg-white/10 hover:text-white">
            <HelpCircle className="h-4 w-4" />
            <span className="hidden sm:inline">Help</span>
          </Button>
          <Button type="button" size="lg" onClick={resetGame} className="h-9 gap-2 bg-[#f1eee4] px-3 text-xs font-black uppercase tracking-[0.12em] text-[#111318] hover:bg-white">
            <RotateCcw className="h-3.5 w-3.5" />
            <span className="hidden sm:inline">Reset</span>
          </Button>
        </div>
      </header>

      {helpOpen && (
        <div className="absolute inset-0 z-50 grid place-items-center bg-black/55 px-4 backdrop-blur-sm" role="presentation" onPointerDown={closeHelp}>
          <dialog open aria-labelledby="controls-help-title" className="hud-glass relative m-0 w-full max-w-md rounded-2xl border border-white/10 p-6 text-[#f6f3e9] shadow-[0_30px_100px_rgb(0_0_0/80%)]" onPointerDown={(event) => event.stopPropagation()}>
            <div className="flex items-start justify-between gap-4">
              <div>
                <h2 id="controls-help-title" className="font-[var(--font-display)] text-xl font-black uppercase italic tracking-[-0.02em]">Crash controls</h2>
                <p className="mt-2 text-sm leading-relaxed text-white/55">Build speed, choose an impact angle, and let the chain reaction play out.</p>
              </div>
              <Button type="button" variant="ghost" size="sm" onClick={closeHelp} className="text-white/55 hover:bg-white/10 hover:text-white">Close</Button>
            </div>
            <dl className="mt-5 grid grid-cols-[auto_1fr] gap-x-4 gap-y-3 border-y border-white/10 py-4 text-sm">
              <dt className="font-mono font-black text-[#ff9a3e]">W / ↑</dt><dd className="text-white/70">Accelerate forward</dd>
              <dt className="font-mono font-black text-[#ff9a3e]">S / ↓</dt><dd className="text-white/70">Brake</dd>
              <dt className="font-mono font-black text-[#ff9a3e]">A D / ← →</dt><dd className="text-white/70">Steer the front wheels</dd>
              <dt className="font-mono font-black text-[#72d9dd]">C</dt><dd className="text-white/70">Cycle near, chase, and overhead cameras</dd>
              <dt className="font-mono font-black text-[#72d9dd]">R</dt><dd className="text-white/70">Restart the run</dd>
            </dl>
            <p className="mt-4 font-mono text-[10px] uppercase leading-relaxed tracking-[0.15em] text-white/40">After impact, steering locks and the camera follows the wreck automatically.</p>
          </dialog>
        </div>
      )}

      <section className="pointer-events-none absolute left-1/2 top-[82px] z-10 -translate-x-1/2 text-center sm:top-6">
        <div className="hud-glass min-w-[190px] rounded-xl px-5 py-3 sm:min-w-[240px]">
          <p className="font-mono text-[9px] uppercase tracking-[0.26em] text-[#72d9dd]">
            {phase === 'result' ? 'Final damage' : 'Live damage'}
          </p>
          <p className="mt-0.5 font-mono text-2xl font-black tabular-nums tracking-[-0.06em] sm:text-3xl">{money(score)}</p>
          <div className="mt-1 flex items-center justify-center gap-2 font-mono text-[9px] uppercase tracking-[0.18em] text-white/50">
            <span className="status-pulse h-1.5 w-1.5 rounded-full bg-[#ff9a3e]" />
            {status}
            {chain > 0 && <span className="text-[#ffb048]">· {chain} vehicles</span>}
          </div>
        </div>
      </section>

      {phase === 'ready' && (
        <div className="hud-glass pointer-events-none absolute bottom-4 left-4 z-10 w-[min(320px,calc(100%-132px))] rounded-2xl px-4 py-4 text-left sm:bottom-6 sm:left-6 sm:px-5">
          <p className="font-[var(--font-display)] text-xl font-black uppercase italic leading-[0.95] tracking-[-0.03em] text-white sm:text-2xl">
            Build speed. <span className="text-[#ff8a35]">Pick your impact.</span>
          </p>
          <p className="mt-2 text-[11px] leading-relaxed text-white/55 sm:text-xs">
            Hold W to launch. Steer with A and D. Press C to change camera.
          </p>
        </div>
      )}

      {(phase === 'crash' || phase === 'aftermath') && (
        <div className="pointer-events-none absolute left-1/2 top-[35%] z-10 -translate-x-1/2 text-center">
          <p className="font-[var(--font-display)] text-[clamp(26px,5vw,56px)] font-black uppercase italic leading-none tracking-[-0.04em] text-[#ff8a35] drop-shadow-[0_4px_28px_rgb(0_0_0/90%)]">
            {phase === 'crash' ? 'Impact!' : `Chain ×${Math.max(chain, 1)}`}
          </p>
          <p className="mt-2 font-mono text-[9px] uppercase tracking-[0.24em] text-white/55">Cinematic crash camera</p>
        </div>
      )}

      {phase === 'result' && (
        <section className="hud-glass absolute left-1/2 top-1/2 z-30 w-[min(420px,calc(100%-32px))] -translate-x-1/2 -translate-y-1/2 rounded-3xl border border-[#ff9a3e]/30 px-7 py-7 text-center shadow-[0_24px_90px_rgb(0_0_0/70%)] sm:px-10 sm:py-9">
          <p className="font-mono text-[9px] uppercase tracking-[0.3em] text-[#72d9dd]">Junction 01 · Complete</p>
          <h2 className="mt-3 font-[var(--font-display)] text-3xl font-black uppercase italic tracking-[-0.04em] text-white sm:text-5xl">Crash total</h2>
          <p className="mt-2 font-mono text-4xl font-black tabular-nums tracking-[-0.07em] text-[#ff9a3e] sm:text-5xl">{money(score)}</p>
          <div className="mx-auto mt-5 flex max-w-xs items-center justify-center gap-6 border-y border-white/10 py-3 font-mono text-[10px] uppercase tracking-[0.16em] text-white/50">
            <span><strong className="mr-1 text-base text-white">{chain}</strong> vehicles</span>
            <span><strong className="mr-1 text-base text-white">×{chain}</strong> chain</span>
          </div>
          <Button type="button" size="lg" onClick={resetGame} className="mt-6 h-11 w-full bg-[#f4772c] font-[var(--font-display)] text-sm font-black uppercase tracking-[0.14em] text-[#17130f] hover:bg-[#ff994a]">
            Run it again
          </Button>
          <p className="mt-3 font-mono text-[9px] uppercase tracking-[0.2em] text-white/35">Press R, Enter, or Space</p>
        </section>
      )}

      {phase !== 'result' && (
        <aside className="hud-glass pointer-events-none absolute right-3 top-1/2 z-20 w-[108px] -translate-y-1/2 rounded-2xl px-3 py-4 sm:right-6 sm:w-[132px] sm:px-4">
          <div className="flex items-center gap-1.5 font-mono text-[9px] uppercase tracking-[0.17em] text-white/45"><Gauge className="h-3 w-3" /> Speed</div>
          <p className="mt-1 font-mono text-2xl font-black tabular-nums tracking-[-0.07em] sm:text-3xl">
            {String(speed).padStart(3, '0')}<span className="ml-1 text-[8px] tracking-normal text-white/40">MPH</span>
          </p>
          <div className="mt-4 border-t border-white/10 pt-3">
            <p className="font-mono text-[8px] uppercase tracking-[0.17em] text-white/40">Impact zone</p>
            <div className="mt-2 grid gap-1">
              {[0, 1, 2, 3, 4, 5, 6].map((bar) => (
                <span key={bar} className={`h-1.5 rounded-full transition-colors ${bar < Math.min(Math.ceil(speed / 14), 7) ? 'bg-[#ff8a35] shadow-[0_0_10px_rgb(255_138_53/35%)]' : 'bg-white/10'}`} />
              ))}
            </div>
          </div>
          <p className="mt-4 font-mono text-[8px] uppercase leading-relaxed tracking-[0.14em] text-[#72d9dd]/65">Camera<br /><span className="text-white/65">{cameraMode}</span></p>
        </aside>
      )}
    </main>
  );
}

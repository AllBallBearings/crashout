'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { Gauge, RotateCcw } from 'lucide-react';
import * as THREE from 'three';

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
  speed: number;
  crashed: boolean;
  hit: boolean;
  velocity: THREE.Vector3;
  spin: number;
};

type GameRuntime = {
  reset: () => void;
};

type VehicleContact = {
  normal: THREE.Vector2;
  depth: number;
};

const TRAFFIC_COLORS = [0x35b8c6, 0xf0b03d, 0xd84c4a, 0x6f7fd7, 0xe6e2d3, 0x6cab63];
const VEHICLE_HALF_WIDTH = 0.7;
const VEHICLE_HALF_LENGTH = 1.08;

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

function resolveVehicleOverlap(
  a: THREE.Object3D,
  b: THREE.Object3D,
  aShare = 0.5,
): VehicleContact | null {
  const aAxes = vehicleAxes(a);
  const bAxes = vehicleAxes(b);
  const centerDelta = new THREE.Vector2(b.position.x - a.position.x, b.position.z - a.position.z);
  let smallestDepth = Number.POSITIVE_INFINITY;
  let collisionNormal = new THREE.Vector2();

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
      collisionNormal = axis.clone().multiplyScalar(signedDistance < 0 ? -1 : 1);
    }
  }

  const bShare = 1 - aShare;
  a.position.x -= collisionNormal.x * smallestDepth * aShare;
  a.position.z -= collisionNormal.y * smallestDepth * aShare;
  b.position.x += collisionNormal.x * smallestDepth * bShare;
  b.position.z += collisionNormal.y * smallestDepth * bShare;

  return { normal: collisionNormal, depth: smallestDepth };
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
  body.position.y = 0.43;
  body.castShadow = true;
  body.receiveShadow = true;
  group.add(body);

  const cabin = new THREE.Mesh(new THREE.BoxGeometry(0.88, 0.38, 0.95), darkMaterial);
  cabin.position.set(0, 0.78, 0.08);
  cabin.castShadow = true;
  group.add(cabin);

  const lightGeometry = new THREE.BoxGeometry(0.28, 0.12, 0.05);
  for (const x of [-0.36, 0.36]) {
    const light = new THREE.Mesh(lightGeometry, lightMaterial);
    light.position.set(x, 0.48, -1.07);
    group.add(light);
  }

  const wheelGeometry = new THREE.CylinderGeometry(0.23, 0.23, 0.16, 12);
  const wheelMaterial = new THREE.MeshStandardMaterial({ color: 0x090b0d, roughness: 0.9 });
  for (const x of [-0.62, 0.62]) {
    for (const z of [-0.68, 0.68]) {
      const wheel = new THREE.Mesh(wheelGeometry, wheelMaterial);
      wheel.rotation.z = Math.PI / 2;
      wheel.position.set(x, 0.28, z);
      group.add(wheel);
    }
  }

  if (player) {
    const glow = new THREE.PointLight(0xff8a2d, 5, 5, 2);
    glow.position.set(0, 0.7, -1.2);
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
  const [score, setScore] = useState(0);
  const [speed, setSpeed] = useState(0);
  const [chain, setChain] = useState(0);
  const [status, setStatus] = useState('APPROACH');
  const [hasDriven, setHasDriven] = useState(false);

  const setControl = useCallback((key: keyof Controls, active: boolean) => {
    controlsRef.current[key] = active;
    if (active) setHasDriven(true);
  }, []);

  const resetGame = useCallback(() => {
    runtimeRef.current?.reset();
    setScore(0);
    setSpeed(0);
    setChain(0);
    setStatus('APPROACH');
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
    player.position.set(0, 0.08, 16);
    scene.add(player);

    const trafficSeed = [
      { x: -15, lane: -2.15, direction: 1 as const, speed: 6.1 },
      { x: -7, lane: -2.15, direction: 1 as const, speed: 5.4 },
      { x: 4, lane: -2.15, direction: 1 as const, speed: 6.8 },
      { x: 15, lane: 2.15, direction: -1 as const, speed: 6.2 },
      { x: 8, lane: 2.15, direction: -1 as const, speed: 5.1 },
      { x: -3, lane: 2.15, direction: -1 as const, speed: 7.0 },
      { x: 21, lane: -2.15, direction: 1 as const, speed: 5.8 },
      { x: -20, lane: 2.15, direction: -1 as const, speed: 5.6 },
    ];

    const traffic: TrafficCar[] = trafficSeed.map((seed, index) => {
      const mesh = makeCar(TRAFFIC_COLORS[index % TRAFFIC_COLORS.length]);
      mesh.position.set(seed.x, 0.08, seed.lane);
      mesh.rotation.y = seed.direction === 1 ? -Math.PI / 2 : Math.PI / 2;
      scene.add(mesh);
      return {
        mesh,
        lane: seed.lane,
        direction: seed.direction,
        speed: seed.speed,
        crashed: false,
        hit: false,
        velocity: new THREE.Vector3(),
        spin: 0,
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

    const reset = () => {
      player.position.set(0, 0.08, 16);
      player.rotation.set(0, 0, 0);
      playerSpeed = 0;
      playerHeading = 0;
      scoreValue = 0;
      chainValue = 0;
      traffic.forEach((car, index) => {
        const seed = trafficSeed[index];
        car.mesh.position.set(seed.x, 0.08, seed.lane);
        car.mesh.rotation.set(0, seed.direction === 1 ? -Math.PI / 2 : Math.PI / 2, 0);
        car.crashed = false;
        car.hit = false;
        car.velocity.set(0, 0, 0);
        car.spin = 0;
      });
    };
    runtimeRef.current = { reset };

    const crashCar = (car: TrafficCar, impact: number, impulse: THREE.Vector3) => {
      car.crashed = true;
      car.hit = true;
      car.velocity.copy(impulse);
      car.spin = (car.direction * 0.9 + Math.sin(car.mesh.position.x) * 0.35) * impact * 0.07;
      chainValue += 1;
      scoreValue += Math.round(impact * (920 + chainValue * 170));
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
      if (event.key === 'ArrowUp' || event.key.toLowerCase() === 'w') setControl('accelerate', true);
      if (event.key === 'ArrowDown' || event.key.toLowerCase() === 's') setControl('brake', true);
      if (event.key === 'ArrowLeft' || event.key.toLowerCase() === 'a') setControl('left', true);
      if (event.key === 'ArrowRight' || event.key.toLowerCase() === 'd') setControl('right', true);
      if (event.key.toLowerCase() === 'r') resetGame();
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

      if (controls.accelerate) playerSpeed = Math.min(playerSpeed + 12.5 * dt, 19);
      if (controls.brake) playerSpeed = Math.max(playerSpeed - 18 * dt, -6);
      if (!controls.accelerate && !controls.brake) playerSpeed *= Math.pow(0.975, dt * 60);
      if (Math.abs(playerSpeed) < 0.025) playerSpeed = 0;

      const steerInput = (controls.left ? 1 : 0) - (controls.right ? 1 : 0);
      if (steerInput !== 0 && Math.abs(playerSpeed) > 0.35) {
        playerHeading += steerInput * Math.sign(playerSpeed) * Math.min(Math.abs(playerSpeed) / 7, 1.25) * dt;
      }

      const forward = forwardFromHeading(playerHeading);
      player.position.addScaledVector(forward, playerSpeed * dt);
      player.position.x = THREE.MathUtils.clamp(player.position.x, -5.1, 5.1);
      player.position.z = THREE.MathUtils.clamp(player.position.z, -21, 20);
      player.rotation.y = playerHeading;

      for (const car of traffic) {
        if (!car.crashed) {
          car.mesh.position.x += car.direction * car.speed * dt;
          if (car.mesh.position.x > 23) {
            car.mesh.position.x = -23;
            car.hit = false;
          } else if (car.mesh.position.x < -23) {
            car.mesh.position.x = 23;
            car.hit = false;
          }
        } else {
          car.mesh.position.addScaledVector(car.velocity, dt);
          car.velocity.multiplyScalar(Math.pow(0.965, dt * 60));
          car.mesh.rotation.y += car.spin * dt;
          car.spin *= Math.pow(0.97, dt * 60);
        }

        const contact = resolveVehicleOverlap(player, car.mesh, car.crashed ? 0.5 : 0.68);
        if (contact && !car.hit && Math.abs(playerSpeed) > 2.2) {
          const impact = Math.abs(playerSpeed) + car.speed * 0.72;
          const impulse = forward.clone().multiplyScalar(Math.abs(playerSpeed) * 0.52);
          impulse.x += car.direction * car.speed * 0.38;
          crashCar(car, impact, impulse);
          playerSpeed *= 0.47;
        }
      }

      for (let i = 0; i < traffic.length; i += 1) {
        const a = traffic[i];
        for (let j = i + 1; j < traffic.length; j += 1) {
          const b = traffic[j];
          if (!a.crashed && !b.crashed) continue;

          const contact = resolveVehicleOverlap(a.mesh, b.mesh);
          if (!contact) continue;

          if (a.crashed && !b.hit) {
            const impact = Math.max(a.velocity.length(), 2.5) + b.speed;
            const impulse = a.velocity.clone().multiplyScalar(0.48);
            impulse.x += b.direction * b.speed * 0.45;
            crashCar(b, impact, impulse);
          } else if (b.crashed && !a.hit) {
            const impact = Math.max(b.velocity.length(), 2.5) + a.speed;
            const impulse = b.velocity.clone().multiplyScalar(0.48);
            impulse.x += a.direction * a.speed * 0.45;
            crashCar(a, impact, impulse);
          } else if (a.crashed && b.crashed) {
            const normal = new THREE.Vector3(contact.normal.x, 0, contact.normal.y);
            const closingSpeed = a.velocity.clone().sub(b.velocity).dot(normal);
            if (closingSpeed > 0) {
              a.velocity.addScaledVector(normal, -closingSpeed * 0.52);
              b.velocity.addScaledVector(normal, closingSpeed * 0.52);
            }
          }
        }
      }

      const desiredCamera = player.position.clone().addScaledVector(forward, -6.2);
      desiredCamera.y += 3.2;
      if (shake > 0.005) {
        desiredCamera.x += (Math.random() - 0.5) * shake * 0.55;
        desiredCamera.y += (Math.random() - 0.5) * shake * 0.28;
        shake *= Math.pow(0.88, dt * 60);
      }
      camera.position.copy(desiredCamera);
      camera.lookAt(player.position.x, player.position.y + 0.52, player.position.z);
      renderer.render(scene, camera);

      if (now - lastHudUpdate > 80) {
        setSpeed(Math.round(Math.abs(playerSpeed) * 6.2));
        if (chainValue === 0) setStatus(player.position.z < 7 ? 'COMMIT' : 'APPROACH');
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
  }, [resetGame, setControl]);

  const pointerHandlers = (control: keyof Controls) => ({
    onPointerDown: (event: React.PointerEvent<HTMLButtonElement>) => {
      event.currentTarget.setPointerCapture(event.pointerId);
      setControl(control, true);
    },
    onPointerUp: () => setControl(control, false),
    onPointerCancel: () => setControl(control, false),
    onPointerLeave: () => setControl(control, false),
  });

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

        <div className="hud-glass pointer-events-auto rounded-xl p-1.5">
          <button type="button" onClick={resetGame} className="flex h-9 items-center gap-2 rounded-lg bg-[#f1eee4] px-3 text-xs font-black uppercase tracking-[0.12em] text-[#111318] transition hover:bg-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#ffad44]">
            <RotateCcw className="h-3.5 w-3.5" />
            <span className="hidden sm:inline">Reset</span>
          </button>
        </div>
      </header>

      <section className="pointer-events-none absolute left-1/2 top-[82px] z-10 -translate-x-1/2 text-center sm:top-6">
        <div className="hud-glass min-w-[190px] rounded-xl px-5 py-3 sm:min-w-[240px]">
          <p className="font-mono text-[9px] uppercase tracking-[0.26em] text-[#72d9dd]">Live damage</p>
          <p className="mt-0.5 font-mono text-2xl font-black tabular-nums tracking-[-0.06em] sm:text-3xl">{money(score)}</p>
          <div className="mt-1 flex items-center justify-center gap-2 font-mono text-[9px] uppercase tracking-[0.18em] text-white/50">
            <span className="status-pulse h-1.5 w-1.5 rounded-full bg-[#ff9a3e]" />
            {status}
            {chain > 0 && <span className="text-[#ffb048]">· {chain} vehicles</span>}
          </div>
        </div>
      </section>

      {!hasDriven && (
        <div className="pointer-events-none absolute left-1/2 top-[38%] z-10 w-[min(420px,calc(100%-32px))] -translate-x-1/2 text-center">
          <p className="font-[var(--font-display)] text-[clamp(24px,4vw,48px)] font-black uppercase italic leading-[0.9] tracking-[-0.03em] text-white drop-shadow-[0_4px_24px_rgb(0_0_0/80%)]">
            Thread the gap.<br /><span className="text-[#ff8a35]">Start the wreck.</span>
          </p>
          <p className="mx-auto mt-3 max-w-xs text-xs leading-relaxed text-white/60 sm:text-sm">
            Use WASD or the controls below. Hit cross traffic at speed and turn one impact into a chain reaction.
          </p>
        </div>
      )}

      <aside className="hud-glass pointer-events-none absolute bottom-4 left-1/2 z-20 flex w-[calc(100%-32px)] max-w-3xl -translate-x-1/2 items-end justify-between rounded-2xl px-3 py-3 sm:bottom-6 sm:px-4">
        <div className="pointer-events-auto grid grid-cols-3 gap-2">
          <span />
          <button type="button" aria-label="Accelerate" className="grid h-12 w-14 select-none place-items-center rounded-xl border border-white/10 bg-white/8 font-mono text-xs font-black text-white/85 shadow-inner transition active:scale-95 active:bg-white/18 sm:h-14 sm:w-16" {...pointerHandlers('accelerate')}>▲</button>
          <span />
          <button type="button" aria-label="Steer left" className="grid h-12 w-14 select-none place-items-center rounded-xl border border-white/10 bg-white/8 font-mono text-xs font-black text-white/85 shadow-inner transition active:scale-95 active:bg-white/18 sm:h-14 sm:w-16" {...pointerHandlers('left')}>◀</button>
          <button type="button" aria-label="Brake and reverse" className="grid h-12 w-14 select-none place-items-center rounded-xl border border-white/10 bg-white/8 font-mono text-xs font-black text-white/85 shadow-inner transition active:scale-95 active:bg-white/18 sm:h-14 sm:w-16" {...pointerHandlers('brake')}>▼</button>
          <button type="button" aria-label="Steer right" className="grid h-12 w-14 select-none place-items-center rounded-xl border border-white/10 bg-white/8 font-mono text-xs font-black text-white/85 shadow-inner transition active:scale-95 active:bg-white/18 sm:h-14 sm:w-16" {...pointerHandlers('right')}>▶</button>
        </div>

        <div className="hidden flex-col items-center gap-1 text-center sm:flex">
          <p className="font-mono text-[9px] uppercase tracking-[0.2em] text-white/40">Impact window</p>
          <div className="flex gap-1">
            {[0, 1, 2, 3, 4, 5, 6].map((bar) => (
              <span key={bar} className={`h-1.5 w-5 rounded-full ${bar < Math.min(Math.ceil(speed / 14), 7) ? 'bg-[#ff8a35]' : 'bg-white/10'}`} />
            ))}
          </div>
        </div>

        <div className="pointer-events-auto flex items-center gap-3">
          <div className="text-right">
            <div className="flex items-center justify-end gap-1.5 font-mono text-[9px] uppercase tracking-[0.17em] text-white/45"><Gauge className="h-3 w-3" /> Speed</div>
            <p className="font-mono text-2xl font-black tabular-nums tracking-[-0.06em] sm:text-3xl">
              {String(speed).padStart(3, '0')}<span className="ml-1 text-[9px] tracking-normal text-white/40">MPH</span>
            </p>
          </div>
          <button type="button" aria-label="Accelerate" className="relative grid h-20 w-20 select-none place-items-center overflow-hidden rounded-full border-2 border-[#ff9d3e]/60 bg-[#f4772c]/88 font-[var(--font-display)] text-sm font-black uppercase tracking-[0.09em] text-[#17130f] shadow-[0_0_34px_rgb(244_119_44/30%),inset_0_2px_0_rgb(255_255_255/28%)] transition active:scale-95 active:bg-[#ff9a43] sm:h-24 sm:w-24" {...pointerHandlers('accelerate')}>Go</button>
        </div>
      </aside>
    </main>
  );
}

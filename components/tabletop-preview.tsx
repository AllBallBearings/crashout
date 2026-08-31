'use client';

import { ArrowRight, Camera, RotateCcw, Smartphone } from 'lucide-react';
import { useCallback, useEffect, useRef, useState } from 'react';
import * as THREE from 'three';

type TabletopPreviewProps = {
  onEnterDriveMode: () => void;
};

type XrSessionLike = {
  addEventListener: (type: 'end', listener: () => void) => void;
  end: () => Promise<void>;
};

type XrSystemLike = {
  isSessionSupported?: (mode: 'immersive-ar') => Promise<boolean>;
  requestSession: (mode: 'immersive-ar', options?: Record<string, unknown>) => Promise<XrSessionLike>;
};

const AR_ASSET_URL = './crashout-board.usdz?rev=2';

function makeMiniCar(color: number, position: [number, number], heading: number) {
  const group = new THREE.Group();
  const bodyMaterial = new THREE.MeshStandardMaterial({ color, roughness: 0.36, metalness: 0.42 });
  const glassMaterial = new THREE.MeshStandardMaterial({ color: 0x121a24, roughness: 0.18, metalness: 0.35 });
  const tireMaterial = new THREE.MeshStandardMaterial({ color: 0x080a0c, roughness: 0.92 });

  const body = new THREE.Mesh(new THREE.BoxGeometry(0.9, 0.32, 1.7), bodyMaterial);
  body.position.y = 0.28;
  body.castShadow = true;
  group.add(body);

  const cabin = new THREE.Mesh(new THREE.BoxGeometry(0.68, 0.3, 0.78), glassMaterial);
  cabin.position.set(0, 0.56, 0.08);
  cabin.castShadow = true;
  group.add(cabin);

  const wheelGeometry = new THREE.CylinderGeometry(0.16, 0.16, 0.12, 10);
  for (const x of [-0.49, 0.49]) {
    for (const z of [-0.53, 0.53]) {
      const wheel = new THREE.Mesh(wheelGeometry, tireMaterial);
      wheel.rotation.z = Math.PI / 2;
      wheel.position.set(x, 0.16, z);
      wheel.castShadow = true;
      group.add(wheel);
    }
  }

  group.position.set(position[0], 0, position[1]);
  group.rotation.y = heading;
  return group;
}

function makeTabletopBoard() {
  const board = new THREE.Group();
  const baseMaterial = new THREE.MeshStandardMaterial({ color: 0x242b31, roughness: 0.88 });
  const roadMaterial = new THREE.MeshStandardMaterial({ color: 0x141a20, roughness: 0.9 });
  const curbMaterial = new THREE.MeshStandardMaterial({ color: 0x8b9492, roughness: 0.78 });
  const stripeMaterial = new THREE.MeshStandardMaterial({ color: 0xe7d58d, emissive: 0x6b551b, emissiveIntensity: 0.25 });

  const base = new THREE.Mesh(new THREE.BoxGeometry(14, 0.35, 14), baseMaterial);
  base.position.y = -0.3;
  base.receiveShadow = true;
  board.add(base);

  const verticalRoad = new THREE.Mesh(new THREE.BoxGeometry(5.2, 0.08, 14), roadMaterial);
  verticalRoad.position.y = -0.06;
  verticalRoad.receiveShadow = true;
  board.add(verticalRoad);
  const horizontalRoad = new THREE.Mesh(new THREE.BoxGeometry(14, 0.08, 5.2), roadMaterial);
  horizontalRoad.position.y = -0.05;
  horizontalRoad.receiveShadow = true;
  board.add(horizontalRoad);

  const laneMarkGeometry = new THREE.BoxGeometry(0.1, 0.03, 0.72);
  for (const z of [-6.1, -4.65, 4.65, 6.1]) {
    const mark = new THREE.Mesh(laneMarkGeometry, stripeMaterial);
    mark.position.set(0, 0.02, z);
    board.add(mark);
  }
  const crossMarkGeometry = new THREE.BoxGeometry(0.72, 0.03, 0.1);
  for (const x of [-6.1, -4.65, 4.65, 6.1]) {
    const mark = new THREE.Mesh(crossMarkGeometry, stripeMaterial);
    mark.position.set(x, 0.02, 0);
    board.add(mark);
  }

  for (const [x, z] of [
    [-4.9, -4.9],
    [4.9, -4.9],
    [-4.9, 4.9],
    [4.9, 4.9],
  ] as Array<[number, number]>) {
    const sidewalk = new THREE.Mesh(new THREE.BoxGeometry(3.5, 0.18, 3.5), curbMaterial);
    sidewalk.position.set(x, 0.03, z);
    sidewalk.receiveShadow = true;
    board.add(sidewalk);

    const building = new THREE.Mesh(
      new THREE.BoxGeometry(2.2, 1.9, 2.2),
      new THREE.MeshStandardMaterial({ color: 0x3a4247, roughness: 0.76, metalness: 0.08 }),
    );
    building.position.set(x, 1.08, z);
    building.castShadow = true;
    building.receiveShadow = true;
    board.add(building);
  }

  const traffic = [
    [0xf07736, [-1.5, -5.1], 0],
    [0x46bac5, [1.45, 5.1], Math.PI],
    [0xd94e4c, [-5.1, 1.5], Math.PI / 2],
    [0xe7b43e, [5.1, -1.5], -Math.PI / 2],
    [0x707ed9, [-3.8, 2.1], Math.PI / 2],
  ] as Array<[number, [number, number], number]>;
  traffic.forEach(([color, position, heading]) => board.add(makeMiniCar(color, position, heading)));

  const player = makeMiniCar(0xff6a2b, [0, 2.15], 0);
  player.scale.setScalar(1.08);
  board.add(player);
  return board;
}

export function TabletopPreview({ onEnterDriveMode }: TabletopPreviewProps) {
  const mountRef = useRef<HTMLDivElement>(null);
  const rendererRef = useRef<THREE.WebGLRenderer | null>(null);
  const sceneRef = useRef<THREE.Scene | null>(null);
  const cameraRef = useRef<THREE.PerspectiveCamera | null>(null);
  const boardRef = useRef<THREE.Group | null>(null);
  const xrSessionRef = useRef<XrSessionLike | null>(null);
  const xrActiveRef = useRef(false);
  const [webXrAvailable, setWebXrAvailable] = useState(false);
  const [arStatus, setArStatus] = useState<'idle' | 'starting' | 'active' | 'error'>('idle');
  const [arMessage, setArMessage] = useState('');

  useEffect(() => {
    const navigatorWithXr = navigator as Navigator & { xr?: XrSystemLike };
    const xr = navigatorWithXr.xr;
    if (!xr) return;
    if (!xr.isSessionSupported) {
      window.setTimeout(() => setWebXrAvailable(true), 0);
      return;
    }
    xr.isSessionSupported('immersive-ar').then(setWebXrAvailable).catch(() => setWebXrAvailable(false));
  }, []);

  useEffect(() => {
    const mount = mountRef.current;
    if (!mount) return;

    const scene = new THREE.Scene();
    const camera = new THREE.PerspectiveCamera(36, 1, 0.1, 100);
    const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true, powerPreference: 'high-performance' });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 1.8));
    renderer.outputColorSpace = THREE.SRGBColorSpace;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.14;
    renderer.setClearColor(0x000000, 0);
    renderer.xr.enabled = false;
    mount.appendChild(renderer.domElement);
    rendererRef.current = renderer;
    sceneRef.current = scene;
    cameraRef.current = camera;

    scene.add(new THREE.HemisphereLight(0xbad8ee, 0x31231a, 2.2));
    const keyLight = new THREE.DirectionalLight(0xffd2a4, 3.8);
    keyLight.position.set(-8, 14, 10);
    keyLight.castShadow = true;
    scene.add(keyLight);
    const fillLight = new THREE.PointLight(0x52ced4, 4, 18, 2);
    fillLight.position.set(4, 5, 4);
    scene.add(fillLight);

    const board = makeTabletopBoard();
    board.scale.setScalar(0.42);
    boardRef.current = board;
    scene.add(board);

    const orbit = { yaw: 0.72, pitch: 0.56, distance: 14, dragging: false, lastX: 0, lastY: 0 };
    const target = new THREE.Vector3(0, 0.35, 0);
    const handleResize = () => {
      const width = mount.clientWidth;
      const height = mount.clientHeight;
      renderer.setSize(width, height);
      camera.aspect = Math.max(width / Math.max(height, 1), 0.7);
      camera.updateProjectionMatrix();
    };
    handleResize();
    const resizeObserver = new ResizeObserver(handleResize);
    resizeObserver.observe(mount);

    const updateCamera = () => {
      const horizontal = Math.cos(orbit.pitch) * orbit.distance;
      camera.position.set(
        target.x + Math.sin(orbit.yaw) * horizontal,
        target.y + Math.sin(orbit.pitch) * orbit.distance,
        target.z + Math.cos(orbit.yaw) * horizontal,
      );
      camera.lookAt(target);
    };
    updateCamera();

    const onPointerDown = (event: PointerEvent) => {
      orbit.dragging = true;
      orbit.lastX = event.clientX;
      orbit.lastY = event.clientY;
      mount.setPointerCapture(event.pointerId);
    };
    const onPointerMove = (event: PointerEvent) => {
      if (!orbit.dragging || xrActiveRef.current) return;
      orbit.yaw -= (event.clientX - orbit.lastX) * 0.008;
      orbit.pitch = THREE.MathUtils.clamp(orbit.pitch + (event.clientY - orbit.lastY) * 0.006, 0.18, 1.25);
      orbit.lastX = event.clientX;
      orbit.lastY = event.clientY;
      updateCamera();
    };
    const onPointerUp = (event: PointerEvent) => {
      orbit.dragging = false;
      if (mount.hasPointerCapture(event.pointerId)) mount.releasePointerCapture(event.pointerId);
    };
    const onWheel = (event: WheelEvent) => {
      if (xrActiveRef.current) return;
      event.preventDefault();
      orbit.distance = THREE.MathUtils.clamp(orbit.distance + event.deltaY * 0.012, 8, 22);
      updateCamera();
    };
    mount.addEventListener('pointerdown', onPointerDown);
    mount.addEventListener('pointermove', onPointerMove);
    mount.addEventListener('pointerup', onPointerUp);
    mount.addEventListener('pointercancel', onPointerUp);
    mount.addEventListener('wheel', onWheel, { passive: false });

    let animationFrame = 0;
    const animate = () => {
      if (!xrActiveRef.current) {
        if (!orbit.dragging) orbit.yaw += 0.0008;
        updateCamera();
        renderer.render(scene, camera);
      }
      animationFrame = requestAnimationFrame(animate);
    };
    animate();

    return () => {
      cancelAnimationFrame(animationFrame);
      resizeObserver.disconnect();
      mount.removeEventListener('pointerdown', onPointerDown);
      mount.removeEventListener('pointermove', onPointerMove);
      mount.removeEventListener('pointerup', onPointerUp);
      mount.removeEventListener('pointercancel', onPointerUp);
      mount.removeEventListener('wheel', onWheel);
      const session = xrSessionRef.current;
      if (session) void session.end().catch(() => undefined);
      renderer.setAnimationLoop(null);
      rendererRef.current = null;
      sceneRef.current = null;
      cameraRef.current = null;
      boardRef.current = null;
      scene.traverse((object) => {
        if (object instanceof THREE.Mesh) {
          object.geometry.dispose();
          const materials = Array.isArray(object.material) ? object.material : [object.material];
          materials.forEach((material) => material.dispose());
        }
      });
      renderer.dispose();
      renderer.domElement.remove();
    };
  }, []);

  const endWebXr = useCallback(() => {
    const session = xrSessionRef.current;
    if (session) void session.end().catch(() => undefined);
  }, []);

  const startWebXr = useCallback(async () => {
    const navigatorWithXr = navigator as Navigator & { xr?: XrSystemLike };
    const xr = navigatorWithXr.xr;
    if (!xr) {
      setArStatus('error');
      setArMessage('This browser does not expose WebXR. Use AR VIEW for iPhone/iPad surface placement.');
      return;
    }
    const renderer = rendererRef.current;
    const scene = sceneRef.current;
    const camera = cameraRef.current;
    const board = boardRef.current;
    if (!renderer || !scene || !camera || !board) return;

    setArStatus('starting');
    setArMessage('Point your device at a clear surface…');
    try {
      const session = await xr.requestSession('immersive-ar', {
        requiredFeatures: ['local-floor'],
        optionalFeatures: ['hit-test', 'dom-overlay'],
        domOverlay: { root: document.body },
      });
      renderer.xr.enabled = true;
      renderer.xr.setReferenceSpaceType('local-floor');
      await renderer.xr.setSession(session as Parameters<typeof renderer.xr.setSession>[0]);
      // Keep the WebXR board at the same tabletop scale as Quick Look.
      board.scale.setScalar(0.1);
      xrSessionRef.current = session;
      xrActiveRef.current = true;
      session.addEventListener('end', () => {
        xrActiveRef.current = false;
        xrSessionRef.current = null;
        board.scale.setScalar(0.42);
        renderer.setAnimationLoop(null);
        setArStatus('idle');
        setArMessage('');
      });
      renderer.setAnimationLoop(() => renderer.render(scene, camera));
      setArStatus('active');
      setArMessage('AR view active. Walk around the board from a distance.');
    } catch {
      setArStatus('error');
      setArMessage('AR could not start here. Use AR VIEW on iPhone/iPad or continue with the tabletop preview.');
    }
  }, []);

  return (
    <section className="absolute inset-0 z-40 overflow-hidden bg-[#07090d] text-[#f6f3e9]" aria-label="Tabletop and AR preview">
      <div ref={mountRef} className="absolute inset-0 cursor-grab touch-none active:cursor-grabbing" aria-label="Interactive 3D tabletop preview" />
      <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_50%_42%,transparent_16%,rgb(5_8_12/28%)_70%,rgb(5_8_12/86%)_100%)]" />

      <div className="pointer-events-none absolute inset-x-0 top-0 z-10 flex items-start justify-between p-4 sm:p-6">
        <div className="hud-glass pointer-events-auto rounded-xl px-4 py-3 sm:px-5">
          <p className="font-mono text-[9px] font-bold uppercase tracking-[0.25em] text-[#72d9dd]">Crashout / tabletop</p>
          <h1 className="mt-1 font-[var(--font-display)] text-2xl font-black uppercase italic tracking-[-0.04em] text-white sm:text-4xl">Place the junction.</h1>
          <p className="mt-2 max-w-sm text-xs leading-relaxed text-white/55 sm:text-sm">Preview the crash board in 3D, place it in your space, or jump straight into the drive.</p>
        </div>
        <div className="hud-glass pointer-events-auto hidden items-center gap-2 rounded-xl px-3 py-2.5 sm:flex">
          <RotateCcw className="size-3.5 text-[#72d9dd]" />
          <span className="font-mono text-[9px] uppercase tracking-[0.16em] text-white/55">Drag to orbit · scroll to zoom</span>
        </div>
      </div>

      <div className="pointer-events-none absolute inset-x-0 bottom-0 z-10 flex flex-col items-center gap-3 px-4 pb-5 sm:pb-8">
        <p className="font-mono text-[10px] uppercase tracking-[0.2em] text-white/45">Choose how you want to crash</p>
        <div className="pointer-events-auto flex w-full max-w-xl flex-col gap-2 sm:flex-row">
          {/* This is an external AR Quick Look asset, not a Next.js route. */}
          {/* oxlint-disable-next-line next/no-html-link-for-pages */}
          <a
            rel="ar"
            href={AR_ASSET_URL}
            className="inline-flex h-12 flex-1 items-center justify-center gap-2 rounded-xl border border-[#72d9dd]/60 bg-[#72d9dd] px-4 font-mono text-[11px] font-black uppercase tracking-[0.14em] text-[#071214] shadow-[0_12px_40px_rgb(0_0_0/38%)] transition hover:bg-[#a1eef0] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#d7ffff]"
          >
            {/* oxlint-disable-next-line next/no-img-element */}
            <img src="./favicon.svg" alt="" className="size-6 rounded-md" />
            AR view
          </a>
          {webXrAvailable && (
            <button
              type="button"
              onClick={arStatus === 'active' ? endWebXr : startWebXr}
              className="inline-flex h-12 flex-1 items-center justify-center gap-2 rounded-xl border border-white/15 bg-white/10 px-4 font-mono text-[11px] font-bold uppercase tracking-[0.14em] text-white transition hover:bg-white/15 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#72d9dd]/70"
            >
              <Camera className="size-4" />
              {arStatus === 'active' ? 'Exit WebXR' : arStatus === 'starting' ? 'Starting AR…' : 'Try WebXR'}
            </button>
          )}
          <button
            type="button"
            onClick={onEnterDriveMode}
            className="inline-flex h-12 flex-1 items-center justify-center gap-2 rounded-xl border border-[#ff9a3e]/70 bg-[#ff9a3e] px-4 font-mono text-[11px] font-black uppercase tracking-[0.14em] text-[#1d1108] shadow-[0_12px_40px_rgb(0_0_0/38%)] transition hover:bg-[#ffb36b] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#ffd0a3]"
          >
            Drive mode
            <ArrowRight className="size-4" />
          </button>
        </div>
        <div className="flex items-center gap-2 text-center font-mono text-[9px] uppercase tracking-[0.15em] text-white/35">
          <Smartphone className="size-3" />
          <span>AR VIEW places a static board preview · close it to return · Drive mode has touch controls</span>
        </div>
        {arMessage && <p className="max-w-xl text-center text-xs text-[#ffb46b]">{arMessage}</p>}
      </div>
    </section>
  );
}

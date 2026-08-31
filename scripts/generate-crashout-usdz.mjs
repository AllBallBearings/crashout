import { mkdir, writeFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import * as THREE from 'three';
import { USDZExporter } from 'three/examples/jsm/exporters/USDZExporter.js';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const outputPath = resolve(root, 'public/crashout-board.usdz');

function makeCar(color, x, z, heading) {
  const group = new THREE.Group();
  const body = new THREE.Mesh(
    new THREE.BoxGeometry(0.9, 0.32, 1.7),
    new THREE.MeshStandardMaterial({ color, roughness: 0.38, metalness: 0.4 }),
  );
  body.position.y = 0.28;
  group.add(body);

  const cabin = new THREE.Mesh(
    new THREE.BoxGeometry(0.68, 0.3, 0.78),
    new THREE.MeshStandardMaterial({ color: 0x121a24, roughness: 0.2, metalness: 0.35 }),
  );
  cabin.position.set(0, 0.56, 0.08);
  group.add(cabin);

  const wheelMaterial = new THREE.MeshStandardMaterial({ color: 0x080a0c, roughness: 0.92 });
  for (const wheelX of [-0.49, 0.49]) {
    for (const wheelZ of [-0.53, 0.53]) {
      const wheel = new THREE.Mesh(new THREE.CylinderGeometry(0.16, 0.16, 0.12, 10), wheelMaterial);
      wheel.rotation.z = Math.PI / 2;
      wheel.position.set(wheelX, 0.16, wheelZ);
      group.add(wheel);
    }
  }

  group.position.set(x, 0, z);
  group.rotation.y = heading;
  return group;
}

function makeBoard() {
  const board = new THREE.Group();
  const baseMaterial = new THREE.MeshStandardMaterial({ color: 0x242b31, roughness: 0.88 });
  const roadMaterial = new THREE.MeshStandardMaterial({ color: 0x141a20, roughness: 0.9 });
  const curbMaterial = new THREE.MeshStandardMaterial({ color: 0x8b9492, roughness: 0.78 });
  const stripeMaterial = new THREE.MeshStandardMaterial({ color: 0xe7d58d, emissive: 0x6b551b, emissiveIntensity: 0.25 });

  const base = new THREE.Mesh(new THREE.BoxGeometry(14, 0.35, 14), baseMaterial);
  base.position.y = -0.3;
  board.add(base);

  const verticalRoad = new THREE.Mesh(new THREE.BoxGeometry(5.2, 0.08, 14), roadMaterial);
  verticalRoad.position.y = -0.06;
  board.add(verticalRoad);
  const horizontalRoad = new THREE.Mesh(new THREE.BoxGeometry(14, 0.08, 5.2), roadMaterial);
  horizontalRoad.position.y = -0.05;
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

  for (const [x, z] of [[-4.9, -4.9], [4.9, -4.9], [-4.9, 4.9], [4.9, 4.9]]) {
    const sidewalk = new THREE.Mesh(new THREE.BoxGeometry(3.5, 0.18, 3.5), curbMaterial);
    sidewalk.position.set(x, 0.03, z);
    board.add(sidewalk);
    const building = new THREE.Mesh(
      new THREE.BoxGeometry(2.2, 1.9, 2.2),
      new THREE.MeshStandardMaterial({ color: 0x3a4247, roughness: 0.76, metalness: 0.08 }),
    );
    building.position.set(x, 1.08, z);
    board.add(building);
  }

  board.add(makeCar(0xf07736, -1.5, -5.1, 0));
  board.add(makeCar(0x46bac5, 1.45, 5.1, Math.PI));
  board.add(makeCar(0xd94e4c, -5.1, 1.5, Math.PI / 2));
  board.add(makeCar(0xe7b43e, 5.1, -1.5, -Math.PI / 2));
  board.add(makeCar(0x707ed9, -3.8, 2.1, Math.PI / 2));
  board.add(makeCar(0xff6a2b, 0, 2.15, 0));
  return board;
}

const scene = new THREE.Scene();
const board = makeBoard();
// Author the Quick Look scene at tabletop scale: a 1.4 m board with cars
// approximately 15 cm long (about twice Hot Wheels / Matchbox scale).
board.scale.setScalar(0.1);
board.position.y = 0.03;
scene.add(board);
const exporter = new USDZExporter();
const buffer = await exporter.parseAsync(scene, {
  quickLookCompatible: true,
  includeAnchoringProperties: true,
  ar: {
    anchoring: { type: 'plane' },
    planeAnchoring: { alignment: 'horizontal' },
  },
});

await mkdir(dirname(outputPath), { recursive: true });
await writeFile(outputPath, Buffer.from(buffer));
console.log(`Wrote ${outputPath} (${buffer.byteLength} bytes)`);

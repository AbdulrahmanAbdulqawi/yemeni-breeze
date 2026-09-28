import {
  AmbientLight,
  BufferGeometry,
  Color,
  DirectionalLight,
  DynamicDrawUsage,
  ExtrudeGeometry,
  Fog,
  Group,
  InstancedMesh,
  MeshStandardMaterial,
  Object3D,
  PerspectiveCamera,
  PointLight,
  Scene,
  Shape,
  WebGLRenderer
} from 'three';

/*
 * "Qamariya breeze" — the Home hero's 3D layer. Pieces of the logo's
 * stained-glass arch (triangles, diamonds, petals, arch wedges) drift upward
 * through the hero like glass carried on a breeze, lit by a warm light that
 * follows the pointer.
 *
 * Kept deliberately cheap: four InstancedMeshes (one draw call per shard
 * shape), a single shared material, no textures or post-processing. This
 * module is only ever loaded through a dynamic import() so three.js lands in
 * its own lazy chunk and never touches the initial bundle.
 */

/** Glass colours sampled from the logo, weighted toward its warm tones. */
const GLASS = ['#da4b3a', '#ed9e42', '#edcb4e', '#3bab4b', '#2b2be0', '#da4b3a', '#ed9e42', '#edcb4e', '#8f1b04'];
/** Matches --yb-brown, so distant shards fade into the hero background. */
const HERO_BROWN = 0x310f02;

const CAMERA_Z = 9;
const HALF_FOV_TAN = Math.tan((42 / 2) * (Math.PI / 180));
/** Near shards stay out of this central band (normalized x) so the hero copy stays readable. */
const TEXT_BAND = 0.5;
const NEAR_Z = -3;

interface Shard {
  mesh: number;
  slot: number;
  nx: number;
  ny: number;
  z: number;
  rot: [number, number, number];
  spin: [number, number, number];
  rise: number;
  drift: number;
  sway: number;
  phase: number;
  scale: number;
  delay: number;
}

function triangle(): Shape {
  const s = new Shape();
  s.moveTo(0, 0.62);
  s.lineTo(-0.54, -0.31);
  s.lineTo(0.54, -0.31);
  s.closePath();
  return s;
}

function diamond(): Shape {
  const s = new Shape();
  s.moveTo(0, 0.72);
  s.lineTo(0.42, 0);
  s.lineTo(0, -0.72);
  s.lineTo(-0.42, 0);
  s.closePath();
  return s;
}

function petal(): Shape {
  const s = new Shape();
  s.moveTo(0, -0.72);
  s.quadraticCurveTo(0.55, 0, 0, 0.72);
  s.quadraticCurveTo(-0.55, 0, 0, -0.72);
  return s;
}

/** A segment of the arch's outer ring. */
function wedge(): Shape {
  const a0 = Math.PI * 0.36;
  const a1 = Math.PI * 0.64;
  const s = new Shape();
  s.absarc(0, 0, 1.05, a0, a1, false);
  s.absarc(0, 0, 0.62, a1, a0, true);
  s.closePath();
  return s;
}

function glassGeometry(shape: Shape): BufferGeometry {
  const geometry = new ExtrudeGeometry(shape, {
    depth: 0.07,
    bevelEnabled: true,
    bevelThickness: 0.025,
    bevelSize: 0.025,
    bevelSegments: 2,
    curveSegments: 10
  });
  geometry.center();
  return geometry;
}

const rand = (min: number, max: number) => min + Math.random() * (max - min);

export interface QamariyaSceneOptions {
  /** Fewer shards for small screens. */
  compact: boolean;
  /** Mirror the breeze direction for right-to-left pages. */
  rtl: boolean;
}

export class QamariyaScene {
  private readonly renderer: WebGLRenderer;
  private readonly scene = new Scene();
  private readonly camera = new PerspectiveCamera(42, 1, 0.1, 40);
  private readonly root = new Group();
  private readonly glow = new PointLight(0xffd9a0, 22, 0, 2);
  private readonly material: MeshStandardMaterial;
  private readonly geometries: BufferGeometry[];
  private readonly meshes: InstancedMesh[];
  private readonly shards: Shard[] = [];
  private readonly dummy = new Object3D();
  private readonly direction: number;

  private aspect = 1;
  private time = 0;
  private intro = 0;
  private lastFrame = 0;
  private pointer = { x: 0, y: 0 };
  private smoothPointer = { x: 0, y: 0 };
  private scroll = 0;
  private smoothScroll = 0;

  /** Throws if WebGL is unavailable — callers treat that as "no 3D layer". */
  constructor(canvas: HTMLCanvasElement, options: QamariyaSceneOptions) {
    this.direction = options.rtl ? -1 : 1;

    this.renderer = new WebGLRenderer({ canvas, alpha: true, antialias: true, powerPreference: 'low-power' });
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    this.renderer.setClearColor(0x000000, 0);

    this.scene.fog = new Fog(HERO_BROWN, 7, 19);
    this.camera.position.set(0, 0, CAMERA_Z);
    this.scene.add(this.root);

    this.scene.add(new AmbientLight(0xfff1d6, 0.9));
    const key = new DirectionalLight(0xffe7b8, 1.8);
    key.position.set(4, 6, 5);
    const rim = new DirectionalLight(0x9fb4ff, 0.6);
    rim.position.set(-5, -3, -4);
    this.glow.position.set(0, 0, 2.5);
    this.scene.add(key, rim, this.glow);

    this.material = new MeshStandardMaterial({
      color: 0xffffff,
      roughness: 0.3,
      metalness: 0.05,
      transparent: true,
      opacity: 0.9,
      emissive: 0xffffff,
      emissiveIntensity: 0.3
    });
    // Tint the emissive term by each instance's colour so every shard glows
    // in its own glass colour, like light through a qamariya window.
    this.material.onBeforeCompile = shader => {
      shader.fragmentShader = shader.fragmentShader.replace(
        '#include <emissivemap_fragment>',
        '#include <emissivemap_fragment>\n\ttotalEmissiveRadiance *= vColor.rgb;'
      );
    };

    this.geometries = [triangle(), diamond(), petal(), wedge()].map(glassGeometry);
    const count = options.compact ? 34 : 64;
    const perMesh = this.geometries.map(() => 0);
    for (let i = 0; i < count; i++) {
      const mesh = i % this.geometries.length;
      this.shards.push(this.createShard(mesh, perMesh[mesh]++, options.compact));
    }

    const color = new Color();
    this.meshes = this.geometries.map((geometry, m) => {
      const mesh = new InstancedMesh(geometry, this.material, perMesh[m]);
      mesh.instanceMatrix.setUsage(DynamicDrawUsage);
      mesh.frustumCulled = false;
      this.root.add(mesh);
      return mesh;
    });
    for (const shard of this.shards) {
      this.meshes[shard.mesh].setColorAt(shard.slot, color.set(GLASS[Math.floor(Math.random() * GLASS.length)]));
    }
  }

  private createShard(mesh: number, slot: number, compact: boolean): Shard {
    // On phones the copy spans the full width, so keep the glass further back.
    const z = rand(-9, compact ? -1 : 2);
    const shard: Shard = {
      mesh,
      slot,
      nx: 0,
      ny: rand(-1.2, 1.2),
      z,
      rot: [rand(0, Math.PI * 2), rand(0, Math.PI * 2), rand(0, Math.PI * 2)],
      spin: [rand(-0.35, 0.35), rand(-0.45, 0.45), rand(-0.25, 0.25)],
      rise: rand(0.018, 0.045),
      drift: z < NEAR_Z ? rand(0.006, 0.018) : 0,
      sway: rand(0.015, 0.04),
      phase: rand(0, Math.PI * 2),
      scale: rand(0.32, 0.72) * (z < NEAR_Z ? 1.25 : 1),
      delay: rand(0, 0.55)
    };
    shard.nx = this.pickX(z);
    return shard;
  }

  private pickX(z: number): number {
    if (z < NEAR_Z) return rand(-1.2, 1.2);
    return (Math.random() < 0.5 ? -1 : 1) * rand(TEXT_BAND, 1.15);
  }

  setSize(width: number, height: number) {
    if (!width || !height) return;
    this.aspect = width / height;
    this.camera.aspect = this.aspect;
    this.camera.updateProjectionMatrix();
    this.renderer.setSize(width, height, false);
  }

  /** Pointer position in -1..1 (y up). */
  setPointer(x: number, y: number) {
    this.pointer.x = x;
    this.pointer.y = y;
  }

  /** How far the hero has been scrolled out of view, 0..1. */
  setScroll(progress: number) {
    this.scroll = Math.min(Math.max(progress, 0), 1);
  }

  /** Renders one fully-settled frame, for reduced motion (no loop). */
  renderStill() {
    this.intro = 1;
    this.smoothScroll = this.scroll;
    this.update(0);
    this.renderer.render(this.scene, this.camera);
  }

  start() {
    this.lastFrame = 0;
    this.renderer.setAnimationLoop(now => {
      const dt = this.lastFrame ? Math.min((now - this.lastFrame) / 1000, 0.1) : 0;
      this.lastFrame = now;
      this.update(dt);
      this.renderer.render(this.scene, this.camera);
    });
  }

  stop() {
    this.renderer.setAnimationLoop(null);
  }

  dispose() {
    this.stop();
    this.meshes.forEach(mesh => mesh.dispose());
    this.geometries.forEach(geometry => geometry.dispose());
    this.material.dispose();
    this.renderer.dispose();
    this.renderer.forceContextLoss();
  }

  private halfHeight(z: number) {
    return (CAMERA_Z - z) * HALF_FOV_TAN;
  }

  private update(dt: number) {
    this.time += dt;
    this.intro = Math.min(this.intro + dt / 1.8, 1);
    const ease = 1 - Math.pow(1 - this.intro, 3);

    const follow = 1 - Math.pow(0.04, dt);
    this.smoothPointer.x += (this.pointer.x - this.smoothPointer.x) * follow;
    this.smoothPointer.y += (this.pointer.y - this.smoothPointer.y) * follow;
    this.smoothScroll += (this.scroll - this.smoothScroll) * (1 - Math.pow(0.01, dt));

    // Camera leans with the pointer and pushes into the glass as the hero scrolls away.
    const p = this.smoothPointer;
    this.camera.position.set(p.x * 0.7, p.y * 0.45, CAMERA_Z - this.smoothScroll * 2.6);
    this.camera.lookAt(0, 0, -2);
    this.root.rotation.z = this.smoothScroll * 0.22 * this.direction;

    const glowZ = 2.5;
    const glowHalfH = this.halfHeight(glowZ);
    this.glow.position.set(p.x * glowHalfH * this.aspect, p.y * glowHalfH, glowZ);

    for (const s of this.shards) {
      s.ny += s.rise * dt;
      s.nx += s.drift * dt * this.direction;
      if (s.ny > 1.25) {
        s.ny = -1.25;
        s.nx = this.pickX(s.z);
      }
      if (s.nx > 1.3) s.nx = -1.3;
      else if (s.nx < -1.3) s.nx = 1.3;

      s.rot[0] += s.spin[0] * dt;
      s.rot[1] += s.spin[1] * dt;
      s.rot[2] += s.spin[2] * dt;

      const halfH = this.halfHeight(s.z);
      const nx = s.nx + Math.sin(this.time * 0.35 + s.phase) * s.sway;
      const grow = Math.min(Math.max(ease * 1.55 - s.delay, 0), 1);

      this.dummy.position.set(nx * halfH * this.aspect, s.ny * halfH, s.z);
      this.dummy.rotation.set(s.rot[0], s.rot[1], s.rot[2]);
      this.dummy.scale.setScalar(s.scale * grow);
      this.dummy.updateMatrix();
      this.meshes[s.mesh].setMatrixAt(s.slot, this.dummy.matrix);
    }
    for (const mesh of this.meshes) mesh.instanceMatrix.needsUpdate = true;
  }
}

"""
From-scratch Blender 5.2 automotive construction system for Crashout.

Coordinate convention inside Blender:
  X = right (+X), left (-X)
  Y = forward (+Y nose, -Y tail)
  Z = up (+Z, road surface at Z = 0)

Exported COV2 binary format (Unity):
  (x, y, z)_Unity = (x, z + ground, y)_Blender
  Triangle winding reversed (i0, i2, i1) to preserve outward normals across handedness flip.
"""

from __future__ import annotations

import math
import struct
from dataclasses import dataclass
from pathlib import Path
from typing import Callable, Iterable

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector

ROOT = Path(__file__).resolve().parents[2]
ART_DIR = ROOT / "Art" / "Vehicles"
OUT_DIR = ROOT / "Unity" / "Assets" / "Crashout" / "Resources" / "Vehicles"

ART_DIR.mkdir(parents=True, exist_ok=True)
OUT_DIR.mkdir(parents=True, exist_ok=True)


# ---------------------------------------------------------------------------
# Spline / interpolation helpers

def clamp(x: float, lo: float, hi: float) -> float:
    return lo if x < lo else (hi if x > hi else x)


def smoothstep(t: float) -> float:
    t = clamp(t, 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def lerp(a: float, b: float, t: float) -> float:
    return a + (b - a) * t


def eval_curve(keys: list[tuple[float, float]], y: float) -> float:
    """Piecewise smoothstep interpolation over sorted (y, value) keyframes."""
    if y <= keys[0][0]:
        return keys[0][1]
    for i in range(1, len(keys)):
        y0, v0 = keys[i - 1]
        y1, v1 = keys[i]
        if y <= y1:
            t = (y - y0) / max(1e-6, y1 - y0)
            return lerp(v0, v1, smoothstep(t))
    return keys[-1][1]


# ---------------------------------------------------------------------------
# Scene & Material Management

class VehicleBuilder:
    """Builds one vehicle inside its own Blender collection with crash-ready detachable parts."""

    def __init__(self, kind: str, paint_rgb: tuple[float, float, float], paint_metal: float = 0.72,
                 paint_rough: float = 0.16, paint_coat: float = 1.0,
                 accent_rgb: tuple[float, float, float] = (0.10, 0.34, 0.64),
                 wheel_rgb: tuple[float, float, float] = (0.28, 0.27, 0.25)):
        self.kind = kind
        self.coll = bpy.data.collections.new(kind)
        bpy.context.scene.collection.children.link(self.coll)
        self.objects: list[bpy.types.Object] = []
        self.mats: dict[str, bpy.types.Material] = {}
        self._init_materials(paint_rgb, paint_metal, paint_rough, paint_coat, accent_rgb, wheel_rgb)

    def _make_mat(self, name: str, color: tuple[float, float, float], metal: float = 0.0,
                  rough: float = 0.4, coat: float = 0.0, alpha: float = 1.0,
                  emit: tuple[float, float, float] | None = None, emit_str: float = 0.0) -> bpy.types.Material:
        mat = bpy.data.materials.new(name=f"{self.kind}_{name}")
        mat.use_nodes = True
        bsdf = mat.node_tree.nodes.get("Principled BSDF")
        bsdf.inputs["Base Color"].default_value = (*color, 1.0)
        bsdf.inputs["Metallic"].default_value = metal
        bsdf.inputs["Roughness"].default_value = rough
        if "Coat Weight" in bsdf.inputs:
            bsdf.inputs["Coat Weight"].default_value = coat
            bsdf.inputs["Coat Roughness"].default_value = 0.035
        if alpha < 1.0:
            bsdf.inputs["Alpha"].default_value = alpha
            if hasattr(mat, "blend_method"):
                mat.blend_method = "BLEND"
        if emit is not None and emit_str > 0.0:
            bsdf.inputs["Emission Color"].default_value = (*emit, 1.0)
            bsdf.inputs["Emission Strength"].default_value = emit_str
        return mat

    def _init_materials(self, paint_rgb, paint_metal, paint_rough, paint_coat, accent_rgb, wheel_rgb):
        m = self.mats
        m["paint"] = self._make_mat("paint", paint_rgb, metal=paint_metal, rough=paint_rough, coat=paint_coat)
        m["framePaint"] = self._make_mat("framePaint", paint_rgb, metal=paint_metal, rough=paint_rough, coat=paint_coat)
        m["matteBlack"] = self._make_mat("matteBlack", (0.042, 0.045, 0.052), metal=0.22, rough=0.46, coat=0.10)
        m["accent"] = self._make_mat("accent", accent_rgb, metal=0.35, rough=0.20, coat=0.88)
        m["gold"] = self._make_mat("gold", (0.88, 0.48, 0.06), metal=0.2, rough=0.14, coat=0.8)
        # Smoked automotive glass with glossy sky reflections
        m["glass"] = self._make_mat("glass", (0.02, 0.032, 0.045), metal=0.18, rough=0.03, coat=1.0, alpha=0.76)
        m["trim"] = self._make_mat("trim", (0.028, 0.030, 0.034), metal=0.15, rough=0.40, coat=0.12)
        m["underbody"] = self._make_mat("underbody", (0.022, 0.023, 0.027), metal=0.25, rough=0.72)
        m["interior"] = self._make_mat("interior", (0.038, 0.040, 0.044), metal=0.0, rough=0.82)
        m["chrome"] = self._make_mat("chrome", (0.94, 0.95, 0.97), metal=1.0, rough=0.05)
        m["steel"] = self._make_mat("steel", (0.58, 0.61, 0.65), metal=0.92, rough=0.26)
        m["headlight"] = self._make_mat("headlight", (0.96, 0.98, 1.0), metal=0.1, rough=0.04, coat=1.0,
                                        emit=(1.0, 0.96, 0.88), emit_str=6.0)
        m["tail"] = self._make_mat("tail", (0.60, 0.02, 0.02), metal=0.0, rough=0.06, coat=0.95,
                                   emit=(1.0, 0.05, 0.03), emit_str=4.5)
        m["indicator"] = self._make_mat("indicator", (0.82, 0.38, 0.02), metal=0.0, rough=0.08,
                                        emit=(1.0, 0.52, 0.04), emit_str=2.5)
        m["plate"] = self._make_mat("plate", (0.88, 0.88, 0.85), metal=0.1, rough=0.35)
        m["preview_rubber"] = self._make_mat("preview_rubber", (0.032, 0.033, 0.037), metal=0.0, rough=0.76)
        m["preview_rim"] = self._make_mat("preview_rim", wheel_rgb, metal=0.94, rough=0.16, coat=0.35)
        m["preview_caliper"] = self._make_mat("preview_caliper", (0.80, 0.06, 0.05), metal=0.45, rough=0.22, coat=0.85)

    def register(self, name: str, mesh: bpy.types.Mesh, part: str, role: str,
                 export: bool = True, smooth: bool = True,
                 solidify: float = 0.0, solidify_offset: float = -1.0,
                 bevel: float = 0.0, bevel_segs: int = 2,
                 subdiv: int = 0) -> bpy.types.Object:
        ob = bpy.data.objects.new(f"{self.kind}_{name}", mesh)
        self.coll.objects.link(ob)
        mat = self.mats.get(role, self.mats["trim"])
        ob.data.materials.append(mat)
        ob["part"] = part
        ob["role"] = role
        ob["export"] = bool(export)

        if smooth:
            mesh.polygons.foreach_set("use_smooth", [True] * len(mesh.polygons))

        if bevel > 0.0:
            mod = ob.modifiers.new("Bevel", "BEVEL")
            mod.width = bevel
            mod.segments = bevel_segs
            mod.limit_method = "ANGLE"
            mod.angle_limit = math.radians(32.0)
            mod.harden_normals = False

        if solidify > 0.0:
            mod = ob.modifiers.new("Solidify", "SOLIDIFY")
            mod.thickness = solidify
            mod.offset = solidify_offset
            mod.use_rim = True
            mod.use_even_offset = False

        if subdiv > 0:
            mod = ob.modifiers.new("Subsurf", "SUBSURF")
            mod.levels = subdiv
            mod.render_levels = subdiv

        self.objects.append(ob)
        return ob

    # ------------------------------------------------------------------
    # Low-level clean mesh builders

    def add_grid_surface(self, name: str, grid: list[list[tuple[float, float, float]]],
                         outward: Vector | Callable[[Vector], Vector],
                         part: str, role: str,
                         solidify: float = 0.0, bevel: float = 0.0,
                         subdiv: int = 0, crease_edges: bool = True,
                         export: bool = True) -> bpy.types.Object:
        """Creates a quad-grid surface mesh from rows x cols of (x, y, z) points.
        Automatically orients face winding so normals match `outward`."""
        rows = len(grid)
        cols = len(grid[0])
        assert rows >= 2 and cols >= 2

        bm = bmesh.new()
        verts = [[bm.verts.new(grid[r][c]) for c in range(cols)] for r in range(rows)]
        bm.verts.ensure_lookup_table()

        faces = []
        for r in range(rows - 1):
            for c in range(cols - 1):
                v00 = verts[r][c]
                v10 = verts[r + 1][c]
                v11 = verts[r + 1][c + 1]
                v01 = verts[r][c + 1]
                if (v10.co - v00.co).length < 1e-6 and (v11.co - v01.co).length < 1e-6:
                    continue
                try:
                    f = bm.faces.new((v00, v10, v11, v01))
                    faces.append(f)
                except ValueError:
                    pass

        bm.normal_update()
        dot_sum = 0.0
        for f in faces:
            c_pos = f.calc_center_median()
            target = outward(c_pos) if callable(outward) else outward
            dot_sum += f.normal.dot(target)
        if dot_sum < 0.0:
            bmesh.ops.reverse_faces(bm, faces=faces)
            bm.normal_update()

        if crease_edges and subdiv > 0:
            crease_layer = bm.edges.layers.float.new("crease_edge")
            for e in bm.edges:
                if e.is_boundary:
                    e[crease_layer] = 0.92

        mesh = bpy.data.meshes.new(name)
        bm.to_mesh(mesh)
        bm.free()

        return self.register(name, mesh, part, role, export=export, smooth=True,
                             solidify=solidify, bevel=bevel, subdiv=subdiv)

    def add_box(self, name: str, center: tuple[float, float, float], size: tuple[float, float, float],
                part: str, role: str, rot_deg: tuple[float, float, float] = (0.0, 0.0, 0.0),
                bevel: float = 0.0, bevel_segs: int = 2, export: bool = True,
                smooth: bool = False) -> bpy.types.Object:
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.0)
        bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
        if any(abs(a) > 1e-5 for a in rot_deg):
            rot = Euler([math.radians(a) for a in rot_deg], "XYZ").to_matrix().to_4x4()
            bmesh.ops.transform(bm, matrix=rot, verts=bm.verts)
        bmesh.ops.translate(bm, vec=Vector(center), verts=bm.verts)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        mesh = bpy.data.meshes.new(name)
        bm.to_mesh(mesh)
        bm.free()
        return self.register(name, mesh, part, role, export=export, smooth=smooth,
                             bevel=bevel, bevel_segs=bevel_segs)

    def add_cylinder(self, name: str, p0: tuple[float, float, float], p1: tuple[float, float, float],
                     r0: float, r1: float | None = None, segs: int = 16,
                     part: str = "Body", role: str = "trim", cap: bool = True,
                     export: bool = True) -> bpy.types.Object:
        if r1 is None:
            r1 = r0
        v0, v1 = Vector(p0), Vector(p1)
        axis = v1 - v0
        length = axis.length
        if length < 1e-5:
            length = 1e-5
            axis = Vector((0, 0, 1))
        bm = bmesh.new()
        bmesh.ops.create_cone(
            bm, cap_ends=cap, cap_tris=False, segments=segs,
            radius1=r0, radius2=r1, depth=length
        )
        quat = Vector((0, 0, 1)).rotation_difference(axis.normalized())
        mat = Matrix.Translation((v0 + v1) * 0.5) @ quat.to_matrix().to_4x4()
        bmesh.ops.transform(bm, matrix=mat, verts=bm.verts)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        mesh = bpy.data.meshes.new(name)
        bm.to_mesh(mesh)
        bm.free()
        return self.register(name, mesh, part, role, export=export, smooth=True)

    def add_tube_path(self, name: str, points: list[tuple[float, float, float]], radius: float,
                      segs: int = 10, part: str = "Body", role: str = "trim",
                      cap: bool = True, export: bool = True) -> bpy.types.Object:
        pts = [Vector(p) for p in points]
        bm = bmesh.new()
        rings = []
        prev_up = Vector((0, 0, 1))
        for i, p in enumerate(pts):
            if i == 0:
                t = (pts[1] - pts[0]).normalized()
            elif i == len(pts) - 1:
                t = (pts[-1] - pts[-2]).normalized()
            else:
                t = ((pts[i] - pts[i - 1]).normalized() + (pts[i + 1] - pts[i]).normalized()).normalized()
                if t.length < 1e-5:
                    t = (pts[i + 1] - pts[i]).normalized()
            if abs(t.dot(prev_up)) > 0.92:
                ref = Vector((1, 0, 0)) if abs(t.x) < 0.8 else Vector((0, 1, 0))
            else:
                ref = prev_up
            side = t.cross(ref).normalized()
            up = side.cross(t).normalized()
            prev_up = up
            ring = []
            for s in range(segs):
                ang = 2.0 * math.pi * s / segs
                v = p + (side * math.cos(ang) + up * math.sin(ang)) * radius
                ring.append(bm.verts.new(v))
            rings.append(ring)

        for i in range(len(rings) - 1):
            r0, r1 = rings[i], rings[i + 1]
            for s in range(segs):
                sn = (s + 1) % segs
                bm.faces.new((r0[s], r0[sn], r1[sn], r1[s]))
        if cap:
            bm.faces.new(list(reversed(rings[0])))
            bm.faces.new(rings[-1])
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        mesh = bpy.data.meshes.new(name)
        bm.to_mesh(mesh)
        bm.free()
        return self.register(name, mesh, part, role, export=export, smooth=True)

    def add_preview_wheels(self, wheel_positions: list[tuple[float, float]], wheel_r: float,
                           tyre_w: float, spokes: int = 6, offroad: bool = False):
        """High-detail studio preview wheels (export=False; Unity spawns its own animated wheels)."""
        for idx, (wx, wy) in enumerate(wheel_positions):
            side = 1.0 if wx >= 0 else -1.0
            cx = wx
            cz = wheel_r
            bm = bmesh.new()
            segs = 32
            prof = [
                (-tyre_w * 0.50, wheel_r * 0.64),
                (-tyre_w * 0.52, wheel_r * 0.80),
                (-tyre_w * 0.45, wheel_r * 0.97),
                (-tyre_w * 0.34, wheel_r * 1.00),
                (tyre_w * 0.34, wheel_r * 1.00),
                (tyre_w * 0.45, wheel_r * 0.97),
                (tyre_w * 0.52, wheel_r * 0.80),
                (tyre_w * 0.50, wheel_r * 0.64),
            ]
            rings = []
            for px, pr in prof:
                ring = []
                for s in range(segs):
                    ang = 2.0 * math.pi * s / segs
                    bump = 1.0 + (0.028 if offroad and abs(px) < tyre_w * 0.42 and s % 2 == 0 else 0.0)
                    ring.append(bm.verts.new((cx + px, wy + math.cos(ang) * pr * bump, cz + math.sin(ang) * pr * bump)))
                rings.append(ring)
            for i in range(len(rings) - 1):
                for s in range(segs):
                    sn = (s + 1) % segs
                    bm.faces.new((rings[i][s], rings[i][sn], rings[i + 1][sn], rings[i + 1][s]))
            for s in range(segs):
                sn = (s + 1) % segs
                bm.faces.new((rings[-1][s], rings[-1][sn], rings[0][sn], rings[0][s]))
            bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
            mesh = bpy.data.meshes.new(f"PreviewTyre_{idx}")
            bm.to_mesh(mesh)
            bm.free()
            self.register(f"PreviewTyre_{idx}", mesh, "PreviewWheel", "preview_rubber", export=False, smooth=True)

            rim_r = wheel_r * (0.58 if offroad else 0.70)
            # Outer stepped chrome/alloy rim lip
            self.add_cylinder(
                f"PreviewRimOuter_{idx}",
                (cx - side * tyre_w * 0.36, wy, cz),
                (cx + side * tyre_w * 0.48, wy, cz),
                rim_r, rim_r, segs=28, part="PreviewWheel", role="preview_rim", cap=False, export=False
            )
            self.add_cylinder(
                f"PreviewRimLip_{idx}",
                (cx + side * tyre_w * 0.40, wy, cz),
                (cx + side * tyre_w * 0.49, wy, cz),
                rim_r * 0.92, rim_r * 1.01, segs=28, part="PreviewWheel", role="chrome", cap=False, export=False
            )
            self.add_cylinder(
                f"PreviewRimHub_{idx}",
                (cx - side * tyre_w * 0.12, wy, cz),
                (cx + side * tyre_w * 0.20, wy, cz),
                rim_r * 0.26, rim_r * 0.22, segs=16, part="PreviewWheel", role="preview_rim", cap=True, export=False
            )
            self.add_cylinder(
                f"PreviewDisc_{idx}",
                (cx - side * tyre_w * 0.14, wy, cz),
                (cx - side * tyre_w * 0.05, wy, cz),
                rim_r * 0.80, rim_r * 0.80, segs=22, part="PreviewWheel", role="steel", cap=True, export=False
            )
            self.add_box(
                f"PreviewCaliper_{idx}",
                (cx - side * tyre_w * 0.06, wy - rim_r * 0.44, cz + rim_r * 0.24),
                (0.06, rim_r * 0.34, rim_r * 0.46),
                part="PreviewWheel", role="preview_caliper", bevel=0.01, export=False
            )
            for sp in range(spokes):
                ang = 2.0 * math.pi * sp / spokes
                p_in = (cx + side * tyre_w * 0.08, wy + math.cos(ang) * rim_r * 0.20, cz + math.sin(ang) * rim_r * 0.20)
                p_out = (cx + side * tyre_w * 0.38, wy + math.cos(ang) * rim_r * 0.95, cz + math.sin(ang) * rim_r * 0.95)
                self.add_cylinder(
                    f"PreviewSpoke_{idx}_{sp}", p_in, p_out,
                    rim_r * 0.088, rim_r * 0.062, segs=6, part="PreviewWheel", role="preview_rim", export=False
                )


# ---------------------------------------------------------------------------
# Parametric Passenger / SUV / Coupe Architecture

@dataclass
class CarSpec:
    kind: str
    length: float
    width: float
    height: float
    ground: float
    wheel_r: float
    tyre_w: float
    track: float
    front_axle: float
    rear_axle: float

    width_keys: list[tuple[float, float]]
    belt_keys: list[tuple[float, float]]
    hood_keys: list[tuple[float, float]]
    rocker_keys: list[tuple[float, float]]

    rear_glass_base: float
    rear_glass_top: float
    front_glass_top: float
    front_glass_base: float
    roof_width_ratio: float = 0.74
    roof_crown: float = 0.032

    paint_rgb: tuple[float, float, float] = (0.76, 0.06, 0.08)
    paint_metal: float = 0.74
    paint_rough: float = 0.16
    paint_coat: float = 1.0
    wheel_rgb: tuple[float, float, float] = (0.28, 0.27, 0.25)
    wheel_spokes: int = 6
    offroad_tyres: bool = False

    doors_per_side: int = 1
    hood_role: str = "paint"
    hood_style: str = "vented"
    grille_style: str = "gtr"
    headlight_style: str = "gtr"
    taillight_style: str = "gtr"
    flare_style: str = "blister"
    flare_out: float = 0.055
    has_gt_wing: bool = False
    wing_height: float = 0.22
    wing_span_ratio: float = 0.92
    has_lip_spoiler: bool = False
    has_chin_splitter: bool = True
    has_bullbar: bool = False
    has_roof_rack: bool = False
    is_hatchback: bool = False
    is_suv: bool = False
    is_pickup: bool = False
    bed_start_y: float = -0.35
    side_skirt_drop: float = 0.025
    tumblehome: float = 0.065
    shoulder_crease: float = 0.032

    @property
    def half_len(self) -> float:
        return self.length * 0.5

    def half_w(self, y: float) -> float:
        return eval_curve(self.width_keys, y)

    def belt(self, y: float) -> float:
        return eval_curve(self.belt_keys, y)

    def hood(self, y: float) -> float:
        return eval_curve(self.hood_keys, y)

    def rocker(self, y: float) -> float:
        return eval_curve(self.rocker_keys, y)

    def arch_r(self) -> float:
        return self.wheel_r + (0.082 if self.is_suv or self.is_pickup else 0.054)

    def side_bottom_z(self, y: float) -> float:
        z = self.rocker(y)
        ar = self.arch_r()
        for ay in (self.front_axle, self.rear_axle):
            dy = y - ay
            if abs(dy) < ar:
                arch_z = self.wheel_r + math.sqrt(max(0.0, ar * ar - dy * dy))
                if arch_z > z:
                    z = arch_z
        return min(z, self.belt(y) - 0.045)

    def side_point(self, side: float, y: float, v: float) -> tuple[float, float, float]:
        z0 = self.side_bottom_z(y)
        z1 = self.belt(y)
        z = lerp(z0, z1, v)
        hw = self.half_w(y)

        arch_swell = 0.0
        for ay, amp in ((self.front_axle, 0.028), (self.rear_axle, 0.040)):
            d = abs(y - ay) / (self.arch_r() * 1.65)
            if d < 1.0:
                arch_swell += amp * (0.5 + 0.5 * math.cos(d * math.pi)) * smoothstep(v * 1.4)

        tuck = -0.052 * ((1.0 - v) ** 2.6)
        belly = 0.022 * math.sin(v * math.pi)
        shoulder = self.shoulder_crease * math.exp(-((v - 0.80) / 0.14) ** 2)
        top_tumble = -self.tumblehome * (v ** 3.2)

        x = side * (hw + arch_swell + tuck + belly + shoulder + top_tumble)
        return (x, y, z)

    def greenhouse_point(self, y: float, across: float, inset: float = 0.0) -> tuple[float, float, float]:
        if y < self.rear_glass_top:
            t = clamp((y - self.rear_glass_base) / max(1e-5, self.rear_glass_top - self.rear_glass_base), 0.0, 1.0)
            rb = smoothstep(t)
        elif y > self.front_glass_top:
            t = clamp((self.front_glass_base - y) / max(1e-5, self.front_glass_base - self.front_glass_top), 0.0, 1.0)
            rb = smoothstep(t)
        else:
            rb = 1.0

        hw_base = abs(self.side_point(1.0, y, 1.0)[0]) - 0.015
        hw_roof = self.half_w(y) * self.roof_width_ratio
        hw = lerp(hw_base, hw_roof, rb) - inset

        wrap_y = 0.0
        if y > self.front_glass_top:
            wrap_y = 0.048 * (1.0 - across * across) * (1.0 - 0.35 * rb)
        elif y < self.rear_glass_top:
            wrap_y = -0.036 * (1.0 - across * across) * (1.0 - 0.35 * rb)

        z_base = self.belt(y)
        mid_roof = 0.5 * (self.rear_glass_top + self.front_glass_top)
        roof_span = max(0.2, 0.5 * (self.front_glass_top - self.rear_glass_top))
        long_arch = 0.016 * (1.0 - clamp((y - mid_roof) / roof_span, -1.0, 1.0) ** 2)
        z_roof = self.height + long_arch
        z = lerp(z_base, z_roof, rb) + self.roof_crown * (1.0 - across * across) * (0.35 + 0.65 * rb)
        return (across * hw, y + wrap_y, z)


def build_sculpted_car(spec: CarSpec) -> tuple[VehicleBuilder, dict]:
    """Constructs a complete, realistic, crash-ready vehicle from a CarSpec."""
    vb = VehicleBuilder(
        spec.kind, spec.paint_rgb, spec.paint_metal, spec.paint_rough,
        spec.paint_coat, wheel_rgb=spec.wheel_rgb
    )
    hl = spec.half_len
    ar = spec.arch_r()

    # Seam coordinates
    fy_seam = hl - 0.24
    ry_seam = -hl + 0.22
    front_door_y = min(spec.front_glass_base + 0.02, spec.front_axle - ar - 0.025)
    rear_door_y = spec.bed_start_y if spec.is_pickup else max(spec.rear_glass_base - 0.04, spec.rear_axle + ar + 0.03)
    mid_door_y = lerp(rear_door_y, front_door_y, 0.48)

    # ------------------------------------------------------------------
    # 1. BODY-IN-WHITE: Underbody chassis, crash beams, inner jambs, wheel tubs, interior
    floor_w = spec.half_w(0.0) * 1.48
    vb.add_box("ChassisFloor", (0.0, 0.0, spec.rocker(0.0) + 0.08),
               (floor_w, spec.length * 0.78, 0.12), "Body", "underbody")
    for side in (-1.0, 1.0):
        vb.add_box(f"FrameRail_{int(side)}", (side * 0.42, 0.05, spec.rocker(0.0) + 0.15),
                   (0.10, spec.length * 0.86, 0.14), "Body", "underbody")
        vb.add_box(f"RockerBeam_{int(side)}", (side * (spec.half_w(0.0) - 0.09), 0.0, spec.rocker(0.0) + 0.07),
                   (0.11, spec.front_axle - spec.rear_axle - ar * 1.7, 0.11), "Body", "trim", bevel=0.015)
        if spec.side_skirt_drop > 0.0:
            y_sk0 = spec.rear_axle + ar + 0.03
            y_sk1 = spec.front_axle - ar - 0.03
            vb.add_box(f"SideSkirt_{int(side)}",
                       (side * (spec.half_w(0.0) - 0.015), 0.5 * (y_sk0 + y_sk1), spec.rocker(0.0) + 0.018),
                       (0.10, y_sk1 - y_sk0, 0.062), "Body", "matteBlack", bevel=0.012)

    # Front & Rear crash beams, engine bay firewall & radiator core
    vb.add_box("FrontCrashBeam", (0.0, hl - 0.22, spec.rocker(hl) + 0.22),
               (spec.half_w(hl) * 1.40, 0.10, 0.13), "Body", "steel")
    vb.add_box("RadiatorCore", (0.0, hl - 0.34, spec.rocker(hl) + 0.32),
               (spec.half_w(hl) * 1.15, 0.08, 0.30), "Body", "trim")
    vb.add_box("EngineBlock", (0.0, spec.front_axle - 0.05, spec.rocker(0.0) + 0.36),
               (0.54, 0.60, 0.34), "Body", "steel", bevel=0.03)
    vb.add_box("RearCrashBeam", (0.0, -hl + 0.20, spec.rocker(-hl) + 0.24),
               (spec.half_w(-hl) * 1.40, 0.10, 0.13), "Body", "steel")
    # Dark cowl panel between hood rear edge and windshield base
    cowl_y = spec.front_glass_base + 0.01
    vb.add_box("WiperCowl", (0.0, cowl_y, spec.hood(cowl_y) - 0.01),
               (spec.half_w(cowl_y) * 1.68, 0.12, 0.06), "Body", "trim", bevel=0.015)

    # Inner wheelhouse tubs (4 wheels)
    for side in (-1.0, 1.0):
        for wname, ay in (("Front", spec.front_axle), ("Rear", spec.rear_axle)):
            tub_grid = []
            for iu in range(19):
                ang = math.pi * iu / 18.0
                wy = ay + math.cos(ang) * (ar - 0.004)
                wz = spec.wheel_r + math.sin(ang) * (ar - 0.004)
                tub_grid.append([
                    (side * (spec.track - spec.tyre_w * 0.72), wy, wz),
                    (side * (spec.half_w(wy) - 0.012), wy, wz),
                ])
            vb.add_grid_surface(
                f"WheelTub_{wname}_{int(side)}", tub_grid,
                lambda p, ay=ay, wr=spec.wheel_r: Vector((0.0, ay - p.y, wr - p.z)).normalized(),
                "Body", "underbody"
            )

    # Interior cabin
    dash_y = spec.front_glass_base - 0.16
    dash_z = spec.belt(dash_y) - 0.12
    vb.add_box("Dashboard", (0.0, dash_y, dash_z),
               (spec.half_w(dash_y) * 1.54, 0.34, 0.22), "Body", "interior", bevel=0.03)
    vb.add_box("CenterConsole", (0.0, dash_y - 0.55, spec.rocker(0.0) + 0.24),
               (0.24, 1.05, 0.22), "Body", "interior", bevel=0.02)
    sw_pos = (-0.36, dash_y - 0.24, dash_z + 0.08)
    vb.add_cylinder("SteeringWheel", (sw_pos[0], sw_pos[1] - 0.03, sw_pos[2] - 0.01),
                    (sw_pos[0], sw_pos[1] + 0.03, sw_pos[2] + 0.01), 0.155, 0.155, segs=14,
                    part="Body", role="trim", cap=False)
    for side in (-1.0, 1.0):
        seat_y = dash_y - 0.58
        vb.add_box(f"SeatBase_{int(side)}", (side * 0.38, seat_y, spec.rocker(0.0) + 0.24),
                   (0.46, 0.50, 0.12), "Body", "interior", bevel=0.03)
        vb.add_box(f"SeatBack_{int(side)}", (side * 0.38, seat_y - 0.22, spec.rocker(0.0) + 0.56),
                   (0.44, 0.14, 0.54), "Body", "interior", rot_deg=(14.0, 0.0, 0.0), bevel=0.03)
    if not spec.is_pickup:
        vb.add_box("RearSeat", (0.0, spec.rear_glass_base + 0.22, spec.rocker(0.0) + 0.38),
                   (spec.half_w(0.0) * 1.40, 0.44, 0.40), "Body", "interior", bevel=0.03)

    # ------------------------------------------------------------------
    # 2. SCULPTED BODYSIDES: Front Fenders (detachable), Doors (detachable), Rear Quarters (Body)
    gap = 0.0018  # Crisp 3.6mm real automotive panel gap
    n_fv = 10

    for side in (-1.0, 1.0):
        sname = "Right" if side > 0 else "Left"
        out_vec = Vector((side, 0.0, 0.08)).normalized()

        # Front fender (from front_door_y to fy_seam) — DETACHABLE!
        fender_y0 = front_door_y + gap
        fender_y1 = fy_seam + gap
        fgrid = [
            [spec.side_point(side, lerp(fender_y0, fender_y1, iy / 25.0), iv / (n_fv - 1)) for iv in range(n_fv)]
            for iy in range(26)
        ]
        vb.add_grid_surface(f"{sname}FrontFender", fgrid, out_vec, f"{sname} front fender", "paint", solidify=0.012)

        # Fender top shoulder return meeting hood edge cleanly
        ftop_grid = []
        for iy in range(18):
            y = lerp(fender_y0, fender_y1, iy / 17.0)
            p_out = spec.side_point(side, y, 1.0)
            x_hood = side * (abs(p_out[0]) - 0.085)
            z_hood = spec.hood(y) + 0.006
            p_mid = (lerp(p_out[0], x_hood, 0.5), y, lerp(p_out[2], z_hood, 0.65) + 0.008)
            ftop_grid.append([p_out, p_mid, (x_hood, y, z_hood)])
        vb.add_grid_surface(f"{sname}FrontFenderTop", ftop_grid, Vector((0, 0, 1)),
                            f"{sname} front fender", "paint", solidify=0.010)

        # Rear quarter panel (from ry_seam to rear_door_y) — Body
        q_y0 = ry_seam - gap
        q_y1 = rear_door_y - gap
        qgrid = [
            [spec.side_point(side, lerp(q_y0, q_y1, iy / 25.0), iv / (n_fv - 1)) for iv in range(n_fv)]
            for iy in range(26)
        ]
        vb.add_grid_surface(f"{sname}RearQuarter", qgrid, out_vec, "Body", "paint", solidify=0.012)

        if not spec.is_pickup:
            qtop_grid = []
            for iy in range(18):
                y = lerp(q_y0, q_y1, iy / 17.0)
                p_out = spec.side_point(side, y, 1.0)
                x_in = side * (abs(p_out[0]) - 0.11)
                z_in = spec.belt(y) + 0.012
                p_mid = (lerp(p_out[0], x_in, 0.5), y, lerp(p_out[2], z_in, 0.65) + 0.008)
                qtop_grid.append([p_out, p_mid, (x_in, y, z_in)])
            vb.add_grid_surface(f"{sname}RearQuarterTop", qtop_grid, Vector((0, 0, 1)),
                                "Body", "paint", solidify=0.010)

        # Doors (DETACHABLE!)
        door_spans = (
            [(f"{sname} front door", rear_door_y + gap, front_door_y - gap)]
            if spec.doors_per_side == 1 else
            [
                (f"{sname} rear door", rear_door_y + gap, mid_door_y - gap),
                (f"{sname} front door", mid_door_y + gap, front_door_y - gap),
            ]
        )
        for dpart, dy0, dy1 in door_spans:
            dgrid = [
                [spec.side_point(side, lerp(dy0, dy1, iy / 17.0), iv / (n_fv - 1)) for iv in range(n_fv)]
                for iy in range(18)
            ]
            vb.add_grid_surface(dpart.replace(" ", "_"), dgrid, out_vec, dpart, "paint", solidify=0.012)
            # Beltline window trim strip on door top
            tgrid = []
            for iy in range(10):
                y = lerp(dy0, dy1, iy / 9.0)
                p = spec.side_point(side, y, 1.0)
                tgrid.append([(p[0] + side * 0.004, y, p[2] - 0.012), (p[0] - side * 0.018, y, p[2] + 0.012)])
            vb.add_grid_surface(f"{dpart}_BeltTrim", tgrid, Vector((side, 0, 0.6)).normalized(), dpart, "trim")
            # Flush door handle
            hy = lerp(dy0, dy1, 0.24)
            hp = spec.side_point(side, hy, 0.82)
            vb.add_box(f"{dpart}_Handle", (hp[0] + side * 0.006, hy, hp[2]),
                       (0.020, 0.14, 0.030), dpart, "trim", bevel=0.007)

        # Wheel Arch Flares / Widebody Overfenders
        flare_role = "matteBlack" if spec.flare_style in ("riveted_black", "rugged_black") else "paint"
        for wname, ay, fpart in (("Front", spec.front_axle, f"{sname} front fender"),
                                 ("Rear", spec.rear_axle, "Body")):
            fl_grid = []
            n_ang = 26
            for ia in range(n_ang):
                ang = lerp(math.radians(-4.0), math.radians(184.0), ia / (n_ang - 1))
                r_in = ar - 0.003
                r_out = ar + (0.095 if spec.flare_style in ("riveted_black", "rugged_black") else 0.068)
                wy_in = ay + math.cos(ang) * r_in
                wz_in = max(spec.rocker(wy_in), spec.wheel_r + math.sin(ang) * r_in)
                wy_out = ay + math.cos(ang) * r_out
                wz_out = max(spec.rocker(wy_out), spec.wheel_r + math.sin(ang) * r_out)
                wz_out = min(wz_out, spec.belt(wy_out) - 0.03)
                wz_in = min(wz_in, wz_out - 0.02)

                hw_y = spec.half_w(wy_in) + 0.018
                fo = spec.flare_out
                fl_grid.append([
                    (side * (hw_y - 0.022), wy_in, wz_in),
                    (side * (hw_y + fo), wy_in, wz_in + 0.014),
                    (side * (hw_y + fo * 0.82), lerp(wy_in, wy_out, 0.62), lerp(wz_in, wz_out, 0.62)),
                    (side * (hw_y - 0.006), wy_out, wz_out),
                ])
            vb.add_grid_surface(f"{sname}_{wname}_Flare", fl_grid,
                                Vector((side, 0.0, 0.25)).normalized(), fpart, flare_role, solidify=0.009)
            if spec.flare_style == "riveted_black":
                for ir in range(7):
                    ang = lerp(math.radians(16.0), math.radians(164.0), ir / 6.0)
                    ry = ay + math.cos(ang) * (ar + 0.072)
                    rz = min(spec.belt(ry) - 0.04, spec.wheel_r + math.sin(ang) * (ar + 0.072))
                    rx = side * (spec.half_w(ry) + spec.flare_out * 0.45)
                    vb.add_cylinder(f"{sname}_{wname}_Rivet_{ir}",
                                    (rx, ry, rz), (rx + side * 0.012, ry, rz),
                                    0.009, 0.007, segs=6, part=fpart, role="steel")

    # ------------------------------------------------------------------
    # 3. SCULPTED HOOD (DETACHABLE!) — flows cleanly right to the front grille header!
    hood_y0 = spec.front_glass_base + 0.010
    hood_y1 = hl - 0.055
    hgrid = []
    n_hy, n_hx = 22, 19
    for iy in range(n_hy):
        u = iy / (n_hy - 1)
        y = lerp(hood_y0, hood_y1, u)
        # Match fender top inner edge width smoothly
        hw_fend = abs(spec.side_point(1.0, min(y, fy_seam), 1.0)[0]) - 0.082
        # Slight plan-view taper on nose overhang
        if y > fy_seam:
            t_nose = (y - fy_seam) / max(1e-5, hood_y1 - fy_seam)
            hw = hw_fend * (1.0 - 0.09 * (t_nose ** 1.6))
        else:
            hw = hw_fend
        base_z = spec.hood(y)
        row = []
        for ix in range(n_hx):
            v = (ix / (n_hx - 1)) * 2.0 - 1.0
            crown = 0.032 * (1.0 - v * v) * (1.0 - 0.45 * u)
            lip_drop = -0.006 * smoothstep((u - 0.86) / 0.14)
            extra = 0.0
            if spec.hood_style == "powerdome":
                if abs(v) < 0.42 and 0.14 < u < 0.86:
                    dome_x = math.cos(v / 0.42 * math.pi * 0.5) ** 1.5
                    dome_y = math.sin((u - 0.14) / 0.72 * math.pi)
                    extra = 0.044 * dome_x * dome_y
            elif spec.hood_style == "muscle":
                if abs(v) < 0.52 and 0.08 < u < 0.88:
                    extra = 0.032 * (1.0 - (v / 0.52) ** 4) * (math.sin((u - 0.08) / 0.80 * math.pi) ** 0.6)
            elif spec.hood_style == "vented":
                if abs(v) < 0.46 and 0.16 < u < 0.82:
                    extra = 0.024 * (1.0 - (v / 0.46) ** 2) * math.sin((u - 0.16) / 0.66 * math.pi)
            elif spec.hood_style == "rugged":
                if abs(v) < 0.58:
                    extra = 0.026 * smoothstep(1.0 - abs(v) / 0.58)
            row.append((v * hw, y, base_z + crown + extra + lip_drop))
        hgrid.append(row)
    vb.add_grid_surface("HoodSkin", hgrid, Vector((0, 0, 1)), "Hood", spec.hood_role, solidify=0.013)

    if spec.hood_style == "vented":
        for iv in range(4):
            vy = lerp(hood_y0, hood_y1, 0.36 + iv * 0.095)
            vz = spec.hood(vy) + 0.044
            vb.add_box(f"HoodLouver_{iv}", (0.0, vy, vz), (0.44, 0.046, 0.016),
                       "Hood", "matteBlack", rot_deg=(11.0, 0.0, 0.0), bevel=0.004)
    elif spec.hood_style == "muscle":
        for side in (-1.0, 1.0):
            vy = lerp(hood_y0, hood_y1, 0.54)
            vb.add_box(f"MuscleHoodVent_{int(side)}", (side * 0.28, vy, spec.hood(vy) + 0.046),
                       (0.18, 0.24, 0.015), "Hood", "trim", bevel=0.005)
            py = hood_y1 - 0.12
            vb.add_cylinder(f"HoodPin_{int(side)}", (side * 0.42, py, spec.hood(py) + 0.012),
                            (side * 0.42, py, spec.hood(py) + 0.030), 0.020, 0.020, segs=10,
                            part="Hood", role="chrome")

    # ------------------------------------------------------------------
    # 4. CONTINUOUS SCULPTED GREENHOUSE (NO STICKING-OUT CYLINDERS!)
    # Roof Panel (DETACHABLE!)
    rgrid = [
        [spec.greenhouse_point(lerp(spec.rear_glass_top, spec.front_glass_top, iy / 13.0),
                               (ix / 14.0) * 2.0 - 1.0, inset=0.0) for ix in range(15)]
        for iy in range(14)
    ]
    vb.add_grid_surface("RoofPanel", rgrid, Vector((0, 0, 1)), "Roof panel", "paint", solidify=0.012)

    # Flush Curved Windshield + Dark Rubber Seal Border
    wgrid = [
        [spec.greenhouse_point(lerp(spec.front_glass_top + 0.015, spec.front_glass_base - 0.008, iy / 11.0),
                               (ix / 14.0) * 2.0 - 1.0, inset=0.042) for ix in range(15)]
        for iy in range(12)
    ]
    vb.add_grid_surface("Windshield", wgrid, Vector((0, 0.72, 0.70)).normalized(), "Body", "glass")

    # Flush Curved Rear Backlight
    if not spec.is_pickup:
        bgrid = [
            [spec.greenhouse_point(lerp(spec.rear_glass_base + 0.015, spec.rear_glass_top - 0.015, iy / 11.0),
                                   (ix / 14.0) * 2.0 - 1.0, inset=0.065) for ix in range(15)]
            for iy in range(12)
        ]
        vb.add_grid_surface("RearGlass", bgrid, Vector((0, -0.72, 0.70)).normalized(), "Body", "glass")
    else:
        p_b = spec.greenhouse_point(spec.rear_glass_base, 0.88, inset=0.04)
        p_t = spec.greenhouse_point(spec.rear_glass_top, 0.88, inset=0.04)
        vb.add_grid_surface("PickupRearGlass", [
            [(-p_b[0], spec.rear_glass_base, p_b[2]), (p_b[0], spec.rear_glass_base, p_b[2])],
            [(-p_t[0], spec.rear_glass_top, p_t[2]), (p_t[0], spec.rear_glass_top, p_t[2])],
        ], Vector((0, -1, 0)), "Body", "glass")

    # Sculpted Ribbon Pillars & Cant Rails (exact surface patches matching greenhouse_point!)
    for side in (-1.0, 1.0):
        sname = "Right" if side > 0 else "Left"
        out_s = Vector((side, 0.0, 0.35)).normalized()

        # A-pillar sculpted ribbon (flows flush from fender beltline to roof edge!)
        ap_grid = []
        for iu in range(10):
            y = lerp(spec.front_glass_top - 0.02, spec.front_glass_base + 0.01, iu / 9.0)
            p_out = spec.greenhouse_point(y, side, inset=-0.002)
            p_in = spec.greenhouse_point(y, side * 0.90, inset=0.002)
            p_side = (p_out[0], y - 0.045, p_out[2] - 0.018)
            ap_grid.append([p_side, p_out, p_in])
        vb.add_grid_surface(f"{sname}_APillar", ap_grid, Vector((side * 0.6, 0.6, 0.5)).normalized(),
                            "Body", "paint", solidify=0.014)

        # C-pillar / Fastback Sail Panel sculpted ribbon (thick, muscular, flush!)
        cp_grid = []
        sail_w = 0.24 if not spec.is_pickup else 0.10
        for iu in range(12):
            u = iu / 11.0
            y_back = lerp(spec.rear_glass_base - 0.01, spec.rear_glass_top + 0.02, u)
            y_front = y_back + lerp(sail_w * 1.15, sail_w * 0.55, u)
            p_b_in = spec.greenhouse_point(y_back, side * 0.84, inset=0.002)
            p_b_out = spec.greenhouse_point(y_back, side, inset=-0.002)
            p_f_out = spec.greenhouse_point(min(y_front, spec.front_glass_top - 0.1), side, inset=0.004)
            # Blend front edge Z cleanly to match the window opening
            p_f_out = (p_f_out[0], y_front, lerp(spec.belt(y_front), p_b_out[2], u))
            cp_grid.append([p_b_in, p_b_out, p_f_out])
        vb.add_grid_surface(f"{sname}_CPillar", cp_grid, Vector((side * 0.75, -0.45, 0.48)).normalized(),
                            "Body", "paint", solidify=0.014)

        # Roof side cant rail (smooth bridge between A-pillar and C-pillar along roof edge)
        cr_grid = []
        for iu in range(12):
            y = lerp(spec.rear_glass_top - 0.02, spec.front_glass_top + 0.02, iu / 11.0)
            p_top = spec.greenhouse_point(y, side, inset=-0.002)
            p_low = (p_top[0] + side * 0.018, y, p_top[2] - 0.045)
            cr_grid.append([p_low, p_top])
        vb.add_grid_surface(f"{sname}_CantRail", cr_grid, out_s, "Body", "paint", solidify=0.012)

        # B-pillar blackout trim
        b_y = mid_door_y if spec.doors_per_side == 2 else lerp(spec.rear_glass_top, spec.front_glass_top, 0.36)
        b_lo = (side * (abs(spec.side_point(1.0, b_y, 1.0)[0]) - 0.015), b_y, spec.belt(b_y))
        b_hi = spec.greenhouse_point(b_y, side, inset=0.015)
        bp_grid = [
            [(b_lo[0], b_y - 0.038, b_lo[2]), (b_lo[0], b_y + 0.038, b_lo[2])],
            [(b_hi[0], b_y - 0.032, b_hi[2] - 0.015), (b_hi[0], b_y + 0.032, b_hi[2] - 0.015)],
        ]
        vb.add_grid_surface(f"{sname}_BPillar", bp_grid, out_s, "Body", "trim", solidify=0.010)

        # Extra mid-rear C-pillar for SUVs / Hatchbacks (separating rear door glass from cargo glass!)
        if spec.is_suv or spec.is_hatchback:
            c2_y = rear_door_y + 0.02
            c2_lo = (side * (abs(spec.side_point(1.0, c2_y, 1.0)[0]) - 0.010), c2_y, spec.belt(c2_y))
            c2_hi = spec.greenhouse_point(c2_y, side, inset=0.008)
            c2_grid = [
                [(c2_lo[0], c2_y - 0.055, c2_lo[2]), (c2_lo[0], c2_y + 0.055, c2_lo[2])],
                [(c2_hi[0], c2_y - 0.048, c2_hi[2] - 0.01), (c2_hi[0], c2_y + 0.048, c2_hi[2] - 0.01)],
            ]
            vb.add_grid_surface(f"{sname}_SuvMidPillar", c2_grid, out_s, "Body", "paint", solidify=0.014)

        # Flush Side Window Glass
        sw_grid = []
        sw_y0 = spec.rear_glass_base + sail_w * 0.95
        sw_y1 = spec.front_glass_base - 0.04
        for iy in range(16):
            y = lerp(sw_y0, sw_y1, iy / 15.0)
            p_lo = (side * (abs(spec.side_point(1.0, y, 1.0)[0]) - 0.018), y, spec.belt(y) + 0.005)
            p_hi = spec.greenhouse_point(y, side, inset=0.012)
            p_hi = (p_hi[0], y, max(p_lo[2] + 0.03, p_hi[2] - 0.028))
            p_mid = (
                lerp(p_lo[0], p_hi[0], 0.5) + side * 0.012,
                y,
                lerp(p_lo[2], p_hi[2], 0.5),
            )
            sw_grid.append([p_lo, p_mid, p_hi])
        vb.add_grid_surface(f"{sname}_SideGlass", sw_grid, out_s, "Body", "glass")

        # Side mirrors (DETACHABLE!)
        my = spec.front_glass_base - 0.15
        mp = spec.side_point(side, my, 0.96)
        mpart = f"{sname} mirror"
        vb.add_cylinder(f"{sname}_MirrorStalk", (mp[0], my, mp[2] + 0.005),
                        (mp[0] + side * 0.085, my - 0.025, mp[2] + 0.055), 0.016, 0.013, segs=8, part=mpart, role="trim")
        vb.add_box(f"{sname}_MirrorCap", (mp[0] + side * 0.125, my - 0.035, mp[2] + 0.065),
                   (0.13, 0.135, 0.082), mpart, "paint", bevel=0.028, smooth=True)
        vb.add_box(f"{sname}_MirrorGlass", (mp[0] + side * 0.125, my - 0.105, mp[2] + 0.065),
                   (0.10, 0.008, 0.062), mpart, "chrome")

    # ------------------------------------------------------------------
    # 5. TRUNK LID / TAILGATE / PICKUP BED
    if spec.is_pickup:
        bed_y0 = -hl + 0.12
        bed_y1 = spec.bed_start_y
        bed_len = bed_y1 - bed_y0
        bed_cy = 0.5 * (bed_y0 + bed_y1)
        bed_w = spec.half_w(bed_cy) * 1.72
        vb.add_box("BedFloor", (0.0, bed_cy, spec.rocker(bed_cy) + 0.34),
                   (bed_w, bed_len, 0.06), "Body", "underbody")
        for side in (-1.0, 1.0):
            vb.add_box(f"BedInnerWall_{int(side)}", (side * (bed_w * 0.5 - 0.04), bed_cy, spec.belt(bed_cy) - 0.22),
                       (0.08, bed_len, 0.44), "Body", "trim")
        vb.add_box("Tailgate", (0.0, bed_y0, spec.belt(bed_y0) - 0.22),
                   (bed_w + 0.08, 0.09, 0.48), "Trunk lid", "paint", bevel=0.02)
    else:
        trunk_y0 = -hl + 0.10
        trunk_y1 = spec.rear_glass_base - 0.01
        if trunk_y1 > trunk_y0 + 0.08:
            tgrid = []
            for iy in range(12):
                y = lerp(trunk_y0, trunk_y1, iy / 11.0)
                hw = abs(spec.side_point(1.0, max(y, ry_seam), 1.0)[0]) - 0.10
                row = []
                for ix in range(13):
                    v = (ix / 12.0) * 2.0 - 1.0
                    z = spec.belt(y) + 0.024 * (1.0 - v * v)
                    row.append((v * hw, y, z))
                tgrid.append(row)
            vb.add_grid_surface("TrunkLid", tgrid, Vector((0, 0, 1)), "Trunk lid", "paint", solidify=0.012)
        else:
            vb.add_box("Tailgate", (0.0, -hl + 0.10, lerp(spec.rocker(-hl) + 0.32, spec.belt(-hl), 0.55)),
                       (spec.half_w(-hl) * 1.72, 0.10, spec.belt(-hl) - spec.rocker(-hl) - 0.22),
                       "Trunk lid", "paint", bevel=0.025)

    # ------------------------------------------------------------------
    # 6. SEAMLESS FRONT FASCIA / BUMPER (DETACHABLE!), GRILLE & HEADLIGHTS
    _build_front_fascia(vb, spec, fy_seam)

    # ------------------------------------------------------------------
    # 7. SEAMLESS REAR FASCIA / BUMPER (DETACHABLE!), DIFFUSER, EXHAUSTS & TAILLIGHTS
    _build_rear_fascia(vb, spec, ry_seam)

    # ------------------------------------------------------------------
    # 8. SIGNATURE ACCESSORIES
    if spec.has_gt_wing:
        _build_gt_wing(vb, spec)
    elif spec.has_lip_spoiler:
        sy = -hl + 0.14
        vb.add_box("LipSpoiler", (0.0, sy, spec.belt(sy) + 0.032),
                   (spec.half_w(sy) * 1.76, 0.14, 0.044), "Trunk lid", "matteBlack",
                   rot_deg=(12.0, 0.0, 0.0), bevel=0.012)

    if spec.has_bullbar:
        _build_bullbar(vb, spec)

    if spec.has_roof_rack:
        _build_roof_rack(vb, spec)

    # ------------------------------------------------------------------
    # 9. PREVIEW WHEELS
    wheel_pos = [
        (-spec.track, spec.front_axle), (spec.track, spec.front_axle),
        (-spec.track, spec.rear_axle), (spec.track, spec.rear_axle),
    ]
    vb.add_preview_wheels(wheel_pos, spec.wheel_r, spec.tyre_w,
                          spokes=spec.wheel_spokes, offroad=spec.offroad_tyres)

    cabin_cy = 0.5 * (spec.rear_glass_top + spec.front_glass_top)
    cabin_cz = 0.5 * (spec.belt(0.0) + spec.height)
    cabin_w = spec.half_w(0.0) * spec.roof_width_ratio * 1.85
    cabin_h = spec.height - spec.belt(0.0)
    cabin_l = (spec.front_glass_top - spec.rear_glass_top) + 0.35

    info = dict(
        ground=spec.ground,
        halfLength=spec.half_len,
        wheelRadius=spec.wheel_r,
        track=spec.track,
        tyreWidth=spec.tyre_w,
        cabin=[0.0, cabin_cz, cabin_cy, cabin_w, cabin_h, cabin_l],
    )
    return vb, info


def _build_front_fascia(vb: VehicleBuilder, spec: CarSpec, fy_seam: float):
    hl = spec.half_len
    fy_front = hl - 0.015
    z_bot_c = spec.rocker(fy_front)
    z_top_c = spec.hood(fy_front)
    bpart = "Front bumper"

    # Wrap-around sculpted front bumper shell whose outer edges match side_point(±1, fy_seam, v) EXACTLY!
    # Power 0.42 gives a crisp, flat, aggressive real-world front face with clean corner wraps.
    corner_pow = 0.42
    bgrid = []
    n_u, n_v = 25, 10
    for iu in range(n_u):
        s = (iu / (n_u - 1)) * 2.0 - 1.0  # -1..+1
        ang = s * math.pi * 0.5
        cos_a = math.cos(ang)
        sin_a = math.sin(ang)
        row = []
        for iv in range(n_v):
            v = iv / (n_v - 1)
            sp = spec.side_point(1.0, fy_seam, v)
            hw_v = abs(sp[0])
            x = sin_a * hw_v
            y_base = fy_seam + (cos_a ** corner_pow) * (fy_front - fy_seam)
            z_side = sp[2]
            z_nose = lerp(z_bot_c, z_top_c + 0.026, v)
            z = lerp(z_side, z_nose, cos_a ** 0.55)
            y_off = 0.028 * math.sin(v * math.pi) - 0.018 * (v ** 2.2)
            in_mouth = (abs(s) < 0.46 and 0.08 < v < 0.46)
            recess = -0.048 * (1.0 - (s / 0.46) ** 4) if in_mouth else 0.0
            row.append((x, y_base + (y_off + recess) * cos_a, z))
        bgrid.append(row)
    bumper_role = "matteBlack" if spec.is_suv else "paint"
    vb.add_grid_surface("FrontBumperShell", bgrid,
                        lambda p: Vector((p.x * 0.45, 1.0, 0.12)).normalized(),
                        bpart, bumper_role, solidify=0.014)

    hw_f = abs(spec.side_point(1.0, fy_seam, 0.72)[0])
    # Aggressive Chin Splitter / Front Aero Lip
    if spec.has_chin_splitter:
        sp_grid = []
        for iu in range(19):
            s = (iu / 18.0) * 2.0 - 1.0
            ang = s * math.pi * 0.48
            cos_a = math.cos(ang)
            x = math.sin(ang) * (hw_f + 0.02)
            y0 = fy_seam + (cos_a ** corner_pow) * (fy_front - fy_seam - 0.02)
            y1 = y0 + 0.072 * cos_a
            sp_grid.append([(x, y0, z_bot_c - 0.004), (x, y1, z_bot_c - 0.010)])
        vb.add_grid_surface("FrontSplitter", sp_grid, Vector((0, 0, 1)), bpart, "matteBlack", solidify=0.016)

    # Exact inverse of the front bumper wrap surface at any lateral x and height fraction v!
    def nose_y_at(x_val: float, v_val: float = 0.72) -> float:
        hw_v = abs(spec.side_point(1.0, fy_seam, v_val)[0])
        sin_a = clamp(abs(x_val) / max(0.1, hw_v), 0.0, 0.985)
        cos_a = math.sqrt(max(0.0, 1.0 - sin_a * sin_a))
        y_b = fy_seam + (cos_a ** corner_pow) * (fy_front - fy_seam)
        y_o = (0.028 * math.sin(v_val * math.pi) - 0.018 * (v_val ** 2.2)) * cos_a
        return y_b + y_o

    # Recessed Grille + Headlights (proud/flush with the nose curve!)
    grille_z = lerp(z_bot_c, z_top_c, 0.68)
    grille_h = (z_top_c - z_bot_c) * 0.30
    gy_c = nose_y_at(0.0, 0.68)
    if spec.grille_style == "bmw":
        for side in (-1.0, 1.0):
            kx = side * 0.20
            ky = nose_y_at(kx, 0.68)
            vb.add_box(f"KidneyFrame_{int(side)}", (kx, ky + 0.005, grille_z),
                       (0.32, 0.06, grille_h * 1.05), bpart, "matteBlack", bevel=0.022)
            vb.add_box(f"KidneyCore_{int(side)}", (kx, ky + 0.014, grille_z),
                       (0.26, 0.05, grille_h * 0.84), bpart, "underbody")
            for islat in range(5):
                sx = kx + lerp(-0.10, 0.10, islat / 4.0)
                vb.add_box(f"KidneySlat_{int(side)}_{islat}", (sx, ky + 0.038, grille_z),
                           (0.012, 0.018, grille_h * 0.78), bpart, "chrome")
        vb.add_box("M4LowerIntake", (0.0, nose_y_at(0.0, 0.25) - 0.01, z_bot_c + 0.11),
                   (hw_f * 0.96, 0.09, 0.16), bpart, "underbody")
        for side in (-1.0, 1.0):
            dx = side * hw_f * 0.66
            vb.add_box(f"M4SideDuct_{int(side)}", (dx, nose_y_at(dx, 0.25) - 0.01, z_bot_c + 0.12),
                       (0.30, 0.08, 0.16), bpart, "matteBlack", rot_deg=(0.0, 0.0, -side * 18.0), bevel=0.014)
    elif spec.grille_style == "muscle":
        # Full-width classic muscle car recessed grille bar
        vb.add_box("MuscleGrilleTrim", (0.0, gy_c + 0.004, grille_z),
                   (hw_f * 1.66, 0.075, grille_h * 1.08), bpart, "matteBlack", bevel=0.016)
        vb.add_box("MuscleGrilleCore", (0.0, gy_c + 0.016, grille_z),
                   (hw_f * 1.56, 0.06, grille_h * 0.84), bpart, "underbody")
        for islat in range(3):
            sz = grille_z + lerp(-grille_h * 0.28, grille_h * 0.28, islat / 2.0)
            vb.add_box(f"MuscleSlat_{islat}", (0.0, gy_c + 0.044, sz),
                       (hw_f * 1.48, 0.014, 0.012), bpart, "trim")
    elif spec.grille_style in ("suv", "pickup"):
        vb.add_box("TruckGrilleFrame", (0.0, gy_c + 0.005, grille_z),
                   (hw_f * 1.12, 0.08, grille_h * 1.30), bpart, "matteBlack", bevel=0.02)
        for islat in range(3):
            sz = grille_z + lerp(-grille_h * 0.36, grille_h * 0.36, islat / 2.0)
            vb.add_box(f"TruckSlat_{islat}", (0.0, gy_c + 0.038, sz),
                       (hw_f * 1.02, 0.026, 0.032), bpart, "steel" if spec.grille_style == "pickup" else "trim", bevel=0.008)
    else:
        vb.add_box("UpperGrille", (0.0, gy_c + 0.004, grille_z + 0.01),
                   (hw_f * 0.84, 0.07, grille_h * 0.62), bpart, "underbody", bevel=0.012)
        vb.add_box("IntercoolerMouth", (0.0, nose_y_at(0.0, 0.24) - 0.015, z_bot_c + 0.12),
                   (hw_f * 1.00, 0.09, 0.18), bpart, "underbody")
        vb.add_box("IntercoolerCore", (0.0, nose_y_at(0.0, 0.24) + 0.02, z_bot_c + 0.12),
                   (hw_f * 0.84, 0.035, 0.14), bpart, "steel")
        for side in (-1.0, 1.0):
            dx = side * hw_f * 0.66
            vb.add_box(f"SideIntake_{int(side)}", (dx, nose_y_at(dx, 0.24) - 0.01, z_bot_c + 0.12),
                       (0.24, 0.08, 0.15), bpart, "underbody", rot_deg=(0.0, 0.0, -side * 16.0))

    # Flush/Proud Headlights with dark bezels + bright projectors!
    hl_z = lerp(z_bot_c, z_top_c, 0.72)
    for side in (-1.0, 1.0):
        hx = side * hw_f * 0.65
        hy = nose_y_at(hx, 0.72)
        if spec.headlight_style == "round":
            # Twin round retro headlights sitting right inside the muscle grille ends!
            for off_x, r_lamp in ((0.0, 0.068), (-side * 0.155, 0.056)):
                lx = hx + off_x
                ly = max(hy, gy_c) + 0.015
                vb.add_cylinder(f"RoundBezel_{int(side)}_{off_x:.2f}",
                                (lx, ly - 0.015, hl_z - 0.01), (lx, ly + 0.036, hl_z - 0.01),
                                r_lamp * 1.16, r_lamp * 1.16, segs=18, part=bpart, role="chrome")
                vb.add_cylinder(f"RoundLamp_{int(side)}_{off_x:.2f}",
                                (lx, ly, hl_z - 0.01), (lx, ly + 0.044, hl_z - 0.01),
                                r_lamp * 0.92, r_lamp * 0.92, segs=18, part=bpart, role="headlight")
            vb.add_box(f"MuscleInd_{int(side)}", (side * hw_f * 0.66, nose_y_at(side * hw_f * 0.66, 0.25) + 0.015, z_bot_c + 0.13),
                       (0.14, 0.035, 0.042), bpart, "indicator", rot_deg=(0, 0, -side * 16.0))
        elif spec.headlight_style == "bmw":
            vb.add_box(f"BmwLampHouse_{int(side)}", (hx, hy + 0.008, hl_z),
                       (0.36, 0.085, 0.095), bpart, "trim", rot_deg=(0.0, -side * 5.0, -side * 18.0), bevel=0.016)
            for ox in (-0.075, 0.075):
                lx = hx + ox
                ly = nose_y_at(lx, 0.72) + 0.015
                vb.add_cylinder(f"BmwProj_{int(side)}_{ox:.2f}",
                                (lx, ly, hl_z), (lx, ly + 0.045, hl_z),
                                0.032, 0.032, segs=14, part=bpart, role="headlight")
            vb.add_box(f"BmwDrl_{int(side)}", (hx, hy + 0.042, hl_z - 0.026),
                       (0.30, 0.022, 0.018), bpart, "headlight", rot_deg=(0.0, -side * 5.0, -side * 18.0))
            vb.add_box(f"BmwInd_{int(side)}", (side * hw_f * 0.83, nose_y_at(side * hw_f * 0.83, 0.72) + 0.008, hl_z),
                       (0.068, 0.045, 0.044), bpart, "indicator", rot_deg=(0.0, 0.0, -side * 28.0))
        elif spec.headlight_style == "suv":
            hx_s = side * hw_f * 0.72
            hy_s = nose_y_at(hx_s, 0.72)
            vb.add_box(f"SuvLampHouse_{int(side)}", (hx_s, hy_s + 0.012, hl_z),
                       (0.28, 0.09, 0.16), bpart, "trim", rot_deg=(0, 0, -side * 16.0), bevel=0.015)
            vb.add_box(f"SuvLampLens_{int(side)}", (hx_s, hy_s + 0.048, hl_z + 0.016),
                       (0.22, 0.026, 0.092), bpart, "headlight", rot_deg=(0, 0, -side * 16.0))
            vb.add_box(f"SuvInd_{int(side)}", (hx_s, hy_s + 0.048, hl_z - 0.050),
                       (0.22, 0.026, 0.034), bpart, "indicator", rot_deg=(0, 0, -side * 16.0))
        else:
            vb.add_box(f"SportLampHouse_{int(side)}", (hx, hy + 0.010, hl_z),
                       (0.36, 0.09, 0.105), bpart, "trim", rot_deg=(0.0, 0.0, -side * 16.0), bevel=0.016)
            for ox in (-0.075, 0.075):
                lx = hx + ox
                ly = nose_y_at(lx, 0.72) + 0.015
                vb.add_cylinder(f"SportProj_{int(side)}_{ox:.2f}",
                                (lx, ly, hl_z + 0.005), (lx, ly + 0.046, hl_z + 0.005),
                                0.034, 0.034, segs=14, part=bpart, role="headlight")
            vb.add_box(f"SportDrl_{int(side)}", (hx, hy + 0.045, hl_z - 0.026),
                       (0.30, 0.022, 0.018), bpart, "headlight", rot_deg=(0.0, 0.0, -side * 16.0))
            vb.add_box(f"SportInd_{int(side)}", (side * hw_f * 0.83, nose_y_at(side * hw_f * 0.83, 0.72) + 0.010, hl_z - 0.005),
                       (0.068, 0.045, 0.052), bpart, "indicator", rot_deg=(0.0, 0.0, -side * 28.0))

    if spec.grille_style not in ("muscle", "suv"):
        vb.add_box("FrontPlate", (0.0, nose_y_at(0.0, 0.28) + 0.028, z_bot_c + 0.14),
                   (0.34, 0.016, 0.095), bpart, "plate")


def _build_rear_fascia(vb: VehicleBuilder, spec: CarSpec, ry_seam: float):
    hl = spec.half_len
    ry_rear = -hl + 0.015
    z_bot_c = spec.rocker(ry_rear)
    z_top_c = spec.belt(ry_rear)
    bpart = "Rear bumper"

    corner_pow = 0.44
    bgrid = []
    n_u, n_v = 25, 10
    for iu in range(n_u):
        s = (iu / (n_u - 1)) * 2.0 - 1.0
        ang = s * math.pi * 0.5
        cos_a = math.cos(ang)
        sin_a = math.sin(ang)
        row = []
        for iv in range(n_v):
            v = iv / (n_v - 1)
            sp = spec.side_point(1.0, ry_seam, v)
            hw_v = abs(sp[0])
            x = sin_a * hw_v
            y_base = ry_seam - (cos_a ** corner_pow) * (ry_seam - ry_rear)
            z = lerp(sp[2], lerp(z_bot_c, z_top_c + 0.018, v), cos_a ** 0.55)
            y_off = -0.028 * math.sin(v * math.pi)
            row.append((x, y_base + y_off * cos_a, z))
        bgrid.append(row)
    bumper_role = "matteBlack" if spec.is_suv else "paint"
    vb.add_grid_surface("RearBumperShell", bgrid,
                        lambda p: Vector((p.x * 0.45, -1.0, 0.12)).normalized(),
                        bpart, bumper_role, solidify=0.014)

    hw_r = abs(spec.side_point(1.0, ry_seam, 0.78)[0])
    def tail_y_at(x_val: float, v_val: float = 0.78) -> float:
        hw_v = abs(spec.side_point(1.0, ry_seam, v_val)[0])
        sin_a = clamp(abs(x_val) / max(0.1, hw_v), 0.0, 0.985)
        cos_a = math.sqrt(max(0.0, 1.0 - sin_a * sin_a))
        return ry_seam - (cos_a ** corner_pow) * (ry_seam - ry_rear) - 0.028 * math.sin(v_val * math.pi) * cos_a

    vb.add_box("RearDiffuser", (0.0, ry_rear + 0.01, z_bot_c + 0.095),
               (hw_r * 1.46, 0.10, 0.17), bpart, "matteBlack", bevel=0.015)
    for ifin in (-2, -1, 0, 1, 2):
        vb.add_box(f"DiffFin_{ifin}", (ifin * hw_r * 0.22, ry_rear - 0.02, z_bot_c + 0.085),
                   (0.022, 0.075, 0.135), bpart, "trim")
    ex_offsets = (-0.64, -0.52, 0.52, 0.64) if spec.grille_style in ("bmw", "gtr") else (-0.56, 0.56)
    for iex, ex in enumerate(ex_offsets):
        vb.add_cylinder(f"Exhaust_{iex}", (ex * hw_r, ry_rear + 0.06, z_bot_c + 0.085),
                        (ex * hw_r, ry_rear - 0.055, z_bot_c + 0.085), 0.042, 0.042, segs=12,
                        part=bpart, role="chrome")

    tl_z = lerp(z_bot_c, z_top_c, 0.78)
    for side in (-1.0, 1.0):
        tx = side * hw_r * 0.62
        ty = tail_y_at(tx, 0.78) - 0.012
        if spec.taillight_style == "gtr":
            vb.add_box(f"GtrTailPanel_{int(side)}", (tx, ty, tl_z),
                       (0.40, 0.06, 0.13), "Body", "trim", rot_deg=(0, 0, side * 10.0), bevel=0.014)
            for off_x, r_t in ((-side * 0.09, 0.046), (side * 0.08, 0.054)):
                vb.add_cylinder(f"GtrTail_{int(side)}_{off_x:.2f}",
                                (tx + off_x, ty + 0.01, tl_z), (tx + off_x, ty - 0.042, tl_z),
                                r_t, r_t, segs=14, part="Body", role="tail")
        else:
            vb.add_box(f"TailHouse_{int(side)}", (tx, ty, tl_z),
                       (0.40, 0.07, 0.105), "Body", "trim", rot_deg=(0, 0, side * 10.0), bevel=0.014)
            vb.add_box(f"TailLens_{int(side)}", (tx, ty - 0.032, tl_z + 0.008),
                       (0.34, 0.025, 0.060), "Body", "tail", rot_deg=(0, 0, side * 10.0))
            vb.add_box(f"TailInd_{int(side)}", (side * hw_r * 0.78, tail_y_at(side * hw_r * 0.78, 0.78) - 0.025, tl_z - 0.022),
                       (0.075, 0.025, 0.030), "Body", "indicator", rot_deg=(0, 0, side * 18.0))

    vb.add_box("RearPlate", (0.0, ry_rear - 0.015, lerp(z_bot_c, z_top_c, 0.50)),
               (0.34, 0.016, 0.105), bpart, "plate")


def _build_gt_wing(vb: VehicleBuilder, spec: CarSpec):
    hl = spec.half_len
    wy = -hl + 0.22
    wz = spec.belt(wy) + spec.wing_height
    span = spec.half_w(wy) * 2.0 * spec.wing_span_ratio
    wpart = "Rear wing"

    vb.add_box("WingBlade", (0.0, wy, wz), (span, 0.25, 0.030), wpart, "matteBlack",
               rot_deg=(7.0, 0.0, 0.0), bevel=0.012, smooth=True)
    for side in (-1.0, 1.0):
        sx = side * span * 0.28
        vb.add_box(f"WingStrut_{int(side)}", (sx, wy + 0.02, spec.belt(wy) + spec.wing_height * 0.5),
                   (0.022, 0.13, spec.wing_height + 0.02), wpart, "matteBlack",
                   rot_deg=(-12.0, 0.0, 0.0), bevel=0.005)
        vb.add_box(f"WingEndplate_{int(side)}", (side * (span * 0.5 + 0.008), wy, wz),
                   (0.018, 0.29, 0.135), wpart, "matteBlack", bevel=0.006)


def _build_bullbar(vb: VehicleBuilder, spec: CarSpec):
    hl = spec.half_len
    hw = spec.half_w(hl)
    fy = hl + 0.03
    z0 = spec.rocker(hl) + 0.08
    z1 = spec.hood(hl) - 0.05
    part = "Bullbar"

    vb.add_tube_path("BullbarMain", [
        (-hw * 0.62, fy - 0.06, z0),
        (-hw * 0.52, fy + 0.04, z0 + 0.05),
        (-hw * 0.44, fy + 0.06, z1),
        (hw * 0.44, fy + 0.06, z1),
        (hw * 0.52, fy + 0.04, z0 + 0.05),
        (hw * 0.62, fy - 0.06, z0),
    ], radius=0.034, segs=10, part=part, role="matteBlack")
    vb.add_cylinder("BullbarMid", (-hw * 0.48, fy + 0.055, lerp(z0, z1, 0.52)),
                    (hw * 0.48, fy + 0.055, lerp(z0, z1, 0.52)), 0.028, 0.028, segs=10, part=part, role="matteBlack")
    for side in (-1.0, 1.0):
        vb.add_box(f"BullbarUpright_{int(side)}", (side * hw * 0.30, fy + 0.04, lerp(z0, z1, 0.50)),
                   (0.045, 0.09, z1 - z0), part, "matteBlack", bevel=0.01)
    vb.add_box("SkidPlate", (0.0, fy - 0.02, z0 - 0.04),
               (hw * 0.82, 0.22, 0.035), part, "steel", rot_deg=(28.0, 0.0, 0.0), bevel=0.008)


def _build_roof_rack(vb: VehicleBuilder, spec: CarSpec):
    rz = spec.height + 0.072
    rw = spec.half_w(0.0) * spec.roof_width_ratio * 0.86
    ry0 = spec.rear_glass_top + 0.08
    ry1 = spec.front_glass_top - 0.08
    part = "Roof rack"

    vb.add_tube_path("RackPerimeter", [
        (-rw, ry0, rz), (-rw, ry1, rz), (rw, ry1, rz), (rw, ry0, rz), (-rw, ry0, rz)
    ], radius=0.020, segs=8, part=part, role="matteBlack")
    for i in range(4):
        cy = lerp(ry0 + 0.08, ry1 - 0.08, i / 3.0)
        vb.add_cylinder(f"RackCross_{i}", (-rw, cy, rz - 0.01), (rw, cy, rz - 0.01),
                        0.015, 0.015, segs=8, part=part, role="matteBlack")
    for side in (-1.0, 1.0):
        for sy in (ry0 + 0.14, ry1 - 0.14):
            vb.add_box(f"RackFoot_{int(side)}_{sy:.2f}", (side * rw, sy, rz - 0.04),
                       (0.035, 0.08, 0.07), part, "trim")
    vb.add_box("LedBarHousing", (0.0, ry1 + 0.04, rz + 0.01),
               (rw * 1.52, 0.065, 0.06), part, "matteBlack", bevel=0.012)
    vb.add_box("LedBarLens", (0.0, ry1 + 0.074, rz + 0.01),
               (rw * 1.44, 0.016, 0.038), part, "headlight")


# ---------------------------------------------------------------------------
# COV2 Binary Exporter (exact match to VehicleBodyAsset.cs & VehicleBodyAssetTests.cs)

def export_cov2(kind: str, info: dict, obs: Iterable[bpy.types.Object], coll: bpy.types.Collection | None = None) -> dict:
    # Ensure collection is visible while evaluating depsgraph!
    prev_hv = coll.hide_viewport if coll else False
    if coll:
        coll.hide_viewport = False
    bpy.context.view_layer.update()
    deps = bpy.context.evaluated_depsgraph_get()

    buckets: dict[tuple[str, str], list[bpy.types.Object]] = {}
    for ob in obs:
        if ob.type != "MESH" or not ob.get("export", True):
            continue
        key = (ob["part"], ob["role"])
        buckets.setdefault(key, []).append(ob)

    path = OUT_DIR / f"{kind}.bytes"
    totalv = totalt = 0
    max_x = max_z = 0.0
    backwards = measured = 0

    def write_str(f, s: str):
        b = s.encode("utf8")
        f.write(struct.pack("<i", len(b)))
        f.write(b)

    with path.open("wb") as f:
        f.write(b"COV2")
        f.write(struct.pack("<5f", *[info[k] for k in ("ground", "halfLength", "wheelRadius", "track", "tyreWidth")]))
        cabin = info.get("cabin") or [0.0] * 6
        f.write(struct.pack("<6f", *cabin))
        f.write(struct.pack("<i", len(buckets)))
        for (part, role), part_obs in buckets.items():
            vertices, normals, uvs, indices = [], [], [], []
            lookup = {}
            for ob in part_obs:
                ev = ob.evaluated_get(deps)
                me = ev.to_mesh()
                me.calc_loop_triangles()
                m = ev.matrix_world
                nm = m.to_3x3().inverted().transposed()
                cn = me.corner_normals
                for tri in me.loop_triangles:
                    face = []
                    for li in tri.loops:
                        loop = me.loops[li]
                        p = m @ me.vertices[loop.vertex_index].co
                        n = (nm @ cn[li].vector).normalized()
                        v = (p.x, p.z + info["ground"], p.y)
                        normal = (n.x, n.z, n.y)
                        max_x = max(max_x, abs(v[0]))
                        max_z = max(max_z, abs(v[2]))
                        k = tuple(round(x, 5) for x in (*v, *normal))
                        if k not in lookup:
                            lookup[k] = len(vertices)
                            vertices.append(v)
                            normals.append(normal)
                            if role == "plate":
                                uvs.append((
                                    (p.x - m.translation.x) / max(0.001, ob.dimensions.x) + 0.5,
                                    (p.z - m.translation.z) / max(0.001, ob.dimensions.z) + 0.5,
                                ))
                            elif abs(n.x) > abs(n.y) and abs(n.x) > abs(n.z):
                                uvs.append((p.y, p.z))
                            elif abs(n.z) > abs(n.y):
                                uvs.append((p.x, p.y))
                            else:
                                uvs.append((p.x, p.z))
                        face.append(lookup[k])
                    i0, i1, i2 = face[0], face[2], face[1]
                    indices.extend([i0, i1, i2])
                    v0, v1, v2 = Vector(vertices[i0]), Vector(vertices[i1]), Vector(vertices[i2])
                    geo = (v1 - v0).cross(v2 - v0)
                    shd = Vector(normals[i0]) + Vector(normals[i1]) + Vector(normals[i2])
                    if geo.length_squared >= 1e-14 and shd.length_squared >= 1e-14:
                        align = geo.normalized().dot(shd.normalized())
                        if abs(align) >= 0.2:
                            measured += 1
                            if align < 0.0:
                                backwards += 1
                ev.to_mesh_clear()

            write_str(f, part)
            write_str(f, role)
            f.write(struct.pack("<ii", len(vertices), len(indices)))
            for p, n, t in zip(vertices, normals, uvs):
                f.write(struct.pack("<8f", *p, *n, *t))
            f.write(struct.pack("<" + "i" * len(indices), *indices))
            totalv += len(vertices)
            totalt += len(indices) // 3

    if coll:
        coll.hide_viewport = prev_hv

    back_ratio = backwards / max(1, measured)
    assert len(buckets) <= 128, f"{kind}: section count {len(buckets)} > 128"
    assert 10000 <= totalv <= 50000, f"{kind}: vertices {totalv} outside [10000, 50000]"
    assert totalt < 80000, f"{kind}: triangles {totalt} >= 80000"
    assert max_x < 1.6, f"{kind}: max |x| {max_x:.3f} >= 1.6"
    assert max_z < info["halfLength"] + 0.2, f"{kind}: max |z| {max_z:.3f} >= {info['halfLength'] + 0.2:.3f}"
    assert back_ratio < 0.04, f"{kind}: backwards normals ratio {back_ratio:.4f} >= 0.04"
    return dict(
        file=path.name,
        vertices=totalv,
        triangles=totalt,
        draws=len(buckets),
        bytes=path.stat().st_size,
        max_x=round(max_x, 3),
        max_z=round(max_z, 3),
        back_ratio=round(back_ratio, 4),
    )


# ---------------------------------------------------------------------------
# Studio Lighting & EEVEE Showroom Setup

def reset_scene():
    scene = bpy.context.scene
    for coll in list(scene.collection.children):
        scene.collection.children.unlink(coll)
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    for coll in list(bpy.data.collections):
        bpy.data.collections.remove(coll)
    for me in list(bpy.data.meshes):
        bpy.data.meshes.remove(me)
    for mat in list(bpy.data.materials):
        bpy.data.materials.remove(mat)
    for light in list(bpy.data.lights):
        bpy.data.lights.remove(light)
    for cam in list(bpy.data.cameras):
        bpy.data.cameras.remove(cam)
    for w in list(bpy.data.worlds):
        if w != scene.world:
            bpy.data.worlds.remove(w)


def setup_studio() -> bpy.types.Object:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 800
    scene.render.resolution_percentage = 100
    scene.view_settings.view_transform = "AgX"
    try:
        scene.view_settings.look = "AgX - Punchy"
    except Exception:
        pass

    world = bpy.data.worlds.new("StudioWorld")
    scene.world = world
    world.use_nodes = True
    nt = world.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputWorld")
    bg = nt.nodes.new("ShaderNodeBackground")
    bg.inputs["Strength"].default_value = 1.65
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].position = 0.0
    ramp.color_ramp.elements[0].color = (0.04, 0.05, 0.07, 1.0)
    ramp.color_ramp.elements[1].position = 0.52
    ramp.color_ramp.elements[1].color = (0.18, 0.21, 0.26, 1.0)
    e2 = ramp.color_ramp.elements.new(0.84)
    e2.color = (0.98, 0.96, 0.92, 1.0)
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    tex = nt.nodes.new("ShaderNodeTexCoord")
    nt.links.new(tex.outputs["Generated"], sep.inputs["Vector"])
    nt.links.new(sep.outputs["Z"], ramp.inputs["Fac"])
    nt.links.new(ramp.outputs["Color"], bg.inputs["Color"])
    nt.links.new(bg.outputs["Background"], out.inputs["Surface"])

    bm_fl = bmesh.new()
    bmesh.ops.create_grid(bm_fl, x_segments=1, y_segments=1, size=80.0)
    fl_mesh = bpy.data.meshes.new("StudioFloor")
    bm_fl.to_mesh(fl_mesh)
    bm_fl.free()
    floor = bpy.data.objects.new("StudioFloor", fl_mesh)
    floor.location = (0, 0, -0.003)
    scene.collection.objects.link(floor)
    fl_mat = bpy.data.materials.new("StudioFloorMat")
    fl_mat.use_nodes = True
    bsdf = fl_mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (0.065, 0.070, 0.082, 1.0)
    bsdf.inputs["Metallic"].default_value = 0.12
    bsdf.inputs["Roughness"].default_value = 0.34
    floor.data.materials.append(fl_mat)

    for name, loc, power, size, col in [
        ("KeySoftbox", (2.8, 5.2, 6.2), 2800, 7.5, (1.0, 0.96, 0.90)),
        ("RimStrip", (-5.5, -4.0, 3.8), 2200, 6.0, (0.80, 0.92, 1.0)),
        ("FillCard", (5.8, 4.5, 2.6), 1200, 5.5, (0.96, 0.97, 1.0)),
    ]:
        ldata = bpy.data.lights.new(name, "AREA")
        ldata.energy = power
        ldata.color = col
        ldata.shape = "RECTANGLE"
        ldata.size = size
        ldata.size_y = size * 0.42
        lo = bpy.data.objects.new(name, ldata)
        scene.collection.objects.link(lo)
        lo.location = loc
        lo.rotation_euler = (Vector((0, 0, 0.65)) - lo.location).to_track_quat("-Z", "Y").to_euler()

    cdata = bpy.data.cameras.new("ReviewCamera")
    cdata.lens = 52
    cam = bpy.data.objects.new("ReviewCamera", cdata)
    scene.collection.objects.link(cam)
    scene.camera = cam
    return cam


def aim_camera(cam: bpy.types.Object, eye: tuple[float, float, float], target: tuple[float, float, float]):
    cam.location = eye
    cam.rotation_euler = (Vector(target) - Vector(eye)).to_track_quat("-Z", "Y").to_euler()

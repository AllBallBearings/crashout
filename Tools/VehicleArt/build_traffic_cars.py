"""
Builds all 6 traffic vehicles in the same realistic, crash-ready automotive style as the reference photo:
  1. Sedan  -> Sleek modern 4-door sport sedan
  2. Hatch  -> Sporty 5-door hot hatch
  3. Van    -> Modern white high-roof commercial delivery van (Sprinter/Transit style)
  4. Pickup -> Rugged crew-cab utility pickup truck with bullbar & flares
  5. Bus    -> Blue & white city transit bus with panoramic glass, roof A/C & rear wing (from photo!)
  6. Tanker -> Long-nose Peterbilt-style semi tractor with chrome stacks + gleaming stainless tanker (from photo!)
"""

from __future__ import annotations

import math
from mathutils import Vector

from vehicle_core import (
    CarSpec,
    VehicleBuilder,
    build_sculpted_car,
    lerp,
)


def build_sedan() -> tuple[VehicleBuilder, dict]:
    spec = CarSpec(
        kind="Sedan",
        length=4.36,
        width=1.84,
        height=1.40,
        ground=-0.29,
        wheel_r=0.31,
        tyre_w=0.22,
        track=0.79,
        front_axle=1.30,
        rear_axle=-1.30,
        width_keys=[
            (-2.18, 0.78), (-1.92, 0.88), (-1.30, 0.91), (0.0, 0.90),
            (1.30, 0.91), (1.94, 0.86), (2.18, 0.75),
        ],
        belt_keys=[
            (-2.18, 0.90), (-1.75, 0.92), (0.60, 0.87), (1.30, 0.83),
            (1.92, 0.74), (2.18, 0.58),
        ],
        hood_keys=[
            (0.60, 0.88), (1.15, 0.83), (1.72, 0.75), (2.06, 0.66), (2.18, 0.57),
        ],
        rocker_keys=[
            (-2.18, 0.22), (-1.75, 0.16), (1.75, 0.15), (2.18, 0.17),
        ],
        rear_glass_base=-1.52,
        rear_glass_top=-0.92,
        front_glass_top=0.28,
        front_glass_base=0.82,
        roof_width_ratio=0.76,
        paint_rgb=(0.14, 0.28, 0.48),
        paint_metal=0.76,
        paint_rough=0.16,
        wheel_rgb=(0.68, 0.70, 0.74),
        wheel_spokes=5,
        doors_per_side=2,
        hood_style="clean",
        grille_style="sport",
        headlight_style="sport",
        taillight_style="sport",
        flare_style="blister",
        flare_out=0.036,
        has_lip_spoiler=True,
        has_chin_splitter=True,
        side_skirt_drop=0.015,
        tumblehome=0.062,
        shoulder_crease=0.028,
    )
    return build_sculpted_car(spec)


def build_hatch() -> tuple[VehicleBuilder, dict]:
    spec = CarSpec(
        kind="Hatch",
        length=3.86,
        width=1.76,
        height=1.45,
        ground=-0.29,
        wheel_r=0.30,
        tyre_w=0.21,
        track=0.75,
        front_axle=1.17,
        rear_axle=-1.17,
        width_keys=[
            (-1.93, 0.76), (-1.68, 0.84), (-1.17, 0.87), (0.0, 0.86),
            (1.17, 0.87), (1.70, 0.82), (1.93, 0.72),
        ],
        belt_keys=[
            (-1.93, 0.94), (-1.55, 0.95), (0.55, 0.89), (1.17, 0.85),
            (1.70, 0.75), (1.93, 0.59),
        ],
        hood_keys=[
            (0.55, 0.90), (1.05, 0.85), (1.52, 0.77), (1.82, 0.67), (1.93, 0.58),
        ],
        rocker_keys=[
            (-1.93, 0.22), (-1.55, 0.16), (1.55, 0.15), (1.93, 0.17),
        ],
        rear_glass_base=-1.78,
        rear_glass_top=-1.36,
        front_glass_top=0.22,
        front_glass_base=0.76,
        roof_width_ratio=0.80,
        paint_rgb=(0.68, 0.71, 0.75),
        paint_metal=0.72,
        paint_rough=0.18,
        wheel_rgb=(0.24, 0.25, 0.28),
        wheel_spokes=6,
        doors_per_side=2,
        hood_style="clean",
        grille_style="sport",
        headlight_style="sport",
        taillight_style="sport",
        flare_style="blister",
        flare_out=0.038,
        has_lip_spoiler=False,
        has_chin_splitter=True,
        is_hatchback=True,
        side_skirt_drop=0.015,
        tumblehome=0.055,
        shoulder_crease=0.025,
    )
    vb, info = build_sculpted_car(spec)
    # Sporty hatchback rear roof spoiler
    vb.add_box("HatchRoofSpoiler", (0.0, -1.46, 1.44), (1.28, 0.22, 0.045),
               "Roof panel", "matteBlack", rot_deg=(10.0, 0.0, 0.0), bevel=0.012)
    return vb, info


def build_pickup() -> tuple[VehicleBuilder, dict]:
    spec = CarSpec(
        kind="Pickup",
        length=4.84,
        width=1.98,
        height=1.78,
        ground=-0.29,
        wheel_r=0.36,
        tyre_w=0.25,
        track=0.86,
        front_axle=1.50,
        rear_axle=-1.50,
        width_keys=[
            (-2.42, 0.92), (-2.12, 0.96), (-1.50, 0.98), (0.0, 0.97),
            (1.50, 0.98), (2.12, 0.95), (2.42, 0.88),
        ],
        belt_keys=[
            (-2.42, 1.10), (-0.45, 1.12), (0.85, 1.13), (1.50, 1.12),
            (2.14, 1.07), (2.42, 0.96),
        ],
        hood_keys=[
            (0.85, 1.14), (1.45, 1.13), (2.06, 1.08), (2.42, 0.96),
        ],
        rocker_keys=[
            (-2.42, 0.36), (-2.00, 0.30), (2.00, 0.30), (2.42, 0.34),
        ],
        rear_glass_base=-0.46,
        rear_glass_top=-0.38,
        front_glass_top=0.58,
        front_glass_base=1.10,
        roof_width_ratio=0.84,
        paint_rgb=(0.22, 0.25, 0.29),
        paint_metal=0.64,
        paint_rough=0.22,
        wheel_rgb=(0.18, 0.19, 0.21),
        wheel_spokes=6,
        offroad_tyres=True,
        doors_per_side=2,
        hood_style="rugged",
        grille_style="pickup",
        headlight_style="suv",
        taillight_style="suv",
        flare_style="rugged_black",
        flare_out=0.064,
        has_chin_splitter=False,
        has_bullbar=True,
        is_pickup=True,
        bed_start_y=-0.46,
        side_skirt_drop=0.0,
        tumblehome=0.042,
        shoulder_crease=0.028,
    )
    vb, info = build_sculpted_car(spec)
    # Sport rollbar in pickup bed
    vb.add_tube_path("BedRollbar", [
        (-0.76, -0.58, 1.10), (-0.70, -0.58, 1.72), (0.70, -0.58, 1.72), (0.76, -0.58, 1.10)
    ], radius=0.032, segs=8, part="Body", role="matteBlack")
    for side in (-1.0, 1.0):
        vb.add_cylinder(f"BedRollStrut_{int(side)}",
                        (side * 0.70, -0.58, 1.66), (side * 0.76, -1.25, 1.12),
                        0.026, 0.026, segs=8, part="Body", role="matteBlack")
    return vb, info


def build_van() -> tuple[VehicleBuilder, dict]:
    """Modern white high-roof Sprinter/Transit commercial delivery van (like the photo)."""
    vb = VehicleBuilder("Van", (0.91, 0.92, 0.94), paint_metal=0.14, paint_rough=0.22,
                        paint_coat=0.88, wheel_rgb=(0.62, 0.64, 0.68))
    hl = 2.32
    hw = 0.96
    wr = 0.34
    tw = 0.23
    track = 0.85
    fa, ra = 1.42, -1.42
    ar = wr + 0.068
    roof_z = 2.08
    belt_z = 1.12
    rock_z = 0.26

    # Underbody chassis, crash beams, interior cab
    vb.add_box("VanChassis", (0.0, 0.0, rock_z + 0.09), (1.58, 4.10, 0.14), "Body", "underbody")
    for side in (-1.0, 1.0):
        vb.add_box(f"VanRail_{int(side)}", (side * 0.46, 0.0, rock_z + 0.15), (0.11, 4.30, 0.14), "Body", "underbody")
    vb.add_box("VanFrontBeam", (0.0, hl - 0.18, rock_z + 0.24), (1.50, 0.10, 0.14), "Body", "steel")
    vb.add_box("VanDash", (0.0, 1.56, 1.02), (1.64, 0.34, 0.24), "Body", "interior", bevel=0.03)
    for side in (-1.0, 1.0):
        vb.add_box(f"VanSeat_{int(side)}", (side * 0.42, 1.05, 0.78), (0.46, 0.46, 0.62), "Body", "interior", bevel=0.03)
    vb.add_box("CargoBulkhead", (0.0, 0.68, 1.24), (1.76, 0.08, 1.50), "Body", "trim")

    # Wheel tubs
    for side in (-1.0, 1.0):
        for wn, ay in (("F", fa), ("R", ra)):
            tg = []
            for iu in range(19):
                ang = math.pi * iu / 18.0
                wy = ay + math.cos(ang) * ar
                wz = wr + math.sin(ang) * ar
                tg.append([(side * (track - tw * 0.7), wy, wz), (side * (hw - 0.01), wy, wz)])
            vb.add_grid_surface(f"VanTub_{wn}_{int(side)}", tg,
                                lambda p, ay=ay: Vector((0, ay - p.y, wr - p.z)).normalized(),
                                "Body", "underbody")

    def van_side_bot(y: float) -> float:
        z = rock_z
        for ay in (fa, ra):
            dy = y - ay
            if abs(dy) < ar:
                z = max(z, wr + math.sqrt(max(0.0, ar * ar - dy * dy)))
        return min(z, belt_z - 0.06)

    def van_side_pt(side: float, y: float, z: float) -> tuple[float, float, float]:
        # Subtle automotive taper toward nose and roof tumblehome
        nose_t = max(0.0, (y - 1.50) / (hl - 1.50))
        w = hw * (1.0 - 0.07 * (nose_t ** 1.8))
        if z > belt_z:
            u = (z - belt_z) / (roof_z - belt_z)
            w -= 0.085 * (u ** 1.5)
        else:
            u = (z - rock_z) / max(0.1, belt_z - rock_z)
            w += 0.015 * math.sin(u * math.pi) - 0.035 * ((1.0 - u) ** 2.4)
        return (side * w, y, z)

    # Sculpted cargo sides + front doors + front fenders
    for side in (-1.0, 1.0):
        sn = "Right" if side > 0 else "Left"
        ov = Vector((side, 0, 0.05)).normalized()
        # Main rear cargo wall (Body) from -hl+0.12 to 0.72
        cgrid = []
        for iy in range(40):
            y = lerp(-hl + 0.12, 0.72, iy / 39.0)
            z0 = van_side_bot(y)
            cgrid.append([van_side_pt(side, y, lerp(z0, roof_z, iv / 15.0)) for iv in range(16)])
        vb.add_grid_surface(f"{sn}CargoWall", cgrid, ov, "Body", "paint", solidify=0.013)
        # Black lower rub-strip molding along van side (strictly between wheel arches!)
        vb.add_box(f"{sn}RubStrip", (side * (hw + 0.005), 0.0, rock_z + 0.24),
                   (0.032, 1.88, 0.14), "Body", "matteBlack", bevel=0.008)
        # Front cab door (DETACHABLE!) from 0.725 to 1.62
        dgrid = []
        for iy in range(20):
            y = lerp(0.725, 1.62, iy / 19.0)
            z0 = van_side_bot(y)
            dgrid.append([van_side_pt(side, y, lerp(z0, belt_z, iv / 10.0)) for iv in range(11)])
        vb.add_grid_surface(f"{sn}CabDoor", dgrid, ov, f"{sn} front door", "paint", solidify=0.012)
        vb.add_box(f"{sn}DoorHandle", (side * (hw + 0.01), 0.90, belt_z - 0.14),
                   (0.025, 0.14, 0.04), f"{sn} front door", "trim", bevel=0.008)
        # Front fender (DETACHABLE!) from 1.625 to hl - 0.18
        fgrid = []
        for iy in range(20):
            y = lerp(1.625, hl - 0.18, iy / 19.0)
            z0 = van_side_bot(y)
            z_top = lerp(belt_z, 0.92, (y - 1.625) / (hl - 0.18 - 1.625))
            fgrid.append([van_side_pt(side, y, lerp(z0, z_top, iv / 10.0)) for iv in range(11)])
        vb.add_grid_surface(f"{sn}VanFender", fgrid, ov, f"{sn} front fender", "paint", solidify=0.012)
        # Cab side window glass + A-pillar
        wg = []
        for iy in range(10):
            y = lerp(0.76, 1.62, iy / 9.0)
            z_hi = lerp(roof_z - 0.08, belt_z + 0.16, max(0.0, (y - 1.15) / 0.62))
            p0 = van_side_pt(side, y, belt_z + 0.02)
            p1 = van_side_pt(side, y, z_hi)
            wg.append([(p0[0] - side * 0.015, y, p0[2]), (p1[0] - side * 0.015, y, p1[2])])
        vb.add_grid_surface(f"{sn}CabGlass", wg, ov, "Body", "glass")
        vb.add_cylinder(f"{sn}VanAPillar", van_side_pt(side, 1.66, belt_z), van_side_pt(side, 1.18, roof_z),
                        0.042, 0.038, segs=8, part="Body", role="paint")
        # Side mirrors (DETACHABLE!)
        vb.add_box(f"{sn}VanMirror", (side * (hw + 0.13), 1.48, belt_z + 0.14),
                   (0.11, 0.12, 0.24), f"{sn} mirror", "matteBlack", bevel=0.02)
        # Wheel arch trim lips
        for wn, ay, fpart in (("F", fa, f"{sn} front fender"), ("R", ra, "Body")):
            fg = []
            for ia in range(20):
                ang = lerp(math.radians(-4), math.radians(184), ia / 19.0)
                wy = ay + math.cos(ang) * ar
                wz = max(rock_z, wr + math.sin(ang) * ar)
                fg.append([(side * (hw - 0.01), wy, wz), (side * (hw + 0.038), wy, wz + 0.01),
                           (side * (hw + 0.01), ay + math.cos(ang) * (ar + 0.065), max(rock_z, wr + math.sin(ang) * (ar + 0.065)))])
            vb.add_grid_surface(f"{sn}VanFlare_{wn}", fg, ov, fpart, "matteBlack", solidify=0.008)

    # Sloped Hood (DETACHABLE!)
    hgrid = []
    for iy in range(14):
        u = iy / 13.0
        y = lerp(1.64, hl - 0.08, u)
        w = hw * lerp(0.97, 0.90, u)
        z = lerp(belt_z, 0.90, u ** 1.15)
        hgrid.append([(v * w, y, z + 0.025 * (1.0 - v * v)) for v in [ix / 12.0 * 2 - 1 for ix in range(13)]])
    vb.add_grid_surface("VanHood", hgrid, Vector((0, 0.3, 0.95)).normalized(), "Hood", "paint", solidify=0.012)

    # Sloped Windshield
    wgrid = []
    for iy in range(10):
        u = iy / 9.0
        y = lerp(1.18, 1.66, u)
        z = lerp(roof_z - 0.04, belt_z + 0.01, u)
        w = lerp(hw - 0.11, hw - 0.04, u)
        wgrid.append([(v * w, y + 0.035 * (1 - v * v), z) for v in [ix / 12.0 * 2 - 1 for ix in range(13)]])
    vb.add_grid_surface("VanWindshield", wgrid, Vector((0, 0.8, 0.6)).normalized(), "Body", "glass")

    # Roof Panel (DETACHABLE!)
    rgrid = []
    for iy in range(30):
        y = lerp(-hl + 0.12, 1.18, iy / 29.0)
        w = hw - 0.085
        rgrid.append([(v * w, y, roof_z + 0.04 * (1 - v * v)) for v in [ix / 16.0 * 2 - 1 for ix in range(17)]])
    vb.add_grid_surface("VanRoof", rgrid, Vector((0, 0, 1)), "Roof panel", "paint", solidify=0.012)

    # Front Bumper + Modern Black Commercial Grille + Headlights (DETACHABLE!)
    vb.add_box("VanFrontBumper", (0.0, hl - 0.08, rock_z + 0.20), (hw * 1.88, 0.24, 0.36),
               "Front bumper", "matteBlack", bevel=0.04, smooth=True)
    vb.add_box("VanNosePanel", (0.0, hl - 0.10, 0.72), (hw * 1.78, 0.16, 0.36),
               "Front bumper", "paint", bevel=0.03)
    vb.add_box("VanGrille", (0.0, hl - 0.01, 0.72), (1.06, 0.05, 0.30),
               "Front bumper", "matteBlack", bevel=0.015)
    for islat in range(3):
        vb.add_box(f"VanSlat_{islat}", (0.0, hl + 0.018, 0.62 + islat * 0.10),
                   (0.96, 0.02, 0.025), "Front bumper", "chrome")
    for side in (-1.0, 1.0):
        vb.add_box(f"VanHLHouse_{int(side)}", (side * 0.70, hl - 0.02, 0.76),
                   (0.28, 0.08, 0.18), "Front bumper", "trim", rot_deg=(0, 0, -side * 12), bevel=0.015)
        vb.add_box(f"VanHL_{int(side)}", (side * 0.70, hl + 0.022, 0.78),
                   (0.22, 0.022, 0.11), "Front bumper", "headlight", rot_deg=(0, 0, -side * 12))
        vb.add_box(f"VanInd_{int(side)}", (side * 0.70, hl + 0.022, 0.69),
                   (0.22, 0.022, 0.038), "Front bumper", "indicator", rot_deg=(0, 0, -side * 12))
    vb.add_box("VanFrontPlate", (0.0, hl + 0.045, rock_z + 0.20), (0.36, 0.018, 0.10), "Front bumper", "plate")

    # Rear Double Cargo Doors + Bumper + Tall Taillights
    vb.add_box("VanRearBumper", (0.0, -hl + 0.06, rock_z + 0.14), (hw * 1.92, 0.20, 0.26),
               "Rear bumper", "matteBlack", bevel=0.03)
    vb.add_box("VanRearDoors", (0.0, -hl + 0.11, 1.24), (hw * 1.82, 0.08, 1.62),
               "Trunk lid", "paint", bevel=0.02)
    vb.add_box("VanRearSeam", (0.0, -hl + 0.065, 1.24), (0.018, 0.02, 1.56), "Trunk lid", "trim")
    for side in (-1.0, 1.0):
        vb.add_box(f"VanTail_{int(side)}", (side * 0.84, -hl + 0.06, 0.96),
                   (0.11, 0.05, 0.48), "Body", "tail", bevel=0.012)
    vb.add_box("VanRearPlate", (0.0, -hl + 0.055, 0.72), (0.36, 0.018, 0.11), "Trunk lid", "plate")

    vb.add_preview_wheels([(-track, fa), (track, fa), (-track, ra), (track, ra)], wr, tw, spokes=5)
    info = dict(ground=-0.35, halfLength=hl, wheelRadius=wr, track=track, tyreWidth=tw,
                cabin=[0.0, 1.48, -0.35, 1.82, 1.22, 4.10])
    return vb, info


def build_bus() -> tuple[VehicleBuilder, dict]:
    """Blue & white modern city transit bus with panoramic glass, roof A/C & sporty rear wing (from photo!)."""
    vb = VehicleBuilder("Bus", (0.92, 0.93, 0.95), paint_metal=0.14, paint_rough=0.20,
                        paint_coat=0.90, accent_rgb=(0.12, 0.42, 0.78), wheel_rgb=(0.56, 0.59, 0.64))
    hl = 3.70
    hw = 1.15
    wr = 0.50
    tw = 0.30
    track = 1.02
    fa, ra = 2.40, -2.40
    ar = wr + 0.075
    rock_z = 0.34
    belt_z = 1.32
    roof_z = 2.68

    # Heavy chassis frame + interior seats + floor
    vb.add_box("BusChassis", (0.0, 0.0, rock_z + 0.10), (2.06, 6.90, 0.18), "Body", "underbody")
    for side in (-1.0, 1.0):
        vb.add_box(f"BusRail_{int(side)}", (side * 0.56, 0.0, rock_z + 0.18), (0.14, 7.10, 0.18), "Body", "steel")
    # Bus interior passenger seats
    for row_i in range(6):
        sy = lerp(-2.6, 1.8, row_i / 5.0)
        for side in (-1.0, 1.0):
            vb.add_box(f"BusSeat_{row_i}_{int(side)}", (side * 0.68, sy, 1.02),
                       (0.56, 0.46, 0.62), "Body", "interior", bevel=0.03)
    vb.add_box("BusDash", (0.0, 3.16, 1.15), (2.04, 0.48, 0.32), "Body", "interior", bevel=0.03)

    # Wheel tubs
    for side in (-1.0, 1.0):
        for wn, ay in (("F", fa), ("R", ra)):
            tg = []
            for iu in range(19):
                ang = math.pi * iu / 18.0
                wy = ay + math.cos(ang) * ar
                wz = wr + math.sin(ang) * ar
                tg.append([(side * (track - tw * 0.75), wy, wz), (side * (hw - 0.01), wy, wz)])
            vb.add_grid_surface(f"BusTub_{wn}_{int(side)}", tg,
                                lambda p, ay=ay: Vector((0, ay - p.y, wr - p.z)).normalized(),
                                "Body", "underbody")

    def bus_bot(y: float) -> float:
        z = rock_z
        for ay in (fa, ra):
            dy = y - ay
            if abs(dy) < ar:
                z = max(z, wr + math.sqrt(max(0.0, ar * ar - dy * dy)))
        return min(z, belt_z - 0.08)

    # Lower body sides (white + blue transit livery stripe!) + upper window band + pillars
    for side in (-1.0, 1.0):
        sn = "Right" if side > 0 else "Left"
        ov = Vector((side, 0, 0)).normalized()
        # Lower painted side skirt (white)
        sgrid = []
        for iy in range(60):
            y = lerp(-hl + 0.14, hl - 0.14, iy / 59.0)
            z0 = bus_bot(y)
            sgrid.append([(side * hw, y, lerp(z0, belt_z, iv / 12.0)) for iv in range(13)])
        vb.add_grid_surface(f"{sn}BusLowerSide", sgrid, ov, "Body", "paint", solidify=0.015)

        # Signature blue transit livery band (lower blue panel + upper blue brow like the photo!)
        vb.add_box(f"{sn}BlueStripe", (side * (hw + 0.008), -0.15, belt_z - 0.18),
                   (0.022, 6.70, 0.34), "Body", "accent", bevel=0.006)
        vb.add_box(f"{sn}BlueHeader", (side * (hw - 0.015), 0.0, roof_z - 0.16),
                   (0.05, 7.12, 0.30), "Body", "accent", bevel=0.01)

        # Large continuous tinted side window band
        vb.add_box(f"{sn}BusSideGlass", (side * (hw - 0.03), -0.05, lerp(belt_z, roof_z - 0.28, 0.5)),
                   (0.03, 6.86, roof_z - 0.28 - belt_z), "Body", "glass")
        # Vertical window mullions (6 bay windows)
        for im in range(7):
            my = lerp(-3.35, 3.25, im / 6.0)
            vb.add_box(f"{sn}BusMullion_{im}", (side * (hw - 0.008), my, lerp(belt_z, roof_z - 0.24, 0.5)),
                       (0.045, 0.085, roof_z - 0.22 - belt_z), "Body", "trim")

        # Bi-fold transit entry door detail on right side front
        if side > 0:
            vb.add_box("BusEntryDoor", (hw + 0.006, 2.95, 1.32), (0.04, 0.82, 1.98),
                       "Right front door", "trim", bevel=0.01)
            vb.add_box("BusEntryGlass", (hw + 0.022, 2.95, 1.48), (0.02, 0.66, 1.42),
                       "Right front door", "glass")

        # Bus wing mirrors on stalk arms (DETACHABLE!)
        vb.add_tube_path(f"{sn}BusMirrorArm", [
            (side * hw, hl - 0.22, 2.18),
            (side * (hw + 0.22), hl - 0.05, 2.14),
            (side * (hw + 0.24), hl - 0.02, 1.92),
        ], radius=0.018, segs=8, part=f"{sn} mirror", role="trim")
        vb.add_box(f"{sn}BusMirrorHead", (side * (hw + 0.24), hl - 0.02, 1.84),
                   (0.09, 0.11, 0.28), f"{sn} mirror", "matteBlack", bevel=0.018)

    # Roof Panel (DETACHABLE!) + Rooftop A/C Pod + Sporty Rear Roof Wing (just like the photo!)
    rgrid = []
    for iy in range(36):
        y = lerp(-hl + 0.12, hl - 0.12, iy / 35.0)
        rgrid.append([(v * (hw - 0.02), y, roof_z + 0.055 * (1 - v * v)) for v in [ix / 18.0 * 2 - 1 for ix in range(19)]])
    vb.add_grid_surface("BusRoof", rgrid, Vector((0, 0, 1)), "Roof panel", "paint", solidify=0.016)
    vb.add_box("BusAcPod", (0.0, -0.30, roof_z + 0.16), (1.46, 2.65, 0.24), "Roof panel", "paint", bevel=0.06, smooth=True)
    vb.add_box("BusAcVents", (0.0, -0.30, roof_z + 0.285), (1.10, 2.10, 0.03), "Roof panel", "trim")
    # Sporty matte-black rear roof wing (direct nod to the photo's awesome bus!)
    vb.add_box("BusRearWingBlade", (0.0, -hl + 0.34, roof_z + 0.30), (hw * 1.96, 0.34, 0.045),
               "Rear wing", "matteBlack", rot_deg=(8.0, 0, 0), bevel=0.014, smooth=True)
    for side in (-1.0, 1.0):
        vb.add_box(f"BusWingStrut_{int(side)}", (side * 0.72, -hl + 0.38, roof_z + 0.15),
                   (0.04, 0.20, 0.28), "Rear wing", "matteBlack")
        vb.add_box(f"BusWingEnd_{int(side)}", (side * (hw * 0.98 + 0.01), -hl + 0.34, roof_z + 0.30),
                   (0.025, 0.38, 0.18), "Rear wing", "matteBlack")

    # Panoramic Front Windshield + LED Destination Header + Blue Brow + Headlights + Bumper
    vb.add_box("BusFrontLower", (0.0, hl - 0.08, lerp(rock_z + 0.24, belt_z, 0.5)),
               (hw * 1.96, 0.18, belt_z - rock_z - 0.24), "Body", "paint", bevel=0.04)
    vb.add_box("BusFrontBlueMask", (0.0, hl + 0.005, belt_z - 0.18),
               (hw * 1.94, 0.04, 0.34), "Body", "accent", bevel=0.01)
    vb.add_box("BusFrontWindshield", (0.0, hl - 0.05, lerp(belt_z, roof_z - 0.34, 0.5)),
               (hw * 1.84, 0.08, roof_z - 0.34 - belt_z), "Body", "glass", rot_deg=(-5.0, 0, 0))
    vb.add_box("BusDestHousing", (0.0, hl - 0.11, roof_z - 0.16),
               (hw * 1.92, 0.18, 0.32), "Body", "accent", bevel=0.04)
    vb.add_box("BusDestSign", (0.0, hl - 0.01, roof_z - 0.16),
               (1.56, 0.03, 0.18), "Body", "gold")
    vb.add_box("BusFrontBumper", (0.0, hl + 0.02, rock_z + 0.14),
               (hw * 2.02, 0.26, 0.26), "Front bumper", "matteBlack", bevel=0.04)
    for side in (-1.0, 1.0):
        vb.add_box(f"BusHLBezel_{int(side)}", (side * 0.84, hl + 0.01, rock_z + 0.42),
                   (0.38, 0.06, 0.18), "Front bumper", "trim", bevel=0.015)
        vb.add_box(f"BusHL_{int(side)}", (side * 0.84, hl + 0.042, rock_z + 0.44),
                   (0.30, 0.02, 0.095), "Front bumper", "headlight")
        vb.add_box(f"BusInd_{int(side)}", (side * 0.84, hl + 0.042, rock_z + 0.36),
                   (0.30, 0.02, 0.038), "Front bumper", "indicator")
    vb.add_box("BusFrontPlate", (0.0, hl + 0.155, rock_z + 0.14), (0.38, 0.018, 0.11), "Front bumper", "plate")

    # Rear Engine Panel + Bumper + Taillights
    vb.add_box("BusRearWall", (0.0, -hl + 0.08, lerp(rock_z + 0.20, roof_z, 0.5)),
               (hw * 1.96, 0.18, roof_z - rock_z - 0.20), "Body", "paint", bevel=0.04)
    vb.add_box("BusRearBumper", (0.0, -hl - 0.02, rock_z + 0.14),
               (hw * 2.02, 0.24, 0.26), "Rear bumper", "matteBlack", bevel=0.04)
    for side in (-1.0, 1.0):
        vb.add_box(f"BusTail_{int(side)}", (side * 0.90, -hl - 0.02, 1.05),
                   (0.16, 0.05, 0.52), "Body", "tail", bevel=0.012)
    vb.add_box("BusRearPlate", (0.0, -hl - 0.03, 0.82), (0.38, 0.018, 0.11), "Rear bumper", "plate")

    vb.add_preview_wheels([(-track, fa), (track, fa), (-track, ra), (track, ra)], wr, tw, spokes=8)
    info = dict(ground=-1.15, halfLength=hl, wheelRadius=wr, track=track, tyreWidth=tw,
                cabin=[0.0, 1.50, 0.0, 2.26, 2.20, 7.20])
    return vb, info


def build_tanker() -> tuple[VehicleBuilder, dict]:
    """Long-nose Peterbilt-style semi tractor + gleaming stainless/chrome cylindrical tanker (from photo!)."""
    vb = VehicleBuilder("Tanker", (0.14, 0.16, 0.20), paint_metal=0.68, paint_rough=0.22,
                        paint_coat=0.85, wheel_rgb=(0.76, 0.78, 0.82))
    hl = 3.98
    wr = 0.50
    tw = 0.30
    track = 1.02
    fa, ra1, ra2 = 3.00, -1.60, -3.00

    # Heavy Ladder Frame Chassis + Saddle Fuel Tanks
    for side in (-1.0, 1.0):
        vb.add_box(f"SemiRail_{int(side)}", (side * 0.44, -0.05, 0.72), (0.14, 7.65, 0.24), "Body", "underbody")
        # Polished cylindrical saddle fuel tanks under cab!
        vb.add_cylinder(f"FuelTank_{int(side)}", (side * 0.86, 0.45, 0.62), (side * 0.86, 1.65, 0.62),
                        0.28, 0.28, segs=18, part="Body", role="chrome")
        # Cab steps
        vb.add_box(f"CabStep_{int(side)}", (side * 1.02, 1.25, 0.54), (0.14, 0.72, 0.04), "Body", "steel")

    # Long-Nose Sculpted Tractor Hood (DETACHABLE!)
    vb.add_box("SemiHoodMain", (0.0, 2.86, 1.56), (1.38, 1.88, 0.96), "Hood", "paint", bevel=0.10, smooth=True)
    # Bold vertical slatted Peterbilt Chrome Grille!
    vb.add_box("SemiGrilleShell", (0.0, 3.80, 1.52), (1.18, 0.14, 1.04), "Front bumper", "chrome", bevel=0.04)
    vb.add_box("SemiGrilleCore", (0.0, 3.84, 1.52), (1.02, 0.08, 0.88), "Front bumper", "underbody")
    for islat in range(7):
        sx = lerp(-0.42, 0.42, islat / 6.0)
        vb.add_box(f"SemiGrilleBar_{islat}", (sx, 3.88, 1.52), (0.032, 0.03, 0.84), "Front bumper", "chrome")

    # Heavy Chrome/Steel Front Bumper + Tubular Bullbar (DETACHABLE!)
    vb.add_box("SemiFrontBumper", (0.0, 3.92, 0.68), (2.26, 0.24, 0.36), "Front bumper", "chrome", bevel=0.03)
    vb.add_tube_path("SemiBullbar", [
        (-0.88, 3.96, 0.68), (-0.84, 4.08, 1.38), (0.84, 4.08, 1.38), (0.88, 3.96, 0.68)
    ], radius=0.036, segs=10, part="Bullbar", role="matteBlack")
    vb.add_cylinder("SemiBullMid", (-0.84, 4.08, 1.04), (0.84, 4.08, 1.04),
                    0.030, 0.030, segs=10, part="Bullbar", role="matteBlack")
    vb.add_box("SemiFrontPlate", (0.0, 4.05, 0.66), (0.38, 0.018, 0.11), "Front bumper", "plate")

    # Rounded Classic Front Fenders + Pod Headlights (DETACHABLE!)
    for side in (-1.0, 1.0):
        sn = "Right" if side > 0 else "Left"
        fgrid = []
        for ia in range(18):
            ang = lerp(math.radians(-10), math.radians(178), ia / 17.0)
            wy = fa + math.cos(ang) * 0.64
            wz = wr + math.sin(ang) * 0.64
            fgrid.append([(side * 0.66, wy, wz), (side * 1.12, wy, wz)])
        vb.add_grid_surface(f"{sn}SemiFender", fgrid,
                            lambda p: Vector((0, p.y - fa, p.z - wr)).normalized(),
                            f"{sn} front fender", "paint", solidify=0.018)
        # Pod headlights mounted on front fenders
        vb.add_box(f"{sn}PodHouse", (side * 0.90, 3.68, 1.08), (0.28, 0.24, 0.18),
                   f"{sn} front fender", "chrome", bevel=0.03)
        vb.add_box(f"{sn}PodLens", (side * 0.90, 3.81, 1.08), (0.22, 0.025, 0.12),
                   f"{sn} front fender", "headlight")
        vb.add_box(f"{sn}PodInd", (side * 0.90, 3.80, 0.98), (0.22, 0.025, 0.04),
                   f"{sn} front fender", "indicator")

    # Day/Sleeper Cab + Split Windshield + Roof Visor + Clearance Lights + Exhaust Stacks!
    vb.add_box("SemiCabLower", (0.0, 1.15, 1.48), (2.10, 1.56, 1.10), "Body", "paint", bevel=0.06)
    vb.add_box("SemiCabUpper", (0.0, 1.08, 2.34), (2.04, 1.44, 0.72), "Body", "paint", bevel=0.06)
    vb.add_box("SemiRoofCap", (0.0, 1.08, 2.74), (1.96, 1.42, 0.14), "Roof panel", "paint", bevel=0.04)
    vb.add_box("SemiSunVisor", (0.0, 1.88, 2.66), (2.06, 0.24, 0.14), "Roof panel", "chrome", rot_deg=(12, 0, 0), bevel=0.015)
    # Windshield + side windows
    vb.add_box("SemiWindshield", (0.0, 1.82, 2.30), (1.82, 0.06, 0.56), "Body", "glass", rot_deg=(-8, 0, 0))
    vb.add_box("SemiWindSplit", (0.0, 1.85, 2.30), (0.05, 0.05, 0.58), "Body", "trim", rot_deg=(-8, 0, 0))
    for side in (-1.0, 1.0):
        sn = "Right" if side > 0 else "Left"
        vb.add_box(f"{sn}SemiDoor", (side * 1.055, 1.38, 1.62), (0.04, 0.88, 1.24),
                   f"{sn} front door", "paint", bevel=0.015)
        vb.add_box(f"{sn}SemiDoorGlass", (side * 1.07, 1.38, 2.28), (0.02, 0.72, 0.50),
                   f"{sn} front door", "glass")
        # Iconic twin tall chrome vertical exhaust stacks behind cab!
        vb.add_cylinder(f"{sn}ExhaustStack", (side * 1.08, 0.42, 0.90), (side * 1.08, 0.42, 3.08),
                        0.068, 0.068, segs=14, part="Body", role="chrome")
        vb.add_cylinder(f"{sn}ExhaustTip", (side * 1.08, 0.42, 3.05), (side * 1.14, 0.34, 3.22),
                        0.068, 0.062, segs=14, part="Body", role="chrome")
        # Big West-Coast chrome truck mirrors (DETACHABLE!)
        vb.add_box(f"{sn}SemiMirror", (side * 1.26, 1.72, 2.18), (0.08, 0.12, 0.38),
                   f"{sn} mirror", "chrome", bevel=0.015)
        vb.add_cylinder(f"{sn}SemiMirrorArm", (side * 1.04, 1.72, 2.32), (side * 1.24, 1.72, 2.32),
                        0.015, 0.015, segs=6, part=f"{sn} mirror", role="chrome")
    # 5 Amber roof clearance marker lights
    for ic in range(5):
        vb.add_box(f"ClearanceLight_{ic}", (lerp(-0.72, 0.72, ic / 4.0), 1.86, 2.78),
                   (0.08, 0.06, 0.045), "Roof panel", "gold")

    # Gleaming Stainless / Chrome Cylindrical Tanker Barrel + Dome Hatches + Walkways!
    tank_y0, tank_y1 = -3.72, 0.24
    tank_cz = 1.88
    tank_r = 0.98
    # Multi-ring sculpted tanker cylinder so crash deformation dents smoothly along its length!
    tgrid = []
    for iy in range(56):
        y = lerp(tank_y0, tank_y1, iy / 55.0)
        tgrid.append([
            (math.sin(2.0 * math.pi * ic / 48.0) * tank_r, y, tank_cz + math.cos(2.0 * math.pi * ic / 48.0) * tank_r)
            for ic in range(49)
        ])
    vb.add_grid_surface("TankerBarrel", tgrid,
                        lambda p: Vector((p.x, 0.0, p.z - tank_cz)).normalized(),
                        "Body", "chrome", solidify=0.016)
    # Domed front & rear tanker endcaps
    for ename, ey, edir in (("Front", tank_y1, 1.0), ("Rear", tank_y0, -1.0)):
        cgrid = []
        for ir in range(8):
            u = ir / 7.0
            r_ring = tank_r * math.cos(u * math.pi * 0.48)
            y_ring = ey + edir * 0.18 * math.sin(u * math.pi * 0.48)
            cgrid.append([
                (math.sin(2.0 * math.pi * ic / 32.0) * r_ring, y_ring, tank_cz + math.cos(2.0 * math.pi * ic / 32.0) * r_ring)
                for ic in range(33)
            ])
        vb.add_grid_surface(f"TankerCap_{ename}", cgrid, Vector((0, edir, 0)), "Body", "chrome")
    # Structural reinforcement rings around barrel
    for ir in range(5):
        ry = lerp(tank_y0 + 0.18, tank_y1 - 0.18, ir / 4.0)
        vb.add_cylinder(f"TankBand_{ir}", (0.0, ry - 0.04, tank_cz), (0.0, ry + 0.04, tank_cz),
                        tank_r + 0.025, tank_r + 0.025, segs=28, part="Body", role="steel", cap=False)
    # 3 Top manhole dome hatches + spill rail
    for idome in range(3):
        dy = lerp(tank_y0 + 0.85, tank_y1 - 0.85, idome / 2.0)
        vb.add_cylinder(f"TankDome_{idome}", (0.0, dy, tank_cz + tank_r - 0.04),
                        (0.0, dy, tank_cz + tank_r + 0.14), 0.24, 0.22, segs=16, part="Body", role="steel")
    for side in (-1.0, 1.0):
        vb.add_box(f"TankTopRail_{int(side)}", (side * 0.42, 0.5 * (tank_y0 + tank_y1), tank_cz + tank_r + 0.08),
                   (0.04, tank_y1 - tank_y0 - 0.5, 0.12), "Body", "steel")
        # Tank cradles onto chassis
        for cy in (-3.2, -1.6, -0.1):
            vb.add_box(f"TankCradle_{int(side)}_{cy:.1f}", (side * 0.56, cy, 1.04),
                       (0.32, 0.18, 0.52), "Body", "underbody")
        # Rear tandem mudguards
        vb.add_box(f"RearMudguard_{int(side)}", (side * 1.04, -2.30, 1.10),
                   (0.46, 2.60, 0.06), "Body", "matteBlack", bevel=0.015)

    # Rear ICC Bumper Bar + Taillights
    vb.add_box("SemiRearBumper", (0.0, -hl + 0.10, 0.62), (2.22, 0.18, 0.24),
               "Rear bumper", "steel", bevel=0.02)
    for side in (-1.0, 1.0):
        vb.add_box(f"SemiTail_{int(side)}", (side * 0.82, -hl + 0.01, 0.64),
                   (0.28, 0.04, 0.10), "Rear bumper", "tail")
        vb.add_box(f"SemiTailInd_{int(side)}", (side * 0.54, -hl + 0.01, 0.64),
                   (0.14, 0.04, 0.10), "Rear bumper", "indicator")
    vb.add_box("SemiRearPlate", (0.0, -hl + 0.01, 0.62), (0.38, 0.018, 0.11), "Rear bumper", "plate")

    vb.add_preview_wheels([
        (-track, fa), (track, fa),
        (-track, ra1), (track, ra1),
        (-track, ra2), (track, ra2),
    ], wr, tw, spokes=8)
    info = dict(ground=-1.25, halfLength=hl, wheelRadius=wr, track=track, tyreWidth=tw,
                cabin=[0.0, 1.65, -0.5, 2.26, 2.30, 7.60])
    return vb, info


def build_all_traffic_cars() -> list[tuple[str, VehicleBuilder, dict]]:
    return [
        ("Sedan", *build_sedan()),
        ("Hatch", *build_hatch()),
        ("Van", *build_van()),
        ("Pickup", *build_pickup()),
        ("Bus", *build_bus()),
        ("Tanker", *build_tanker()),
    ]

"""
Builds the 4 selectable playable vehicles matching the user's reference photo:
  1. Player       -> Red GT-R Coupe (R34-inspired JDM sport coupe)
  2. PlayerMuscle -> Orange 70s Widebody Muscle Car (matte black hood + riveted flares)
  3. PlayerBmw    -> Austin Yellow / Lime BMW M4 GT (twin kidney grilles + GT wing)
  4. PlayerSuv    -> Matte Black Lifted Tactical SUV (bullbar + roof rack + LED bar)
"""

from vehicle_core import CarSpec, VehicleBuilder, build_sculpted_car


def player_car_specs() -> dict[str, CarSpec]:
    specs: dict[str, CarSpec] = {}

    # 1. RED GT-R COUPE (front-left in photo)
    specs["Player"] = CarSpec(
        kind="Player",
        length=4.46,
        width=1.90,
        height=1.28,
        ground=-0.405,
        wheel_r=0.31,
        tyre_w=0.25,
        track=0.81,
        front_axle=1.34,
        rear_axle=-1.32,
        width_keys=[
            (-2.23, 0.80), (-2.00, 0.90), (-1.32, 0.95), (-0.45, 0.91),
            (0.45, 0.90), (1.34, 0.93), (2.00, 0.88), (2.23, 0.78),
        ],
        belt_keys=[
            (-2.23, 0.86), (-1.75, 0.88), (-0.80, 0.86), (0.55, 0.82),
            (1.34, 0.78), (1.95, 0.71), (2.23, 0.56),
        ],
        hood_keys=[
            (0.55, 0.83), (1.10, 0.79), (1.65, 0.73), (2.08, 0.65), (2.23, 0.55),
        ],
        rocker_keys=[
            (-2.23, 0.21), (-1.80, 0.14), (1.80, 0.13), (2.23, 0.14),
        ],
        rear_glass_base=-1.52,
        rear_glass_top=-0.76,
        front_glass_top=0.16,
        front_glass_base=0.76,
        roof_width_ratio=0.73,
        paint_rgb=(0.74, 0.055, 0.075),
        paint_metal=0.78,
        paint_rough=0.14,
        paint_coat=1.0,
        wheel_rgb=(0.24, 0.22, 0.18),
        wheel_spokes=6,
        doors_per_side=1,
        hood_role="paint",
        hood_style="vented",
        grille_style="gtr",
        headlight_style="gtr",
        taillight_style="gtr",
        flare_style="blister",
        flare_out=0.055,
        has_gt_wing=True,
        wing_height=0.25,
        wing_span_ratio=0.95,
        has_chin_splitter=True,
        side_skirt_drop=0.025,
        tumblehome=0.068,
        shoulder_crease=0.036,
    )

    # 2. ORANGE 70s WIDEBODY MUSCLE CAR (front-center-left in photo)
    specs["PlayerMuscle"] = CarSpec(
        kind="PlayerMuscle",
        length=4.58,
        width=1.96,
        height=1.27,
        ground=-0.405,
        wheel_r=0.32,
        tyre_w=0.27,
        track=0.83,
        front_axle=1.36,
        rear_axle=-1.34,
        width_keys=[
            (-2.29, 0.86), (-2.00, 0.94), (-1.34, 0.98), (-0.45, 0.93),
            (0.45, 0.92), (1.36, 0.96), (2.02, 0.92), (2.29, 0.85),
        ],
        belt_keys=[
            (-2.29, 0.85), (-1.80, 0.89), (-0.85, 0.88), (0.50, 0.84),
            (1.36, 0.82), (2.05, 0.78), (2.29, 0.66),
        ],
        hood_keys=[
            (0.50, 0.85), (1.15, 0.83), (1.75, 0.79), (2.14, 0.74), (2.29, 0.65),
        ],
        rocker_keys=[
            (-2.29, 0.22), (-1.85, 0.15), (1.85, 0.15), (2.29, 0.16),
        ],
        rear_glass_base=-1.68,
        rear_glass_top=-0.78,
        front_glass_top=0.06,
        front_glass_base=0.66,
        roof_width_ratio=0.72,
        paint_rgb=(0.88, 0.30, 0.045),
        paint_metal=0.72,
        paint_rough=0.18,
        paint_coat=0.95,
        wheel_rgb=(0.14, 0.14, 0.15),
        wheel_spokes=5,
        doors_per_side=1,
        hood_role="matteBlack",
        hood_style="muscle",
        grille_style="muscle",
        headlight_style="round",
        taillight_style="bar",
        flare_style="riveted_black",
        flare_out=0.072,
        has_gt_wing=False,
        has_lip_spoiler=True,
        has_chin_splitter=True,
        side_skirt_drop=0.020,
        tumblehome=0.072,
        shoulder_crease=0.040,
    )

    # 3. YELLOW/LIME BMW M4 GT (middle-left in photo)
    specs["PlayerBmw"] = CarSpec(
        kind="PlayerBmw",
        length=4.50,
        width=1.92,
        height=1.29,
        ground=-0.405,
        wheel_r=0.31,
        tyre_w=0.25,
        track=0.81,
        front_axle=1.35,
        rear_axle=-1.33,
        width_keys=[
            (-2.25, 0.80), (-1.98, 0.91), (-1.33, 0.96), (-0.42, 0.91),
            (0.45, 0.90), (1.35, 0.94), (2.00, 0.88), (2.25, 0.76),
        ],
        belt_keys=[
            (-2.25, 0.88), (-1.70, 0.90), (-0.75, 0.88), (0.55, 0.83),
            (1.35, 0.78), (1.96, 0.70), (2.25, 0.54),
        ],
        hood_keys=[
            (0.55, 0.84), (1.12, 0.80), (1.68, 0.73), (2.10, 0.63), (2.25, 0.53),
        ],
        rocker_keys=[
            (-2.25, 0.21), (-1.80, 0.14), (1.80, 0.13), (2.25, 0.14),
        ],
        rear_glass_base=-1.56,
        rear_glass_top=-0.72,
        front_glass_top=0.14,
        front_glass_base=0.78,
        roof_width_ratio=0.73,
        paint_rgb=(0.74, 0.72, 0.04),
        paint_metal=0.82,
        paint_rough=0.14,
        paint_coat=1.0,
        wheel_rgb=(0.18, 0.19, 0.21),
        wheel_spokes=8,
        doors_per_side=1,
        hood_role="paint",
        hood_style="powerdome",
        grille_style="bmw",
        headlight_style="bmw",
        taillight_style="bmw",
        flare_style="blister",
        flare_out=0.052,
        has_gt_wing=True,
        wing_height=0.22,
        wing_span_ratio=0.90,
        has_chin_splitter=True,
        side_skirt_drop=0.025,
        tumblehome=0.070,
        shoulder_crease=0.038,
    )

    # 4. MATTE BLACK LIFTED TACTICAL SUV (back-left in photo)
    specs["PlayerSuv"] = CarSpec(
        kind="PlayerSuv",
        length=4.68,
        width=2.02,
        height=1.80,
        ground=-0.42,
        wheel_r=0.39,
        tyre_w=0.28,
        track=0.86,
        front_axle=1.44,
        rear_axle=-1.40,
        width_keys=[
            (-2.34, 0.92), (-2.05, 0.98), (-1.40, 1.00), (0.0, 0.99),
            (1.44, 1.00), (2.06, 0.97), (2.34, 0.90),
        ],
        belt_keys=[
            (-2.34, 1.12), (-1.95, 1.14), (0.80, 1.13), (1.44, 1.12),
            (2.08, 1.09), (2.34, 0.98),
        ],
        hood_keys=[
            (0.80, 1.14), (1.40, 1.13), (2.00, 1.09), (2.34, 0.98),
        ],
        rocker_keys=[
            (-2.34, 0.42), (-1.90, 0.34), (1.90, 0.34), (2.34, 0.40),
        ],
        rear_glass_base=-2.18,
        rear_glass_top=-1.92,
        front_glass_top=0.52,
        front_glass_base=1.02,
        roof_width_ratio=0.84,
        roof_crown=0.024,
        paint_rgb=(0.022, 0.025, 0.030),
        paint_metal=0.22,
        paint_rough=0.46,
        paint_coat=0.12,
        wheel_rgb=(0.12, 0.12, 0.13),
        wheel_spokes=6,
        offroad_tyres=True,
        doors_per_side=2,
        hood_role="paint",
        hood_style="rugged",
        grille_style="suv",
        headlight_style="suv",
        taillight_style="suv",
        flare_style="rugged_black",
        flare_out=0.075,
        has_gt_wing=False,
        has_chin_splitter=False,
        has_bullbar=True,
        has_roof_rack=True,
        is_suv=True,
        side_skirt_drop=0.0,
        tumblehome=0.042,
        shoulder_crease=0.026,
    )

    return specs


def build_all_player_cars() -> list[tuple[str, VehicleBuilder, dict]]:
    results = []
    for kind, spec in player_car_specs().items():
        vb, info = build_sculpted_car(spec)
        results.append((kind, vb, info))
    return results

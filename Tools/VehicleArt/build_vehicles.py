"""
Master Blender 5.2 vehicle builder & COV2 exporter for Crashout.
Builds all 4 selectable player vehicles + all 6 traffic vehicles from scratch,
exports COV2 `.bytes` assets for Unity, saves `Art/Vehicles/CrashoutVehicles.blend`,
and renders studio preview images + a labelled `Fleet.png` showcase sheet.
"""

from __future__ import annotations

import importlib
import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
if str(HERE) not in sys.path:
    sys.path.insert(0, str(HERE))

import bpy
import vehicle_core
import build_player_cars
import build_traffic_cars

importlib.reload(vehicle_core)
importlib.reload(build_player_cars)
importlib.reload(build_traffic_cars)

LABELS = {
    "Player": "PLAYER 1 · GT-R COUPE",
    "PlayerMuscle": "PLAYER 2 · 70S WIDEBODY MUSCLE",
    "PlayerBmw": "PLAYER 3 · M4 GT COUPE",
    "PlayerSuv": "PLAYER 4 · TACTICAL SUV",
    "Sedan": "TRAFFIC · SPORT SEDAN",
    "Hatch": "TRAFFIC · HOT HATCH",
    "Van": "TRAFFIC · CARGO VAN",
    "Pickup": "TRAFFIC · UTILITY PICKUP",
    "Bus": "TRAFFIC · CITY TRANSIT BUS",
    "Tanker": "TRAFFIC · SEMI FUEL TANKER",
}


def build_and_export(render: bool = True) -> list[dict]:
    vehicle_core.reset_scene()
    built = build_player_cars.build_all_player_cars() + build_traffic_cars.build_all_traffic_cars()

    stats = []
    for kind, vb, info in built:
        s = vehicle_core.export_cov2(kind, info, vb.objects, vb.coll)
        stats.append(s)
        print("EXPORTED", s, flush=True)

    budget_path = vehicle_core.ART_DIR / "mesh-budget.json"
    budget_path.write_text(
        json.dumps([{k: s[k] for k in ("file", "vertices", "triangles", "draws", "bytes")} for s in stats], indent=2)
    )

    cam = vehicle_core.setup_studio()

    # Keep only Player visible by default in the saved .blend
    for kind, vb, _ in built:
        vb.coll.hide_render = (kind != "Player")
        vb.coll.hide_viewport = (kind != "Player")

    bpy.ops.wm.save_as_mainfile(filepath=str(vehicle_core.ART_DIR / "CrashoutVehicles.blend"))

    if render:
        scene = bpy.context.scene
        # Hero studio front & rear shots of default Player car
        scene.render.resolution_x = 1280
        scene.render.resolution_y = 800
        vehicle_core.aim_camera(cam, (4.3, 5.2, 1.95), (0.0, 0.0, 0.56))
        scene.render.filepath = str(vehicle_core.ART_DIR / "Player-studio.png")
        bpy.ops.render.render(write_still=True)

        vehicle_core.aim_camera(cam, (-4.3, -5.2, 1.95), (0.0, 0.0, 0.56))
        scene.render.filepath = str(vehicle_core.ART_DIR / "Player-rear.png")
        bpy.ops.render.render(write_still=True)

        # Individual fleet tiles for contact sheet
        scene.render.resolution_x = 760
        scene.render.resolution_y = 460
        for kind, vb, _ in built:
            for k2, vb2, _ in built:
                vb2.coll.hide_render = (k2 != kind)
                vb2.coll.hide_viewport = (k2 != kind)
            d_scale = {
                "Bus": 1.68, "Tanker": 1.78, "Van": 1.22,
                "Pickup": 1.15, "PlayerSuv": 1.12,
            }.get(kind, 1.02)
            tz = {
                "Bus": 1.25, "Tanker": 1.38, "Van": 0.96,
                "Pickup": 0.80, "PlayerSuv": 0.78,
            }.get(kind, 0.56)
            vehicle_core.aim_camera(cam, (4.3 * d_scale, 5.2 * d_scale, 1.95 * d_scale), (0.0, 0.0, tz))
            scene.render.filepath = str(vehicle_core.ART_DIR / f"_fleet_{kind}.png")
            bpy.ops.render.render(write_still=True)

        for kind, vb, _ in built:
            vb.coll.hide_render = (kind != "Player")
            vb.coll.hide_viewport = (kind != "Player")

    return stats


if __name__ == "__main__":
    no_render = "--no-render" in sys.argv
    stats = build_and_export(render=not no_render)
    result = {"stats": stats}

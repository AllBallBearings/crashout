# Vehicle art

Seven original, unbranded vehicle bodies are stored in `Unity/Assets/Crashout/Resources/Vehicles`.
They load automatically through `VehicleBodyAsset` and retain the existing vehicle controllers,
wheel animation, scoring, reset and replay. No external asset licences or runtime Blender dependency.

Build the meshes and review renders with Python, NumPy and Pillow:

```sh
python3 Tools/VehicleArt/build_vehicles.py --portable
```

Or build the editable Blender scene and export the same body definitions:

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --python Tools/VehicleArt/build_vehicles.py
```

The Blender route saves `Art/Vehicles/CrashoutVehicles.blend`. It requires a working Blender graphics
context even in background mode. The delivered assets were built with the portable backend because
Blender 5.2.1 crashed in its Metal-device initialization in the coding environment. No `.blend` has
been generated yet. Portable previews use a CPU inspection renderer, not Unity or Blender lighting;
their wheel geometry is a preview stand-in for the game's existing animated wheel meshes.

`Art/Vehicles/mesh-budget.json` records the actual vertex, triangle, material-section and byte counts.
The `.bytes` files are little-endian COV2 packs: magic, five vehicle floats, six cabin-collider floats,
section count; then length-prefixed UTF-8 part/material names, vertex/index counts, interleaved
position/normal/UV float data, and 32-bit indices. Positions are metres, Y up, forward +Z, relative
to the rigidbody root. Each detachable part owns its material sections. Sheet panels have inner
faces and boundary edges; do not replace them with single-sided planes.

The portable and Blender routes share body profiles but use different bevel implementations.
Always inspect both silhouettes and normals after re-exporting. The export changes handedness
and reverses triangle winding. The edit-mode asset tests validate the packs without loading meshes
into a graphics device.

Damage uses a coarse plastic chassis map plus surface folds whose amplitude stores the lost
projected sheet length. Local contacts dent and crease panels and write scrape masks into vertex
colours. The automotive URP shader reads these masks; torn sections interpolate them and add curled,
bare-metal lips. This is an arcade approximation, not a finite-element crash solver. Normal crashes
should crumple ends and shed attachments; chassis separation requires extreme accumulated loads.

## Unity/device review

1. Let Unity import the new assets and shader. Check the Console for shader/import errors.
2. Run EditMode tests, then `VehicleDestructionTests` and `RunwayContactTests` in PlayMode.
3. Inspect the coupe and all six traffic classes in daylight, shadows, replay and AR.
4. Test frontal, glancing, side, pinned-between-vehicles and rollover impacts. Look for retained
   crumpled metal, intact unstruck panels, detached-part velocity, and sensible wheel behaviour.
5. Replay, seek backwards, then Retry: geometry, paint, glass, attachments and mass must restore.
6. Profile crowded crashes on iPhone: frame time, draw calls, transparent glass and replay memory.
7. Make a **full Unity iOS build** with **Scripts Only disabled**. New meshes and the paint shader
   cannot be delivered by a scripts-only build. Then run the existing Xcode project.

The CPU checks do not establish Unity shader support, real-time performance, or final visual parity
with a commercial racing game. Those require native runtime review.

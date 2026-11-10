# Crashout

Crashout is a local turn-based crash game for people gathered around the same table. One player drives into a seeded stream of traffic while everyone watches the same chain reaction from an independently chosen AR or virtual camera.

The production game is native Unity. The website at the repository root is the public companion—not a browser gameplay client.

## Repository layout

- `Unity/` — Unity 6.3 LTS (`6000.3.27f2`) native iPhone/iPad game project.
- `app/` and `public/` — the Vinext companion site.
- `docs/ARCHITECTURE.md` — the one-world implementation boundary.
- `docs/BROWSER_PROTOTYPE_LESSONS.md` — mechanics retained from the archived browser prototype.
- `docs/product-requirements.md` — the complete product and engineering requirements.
- `docs/NEXT_STEPS.md` — prioritised roadmap: first device build, tuning, multiplayer turns, shared AR table, and technical debt.

The old Three.js / Quick Look implementation remains available in Git history through commit `50d158f`. It is deliberately not duplicated in the production tree.

## Play the native prototype

1. Use Unity `6000.3.27f2` (Apple Silicon on Apple Silicon Macs) with iOS Build Support.
2. Open the `Unity` directory from Unity Hub.
3. Open `Assets/Crashout/Scenes/Startup.unity` (it is the only scene in the build list) and press Play. `CrashoutBootstrapper` creates the junction, traffic and HUD at runtime.
4. Hold `W` / Up Arrow to accelerate, use `A` and `D` / arrow keys to steer, `S` to brake, hold `Shift` to boost, `C` to cycle cameras, `R` to retry, and `N` for the next junction. Each run starts frozen: tap `W` / `Space` (or the GAS pad) with the launch bar in the green to start, and the traffic is released at that moment.

The runtime also draws press-and-hold touch controls for device builds. The single-device slice already uses a 60 Hz authority tick, physical Rigidbody traffic, host-only damage scoring, repeat-contact filtering, settle detection, and a 20-second safety timeout.

## Crash Mode

The game follows Burnout Revenge's Crash Mode: drive into a busy junction and cause the biggest pile-up you can, scored in dollars of damage.

- **Junctions:** Cross Traffic, Rush Hour (buses) and Tanker Run (a fuel tanker), each with Bronze/Silver/Gold targets and a marked target car worth a bonus.
- **Boost:** hold `Shift` or the BOOST button. Boost left at impact becomes Crashbreaker power.
- **Impact time and aftertouch:** slow motion on impact, then steer the wreck relative to the camera for a few seconds.
- **Crashbreaker:** `Space` / `F` / DETONATE blows up your car. The meter fills as cars are wrecked, and its charge sets the blast radius and force. It can be fired more than once.
- **Chain reactions:** traffic only gives way to other moving traffic, so cars drive into wrecks. Wrecks slide and tumble, buses and tankers carry real mass, and tankers explode.

Scenarios live in `Runtime/Simulation/CrashScenario.cs` and vehicle classes in `Runtime/Vehicle/VehicleSpec.cs`.

## Approach runways

Every junction now starts far outside the 52 m board, one to two board-lengths out and well above it, so the run-in builds tension before the pile-up:

| Junction | Approach | Start |
| --- | --- | --- |
| Cross Traffic | **Hilltop Drop**: the brow of a 16 m hill, then a 26% descent onto the junction | 98 m out |
| Rush Hour | **The Overpass**: a cable-stayed expressway over a river, then a ramp down into town | 92 m out |
| Tanker Run | **Mountain Pass**: out of a tunnel, down a 30% pass and off a kicker ramp | 103 m out |

Profiles live in `RunwayProfile` (grade breaks blended through parabolic vertical curves) and are set per junction in `CrashScenario.cs`. `ApproachPlanner` predicts the player's arrival from their position, speed, inputs and the grade; traffic is parked and then phase-locked to that estimate (`TrafficVehicle.SteerToPhase`) so the junction converges as the player arrives, whether they cruise or boost the whole way. Out on the runway the barriers only scrape; scenery ends a run only on the board, and touching down off a crest or the ramp never does.

## Look and sound

- **Art** is still generated at runtime, now physically based: synthesized albedo, normal and smoothness maps (asphalt, painted road, paving, brick and render facades, rock, terrain, foliage), clear-coat car paint, lofted car bodies with wheel arches and tumblehome, spoked alloy wheels, real-time reflections, a cloud layer, 4-cascade soft shadows, SSAO and a filmic post stack (ACES, white balance, split-tone grade, lens dirt, grain, motion blur, slow-motion depth of field) directed by `CinematicFx`.
- **Damage**: `VehicleDeformer` dents render meshes around each contact, crazes glass, kills lamps and tears off bumpers; `VehicleLook` drives brake lights and post-crash hazards.
- **Sound**: `EngineSynth` is a real-time V8 on the audio thread with a six-speed gearbox, overrun crackle and supercharger whine. Impacts are modal-synthesis metal with crumple grains, granular glass and debris; explosions layer a crack, sub boom and rumble. Wind, tyre roar, squeal, scrape, Doppler traffic, a tension drone and heartbeat, a low-pass "underwater" slow motion and reverb zones complete the mix.

Set `CRASHOUT_CAPTURE_DIR` and run the `CaptureFrames` Play Mode test to render each junction's key moments to PNG for art review. Batch-mode (headless) runs are silent; the game mutes its listener when `Application.isBatchMode` is true.

## Tabletop AR on iPhone

On an iPhone build the world is shown on a real table. The physics world is never scaled; the AR camera pose is mapped through the board transform into canonical space and a second camera draws the simulation over the camera feed. Aim at a flat surface, size the board with `-` / `+`, and tap PLACE BOARD. If ARKit is unavailable the game falls back to the virtual camera.

`Editor/ArProjectSetup.cs` (menu `Crashout > Configure AR For iPhone`) assigns the ARKit loader, adds the AR background feature to `CrashoutRenderer.asset` and sets the camera usage string. Before the first device build set your Apple Team ID in Player Settings. `Editor/EnsureRuntimeShaders.cs` keeps the runtime-created shaders in Always Included Shaders so player builds do not render pink.

## Tests

Run Edit Mode tests from Unity Test Runner. The tests cover phase authority, score caps, repeated-contact filtering, chain scoring, and the 1:32 AR mapping.

Edit Mode also covers runway geometry and arrival timing (`RunwayTests`). In the Play Mode tab, run `JunctionSmokeTests` and `CrashModeTests` with graphics enabled. `CrashModeTests` plays every junction, fires the Crashbreaker and asserts a chain reaction. `JunctionSmokeTests` checks a real camera frame for visible vehicle color and shader errors, then drives the car into traffic using simulated keyboard input and checks for a scored collision. The graphics default is `CrashoutPipeline`; quality levels inherit it.

## Run the companion site

```bash
npm install
npm run dev
```

The site intentionally contains no fake AR launch or browser gameplay button. It explains the native game and will become the TestFlight/App Store destination.

## Current milestone

Crash Mode (three junctions, boost, aftertouch, Crashbreaker, target bonus, medals) and the tabletop AR path are implemented and covered by Edit Mode and Play Mode tests on macOS. A physical iPhone build, touch handling, real plane placement, and shared multiplayer alignment have not yet been validated. Turn-based multiplayer scoring, Bonjour discovery, Netcode replication, and production art remain subsequent milestones.

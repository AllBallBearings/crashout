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
4. Hold `W` / Up Arrow to accelerate, use `A` and `D` / arrow keys to steer, `S` to brake, hold `Shift` to boost, `C` to cycle cameras, `R` to retry, and `N` for the next junction.

The runtime also draws press-and-hold touch controls for device builds. The single-device slice already uses a 60 Hz authority tick, physical Rigidbody traffic, host-only damage scoring, repeat-contact filtering, settle detection, and a 20-second safety timeout.

## Crash Mode

The game follows Burnout Revenge's Crash Mode: drive into a busy junction and cause the biggest pile-up you can, scored in dollars of damage.

- **Junctions:** Cross Traffic, Rush Hour (buses) and Tanker Run (a fuel tanker), each with Bronze/Silver/Gold targets and a marked target car worth a bonus.
- **Boost:** hold `Shift` or the BOOST button. Boost left at impact becomes Crashbreaker power.
- **Impact time and aftertouch:** slow motion on impact, then steer the wreck relative to the camera for a few seconds.
- **Crashbreaker:** `Space` / `F` / DETONATE blows up your car. The meter fills as cars are wrecked, and its charge sets the blast radius and force. It can be fired more than once.
- **Chain reactions:** traffic only gives way to other moving traffic, so cars drive into wrecks. Wrecks slide and tumble, buses and tankers carry real mass, and tankers explode.

Scenarios live in `Runtime/Simulation/CrashScenario.cs` and vehicle classes in `Runtime/Vehicle/VehicleSpec.cs`.

## Tabletop AR on iPhone

On an iPhone build the world is shown on a real table. The physics world is never scaled; the AR camera pose is mapped through the board transform into canonical space and a second camera draws the simulation over the camera feed. Aim at a flat surface, size the board with `-` / `+`, and tap PLACE BOARD. If ARKit is unavailable the game falls back to the virtual camera.

`Editor/ArProjectSetup.cs` (menu `Crashout > Configure AR For iPhone`) assigns the ARKit loader, adds the AR background feature to `CrashoutRenderer.asset` and sets the camera usage string. Before the first device build set your Apple Team ID in Player Settings. `Editor/EnsureRuntimeShaders.cs` keeps the runtime-created shaders in Always Included Shaders so player builds do not render pink.

## Tests

Run Edit Mode tests from Unity Test Runner. The tests cover phase authority, score caps, repeated-contact filtering, chain scoring, and the 1:32 AR mapping.

In the Play Mode tab, run `JunctionSmokeTests` and `CrashModeTests` with graphics enabled. `CrashModeTests` plays every junction, fires the Crashbreaker and asserts a chain reaction. `JunctionSmokeTests` checks a real camera frame for visible vehicle color and shader errors, then drives the car into traffic using simulated keyboard input and checks for a scored collision. The graphics default is `CrashoutPipeline`; quality levels inherit it.

## Run the companion site

```bash
npm install
npm run dev
```

The site intentionally contains no fake AR launch or browser gameplay button. It explains the native game and will become the TestFlight/App Store destination.

## Current milestone

Crash Mode (three junctions, boost, aftertouch, Crashbreaker, target bonus, medals) and the tabletop AR path are implemented and covered by Edit Mode and Play Mode tests on macOS. A physical iPhone build, touch handling, real plane placement, and shared multiplayer alignment have not yet been validated. Turn-based multiplayer scoring, Bonjour discovery, Netcode replication, and production art remain subsequent milestones.

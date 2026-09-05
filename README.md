# Crashout

Crashout is a local turn-based crash game for people gathered around the same table. One player drives into a seeded stream of traffic while everyone watches the same chain reaction from an independently chosen AR or virtual camera.

The production game is native Unity. The website at the repository root is the public companion—not a browser gameplay client.

## Repository layout

- `Unity/` — Unity 6.3 LTS native iPhone/iPad game project.
- `app/` and `public/` — the Vinext companion site.
- `docs/ARCHITECTURE.md` — the one-world implementation boundary.
- `docs/BROWSER_PROTOTYPE_LESSONS.md` — mechanics retained from the archived browser prototype.
- `docs/product-requirements.md` — the complete product and engineering requirements.

The old Three.js / Quick Look implementation remains available in Git history through commit `50d158f`. It is deliberately not duplicated in the production tree.

## Play the native prototype

1. Install Unity `6000.3.16f1` with iOS Build Support.
2. Open the `Unity` directory from Unity Hub.
3. Open any empty scene and press Play. `CrashoutBootstrapper` creates the current prototype junction at runtime.
4. Hold `W` / Up Arrow to accelerate, use `A` and `D` / arrow keys to steer, `S` to brake, `C` to cycle cameras, and `R` to reset.

The runtime also draws press-and-hold touch controls for device builds. The single-device slice already uses a 60 Hz authority tick, physical Rigidbody traffic, host-only damage scoring, repeat-contact filtering, settle detection, and a 20-second safety timeout.

Run Edit Mode tests from Unity Test Runner. The tests cover phase authority, score caps, repeated-contact filtering, chain scoring, and the 1:32 AR mapping.

## Run the companion site

```bash
npm install
npm run dev
```

The site intentionally contains no fake AR launch or browser gameplay button. It explains the native game and will become the TestFlight/App Store destination.

## Current milestone

Milestone 1 is implemented as a source-complete vertical slice and is ready for its first Unity Editor/device validation pass. Native AR relocalization, Bonjour discovery, Netcode replication, and production art remain subsequent milestones; their data boundaries are already represented in the simulation and presentation code.

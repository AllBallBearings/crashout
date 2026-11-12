# One-world architecture

Crashout has exactly one canonical simulation per match. The host advances physics, traffic, damage, scoring, turns, and the game clock. Clients submit input commands and render replicated entity snapshots; they never submit transforms or score claims.

```text
Local / remote driver input
          |
          v
CrashoutGameSession (authority, 60 Hz)
  ArcadeVehicleController
  TrafficVehicle
  DamageableVehicle -> ScoreLedger
  SessionStateMachine
          |
          +-----------------------+
          |                       |
          v                       v
Canonical virtual cameras    EntitySnapshot stream
same Rigidbody world              |
                                  v
                           PresentationSynchronizer
                                  |
                                  v
                           AR board render proxies
                           position × 1/32 scale
```

## Runtime boundaries

### Simulation

`CrashoutGameSession` owns the fixed host tick, control token boundary, turn phase, collision intake, score ledger, settle decision, reset, and snapshot capture. `SessionStateMachine` rejects illegal or stale progression. `ScoreLedger` filters repeat contacts, caps each entity's value, and rewards distinct vehicles joining the unresolved pileup.

### Vehicles

`ArcadeVehicleController` applies throttle along `transform.forward`, uses raycast suspension, suppresses lateral slip, and derives visible wheel steering and roll from physical state. `TrafficVehicle` uses forces to chase an authored lane and speed, brakes for leaders, and gives up propulsion after material damage. Nothing teleports along a spline during the running turn.

### Presentation

`CrashoutCameraRig` observes the canonical player Rigidbody in tabletop, chase, first-person, and overhead modes. Camera changes are local and do not touch `CrashoutGameSession`.

`ArBoardCoordinator` maps canonical full-size meters through a shared tabletop origin and a default scale of `1 / 32`. `PresentationSynchronizer` updates render-only proxies from the same entity snapshots used by network spectators. Physics bodies never sit below the scaled AR hierarchy.

### Turn replay

`TurnReplay` starts automatically at Driving and captures the final frame after Results. It samples presentation state at 20 Hz in unscaled time, retaining the original impact slow motion. `ReplayWorld` stores sparse render tracks in canonical metres, copies mesh versions only when `ReplayMeshRevision` changes, and shares unchanged material versions. Particle history stores evaluated world positions, colours, sizes and seeds, then interpolates matching particles independently of the viewing camera. Cosmetic particle samples are thinned around a 48 MiB budget on unusually long turns, preserving effect birth/death boundaries; vehicle tracks retain their sampling rate.

Playback creates render-only geometry, hides live renderers/lights, and pauses simulation time and live audio. `ReplayPlaybackClock` advances using unscaled delta time at the selected speed. AR Foundation and `ArTabletopMode.SyncCamera` continue updating the physical viewpoint; no camera pose is recorded. The canonical rigidbodies, scores and phase are not rewound, and no session events are re-emitted. Closing restores the original render flags, lights, audio pause state and time scale. Retry/scenario change releases the recording and cloned resources before reset.

The current scope retains the most recent turn in memory, with silent playback and no file export or network transport. New in-place mesh editing systems must call `ReplayMeshRevision.Changed` after updating a mesh.

### Network seam

The serializable `DriverInputCommand`, `EntitySnapshot`, `ScoreEvent`, host tick, entity IDs, and phase sequence are deliberately engine-level contracts. The LAN transport layer can wrap these with Netcode for GameObjects without moving authority into `NetworkBehaviour` callbacks or creating a second simulation.

The intended rates are 60 Hz host physics, 30–60 Hz driver input, and 20 Hz snapshots with a roughly 100 ms spectator interpolation buffer. Phase, spawn, score, and result events use reliable delivery; continuous input and snapshots use sequenced unreliable delivery.

## Next implementation slices

1. Validate and tune the primitive bootstrap scene in Unity 6.3 LTS (`6000.3.27f2`) on a non-Pro iPhone.
2. Replace prototype geometry with prefabs and ScriptableObject tuning assets without changing the simulation contracts.
3. Add an AR Foundation session and bind its accepted host anchor to `ArBoardCoordinator`.
4. Prove ARWorldMap transfer on two physical devices; retain an image-marker fallback.
5. Add Netcode for GameObjects plus Unity Transport and Bonjour discovery at the existing command/snapshot seam.
6. Add driver prediction and reconciliation, then finish the lobby, turn handoff, and shared results loop.

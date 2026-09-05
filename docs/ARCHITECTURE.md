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

### Network seam

The serializable `DriverInputCommand`, `EntitySnapshot`, `ScoreEvent`, host tick, entity IDs, and phase sequence are deliberately engine-level contracts. The LAN transport layer can wrap these with Netcode for GameObjects without moving authority into `NetworkBehaviour` callbacks or creating a second simulation.

The intended rates are 60 Hz host physics, 30–60 Hz driver input, and 20 Hz snapshots with a roughly 100 ms spectator interpolation buffer. Phase, spawn, score, and result events use reliable delivery; continuous input and snapshots use sequenced unreliable delivery.

## Next implementation slices

1. Validate and tune the primitive bootstrap scene in Unity 6.3 LTS on a non-Pro iPhone.
2. Replace prototype geometry with prefabs and ScriptableObject tuning assets without changing the simulation contracts.
3. Add an AR Foundation session and bind its accepted host anchor to `ArBoardCoordinator`.
4. Prove ARWorldMap transfer on two physical devices; retain an image-marker fallback.
5. Add Netcode for GameObjects plus Unity Transport and Bonjour discovery at the existing command/snapshot seam.
6. Add driver prediction and reconciliation, then finish the lobby, turn handoff, and shared results loop.

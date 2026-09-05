# Crashout Tabletop AR Crash Game

## Product Requirements and Engineering Architecture

Version 2.0  
September 4 2026  
Primary product decision: native Unity game for iPhone and iPad with one shared simulation across AR tabletop and virtual driving views

## 1 Product Decision

Crashout will be designed as a native mobile game rather than a browser game. The initial production target is iPhone and iPad. Unity will provide the game runtime, vehicle physics, rendering, camera system, user interface, and multiplayer client. AR Foundation and the ARKit XR Plugin will provide native iOS augmented reality.

The defining architectural rule is that every view observes the same authoritative game world. The tabletop AR view is not a separate model, animation, export, or preview. It renders the live positions, rotations, damage states, traffic behavior, crash events, score, and turn state produced by the multiplayer simulation. First-person, chase, overhead, non-AR tabletop, and AR tabletop views differ only in how a device presents that shared state.

The current Three.js progressive web application remains useful as a driving, camera, collision, and scoring prototype. It will not remain the production runtime for iPhone AR because iOS browsers do not provide supported WebXR immersive AR. Apple Quick Look will not be used as a gameplay mode because it displays a separate USDZ scene and cannot participate directly in the running Unity physics and multiplayer session.

The first production proof of concept will use TestFlight for device testing. App Store release is the default distribution path after validation. An App Clip may be investigated later as a reduced-friction entry point, but it is not a dependency for the proof of concept because Unity build size and App Clip constraints may make the complete game impractical in that format.

## 2 Product Vision

Crashout is a local turn-based crash game for people gathered around the same table. A detailed miniature road junction appears on the table through each spectator's iPhone or iPad. Players take turns driving one car into moving traffic and trying to create the most valuable chain-reaction pileup.

During a turn, the active player may drive from a conventional virtual chase or first-person camera, or remain in AR and control the car like an RC vehicle on the table. Other players continue watching the same action from independently movable AR viewpoints. A camera change on one device never pauses, resets, advances, duplicates, or resimulates the game.

The central product question remains whether aiming a fast car at a busy junction, causing a skill-dependent pileup, and watching the consequences with friends is entertaining enough to invite immediate replay.

## 3 Design References and Intellectual Property Boundary

Burnout 3 Takedown Crash mode is the primary gameplay reference. Smash Tanks is the primary experience reference for a shared tabletop AR game.

The relevant Burnout 3 qualities are:

- The player drives into a prepared traffic situation rather than selecting a passive launch angle.
- Timing, speed, steering, and the initial impact location determine the chain reaction.
- Secondary and tertiary impacts are a major source of score and spectacle.
- A short run produces a clear result and invites another attempt.
- The junction can be studied and mastered over repeated turns.

The relevant Smash Tanks qualities are:

- The board appears to occupy a real shared surface.
- Players can view the same action from different physical positions.
- The experience feels like a living board game rather than a first-person AR scene.
- Local multiplayer makes watching another player's turn part of the game.

These products are references, not content sources. Crashout must use original branding, maps, vehicles, art, sounds, terminology, scoring values, user interface, and implementation.

## 4 Experience Contract

The following requirements define the product and must survive technical or scheduling tradeoffs.

### One World

One session contains exactly one canonical simulation. The authoritative host advances physics, traffic, damage, scoring, turns, and the game clock. No client runs an independent crash whose outcome can diverge from the other devices.

### Several Views

Every device receives the same entity state and chooses its own local presentation. A spectator may remain in tabletop AR while the active player uses a virtual chase camera. The active player may switch back to AR and continue driving the same car at the same simulation tick.

### One Driver

Exactly one player owns the control token during a turn. Only that device may submit throttle, brake, reverse, and steering commands. Camera choice is always local and does not require control authority.

### Shared Spectacle

All players see the same collisions and score changes in real time. Network smoothing may cause small presentation differences, but the host's state and final outcome are identical for every player.

### Tabletop Scale

AR presents the world as a large miniature board placed on a horizontal surface. It must not resemble a full-scale street surrounding the viewer or a camera placed at road level. The initial scale target is approximately 1 to 32, producing player and traffic cars roughly 12 to 15 centimeters long. Scale remains a tunable presentation value.

## 5 Target Experience

Players: 2 to 4 people for the proof of concept, with 6 as a later performance and usability target.

Devices: ARKit-compatible iPhones and iPads on the same local Wi-Fi network. The initial test matrix should include at least one recent non-Pro iPhone and one iPad.

Location: living-room coffee table, dining table, conference table, or another horizontal surface with enough room for several people to view it safely.

Session length: approximately 5 to 20 minutes.

Turn length: approximately 20 to 60 seconds from countdown to final score.

Default match: 3 rounds per player, configurable by the host.

Default board footprint: approximately 1.0 to 1.4 meters across at the selected physical scale. Placement controls may reduce or enlarge it within tested bounds.

## 6 Core Game Loop

1. A player creates a nearby game and becomes the session host.
2. Other players discover the session on the same Wi-Fi network or scan a join code.
3. The host scans a horizontal surface and places the board.
4. Joining devices align to the host's tabletop coordinate system.
5. Players confirm their names, readiness, and turn order.
6. The host loads a seeded junction state and starts the first turn.
7. The active player selects AR tabletop, chase, or first-person view.
8. The active player drives toward the live intersection.
9. Any significant collision involving the player car begins the crash phase, regardless of which vehicle initiated contact.
10. Traffic continues moving and colliding with the player car, wreckage, and other traffic.
11. The authoritative score rises on every device as the pileup develops.
12. A close crash camera holds while consequential impacts continue.
13. After the action has had time to play out, the camera may widen to show the full damage field.
14. The host detects that the crash has settled or reaches the safety timeout.
15. Every device displays the same result and leaderboard.
16. The host resets the junction to an equivalent seeded state and passes the control token.
17. After the chosen number of rounds, the highest match score wins.

## 7 Player Roles

### Session Host

The session host runs the canonical simulation and multiplayer state. The host may also be a driver or spectator. For the proof of concept, the session ends if the host leaves. Host migration is a later feature.

### Active Driver

The active driver owns the control token and may choose any supported camera. The driver can control the car in AR tabletop mode as an RC-like vehicle or use a non-AR driving camera. Changing view does not transfer authority or alter the simulation.

### Spectator

Spectators receive live state and independently choose AR tabletop or a permitted virtual overview camera. They cannot move vehicles, trigger reset, change the score, or advance the turn.

## 8 View and Camera System

Camera mode is a local presentation setting. It is never stored as the authoritative world state and never creates a separate scene.

### AR Tabletop View

- Uses the rear camera and ARKit tracking.
- Places the live game beneath an AR board root aligned to the shared tabletop origin.
- Allows the viewer to move physically around the surface.
- Shows the same entity transforms and events as every virtual camera.
- Allows the active player to drive with native touch controls.
- Shows simplified spectator HUD elements when the local user is not driving.

### Chase View

- Uses a fully virtual camera with no live camera feed.
- Is positioned above and behind the player car, angled slightly downward.
- Keeps the car centered horizontally.
- Keeps the entire car in the lower half of the screen with a visible buffer behind the rear bumper.
- Targets a composition in which the front of the car remains below the screen midline while the road ahead occupies most of the frame.
- Rotates with the car so forward input always moves toward the visible front of the vehicle.

### First Person View

- Uses a virtual hood or low windshield camera attached to the player car.
- Preserves a clear sense of the vehicle's heading and speed.
- Does not use AR camera passthrough.
- May hide the exterior car body where necessary to prevent clipping.

### Non AR Tabletop View

- Uses a movable virtual overview camera over the same miniature presentation.
- Supports development, accessibility, replay, and play when AR is unavailable.
- Must not be implemented as a separate simulation.

### Crash Camera Behavior

The first significant impact starts a close observation window rather than an immediate zoom-out. The default tuning target is:

- Hold a close chase or impact framing for at least 4 seconds.
- Extend the hold while high-value impacts continue.
- Begin widening only after pileup activity decreases or a maximum hold time is reached.
- Blend to a wider damage view over approximately 2 to 3 seconds.
- Keep the player car and main impact cluster near the visual center during the transition.

The AR tabletop camera does not move automatically because the viewer controls it by moving the device. It may display a subtle directional cue toward off-screen impacts.

## 9 Controls and HUD

### Native Touch Controls

The active player's iPhone or iPad shows:

- Left-side steering control.
- Right-side accelerator.
- Right-side brake and reverse control.
- Camera switch control reachable without releasing the accelerator.
- Optional haptic feedback on major impacts.

Controls remain available in AR tabletop mode. Spectators do not see driving controls.

### Keyboard and Controller Support

Unity Editor and desktop development builds use W or Up Arrow for throttle, S or Down Arrow for brake and reverse, and A and D or Left and Right Arrow for steering. Gamepad support is desirable after touch handling is proven.

### Vehicle Direction

Throttle applies force along the car's forward axis. Steering changes the front wheel angle and the vehicle's resulting yaw. Controls must never be interpreted relative to a fixed world direction or screen direction.

### HUD Layout

The center of the screen remains reserved for gameplay. Speed, score, impact status, active player, and camera mode sit near the screen edges and respect device safe areas. Instructions belong in a help overlay rather than permanently occupying the driving view.

## 10 Vehicle and Traffic Simulation

### Player Vehicle

- One original sports car for the first playable build.
- Arcade handling with understandable weight transfer and traction.
- Raycast suspension or another tunable wheel-force controller rather than default transform-based movement.
- Front wheel steering visible on the model.
- Wheel roll derived from distance traveled and wheel radius.
- Wheel meshes never spin about the wrong axis and never rotate while the car is stationary.
- Continuous collision detection at high speed.
- Compound primitive or convex colliders sized to the visible body.
- Tuned center of mass, tire friction, drag, and angular damping.

### Traffic Vehicles

Traffic follows authored splines and a seeded timeline before disruption. Each vehicle remains a physical rigid body controlled toward its lane target rather than a visual mesh teleported along a path.

Traffic control must include:

- Safe spawn occupancy checks.
- Lane leader detection and braking distance.
- Traffic signal or authored right-of-way behavior where used.
- Minimum following gaps.
- Continuous collision detection near the junction.
- A clean transition from lane-following control to damaged or disabled behavior.

Traffic vehicles must not overlap or clip into one another during normal driving. Their colliders, spawn spacing, and control logic are part of acceptance testing.

### Collision Response

Every material collision is resolved by the host's physics scene. A significant player-car collision starts the crash phase whether the player hits traffic or traffic hits the player.

After impact, vehicle propulsion is reduced or disabled according to damage. Rolling resistance, tire friction, drivetrain drag, and sleep thresholds bring vehicles to rest. A damaged car may continue moving when transferred momentum justifies it, but traffic must not push an inert player car indefinitely without producing collisions, damage, and score events.

### Crash Variety

Vehicle mass, center of mass, suspension, friction, collision angle, curb geometry, and impact speed should produce different outcomes. Cars may spin, roll, ride over low wreckage, or pile on top of one another when the forces support it. Variation comes from physical conditions and seeded scenario parameters, not arbitrary unrepeatable animation.

Dynamic vehicle colliders should use convex shapes with enough separation to prevent deep interpenetration. Solver iteration counts, substeps, depenetration limits, and continuous collision detection must be profiled under dense pileups.

## 11 Crash Phase and Turn Resolution

The crash phase begins when any of these conditions occurs:

- The player car participates in a collision above the configured impulse threshold.
- The player car becomes disabled through accumulated damage.
- The player car reaches a designated crash trigger in the junction, if a scenario requires one.

During the crash phase:

- Traffic continues to enter for an authored period.
- All significant vehicle-to-vehicle and vehicle-to-environment collisions can score.
- Previously sleeping bodies wake when struck.
- Repeated low-value contacts are filtered so resting stacks do not farm score.
- The active player retains limited or full control only if the selected ruleset allows it.
- Camera widening waits long enough for secondary impacts to remain visible.

The turn ends when no scoring impact has occurred for approximately 3 seconds and most involved bodies remain below configured linear and angular velocity thresholds. A hard timeout, initially 20 seconds after the first major impact, prevents a turn from hanging.

## 12 Scoring

Only the session host calculates score. Clients display replicated score events and totals.

The initial model is:

```text
Turn score
  = vehicle damage
  + environment damage
  + chain bonuses
  + target bonuses
  + optional special event bonuses
```

Vehicle damage derives from collision impulse, relative speed, vehicle class, remaining damage capacity, and whether the collision is a new material event. Each entity has a maximum score value to prevent repeated contact from generating unlimited points.

Chain bonuses reward distinct vehicles that join the same unresolved pileup. The score presentation should make large cascades feel more valuable than isolated impacts without hiding how the result was earned.

Results include final score, vehicles involved, highest-value impact, longest chain, environmental damage, round standing, and match leaderboard.

## 13 Single World Architecture

The architecture separates simulation, replication, and presentation without duplicating game logic.

```text
Driver input commands
        |
        v
Session host authoritative simulation
  physics  traffic  damage  score  turns
        |
        v
Ticked snapshots and game events
        |
        +----------------------+----------------------+
        |                      |                      |
        v                      v                      v
AR tabletop client       Chase view client      First person client
local AR anchor          local virtual camera   local virtual camera
same entity state        same entity state      same entity state
```

The host maintains canonical transforms in full-scale simulation coordinates. Each client has a presentation synchronizer that reads the most recent replicated state. In AR mode, the synchronizer converts canonical coordinates through the shared board transform and miniature scale. In a virtual driving mode, it renders those coordinates at normal game scale.

The AR presentation may use separate visual proxy objects so physics bodies are never placed beneath a scaled transform hierarchy. These proxies are views of the same entity identifiers and snapshots, not another world or another simulation.

## 14 Runtime Components

### Game Session

Owns lobby state, player roster, ready state, round count, turn order, control token, phase changes, results, and match completion.

### Authoritative Simulation

Owns the fixed game tick, Unity physics scene, entity spawning, vehicle controllers, traffic routes, collision processing, sleep and settle detection, and seeded scenario reset.

### Vehicle Controller

Converts normalized throttle, brake, reverse, and steering input into suspension, tire, drivetrain, and body forces. It also drives visual steering angle and wheel roll from the physical state.

### Traffic System

Spawns seeded traffic, assigns splines and lanes, maintains safe headway, responds to blocked lanes, and yields to physical collision behavior after disruption.

### Crash and Damage System

Classifies collision events, accumulates damage, changes propulsion state, produces scoreable events, and tracks the unresolved pileup.

### Scoring System

Consumes authoritative damage events, enforces per-entity caps, applies chain rules, and produces the replicated score ledger.

### Network Session

Discovers nearby hosts, authenticates a join request within the local session, transmits input commands, replicates snapshots and reliable events, and detects disconnects.

### AR Board Coordinator

Manages plane detection, host placement, world map capture, peer relocalization, board origin, physical scale, tracking quality, reacquisition, and optional marker fallback.

### Presentation Synchronizer

Maps authoritative entity state to local visual objects, interpolates snapshots, selects the active presentation scale, and keeps AR and virtual camera modes tied to the same simulation tick.

### Camera Mode Controller

Switches AR tabletop, non-AR tabletop, chase, first-person, and results cameras locally without loading another gameplay scene.

### User Interface

Displays role-appropriate controls, safe-area-aware HUD, placement guidance, lobby, turn transitions, results, errors, help, and accessibility settings.

## 15 Coordinate Systems and Physical Scale

The simulation uses canonical full-size units, with 1 Unity unit representing 1 meter. This preserves stable vehicle and rigid-body tuning.

ARKit also reports physical space in meters. The AR board transform maps the canonical simulation into physical miniature space:

```text
AR position = board origin + board rotation * canonical position * presentation scale
```

The default presentation scale is 1 divided by 32, or 0.03125. A 4.5-meter canonical car therefore appears approximately 14 centimeters long. Scale can be adjusted during placement within a validated range, but the host's selected value is part of the session configuration and is shared with every peer.

Physics objects remain in the unscaled simulation scene. AR render proxies sit below the locally tracked board origin and receive scaled transforms. A view switch changes the local presentation mapping and camera, not the canonical coordinates.

## 16 Multiplayer Architecture

### Authority Model

The host is authoritative for physics, traffic, collisions, damage, score, phase, and turn ownership. Clients send commands rather than transforms. A client cannot claim a score or set its vehicle position.

### Local Network Transport

The proof of concept uses direct local networking on the same Wi-Fi network:

- Unity Netcode for GameObjects for networked entities and session messages.
- Unity Transport for direct host and client traffic.
- Bonjour or mDNS advertisement for nearby session discovery.
- A QR join code containing the local endpoint and session identifier as a discovery fallback.
- iOS Local Network permission and declared Bonjour service types.

Guest Wi-Fi client isolation can prevent direct device-to-device communication. The application must detect this condition and explain it clearly. A later internet mode may add Unity Lobby and Relay as a fallback, but the initial LAN match must not silently depend on a paid game server.

### Topology

The POC uses a star topology. Every client connects to the host. Clients do not exchange authoritative gameplay state directly with one another.

### Update Rates

Initial targets are:

- Fixed host physics tick at 60 Hz, with 50 Hz acceptable if profiling supports it better.
- Active driver input messages at 30 to 60 Hz.
- State snapshots at approximately 20 Hz.
- Reliable game events for phase, score, spawn, despawn, turn, and results.
- Client rendering at 60 frames per second when possible.
- Spectator interpolation buffer near 75 to 125 milliseconds.

### Driver Responsiveness

The active driver's client predicts its own vehicle from recently submitted input and reconciles toward host snapshots. Spectators interpolate authoritative snapshots. Collision and scoring outcomes always come from the host, even when the driver predicted a slightly different contact moment.

Do not attempt deterministic physics on every device. Only the host simulates the canonical pileup.

## 17 Network State and Messages

Each networked entity has a stable session entity identifier and type. A transform snapshot includes at least:

- Host tick.
- Entity identifier.
- Position and rotation.
- Linear and angular velocity.
- Damage and propulsion state.
- Sleep state when useful for presentation.

Driver input includes:

- Client and turn identifier.
- Target simulation tick.
- Throttle.
- Brake or reverse.
- Steering.
- Sequence number.

Reliable events include:

- Player joined or left.
- Board configuration accepted.
- Ready state changed.
- Turn assigned.
- Countdown started.
- Crash phase started.
- Damage and score event.
- Camera-independent impact focus target.
- Turn settled.
- Results finalized.
- Junction reset.
- Match completed.

Camera changes usually remain local. A player may optionally broadcast a viewing status icon, but a camera message never controls the simulation.

## 18 Shared AR Alignment

### Host Placement

The host scans the table, selects a detected horizontal plane, previews the board footprint, rotates and scales it, and confirms placement. The host's board origin and selected scale become session configuration.

### Peer Relocalization

The host captures an ARWorldMap or equivalent ARKit-specific mapping data and transmits it to joining iOS devices. Each peer relocalizes against the physical environment and places its local board origin at the shared anchor.

AR Foundation will manage common AR session behavior. Where AR Foundation does not expose the required world map or collaboration feature cleanly, a small native iOS plugin will bridge Unity to ARKit. This capability must be proven in an early technical spike rather than assumed.

### Marker Fallback

A printed image or QR marker at the table may provide a fast fallback origin and a cross-platform path for later Android support. The marker is an alignment aid, not a separate WebAR implementation. Once alignment is stable, the virtual board may cover it.

### Tracking Quality

Each client monitors its own tracking state. If tracking degrades, the game keeps receiving the authoritative simulation but pauses or softens AR presentation until the board can be reacquired. One client's tracking loss must not pause the match for everyone unless the active driver's rules require it.

### Safety

Placement guidance should encourage a clear surface and adequate walking space. Spectators receive a boundary reminder and should not be required to stare through the screen while moving quickly around furniture.

## 19 Game Phase State Machine

The host controls this state sequence:

```text
Lobby
  -> Board alignment
  -> Player ready check
  -> Turn countdown
  -> Driving
  -> Crash
  -> Settle delay
  -> Results
  -> Reset
  -> Next turn or match results
```

Only the host advances phases. Every phase change carries a host tick and a sequence number so clients cannot apply stale transitions.

## 20 Session Setup and Joining

### Host Flow

1. Launch the native game.
2. Tap Create Nearby Game.
3. Enter a name and choose the number of rounds.
4. Grant local network and camera permissions with clear explanations.
5. Scan and place the tabletop board.
6. Advertise the session over the local network.
7. Show a short room code and optional QR code.
8. Review joined players and start when everyone is ready.

### Join Flow

1. Launch the native game.
2. Tap Join Nearby Game.
3. Select the discovered host or scan the QR code.
4. Enter a name.
5. Grant local network and camera permissions.
6. Receive the board map and relocalize to the table.
7. Confirm that the board outline matches the physical surface.
8. Mark ready.

### Turn Handoff

At the end of a turn, all devices show the same result. The next player receives the control token after reset. Other players remain connected and may continue inspecting the reset board in AR.

## 21 Technology Stack

### Game Runtime

- Current validated Unity 6 LTS release at project start.
- C sharp.
- Universal Render Pipeline.
- Unity Input System.
- Cinemachine for virtual camera composition and transitions where appropriate.
- Unity Physics through PhysX with custom arcade vehicle forces.

### Augmented Reality

- AR Foundation.
- Apple ARKit XR Plugin.
- Native ARKit bridge for ARWorldMap or collaboration functions not exposed by the selected package version.
- ARCore XR Plugin in a later Android phase.

### Multiplayer

- Netcode for GameObjects.
- Unity Transport with direct LAN host connections.
- Bonjour or mDNS discovery bridge.
- Optional Unity Lobby and Relay only as a later remote or network-isolation fallback.

### Content

- Unity scenes and prefabs.
- ScriptableObjects for vehicle tuning, scoring rules, traffic schedules, map configuration, and quality levels.
- Addressables only if content loading needs justify the added complexity.
- Original optimized 3D assets, textures, particles, audio, and haptic patterns.

### Web Companion

- Existing Vite and Three.js site may remain on GitHub Pages as a landing page, development reference, instructions page, and App Store or TestFlight link destination.
- The web companion is not the authoritative game client and does not offer a fake AR gameplay button.
- A future browser spectator would require a separately scoped network protocol and renderer and is not part of the first production architecture.

## 22 Distribution Strategy

### Proof of Concept

Use signed development builds and TestFlight for iPhone and iPad testing. This provides the ARKit and local network access required to validate the product.

### Initial Release

Use the Apple App Store for the full iOS and iPadOS game. The website explains the game and links to the correct installation path.

### App Clip Investigation

After the full game loop is proven, run a separate App Clip feasibility spike. Measure binary size, startup time, asset delivery, ARKit availability, LAN discovery, and multiplayer join behavior. Do not promise the complete Unity game through an App Clip until a device build proves it fits Apple's current technical and review requirements.

### Android

Unity and AR Foundation allow a later native Android build using ARCore. Shared physical alignment across iOS and Android may use a common image marker because ARKit world maps are Apple-specific. Android Chrome WebXR is not the production path for the shared native game.

## 23 Graphics and Art Direction

The target is a premium miniature crash diorama rather than a photorealistic open world.

The first junction should include:

- Distinct road surfaces, lane markings, curbs, sidewalks, barriers, signs, and street furniture.
- Original vehicle silhouettes readable at tabletop scale.
- Strong contact shadows that visually attach cars to the board.
- Directional sunlight and controlled ambient lighting.
- Brake lights, headlights, tire smoke, sparks, glass, debris, and limited fire effects.
- Level-of-detail models and effect budgets appropriate to non-Pro iPhones.

AR lighting estimation may influence the board's ambient tone, but gameplay readability takes priority over matching every room light.

## 24 Audio and Haptics

The proof of concept needs:

- Engine sound tied to throttle and drivetrain state.
- Tire and road sound tied to slip and speed.
- Impact sounds selected by material and severity.
- Glass, debris, horn, traffic, and environmental sounds.
- Score escalation and end-of-turn cues.
- Haptic feedback for meaningful impacts on the active player's device.

Each device should avoid playing every distant impact at full volume. The local camera position and role determine the mix, while the underlying impact events remain shared.

## 25 Content Definition

The first release contains one original four-way urban junction with:

- One player approach road.
- Cross traffic in both directions.
- At least one additional traffic stream that can feed a pileup.
- Two to four lanes per major road as performance permits.
- Guardrails or readable boundaries on the approach.
- Several light traffic cars.
- At least one heavy vehicle.
- A few parked or stopped vehicles that can become blockers.
- Destructible lightweight roadside props.
- One or two high-value environmental targets.

The scenario uses a seeded traffic schedule so every player in a round receives an equivalent opportunity. Player actions and host physics create the emergent result.

## 26 Persistence and Privacy

No account is required for local play.

Store locally:

- Player nickname.
- Audio and haptic preferences.
- Control and camera preferences.
- Accessibility settings.
- Local best scores.
- Recently used round count.

Do not collect camera imagery or spatial maps on a server. AR mapping data is exchanged only within the local session and discarded when the session ends unless a later feature explicitly obtains user consent for local persistence.

## 27 Performance Targets

Initial targets are:

- 60 frames per second during driving on the baseline device.
- At least 30 frames per second during the worst expected pileup.
- Local control response below 50 milliseconds before network reconciliation.
- Host-mediated visible response below 100 milliseconds on a healthy local network.
- View switching without a world reset or visible state discontinuity.
- AR board placement stable enough that normal viewing does not produce distracting swimming.
- Peer tabletop alignment within approximately 5 centimeters and 5 degrees after relocalization, subject to device and room conditions.
- Cached scene load to playable state under 8 seconds on the baseline device.
- Turn reset under 3 seconds.
- Initial physics budget near 1 player vehicle, 20 to 40 traffic vehicles, and a controlled number of debris bodies.

The host may run physics without AR rendering when using a virtual camera, but it still carries the highest CPU and thermal load. Host performance must be measured separately from spectator performance.

## 28 Testing Strategy

### Simulation Tests

- Verify throttle force follows the vehicle's forward axis.
- Verify steering angle and wheel visuals match vehicle heading changes.
- Verify wheel roll stops when travel stops.
- Verify traffic maintains spacing before disruption.
- Verify every high-speed collision uses continuous collision handling.
- Verify traffic hitting the player starts and scores a crash.
- Verify damaged vehicles lose propulsion and settle.
- Verify repeated resting contact does not farm score.
- Verify seeded reset returns the same starting opportunity.

### Network Tests

- Compare entity identifiers, scores, phase, and turn state on every device.
- Introduce latency and packet loss and verify interpolation and reconciliation.
- Verify unauthorized clients cannot submit driving commands.
- Verify joining and reconnect error states are understandable.
- Test home Wi-Fi, guest Wi-Fi, enterprise Wi-Fi, and personal hotspot conditions.

### View Consistency Tests

Record the same turn simultaneously from an AR spectator and the active driver's virtual camera. At matched host ticks, every vehicle position, rotation, crash, and score event must correspond. Switching a device between AR and virtual view must reveal the current state immediately without reloading or replaying the turn.

### AR Tests

- Place the board on tables of different sizes, textures, and lighting.
- Join from opposite sides of the table.
- Walk around the board and test occlusion and tracking loss.
- Verify the board remains above the detected surface and at miniature scale.
- Verify relocalization after briefly pointing away.
- Verify one client's tracking failure does not corrupt the shared simulation.

## 29 Milestones

### Milestone 0 Preserve the Browser Mechanics Reference

- Freeze the current PWA as a reference build.
- Document handling, camera, scoring, and collision lessons worth carrying forward.
- Remove Quick Look AR from the product roadmap.

Success means the team has a clear list of reusable design findings without treating the browser code as the production foundation.

### Milestone 1 Unity Single Device Crash Prototype

- Create the Unity project and core scenes.
- Port one road, player car, traffic stream, collision scoring, and reset loop.
- Implement native touch controls and virtual chase camera.
- Establish a fixed authoritative simulation tick even before networking.

Success means the crash loop is at least as playable as the current browser prototype on one iPhone.

### Milestone 2 Driving and Pileup Quality

- Tune arcade steering, suspension, traction, braking, and camera composition.
- Prevent pre-impact traffic clipping.
- Support rollover, stacking, varied impulses, and realistic settling.
- Implement first-person, chase, and non-AR tabletop views from one state.
- Add delayed crash-camera widening.

Success means driving is readable and repeatable, and pileups are varied without looking broken.

### Milestone 3 Single Device Native AR Tabletop

- Detect a horizontal surface.
- Preview, place, rotate, and scale the live board.
- Render the active Unity simulation through AR tabletop presentation.
- Drive the car in AR with RC-like controls.
- Switch between AR and virtual cameras during a running turn.

Success means camera switching changes only presentation and the same turn continues without discontinuity.

### Milestone 4 Local Multiplayer and Shared Alignment

- Add nearby session discovery and QR fallback.
- Add host-authoritative replication and driver prediction.
- Share the board origin through ARWorldMap or the validated fallback.
- Support one AR host and at least three joining devices.
- Add control-token turn handoff and shared results.

Success means 2 to 4 iPhones complete a match and observe the same crashes from different views.

### Milestone 5 Presentation and Device Readiness

- Add final HUD, results, audio, haptics, particles, quality settings, and permission flows.
- Profile thermal behavior, memory, battery, and network resilience.
- Test TestFlight onboarding with new users.

Success means a group can install the build, create a session, align the board, and finish a match without developer assistance.

### Milestone 6 Distribution Experiments

- Prepare App Store release requirements.
- Run the App Clip feasibility spike.
- Evaluate native Android and common-marker alignment.
- Consider optional Relay fallback only after LAN behavior is understood.

## 30 Acceptance Criteria

### Gameplay

- Forward input accelerates along the visible front of the car.
- Steering visibly turns the front wheels and changes the car's heading naturally.
- The player car remains framed correctly in chase view.
- Normal traffic does not clip or overlap before a crash.
- Player-initiated and traffic-initiated impacts both create crash events.
- Secondary traffic impacts produce damage, score, and physical response.
- Cars spin, roll, stack, and settle according to forces without remaining unnaturally powered.
- The crash camera waits for the pileup before widening.
- Reset restores an equivalent seeded scenario.

### Shared World

- Every device displays entity state originating from one host simulation.
- AR and virtual cameras show the same action at the same host time.
- Switching camera mode never starts another simulation or loses current state.
- The active player can drive in AR tabletop mode.
- The active player can enter first-person mode while spectators continue watching the same turn in AR.

### Augmented Reality

- The board appears on the selected horizontal surface rather than at road level.
- Cars appear near the chosen miniature scale.
- At least two devices align the board to the same physical table location.
- Temporary tracking loss does not reset or desynchronize gameplay.

### Multiplayer

- Two to four players can join on a supported local network.
- Exactly one player controls the car during a turn.
- Scores, collisions, turn state, and results agree on every device.
- A complete multi-round match can be played.

### Performance

- Driving targets 60 frames per second on the baseline device.
- Worst-case pileup remains at or above 30 frames per second.
- Local network latency does not make steering feel detached from the car.
- The host completes a full match without thermal shutdown or unrecoverable memory pressure.

## 31 Explicit Non Goals for the Proof of Concept

- Pure PWA gameplay as the production iPhone AR runtime.
- Apple Quick Look as an interactive game mode.
- Separate AR and driving simulations.
- Remote internet matchmaking.
- User accounts or cloud save.
- Voice chat.
- Career mode or progression.
- Vehicle garage or customization.
- Multiple maps.
- Photorealistic graphics.
- Soft-body vehicle deformation.
- Host migration.
- Cross-platform shared AR in the first release.
- A guaranteed App Clip distribution path before feasibility testing.
- AI-generated maps, traffic, or commentary.

## 32 Risks and Mitigations

### Shared AR Relocalization

Risk: devices may not agree closely enough on the board's physical location.

Mitigation: prove ARWorldMap transfer early, provide a marker fallback, let clients confirm alignment, and define measurable position and rotation tolerances.

### Host Performance

Risk: one phone may overheat while running physics, networking, and AR rendering.

Mitigation: profile a non-Pro iPhone, cap active bodies and particles, use sleeping and level of detail, and allow the host to use a lower rendering quality without changing simulation quality.

### Driver Latency

Risk: sending input through another player's host device may make steering feel delayed.

Mitigation: use local prediction and host reconciliation, keep the host tick stable, and measure end-to-end latency before adding content.

### Dense Collision Stability

Risk: fast vehicles may tunnel, interpenetrate, jitter, or explode apart in a pileup.

Mitigation: use continuous collision detection, convex colliders, appropriate substeps and solver iterations, capped depenetration, and deliberate mass ratios.

### LAN Restrictions

Risk: guest or managed Wi-Fi may block peer connections.

Mitigation: detect discovery and connection failures, support QR endpoint entry, document client-isolation limitations, and evaluate Relay only as an explicit fallback.

### Unity and App Clip Size

Risk: the complete Unity runtime and assets may not fit or launch quickly enough as an App Clip.

Mitigation: keep App Clip delivery outside the critical path and test a measured build before committing to that promise.

### Intellectual Property Similarity

Risk: the game may resemble its inspirations too closely.

Mitigation: use original creative assets, map design, names, interface, scoring constants, sounds, and progression. Treat the references as interaction patterns only.

## 33 Open Product Decisions

- Whether the first public release requires a full App Store installation or also attempts an App Clip.
- Whether the proof of concept supports 2 to 4 players or stretches to 6.
- The minimum supported iPhone and iPad models.
- Whether the active driver retains steering after the first major impact.
- Whether cumulative score or best single-turn score wins a match.
- The final board scale and allowed user adjustment range.
- Whether a printed marker is mandatory, optional, or only a recovery tool.
- Whether the host may choose a non-AR placement mode for accessibility.
- When Android becomes a supported target.
- Whether internet Relay is acceptable as a fallback for isolated Wi-Fi networks.

None of these decisions changes the one-world architecture.

## 34 Recommended Immediate Build Order

1. Archive the browser prototype as a reference milestone.
2. Create a Unity 6 project with URP, Input System, AR Foundation, ARKit, Netcode for GameObjects, and Unity Transport.
3. Define canonical entity identifiers, fixed ticks, input commands, snapshots, and game phases before building screens.
4. Port the player vehicle, one traffic lane, one collision event, scoring, and reset.
5. Tune driving, wheel visuals, traffic separation, collision stability, and settling.
6. Implement chase, first-person, and non-AR tabletop cameras over the same state.
7. Add a single-device AR board root and verify live view switching.
8. Prove iOS ARWorldMap sharing or select the marker fallback.
9. Add direct LAN discovery, host authority, client interpolation, and driver prediction.
10. Add lobby, permissions, turn handoff, results, audio, haptics, and visual polish.
11. Test with 2 to 4 physical devices around one table.
12. Decide App Store, App Clip, Android, and Relay work only after the shared game is stable.

## 35 Migration from the Existing Web Prototype

The current web build should inform, but not constrain, the native architecture.

Reuse as design reference:

- Desired car framing and delayed crash camera behavior.
- Control feel observations.
- Traffic timing and junction layout experiments.
- Collision and scoring thresholds that proved useful.
- HUD hierarchy and help content.
- Original visual identity and naming that remain appropriate.

Rebuild in Unity:

- Physics and vehicle controller.
- Traffic movement and separation.
- Camera system.
- Scene and asset pipeline.
- Multiplayer authority and replication.
- AR placement and shared alignment.
- Touch interface and native permissions.
- Audio, haptics, persistence, and distribution.

Do not port:

- Quick Look USDZ gameplay path.
- Marker-based browser AR as the primary iPhone experience.
- Browser WebRTC QR offer and answer exchange.
- Independent AR animation or exported static board state.

## 36 Implementation References

- [Apple multiuser AR experience](https://developer.apple.com/documentation/arkit/creating-a-multiuser-ar-experience)
- [Apple collaborative AR session](https://developer.apple.com/documentation/arkit/creating-a-collaborative-session)
- [Apple ARWorldMap](https://developer.apple.com/documentation/arkit/arworldmap)
- [Apple local network privacy](https://developer.apple.com/videos/play/wwdc2020/10110/)
- [Apple App Clips](https://developer.apple.com/documentation/appclip)
- [Unity AR development](https://docs.unity3d.com/Manual/AROverview.html)
- [Unity Netcode for GameObjects](https://docs.unity.com/multiplayer/netcode/netcode)
- [Unity Relay and Netcode for GameObjects](https://docs.unity.com/en-us/relay/relay-and-ngo)
- [WebKit iOS WebXR status](https://bugs.webkit.org/show_bug.cgi?id=309550)
- [Apple AR Quick Look](https://developer.apple.com/quick-look-gallery/)

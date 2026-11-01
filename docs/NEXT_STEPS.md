# Next steps

Where Crashout stands after the Crash Mode, art and tabletop-AR work (PR #1), and what to do next, in priority order. Each item says what "done" looks like so it can be picked up cold.

## 1. Where things stand

| Area | State |
| --- | --- |
| Crash Mode rules (junctions, boost, impact time, aftertouch, Crashbreaker, target car, medals) | Implemented. Covered by Play Mode tests on macOS. Not hand-tuned. |
| Stylized art, post-processing, HUD, crash FX | Implemented. Looks right in the editor. Not seen on a phone. |
| Tabletop AR (`ArTabletopMode`) | Written against AR Foundation 6.3. Falls back correctly in the editor. **Never run on a device.** |
| Touch controls (steer, throttle, brake, boost, Crashbreaker) | Drawn and wired. **Never tested on a touchscreen.** |
| Turn-based multiplayer | Not started. One player, one run. |
| Shared table alignment, discovery, replication | Not started. Data boundaries exist (`PresentationSynchronizer`, `EntitySnapshot`) but nothing uses them yet. |

Tests: Edit Mode 6, Play Mode 4 (`JunctionSmokeTests`, `CrashModeTests` x3). Edit Mode was not re-run after the Crash Mode work.

## 2. First iPhone build (do this first)

Everything else is guesswork until the game runs on a phone. This is the cheapest way to find out what is actually wrong.

**Setup**
1. Open `Unity/` in Unity `6000.3.27f2` with iOS Build Support.
2. Run `Crashout > Configure AR For iPhone` once. It should report "already complete". It assigns the ARKit loader, adds the AR background feature to `CrashoutRenderer.asset`, and sets the camera usage text.
3. Set the Apple Team ID in Player Settings > iOS (it is blank in the repo). Confirm the bundle ID `com.crashout.game` is yours to sign.
4. File > Build Profiles, switch to iOS, confirm `Startup.unity` is the only scene, and build. Turn on Development Build for the first pass.
5. Open the generated Xcode project, pick the device, run.

**What to check, in order**
- [ ] App launches, asks for camera permission, and the camera feed appears.
- [ ] "Move your phone slowly" message shows; a cyan board outline appears on a flat surface.
- [ ] `-` / `+` resize the board; PLACE BOARD puts the junction on the table and starts the run.
- [ ] The city stays pinned to the table as you walk around it (this validates the camera-pose mapping).
- [ ] Left/right, throttle, brake and BOOST all respond to touch; two buttons can be held at once.
- [ ] Crash, aftertouch, DETONATE, results screen, RETRY and NEXT JUNCTION all work by touch.
- [ ] Frame rate (see below), heat, and whether the HUD is readable and the buttons are big enough.
- [ ] Tracking-lost banner appears and clears when you cover the camera.

**Most likely things to break**
- Overlay camera does not line up with the feed, or the scene is mirrored or offset. The mapping is in `ArTabletopMode.SyncCamera`. Compare against a known marker on the table.
- The feed is black. Check that `CrashoutRenderer.asset` lists the AR Background feature and that the pipeline asset uses that renderer.
- Pink materials. `Editor/EnsureRuntimeShaders.cs` should have added the shaders; confirm in Project Settings > Graphics > Always Included Shaders.
- The AR rig never starts. Look for `Crashout: AR is unavailable` in the Xcode log. That means the ARKit loader or camera permission is missing and the app silently fell back to the virtual camera.
- Multi-touch: IMGUI `RepeatButton` handles simultaneous presses poorly on some devices. If holding steer plus throttle fails, move to the UI approach in section 6.

**Performance budget.** The scene is roughly 500 objects with bloom, soft shadows, 4x MSAA and HDR over a camera feed. Target 60 fps. If it misses, cut in this order: bloom, MSAA to 2x, shadow distance (70 m to 40 m), the 100-building skyline (it is already skipped in AR mode), then shadow cascades.

## 3. Tune the feel

Do this by playing, not by reading. Numbers below are the first guesses and where to change them.

| Knob | Now | Where |
| --- | --- | --- |
| Medal thresholds (Bronze/Silver/Gold) | 50k/100k/160k, 80k/160k/250k, 100k/200k/320k | `CrashScenario.cs` |
| Target car bonus | $25,000 | `CrashScenario.TargetBonus` |
| Traffic speed per lane | 9 to 13 m/s | `Lane(...)` calls in `CrashScenario.cs` |
| Traffic spacing and timing | hand-placed | `At(...)` slots in `CrashScenario.cs` |
| Vehicle mass and score cap | sedan 1.4 t, bus 7.5 t, tanker 9 t | `VehicleSpec.cs` |
| Per-contact score | `impulse*58 + speed*125` | `ScoreLedger.TryScore` |
| Chain bonus | $225 per extra distinct car | `ScoreLedger` |
| Crashbreaker charge per wreck | 0.09 to 0.35, plus amount/160000 | `VehicleSpec.CrashbreakerCharge`, `CrashoutGameSession.OnScored` |
| Crashbreaker blast | radius 6 to 17 m, force `mass*strength*15` | `CrashoutGameSession.TryCrashbreaker` / `Detonate` |
| Impact time | 1.5 s at 0.3x | `CrashoutGameSession` constants |
| Aftertouch | 3.6 s budget, 22 m/s2 push | `CrashoutGameSession.ApplyAftertouch` |
| Boost | 3.2 s, 2.1x force, 1.45x top speed | `ArcadeVehicleController` |

Scripted straight-line runs landed at Bronze or Silver on every junction (about $87k, $140k, $175k). A good human run should reach Gold; if Gold is trivial or impossible after a few real attempts, move the thresholds, not the physics.

Questions to settle by playing:
- Does the pile-up look chaotic enough, or do wrecks stop too soon? (`TrafficVehicle` wreck damping is 0.12 / 0.1.)
- Is aftertouch too strong, too weak, or too short?
- Is the Crashbreaker satisfying to time? Burnout makes you mash the button to refill the meter before it detonates; here it is one tap. Decide whether to add the mash.
- Is the starting approach long enough, and does the player get a real chance to aim?

## 4. Turn-based multiplayer (the core product loop)

The PRD describes players on one table taking turns on the same junction. Burnout's own multiplayer maps onto this directly:

| Burnout mode | Crashout equivalent |
| --- | --- |
| Crash Tour (golf-style, fewest attempts to hit a target) | Optional later variant |
| Crash Party (take turns over several rounds) | **The main mode.** Highest total after N rounds wins. |
| Crash Battle (simultaneous) | Out of scope for the table format |

**Suggested build order**
1. Local pass-and-play first, no networking: a `MatchController` that holds players, rounds and scores, runs one turn per player on the same seeded junction, and shows a leaderboard.
2. Use the session phases that already exist but are unused: `Lobby`, `ReadyCheck`, `Countdown`, `MatchResults` in `SessionStateMachine`.
3. Add a countdown before each turn (3, 2, 1) so players are ready.
4. Per-round scenario selection (fixed, rotating, or vote).
5. Match results screen: totals, best chain, medals, who hit the target.

Done when: three people can pass one phone around, play three rounds, and see a winner.

The scenario is already seeded and deterministic in layout (`CrashScenarios.Create(index)`), so every player gets the same traffic. Physics itself is not bit-exact across devices, which is why the host-authoritative model below matters.

## 5. Shared AR table (multi-device)

Not started. Work needed, roughly in order:
1. **Discovery.** Bonjour/Multipeer so nearby phones find each other without accounts.
2. **Host-authoritative simulation.** One device runs the physics; the others render snapshots. `EntitySnapshot` and `PresentationSynchronizer` (interpolated proxies) were written for this and are currently unused. `Netcode for GameObjects 2.13.1` and `Unity Transport 2.7.3` are already in the manifest.
3. **Proxy rendering.** Clients need visual copies of the cars and city driven by snapshots. The single-device AR mode renders the real sim with a second camera, which does not work for clients. Expect this to be the biggest structural change.
4. **Shared alignment.** Every phone must see the board in the same place. Options: ARKit collaborative sessions / shared world map, or a marker (QR or image) on the table that every device detects. A marker is simpler and more robust to try first.
5. **Input path.** Clients send `DriverInputCommand` (already defined, includes `Boost`) to the host; the host applies it to the active driver.
6. **Join in progress, disconnects, host migration.** Decide what happens when the host leaves (simplest: end the match).

Done when: two iPhones show the same junction in the same spot on the same table and the second player can take a turn.

## 6. Technical debt and risks

- **IMGUI HUD.** Fast to build, but multi-touch, safe-area handling, scaling and styling are all weak. Move to UI Toolkit or uGUI before shipping. `CrashoutHud.cs` is the only file affected.
- **Runtime-created materials and shaders.** The art relies on `Shader.Find` at runtime. `EnsureRuntimeShaders` covers the three shaders in use; adding any new one requires adding it there too. Longer term, ship real material assets.
- **`CrashoutArt.cs` is large.** It builds materials, textures, the city, props, cars, post-processing and atmosphere. Split it before it grows (cars, city, atmosphere).
- **Time scale used for pause and slow motion.** `ArTabletopMode` pauses with `Time.timeScale = 0` and the session uses 0.3 for impact time. They guard against each other, but a proper pause/time service would be safer.
- **Magic layer numbers.** Vehicles are layer 8, debris layer 9, the AR reticle layer 31, with `Physics.IgnoreLayerCollision(9, 8)`. Name them and set them in project settings.
- **Board scale range widened.** The PRD says 1:24 to 1:40; code allows 1:24 to 1:64 so the 52 m junction fits a coffee table (1:64 is about 0.8 m). Decide whether to keep this or shrink the AR junction instead, and update the PRD to match.
- **Test order dependence.** Play Mode tests share one running world. The smoke test now resets to the first junction, but any new test should do the same.
- **Mesh cache.** `ProceduralMeshes` caches meshes in a static dictionary and never frees them. Fine today, worth revisiting if sizes become dynamic.
- **No CI.** The repo has no checks configured. A GitHub Actions run of the Edit Mode tests (and Play Mode with a Unity license) would protect the next PRs.
- **Edit Mode tests need extending.** Add unit tests for `ScoreLedger.TryAward` / `AddBonus`, `CrashScenario.MedalFor`, and the Crashbreaker charge maths.

## 7. Crash Mode depth (after the basics feel good)

Ideas drawn from Burnout, none started:
- More junctions, and **layered** junctions (bridges, ramps, tunnels, multiple levels), which Revenge leaned on.
- Traffic checking: ram same-direction traffic on the approach to launch it into the junction.
- More vehicle classes (motorbike, police car, ambulance, school bus) with their own mass and score.
- Hazards and set pieces: gas stations, billboards, trains, a lamp-post domino row.
- Mash-to-charge Crashbreaker and a tap-to-slow Impact Time button instead of automatic slow motion.
- Score "feel": stronger audio-visual feedback per hit, a big end-of-run replay of the best moment, damage deformation or detaching panels.
- **Audio.** There is none yet: engine, impacts, glass, explosions, a score tick, a music sting on medals. This will change how the game feels more than most visual work.
- Persistence: best score and medal per junction, unlocks.
- Replay or share: record the crash and export a clip. High value for a game built around spectacle.

## 8. Product and release

- App icon, launch screen, name and bundle ID.
- TestFlight build and a small playtest with real groups around a real table.
- Update the companion website (`app/`, `public/`) from "coming soon" to a TestFlight link when a build exists.
- Apple requirements: privacy policy and the camera usage string (already set), age rating, App Store screenshots.
- Accessibility: colour-blind safe target marker, a reduce-shake option, larger touch targets.
- Burnout homage: the game borrows Crash Mode's mechanics. Game mechanics are generally not covered by copyright, but keep Burnout/EA names, art, sounds and trade dress out of anything shipped, and get proper legal advice before release.

## 9. Open questions for the owner

1. Is the first release single-device AR or does it need multi-device? That decides whether section 5 blocks launch.
2. Is the tabletop board size (about 0.8 to 1.6 m) acceptable, or should the junction be smaller for the table format?
3. Should the Crashbreaker stay one tap, or become the Burnout mash?
4. Fixed junction rotation per match, or let the group pick?
5. Which devices must be supported (minimum iPhone, iOS 15 is the current floor)?

## Suggested order

1. First iPhone build and fix what it exposes (section 2).
2. Tune the feel with real play (section 3).
3. Pass-and-play turns and leaderboard (section 4).
4. Audio and a first TestFlight build (sections 7 and 8).
5. Multi-device shared table (section 5).

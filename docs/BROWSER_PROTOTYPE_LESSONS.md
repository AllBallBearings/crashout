# Browser prototype lessons

The Three.js prototype was frozen in Git at `50d158f` before the native rebuild. It remains a mechanics reference, not production runtime code.

## Carry forward

- The player approach reads best when the chase camera keeps the entire car below screen center and leaves most of the frame for the road ahead.
- Throttle must always act along the visible front of the vehicle. Steering should rotate visible front wheels and scale with forward velocity.
- A player speed near 20–22 metres per second made the approach readable in the browser prototype; the native controller begins with a 31 m/s ceiling so device tuning has room.
- Traffic was most legible with roughly 2.1–2.3 metre lane offsets and 7–9 metre pre-impact spacing.
- The initial impact should not trigger an immediate overhead cut. Hold the close camera for at least four seconds and blend wide over about 2.5 seconds, extending the hold when scoring contacts continue.
- Significant contacts should require both impulse and relative speed. Repeated contact inside a short cooldown must not award more score.
- Distinct vehicles joining the same unresolved pileup are a clearer chain metric than raw collision count.
- A deterministic starting arrangement is essential for fair pass-the-device testing and eventual multiplayer turn order.

## Rebuilt natively

- Vehicle, traffic, and pileup physics.
- Camera modes and crash-camera state.
- Host-authoritative damage, score, settle, and reset.
- Touch controls, haptics, audio, and safe-area HUD.
- Multiplayer input and snapshot replication.
- AR placement, scale, and shared physical alignment.

## Explicitly retired

- Quick Look USDZ as a gameplay or launch path.
- WebXR as the iPhone production runtime.
- Independent AR animation or a second board simulation.
- Browser-side QR offer/answer exchange.

The companion website may show screenshots, explain the game, and link to TestFlight or the App Store. It must not imply that its visual is the live native match.

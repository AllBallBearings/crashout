using System;
using System.Collections.Generic;
using Crashout.Presentation;
using Crashout.Vehicle;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Crashout.Simulation
{
    [DefaultExecutionOrder(-200)]
    public sealed class CrashoutGameSession : MonoBehaviour
    {
        private const float SettleQuietSeconds = 3f;
        private const float CrashSafetyTimeout = 24f;
        private const float AftertouchSeconds = 3.6f;
        private const float ImpactTimeSeconds = 1.5f;
        private const float ImpactTimeScale = 0.3f;
        private const float MinimumCrashbreakerCharge = 0.12f;
        // Launch meter: a golf-style swing bar. The green zone gives a perfect, boosted start.
        private const float LaunchSweepSeconds = 1.1f;
        public const float LaunchZoneMin = 0.8f;
        public const float LaunchZoneMax = 0.95f;
        private const float CruiseSpeed = 20f;
        private const float PerfectLaunchBoostSeconds = 1.3f;
        private const float NominalLaunchSpeed = 5f;
        /// <summary>
        /// Seconds from launch to the junction centre on the original 22 m flat approach. The traffic
        /// layouts in <see cref="CrashScenarios"/> were tuned for this, so traffic is phase-shifted by
        /// the difference between it and the live estimate for the real (much longer) runway.
        /// </summary>
        public static readonly float DesignArrivalSeconds =
            ApproachPlanner.SecondsToJunction(null, -22f, NominalLaunchSpeed, false, PerfectLaunchBoostSeconds);

        private readonly SessionStateMachine stateMachine = new SessionStateMachine();
        private readonly ScoreLedger scoreLedger = new ScoreLedger();
        private readonly LocalDriverInput localInput = new LocalDriverInput();
        private readonly List<DamageableVehicle> vehicles = new List<DamageableVehicle>();
        private readonly List<TrafficVehicle> trafficVehicles = new List<TrafficVehicle>();
        private readonly List<ResettableBody> resettableBodies = new List<ResettableBody>();
        private readonly HashSet<uint> detonated = new HashSet<uint>();

        private ArcadeVehicleController player;
        private DamageableVehicle playerVehicle;
        private CrashoutCameraRig cameraRig;
        private CrashScenario scenario;
        private uint targetEntityId;
        private bool targetDown;
        private float crashStartedAt;
        private float lastScoringImpactAt;
        private float impactTimeUntil;
        private float aftertouchRemaining;
        private bool timeScaleAltered;
        private float crashbreakerCharge;
        private int crashbreakersUsed;
        private int bestChain;
        private float readyStartedAt;
        private uint turnId = 1;
        private RunwayProfile runway;
        private float junctionEta;
        private bool approachLocked;
        private float launchedAt;
        private float stalledSeconds;
        private float flippedSeconds;
        private float rawThrottle;
        private bool rawBoost;
        private Vector3 aftertouchRight = Vector3.right;
        private Vector3 aftertouchForward = Vector3.forward;
        private bool aftertouchFrameHeld;

        public bool IsAuthority { get; private set; } = true;
        public uint HostTick => stateMachine.HostTick;
        public GamePhase Phase => stateMachine.Phase;
        public int Score => scoreLedger.Total;
        public int BonusScore => scoreLedger.BonusTotal;
        public int ChainCount => scoreLedger.ChainCount;
        public int BestChain => Mathf.Max(bestChain, scoreLedger.ChainCount);
        public int WreckedCount => scoreLedger.ChainCount;
        public int CrashbreakersUsed => crashbreakersUsed;
        public float CrashbreakerCharge => crashbreakerCharge;
        public float AftertouchFraction => Mathf.Clamp01(aftertouchRemaining / AftertouchSeconds);
        /// <summary>The aftertouch wish being applied this tick (screen space), or zero.</summary>
        public Vector2 AftertouchInput { get; private set; }
        public bool AftertouchActive => AftertouchInput.sqrMagnitude > 0.01f;
        /// <summary>World direction the wreck is being pushed, for the HUD arrow and effects.</summary>
        public Vector3 AftertouchWorldDirection => aftertouchRight * AftertouchInput.x + aftertouchForward * AftertouchInput.y;
        public bool IsImpactTime => Phase == GamePhase.Crash && Time.unscaledTime < impactTimeUntil;
        public bool CanCrashbreak => (Phase == GamePhase.Crash || Phase == GamePhase.SettleDelay) && crashbreakerCharge >= MinimumCrashbreakerCharge;
        public bool TargetDown => targetDown;
        public bool IsReadyToLaunch => Phase == GamePhase.ReadyCheck;
        /// <summary>0 to 1 and back, sweeping while the player waits to launch.</summary>
        public float LaunchMeter => Mathf.PingPong((Time.unscaledTime - readyStartedAt) / LaunchSweepSeconds, 1f);
        public float LastLaunchQuality { get; private set; }
        public uint TargetEntityId => targetEntityId;
        public CrashScenario Scenario => scenario;
        public RunwayProfile Runway => runway;
        /// <summary>Live estimate of seconds until the player reaches the junction centre.</summary>
        public float JunctionEta => junctionEta;
        /// <summary>True while traffic is still being timed to meet the player.</summary>
        public bool ApproachLocked => approachLocked;
        public float DistanceToJunction => player == null ? 0f : Mathf.Max(0f, ApproachPlanner.JunctionZ - player.transform.position.z);
        public CrashMedal Medal => scenario == null ? CrashMedal.None : scenario.MedalFor(Score);
        public float CrashElapsed => Phase == GamePhase.Crash || Phase == GamePhase.SettleDelay
            ? Time.time - crashStartedAt
            : 0f;
        public float TimeSinceLastScore => Time.time - lastScoringImpactAt;
        public ArcadeVehicleController Player => player;
        public LocalDriverInput LocalInput => localInput;
        public TurnReplay Replay { get; private set; }
        public IReadOnlyList<DamageableVehicle> Vehicles => vehicles;

        public event Action<ScoreEvent> ScoreChanged;
        public event Action<GamePhase> PhaseChanged;
        /// <summary>Label, amount and world position of a bonus such as the target or a blast.</summary>
        public event Action<string, int, Vector3> BonusAwarded;
        /// <summary>Centre, radius and whether the player's Crashbreaker caused the blast.</summary>
        public event Action<Vector3, float, bool> Exploded;
        public event Action<int> ScenarioChangeRequested;

        public void Configure(
            ArcadeVehicleController playerController,
            CrashoutCameraRig rig,
            IEnumerable<DamageableVehicle> damageableVehicles)
        {
            player = playerController;
            playerVehicle = player.GetComponent<DamageableVehicle>();
            cameraRig = rig;
            SetVehicles(damageableVehicles);
            Replay = gameObject.AddComponent<TurnReplay>();
            Replay.Configure(this);
            stateMachine.PhaseChanged += HandlePhaseChanged;
            readyStartedAt = Time.unscaledTime;
            stateMachine.StartReady();
        }

        /// <summary>Installs a freshly built set of vehicles for a scenario and restarts the turn.</summary>
        public void LoadScenario(CrashScenario newScenario, IEnumerable<DamageableVehicle> damageableVehicles, uint targetId)
        {
            scenario = newScenario;
            runway = newScenario?.Runway;
            targetEntityId = targetId;
            SetVehicles(damageableVehicles);
            ResetTurn();
        }

        public void RequestScenarioChange(int delta)
        {
            Replay?.Clear();
            ScenarioChangeRequested?.Invoke(delta);
        }

        private void SetVehicles(IEnumerable<DamageableVehicle> damageableVehicles)
        {
            vehicles.Clear();
            vehicles.AddRange(damageableVehicles);
            trafficVehicles.Clear();
            resettableBodies.Clear();
            foreach (DamageableVehicle vehicle in vehicles)
            {
                TrafficVehicle traffic = vehicle.GetComponent<TrafficVehicle>();
                if (traffic != null) trafficVehicles.Add(traffic);
                ResettableBody resettable = vehicle.GetComponent<ResettableBody>();
                if (resettable != null) resettableBodies.Add(resettable);
            }
        }

        private void OnDestroy()
        {
            stateMachine.PhaseChanged -= HandlePhaseChanged;
            RestoreTimeScale();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && Replay != null && Replay.IsActive)
            {
                if (keyboard.spaceKey.wasPressedThisFrame) Replay.TogglePlayback();
                if (keyboard.escapeKey.wasPressedThisFrame) Replay.Close();
                if (keyboard.leftArrowKey.wasPressedThisFrame) Replay.Seek(Replay.Position - 1f);
                if (keyboard.rightArrowKey.wasPressedThisFrame) Replay.Seek(Replay.Position + 1f);
                if (keyboard.cKey.wasPressedThisFrame && cameraRig != null) cameraRig.CycleMode();
                if (keyboard.rKey.wasPressedThisFrame) ResetTurn();
                if (keyboard.nKey.wasPressedThisFrame) RequestScenarioChange(1);
                return;
            }
            if (keyboard != null && keyboard.cKey.wasPressedThisFrame && cameraRig != null)
            {
                cameraRig.CycleMode();
            }
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                ResetTurn();
            }
            if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.fKey.wasPressedThisFrame))
            {
                if (Phase == GamePhase.Results) ResetTurn();
                else if (!TryLaunch()) TryCrashbreaker();
            }
            if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
            {
                if (Phase == GamePhase.Results) ResetTurn();
                else TryLaunch();
            }
            if (keyboard != null && (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame))
            {
                TryLaunch();
            }
            if (keyboard != null && keyboard.nKey.wasPressedThisFrame)
            {
                RequestScenarioChange(1);
            }
            UpdateImpactTime();
        }

        private void UpdateImpactTime()
        {
            // Another system (AR placement) may have paused time; never fight it.
            if (Time.timeScale <= 0.01f && !timeScaleAltered) return;
            if (IsImpactTime)
            {
                Time.timeScale = ImpactTimeScale;
                timeScaleAltered = true;
            }
            else if (timeScaleAltered)
            {
                RestoreTimeScale();
            }
        }

        private void RestoreTimeScale()
        {
            if (!timeScaleAltered) return;
            timeScaleAltered = false;
            Time.timeScale = 1f;
        }

        private void FixedUpdate()
        {
            if (!IsAuthority || player == null || (Replay != null && Replay.IsActive)) return;
            stateMachine.Tick();

            if (Phase == GamePhase.Driving)
            {
                DriverInputCommand command = localInput.Sample(HostTick, turnId);
                rawThrottle = command.Throttle;
                rawBoost = command.Boost;
                // The car accelerates itself once launched; the player steers, brakes and boosts.
                float forwardSpeed = Vector3.Dot(player.Body.linearVelocity, player.transform.forward);
                if (command.Brake < 0.1f && (forwardSpeed < CruiseSpeed || command.Boost)) command.Throttle = 1f;
                player.StepAuthority(command);
                UpdateApproach();
                CheckForStall();
            }
            else
            {
                approachLocked = false;
                foreach (TrafficVehicle traffic in trafficVehicles) traffic.RelaxPhase(Time.fixedDeltaTime);
                if (Phase == GamePhase.Crash || Phase == GamePhase.SettleDelay) ApplyAftertouch();
                else AftertouchInput = Vector2.zero;
            }

            if (Phase == GamePhase.Driving || Phase == GamePhase.Crash || Phase == GamePhase.SettleDelay)
            {
                foreach (TrafficVehicle traffic in trafficVehicles)
                {
                    traffic.StepAuthority();
                }
            }

            if (Phase == GamePhase.Driving || Phase == GamePhase.Crash || Phase == GamePhase.SettleDelay)
            {
                ContainOnBoard();
            }

            if (Phase == GamePhase.Crash || Phase == GamePhase.SettleDelay)
            {
                EvaluateTurnResolution();
            }
        }

        /// <summary>
        /// Nothing leaves the board through a portal. Wrecks that slide into the dark at the far end
        /// of a tunnel are turned back, and a player who drives clean through the junction ends the
        /// run there instead of dropping off the edge of the world. Looping traffic is untouched.
        /// </summary>
        private void ContainOnBoard()
        {
            const float limit = RunwayProfile.BoardHalfExtent - 1.6f;
            Rigidbody body = player.Body;
            if (Phase == GamePhase.Driving && (body.position.z > limit || Mathf.Abs(body.position.x) > limit) && RunwayProfile.IsOnBoard(new Vector3(0f, 0f, body.position.z)))
            {
                BeginCrash();
            }
            if (Phase != GamePhase.Driving) Contain(body, limit);
            foreach (TrafficVehicle traffic in trafficVehicles)
            {
                if (traffic != null && !traffic.DriveEnabled) Contain(traffic.Body, limit);
            }
        }

        private static void Contain(Rigidbody body, float limit)
        {
            Vector3 position = body.position;
            Vector3 velocity = body.linearVelocity;
            bool turned = false;
            if (position.x > limit && velocity.x > 0f) { velocity.x *= -0.3f; turned = true; }
            else if (position.x < -limit && velocity.x < 0f) { velocity.x *= -0.3f; turned = true; }
            if (position.z > limit && velocity.z > 0f) { velocity.z *= -0.3f; turned = true; }
            if (!turned) return;
            body.linearVelocity = velocity;
            body.angularVelocity *= 0.6f;
        }

        /// <summary>
        /// After impact the wreck is a passive body that the player can still steer, relative to the
        /// camera, for a budget of time. The frame of reference is frozen while the stick is held (the
        /// camera stops orbiting too), the push is stronger in slow motion so it is felt when it
        /// matters, and the wreck turns to face where it is being sent.
        /// </summary>
        private void ApplyAftertouch()
        {
            if (player == null) return;
            DriverInputCommand command = localInput.Sample(HostTick, turnId);
            Vector2 wish2 = localInput.AftertouchVector(command);
            float input = wish2.magnitude;
            if (aftertouchRemaining <= 0f || input < 0.12f)
            {
                AftertouchInput = Vector2.zero;
                aftertouchFrameHeld = false;
                return;
            }

            if (!aftertouchFrameHeld)
            {
                Transform view = Camera.main != null ? Camera.main.transform : transform;
                aftertouchRight = Vector3.ProjectOnPlane(view.right, Vector3.up).normalized;
                aftertouchForward = Vector3.ProjectOnPlane(view.forward, Vector3.up);
                if (aftertouchForward.sqrMagnitude < 0.01f) aftertouchForward = Vector3.ProjectOnPlane(view.up, Vector3.up);
                aftertouchForward.Normalize();
                aftertouchFrameHeld = true;
            }
            AftertouchInput = wish2;
            aftertouchRemaining -= Time.fixedDeltaTime * input;

            Rigidbody body = player.Body;
            Vector3 wish = aftertouchRight * wish2.x + aftertouchForward * wish2.y;
            // Slow motion shrinks every force's effect per real second; give some of it back.
            float slowBoost = IsImpactTime ? 1.9f : 1f;
            // A sliding wreck resists less than a tumbling one, and a stopped one needs a real shove.
            float speed = body.linearVelocity.magnitude;
            float shove = Mathf.Lerp(30f, 21f, Mathf.Clamp01(speed / 12f));
            body.AddForce(wish * (shove * slowBoost), ForceMode.Acceleration);
            // Bend the existing motion toward the wish without bleeding its speed.
            Vector3 flat = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
            if (flat.sqrMagnitude > 9f)
            {
                Vector3 bent = Vector3.RotateTowards(flat, wish * flat.magnitude, 1.1f * slowBoost * Time.fixedDeltaTime * input, 0f);
                body.linearVelocity = new Vector3(bent.x, body.linearVelocity.y, bent.z);
            }
            // Airborne wrecks stay up a touch longer while steered, so a flying car can be placed.
            if (!Physics.Raycast(body.worldCenterOfMass, Vector3.down, 1.1f, 1 << 0, QueryTriggerInteraction.Ignore))
            {
                body.AddForce(Vector3.up * (3.2f * input), ForceMode.Acceleration);
            }
            body.AddTorque(Vector3.Cross(Vector3.up, wish) * (1.6f * slowBoost), ForceMode.Acceleration);
        }

        public static float LaunchQualityAt(float meter)
        {
            if (meter >= LaunchZoneMin && meter <= LaunchZoneMax) return 1f;
            if (meter < LaunchZoneMin) return Mathf.Lerp(0.25f, 0.8f, meter / LaunchZoneMin);
            return Mathf.Lerp(0.8f, 0.3f, (meter - LaunchZoneMax) / (1f - LaunchZoneMax));
        }

        /// <summary>Launches using the meter's current position.</summary>
        public bool TryLaunch()
        {
            if (Phase != GamePhase.ReadyCheck) return false;
            Launch(LaunchQualityAt(LaunchMeter));
            return true;
        }

        /// <summary>
        /// Starts the run. Traffic is released on this moment, so the junction is always timed
        /// from the launch no matter when the player chooses to start.
        /// </summary>
        public void Launch(float quality)
        {
            if (Phase != GamePhase.ReadyCheck || player == null) return;
            quality = Mathf.Clamp01(quality);
            LastLaunchQuality = quality;
            stateMachine.TryAdvance(GamePhase.Countdown);
            stateMachine.TryAdvance(GamePhase.Driving);

            foreach (TrafficVehicle traffic in trafficVehicles)
            {
                traffic.Launch();
            }
            launchedAt = Time.time;
            stalledSeconds = 0f;
            flippedSeconds = 0f;
            approachLocked = true;

            player.Body.linearVelocity = player.transform.forward * Mathf.Lerp(1f, 5f, quality);
            player.Body.WakeUp();
            Vector3 label = player.transform.position + Vector3.up * 1.5f;
            if (quality >= 0.99f)
            {
                player.GrantLaunchBoost(1.3f);
                Award("PERFECT LAUNCH", 2500, label);
            }
            else
            {
                BonusAwarded?.Invoke(quality >= 0.7f ? "GOOD LAUNCH" : quality >= 0.45f ? "SLOW START" : "BAD START", 0, label);
            }
        }

        public bool TryCrashbreaker()
        {
            if (!CanCrashbreak || player == null) return false;
            float charge = crashbreakerCharge;
            crashbreakerCharge = 0f;
            crashbreakersUsed++;

            Vector3 centre = player.Body.worldCenterOfMass;
            float radius = Mathf.Lerp(6f, 17f, charge);
            float strength = Mathf.Lerp(0.55f, 1.45f, charge);
            int hit = Detonate(centre, radius, strength, playerVehicle, true);
            if (hit >= 3)
            {
                Award("BLAST x" + hit, hit * 1500, centre);
            }

            // The player's own car is thrown by its own detonation.
            player.Body.AddExplosionForce(player.Body.mass * 9f, centre + Vector3.down * 0.4f, 3f, 1.2f, ForceMode.Impulse);
            aftertouchRemaining = Mathf.Max(aftertouchRemaining, AftertouchSeconds * 0.4f);
            lastScoringImpactAt = Time.time;
            return true;
        }

        private int Detonate(Vector3 centre, float radius, float strength, DamageableVehicle source, bool byPlayer)
        {
            if (source != null) detonated.Add(source.EntityId);
            Exploded?.Invoke(centre, radius, byPlayer);

            int hit = 0;
            foreach (DamageableVehicle vehicle in vehicles)
            {
                if (vehicle == null || vehicle == source) continue;
                Rigidbody body = vehicle.Body;
                float distance = Vector3.Distance(body.worldCenterOfMass, centre);
                if (distance > radius) continue;

                float falloff = 1f - distance / radius;
                body.AddExplosionForce(body.mass * strength * 15f, centre, radius, 0.9f, ForceMode.Impulse);
                body.AddTorque(UnityEngine.Random.onUnitSphere * body.mass * strength * 3f * falloff, ForceMode.Impulse);
                if (vehicle.IsPlayerVehicle) continue;

                int amount = Mathf.RoundToInt(Mathf.Lerp(900f, 3800f, falloff) * (1f + strength));
                AwardDamage(vehicle, amount, strength * 20f * falloff, 10f * falloff);
                hit++;
            }
            return hit;
        }

        public void ReportVehicleCollision(DamageableVehicle vehicle, uint otherEntityId, float impulse, float relativeSpeed, Vector3 contactNormal = default)
        {
            if (!IsAuthority || Phase == GamePhase.Results || vehicle == null) return;

            DamageableVehicle other = FindVehicle(otherEntityId);
            bool playerParticipated = vehicle.IsPlayerVehicle || other?.IsPlayerVehicle == true;
            bool significant = impulse >= 1.8f && relativeSpeed >= 1.4f;
            // Scenery only ends the run on the board, and never for touching down on the road
            // (a hard landing off a ramp or a sag). Out on the runway the barriers just scrape.
            bool crashes = other != null
                || (RunwayProfile.IsOnBoard(vehicle.transform.position) && Mathf.Abs(contactNormal.y) < 0.6f);
            if (Phase == GamePhase.Driving && playerParticipated && significant && crashes)
            {
                BeginCrash();
            }
            if (Phase != GamePhase.Crash && Phase != GamePhase.SettleDelay) return;

            DamageableVehicle scoreTarget = vehicle.IsPlayerVehicle && other != null ? other : vehicle;
            uint counterpartId = scoreTarget == vehicle ? otherEntityId : vehicle.EntityId;
            bool firstWreck = !scoreLedger.HasScored(scoreTarget.EntityId);
            if (scoreLedger.TryScore(HostTick, scoreTarget.EntityId, counterpartId, impulse, relativeSpeed, scoreTarget.ScoreCap, out ScoreEvent scoreEvent))
            {
                OnScored(scoreTarget, scoreEvent, firstWreck);
                CheckForTankerBlast(scoreTarget, impulse);
            }
        }

        private void AwardDamage(DamageableVehicle vehicle, int amount, float impulse, float relativeSpeed)
        {
            bool firstWreck = !scoreLedger.HasScored(vehicle.EntityId);
            if (scoreLedger.TryAward(HostTick, vehicle.EntityId, amount, vehicle.ScoreCap, impulse, relativeSpeed, out ScoreEvent scoreEvent))
            {
                OnScored(vehicle, scoreEvent, firstWreck);
            }
        }

        private void OnScored(DamageableVehicle scoreTarget, ScoreEvent scoreEvent, bool firstWreck)
        {
            lastScoringImpactAt = Time.time;
            bestChain = Mathf.Max(bestChain, scoreEvent.ChainCount);
            crashbreakerCharge = Mathf.Min(1f, crashbreakerCharge + scoreEvent.Amount / 160000f
                + (firstWreck ? VehicleSpec.For(scoreTarget.Class).CrashbreakerCharge : 0f));
            ScoreChanged?.Invoke(scoreEvent);
            scoreTarget.GetComponent<TrafficVehicle>()?.DisableDrive();
            // Every new wreck buys a little more steering time, so a growing pile-up stays playable.
            if (firstWreck) aftertouchRemaining = Mathf.Min(AftertouchSeconds, aftertouchRemaining + AftertouchSeconds * 0.12f);

            if (scenario != null && !targetDown && scoreTarget.EntityId == targetEntityId)
            {
                targetDown = true;
                Award("TARGET DOWN", scenario.TargetBonus, scoreTarget.transform.position + Vector3.up * 1.5f);
                crashbreakerCharge = Mathf.Min(1f, crashbreakerCharge + 0.25f);
            }
        }

        private void CheckForTankerBlast(DamageableVehicle vehicle, float impulse)
        {
            if (!VehicleSpec.For(vehicle.Class).Explosive || detonated.Contains(vehicle.EntityId)) return;
            if (impulse < 14f && vehicle.Damage < 12f) return;
            Vector3 centre = vehicle.Body.worldCenterOfMass;
            int hit = Detonate(centre, 10f, 1.15f, vehicle, false);
            Award("FUEL BLAST", 12000 + hit * 2000, centre + Vector3.up * 2f);
            crashbreakerCharge = Mathf.Min(1f, crashbreakerCharge + 0.3f);
        }

        private void Award(string label, int amount, Vector3 position)
        {
            scoreLedger.AddBonus(amount);
            lastScoringImpactAt = Time.time;
            BonusAwarded?.Invoke(label, amount, position);
            ScoreChanged?.Invoke(new ScoreEvent { HostTick = HostTick, EntityId = 0, Amount = 0, Total = Score, ChainCount = ChainCount });
        }

        public bool TryGetVehiclePosition(uint entityId, out Vector3 position)
        {
            DamageableVehicle vehicle = FindVehicle(entityId);
            position = vehicle != null ? vehicle.transform.position : Vector3.zero;
            return vehicle != null;
        }

        public void ResetTurn()
        {
            Replay?.Clear();
            foreach (ResettableBody resettable in resettableBodies)
            {
                resettable.ResetBody();
            }
            foreach (DamageableVehicle damageable in vehicles)
            {
                damageable.ResetDamage();
            }
            foreach (TrafficVehicle traffic in trafficVehicles)
            {
                traffic.ResetController();
            }

            scoreLedger.Reset();
            detonated.Clear();
            player.ResetController();
            RestoreTimeScale();
            crashbreakerCharge = 0f;
            crashbreakersUsed = 0;
            aftertouchRemaining = 0f;
            AftertouchInput = Vector2.zero;
            aftertouchFrameHeld = false;
            bestChain = 0;
            targetDown = false;
            impactTimeUntil = 0f;
            turnId++;
            lastScoringImpactAt = Time.time;
            readyStartedAt = Time.unscaledTime;
            stalledSeconds = 0f;
            flippedSeconds = 0f;
            approachLocked = false;
            PlaceTrafficForApproach();
            stateMachine.ResetToReady();
            ScoreChanged?.Invoke(default);
        }

        /// <summary>
        /// Parks traffic so that, if the player launches perfectly and lets the car drive itself,
        /// every car reaches its designed slot at the moment the player reaches the junction.
        /// </summary>
        private void PlaceTrafficForApproach()
        {
            if (player == null) return;
            junctionEta = ApproachPlanner.SecondsToJunction(runway, player.Body.position.z, NominalLaunchSpeed, false, PerfectLaunchBoostSeconds);
            foreach (TrafficVehicle traffic in trafficVehicles)
            {
                traffic.PlaceOnPhase(traffic.LaneSpeed * (DesignArrivalSeconds - junctionEta));
            }
        }

        /// <summary>
        /// While the player is out on the runway, re-estimate their arrival from where they are, how
        /// fast they are going and what they are holding, and nudge traffic to keep the convergence
        /// on time. Hands traffic back to free driving just before the player reaches the board.
        /// </summary>
        private void UpdateApproach()
        {
            Rigidbody body = player.Body;
            float along = Mathf.Max(0f, body.linearVelocity.z);
            float boostSeconds = player.LaunchBoostRemaining + (rawBoost ? player.BoostFraction * ArcadeVehicleController.BoostSeconds : 0f);
            junctionEta = ApproachPlanner.SecondsToJunction(runway, body.position.z, along, rawThrottle > 0.1f, boostSeconds);

            if (approachLocked && (body.position.z > RunwayProfile.BoardEdgeZ - 4f || junctionEta < 1.2f))
            {
                approachLocked = false;
            }
            foreach (TrafficVehicle traffic in trafficVehicles)
            {
                if (approachLocked) traffic.SteerToPhase(traffic.LaneSpeed * (DesignArrivalSeconds - junctionEta), Time.fixedDeltaTime);
                else traffic.RelaxPhase(Time.fixedDeltaTime);
            }
        }

        /// <summary>A car that is stuck, upside down or has fallen off the world ends the run.</summary>
        private void CheckForStall()
        {
            float dt = Time.fixedDeltaTime;
            bool moving = player.SpeedMetresPerSecond > 0.7f;
            stalledSeconds = Time.time - launchedAt > 2f && !moving ? stalledSeconds + dt : 0f;
            flippedSeconds = player.transform.up.y < 0.3f ? flippedSeconds + dt : 0f;
            if (stalledSeconds > 2.5f || flippedSeconds > 1.5f || player.Body.position.y < -25f)
            {
                BeginCrash();
            }
        }

        public IReadOnlyList<EntitySnapshot> CaptureSnapshot()
        {
            EntitySnapshot[] snapshot = new EntitySnapshot[vehicles.Count];
            for (int i = 0; i < vehicles.Count; i++)
            {
                snapshot[i] = vehicles[i].Capture(HostTick);
            }
            return snapshot;
        }

        private void BeginCrash()
        {
            if (!stateMachine.TryAdvance(GamePhase.Crash)) return;
            crashStartedAt = Time.time;
            lastScoringImpactAt = Time.time;
            impactTimeUntil = Time.unscaledTime + ImpactTimeSeconds;
            aftertouchRemaining = AftertouchSeconds;
            // Boost carried into the crash is stored as Crashbreaker power.
            crashbreakerCharge = Mathf.Max(crashbreakerCharge, 0.15f + 0.35f * player.BoostFraction);
            player.SetPropulsionEnabled(false);
        }

        private void EvaluateTurnResolution()
        {
            float elapsed = Time.time - crashStartedAt;
            int movingBodies = 0;
            foreach (DamageableVehicle vehicle in vehicles)
            {
                Rigidbody body = vehicle.GetComponent<Rigidbody>();
                if (body.linearVelocity.sqrMagnitude > 0.36f || body.angularVelocity.sqrMagnitude > 0.2f)
                {
                    movingBodies++;
                }
            }
            bool mostBodiesQuiet = movingBodies <= Mathf.Max(1, vehicles.Count / 5);

            if (Phase == GamePhase.Crash && elapsed >= 4.5f && TimeSinceLastScore >= 1.4f)
            {
                stateMachine.TryAdvance(GamePhase.SettleDelay);
            }
            if (Phase == GamePhase.SettleDelay && ((TimeSinceLastScore >= SettleQuietSeconds && mostBodiesQuiet) || elapsed >= CrashSafetyTimeout))
            {
                stateMachine.TryAdvance(GamePhase.Results);
            }
        }

        private DamageableVehicle FindVehicle(uint entityId)
        {
            for (int i = 0; i < vehicles.Count; i++)
            {
                if (vehicles[i].EntityId == entityId) return vehicles[i];
            }
            return null;
        }

        private void HandlePhaseChanged(GamePhase phase, uint tick, uint sequence)
        {
            if (phase == GamePhase.Results) RestoreTimeScale();
            PhaseChanged?.Invoke(phase);
        }
    }
}

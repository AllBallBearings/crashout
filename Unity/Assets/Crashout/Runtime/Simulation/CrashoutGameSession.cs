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
        private uint turnId = 1;

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
        public bool IsImpactTime => Phase == GamePhase.Crash && Time.unscaledTime < impactTimeUntil;
        public bool CanCrashbreak => (Phase == GamePhase.Crash || Phase == GamePhase.SettleDelay) && crashbreakerCharge >= MinimumCrashbreakerCharge;
        public bool TargetDown => targetDown;
        public uint TargetEntityId => targetEntityId;
        public CrashScenario Scenario => scenario;
        public CrashMedal Medal => scenario == null ? CrashMedal.None : scenario.MedalFor(Score);
        public float CrashElapsed => Phase == GamePhase.Crash || Phase == GamePhase.SettleDelay
            ? Time.time - crashStartedAt
            : 0f;
        public float TimeSinceLastScore => Time.time - lastScoringImpactAt;
        public ArcadeVehicleController Player => player;
        public LocalDriverInput LocalInput => localInput;
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
            stateMachine.PhaseChanged += HandlePhaseChanged;
            stateMachine.StartPrototypeTurn();
        }

        /// <summary>Installs a freshly built set of vehicles for a scenario and restarts the turn.</summary>
        public void LoadScenario(CrashScenario newScenario, IEnumerable<DamageableVehicle> damageableVehicles, uint targetId)
        {
            scenario = newScenario;
            targetEntityId = targetId;
            SetVehicles(damageableVehicles);
            ResetTurn();
        }

        public void RequestScenarioChange(int delta)
        {
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
            if (keyboard != null && keyboard.cKey.wasPressedThisFrame)
            {
                cameraRig.CycleMode();
            }
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                ResetTurn();
            }
            if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.fKey.wasPressedThisFrame))
            {
                TryCrashbreaker();
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
            if (!IsAuthority || player == null) return;
            stateMachine.Tick();

            if (Phase == GamePhase.Driving)
            {
                player.StepAuthority(localInput.Sample(HostTick, turnId));
            }
            else if (Phase == GamePhase.Crash || Phase == GamePhase.SettleDelay)
            {
                ApplyAftertouch();
            }

            if (Phase == GamePhase.Driving || Phase == GamePhase.Crash || Phase == GamePhase.SettleDelay)
            {
                foreach (TrafficVehicle traffic in trafficVehicles)
                {
                    traffic.StepAuthority();
                }
            }

            if (Phase == GamePhase.Crash || Phase == GamePhase.SettleDelay)
            {
                EvaluateTurnResolution();
            }
        }

        /// <summary>
        /// After impact the wreck is a passive body that the player can still nudge,
        /// relative to the camera, for a short budget of time.
        /// </summary>
        private void ApplyAftertouch()
        {
            if (aftertouchRemaining <= 0f || player == null) return;
            DriverInputCommand command = localInput.Sample(HostTick, turnId);
            float steer = command.Steering;
            float push = command.Throttle - command.Brake;
            float input = Mathf.Clamp01(Mathf.Sqrt(steer * steer + push * push));
            aftertouchRemaining -= Time.fixedDeltaTime * (input > 0.1f ? 1f : 0.2f);
            if (input < 0.05f) return;

            Transform view = Camera.main != null ? Camera.main.transform : transform;
            Vector3 right = Vector3.ProjectOnPlane(view.right, Vector3.up).normalized;
            Vector3 forward = Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized;
            Rigidbody body = player.Body;
            Vector3 wish = right * steer + forward * push;
            body.AddForce(wish * body.mass * 22f, ForceMode.Force);
            body.AddTorque(Vector3.Cross(Vector3.up, wish) * body.mass * 1.5f, ForceMode.Force);
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

        public void ReportVehicleCollision(DamageableVehicle vehicle, uint otherEntityId, float impulse, float relativeSpeed)
        {
            if (!IsAuthority || Phase == GamePhase.Results || vehicle == null) return;

            DamageableVehicle other = FindVehicle(otherEntityId);
            bool playerParticipated = vehicle.IsPlayerVehicle || other?.IsPlayerVehicle == true;
            bool significant = impulse >= 1.8f && relativeSpeed >= 1.4f;
            if (Phase == GamePhase.Driving && playerParticipated && significant)
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
            bestChain = 0;
            targetDown = false;
            impactTimeUntil = 0f;
            turnId++;
            lastScoringImpactAt = Time.time;
            stateMachine.ResetToDriving();
            ScoreChanged?.Invoke(default);
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

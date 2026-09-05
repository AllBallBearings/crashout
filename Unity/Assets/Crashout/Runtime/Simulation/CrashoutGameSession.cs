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
        private const float CrashSafetyTimeout = 20f;

        private readonly SessionStateMachine stateMachine = new SessionStateMachine();
        private readonly ScoreLedger scoreLedger = new ScoreLedger();
        private readonly LocalDriverInput localInput = new LocalDriverInput();
        private readonly List<DamageableVehicle> vehicles = new List<DamageableVehicle>();
        private readonly List<TrafficVehicle> trafficVehicles = new List<TrafficVehicle>();
        private readonly List<ResettableBody> resettableBodies = new List<ResettableBody>();

        private ArcadeVehicleController player;
        private CrashoutCameraRig cameraRig;
        private float crashStartedAt;
        private float lastScoringImpactAt;
        private uint turnId = 1;

        public bool IsAuthority { get; private set; } = true;
        public uint HostTick => stateMachine.HostTick;
        public GamePhase Phase => stateMachine.Phase;
        public int Score => scoreLedger.Total;
        public int ChainCount => scoreLedger.ChainCount;
        public float CrashElapsed => Phase == GamePhase.Crash || Phase == GamePhase.SettleDelay
            ? Time.time - crashStartedAt
            : 0f;
        public float TimeSinceLastScore => Time.time - lastScoringImpactAt;
        public ArcadeVehicleController Player => player;
        public LocalDriverInput LocalInput => localInput;

        public event Action<ScoreEvent> ScoreChanged;
        public event Action<GamePhase> PhaseChanged;

        public void Configure(
            ArcadeVehicleController playerController,
            CrashoutCameraRig rig,
            IEnumerable<DamageableVehicle> damageableVehicles)
        {
            player = playerController;
            cameraRig = rig;
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
            stateMachine.PhaseChanged += HandlePhaseChanged;
            stateMachine.StartPrototypeTurn();
        }

        private void OnDestroy()
        {
            stateMachine.PhaseChanged -= HandlePhaseChanged;
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
        }

        private void FixedUpdate()
        {
            if (!IsAuthority || player == null) return;
            stateMachine.Tick();

            if (Phase == GamePhase.Driving || Phase == GamePhase.Crash)
            {
                DriverInputCommand command = localInput.Sample(HostTick, turnId);
                if (Phase == GamePhase.Crash)
                {
                    command.Throttle *= 0.2f;
                    command.Steering *= 0.35f;
                }
                player.StepAuthority(command);

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
            if (scoreLedger.TryScore(HostTick, scoreTarget.EntityId, counterpartId, impulse, relativeSpeed, scoreTarget.ScoreCap, out ScoreEvent scoreEvent))
            {
                lastScoringImpactAt = Time.time;
                ScoreChanged?.Invoke(scoreEvent);
                scoreTarget.GetComponent<TrafficVehicle>()?.DisableDrive();
            }
            if (vehicle.IsPlayerVehicle && vehicle.Damage > 28f)
            {
                player.SetPropulsionEnabled(false);
            }
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
            player.ResetController();
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
        }

        private void EvaluateTurnResolution()
        {
            float elapsed = Time.time - crashStartedAt;
            bool mostBodiesQuiet = true;
            int movingBodies = 0;
            foreach (DamageableVehicle vehicle in vehicles)
            {
                Rigidbody body = vehicle.GetComponent<Rigidbody>();
                if (body.linearVelocity.sqrMagnitude > 0.36f || body.angularVelocity.sqrMagnitude > 0.2f)
                {
                    movingBodies++;
                }
            }
            mostBodiesQuiet = movingBodies <= Mathf.Max(1, vehicles.Count / 5);

            if (Phase == GamePhase.Crash && elapsed >= 4f && TimeSinceLastScore >= 1.2f)
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
            PhaseChanged?.Invoke(phase);
        }
    }
}

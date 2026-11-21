using Crashout.Simulation;
using UnityEngine;

namespace Crashout.Vehicle
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class DamageableVehicle : MonoBehaviour
    {
        [SerializeField] private uint entityId;
        [SerializeField] private bool playerVehicle;
        [SerializeField] private int scoreCap = 18000;

        private CrashoutGameSession session;
        private Rigidbody body;
        private float damage;
        private VehicleClass vehicleClass;

        public uint EntityId => entityId;
        public bool IsPlayerVehicle => playerVehicle;
        public int ScoreCap => scoreCap;
        public float Damage => damage;
        public VehicleClass Class => vehicleClass;
        public Vector3 LastImpactPoint { get; private set; }
        public Vector3 LastImpactNormal { get; private set; } = Vector3.up;
        public Rigidbody Body => body != null ? body : (body = GetComponent<Rigidbody>());

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
        }

        public void Configure(uint id, bool isPlayer, CrashoutGameSession authority, int maximumScore = 18000, VehicleClass classification = VehicleClass.Sedan)
        {
            entityId = id;
            playerVehicle = isPlayer;
            session = authority;
            scoreCap = maximumScore;
            vehicleClass = classification;
        }

        public void ResetDamage()
        {
            damage = 0f;
            GetComponent<Crashout.Presentation.VehicleDeformer>()?.RestoreVehicle();
        }

        public void ReportFragmentCollision(Collision collision) => OnCollisionEnter(collision);
        public void ReportStructuralFailure() => session?.ReportStructuralFailure(this);

        public EntitySnapshot Capture(uint hostTick)
        {
            return new EntitySnapshot
            {
                HostTick = hostTick,
                EntityId = entityId,
                Position = body.position,
                Rotation = body.rotation,
                LinearVelocity = body.linearVelocity,
                AngularVelocity = body.angularVelocity,
                Damage = damage,
                PropulsionEnabled = GetComponent<TrafficVehicle>()?.DriveEnabled
                    ?? GetComponent<ArcadeVehicleController>()?.PropulsionEnabled
                    ?? false,
                Sleeping = body.IsSleeping()
            };
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (session == null || !session.IsAuthority) return;
            VehicleImpact impact = VehicleImpact.Read(collision);
            if (impact.IsRoutineRoadContact) return;
            LastImpactPoint = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            LastImpactNormal = impact.Normal;
            DamageableVehicle other = collision.collider.GetComponentInParent<DamageableVehicle>();
            uint otherId = other == null ? 0u : other.EntityId;
            float impulse = collision.impulse.magnitude / 1000f;
            float relativeSpeed = impact.Speed;
            float transfer = Mathf.Clamp01(collision.impulse.magnitude / Mathf.Max(1f, Body.mass * relativeSpeed * 0.15f));
            damage += impulse * 0.04f + relativeSpeed * 0.6f * transfer;
            session.ReportVehicleCollision(this, otherId, impulse, relativeSpeed, impact.Normal);
        }
    }
}

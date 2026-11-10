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
        }

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
            DamageableVehicle other = collision.collider.GetComponentInParent<DamageableVehicle>();
            uint otherId = other == null ? 0u : other.EntityId;
            float impulse = collision.impulse.magnitude / 1000f;
            float relativeSpeed = collision.relativeVelocity.magnitude;
            damage += impulse * 0.04f + relativeSpeed * 0.6f;
            Vector3 normal = collision.contactCount > 0 ? collision.GetContact(0).normal : Vector3.zero;
            session.ReportVehicleCollision(this, otherId, impulse, relativeSpeed, normal);
        }
    }
}

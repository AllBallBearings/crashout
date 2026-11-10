using Crashout.Vehicle;
using UnityEngine;

namespace Crashout.Presentation
{
    /// <summary>
    /// Per-vehicle presentation state: brake lights that flare under deceleration, hazard lights that
    /// blink once a car is wrecked, lamps that die when smashed and glass that crazes on hard hits.
    /// Reads the rigidbody and controllers; never writes to the simulation.
    /// </summary>
    public sealed class VehicleLook : MonoBehaviour
    {
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        private Material paint;
        private Material taillight;
        private Material indicator;
        private Renderer[] glassRenderers;
        private Material[] originalGlass;
        private Material shattered;
        private Color tailBase;
        private Rigidbody body;
        private ArcadeVehicleController player;
        private TrafficVehicle traffic;
        private DamageableVehicle damageable;
        private Vector3 lastVelocity;
        private float brakeLevel;
        private bool frontBroken;
        private bool rearBroken;
        private bool glassBroken;

        public Material Paint => paint;
        public float HalfLength { get; private set; }

        public void Configure(Material paintMaterial, Material tail, Material hazard, Renderer[] glass, Material shatteredGlass, float halfLength)
        {
            paint = paintMaterial;
            taillight = tail;
            indicator = hazard;
            glassRenderers = glass;
            shattered = shatteredGlass;
            HalfLength = halfLength;
            tailBase = taillight.GetColor(EmissionColor);
            originalGlass = new Material[glass.Length];
            for (int i = 0; i < glass.Length; i++) originalGlass[i] = glass[i].sharedMaterial;
        }

        private void Start()
        {
            body = GetComponent<Rigidbody>();
            player = GetComponent<ArcadeVehicleController>();
            traffic = GetComponent<TrafficVehicle>();
            damageable = GetComponent<DamageableVehicle>();
        }

        public void BreakLights(bool front)
        {
            if (front) frontBroken = true;
            else rearBroken = true;
        }

        public void ShatterGlass()
        {
            if (glassBroken || shattered == null) return;
            glassBroken = true;
            foreach (Renderer r in glassRenderers) r.sharedMaterial = shattered;
        }

        public void Repair()
        {
            frontBroken = rearBroken = false;
            if (glassBroken)
            {
                for (int i = 0; i < glassRenderers.Length; i++) glassRenderers[i].sharedMaterial = originalGlass[i];
            }
            glassBroken = false;
        }

        private void LateUpdate()
        {
            if (body == null || taillight == null) return;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            Vector3 velocity = body.linearVelocity;
            float forwardAcceleration = Vector3.Dot(velocity - lastVelocity, transform.forward) / dt;
            lastVelocity = velocity;

            bool wrecked = (traffic != null && !traffic.DriveEnabled) || (player != null && !player.PropulsionEnabled);
            bool braking = player != null
                ? player.AppliedBrake > 0.1f
                : traffic != null && traffic.DriveEnabled && forwardAcceleration < -2.5f && velocity.sqrMagnitude > 1f;
            brakeLevel = Mathf.MoveTowards(brakeLevel, braking ? 1f : 0f, dt * 8f);

            Color tail = rearBroken ? Color.black : tailBase * Mathf.Lerp(1f, 4.5f, brakeLevel);
            taillight.SetColor(EmissionColor, tail);

            bool blink = wrecked && Mathf.Repeat(Time.time, 0.9f) < 0.45f;
            indicator.SetColor(EmissionColor, blink ? new Color(6f, 2.6f, 0.2f) : Color.black);

            if (damageable != null && damageable.Damage <= 0.0001f && (frontBroken || rearBroken || glassBroken)) Repair();
        }

        private void OnDestroy()
        {
            if (taillight != null) Destroy(taillight);
            if (indicator != null) Destroy(indicator);
        }
    }
}

using Crashout.Simulation;
using UnityEngine;

namespace Crashout.Vehicle
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ArcadeVehicleController : MonoBehaviour
    {
        private static readonly Vector3[] WheelLocalPositions =
        {
            new Vector3(-0.72f, -0.1f, 1.18f),
            new Vector3(0.72f, -0.1f, 1.18f),
            new Vector3(-0.72f, -0.1f, -1.18f),
            new Vector3(0.72f, -0.1f, -1.18f)
        };

        [SerializeField] private float motorForce = 6200f;
        [SerializeField] private float brakeForce = 7600f;
        [SerializeField] private float steeringTorque = 1350f;
        [SerializeField] private float lateralGrip = 5.8f;
        [SerializeField] private float suspensionRestLength = 0.47f;
        [SerializeField] private float suspensionStrength = 9200f;
        [SerializeField] private float suspensionDamping = 1100f;
        [SerializeField] private float maximumSpeed = 31f;

        private Rigidbody body;
        private Transform[] frontWheelPivots;
        private Transform[] wheelRollPivots;
        private float steeringVisual;
        private bool propulsionEnabled = true;
        private float boostMeter = 1f;
        private float launchBoostUntil;

        /// <summary>Seconds of boost in a full meter.</summary>
        public const float BoostSeconds = 3.2f;
        public const float WheelRadius = 0.31f;

        public Rigidbody Body => body;
        public float SpeedMetresPerSecond => body == null ? 0f : body.linearVelocity.magnitude;
        public bool PropulsionEnabled => propulsionEnabled;
        public float BoostFraction => boostMeter;
        public bool IsBoosting { get; private set; }
        public float LaunchBoostRemaining => Mathf.Max(0f, launchBoostUntil - Time.time);

        // Read-only driving state for sound and effects; never fed back into the simulation.
        public float AppliedThrottle { get; private set; }
        public float AppliedBrake { get; private set; }
        public float SteeringInput { get; private set; }
        public float LateralSlip { get; private set; }
        public int WheelsGrounded { get; private set; }
        public float SuspensionCompression { get; private set; }
        public float TopSpeed => IsBoosting ? maximumSpeed * 1.45f : maximumSpeed;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            ConfigureBody();
        }

        public void SetWheelVisuals(Transform[] frontPivots, Transform[] rollPivots)
        {
            frontWheelPivots = frontPivots;
            wheelRollPivots = rollPivots;
        }

        public void SetPropulsionEnabled(bool enabled)
        {
            propulsionEnabled = enabled;
        }

        /// <summary>A perfect launch gives a free burst of boost that does not drain the meter.</summary>
        public void GrantLaunchBoost(float seconds)
        {
            launchBoostUntil = Time.time + seconds;
        }

        public void ResetController()
        {
            propulsionEnabled = true;
            steeringVisual = 0f;
            boostMeter = 1f;
            IsBoosting = false;
            launchBoostUntil = 0f;
            AppliedThrottle = 0f;
            AppliedBrake = 0f;
            SteeringInput = 0f;
            LateralSlip = 0f;
        }

        public void StepAuthority(DriverInputCommand command)
        {
            if (body == null) return;

            WheelsGrounded = ApplySuspension();
            float forwardSpeed = Vector3.Dot(body.linearVelocity, transform.forward);
            float lateralSpeed = Vector3.Dot(body.linearVelocity, transform.right);
            LateralSlip = lateralSpeed;
            AppliedThrottle = propulsionEnabled ? command.Throttle : 0f;
            AppliedBrake = propulsionEnabled ? command.Brake : 0f;
            SteeringInput = command.Steering;
            // Tyres only grip while they touch the road; in the air the car flies ballistic.
            float grip = WheelsGrounded > 0 ? lateralGrip : lateralGrip * 0.05f;
            body.AddForce(-transform.right * lateralSpeed * grip * body.mass, ForceMode.Force);

            bool launchBoost = propulsionEnabled && Time.time < launchBoostUntil;
            IsBoosting = launchBoost || (propulsionEnabled && command.Boost && command.Throttle > 0f && boostMeter > 0.01f);
            if (IsBoosting && !launchBoost) boostMeter = Mathf.Max(0f, boostMeter - Time.fixedDeltaTime / BoostSeconds);
            float speedLimit = IsBoosting ? maximumSpeed * 1.45f : maximumSpeed;

            if (propulsionEnabled)
            {
                if (command.Throttle > 0f && forwardSpeed < speedLimit && WheelsGrounded > 0)
                {
                    body.AddForce(transform.forward * (command.Throttle * motorForce * (IsBoosting ? 2.1f : 1f)), ForceMode.Force);
                }
                else if (forwardSpeed > speedLimit && WheelsGrounded > 0)
                {
                    // Gearing and drag cap the car near its top speed, even running down a steep hill.
                    body.AddForce(-transform.forward * ((forwardSpeed - speedLimit) * 1.2f * body.mass), ForceMode.Force);
                }

                if (command.Brake > 0f)
                {
                    Vector3 brakingDirection = body.linearVelocity.sqrMagnitude > 0.04f
                        ? -body.linearVelocity.normalized
                        : -transform.forward;
                    body.AddForce(brakingDirection * (command.Brake * brakeForce), ForceMode.Force);
                }

                float steeringAuthority = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / 2.2f) * (WheelsGrounded > 0 ? 1f : 0.15f);
                float speedFalloff = Mathf.Lerp(1f, 0.42f, Mathf.Clamp01(Mathf.Abs(forwardSpeed) / maximumSpeed));
                body.AddTorque(Vector3.up * (command.Steering * steeringTorque * steeringAuthority * speedFalloff), ForceMode.Force);
            }

            body.AddForce(-body.linearVelocity * body.mass * (propulsionEnabled ? 0.06f : 0.75f), ForceMode.Force);
            body.AddTorque(-body.angularVelocity * body.mass * (propulsionEnabled ? 0.08f : 0.42f), ForceMode.Force);
            UpdateWheelVisuals(command.Steering, forwardSpeed);
        }

        private void ConfigureBody()
        {
            body.mass = 1320f;
            body.centerOfMass = new Vector3(0f, -0.28f, 0.08f);
            body.linearDamping = 0.02f;
            body.angularDamping = 0.18f;
            body.maxAngularVelocity = 18f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        private int ApplySuspension()
        {
            const int environmentMask = 1 << 0;
            int grounded = 0;
            float totalCompression = 0f;
            foreach (Vector3 localPosition in WheelLocalPositions)
            {
                Vector3 origin = transform.TransformPoint(localPosition + Vector3.up * 0.28f);
                if (!Physics.Raycast(origin, -transform.up, out RaycastHit hit, suspensionRestLength + 0.34f, environmentMask, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                float compression = Mathf.Clamp01((suspensionRestLength - hit.distance + 0.28f) / suspensionRestLength);
                grounded++;
                totalCompression += compression;
                float pointVelocity = Vector3.Dot(body.GetPointVelocity(origin), transform.up);
                float force = compression * suspensionStrength - pointVelocity * suspensionDamping;
                body.AddForceAtPosition(transform.up * Mathf.Max(0f, force), origin, ForceMode.Force);
            }
            SuspensionCompression = grounded > 0 ? totalCompression / grounded : 0f;
            return grounded;
        }

        private void UpdateWheelVisuals(float steering, float forwardSpeed)
        {
            steeringVisual = Mathf.MoveTowards(steeringVisual, steering * 28f, 160f * Time.fixedDeltaTime);
            if (frontWheelPivots != null)
            {
                foreach (Transform pivot in frontWheelPivots)
                {
                    pivot.localRotation = Quaternion.Euler(0f, steeringVisual, 0f);
                }
            }

            if (wheelRollPivots == null || Mathf.Abs(forwardSpeed) < 0.025f) return;
            float rollDegrees = forwardSpeed * Time.fixedDeltaTime / WheelRadius * Mathf.Rad2Deg;
            foreach (Transform pivot in wheelRollPivots)
            {
                pivot.Rotate(Vector3.right, rollDegrees, Space.Self);
            }
        }
    }
}

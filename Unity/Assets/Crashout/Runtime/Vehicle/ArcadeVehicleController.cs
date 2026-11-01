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

        public Rigidbody Body => body;
        public float SpeedMetresPerSecond => body == null ? 0f : body.linearVelocity.magnitude;
        public bool PropulsionEnabled => propulsionEnabled;
        public float BoostFraction => boostMeter;
        public bool IsBoosting { get; private set; }

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

        public void ResetController()
        {
            propulsionEnabled = true;
            steeringVisual = 0f;
            boostMeter = 1f;
            IsBoosting = false;
        }

        public void StepAuthority(DriverInputCommand command)
        {
            if (body == null) return;

            ApplySuspension();
            float forwardSpeed = Vector3.Dot(body.linearVelocity, transform.forward);
            float lateralSpeed = Vector3.Dot(body.linearVelocity, transform.right);
            body.AddForce(-transform.right * lateralSpeed * lateralGrip * body.mass, ForceMode.Force);

            IsBoosting = propulsionEnabled && command.Boost && command.Throttle > 0f && boostMeter > 0.01f;
            if (IsBoosting) boostMeter = Mathf.Max(0f, boostMeter - Time.fixedDeltaTime / 3.2f);
            float speedLimit = IsBoosting ? maximumSpeed * 1.45f : maximumSpeed;

            if (propulsionEnabled)
            {
                if (command.Throttle > 0f && forwardSpeed < speedLimit)
                {
                    body.AddForce(transform.forward * (command.Throttle * motorForce * (IsBoosting ? 2.1f : 1f)), ForceMode.Force);
                }

                if (command.Brake > 0f)
                {
                    Vector3 brakingDirection = body.linearVelocity.sqrMagnitude > 0.04f
                        ? -body.linearVelocity.normalized
                        : -transform.forward;
                    body.AddForce(brakingDirection * (command.Brake * brakeForce), ForceMode.Force);
                }

                float steeringAuthority = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / 2.2f);
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

        private void ApplySuspension()
        {
            const int environmentMask = 1 << 0;
            foreach (Vector3 localPosition in WheelLocalPositions)
            {
                Vector3 origin = transform.TransformPoint(localPosition + Vector3.up * 0.28f);
                if (!Physics.Raycast(origin, -transform.up, out RaycastHit hit, suspensionRestLength + 0.34f, environmentMask, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                float compression = Mathf.Clamp01((suspensionRestLength - hit.distance + 0.28f) / suspensionRestLength);
                float pointVelocity = Vector3.Dot(body.GetPointVelocity(origin), transform.up);
                float force = compression * suspensionStrength - pointVelocity * suspensionDamping;
                body.AddForceAtPosition(transform.up * Mathf.Max(0f, force), origin, ForceMode.Force);
            }
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
            float rollDegrees = forwardSpeed * Time.fixedDeltaTime / 0.31f * Mathf.Rad2Deg;
            foreach (Transform pivot in wheelRollPivots)
            {
                pivot.Rotate(Vector3.right, rollDegrees, Space.Self);
            }
        }
    }
}

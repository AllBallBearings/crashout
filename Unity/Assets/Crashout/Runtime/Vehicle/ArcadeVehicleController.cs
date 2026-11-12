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
        [SerializeField] private float steeringTorque = 2300f;
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
        private float recoverUntil;
        private float lastKnockAt = -10f;

        /// <summary>Seconds of extra stability and steering authority after a glancing hit.</summary>
        private const float RecoverySeconds = 1.25f;
        private const float ReverseSpeed = 6f;

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
        /// <summary>1 just after a survivable knock, fading to 0 as the stability assist hands back control.</summary>
        public float RecoveryFraction => propulsionEnabled ? Mathf.Clamp01((recoverUntil - Time.time) / RecoverySeconds) : 0f;
        /// <summary>Time of the last survivable knock, for sound and camera feedback.</summary>
        public float LastKnockAt => lastKnockAt;
        public float LastKnockSpeed { get; private set; }

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

        private void OnCollisionEnter(Collision collision)
        {
            if (!propulsionEnabled || body == null) return;
            // Hitting another vehicle is the crash itself; that is the session's business, not a knock.
            if (collision.collider.GetComponentInParent<DamageableVehicle>() != null) return;
            float speed = collision.relativeVelocity.magnitude;
            Vector3 normal = collision.contactCount > 0 ? collision.GetContact(0).normal : Vector3.up;
            // Touching down on the road is not a knock.
            if (speed < 1.2f || normal.y > 0.75f) return;
            recoverUntil = Time.time + RecoverySeconds;
            lastKnockAt = Time.time;
            LastKnockSpeed = speed;
            // Take the sting out of the hit: keep most of the spin a glancing blow would add.
            Vector3 spin = body.angularVelocity;
            body.angularVelocity = new Vector3(spin.x * 0.45f, spin.y * 0.5f, spin.z * 0.45f);
        }

        private void OnCollisionStay(Collision collision)
        {
            if (!propulsionEnabled || body == null || collision.contactCount == 0) return;
            if (collision.rigidbody != null) return;
            Vector3 normal = collision.GetContact(0).normal;
            if (normal.y > 0.6f) return;
            // Scraping a wall or barrier: peel away from it and slide along instead of digging in.
            Vector3 flat = Vector3.ProjectOnPlane(normal, Vector3.up);
            if (flat.sqrMagnitude < 0.01f) return;
            flat.Normalize();
            recoverUntil = Mathf.Max(recoverUntil, Time.time + 0.5f);
            float into = Vector3.Dot(body.linearVelocity, -flat);
            if (into > 0f) body.AddForce(flat * (into * 6f), ForceMode.Acceleration);
            body.AddForce(flat * 3.5f, ForceMode.Acceleration);
            // Turn the nose parallel to the wall, toward the way the car is already travelling.
            Vector3 along = Vector3.ProjectOnPlane(body.linearVelocity, flat);
            along.y = 0f;
            if (along.sqrMagnitude > 4f)
            {
                float turn = Vector3.SignedAngle(Vector3.ProjectOnPlane(transform.forward, Vector3.up), along, Vector3.up);
                body.AddTorque(Vector3.up * Mathf.Clamp(turn * 0.12f, -4f, 4f), ForceMode.Acceleration);
            }
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
            recoverUntil = 0f;
            lastKnockAt = -10f;
            LastKnockSpeed = 0f;
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
                    if (forwardSpeed > 0.6f || WheelsGrounded == 0)
                    {
                        // Brakes act against the direction of travel and fade out near a standstill.
                        Vector3 flatVelocity = Vector3.ProjectOnPlane(body.linearVelocity, transform.up);
                        float fade = Mathf.Clamp01(flatVelocity.magnitude / 1.5f);
                        if (flatVelocity.sqrMagnitude > 0.01f && WheelsGrounded > 0)
                        {
                            body.AddForce(-flatVelocity.normalized * (command.Brake * brakeForce * fade), ForceMode.Force);
                        }
                    }
                    else if (forwardSpeed > -ReverseSpeed)
                    {
                        // Held at a standstill the brake becomes reverse, capped to a crawl.
                        body.AddForce(-transform.forward * (command.Brake * motorForce * 0.55f), ForceMode.Force);
                    }
                }

                float recovery = RecoveryFraction;
                float steeringAuthority = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / 2.2f) * (WheelsGrounded > 0 ? 1f : 0.15f);
                float speedFalloff = Mathf.Lerp(1f, 0.42f, Mathf.Clamp01(Mathf.Abs(forwardSpeed) / maximumSpeed));
                float direction = forwardSpeed < -0.5f ? -1f : 1f;
                // After a knock the driver gets extra bite to catch the car.
                float assist = 1f + recovery * 1.6f;
                body.AddTorque(transform.up * (command.Steering * direction * steeringTorque * steeringAuthority * speedFalloff * assist), ForceMode.Force);
                ApplyStability(command.Steering, forwardSpeed, recovery);
            }

            body.AddForce(-body.linearVelocity * body.mass * (propulsionEnabled ? 0.06f : 0.75f), ForceMode.Force);
            body.AddTorque(-body.angularVelocity * body.mass * (propulsionEnabled ? 0.08f : 0.42f), ForceMode.Force);
            UpdateWheelVisuals(command.Steering, forwardSpeed);
        }

        /// <summary>
        /// Keeps a knocked car catchable: unwanted spin is damped (strongly while the stick is centred),
        /// the nose is eased back onto the direction of travel, and the body is levelled so it lands
        /// and settles on its wheels instead of barrel-rolling off a kerb or barrier.
        /// </summary>
        private void ApplyStability(float steering, float forwardSpeed, float recovery)
        {
            bool grounded = WheelsGrounded > 0;
            Vector3 up = transform.up;
            float yawRate = Vector3.Dot(body.angularVelocity, up);
            float hands = 1f - Mathf.Clamp01(Mathf.Abs(steering) * 1.5f);

            // Spin damping: light always, firm with the stick centred, firmer still after a knock.
            float yawDamping = (grounded ? 0.5f + hands * 1.6f : 0.25f) + recovery * (2.5f + hands * 3.5f);
            body.AddTorque(-up * (yawRate * yawDamping), ForceMode.Acceleration);

            Vector3 flatVelocity = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
            if (grounded && recovery > 0f && flatVelocity.sqrMagnitude > 16f && forwardSpeed > 0f)
            {
                // Countersteer assist: point the car where it is actually going.
                Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                float slipAngle = Vector3.SignedAngle(flatForward, flatVelocity, Vector3.up);
                body.AddTorque(up * (Mathf.Clamp(slipAngle, -50f, 50f) * 0.09f * recovery * (0.35f + hands * 0.65f)), ForceMode.Acceleration);
                // And a little extra tyre grip to scrub off the slide.
                float lateral = Vector3.Dot(body.linearVelocity, transform.right);
                body.AddForce(-transform.right * (lateral * 2.2f * recovery), ForceMode.Acceleration);
            }

            // Self-levelling. In the air it sets the car up for the landing; after a knock it stops a roll-over.
            float level = grounded ? recovery * 9f : 3.2f + recovery * 5f;
            if (level > 0f && up.y > -0.2f)
            {
                Vector3 correction = Vector3.Cross(up, Vector3.up);
                Vector3 tumble = body.angularVelocity - up * yawRate;
                body.AddTorque(correction * level - tumble * (grounded ? recovery * 2.5f : 1.1f + recovery * 2f), ForceMode.Acceleration);
            }
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

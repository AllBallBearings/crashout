using UnityEngine;

namespace Crashout.Vehicle
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class TrafficVehicle : MonoBehaviour
    {
        [SerializeField] private float targetSpeed = 8f;
        [SerializeField] private float laneCoordinate;
        [SerializeField] private bool laneRunsAlongX = true;

        private Rigidbody body;
        private bool driveEnabled = true;
        private Vector3 travelDirection = Vector3.right;

        public bool DriveEnabled => driveEnabled;
        public Rigidbody Body => body;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.mass = 1450f;
            body.centerOfMass = new Vector3(0f, -0.25f, 0f);
            body.linearDamping = 0.08f;
            body.angularDamping = 0.3f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        public void Configure(Vector3 direction, float lane, float speed, bool alongX)
        {
            travelDirection = direction.normalized;
            laneCoordinate = lane;
            targetSpeed = speed;
            laneRunsAlongX = alongX;
            transform.rotation = Quaternion.LookRotation(travelDirection, Vector3.up);
        }

        public void DisableDrive()
        {
            driveEnabled = false;
        }

        public void ResetController()
        {
            driveEnabled = true;
        }

        public void StepAuthority()
        {
            if (body == null) return;
            if (!driveEnabled)
            {
                body.AddForce(-body.linearVelocity * body.mass * 0.82f, ForceMode.Force);
                body.AddTorque(-body.angularVelocity * body.mass * 0.46f, ForceMode.Force);
                return;
            }

            Vector3 forward = transform.forward;
            float forwardSpeed = Vector3.Dot(body.linearVelocity, forward);
            float speedError = targetSpeed - forwardSpeed;
            float force = Mathf.Clamp(speedError * body.mass * 1.8f, -5200f, 5200f);

            if (Physics.SphereCast(transform.position + forward * 2.2f, 0.5f, forward, out RaycastHit hit, 4.5f, 1 << 8, QueryTriggerInteraction.Ignore))
            {
                float brake = Mathf.InverseLerp(4.5f, 1.4f, hit.distance);
                force = Mathf.Lerp(force, -7000f, brake);
            }

            float currentLane = laneRunsAlongX ? transform.position.z : transform.position.x;
            Vector3 laneNormal = laneRunsAlongX ? Vector3.forward : Vector3.right;
            float laneError = laneCoordinate - currentLane;
            body.AddForce(forward * force + laneNormal * (laneError * body.mass * 4f), ForceMode.Force);
            body.AddTorque(Vector3.Cross(forward, travelDirection) * (body.mass * 3f), ForceMode.Force);
            body.AddForce(-Vector3.Project(body.linearVelocity, laneNormal) * body.mass * 2.6f, ForceMode.Force);
        }
    }
}

using UnityEngine;

namespace Crashout.Vehicle
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ResettableBody : MonoBehaviour
    {
        private Rigidbody body;
        private Vector3 initialPosition;
        private Quaternion initialRotation;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            CapturePose();
        }

        public void CapturePose()
        {
            if (body == null) body = GetComponent<Rigidbody>();
            initialPosition = body.position;
            initialRotation = body.rotation;
        }

        /// <summary>Moves the body and records the pose to return to. Writes the Rigidbody, not just the Transform.</summary>
        public void SetStartPose(Vector3 position, Quaternion rotation)
        {
            if (body == null) body = GetComponent<Rigidbody>();
            initialPosition = position;
            initialRotation = rotation;
            ResetBody();
        }

        public void ResetBody()
        {
            // A sleeping body never syncs its Transform, so a never-simulated car would keep rendering at its old pose.
            transform.SetPositionAndRotation(initialPosition, initialRotation);
            body.position = initialPosition;
            body.rotation = initialRotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.Sleep();
        }
    }
}

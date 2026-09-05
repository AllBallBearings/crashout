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

        public void ResetBody()
        {
            body.position = initialPosition;
            body.rotation = initialRotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.Sleep();
        }
    }
}

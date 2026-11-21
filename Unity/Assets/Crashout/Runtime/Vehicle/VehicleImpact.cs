using UnityEngine;

namespace Crashout.Vehicle
{
    /// <summary>Separates speed into the contacted surface from harmless travel along it.</summary>
    public readonly struct VehicleImpact
    {
        public readonly float Speed;
        public readonly Vector3 Normal;
        public readonly bool IsRoutineRoadContact;

        private VehicleImpact(float speed, Vector3 normal, bool roadContact)
        {
            Speed = speed;
            Normal = normal;
            IsRoutineRoadContact = roadContact && speed < 16f;
        }

        public static float NormalSpeed(Vector3 relativeVelocity, Vector3 normal)
        {
            return Mathf.Abs(Vector3.Dot(relativeVelocity, normal));
        }

        public static VehicleImpact Read(Collision collision)
        {
            float speed = 0f;
            Vector3 normal = Vector3.zero;
            bool roadContact = collision.contactCount > 0 && collision.rigidbody == null;
            for (int i = 0; i < collision.contactCount; i++)
            {
                Vector3 contactNormal = collision.GetContact(i).normal;
                float contactSpeed = NormalSpeed(collision.relativeVelocity, contactNormal);
                if (i == 0 || contactSpeed > speed)
                {
                    speed = contactSpeed;
                    normal = contactNormal;
                }
                // Check every contact so a wall hit mixed with a road contact still does damage.
                roadContact &= Mathf.Abs(contactNormal.y) > 0.7f;
            }
            return new VehicleImpact(speed, normal, roadContact);
        }
    }
}

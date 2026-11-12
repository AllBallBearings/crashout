using System.Collections.Generic;
using Crashout.Vehicle;
using UnityEngine;

namespace Crashout.Presentation
{
    /// <summary>
    /// Soft-body style crash damage for the render meshes: panels crumple in around each contact,
    /// glass crazes, lamps die and bumpers tear off. Purely visual; the physics colliders never change.
    /// The car is restored automatically when the session resets its damage.
    /// </summary>
    public sealed class VehicleDeformer : MonoBehaviour
    {
        private const float MaximumDisplacement = 0.42f;
        private const int DebrisLayer = 9;

        private struct Target
        {
            public MeshFilter Filter;
            public Mesh Mesh;
            public Vector3[] Original;
            public Vector3[] Current;
        }

        private sealed class Detachable
        {
            public GameObject Part;
            public Transform Parent;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
            public bool Detached;
        }

        private readonly List<Target> targets = new List<Target>();
        private readonly List<Detachable> detachables = new List<Detachable>();
        private VehicleLook look;
        private DamageableVehicle damageable;
        private Rigidbody body;
        private float halfLength;
        private float stiffness = 1f;
        private float lastDentAt;
        private bool dented;

        public int DentCount { get; private set; }

        public void Configure(List<MeshFilter> filters, List<GameObject> parts, VehicleLook vehicleLook, float vehicleHalfLength, float softness)
        {
            look = vehicleLook;
            halfLength = vehicleHalfLength;
            stiffness = softness;
            foreach (MeshFilter filter in filters)
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null) continue;
                mesh.MarkDynamic();
                Vector3[] vertices = mesh.vertices;
                targets.Add(new Target { Filter = filter, Mesh = mesh, Original = vertices, Current = (Vector3[])vertices.Clone() });
            }
            foreach (GameObject part in parts)
            {
                detachables.Add(new Detachable
                {
                    Part = part,
                    Parent = part.transform.parent,
                    LocalPosition = part.transform.localPosition,
                    LocalRotation = part.transform.localRotation
                });
            }
        }

        private void Start()
        {
            damageable = GetComponent<DamageableVehicle>();
            body = GetComponent<Rigidbody>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            float speed = collision.relativeVelocity.magnitude;
            if (speed < 2.2f || Time.time - lastDentAt < 0.03f) return;
            int count = Mathf.Min(collision.contactCount, 4);
            if (count == 0) return;

            // Shrug off the road: only hard landings dent the underside.
            Vector3 averageNormal = Vector3.zero;
            Vector3 averagePoint = Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                ContactPoint contact = collision.GetContact(i);
                averageNormal += contact.normal;
                averagePoint += contact.point;
            }
            averageNormal /= count;
            averagePoint /= count;
            bool scenery = collision.collider.GetComponentInParent<DamageableVehicle>() == null;
            // (A fast landing off a crest is not a crash: bumpers stay on until something is actually hit.)
            if (scenery && Mathf.Abs(averageNormal.y) > 0.7f && speed < 16f) return;

            lastDentAt = Time.time;
            float mass = body != null ? body.mass : 1400f;
            float massFactor = Mathf.Clamp(1500f / mass, 0.35f, 1.2f);
            float depth = Mathf.Clamp(speed * 0.02f, 0.03f, 0.34f) * massFactor * stiffness;
            float radius = Mathf.Clamp(0.45f + speed * 0.045f, 0.5f, 1.25f) * Mathf.Lerp(1f, 1.6f, 1f - massFactor);

            Vector3 centre = transform.TransformPoint(new Vector3(0f, 0.1f, 0f));
            for (int i = 0; i < count; i++)
            {
                ContactPoint contact = collision.GetContact(i);
                Vector3 push = contact.normal;
                if (Vector3.Dot(push, centre - contact.point) < 0f) push = -push;
                Dent(transform.InverseTransformPoint(contact.point), transform.InverseTransformDirection(push), depth / count * 1.6f, radius);
            }
            Apply();

            Vector3 local = transform.InverseTransformPoint(averagePoint);
            bool frontHit = local.z > halfLength - 0.9f;
            bool rearHit = local.z < -halfLength + 0.9f;
            if (look != null)
            {
                if (speed > 6f && frontHit) look.BreakLights(true);
                if (speed > 6f && rearHit) look.BreakLights(false);
                if (speed > 8.5f) look.ShatterGlass();
            }
            if (speed > 8f)
            {
                foreach (Detachable part in detachables)
                {
                    if (part.Detached || part.Part == null) continue;
                    float partZ = part.LocalPosition.z;
                    if (Mathf.Abs(partZ - local.z) < 0.9f || speed > 16f && Random.value < 0.5f) Detach(part, collision.relativeVelocity);
                }
            }
        }

        private void Dent(Vector3 point, Vector3 push, float depth, float radius)
        {
            float radiusSq = radius * radius;
            // A little crumple noise so the dent is not a perfect dish.
            int seed = DentCount * 7 + 3;
            foreach (Target target in targets)
            {
                Vector3[] current = target.Current;
                Vector3[] original = target.Original;
                for (int v = 0; v < current.Length; v++)
                {
                    Vector3 offset = current[v] - point;
                    float distanceSq = offset.sqrMagnitude;
                    if (distanceSq > radiusSq) continue;
                    float falloff = 1f - Mathf.Sqrt(distanceSq) / radius;
                    falloff *= falloff;
                    float crumple = 0.75f + 0.5f * ProceduralTextures.Value(current[v].x * 9f + seed, current[v].z * 9f + current[v].y * 5f, 0, seed);
                    Vector3 moved = current[v] + push * (depth * falloff * crumple);
                    Vector3 total = moved - original[v];
                    if (total.sqrMagnitude > MaximumDisplacement * MaximumDisplacement)
                    {
                        moved = original[v] + total.normalized * MaximumDisplacement;
                    }
                    current[v] = moved;
                }
            }
            DentCount++;
            dented = true;
        }

        private void Apply()
        {
            foreach (Target target in targets)
            {
                target.Mesh.SetVertices(target.Current);
                target.Mesh.RecalculateNormals();
                target.Mesh.RecalculateBounds();
                ReplayMeshRevision.Changed(target.Filter);
            }
        }

        private void Detach(Detachable part, Vector3 relativeVelocity)
        {
            part.Detached = true;
            GameObject piece = part.Part;
            piece.transform.SetParent(null, true);
            SetLayer(piece, DebrisLayer);
            BoxCollider box = piece.AddComponent<BoxCollider>();
            Renderer renderer = piece.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                Bounds bounds = renderer.bounds;
                box.center = piece.transform.InverseTransformPoint(bounds.center);
                box.size = new Vector3(Mathf.Max(0.1f, bounds.size.x * 0.9f), Mathf.Max(0.08f, bounds.size.y), Mathf.Max(0.08f, bounds.size.z));
            }
            Rigidbody partBody = piece.AddComponent<Rigidbody>();
            partBody.mass = 18f;
            partBody.linearDamping = 0.2f;
            partBody.angularDamping = 0.3f;
            Vector3 carVelocity = body != null ? body.linearVelocity : Vector3.zero;
            partBody.linearVelocity = carVelocity + relativeVelocity * 0.25f + Random.insideUnitSphere * 2.5f + Vector3.up * 2.5f;
            partBody.angularVelocity = Random.insideUnitSphere * 9f;
        }

        private static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayer(child.gameObject, layer);
        }

        private void Restore()
        {
            foreach (Target target in targets)
            {
                System.Array.Copy(target.Original, target.Current, target.Original.Length);
                target.Mesh.SetVertices(target.Current);
                target.Mesh.RecalculateNormals();
                target.Mesh.RecalculateBounds();
                ReplayMeshRevision.Changed(target.Filter);
            }
            foreach (Detachable part in detachables)
            {
                if (!part.Detached || part.Part == null) continue;
                Rigidbody partBody = part.Part.GetComponent<Rigidbody>();
                if (partBody != null) Destroy(partBody);
                Collider collider = part.Part.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
                part.Part.transform.SetParent(part.Parent, false);
                part.Part.transform.localPosition = part.LocalPosition;
                part.Part.transform.localRotation = part.LocalRotation;
                SetLayer(part.Part, gameObject.layer);
                part.Detached = false;
            }
            dented = false;
            DentCount = 0;
        }

        private void LateUpdate()
        {
            if (damageable == null) return;
            bool anyDetached = false;
            foreach (Detachable part in detachables) anyDetached |= part.Detached;
            if ((dented || anyDetached) && damageable.Damage <= 0.0001f) Restore();
        }

        private void OnDestroy()
        {
            foreach (Detachable part in detachables)
            {
                if (part.Detached && part.Part != null) Destroy(part.Part);
            }
            foreach (Target target in targets)
            {
                if (target.Mesh != null) Destroy(target.Mesh);
            }
        }
    }
}

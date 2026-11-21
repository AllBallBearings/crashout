using System.Collections.Generic;
using Crashout.Vehicle;
using UnityEngine;

namespace Crashout.Presentation
{
    /// <summary>Contact-driven plastic damage, detachable panels/wheels and physical chassis separation.</summary>
    public sealed class VehicleDeformer : MonoBehaviour
    {
        private const int DebrisLayer = 9;
        private sealed class Target
        {
            public MeshFilter Filter;
            public Mesh Mesh;
            public Vector3[] Original, RootOriginal, Dents, Current;
            public Vector3[] OriginalNormals;
            public Vector3[] RootNormals;
            public Vector4[] OriginalTangents;
            public Color[] DamageColours;
            public bool SheetMetal;
        }
        private sealed class Detachable
        {
            public GameObject Part;
            public Transform Parent;
            public Vector3 Position, Centre;
            public Quaternion Rotation;
            public int Layer;
            public float Strain, Mass;
            public bool Detached, Wheel;
        }
        private sealed class ContactBox
        {
            public BoxCollider Box;
            public Vector3 Centre, Size;
        }
        private readonly List<Target> targets = new List<Target>();
        private readonly List<Detachable> parts = new List<Detachable>();
        private readonly List<ContactBox> contactBoxes = new List<ContactBox>();
        private readonly List<Mesh> tearMeshes = new List<Mesh>();
        private readonly List<GameObject> wrecks = new List<GameObject>();
        private VehicleDamageShape shape = new VehicleDamageShape();
        private VehicleLook look;
        private DamageableVehicle damageable;
        private Rigidbody body;
        private float halfLength, halfWidth = 0.9f, softness = 1f, initialMass;
        private Vector3 initialCentreOfMass;
        private float lastDentAt = -1f, lastPressureAt = -1f;
        private Vector3 lastPush;
        private bool fractured, glassShed;
        public int DentCount { get; private set; }
        public bool IsFractured => fractured;
        public int DetachedPartCount { get { int n = 0; foreach (var p in parts) if (p.Detached) n++; return n; } }

        public void Configure(List<MeshFilter> filters, List<GameObject> detachables, VehicleLook vehicleLook, float vehicleHalfLength, float stiffness)
        {
            look = vehicleLook; halfLength = vehicleHalfLength; softness = stiffness;
            foreach (MeshFilter filter in filters)
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null) continue;
                if (ProceduralMeshes.IsShared(mesh)) { mesh = Instantiate(mesh); filter.sharedMesh = mesh; }
                mesh.MarkDynamic();
                Vector3[] original = mesh.vertices;
                var root = new Vector3[original.Length];
                var normals = mesh.normals; var rootNormals = new Vector3[original.Length];
                Matrix4x4 toRoot = transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int i = 0; i < original.Length; i++)
                {
                    root[i] = toRoot.MultiplyPoint3x4(original[i]);
                    rootNormals[i] = toRoot.MultiplyVector(normals.Length > i ? normals[i] : Vector3.up).normalized;
                }
                targets.Add(new Target { Filter = filter, Mesh = mesh, Original = original, OriginalNormals = mesh.normals, OriginalTangents = mesh.tangents,
                    RootOriginal = root, RootNormals = rootNormals, DamageColours = new Color[root.Length],
                    SheetMetal = (filter.GetComponent<Renderer>().sharedMaterial == look.Paint && !filter.name.EndsWith("framePaint")) || filter.GetComponent<Renderer>().sharedMaterial.name == "Galvanised steel",
                    Dents = new Vector3[root.Length], Current = (Vector3[])original.Clone() });
                mesh.colors = new Color[root.Length];
            }
            foreach (GameObject part in detachables)
            {
                Bounds bounds = LocalBounds(part.transform, transform);
                bool wheel = part.name.StartsWith("Wheel steer");
                parts.Add(new Detachable
                {
                    Part = part, Parent = part.transform.parent, Position = part.transform.localPosition, Rotation = part.transform.localRotation,
                    Centre = bounds.center, Layer = part.layer, Wheel = wheel,
                    Mass = wheel ? 24f : part.name.Contains("door") ? 32f : part.name.Contains("mirror") ? 1.5f : part.name.Contains("Roof") ? 24f : 12f
                });
            }
        }

        private void Start()
        {
            body = GetComponent<Rigidbody>(); damageable = GetComponent<DamageableVehicle>();
            initialMass = body != null ? body.mass : 1400f;
            initialCentreOfMass = body != null ? body.centerOfMass : Vector3.zero;
            foreach (BoxCollider box in GetComponentsInChildren<BoxCollider>())
                contactBoxes.Add(new ContactBox { Box = box, Centre = box.center, Size = box.size });
            BoxCollider chassis = GetComponent<BoxCollider>();
            if (chassis != null) halfWidth = chassis.size.x * 0.5f;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (fractured || Time.time - lastDentAt < 0.025f) return;
            VehicleImpact impact = VehicleImpact.Read(collision);
            if (impact.IsRoutineRoadContact || impact.Speed < 2.2f || collision.contactCount == 0) return;
            Contact(collision, impact, 0f);
        }

        private void OnCollisionStay(Collision collision)
        {
            if (fractured || collision.contactCount == 0 || body == null) return;
            VehicleImpact impact = VehicleImpact.Read(collision);
            if (impact.IsRoutineRoadContact || Mathf.Abs(impact.Normal.y) > 0.7f) return;
            Vector3 push = transform.InverseTransformDirection(impact.Normal);
            float deltaV = collision.impulse.magnitude / Mathf.Max(1f, body.mass);
            // Opposing loaded contacts mean the car is pinned, even after its relative speed reaches zero.
            bool opposed = Time.time - lastPressureAt < 0.3f && Vector3.Dot(push, lastPush) < -0.5f;
            lastPush = push; lastPressureAt = Time.time;
            if (!opposed || deltaV < 0.025f || Time.time - lastDentAt < 0.06f) return;
            Contact(collision, impact, Mathf.Min(0.03f, deltaV * 0.025f));
        }

        private void Contact(Collision collision, VehicleImpact impact, float pressure)
        {
            if (body == null) return;
            float deltaV = collision.impulse.magnitude / Mathf.Max(1f, body.mass);
            if (deltaV < 0.04f && pressure <= 0f) return; // Light splinters cannot crush a heavy chassis.
            lastDentAt = Time.time;
            Vector3 worldPoint = Vector3.zero;
            int count = Mathf.Min(collision.contactCount, 4);
            for (int i = 0; i < count; i++) worldPoint += collision.GetContact(i).point;
            worldPoint /= count;
            Vector3 point = Quaternion.Inverse(body.rotation) * (worldPoint - body.position);
            Vector3 push = (Quaternion.Inverse(body.rotation) * impact.Normal).normalized;
            shape.Contact(point, push, impact.Speed, deltaV, halfLength, softness, pressure);
            float transfer = Mathf.Clamp01(deltaV / Mathf.Max(1f, impact.Speed * 0.15f));
            float depth = Mathf.Clamp(impact.Speed * 0.014f * transfer + deltaV * 0.01f + pressure, 0.015f, 0.38f) * softness;
            float radius = Mathf.Clamp(0.35f + impact.Speed * 0.045f, 0.45f, 1.7f);
            foreach (Target target in targets)
            {
                if (target.Filter == null || !target.Filter.transform.IsChildOf(transform)) continue;
                for (int i = 0; i < target.RootOriginal.Length; i++)
                {
                    Vector3 original = target.RootOriginal[i] + target.Dents[i];
                    Vector3 p = target.SheetMetal ? shape.MapSurface(original, target.RootNormals[i], halfLength, halfWidth) : shape.Map(original, halfLength, halfWidth);
                    float distance = Vector3.Distance(p, point);
                    if (distance >= radius) continue;
                    float falloff = 1f - distance / radius;
                    // A contact depression has a raised crease around its perimeter. It is local
                    // to the struck sheet; the rest of the panel keeps its original silhouette.
                    float rim = Mathf.Sin(falloff * Mathf.PI) * Mathf.Sin(falloff * Mathf.PI) * 0.22f;
                    Vector3 outward = target.RootNormals[i];
                    if (Vector3.Dot(outward, push) > 0f) outward = -outward;
                    Vector3 dent = target.Dents[i] + push * (depth * falloff * falloff) + outward * (depth * rim);
                    target.Dents[i] = Vector3.ClampMagnitude(dent, halfWidth * 0.6f);
                    Color damage = target.DamageColours[i];
                    damage.r = Mathf.Clamp01(damage.r + depth * falloff * 2.8f);
                    damage.g = Mathf.Clamp01(damage.g + depth * rim * 5f);
                    target.DamageColours[i] = damage;
                }
            }
            DentCount++;
            Apply(); UpdateContactShape();
            if (look != null)
            {
                if (impact.Speed > 7f && point.z > halfLength * 0.6f) look.BreakLights(true);
                if (impact.Speed > 7f && point.z < -halfLength * 0.6f) look.BreakLights(false);
                if ((impact.Speed > 10f && deltaV > 1.5f) || (pressure > 0f && shape.FrontCrush + shape.RearCrush > 0.35f))
                {
                    look.ShatterGlass();
                    if (!glassShed) ShedGlass(worldPoint, transform.TransformDirection(push), deltaV);
                }
            }
            foreach (Detachable part in parts)
            {
                if (part.Detached || part.Part == null) continue;
                float distance = Vector3.Distance(part.Centre, point);
                float influence = Mathf.Clamp01(1f - distance / (part.Wheel ? 1.15f : 1.7f));
                part.Strain += influence * (deltaV * 0.08f + impact.Speed * 0.018f + pressure * 2f) * softness;
                float failure = part.Wheel ? 3.6f : part.Mass < 3f ? 0.9f : part.Part.name.Contains("Roof") ? 3.8f : part.Part.name.Contains("door") ? 2.5f : 1.8f;
                if (part.Strain > failure && (impact.Speed > 9f || pressure > 0f))
                    Detach(part, transform.TransformDirection(push) * Mathf.Min(5f, deltaV * 0.25f));
            }
            if (shape.ShouldFracture) Fracture(Mathf.Clamp(point.z, -halfLength * 0.3f, halfLength * 0.3f));
        }

        private void Apply()
        {
            foreach (Detachable part in parts)
                if (part.Wheel && !part.Detached && part.Part != null)
                    part.Part.transform.localPosition = shape.Map(part.Position, halfLength, halfWidth);
            foreach (Target target in targets)
            {
                if (target.Filter == null || !target.Filter.transform.IsChildOf(transform)) continue;
                Matrix4x4 toLocal = target.Filter.transform.worldToLocalMatrix * transform.localToWorldMatrix;
                bool changed = false;
                for (int i = 0; i < target.Current.Length; i++)
                {
                    Vector3 original = target.RootOriginal[i] + target.Dents[i];
                    Vector3 next = toLocal.MultiplyPoint3x4(target.SheetMetal
                        ? shape.MapSurface(original, target.RootNormals[i], halfLength, halfWidth)
                        : shape.Map(original, halfLength, halfWidth));
                    changed |= (next - target.Current[i]).sqrMagnitude > 1e-10f;
                    target.Current[i] = next;
                }
                if (!changed) continue;
                target.Mesh.SetVertices(target.Current); target.Mesh.RecalculateNormals(); target.Mesh.RecalculateBounds(); target.Mesh.RecalculateTangents();
                target.Mesh.colors = target.DamageColours;
                ReplayMeshRevision.Changed(target.Filter);
            }
        }

        private void UpdateContactShape()
        {
            foreach (ContactBox box in contactBoxes)
            {
                if (box.Box == null) continue;
                Matrix4x4 root = transform.worldToLocalMatrix * box.Box.transform.localToWorldMatrix;
                Matrix4x4 local = root.inverse;
                Bounds b = new Bounds();
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = box.Centre + Vector3.Scale(box.Size * 0.5f, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    Vector3 p = local.MultiplyPoint3x4(shape.Map(root.MultiplyPoint3x4(corner), halfLength, halfWidth));
                    if (i == 0) b = new Bounds(p, Vector3.zero); else b.Encapsulate(p);
                }
                box.Box.center = b.center; box.Box.size = Vector3.Max(b.size, Vector3.one * 0.1f);
            }
        }

        private static Bounds LocalBounds(Transform part, Transform space)
        {
            Bounds b = new Bounds(); bool any = false;
            foreach (MeshFilter filter in part.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null || filter.sharedMesh.vertexCount == 0) continue;
                Matrix4x4 m = space.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                Bounds bounds = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    Vector3 p = m.MultiplyPoint3x4(corner);
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            return b;
        }

        private void IgnoreOwnerCollisions(Collider piece)
        {
            foreach (Collider owner in GetComponentsInChildren<Collider>())
                if (owner != piece) Physics.IgnoreCollision(piece, owner);
            foreach (GameObject wreck in wrecks)
                if (wreck != null) foreach (Collider other in wreck.GetComponentsInChildren<Collider>())
                    if (other != piece) Physics.IgnoreCollision(piece, other);
            foreach (Detachable part in parts)
                if (part.Detached && part.Part != null)
                    foreach (Collider other in part.Part.GetComponentsInChildren<Collider>()) if (other != piece) Physics.IgnoreCollision(piece, other);
        }

        private void Detach(Detachable part, Vector3 impulseVelocity)
        {
            Bounds bounds = LocalBounds(part.Part.transform, part.Part.transform);
            Vector3 worldCentre = part.Part.transform.TransformPoint(bounds.center);
            Vector3 localCentre = transform.InverseTransformPoint(worldCentre);
            Vector3 velocity = body.GetPointVelocity(worldCentre);
            part.Detached = true;
            Transform piece = part.Part.transform; piece.SetParent(null, true); SetLayer(part.Part, DebrisLayer);
            BoxCollider box = part.Part.AddComponent<BoxCollider>(); box.center = bounds.center; box.size = Vector3.Max(bounds.size, Vector3.one * 0.06f);
            Rigidbody rb = part.Part.AddComponent<Rigidbody>(); rb.mass = part.Mass;
            rb.centerOfMass = bounds.center; rb.linearDamping = 0.1f; rb.angularDamping = 0.12f;
            rb.linearVelocity = velocity + impulseVelocity; rb.angularVelocity = body.angularVelocity;
            // Contact force applied away from the piece's centre creates spin instead of a random kick.
            Vector3 torquePoint = piece.TransformPoint(bounds.center + bounds.extents * 0.4f);
            Vector3 torqueImpulse = impulseVelocity * part.Mass * 0.15f;
            rb.AddForceAtPosition(torqueImpulse, torquePoint, ForceMode.Impulse);
            float oldMass = body.mass, newMass = Mathf.Max(initialMass * 0.55f, oldMass - part.Mass);
            body.centerOfMass = (body.centerOfMass * oldMass - localCentre * part.Mass) / newMass;
            body.mass = newMass;
            body.AddForce(-impulseVelocity * part.Mass, ForceMode.Impulse);
            body.AddForceAtPosition(-torqueImpulse, torquePoint, ForceMode.Impulse);
            IgnoreOwnerCollisions(box);
        }

        private void ShedGlass(Vector3 point, Vector3 normal, float deltaV)
        {
            glassShed = true;
            for (int i = 0; i < 6; i++)
            {
                float size = 0.035f + i * 0.012f;
                var kit = new MeshKit();
                int a = kit.Vertex(new Vector3(-size, 0f, -size * 0.5f), Vector3.up, Vector2.zero);
                int b = kit.Vertex(new Vector3(size * 0.8f, 0.006f, 0f), Vector3.up, Vector2.right);
                int c = kit.Vertex(new Vector3(0f, 0f, size), Vector3.up, Vector2.up);
                kit.Triangle(a, c, b);
                Mesh mesh = kit.ToMesh("Glass splinter"); tearMeshes.Add(mesh);
                GameObject shard = MeshKit.Spawn(null, "Glass splinter", mesh, look.ShatteredGlass, false); SetLayer(shard, DebrisLayer);
                shard.transform.position = point + Vector3.up * 0.12f;
                shard.transform.rotation = transform.rotation * Quaternion.Euler(i * 29f, i * 67f, i * 41f);
                BoxCollider box = shard.AddComponent<BoxCollider>(); box.size = new Vector3(size * 1.8f, 0.008f, size * 1.5f);
                Rigidbody rb = shard.AddComponent<Rigidbody>(); rb.mass = 0.12f; rb.linearDamping = 0.12f;
                Vector3 scatter = transform.TransformDirection(new Vector3(Mathf.Sin(i * 2.4f), 0.4f, Mathf.Cos(i * 2.4f)));
                Vector3 kick = normal * Mathf.Min(4f, deltaV * 0.2f) + scatter * 1.4f;
                rb.linearVelocity = body.GetPointVelocity(point) + kick;
                rb.angularVelocity = body.angularVelocity + Vector3.Cross(normal, scatter) * 8f;
                body.mass -= rb.mass; body.AddForce(-kick * rb.mass, ForceMode.Impulse);
                IgnoreOwnerCollisions(box); wrecks.Add(shard);
            }
        }

        private void Fracture(float cutZ)
        {
            fractured = true;
            GameObject rear = new GameObject(name + " torn rear section"); rear.transform.SetPositionAndRotation(transform.position, transform.rotation); SetLayer(rear, DebrisLayer);
            foreach (Target target in targets)
            {
                if (target.Filter == null || !target.Filter.transform.IsChildOf(transform)) continue;
                Mesh source = target.Mesh; Vector3[] points = source.vertices, normals = source.normals;
                Matrix4x4 root = transform.worldToLocalMatrix * target.Filter.transform.localToWorldMatrix;
                for (int i = 0; i < points.Length; i++) { points[i] = root.MultiplyPoint3x4(points[i]); normals[i] = root.MultiplyVector(normals[i]).normalized; }
                VehicleMeshSlice front = VehicleMeshSlice.Cut(points, normals, source.uv, source.triangles, cutZ, true, source.colors);
                VehicleMeshSlice back = VehicleMeshSlice.Cut(points, normals, source.uv, source.triangles, cutZ, false, source.colors);
                front.CurlTornEdges(true); back.CurlTornEdges(false);
                if (back.Triangles.Count > 0)
                {
                    Mesh mesh = back.ToMesh(source.name + " torn rear"); tearMeshes.Add(mesh);
                    MeshKit.Spawn(rear.transform, target.Filter.name + " torn", mesh, target.Filter.GetComponent<Renderer>().sharedMaterial);
                }
                Matrix4x4 local = root.inverse;
                for (int i = 0; i < front.Vertices.Count; i++) { front.Vertices[i] = local.MultiplyPoint3x4(front.Vertices[i]); front.Normals[i] = local.MultiplyVector(front.Normals[i]).normalized; }
                Mesh frontMesh = front.ToMesh(source.name + " torn front"); tearMeshes.Add(frontMesh);
                target.Filter.sharedMesh = frontMesh; ReplayMeshRevision.Changed(target.Filter);
            }
            foreach (Detachable part in parts)
            {
                if (part.Detached || !part.Wheel || part.Centre.z > cutZ) continue;
                part.Part.transform.SetParent(rear.transform, true); part.Detached = true; SetLayer(part.Part, DebrisLayer);
            }
            Bounds bounds = LocalBounds(rear.transform, rear.transform);
            BoxCollider collider = rear.AddComponent<BoxCollider>(); collider.center = bounds.center; collider.size = Vector3.Max(bounds.size, Vector3.one * 0.15f);
            float fraction = Mathf.Clamp((cutZ + halfLength) / (halfLength * 2f), 0.25f, 0.75f);
            Rigidbody rearBody = rear.AddComponent<Rigidbody>(); rearBody.mass = body.mass * fraction; rearBody.centerOfMass = bounds.center;
            rearBody.linearVelocity = body.GetPointVelocity(rear.transform.TransformPoint(bounds.center)); rearBody.angularVelocity = body.angularVelocity;
            rearBody.linearDamping = body.linearDamping; rearBody.angularDamping = body.angularDamping;
            body.mass *= 1f - fraction;
            rear.AddComponent<VehicleWreckPiece>().Owner = damageable;
            foreach (ContactBox box in contactBoxes)
            {
                float max = box.Box.center.z + box.Box.size.z * 0.5f, min = Mathf.Max(cutZ, box.Box.center.z - box.Box.size.z * 0.5f);
                if (max <= min) { box.Box.enabled = false; continue; }
                Vector3 c = box.Box.center, s = box.Box.size; c.z = (min + max) * 0.5f; s.z = max - min; box.Box.center = c; box.Box.size = s;
            }
            IgnoreOwnerCollisions(collider); wrecks.Add(rear);
            Bounds frontBounds = LocalBounds(transform, transform);
            body.centerOfMass = new Vector3(frontBounds.center.x, initialCentreOfMass.y, Mathf.Max(cutZ + 0.1f, frontBounds.center.z));
            GetComponent<ArcadeVehicleController>()?.SetPropulsionEnabled(false);
            GetComponent<TrafficVehicle>()?.DisableDrive();
            damageable?.ReportStructuralFailure();
        }

        public void RestoreVehicle()
        {
            foreach (Detachable part in parts)
            {
                if (part.Part == null) continue;
                if (part.Detached)
                {
                    foreach (Collider c in part.Part.GetComponents<Collider>()) { c.enabled = false; Destroy(c); }
                    Rigidbody rb = part.Part.GetComponent<Rigidbody>(); if (rb != null) { rb.isKinematic = true; Destroy(rb); }
                }
                part.Part.transform.SetParent(part.Parent, false); part.Part.transform.localPosition = part.Position; part.Part.transform.localRotation = part.Rotation;
                SetLayer(part.Part, part.Layer); part.Detached = false; part.Strain = 0f;
            }
            foreach (Target target in targets)
            {
                if (target.Filter == null) continue;
                System.Array.Clear(target.Dents, 0, target.Dents.Length); System.Array.Copy(target.Original, target.Current, target.Current.Length);
                System.Array.Clear(target.DamageColours, 0, target.DamageColours.Length); target.Mesh.colors = target.DamageColours;
                target.Mesh.SetVertices(target.Original); target.Mesh.normals = target.OriginalNormals;
                target.Mesh.tangents = target.OriginalTangents; target.Mesh.RecalculateBounds();
                target.Filter.sharedMesh = target.Mesh; ReplayMeshRevision.Changed(target.Filter);
            }
            foreach (ContactBox box in contactBoxes) if (box.Box != null) { box.Box.enabled = true; box.Box.center = box.Centre; box.Box.size = box.Size; }
            foreach (GameObject wreck in wrecks) if (wreck != null) { wreck.SetActive(false); Destroy(wreck); } wrecks.Clear();
            foreach (Mesh mesh in tearMeshes) if (mesh != null) Destroy(mesh); tearMeshes.Clear();
            if (body != null && initialMass > 0f) { body.mass = initialMass; body.centerOfMass = initialCentreOfMass; }
            look?.Repair(); shape = new VehicleDamageShape(); fractured = glassShed = false; DentCount = 0; lastDentAt = -1f; lastPressureAt = -1f;
        }

        private static void SetLayer(GameObject go, int layer)
        { go.layer = layer; foreach (Transform child in go.transform) SetLayer(child.gameObject, layer); }
        private void LateUpdate()
        {
            if (damageable != null && (DentCount > 0 || fractured) && damageable.Damage <= 0.0001f) RestoreVehicle();
        }
        private void OnDestroy()
        {
            foreach (Detachable part in parts) if (part.Detached && part.Part != null) Destroy(part.Part);
            foreach (GameObject wreck in wrecks) if (wreck != null) Destroy(wreck);
            foreach (Mesh mesh in tearMeshes) if (mesh != null) Destroy(mesh);
            foreach (Target target in targets) if (target.Mesh != null) Destroy(target.Mesh);
        }
    }
}

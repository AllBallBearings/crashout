using System;
using System.Collections.Generic;
using Crashout.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Crashout.Presentation
{
    public enum StructureKind : byte
    {
        Masonry,
        Glass,
        Pole,
        Tree,
        Plastic,
        Hydrant,
        Loose
    }

    public struct StructureHit
    {
        public StructureKind Kind;
        public Vector3 Position;
        public Vector3 Normal;
        /// <summary>0 (a scuff) to 1 (a bus at speed or a blast).</summary>
        public float Severity;
    }

    /// <summary>
    /// Damage to the city itself. Facades take cracked, gouged impact scars and shed masonry; windows
    /// near a hit or a blast are blown out; awnings, air-conditioning units and signs are knocked off
    /// their fixings; street furniture (lamps, trees, hydrants, bins, cones, barriers) is real physics
    /// held by breakable joints. Everything is restored when the turn resets. The buildings' own
    /// colliders never change, so the junction plays the same every run.
    /// </summary>
    public sealed class Destruction : MonoBehaviour
    {
        private const int DebrisLayer = 9;
        public const int PropLayer = 10;
        private const int MaxScars = 70;
        private const int MaxChunks = 90;

        private struct Window
        {
            public Vector3 Centre;
            public Vector3 Normal;
            public float Width, Height;
            public bool Broken;
        }

        private sealed class Loose
        {
            public GameObject Part;
            public Transform Parent;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
            public Vector3 WorldPosition;
            public bool Fallen;
        }

        private readonly List<Window> windows = new List<Window>();
        private readonly List<Loose> looseParts = new List<Loose>();
        private readonly List<BreakableProp> props = new List<BreakableProp>();
        private readonly List<DamageableFacade> facades = new List<DamageableFacade>();
        private readonly List<GameObject> scars = new List<GameObject>();
        private readonly List<GameObject> chunks = new List<GameObject>();
        private readonly List<GameObject> transient = new List<GameObject>();
        private readonly Dictionary<long, float> lastHitAt = new Dictionary<long, float>();

        private CrashoutGameSession session;
        private Material[] scarMaterials;
        private Material scorchMaterial;
        private Material brokenWindowMaterial;
        private Mesh[] chunkMeshes;
        private Mesh brokenWindowMesh;
        private GameObject brokenWindowObject;
        private bool windowsDirty;
        private int brokenWindows;

        public static Destruction Instance { get; private set; }

        /// <summary>Raised for every piece of damage so effects and sound can answer it.</summary>
        public event Action<StructureHit> Hit;

        public int ScarCount => scars.Count;
        public int BrokenWindowCount => brokenWindows;
        public int BrokenPropCount
        {
            get
            {
                int count = 0;
                foreach (BreakableProp prop in props) if (prop != null && prop.Broken) count++;
                return count;
            }
        }

        private void Awake()
        {
            Instance = this;
            Physics.IgnoreLayerCollision(DebrisLayer, PropLayer, false);
        }

        private void OnDestroy()
        {
            if (session != null)
            {
                session.Exploded -= OnExploded;
                session.PhaseChanged -= OnPhase;
            }
            if (Instance == this) Instance = null;
        }

        public void Configure(CrashoutGameSession gameSession)
        {
            session = gameSession;
            session.Exploded += OnExploded;
            session.PhaseChanged += OnPhase;
        }

        // ------------------------------------------------------------------ registration (called while the city is built)

        public void RegisterFacade(DamageableFacade facade)
        {
            facades.Add(facade);
        }

        public void RegisterWindow(Vector3 centre, Vector3 normal, float width, float height)
        {
            windows.Add(new Window { Centre = centre, Normal = normal, Width = width, Height = height });
        }

        /// <summary>A fitting (awning, sign, AC unit) that a nearby hit can knock off the building.</summary>
        public void RegisterLoose(GameObject part)
        {
            looseParts.Add(new Loose
            {
                Part = part,
                Parent = part.transform.parent,
                LocalPosition = part.transform.localPosition,
                LocalRotation = part.transform.localRotation,
                WorldPosition = part.transform.position
            });
        }

        public void RegisterProp(BreakableProp prop)
        {
            props.Add(prop);
        }

        /// <summary>Forgets the city (it is about to be rebuilt) and removes everything this spawned.</summary>
        public void Clear()
        {
            Restore();
            windows.Clear();
            looseParts.Clear();
            props.Clear();
            facades.Clear();
        }

        // ------------------------------------------------------------------ events

        private void OnPhase(GamePhase phase)
        {
            if (phase == GamePhase.ReadyCheck) Restore();
        }

        private void Restore()
        {
            // Deactivate first: Destroy waits for the end of the frame, and nothing here may touch the
            // restored street furniture in the meantime.
            foreach (GameObject scar in scars) Remove(scar);
            scars.Clear();
            foreach (GameObject chunk in chunks) Remove(chunk);
            chunks.Clear();
            foreach (GameObject effect in transient) Remove(effect);
            transient.Clear();
            lastHitAt.Clear();
            for (int i = 0; i < windows.Count; i++)
            {
                Window window = windows[i];
                window.Broken = false;
                windows[i] = window;
            }
            brokenWindows = 0;
            windowsDirty = true;
            foreach (Loose loose in looseParts)
            {
                if (!loose.Fallen || loose.Part == null) continue;
                Rigidbody body = loose.Part.GetComponent<Rigidbody>();
                if (body != null) Destroy(body);
                Collider collider = loose.Part.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
                loose.Part.transform.SetParent(loose.Parent, false);
                loose.Part.transform.localPosition = loose.LocalPosition;
                loose.Part.transform.localRotation = loose.LocalRotation;
                loose.Part.layer = 0;
                loose.Fallen = false;
            }
            foreach (BreakableProp prop in props) if (prop != null) prop.Restore();
        }

        private static void Remove(GameObject spawned)
        {
            if (spawned == null) return;
            spawned.SetActive(false);
            Destroy(spawned);
        }

        private void OnExploded(Vector3 centre, float radius, bool byPlayer)
        {
            float reach = radius * 1.15f;
            foreach (DamageableFacade facade in facades)
            {
                if (facade == null) continue;
                Vector3 nearest = facade.Box.ClosestPoint(centre);
                float distance = Vector3.Distance(nearest, centre);
                if (distance > reach) continue;
                float severity = Mathf.Clamp01(1.15f - distance / reach);
                Vector3 normal = facade.FaceNormal(nearest, centre);
                // Aim the scar at chest-to-first-floor height so it reads from the street.
                nearest.y = Mathf.Clamp(nearest.y + UnityEngine.Random.Range(0.4f, 2.6f), 0.5f, facade.Box.bounds.max.y - 0.5f);
                DamageFacade(facade, nearest, normal, severity, true);
            }
            foreach (BreakableProp prop in props)
            {
                if (prop == null) continue;
                Vector3 offset = prop.transform.position - centre;
                if (offset.magnitude > reach) continue;
                prop.Blast(centre, reach);
            }
        }

        // ------------------------------------------------------------------ facades

        /// <summary>A vehicle or heavy debris has struck a building.</summary>
        public void DamageFacade(DamageableFacade facade, Vector3 point, Vector3 normal, float severity, bool blast = false)
        {
            // One scar per stretch of wall per moment, however many contacts the solver reports.
            long cell = ((long)Mathf.RoundToInt(point.x / 1.2f) * 73856093L) ^ ((long)Mathf.RoundToInt(point.y / 1.2f) * 19349663L) ^ ((long)Mathf.RoundToInt(point.z / 1.2f) * 83492791L);
            if (lastHitAt.TryGetValue(cell, out float last) && Time.time - last < 0.6f) return;
            lastHitAt[cell] = Time.time;
            EnsureAssets();

            float size = Mathf.Lerp(0.8f, 3.1f, severity) * UnityEngine.Random.Range(0.85f, 1.15f);
            var kit = new MeshKit();
            kit.Panel(Vector3.zero, Vector3.forward, Vector3.up, size, size);
            Material material = blast && UnityEngine.Random.value < 0.6f ? scorchMaterial : scarMaterials[UnityEngine.Random.Range(0, scarMaterials.Length)];
            GameObject scar = MeshKit.Spawn(transform, "Facade scar", kit.ToMesh("Facade scar"), material, false);
            scar.transform.SetPositionAndRotation(point + normal * (0.035f + scars.Count * 0.0006f),
                Quaternion.LookRotation(normal, Vector3.up) * Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f)));
            scar.GetComponent<MeshRenderer>().receiveShadows = true;
            if (scars.Count >= MaxScars)
            {
                if (scars[0] != null) Destroy(scars[0]);
                scars.RemoveAt(0);
            }
            scars.Add(scar);

            int pieces = Mathf.RoundToInt(Mathf.Lerp(2f, 11f, severity));
            SpawnChunks(point + normal * 0.25f, normal, pieces, severity, facade != null ? facade.Wall : null);
            Hit?.Invoke(new StructureHit { Kind = StructureKind.Masonry, Position = point, Normal = normal, Severity = severity });

            if (severity > 0.22f) BreakWindowsNear(point, Mathf.Lerp(1.6f, blast ? 9f : 5.5f, severity), blast ? 14 : 8);
            if (severity > 0.4f) KnockLoose(point, Mathf.Lerp(2.5f, blast ? 10f : 6f, severity), normal);
        }

        private void SpawnChunks(Vector3 origin, Vector3 normal, int count, float severity, Material wall)
        {
            for (int i = 0; i < count; i++)
            {
                if (chunks.Count >= MaxChunks)
                {
                    if (chunks[0] != null) Destroy(chunks[0]);
                    chunks.RemoveAt(0);
                }
                Mesh mesh = chunkMeshes[UnityEngine.Random.Range(0, chunkMeshes.Length)];
                GameObject chunk = MeshKit.Spawn(null, "Masonry", mesh, wall != null ? wall : scarMaterials[0], true);
                chunk.layer = DebrisLayer;
                float scale = UnityEngine.Random.Range(0.6f, 1.5f) * Mathf.Lerp(0.8f, 1.35f, severity);
                chunk.transform.localScale = Vector3.one * scale;
                chunk.transform.SetPositionAndRotation(origin + UnityEngine.Random.insideUnitSphere * 0.5f, UnityEngine.Random.rotation);
                BoxCollider box = chunk.AddComponent<BoxCollider>();
                box.size = mesh.bounds.size;
                Rigidbody body = chunk.AddComponent<Rigidbody>();
                body.mass = 9f * scale;
                body.linearDamping = 0.1f;
                body.angularDamping = 0.4f;
                Vector3 scatter = Vector3.ProjectOnPlane(UnityEngine.Random.insideUnitSphere, normal) * 3.2f;
                body.linearVelocity = normal * UnityEngine.Random.Range(1.5f, 4.5f + severity * 5f) + scatter + Vector3.up * UnityEngine.Random.Range(0.5f, 3.5f);
                body.angularVelocity = UnityEngine.Random.insideUnitSphere * 10f;
                chunks.Add(chunk);
            }
        }

        private void BreakWindowsNear(Vector3 point, float radius, int limit)
        {
            int broken = 0;
            float radiusSq = radius * radius;
            for (int i = 0; i < windows.Count && broken < limit; i++)
            {
                Window window = windows[i];
                if (window.Broken) continue;
                float distanceSq = (window.Centre - point).sqrMagnitude;
                if (distanceSq > radiusSq) continue;
                // Nearer panes always go; the edge of the radius is patchy.
                if (distanceSq > radiusSq * 0.3f && UnityEngine.Random.value < 0.4f) continue;
                window.Broken = true;
                windows[i] = window;
                broken++;
                brokenWindows++;
                windowsDirty = true;
                Hit?.Invoke(new StructureHit { Kind = StructureKind.Glass, Position = window.Centre, Normal = window.Normal, Severity = Mathf.Clamp01(window.Width * window.Height / 4f) });
            }
        }

        private void KnockLoose(Vector3 point, float radius, Vector3 normal)
        {
            foreach (Loose loose in looseParts)
            {
                if (loose.Fallen || loose.Part == null) continue;
                if ((loose.WorldPosition - point).sqrMagnitude > radius * radius) continue;
                loose.Fallen = true;
                GameObject part = loose.Part;
                part.transform.SetParent(null, true);
                part.layer = DebrisLayer;
                Renderer renderer = part.GetComponent<Renderer>();
                BoxCollider box = part.AddComponent<BoxCollider>();
                MeshFilter filter = part.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    box.center = filter.sharedMesh.bounds.center;
                    box.size = Vector3.Max(filter.sharedMesh.bounds.size, Vector3.one * 0.06f);
                }
                Rigidbody body = part.AddComponent<Rigidbody>();
                body.mass = 35f;
                body.angularDamping = 0.3f;
                body.linearVelocity = normal * UnityEngine.Random.Range(0.8f, 2.4f) + UnityEngine.Random.insideUnitSphere;
                body.angularVelocity = UnityEngine.Random.insideUnitSphere * 2.5f;
                Hit?.Invoke(new StructureHit { Kind = StructureKind.Loose, Position = loose.WorldPosition, Normal = normal, Severity = 0.6f });
                if (renderer != null) renderer.shadowCastingMode = ShadowCastingMode.On;
            }
        }

        /// <summary>Street furniture reports its own breakage so sound and particles can follow.</summary>
        public void ReportProp(BreakableProp prop, StructureKind kind, Vector3 position, float severity)
        {
            Hit?.Invoke(new StructureHit { Kind = kind, Position = position, Normal = Vector3.up, Severity = severity });
        }

        /// <summary>An effect that should be removed when the turn resets (a hydrant geyser, say).</summary>
        public void Track(GameObject effect)
        {
            transient.Add(effect);
        }

        // All blown-out panes share one mesh, rebuilt only when the set changes.
        private void LateUpdate()
        {
            if (!windowsDirty) return;
            windowsDirty = false;
            EnsureAssets();
            var kit = new MeshKit();
            foreach (Window window in windows)
            {
                if (!window.Broken) continue;
                kit.Panel(window.Centre + window.Normal * 0.012f, window.Normal, Vector3.up, window.Width, window.Height);
            }
            if (brokenWindowObject == null)
            {
                brokenWindowMesh = new Mesh { name = "Blown windows" };
                brokenWindowObject = MeshKit.Spawn(transform, "Blown windows", brokenWindowMesh, brokenWindowMaterial, false);
            }
            Mesh built = kit.ToMesh("Blown windows");
            brokenWindowMesh.Clear();
            if (built.vertexCount > 0)
            {
                brokenWindowMesh.SetVertices(built.vertices);
                brokenWindowMesh.SetNormals(built.normals);
                brokenWindowMesh.SetUVs(0, built.uv);
                brokenWindowMesh.SetTriangles(built.triangles, 0);
                brokenWindowMesh.RecalculateBounds();
                brokenWindowMesh.RecalculateTangents();
            }
            ReplayMeshRevision.Changed(brokenWindowObject.GetComponent<MeshFilter>());
            Destroy(built);
        }

        private void EnsureAssets()
        {
            if (scarMaterials != null) return;
            scarMaterials = new Material[3];
            for (int i = 0; i < scarMaterials.Length; i++)
            {
                ProceduralTextures.ImpactScar(31 + i * 17, out Texture2D albedo, out Texture2D normal);
                scarMaterials[i] = CrashoutArt.DecalMaterial("Facade scar " + i, albedo, Color.white, normal);
            }
            ProceduralTextures.ImpactScar(97, out Texture2D sootAlbedo, out Texture2D sootNormal, true);
            scorchMaterial = CrashoutArt.DecalMaterial("Blast scar", sootAlbedo, Color.white, sootNormal);
            brokenWindowMaterial = CrashoutArt.Lit("Blown window", Color.white, 0f, 0.3f);
            brokenWindowMaterial.SetTexture("_BaseMap", ProceduralTextures.BrokenWindow());

            chunkMeshes = new Mesh[4];
            for (int i = 0; i < chunkMeshes.Length; i++)
            {
                var kit = new MeshKit();
                Vector3 size = new Vector3(0.22f + i * 0.05f, 0.1f + (i % 2) * 0.07f, 0.14f + (3 - i) * 0.05f);
                kit.Box(Vector3.zero, size, Quaternion.identity);
                if (i % 2 == 1) kit.Box(new Vector3(size.x * 0.3f, size.y * 0.5f, 0f), size * 0.55f, Quaternion.Euler(12f, 30f, 8f));
                chunkMeshes[i] = kit.ToMesh("Masonry chunk " + i);
            }
        }
    }

    /// <summary>A building face that scars when something hits it hard.</summary>
    public sealed class DamageableFacade : MonoBehaviour
    {
        public BoxCollider Box { get; private set; }
        public Material Wall { get; private set; }

        public void Configure(BoxCollider box, Material wall)
        {
            Box = box;
            Wall = wall;
            if (Destruction.Instance != null) Destruction.Instance.RegisterFacade(this);
        }

        /// <summary>The outward normal of the box face nearest <paramref name="point"/>, facing <paramref name="toward"/>.</summary>
        public Vector3 FaceNormal(Vector3 point, Vector3 toward)
        {
            Vector3 local = transform.InverseTransformPoint(point) - Box.center;
            Vector3 half = Box.size * 0.5f;
            float x = Mathf.Abs(local.x) / Mathf.Max(0.01f, half.x), z = Mathf.Abs(local.z) / Mathf.Max(0.01f, half.z);
            Vector3 normal = x >= z ? new Vector3(Mathf.Sign(local.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(local.z));
            Vector3 world = transform.TransformDirection(normal);
            Vector3 outward = Vector3.ProjectOnPlane(toward - point, Vector3.up);
            if (outward.sqrMagnitude > 0.04f && Vector3.Dot(world, outward) < -0.2f)
            {
                // The source is on another side (a corner hit): face it.
                Vector3 other = x >= z ? new Vector3(0f, 0f, Mathf.Sign(local.z)) : new Vector3(Mathf.Sign(local.x), 0f, 0f);
                world = transform.TransformDirection(other);
            }
            return world;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (Destruction.Instance == null || Box == null || collision.contactCount == 0) return;
            Rigidbody other = collision.rigidbody;
            if (other == null || other.mass < 300f) return;
            float speed = collision.relativeVelocity.magnitude;
            if (speed < 3.5f) return;
            ContactPoint contact = collision.GetContact(0);
            // Heavier and faster is worse: a hatchback at 20 mph scuffs, a tanker at 50 takes a bite out.
            float energy = speed * Mathf.Sqrt(other.mass / 1400f);
            float severity = Mathf.Clamp01((energy - 3.5f) / 22f);
            Vector3 normal = FaceNormal(contact.point, other.worldCenterOfMass);
            Vector3 point = Box.ClosestPoint(contact.point);
            point.y = Mathf.Max(point.y, 0.35f);
            Destruction.Instance.DamageFacade(this, point, normal, severity);
        }
    }

    /// <summary>
    /// Street furniture with real mass. Fixed items are bolted to the world by a joint that snaps under
    /// a hard enough hit, so a car ploughs through a lamp post but is stopped by a tree; loose items
    /// (cones, bins) simply rest where they are until something sends them flying.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class BreakableProp : MonoBehaviour
    {
        private Rigidbody body;
        private FixedJoint joint;
        private Vector3 startPosition;
        private Quaternion startRotation;
        private float breakForce;
        private StructureKind kind;
        private GameObject[] dieOnBreak;
        private bool disturbed;
        private float settleUntil;
        private bool refix;

        public bool Broken { get; private set; }
        public StructureKind Kind => kind;

        /// <param name="holdForce">Newtons the fixing withstands; 0 for an item that just rests on the ground.</param>
        /// <param name="lightsOut">Children switched off when the prop breaks (a lamp's glowing lens).</param>
        public void Configure(StructureKind structureKind, float mass, float holdForce, Vector3 centreOfMass, params GameObject[] lightsOut)
        {
            kind = structureKind;
            breakForce = holdForce;
            dieOnBreak = lightsOut;
            body = GetComponent<Rigidbody>();
            body.mass = mass;
            body.centerOfMass = centreOfMass;
            body.linearDamping = 0.05f;
            body.angularDamping = 0.35f;
            body.maxAngularVelocity = 12f;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            startPosition = transform.position;
            startRotation = transform.rotation;
            SetLayer(gameObject, Destruction.PropLayer);
            Fix();
            body.Sleep();
            if (Destruction.Instance != null) Destruction.Instance.RegisterProp(this);
        }

        private static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayer(child.gameObject, layer);
        }

        private void Fix()
        {
            if (breakForce <= 0f || joint != null) return;
            joint = gameObject.AddComponent<FixedJoint>();
            joint.breakForce = breakForce;
            joint.breakTorque = breakForce * 2.2f;
            joint.enablePreprocessing = false;
        }

        private void OnJointBreak(float force)
        {
            if (Time.time < settleUntil)
            {
                // Something was still lying on the spot it was just put back on: bolt it down again.
                joint = null;
                refix = true;
                return;
            }
            Snap(Mathf.Clamp01(force / (breakForce * 3f)));
        }

        private void FixedUpdate()
        {
            if (!refix || joint != null) return;
            refix = false;
            Place();
            Fix();
            body.Sleep();
        }

        private void Place()
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(startPosition, startRotation);
            body.position = startPosition;
            body.rotation = startRotation;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (breakForce > 0f || disturbed || collision.rigidbody == null || Time.time < settleUntil) return;
            float speed = collision.relativeVelocity.magnitude;
            if (speed < 2f) return;
            disturbed = true;
            Broken = true;
            if (Destruction.Instance != null) Destruction.Instance.ReportProp(this, kind, transform.position, Mathf.Clamp01(speed / 20f));
        }

        private void Snap(float severity)
        {
            if (Broken) return;
            Broken = true;
            joint = null;
            if (dieOnBreak != null) foreach (GameObject light in dieOnBreak) if (light != null) light.SetActive(false);
            if (Destruction.Instance != null) Destruction.Instance.ReportProp(this, kind, startPosition, Mathf.Max(0.35f, severity));
        }

        /// <summary>An explosion tears the prop off its fixing and throws it.</summary>
        public void Blast(Vector3 centre, float radius)
        {
            if (joint != null)
            {
                Destroy(joint);
                Snap(0.8f);
            }
            else if (!Broken && breakForce <= 0f)
            {
                disturbed = true;
                Broken = true;
            }
            body.WakeUp();
            body.AddExplosionForce(body.mass * 11f, centre, radius, 0.6f, ForceMode.Impulse);
        }

        public void Restore()
        {
            if (body == null) return;
            if (joint != null)
            {
                // Still bolted down; nothing to do unless it was nudged.
                if ((transform.position - startPosition).sqrMagnitude < 1e-4f) return;
                DestroyImmediate(joint);
                joint = null;
            }
            Broken = false;
            disturbed = false;
            refix = false;
            settleUntil = Time.time + 0.4f;
            Place();
            if (dieOnBreak != null) foreach (GameObject light in dieOnBreak) if (light != null) light.SetActive(true);
            Fix();
            body.Sleep();
        }
    }
}

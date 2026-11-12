using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Crashout.Presentation
{
    /// <summary>
    /// Render-only history in canonical metres. No colliders, scripts, scoring or recorded camera.
    /// Static geometry and unchanged meshes/materials are shared across keys; detached and destroyed
    /// objects retain their recorded appearance. Particle samples are independent of the viewer.
    /// </summary>
    internal sealed class ReplayWorld
    {
        private struct Pose
        {
            public Vector3 Position, Scale;
            public Quaternion Rotation;
            public static Pose Read(Transform t) => new Pose { Position = t.position, Rotation = t.rotation, Scale = t.lossyScale };
            public void Apply(Transform t, Pose next, float blend)
            {
                t.SetPositionAndRotation(Vector3.Lerp(Position, next.Position, blend), Quaternion.Slerp(Rotation, next.Rotation, blend));
                t.localScale = Vector3.Lerp(Scale, next.Scale, blend);
            }
            public bool Same(Pose other) => Position == other.Position && Rotation == other.Rotation && Scale == other.Scale;
        }

        private struct View
        {
            public Pose Pose;
            public bool Visible;
            public Mesh Mesh;
            public Material[] Materials;
        }

        private sealed class Geometry
        {
            public Renderer Source;
            public string Name;
            public int Layer;
            public ShadowCastingMode Shadows;
            public bool ReceiveShadows;
            public MeshFilter Filter;
            public readonly ReplayTimeline<View> Track = new ReplayTimeline<View>();
            public MeshFilter ProxyFilter;
            public MeshRenderer Proxy;
            public float SampledAt;
            public bool Ended, AnimatedMaterials;
            public int TrailPoints = -1;
        }

        private struct Particle
        {
            public Vector3 Position, Velocity, Size, Rotation;
            public Color32 Color;
            public uint Seed;
        }

        private sealed class Particles
        {
            public ParticleSystem Source;
            public ParticleSystemRenderer Renderer;
            public Material Material;
            public ParticleSystemRenderMode RenderMode;
            public ParticleSystemRenderSpace Alignment;
            public float VelocityScale, LengthScale;
            public Mesh Mesh;
            public readonly ReplayTimeline<Particle[]> Track = new ReplayTimeline<Particle[]>();
            public ParticleSystem Proxy;
            public ParticleSystem.Particle[] Buffer;
            public bool Ended;
            public Particle[] IndexedSample;
            public readonly Dictionary<uint, int> NextBySeed = new Dictionary<uint, int>();
        }

        private struct LampView
        {
            public Pose Pose;
            public float Intensity, Range;
            public Color Color;
            public bool Enabled;
        }

        private sealed class Lamp
        {
            public Light Source, Proxy;
            public readonly ReplayTimeline<LampView> Track = new ReplayTimeline<LampView>();
        }

        private readonly Dictionary<int, Geometry> geometry = new Dictionary<int, Geometry>();
        private readonly Dictionary<int, Particles> particles = new Dictionary<int, Particles>();
        private readonly Dictionary<int, Lamp> lamps = new Dictionary<int, Lamp>();
        private readonly Dictionary<(int, int), Mesh> meshes = new Dictionary<(int, int), Mesh>();
        private readonly Dictionary<(int, Color, Color), Material> materials = new Dictionary<(int, Color, Color), Material>();
        private readonly Dictionary<Renderer, bool> hidden = new Dictionary<Renderer, bool>();
        private readonly Dictionary<Light, bool> hiddenLights = new Dictionary<Light, bool>();
        private readonly List<Mesh> bakedMeshes = new List<Mesh>();
        private readonly List<Vector3> meshVertices = new List<Vector3>();
        private GameObject root;
        private long particleBytes;
        private const long ParticleBudget = 48L * 1024 * 1024;
        private float particleInterval = TurnReplay.SampleInterval;
        private float nextParticleAt;

        private Material CopyMaterial(Material source)
        {
            if (source == null) return null;
            Color colour = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor")
                : source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
            Color emission = source.HasProperty("_EmissionColor") ? source.GetColor("_EmissionColor") : Color.black;
            var key = (source.GetInstanceID(), colour, emission);
            if (!materials.TryGetValue(key, out Material copy))
            {
                copy = new Material(source) { name = source.name + " (replay)" };
                materials.Add(key, copy);
            }
            return copy;
        }

        private Mesh CopyMesh(MeshFilter filter)
        {
            Mesh source = filter.sharedMesh;
            if (source == null) return null;
            var revision = filter.GetComponent<ReplayMeshRevision>();
            var key = (source.GetInstanceID(), revision != null ? revision.Version : 0);
            if (!meshes.TryGetValue(key, out Mesh copy))
            {
                copy = Object.Instantiate(source);
                copy.name = source.name + " (replay)";
                meshes.Add(key, copy);
            }
            return copy;
        }

        public void Capture(float time, bool finalFrame = false)
        {
            // Includes objects created by blasts and destruction, even if they are not under the runtime root.
            foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (renderer.gameObject.layer == 31 || !renderer.gameObject.scene.IsValid() || (renderer.transform.root.hideFlags & HideFlags.DontSave) != 0) continue;
                int id = renderer.GetInstanceID();
                if (renderer is ParticleSystemRenderer particleRenderer)
                {
                    if (!particles.ContainsKey(id))
                    {
                        var system = renderer.GetComponent<ParticleSystem>();
                        if (system != null) particles.Add(id, new Particles
                        {
                            Source = system, Renderer = particleRenderer, Material = CopyMaterial(renderer.sharedMaterial),
                            RenderMode = particleRenderer.renderMode, Alignment = particleRenderer.alignment,
                            VelocityScale = particleRenderer.velocityScale, LengthScale = particleRenderer.lengthScale, Mesh = particleRenderer.mesh
                        });
                    }
                    continue;
                }
                if (geometry.ContainsKey(id)) continue;
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null && !(renderer is TrailRenderer)) continue;
                geometry.Add(id, new Geometry
                {
                    Source = renderer, Name = renderer.name, Layer = renderer.gameObject.layer,
                    Shadows = renderer.shadowCastingMode, ReceiveShadows = renderer.receiveShadows, Filter = filter, SampledAt = time,
                    AnimatedMaterials = renderer.GetComponentInParent<Rigidbody>() != null || renderer is TrailRenderer
                });
            }
            foreach (Geometry item in geometry.Values) CaptureGeometry(item, time);
            if (time >= nextParticleAt || finalFrame)
            {
                foreach (Particles item in particles.Values) CaptureParticles(item, time);
                nextParticleAt = time + particleInterval;
                if (particleBytes > ParticleBudget)
                {
                    particleBytes = 0;
                    foreach (Particles item in particles.Values)
                    {
                        item.Track.Compact((a, b) => (a.Length == 0) != (b.Length == 0));
                        foreach (var key in item.Track.Keys) particleBytes += key.Value.Length * 56L;
                    }
                    particleInterval *= 2f;
                }
            }
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                int id = light.GetInstanceID();
                if (!lamps.ContainsKey(id)) lamps.Add(id, new Lamp { Source = light });
            }
            foreach (Lamp lamp in lamps.Values)
            {
                Light light = lamp.Source;
                lamp.Track.Add(time, light == null ? default : new LampView
                {
                    Pose = Pose.Read(light.transform), Enabled = light.isActiveAndEnabled,
                    Color = light.color, Intensity = light.intensity, Range = light.range
                });
            }
        }

        private void CaptureGeometry(Geometry item, float time)
        {
            if (item.Ended) return;
            Renderer source = item.Source;
            bool visible = source != null && source.enabled && source.gameObject.activeInHierarchy && !source.forceRenderingOff;
            View view = item.Track.Count > 0 ? item.Track.Last.Value : default;
            view.Visible = visible;
            if (source != null)
            {
                view.Pose = Pose.Read(source.transform);
                if (item.Filter != null) view.Mesh = CopyMesh(item.Filter);
                else if (source is TrailRenderer trail && trail.positionCount > 1 && (trail.emitting || trail.positionCount != item.TrailPoints) && Camera.main != null)
                {
                    // Skid trails use TransformZ alignment: their baked mesh remains valid from any AR angle.
                    Mesh mesh = new Mesh { name = "Replay skid" };
                    trail.BakeMesh(mesh, Camera.main, false);
                    meshVertices.Clear();
                    mesh.GetVertices(meshVertices);
                    for (int i = 0; i < meshVertices.Count; i++) meshVertices[i] = trail.transform.TransformPoint(meshVertices[i]);
                    mesh.SetVertices(meshVertices);
                    mesh.RecalculateNormals();
                    mesh.RecalculateBounds();
                    view.Mesh = mesh;
                    bakedMeshes.Add(mesh);
                    item.TrailPoints = trail.positionCount;
                    view.Pose = new Pose { Rotation = Quaternion.identity, Scale = Vector3.one };
                }
                if (item.Track.Count == 0 || item.AnimatedMaterials)
                {
                    Material[] originals = source.sharedMaterials;
                    Material[] copies = new Material[originals.Length];
                    for (int i = 0; i < originals.Length; i++) copies[i] = CopyMaterial(originals[i]);
                    view.Materials = copies;
                }
                if (source is TrailRenderer && item.Filter == null)
                {
                    view.Pose = new Pose { Rotation = Quaternion.identity, Scale = Vector3.one };
                    view.Visible &= ((TrailRenderer)source).positionCount > 1;
                }
            }
            bool changed = item.Track.Count == 0 || !Same(view, item.Track.Last.Value);
            if (changed)
            {
                // Hold a stationary object still until the sample immediately before its first motion.
                if (item.Track.Count > 0 && item.SampledAt > item.Track.Last.Time) item.Track.Add(item.SampledAt, item.Track.Last.Value);
                item.Track.Add(time, view);
            }
            item.SampledAt = time;
            item.Ended = source == null;
        }

        private static bool Same(View a, View b)
        {
            if (a.Visible != b.Visible || a.Mesh != b.Mesh || !a.Pose.Same(b.Pose) || a.Materials.Length != b.Materials.Length) return false;
            for (int i = 0; i < a.Materials.Length; i++) if (a.Materials[i] != b.Materials[i]) return false;
            return true;
        }

        private void CaptureParticles(Particles item, float time)
        {
            if (item.Ended) return;
            ParticleSystem system = item.Source;
            int count = system != null && system.gameObject.activeInHierarchy && item.Renderer.enabled ? system.particleCount : 0;
            if (count == 0)
            {
                if (item.Track.Count > 0 && item.Track.Last.Value.Length > 0) item.Track.Add(time, System.Array.Empty<Particle>());
                item.Ended = system == null;
                return;
            }
            if (item.Buffer == null || item.Buffer.Length < count) item.Buffer = new ParticleSystem.Particle[count];
            count = system.GetParticles(item.Buffer);
            var sample = new Particle[count];
            var main = system.main;
            Transform space = main.simulationSpace == ParticleSystemSimulationSpace.Local ? system.transform
                : main.simulationSpace == ParticleSystemSimulationSpace.Custom ? main.customSimulationSpace : null;
            for (int i = 0; i < count; i++)
            {
                ParticleSystem.Particle p = item.Buffer[i];
                sample[i] = new Particle
                {
                    Position = space != null ? space.TransformPoint(p.position) : p.position,
                    Velocity = space != null ? space.TransformVector(p.velocity) : p.velocity,
                    Size = p.GetCurrentSize3D(system), Rotation = p.rotation3D, Color = p.GetCurrentColor(system), Seed = p.randomSeed
                };
            }
            item.Track.Add(time, sample);
            particleBytes += sample.Length * 56L;
        }

        public void Show()
        {
            root = new GameObject("Turn replay (render only)") { hideFlags = HideFlags.DontSave };
            foreach (Geometry item in geometry.Values)
            {
                var go = new GameObject("Replay: " + item.Name);
                go.transform.SetParent(root.transform, false);
                go.layer = item.Layer;
                item.ProxyFilter = go.AddComponent<MeshFilter>();
                item.Proxy = go.AddComponent<MeshRenderer>();
                item.Proxy.shadowCastingMode = item.Shadows;
                item.Proxy.receiveShadows = item.ReceiveShadows;
            }
            foreach (Particles item in particles.Values)
            {
                var go = new GameObject("Replay particles");
                go.transform.SetParent(root.transform, false);
                item.Proxy = go.AddComponent<ParticleSystem>();
                item.Proxy.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = item.Proxy.main;
                main.playOnAwake = false;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startSize3D = true;
                main.startRotation3D = true;
                var emission = item.Proxy.emission;
                emission.enabled = false;
                var renderer = go.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = item.Material;
                renderer.renderMode = item.RenderMode;
                renderer.alignment = item.Alignment;
                renderer.velocityScale = item.VelocityScale;
                renderer.lengthScale = item.LengthScale;
                if (item.Mesh != null) renderer.mesh = item.Mesh;
            }
            foreach (Lamp lamp in lamps.Values)
            {
                var go = new GameObject("Replay light");
                go.transform.SetParent(root.transform, false);
                lamp.Proxy = go.AddComponent<Light>();
                if (lamp.Source != null)
                {
                    lamp.Proxy.type = lamp.Source.type;
                    lamp.Proxy.shadows = lamp.Source.shadows;
                    lamp.Proxy.spotAngle = lamp.Source.spotAngle;
                    lamp.Proxy.cullingMask = lamp.Source.cullingMask;
                }
            }
            HideLiveWorld();
        }

        public void HideLiveWorld()
        {
            foreach (Renderer source in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (source.gameObject.layer == 31 || source.transform.IsChildOf(root.transform)) continue;
                if (!hidden.ContainsKey(source)) hidden.Add(source, source.forceRenderingOff);
                source.forceRenderingOff = true;
            }
            foreach (Light source in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (source.transform.IsChildOf(root.transform)) continue;
                if (!hiddenLights.ContainsKey(source)) hiddenLights.Add(source, source.enabled);
                source.enabled = false;
            }
        }

        public void Seek(float time)
        {
            foreach (Geometry item in geometry.Values)
            {
                bool found = item.Track.Sample(time, out View a, out View b, out float blend);
                item.Proxy.enabled = found && a.Visible;
                if (!found) continue;
                // Spawn/despawn and visibility are discrete. Never interpolate towards a future spawn.
                bool continuous = b.Visible == a.Visible && (b.Pose.Position - a.Pose.Position).sqrMagnitude < 225f;
                a.Pose.Apply(item.Proxy.transform, continuous ? b.Pose : a.Pose, blend);
                item.ProxyFilter.sharedMesh = a.Mesh;
                item.Proxy.sharedMaterials = a.Materials;
            }
            foreach (Particles item in particles.Values)
            {
                bool found = item.Track.Sample(time, out Particle[] sample, out Particle[] next, out float blend);
                if (found && item.IndexedSample != next)
                {
                    item.IndexedSample = next;
                    item.NextBySeed.Clear();
                    for (int i = 0; i < next.Length; i++) item.NextBySeed[next[i].Seed] = i;
                }
                int count = found ? sample.Length : 0;
                if (item.Buffer == null || item.Buffer.Length < count) item.Buffer = new ParticleSystem.Particle[count];
                var main = item.Proxy.main;
                main.maxParticles = Mathf.Max(1, count);
                for (int i = 0; i < count; i++)
                {
                    Particle p = sample[i];
                    if (item.NextBySeed.TryGetValue(p.Seed, out int index))
                    {
                        Particle later = next[index];
                        p.Position = Vector3.Lerp(p.Position, later.Position, blend);
                        p.Velocity = Vector3.Lerp(p.Velocity, later.Velocity, blend);
                        p.Size = Vector3.Lerp(p.Size, later.Size, blend);
                        p.Rotation = new Vector3(Mathf.LerpAngle(p.Rotation.x, later.Rotation.x, blend),
                            Mathf.LerpAngle(p.Rotation.y, later.Rotation.y, blend), Mathf.LerpAngle(p.Rotation.z, later.Rotation.z, blend));
                        p.Color = Color32.Lerp(p.Color, later.Color, blend);
                    }
                    item.Buffer[i] = new ParticleSystem.Particle
                    {
                        position = p.Position, velocity = p.Velocity, startSize3D = p.Size,
                        rotation3D = p.Rotation, startColor = p.Color, startLifetime = 10000f, remainingLifetime = 10000f
                    };
                }
                item.Proxy.SetParticles(item.Buffer ?? System.Array.Empty<ParticleSystem.Particle>(), count);
                item.Proxy.Pause();
            }
            foreach (Lamp lamp in lamps.Values)
            {
                bool found = lamp.Track.Sample(time, out LampView a, out LampView b, out float blend);
                lamp.Proxy.enabled = found && a.Enabled;
                if (!found) continue;
                a.Pose.Apply(lamp.Proxy.transform, b.Pose, blend);
                lamp.Proxy.color = a.Color;
                lamp.Proxy.intensity = a.Intensity;
                lamp.Proxy.range = a.Range;
            }
        }

        public void Hide()
        {
            foreach (var pair in hidden) if (pair.Key != null) pair.Key.forceRenderingOff = pair.Value;
            hidden.Clear();
            foreach (var pair in hiddenLights) if (pair.Key != null) pair.Key.enabled = pair.Value;
            hiddenLights.Clear();
            if (root != null) { root.SetActive(false); Object.Destroy(root); }
            root = null;
        }

        public void Dispose()
        {
            Hide();
            foreach (Mesh mesh in meshes.Values) Object.Destroy(mesh);
            foreach (Mesh mesh in bakedMeshes) Object.Destroy(mesh);
            foreach (Material material in materials.Values) Object.Destroy(material);
        }
    }
}

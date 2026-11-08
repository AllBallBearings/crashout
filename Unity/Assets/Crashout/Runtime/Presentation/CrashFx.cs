using System.Collections.Generic;
using Crashout.Simulation;
using Crashout.Vehicle;
using UnityEngine;

namespace Crashout.Presentation
{
    /// <summary>
    /// Presentation-only impact feedback: sparks, a flash, smoke and camera shake.
    /// Driven entirely by authoritative score events so it never touches simulation.
    /// </summary>
    public sealed class CrashFx : MonoBehaviour
    {
        private CrashoutGameSession session;
        private CrashoutCameraRig cameraRig;
        private Material particleMaterial;
        private Texture2D softDot;
        private readonly List<GameObject> debris = new List<GameObject>();
        private readonly List<GameObject> wreckEffects = new List<GameObject>();
        private readonly HashSet<uint> smoking = new HashSet<uint>();
        private const int MaxDebris = 70;

        public void Configure(CrashoutGameSession gameSession, CrashoutCameraRig rig)
        {
            Unsubscribe();
            session = gameSession;
            cameraRig = rig;
            session.ScoreChanged += OnScore;
            session.Exploded += OnExploded;
            session.PhaseChanged += OnPhase;
            Physics.IgnoreLayerCollision(9, 8, true);
        }

        private void Unsubscribe()
        {
            if (session == null) return;
            session.ScoreChanged -= OnScore;
            session.Exploded -= OnExploded;
            session.PhaseChanged -= OnPhase;
        }

        private void OnPhase(GamePhase phase)
        {
            if (phase != GamePhase.Driving) return;
            foreach (GameObject piece in debris) if (piece != null) Destroy(piece);
            debris.Clear();
            foreach (GameObject effect in wreckEffects) if (effect != null) Destroy(effect);
            wreckEffects.Clear();
            smoking.Clear();
        }

        private void OnExploded(Vector3 centre, float radius, bool byPlayer)
        {
            EnsureMaterial();
            Spawn("Blast flash", centre, 1, 0.16f, 0.22f, 0f, 0f, radius * 0.95f, new Color(1f, 0.95f, 0.7f, 0.95f), new Color(1f, 0.6f, 0.15f, 0.9f), 0f);
            Spawn("Fireball", centre, 18, 0.5f, 1.1f, 1f, radius * 0.9f, 1.9f, new Color(1f, 0.7f, 0.2f, 0.95f), new Color(0.9f, 0.2f, 0.05f, 0.9f), -0.25f);
            Spawn("Blast sparks", centre, 48, 0.6f, 1.4f, 5f, radius * 1.6f, 0.16f, new Color(1f, 0.95f, 0.5f, 1f), new Color(1f, 0.45f, 0.1f, 1f), 2f);
            Spawn("Blast smoke", centre + Vector3.up * 0.5f, 16, 1.6f, 3f, 0.5f, 3f, 2.6f, new Color(0.25f, 0.24f, 0.23f, 0.7f), new Color(0.1f, 0.1f, 0.1f, 0.6f), -0.1f);
            if (cameraRig != null) cameraRig.AddShake(Mathf.Clamp01(0.5f + radius / 30f));
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (particleMaterial != null) Destroy(particleMaterial);
            if (softDot != null) Destroy(softDot);
        }

        private void OnScore(ScoreEvent scoreEvent)
        {
            if (scoreEvent.Amount <= 0) return;
            if (!session.TryGetVehiclePosition(scoreEvent.EntityId, out Vector3 vehiclePosition)) return;
            Vector3? position = vehiclePosition;

            EnsureMaterial();
            float intensity = Mathf.Clamp(scoreEvent.RelativeSpeed / 10f, 0.5f, 1.8f);
            Vector3 origin = position.Value + Vector3.up * 0.35f;
            Spawn("Impact flash", origin, 1, 0.1f, 0.14f, 0f, 0f, 3.2f * intensity, new Color(1f, 0.9f, 0.6f, 0.95f), new Color(1f, 0.6f, 0.2f, 0.9f), 0f);
            Spawn("Impact sparks", origin, Mathf.RoundToInt(28 * intensity), 0.35f, 0.8f, 4f, 11f * intensity, 0.13f, new Color(1f, 0.95f, 0.5f, 1f), new Color(1f, 0.45f, 0.1f, 1f), 1.6f);
            Spawn("Impact smoke", origin + Vector3.up * 0.2f, Mathf.RoundToInt(9 * intensity), 1.2f, 2.2f, 0.4f, 1.8f, 1.5f, new Color(0.42f, 0.4f, 0.38f, 0.55f), new Color(0.2f, 0.2f, 0.2f, 0.45f), -0.08f);
            if (cameraRig != null) cameraRig.AddShake(Mathf.Clamp01(scoreEvent.RelativeSpeed / 14f));
            SpawnDebris(scoreEvent, origin);
            AttachWreckSmoke(scoreEvent.EntityId);
        }

        private void SpawnDebris(ScoreEvent scoreEvent, Vector3 origin)
        {
            Material paint = null;
            DamageableVehicle owner = FindVehicle(scoreEvent.EntityId);
            if (owner != null)
            {
                MeshRenderer body = owner.GetComponentInChildren<MeshRenderer>();
                if (body != null) paint = body.sharedMaterial;
            }
            int count = Mathf.Clamp(Mathf.RoundToInt(scoreEvent.Amount / 900f), 1, 5);
            for (int i = 0; i < count && debris.Count < MaxDebris; i++)
            {
                GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                piece.name = "Debris";
                piece.layer = 9;
                piece.transform.position = origin + Random.insideUnitSphere * 0.5f;
                piece.transform.rotation = Random.rotation;
                piece.transform.localScale = new Vector3(Random.Range(0.12f, 0.5f), Random.Range(0.06f, 0.18f), Random.Range(0.12f, 0.5f));
                if (paint != null) piece.GetComponent<MeshRenderer>().sharedMaterial = paint;
                Rigidbody rb = piece.AddComponent<Rigidbody>();
                rb.mass = 12f;
                rb.linearVelocity = Random.insideUnitSphere * 5f * Mathf.Clamp(scoreEvent.RelativeSpeed / 8f, 0.6f, 2f) + Vector3.up * 3f;
                rb.angularVelocity = Random.insideUnitSphere * 12f;
                debris.Add(piece);
            }
        }

        private void AttachWreckSmoke(uint entityId)
        {
            DamageableVehicle owner = FindVehicle(entityId);
            if (owner == null || owner.IsPlayerVehicle || !smoking.Add(entityId)) return;
            bool burning = Crashout.Vehicle.VehicleSpec.For(owner.Class).Explosive;

            GameObject effect = new GameObject("Wreck smoke");
            effect.transform.SetParent(owner.transform, false);
            effect.transform.localPosition = new Vector3(0f, 0.5f, 0.6f);
            ParticleSystem system = effect.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            main.startColor = burning
                ? new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.1f, 0.9f), new Color(0.3f, 0.28f, 0.27f, 0.8f))
                : new ParticleSystem.MinMaxGradient(new Color(0.4f, 0.4f, 0.4f, 0.55f), new Color(0.15f, 0.15f, 0.15f, 0.5f));
            main.gravityModifier = -0.12f;
            main.maxParticles = 60;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = burning ? 22f : 10f;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.35f;
            ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
            colour.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            colour.color = fade;
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.4f, 1f, 1.6f));
            effect.GetComponent<ParticleSystemRenderer>().sharedMaterial = particleMaterial;
            wreckEffects.Add(effect);
        }

        private DamageableVehicle FindVehicle(uint entityId)
        {
            IReadOnlyList<DamageableVehicle> all = session.Vehicles;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && all[i].EntityId == entityId) return all[i];
            }
            return null;
        }

        private void Spawn(string label, Vector3 position, int count, float lifeMin, float lifeMax, float speedMin, float speedMax, float size,
            Color colourA, Color colourB, float gravity)
        {
            GameObject effect = new GameObject(label);
            effect.transform.position = position;
            ParticleSystem system = effect.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.loop = false;
            main.duration = 0.6f;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, Mathf.Max(speedMin, speedMax));
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.startColor = new ParticleSystem.MinMaxGradient(colourA, colourB);
            main.gravityModifier = gravity;
            main.maxParticles = 64;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Max(1, count)) });

            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;

            ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
            colour.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) });
            colour.color = fade;

            ParticleSystem.SizeOverLifetimeModule sizeOverLife = system.sizeOverLifetime;
            sizeOverLife.enabled = true;
            bool smoke = gravity < 0f;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, smoke
                ? AnimationCurve.EaseInOut(0f, 0.4f, 1f, 1.4f)
                : AnimationCurve.EaseInOut(0f, 1f, 1f, 0.1f));

            ParticleSystemRenderer particleRenderer = effect.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = particleMaterial;
            particleRenderer.sortingFudge = smoke ? 2f : -1f;

            system.Play();
            Destroy(effect, 3.5f);
        }

        private void EnsureMaterial()
        {
            if (particleMaterial != null) return;
            const int size = 32;
            softDot = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(size * 0.5f - 0.5f, size * 0.5f - 0.5f)) / (size * 0.5f);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);
                    softDot.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            softDot.Apply();
            Shader shader = Shader.Find("Sprites/Default");
            particleMaterial = new Material(shader) { name = "Impact particle", mainTexture = softDot };
        }
    }
}

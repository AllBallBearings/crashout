using System.Collections.Generic;
using Crashout.Simulation;
using Crashout.Vehicle;
using UnityEngine;
using UnityEngine.Rendering;

namespace Crashout.Presentation
{
    /// <summary>
    /// Presentation-only crash and driving effects: HDR sparks, impact flashes with real light,
    /// glass and body shards that bounce off the world, billowing smoke, fireballs with shockwaves,
    /// burning wrecks, tyre smoke, skid marks, boost flames and landing dust.
    /// Driven entirely by authoritative session events and read-only vehicle state.
    /// </summary>
    public sealed class CrashFx : MonoBehaviour
    {
        private const int MaxDebris = 60;
        private const int DebrisLayer = 9;

        private CrashoutGameSession session;
        private CrashoutCameraRig cameraRig;
        private CinematicFx cinematic;

        private Material sparkMaterial, flashMaterial, fireMaterial, smokeMaterial, dustMaterial, shardMaterial, ringMaterial, skidMaterial, scorchMaterial;
        private readonly List<GameObject> debris = new List<GameObject>();
        private readonly List<GameObject> wreckEffects = new List<GameObject>();
        private readonly HashSet<uint> smoking = new HashSet<uint>();
        private readonly List<Light> flashLights = new List<Light>();
        private readonly List<float> flashStarts = new List<float>();
        private readonly List<float> flashPeaks = new List<float>();

        private TrailRenderer[] skids;
        private ParticleSystem tyreSmoke;
        private ParticleSystem boostFlame;
        private ParticleSystem landingDust;
        private int lastGrounded = 4;
        private float airborneSince;

        public void Configure(CrashoutGameSession gameSession, CrashoutCameraRig rig, CinematicFx cinematicFx = null)
        {
            Unsubscribe();
            session = gameSession;
            cameraRig = rig;
            cinematic = cinematicFx;
            session.ScoreChanged += OnScore;
            session.Exploded += OnExploded;
            session.PhaseChanged += OnPhase;
            Physics.IgnoreLayerCollision(DebrisLayer, 8, true);
            EnsureMaterials();
            BuildFlashLights();
        }

        private void Unsubscribe()
        {
            if (session == null) return;
            session.ScoreChanged -= OnScore;
            session.Exploded -= OnExploded;
            session.PhaseChanged -= OnPhase;
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        // ------------------------------------------------------------------ phases

        private void OnPhase(GamePhase phase)
        {
            if (phase == GamePhase.ReadyCheck)
            {
                foreach (GameObject piece in debris) if (piece != null) Destroy(piece);
                debris.Clear();
                foreach (GameObject effect in wreckEffects) if (effect != null) Destroy(effect);
                wreckEffects.Clear();
                smoking.Clear();
                if (skids != null) foreach (TrailRenderer trail in skids) { trail.emitting = false; trail.Clear(); }
            }
        }

        // ------------------------------------------------------------------ driving effects

        private void LateUpdate()
        {
            UpdateFlashLights();
            if (session == null || session.Player == null) return;
            ArcadeVehicleController player = session.Player;
            if (skids == null) AttachDrivingEffects(player);

            GamePhase phase = session.Phase;
            bool live = phase == GamePhase.Driving || phase == GamePhase.Crash || phase == GamePhase.SettleDelay;
            Rigidbody body = player.Body;
            Vector3 velocity = body.linearVelocity;
            float speed = velocity.magnitude;
            float lateral = Mathf.Abs(Vector3.Dot(velocity, player.transform.right));
            bool grounded = phase == GamePhase.Driving ? player.WheelsGrounded > 0 : NearGround(player.transform);
            bool sliding = live && grounded && speed > 4f && (lateral > 3.2f || (player.AppliedBrake > 0.5f && speed > 10f) || phase != GamePhase.Driving);
            foreach (TrailRenderer trail in skids) trail.emitting = sliding;

            ParticleSystem.EmissionModule smoke = tyreSmoke.emission;
            smoke.rateOverTime = sliding ? Mathf.Clamp(lateral * 6f + speed, 10f, 70f) : 0f;

            ParticleSystem.EmissionModule flame = boostFlame.emission;
            flame.rateOverTime = phase == GamePhase.Driving && player.IsBoosting ? 90f : 0f;

            if (phase == GamePhase.Driving)
            {
                int wheels = player.WheelsGrounded;
                if (wheels == 0 && lastGrounded > 0) airborneSince = Time.time;
                if (wheels > 0 && lastGrounded == 0 && Time.time - airborneSince > 0.25f)
                {
                    landingDust.transform.position = player.transform.position + Vector3.down * 0.35f;
                    landingDust.Emit(Mathf.RoundToInt(Mathf.Clamp(speed, 10f, 40f)));
                    Spawn("Landing sparks", player.transform.position + Vector3.down * 0.4f, 24, 0.25f, 0.6f, 3f, 9f, 0.08f, new Color(1f, 0.85f, 0.4f, 1f), new Color(1f, 0.5f, 0.1f, 1f), 1.4f, sparkMaterial, true);
                    if (cameraRig != null) cameraRig.AddShake(0.45f);
                }
                lastGrounded = wheels;
            }
        }

        private static bool NearGround(Transform car)
        {
            return Physics.Raycast(car.position, Vector3.down, 1.1f, 1 << 0, QueryTriggerInteraction.Ignore);
        }

        private void AttachDrivingEffects(ArcadeVehicleController player)
        {
            skids = new TrailRenderer[2];
            for (int i = 0; i < 2; i++)
            {
                GameObject emitter = new GameObject("Skid " + i);
                emitter.transform.SetParent(player.transform, false);
                emitter.transform.localPosition = new Vector3(i == 0 ? -0.71f : 0.71f, -0.38f, -0.98f);
                emitter.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                TrailRenderer trail = emitter.AddComponent<TrailRenderer>();
                trail.alignment = LineAlignment.TransformZ;
                trail.time = 25f;
                trail.minVertexDistance = 0.25f;
                trail.widthMultiplier = 0.24f;
                trail.sharedMaterial = skidMaterial;
                trail.textureMode = LineTextureMode.Tile;
                trail.shadowCastingMode = ShadowCastingMode.Off;
                trail.receiveShadows = true;
                trail.numCapVertices = 0;
                trail.colorGradient = Fade(new Color(1f, 1f, 1f, 0.85f), new Color(1f, 1f, 1f, 0.85f), 0.85f, 0.6f);
                trail.emitting = false;
                skids[i] = trail;
            }

            tyreSmoke = MakeLooping("Tyre smoke", player.transform, new Vector3(0f, -0.3f, -1.05f), smokeMaterial, false);
            ParticleSystem.MainModule main = tyreSmoke.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 0.85f, 0.86f, 0.5f), new Color(0.7f, 0.7f, 0.72f, 0.35f));
            main.gravityModifier = -0.03f;
            main.maxParticles = 220;
            ParticleSystem.ShapeModule shape = tyreSmoke.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1.6f, 0.1f, 0.3f);
            Grow(tyreSmoke, 0.6f, 3.2f);
            FadeOut(tyreSmoke, 0.55f);
            Rotate(tyreSmoke);

            boostFlame = MakeLooping("Boost flame", player.transform, new Vector3(0f, -0.25f, -1.68f), fireMaterial, true);
            ParticleSystem.MainModule flameMain = boostFlame.main;
            flameMain.startLifetime = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            flameMain.startSpeed = new ParticleSystem.MinMaxCurve(5f, 9f);
            flameMain.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.36f);
            flameMain.startColor = new ParticleSystem.MinMaxGradient(new Color(0.4f, 0.6f, 1f, 1f), new Color(1f, 0.6f, 0.2f, 1f));
            flameMain.simulationSpace = ParticleSystemSimulationSpace.Local;
            boostFlame.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            ParticleSystem.ShapeModule flameShape = boostFlame.shape;
            flameShape.shapeType = ParticleSystemShapeType.Cone;
            flameShape.angle = 6f;
            flameShape.radius = 0.04f;
            Grow(boostFlame, 1f, 0.2f);
            FadeOut(boostFlame, 1f);

            landingDust = MakeLooping("Landing dust", null, Vector3.zero, dustMaterial, false);
            ParticleSystem.MainModule dust = landingDust.main;
            dust.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
            dust.startSpeed = new ParticleSystem.MinMaxCurve(1f, 4f);
            dust.startSize = new ParticleSystem.MinMaxCurve(0.8f, 2f);
            dust.startColor = new ParticleSystem.MinMaxGradient(new Color(0.62f, 0.57f, 0.5f, 0.55f), new Color(0.5f, 0.46f, 0.4f, 0.4f));
            ParticleSystem.ShapeModule dustShape = landingDust.shape;
            dustShape.shapeType = ParticleSystemShapeType.Circle;
            dustShape.radius = 1.4f;
            dustShape.rotation = new Vector3(90f, 0f, 0f);
            ParticleSystem.EmissionModule dustEmission = landingDust.emission;
            dustEmission.rateOverTime = 0f;
            Grow(landingDust, 0.6f, 2.4f);
            FadeOut(landingDust, 0.6f);
        }

        // ------------------------------------------------------------------ impacts

        private void OnScore(ScoreEvent scoreEvent)
        {
            if (scoreEvent.Amount <= 0) return;
            DamageableVehicle owner = FindVehicle(scoreEvent.EntityId);
            if (owner == null) return;
            Vector3 origin = owner.transform.position + Vector3.up * 0.35f;
            float intensity = Mathf.Clamp(scoreEvent.RelativeSpeed / 10f, 0.4f, 2f);

            Spawn("Impact flash", origin, 1, 0.08f, 0.12f, 0f, 0f, 3.6f * intensity, new Color(1f, 0.9f, 0.7f, 1f), new Color(1f, 0.7f, 0.4f, 1f), 0f, flashMaterial, false);
            Spawn("Impact sparks", origin, Mathf.RoundToInt(46 * intensity), 0.25f, 0.75f, 5f, 15f * intensity, 0.07f, new Color(1f, 0.92f, 0.6f, 1f), new Color(1f, 0.55f, 0.15f, 1f), 1.8f, sparkMaterial, true);
            Spawn("Impact smoke", origin + Vector3.up * 0.2f, Mathf.RoundToInt(10 * intensity), 1.4f, 2.8f, 0.4f, 1.8f, 1.8f, new Color(0.55f, 0.53f, 0.5f, 0.45f), new Color(0.32f, 0.31f, 0.3f, 0.35f), -0.05f, smokeMaterial, false);
            if (scoreEvent.RelativeSpeed > 4f) Shards(origin, owner, intensity);
            Flash(origin, Mathf.Lerp(4f, 14f, intensity * 0.5f), new Color(1f, 0.75f, 0.45f), 0.18f);
            if (cameraRig != null) cameraRig.AddShake(Mathf.Clamp01(scoreEvent.RelativeSpeed / 14f));
            if (cinematic != null) cinematic.Impact(Mathf.Clamp01(scoreEvent.RelativeSpeed / 16f));
            SpawnDebris(scoreEvent, origin, owner);
            AttachWreckSmoke(owner);
        }

        /// <summary>Glass and plastic shards that bounce on the road (particle collisions, no rigidbodies).</summary>
        private void Shards(Vector3 origin, DamageableVehicle owner, float intensity)
        {
            ParticleSystem system = Burst("Glass shards", origin + Vector3.up * 0.3f, Mathf.RoundToInt(40 * intensity), 1.5f, 3.5f, 2.5f, 7f * intensity, 0.08f,
                new Color(0.85f, 0.95f, 1f, 1f), new Color(0.6f, 0.7f, 0.75f, 1f), 1f, shardMaterial, false);
            ParticleSystem.CollisionModule collision = system.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.bounce = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
            collision.dampen = 0.4f;
            collision.lifetimeLoss = 0f;
            collision.collidesWith = 1 << 0;
            collision.quality = ParticleSystemCollisionQuality.Medium;
            ParticleSystem.MainModule main = system.main;
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            ParticleSystem.RotationOverLifetimeModule spin = system.rotationOverLifetime;
            spin.enabled = true;
            spin.separateAxes = true;
            spin.x = new ParticleSystem.MinMaxCurve(-8f, 8f);
            spin.y = new ParticleSystem.MinMaxCurve(-8f, 8f);
            spin.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            system.Play();
        }

        private void OnExploded(Vector3 centre, float radius, bool byPlayer)
        {
            float size = Mathf.Clamp01(radius / 17f);
            Spawn("Blast flash", centre, 1, 0.14f, 0.2f, 0f, 0f, radius * 1.1f, new Color(1f, 0.95f, 0.8f, 1f), new Color(1f, 0.7f, 0.3f, 1f), 0f, flashMaterial, false);

            ParticleSystem fireball = Burst("Fireball", centre, Mathf.RoundToInt(36 + 30 * size), 0.6f, 1.5f, 2f, radius * 0.75f, 2.6f + size * 2f,
                new Color(1f, 0.85f, 0.5f, 1f), new Color(1f, 0.4f, 0.1f, 1f), -0.35f, fireMaterial, false);
            ParticleSystem.ColorOverLifetimeModule fireColour = fireball.colorOverLifetime;
            fireColour.enabled = true;
            Gradient burn = new Gradient();
            burn.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.95f, 0.8f), 0f), new GradientColorKey(new Color(1f, 0.55f, 0.15f), 0.35f), new GradientColorKey(new Color(0.5f, 0.1f, 0.03f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            fireColour.color = burn;
            Grow(fireball, 0.5f, 1.8f);
            Rotate(fireball);
            fireball.Play();

            ParticleSystem ring = Burst("Shockwave", centre + Vector3.up * 0.2f, 1, 0.45f, 0.45f, 0f, 0f, radius * 2.6f, new Color(1f, 0.85f, 0.6f, 0.8f), new Color(1f, 0.8f, 0.6f, 0.8f), 0f, ringMaterial, false);
            ParticleSystemRenderer ringRenderer = ring.GetComponent<ParticleSystemRenderer>();
            ringRenderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            Grow(ring, 0.15f, 1f);
            FadeOut(ring, 1f);
            ring.Play();

            Spawn("Blast sparks", centre, 90, 0.6f, 1.6f, 8f, radius * 2f, 0.09f, new Color(1f, 0.95f, 0.6f, 1f), new Color(1f, 0.5f, 0.1f, 1f), 1.6f, sparkMaterial, true);
            ParticleSystem column = Burst("Smoke column", centre + Vector3.up * 1f, 26, 4f, 7f, 1f, 4f, 4.5f + size * 3f,
                new Color(0.18f, 0.17f, 0.16f, 0.75f), new Color(0.08f, 0.08f, 0.08f, 0.65f), -0.18f, smokeMaterial, false);
            ParticleSystem.MainModule columnMain = column.main;
            columnMain.startDelay = new ParticleSystem.MinMaxCurve(0.15f, 0.5f);
            Grow(column, 0.4f, 2.4f);
            Rotate(column);
            column.Play();
            Destroy(column.gameObject, 8f);

            Flash(centre + Vector3.up, Mathf.Lerp(26f, 60f, size), new Color(1f, 0.6f, 0.25f), 0.9f);
            Scorch(centre, radius * 0.45f);
            if (cameraRig != null) cameraRig.AddShake(Mathf.Clamp01(0.55f + radius / 30f));
            if (cinematic != null) cinematic.Explosion(size);
        }

        private void Scorch(Vector3 centre, float radius)
        {
            if (!Physics.Raycast(centre + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 8f, 1 << 0, QueryTriggerInteraction.Ignore)) return;
            var kit = new MeshKit();
            kit.Panel(Vector3.zero, Vector3.up, Vector3.forward, radius * 2f, radius * 2f);
            GameObject decal = MeshKit.Spawn(null, "Scorch", kit.ToMesh("Scorch"), scorchMaterial, false);
            decal.transform.SetPositionAndRotation(hit.point + Vector3.up * 0.02f, Quaternion.Euler(0f, Random.value * 360f, 0f));
            wreckEffects.Add(decal);
        }

        private void SpawnDebris(ScoreEvent scoreEvent, Vector3 origin, DamageableVehicle owner)
        {
            VehicleLook look = owner.GetComponent<VehicleLook>();
            Material paint = look != null ? look.Paint : null;
            int count = Mathf.Clamp(Mathf.RoundToInt(scoreEvent.Amount / 1200f), 1, 4);
            for (int i = 0; i < count && debris.Count < MaxDebris; i++)
            {
                var kit = new MeshKit();
                // A bent panel: two hinged plates.
                Vector3 size = new Vector3(Random.Range(0.2f, 0.55f), 0.02f, Random.Range(0.15f, 0.45f));
                kit.Box(Vector3.zero, size, Quaternion.identity);
                kit.Box(new Vector3(size.x * 0.5f, size.z * 0.15f, 0f), new Vector3(0.02f, size.z * 0.3f, size.z), Quaternion.Euler(0f, 0f, 25f));
                GameObject piece = MeshKit.Spawn(null, "Debris", kit.ToMesh("Debris"), paint != null && Random.value < 0.75f ? paint : TrimMaterial(), true);
                piece.layer = DebrisLayer;
                piece.transform.position = origin + Random.insideUnitSphere * 0.5f;
                piece.transform.rotation = Random.rotation;
                BoxCollider box = piece.AddComponent<BoxCollider>();
                box.size = new Vector3(size.x, 0.06f, size.z);
                Rigidbody rb = piece.AddComponent<Rigidbody>();
                rb.mass = 8f;
                rb.linearDamping = 0.15f;
                rb.linearVelocity = Random.insideUnitSphere * 6f * Mathf.Clamp(scoreEvent.RelativeSpeed / 8f, 0.6f, 2f) + Vector3.up * 3.5f;
                rb.angularVelocity = Random.insideUnitSphere * 14f;
                debris.Add(piece);
            }
        }

        private Material trimDebris;

        private Material TrimMaterial()
        {
            if (trimDebris == null)
            {
                trimDebris = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Debris trim" };
                trimDebris.SetColor("_BaseColor", new Color(0.04f, 0.04f, 0.045f));
                trimDebris.SetFloat("_Smoothness", 0.4f);
            }
            return trimDebris;
        }

        private void AttachWreckSmoke(DamageableVehicle owner)
        {
            if (owner == null || owner.IsPlayerVehicle || !smoking.Add(owner.EntityId)) return;
            bool burning = VehicleSpec.For(owner.Class).Explosive;

            ParticleSystem smoke = MakeLooping("Wreck smoke", owner.transform, new Vector3(0f, 0.5f, burning ? 0f : 0.9f), smokeMaterial, false);
            ParticleSystem.MainModule main = smoke.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.8f);
            main.startColor = burning
                ? new ParticleSystem.MinMaxGradient(new Color(0.12f, 0.11f, 0.1f, 0.8f), new Color(0.05f, 0.05f, 0.05f, 0.7f))
                : new ParticleSystem.MinMaxGradient(new Color(0.6f, 0.6f, 0.6f, 0.4f), new Color(0.35f, 0.35f, 0.35f, 0.3f));
            main.gravityModifier = -0.1f;
            main.maxParticles = 120;
            ParticleSystem.EmissionModule emission = smoke.emission;
            emission.rateOverTime = burning ? 26f : 12f;
            Grow(smoke, 0.5f, 3f);
            FadeOut(smoke, 0.7f);
            Rotate(smoke);
            ParticleSystem.VelocityOverLifetimeModule drift = smoke.velocityOverLifetime;
            drift.enabled = true;
            drift.space = ParticleSystemSimulationSpace.World;
            drift.x = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            drift.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            drift.z = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            wreckEffects.Add(smoke.gameObject);

            if (burning || Random.value < 0.35f)
            {
                ParticleSystem fire = MakeLooping("Wreck fire", owner.transform, new Vector3(0f, burning ? 1.2f : 0.45f, burning ? 0f : 1.1f), fireMaterial, false);
                ParticleSystem.MainModule fireMain = fire.main;
                fireMain.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
                fireMain.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.4f);
                fireMain.startSize = new ParticleSystem.MinMaxCurve(burning ? 1.2f : 0.5f, burning ? 2.6f : 1.1f);
                fireMain.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.8f, 0.4f, 1f), new Color(1f, 0.4f, 0.1f, 1f));
                fireMain.gravityModifier = -0.4f;
                ParticleSystem.EmissionModule fireEmission = fire.emission;
                fireEmission.rateOverTime = burning ? 60f : 22f;
                ParticleSystem.ShapeModule fireShape = fire.shape;
                fireShape.shapeType = ParticleSystemShapeType.Box;
                fireShape.scale = burning ? new Vector3(1.8f, 0.4f, 5f) : new Vector3(0.9f, 0.2f, 0.6f);
                Grow(fire, 1f, 0.2f);
                FadeOut(fire, 1f);
                Rotate(fire);
                wreckEffects.Add(fire.gameObject);

                GameObject glow = new GameObject("Fire light");
                glow.transform.SetParent(fire.transform, false);
                Light light = glow.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.55f, 0.2f);
                light.range = burning ? 14f : 7f;
                light.intensity = burning ? 6f : 2.5f;
                light.shadows = LightShadows.None;
                glow.AddComponent<Flicker>();
            }
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

        // ------------------------------------------------------------------ lights

        private void BuildFlashLights()
        {
            if (flashLights.Count > 0) return;
            for (int i = 0; i < 4; i++)
            {
                GameObject go = new GameObject("Flash light " + i);
                go.transform.SetParent(transform, false);
                Light light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.shadows = LightShadows.None;
                light.enabled = false;
                flashLights.Add(light);
                flashStarts.Add(-10f);
                flashPeaks.Add(0f);
            }
        }

        private float flashDuration = 0.3f;

        private void Flash(Vector3 position, float intensity, Color colour, float duration)
        {
            int slot = 0;
            float oldest = float.MaxValue;
            for (int i = 0; i < flashLights.Count; i++)
            {
                if (flashStarts[i] < oldest) { oldest = flashStarts[i]; slot = i; }
            }
            Light light = flashLights[slot];
            light.transform.position = position;
            light.color = colour;
            light.range = Mathf.Lerp(8f, 32f, Mathf.InverseLerp(4f, 60f, intensity));
            light.enabled = true;
            flashStarts[slot] = Time.unscaledTime;
            flashPeaks[slot] = intensity;
            flashDuration = duration;
        }

        private void UpdateFlashLights()
        {
            for (int i = 0; i < flashLights.Count; i++)
            {
                Light light = flashLights[i];
                if (!light.enabled) continue;
                float t = (Time.unscaledTime - flashStarts[i]) / Mathf.Max(0.05f, flashDuration);
                if (t >= 1f) { light.enabled = false; continue; }
                light.intensity = flashPeaks[i] * (1f - t) * (1f - t);
            }
        }

        // ------------------------------------------------------------------ particle helpers

        private ParticleSystem Burst(string label, Vector3 position, int count, float lifeMin, float lifeMax, float speedMin, float speedMax, float size,
            Color colourA, Color colourB, float gravity, Material material, bool stretch)
        {
            GameObject effect = new GameObject(label);
            effect.transform.position = position;
            ParticleSystem system = effect.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.loop = false;
            main.duration = 1f;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, Mathf.Max(speedMin, speedMax));
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.startColor = new ParticleSystem.MinMaxGradient(colourA, colourB);
            main.gravityModifier = gravity;
            main.maxParticles = Mathf.Max(64, count);
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Max(1, count)) });
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;
            ParticleSystemRenderer renderer = effect.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            if (stretch)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = 0.045f;
                renderer.lengthScale = 1.2f;
            }
            Destroy(effect, lifeMax + 4f);
            return system;
        }

        private void Spawn(string label, Vector3 position, int count, float lifeMin, float lifeMax, float speedMin, float speedMax, float size,
            Color colourA, Color colourB, float gravity, Material material, bool stretch)
        {
            ParticleSystem system = Burst(label, position, count, lifeMin, lifeMax, speedMin, speedMax, size, colourA, colourB, gravity, material, stretch);
            FadeOut(system, gravity < 0f ? 0.6f : 1f);
            if (gravity < 0f) { Grow(system, 0.5f, 2.2f); Rotate(system); }
            else Grow(system, 1f, 0.15f);
            system.Play();
        }

        private ParticleSystem MakeLooping(string label, Transform parent, Vector3 localPosition, Material material, bool local)
        {
            GameObject effect = new GameObject(label);
            if (parent != null) effect.transform.SetParent(parent, false);
            effect.transform.localPosition = localPosition;
            ParticleSystem system = effect.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.simulationSpace = local ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;
            ParticleSystemRenderer renderer = effect.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            system.Play();
            return system;
        }

        private static void Grow(ParticleSystem system, float from, float to)
        {
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, from, 1f, to));
        }

        private static void FadeOut(ParticleSystem system, float peakAlpha)
        {
            ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
            colour.enabled = true;
            colour.color = Fade(Color.white, Color.white, peakAlpha, 0f);
        }

        private static Gradient Fade(Color a, Color b, float startAlpha, float endAlpha)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 1f) },
                new[] { new GradientAlphaKey(startAlpha, 0f), new GradientAlphaKey(startAlpha * 0.8f, 0.4f), new GradientAlphaKey(endAlpha, 1f) });
            return gradient;
        }

        private static void Rotate(ParticleSystem system)
        {
            ParticleSystem.MainModule main = system.main;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            ParticleSystem.RotationOverLifetimeModule rotation = system.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
        }

        // ------------------------------------------------------------------ materials

        private void EnsureMaterials()
        {
            if (sparkMaterial != null) return;
            sparkMaterial = Particle("Spark", ProceduralTextures.Spark(), true, new Color(6f, 3.6f, 1.4f, 1f), false);
            flashMaterial = Particle("Flash", ProceduralTextures.SoftDot(), true, new Color(5f, 3.8f, 2.4f, 1f), false);
            fireMaterial = Particle("Fire", ProceduralTextures.Flame(), true, new Color(4f, 2.2f, 0.9f, 1f), true);
            smokeMaterial = Particle("Smoke", ProceduralTextures.SmokePuff(), false, Color.white, true);
            dustMaterial = Particle("Dust", ProceduralTextures.SmokePuff(128, 23), false, Color.white, true);
            shardMaterial = Particle("Shard", ProceduralTextures.Shard(), true, new Color(2.2f, 2.4f, 2.6f, 1f), false);
            ringMaterial = Particle("Shock ring", ProceduralTextures.Ring(), true, new Color(3f, 2.2f, 1.4f, 1f), false);
            skidMaterial = new Material(Shader.Find("Sprites/Default")) { name = "Skid marks", mainTexture = ProceduralTextures.SkidMark() };
            skidMaterial.renderQueue = 2460;
            scorchMaterial = new Material(Shader.Find("Sprites/Default")) { name = "Scorch", mainTexture = ProceduralTextures.SoftDot(), color = new Color(0.02f, 0.02f, 0.02f, 0.85f) };
            scorchMaterial.renderQueue = 2470;
        }

        /// <summary>A URP particle material: additive (HDR tint for bloom) or alpha-blended with soft depth fade.</summary>
        private static Material Particle(string name, Texture2D texture, bool additive, Color tint, bool soft)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                return new Material(Shader.Find("Sprites/Default")) { name = name, mainTexture = texture };
            }
            Material material = new Material(shader) { name = name };
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", additive ? 2f : 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            if (soft)
            {
                material.SetFloat("_SoftParticlesEnabled", 1f);
                material.SetFloat("_SoftParticlesNearFadeDistance", 0f);
                material.SetFloat("_SoftParticlesFarFadeDistance", 1.2f);
                material.SetVector("_SoftParticleFadeParams", new Vector4(0f, 1f / 1.2f, 0f, 0f));
                material.EnableKeyword("_SOFTPARTICLES_ON");
            }
            return material;
        }
    }

    /// <summary>Firelight flicker.</summary>
    public sealed class Flicker : MonoBehaviour
    {
        private Light target;
        private float baseIntensity;
        private float seed;

        private void Start()
        {
            target = GetComponent<Light>();
            baseIntensity = target.intensity;
            seed = Random.value * 100f;
        }

        private void Update()
        {
            target.intensity = baseIntensity * (0.7f + 0.6f * Mathf.PerlinNoise(Time.time * 9f, seed));
        }
    }
}

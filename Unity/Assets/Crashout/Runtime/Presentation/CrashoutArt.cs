using Crashout.Vehicle;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Crashout.Presentation
{
    /// <summary>
    /// Procedural stylised art: golden-hour lighting, post-processing, a lit city
    /// and chunky toy-like cars. Everything is generated at runtime so the project
    /// needs no imported assets.
    /// </summary>
    public sealed class CrashoutArt
    {
        public static readonly Color[] TrafficPalette =
        {
            new Color(0.10f, 0.62f, 0.66f),
            new Color(0.98f, 0.70f, 0.12f),
            new Color(0.86f, 0.18f, 0.20f),
            new Color(0.30f, 0.38f, 0.86f),
            new Color(0.92f, 0.88f, 0.78f),
            new Color(0.34f, 0.68f, 0.30f),
            new Color(0.80f, 0.34f, 0.74f)
        };

        private static readonly Color[] FacadeTints =
        {
            new Color(0.93f, 0.80f, 0.66f), new Color(0.72f, 0.80f, 0.88f),
            new Color(0.88f, 0.62f, 0.56f), new Color(0.70f, 0.82f, 0.72f),
            new Color(0.84f, 0.84f, 0.80f), new Color(0.86f, 0.74f, 0.88f),
            new Color(0.95f, 0.88f, 0.62f), new Color(0.62f, 0.68f, 0.80f)
        };

        private const float FacadeSpanX = 7.2f;
        private const float FacadeSpanY = 12f;

        private readonly Material asphalt;
        private readonly Material sidewalk;
        private readonly Material ground;
        private readonly Material lanePaint;
        private readonly Material whitePaint;
        private readonly Material roofMaterial;
        private readonly Material glass;
        private readonly Material rubber;
        private readonly Material rim;
        private readonly Material trim;
        private readonly Material underbody;
        private readonly Material headlight;
        private readonly Material taillight;
        private readonly Material neon;
        private readonly Material shopGlow;
        private readonly Material lampGlow;
        private readonly Material trunk;
        private readonly Material targetGlow;
        private readonly Material[] leaves;
        private readonly Material[] facades;
        private readonly Texture2D facadeAlbedo;
        private readonly Texture2D facadeEmission;
        private readonly Texture2D grit;
        private Material skybox;

        public Material PlayerPaint { get; }

        public CrashoutArt()
        {
            grit = MakeGrit();
            MakeFacadeTextures(out facadeAlbedo, out facadeEmission);

            asphalt = Lit("Asphalt", new Color(0.19f, 0.2f, 0.23f), 0f, 0.5f, grit, 1f / 6f);
            sidewalk = Lit("Sidewalk", new Color(0.6f, 0.62f, 0.66f), 0f, 0.28f, grit, 1f / 4f);
            ground = Lit("Ground", new Color(0.3f, 0.36f, 0.32f), 0f, 0.1f, grit, 1f / 8f);
            lanePaint = Lit("Lane paint", new Color(0.97f, 0.86f, 0.46f), 0f, 0.5f);
            whitePaint = Lit("Road white", new Color(0.93f, 0.92f, 0.86f), 0f, 0.5f);
            roofMaterial = Lit("Roof", new Color(0.3f, 0.3f, 0.32f), 0f, 0.2f, grit, 1f / 4f);
            glass = Lit("Car glass", new Color(0.08f, 0.14f, 0.2f), 0.2f, 0.92f);
            rubber = Lit("Tyre", new Color(0.04f, 0.04f, 0.045f), 0f, 0.35f);
            rim = Lit("Rim", new Color(0.82f, 0.84f, 0.88f), 0.9f, 0.75f);
            trim = Lit("Trim", new Color(0.09f, 0.09f, 0.1f), 0f, 0.4f);
            underbody = Lit("Underbody", new Color(0.03f, 0.03f, 0.035f), 0f, 0.1f);
            headlight = Lit("Headlight", new Color(1f, 0.96f, 0.82f), 0f, 0.9f, null, 1f, new Color(4.2f, 3.7f, 2.6f));
            taillight = Lit("Taillight", new Color(0.8f, 0.05f, 0.05f), 0f, 0.8f, null, 1f, new Color(3.2f, 0.18f, 0.12f));
            neon = Lit("Neon", new Color(0.1f, 0.9f, 1f), 0f, 0.8f, null, 1f, new Color(0.2f, 3.4f, 4.4f));
            shopGlow = Lit("Shop window", new Color(0.1f, 0.08f, 0.06f), 0f, 0.7f, null, 1f, new Color(0.9f, 0.58f, 0.28f));
            lampGlow = Lit("Lamp", new Color(1f, 0.9f, 0.7f), 0f, 0.5f, null, 1f, new Color(2.2f, 1.7f, 1f));
            targetGlow = Lit("Target", new Color(1f, 0.15f, 0.1f), 0f, 0.6f, null, 1f, new Color(5f, 0.35f, 0.25f));
            trunk = Lit("Trunk", new Color(0.32f, 0.22f, 0.15f), 0f, 0.15f);
            PlayerPaint = Lit("Crashout orange", new Color(1f, 0.27f, 0.02f), 0.05f, 0.55f);

            leaves = new[]
            {
                Lit("Leaves A", new Color(0.3f, 0.62f, 0.25f), 0f, 0.2f),
                Lit("Leaves B", new Color(0.42f, 0.72f, 0.3f), 0f, 0.2f),
                Lit("Leaves C", new Color(0.88f, 0.52f, 0.2f), 0f, 0.2f)
            };
            facades = new Material[FacadeTints.Length];
            for (int i = 0; i < facades.Length; i++)
            {
                facades[i] = Lit("Facade " + i, FacadeTints[i], 0f, 0.3f, facadeAlbedo, 1f, new Color(1f, 0.82f, 0.5f) * 1.4f, facadeEmission);
                facades[i].SetTextureScale("_BaseMap", new Vector2(1f / FacadeSpanX, 1f / FacadeSpanY));
                facades[i].SetTextureScale("_EmissionMap", new Vector2(1f / FacadeSpanX, 1f / FacadeSpanY));
            }
        }

        public Material CreatePaint(Color colour, string name = "Paint")
        {
            return Lit(name, colour, 0.05f, 0.55f);
        }

        // ------------------------------------------------------------------ atmosphere

        public Light ApplyAtmosphere(Transform parent, bool tabletop = false)
        {
            Shader skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                skybox = new Material(skyShader) { name = "Golden hour sky" };
                skybox.SetFloat("_SunSize", 0.06f);
                skybox.SetFloat("_SunSizeConvergence", 4f);
                skybox.SetFloat("_AtmosphereThickness", 0.9f);
                skybox.SetColor("_SkyTint", new Color(0.45f, 0.58f, 0.9f));
                skybox.SetColor("_GroundColor", new Color(0.55f, 0.48f, 0.42f));
                skybox.SetFloat("_Exposure", 1.3f);
                RenderSettings.skybox = skybox;
            }

            GameObject sunObject = new GameObject("Sun");
            sunObject.transform.SetParent(parent, false);
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.74f, 0.5f);
            sun.intensity = 2.5f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.82f;
            sun.shadowBias = 0.03f;
            sun.shadowNormalBias = 0.5f;
            sunObject.transform.rotation = Quaternion.Euler(24f, -38f, 0f);
            RenderSettings.sun = sun;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.58f, 0.68f, 0.88f);
            RenderSettings.ambientEquatorColor = new Color(0.72f, 0.6f, 0.54f);
            RenderSettings.ambientGroundColor = new Color(0.34f, 0.3f, 0.3f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 0.45f;
            RenderSettings.fog = !tabletop;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.86f, 0.7f, 0.58f);
            RenderSettings.fogDensity = 0.0105f;
            DynamicGI.UpdateEnvironment();
            return sun;
        }

        public void InstallPostProcessing(Camera camera, Transform parent)
        {
            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.None;
            camera.allowHDR = true;

            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Crashout look";

            Bloom bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(0.75f);
            bloom.scatter.Override(0.68f);
            bloom.tint.Override(new Color(1f, 0.92f, 0.85f));

            Tonemapping tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.ACES);

            ColorAdjustments adjustments = profile.Add<ColorAdjustments>(true);
            adjustments.postExposure.Override(0.15f);
            adjustments.contrast.Override(12f);
            adjustments.saturation.Override(16f);

            Vignette vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.26f);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(new Color(0.12f, 0.05f, 0.08f));

            GameObject volumeObject = new GameObject("Crashout look volume");
            volumeObject.transform.SetParent(parent, false);
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
        }

        // ------------------------------------------------------------------ city

        public Transform BuildCity(Transform parent, bool tabletop = false)
        {
            float reach = tabletop ? 26f : 150f;
            Transform root = new GameObject("City").transform;
            root.SetParent(parent, false);

            float groundSize = tabletop ? 52f : 700f;
            Slab(root, "Ground", new Vector3(0f, -0.2f, 0f), new Vector3(groundSize, 0.3f, groundSize), tabletop ? 0.08f : 0.01f, ground, true);
            Slab(root, "North / South road", new Vector3(0f, 0f, 0f), new Vector3(10f, 0.08f, reach * 2f), 0.02f, asphalt, true);
            Slab(root, "East / West road", new Vector3(0f, 0.01f, 0f), new Vector3(reach * 2f, 0.08f, 10f), 0.02f, asphalt, true);

            float sidewalkSize = reach - 5f - (tabletop ? 0f : 35f);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Slab(root, "Sidewalk", new Vector3(sx * (5f + sidewalkSize * 0.5f), 0f, sz * (5f + sidewalkSize * 0.5f)),
                        new Vector3(sidewalkSize, 0.2f, sidewalkSize), 0.05f, sidewalk, true);
                }
            }

            BuildRoadMarkings(root, reach);
            BuildCornerBlocks(root);
            if (!tabletop) BuildSkyline(root);
            BuildStreetFurniture(root, tabletop);

            Slab(root, "Impact target", new Vector3(7f, 0.6f, -5.7f), new Vector3(3.8f, 1.2f, 0.35f), 0.06f, PlayerPaint, true);
            return root;
        }

        private void BuildRoadMarkings(Transform root, float reach)
        {
            for (float d = -reach; d <= reach; d += 3.6f)
            {
                if (Mathf.Abs(d) < 10.8f) continue;
                Decal(root, "Lane mark", new Vector3(0f, 0.052f, d), new Vector3(0.14f, 0.012f, 1.8f), lanePaint);
                Decal(root, "Lane mark", new Vector3(d, 0.062f, 0f), new Vector3(1.8f, 0.012f, 0.14f), lanePaint);
            }

            for (int side = -1; side <= 1; side += 2)
            {
                for (int arm = -1; arm <= 1; arm += 2)
                {
                    float length = reach - 12f;
                    float centre = 11f + length * 0.5f;
                    Decal(root, "Edge line", new Vector3(side * 4.55f, 0.052f, arm * centre), new Vector3(0.14f, 0.012f, length), whitePaint);
                    Decal(root, "Edge line", new Vector3(arm * centre, 0.062f, side * 4.55f), new Vector3(length, 0.012f, 0.14f), whitePaint);
                }
            }

            for (int arm = -1; arm <= 1; arm += 2)
            {
                for (float offset = -4f; offset <= 4.01f; offset += 1f)
                {
                    Decal(root, "Crosswalk", new Vector3(offset, 0.052f, arm * 7.6f), new Vector3(0.5f, 0.012f, 2.4f), whitePaint);
                    Decal(root, "Crosswalk", new Vector3(arm * 7.6f, 0.062f, offset), new Vector3(2.4f, 0.012f, 0.5f), whitePaint);
                }
                Decal(root, "Stop line", new Vector3(-arm * 2.5f, 0.052f, arm * 9.6f), new Vector3(4.6f, 0.012f, 0.3f), whitePaint);
                Decal(root, "Stop line", new Vector3(arm * 9.6f, 0.062f, arm * 2.5f), new Vector3(0.3f, 0.012f, 4.6f), whitePaint);
            }
        }

        private void BuildCornerBlocks(Transform root)
        {
            float[] heights = { 8f, 10.5f, 7f, 9.5f };
            int index = 0;
            for (int sz = -1; sz <= 1; sz += 2)
            {
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    float h = heights[index];
                    Material facade = facades[(index * 3 + 1) % facades.Length];
                    Vector3 centre = new Vector3(sx * 8.8f, h * 0.5f, sz * 8.8f);
                    Slab(root, "Corner block " + (index + 1), centre, new Vector3(7f, h, 7f), 0.12f, facade, true);
                    Slab(root, "Roof", new Vector3(centre.x, h + 0.15f, centre.z), new Vector3(7.4f, 0.3f, 7.4f), 0.06f, roofMaterial, false);
                    Slab(root, "Rooftop unit", new Vector3(centre.x - sx * 1.2f, h + 0.7f, centre.z + sz * 1f), new Vector3(1.8f, 0.9f, 1.4f), 0.1f, trim, false);
                    Slab(root, "Rooftop unit", new Vector3(centre.x + sx * 1.6f, h + 0.55f, centre.z - sz * 1.4f), new Vector3(1.2f, 0.6f, 1.2f), 0.08f, whitePaint, false);

                    Color awningColour = TrafficPalette[(index * 2 + 1) % TrafficPalette.Length];
                    Material awning = Lit("Awning " + index, awningColour, 0f, 0.35f);

                    // Shop front on the face toward the north/south road.
                    Slab(root, "Shop glass", new Vector3(sx * 5.27f, 1.35f, sz * 8.8f), new Vector3(0.06f, 2.2f, 5.4f), 0.02f, shopGlow, false);
                    Slab(root, "Awning", new Vector3(sx * 4.55f, 3.1f, sz * 8.8f), new Vector3(1.6f, 0.1f, 5.8f), 0.04f, awning, false, new Vector3(0f, 0f, 13f * sx));
                    // Shop front on the face toward the east/west road.
                    Slab(root, "Shop glass", new Vector3(sx * 8.8f, 1.35f, sz * 5.27f), new Vector3(5.4f, 2.2f, 0.06f), 0.02f, shopGlow, false);
                    Slab(root, "Awning", new Vector3(sx * 8.8f, 3.1f, sz * 4.55f), new Vector3(5.8f, 0.1f, 1.6f), 0.04f, awning, false, new Vector3(-13f * sz, 0f, 0f));
                    index++;
                }
            }
        }

        private void BuildSkyline(Transform root)
        {
            float[] centres = { 11.5f, 22f, 34f, 48f, 66f };
            for (int ix = 0; ix < centres.Length; ix++)
            {
                for (int iz = 0; iz < centres.Length; iz++)
                {
                    if (ix == 0 && iz == 0) continue;
                    for (int sx = -1; sx <= 1; sx += 2)
                    {
                        for (int sz = -1; sz <= 1; sz += 2)
                        {
                            int seed = ix * 31 + iz * 17 + (sx > 0 ? 5 : 0) + (sz > 0 ? 11 : 0);
                            float randomA = Hash(seed, 3);
                            float randomB = Hash(seed, 9);
                            float distance = Mathf.Max(centres[ix], centres[iz]);
                            float height = Mathf.Lerp(8f, 20f, randomA) + distance * Mathf.Lerp(0.1f, 0.45f, randomB);
                            float width = Mathf.Lerp(7.6f, 8.6f, randomB);
                            float depth = Mathf.Lerp(7.6f, 8.6f, randomA);
                            Vector3 centre = new Vector3(sx * centres[ix], height * 0.5f, sz * centres[iz]);
                            Material facade = facades[seed % facades.Length];
                            Slab(root, "Skyline building", centre, new Vector3(width, height, depth), 0.12f, facade, false);
                            Slab(root, "Skyline roof", new Vector3(centre.x, height + 0.15f, centre.z), new Vector3(width + 0.4f, 0.3f, depth + 0.4f), 0.06f, roofMaterial, false);
                        }
                    }
                }
            }
        }

        private void BuildStreetFurniture(Transform root, bool tabletop)
        {
            float[] along = tabletop ? new[] { 16f, 23f } : new[] { 16f, 28f, 40f, 54f };
            for (int arm = 0; arm < 4; arm++)
            {
                Vector3 forward = arm == 0 ? Vector3.forward : arm == 1 ? Vector3.back : arm == 2 ? Vector3.right : Vector3.left;
                Vector3 side = Vector3.Cross(Vector3.up, forward);
                for (int s = -1; s <= 1; s += 2)
                {
                    for (int i = 0; i < along.Length; i++)
                    {
                        Vector3 position = forward * along[i] + side * (s * 5.8f);
                        if (i % 2 == 0) Lamp(root, position, -side * s);
                        else Tree(root, position, Hash(arm * 7 + i, s + 3));
                    }
                }
            }
        }

        private void Lamp(Transform root, Vector3 position, Vector3 towardRoad)
        {
            GameObject pole = Primitive(root, PrimitiveType.Cylinder, "Lamp pole", position + Vector3.up * 2.7f, new Vector3(0.14f, 2.6f, 0.14f), trim, false);
            CapsuleCollider collider = pole.AddComponent<CapsuleCollider>();
            collider.radius = 0.5f;
            collider.height = 2f;
            Quaternion facing = Quaternion.LookRotation(towardRoad, Vector3.up);
            Slab(root, "Lamp arm", position + Vector3.up * 5.25f + towardRoad * 0.65f, new Vector3(0.1f, 0.1f, 1.4f), 0.03f, trim, false, facing.eulerAngles);
            Slab(root, "Lamp head", position + Vector3.up * 5.18f + towardRoad * 1.25f, new Vector3(0.4f, 0.1f, 0.7f), 0.04f, lampGlow, false, facing.eulerAngles, false);
        }

        private void Tree(Transform root, Vector3 position, float random)
        {
            float scale = Mathf.Lerp(0.85f, 1.25f, random);
            GameObject stem = Primitive(root, PrimitiveType.Cylinder, "Trunk", position + Vector3.up * (0.95f * scale), new Vector3(0.28f, 0.85f * scale, 0.28f), trunk, true);
            CapsuleCollider collider = stem.AddComponent<CapsuleCollider>();
            collider.radius = 0.5f;
            collider.height = 2f;
            Material foliage = leaves[Mathf.Min(leaves.Length - 1, Mathf.FloorToInt(random * leaves.Length))];
            Primitive(root, PrimitiveType.Sphere, "Canopy", position + Vector3.up * (2.5f * scale), Vector3.one * 2.5f * scale, foliage, true);
            Primitive(root, PrimitiveType.Sphere, "Canopy", position + new Vector3(0.45f, 3.2f * scale, 0.2f), Vector3.one * 1.7f * scale, leaves[1], true);
        }

        // ------------------------------------------------------------------ cars

        public GameObject CreateCar(string name, Material paint, VehicleClass vehicleClass, bool player, out Transform[] frontPivots, out Transform[] rollPivots)
        {
            VehicleSpec spec = VehicleSpec.For(vehicleClass);
            GameObject root = new GameObject(name);
            Transform t = root.transform;
            if (vehicleClass == VehicleClass.Bus) BuildBus(t, paint);
            else if (vehicleClass == VehicleClass.Tanker) BuildTanker(t, paint);
            else BuildCar(t, paint, vehicleClass, player);

            Vector3[] wheelPositions = spec.WheelPositions;
            float wheelDiameter = spec.WheelRadius * 2f;
            float wheelScale = spec.WheelRadius / 0.31f;
            frontPivots = new Transform[2];
            rollPivots = new Transform[wheelPositions.Length];
            for (int i = 0; i < wheelPositions.Length; i++)
            {
                GameObject steerPivot = new GameObject("Wheel steer " + i);
                steerPivot.transform.SetParent(t, false);
                steerPivot.transform.localPosition = wheelPositions[i];
                GameObject rollPivot = new GameObject("Wheel roll " + i);
                rollPivot.transform.SetParent(steerPivot.transform, false);

                bool left = wheelPositions[i].x < 0f;
                GameObject tyre = Primitive(rollPivot.transform, PrimitiveType.Cylinder, "Tyre", Vector3.zero, new Vector3(wheelDiameter, 0.13f * wheelScale, wheelDiameter), rubber, false);
                tyre.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                GameObject wheelFace = Primitive(rollPivot.transform, PrimitiveType.Cylinder, "Rim", new Vector3((left ? -0.03f : 0.03f) * wheelScale, 0f, 0f), new Vector3(wheelDiameter * 0.61f, 0.135f * wheelScale, wheelDiameter * 0.61f), rim, false);
                wheelFace.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                // A spoke bar keeps wheel rotation readable.
                Primitive(rollPivot.transform, PrimitiveType.Cube, "Spoke", new Vector3((left ? -0.075f : 0.075f) * wheelScale, 0f, 0f), new Vector3(0.02f, wheelDiameter * 0.65f, 0.07f * wheelScale), trim, false);
                if (i < 2) frontPivots[i] = steerPivot.transform;
                rollPivots[i] = rollPivot.transform;
            }
            return root;
        }

        private void BuildCar(Transform t, Material paint, VehicleClass vehicleClass, bool player)
        {
            Part(t, "Body", new Vector3(0f, -0.02f, 0f), new Vector3(1.58f, 0.4f, 3.1f), 0.15f, paint, true);
            Part(t, "Underbody", new Vector3(0f, -0.25f, 0f), new Vector3(1.4f, 0.1f, 2.8f), 0.04f, underbody, false);
            Part(t, "Front bumper", new Vector3(0f, -0.16f, 1.53f), new Vector3(1.5f, 0.14f, 0.12f), 0.05f, trim, false);
            Part(t, "Rear bumper", new Vector3(0f, -0.16f, -1.53f), new Vector3(1.5f, 0.14f, 0.12f), 0.05f, trim, false);
            Part(t, "Grille", new Vector3(0f, -0.04f, 1.56f), new Vector3(0.7f, 0.1f, 0.04f), 0.02f, trim, false);
            Part(t, "Headlight L", new Vector3(-0.52f, 0.05f, 1.55f), new Vector3(0.32f, 0.12f, 0.05f), 0.03f, headlight, false);
            Part(t, "Headlight R", new Vector3(0.52f, 0.05f, 1.55f), new Vector3(0.32f, 0.12f, 0.05f), 0.03f, headlight, false);
            Part(t, "Taillight L", new Vector3(-0.52f, 0.06f, -1.55f), new Vector3(0.38f, 0.1f, 0.05f), 0.03f, taillight, false);
            Part(t, "Taillight R", new Vector3(0.52f, 0.06f, -1.55f), new Vector3(0.38f, 0.1f, 0.05f), 0.03f, taillight, false);

            switch (vehicleClass)
            {
                case VehicleClass.Van:
                    Part(t, "Cabin", new Vector3(0f, 0.4f, -0.3f), new Vector3(1.5f, 0.66f, 2.3f), 0.2f, paint, true);
                    Part(t, "Windscreen", new Vector3(0f, 0.5f, 0.86f), new Vector3(1.26f, 0.3f, 0.05f), 0.03f, glass, false);
                    Part(t, "Side glass L", new Vector3(-0.755f, 0.5f, 0.1f), new Vector3(0.05f, 0.28f, 1.1f), 0.03f, glass, false);
                    Part(t, "Side glass R", new Vector3(0.755f, 0.5f, 0.1f), new Vector3(0.05f, 0.28f, 1.1f), 0.03f, glass, false);
                    break;
                case VehicleClass.Pickup:
                    Part(t, "Cab", new Vector3(0f, 0.34f, 0.42f), new Vector3(1.46f, 0.46f, 1.2f), 0.16f, paint, true);
                    Part(t, "Cab glass", new Vector3(0f, 0.38f, 0.42f), new Vector3(1.5f, 0.28f, 0.95f), 0.1f, glass, false);
                    Part(t, "Roof", new Vector3(0f, 0.58f, 0.42f), new Vector3(1.4f, 0.07f, 1.0f), 0.03f, paint, true);
                    Part(t, "Bed floor", new Vector3(0f, 0.2f, -0.85f), new Vector3(1.4f, 0.05f, 1.5f), 0.02f, trim, false);
                    Part(t, "Bed wall L", new Vector3(-0.74f, 0.3f, -0.85f), new Vector3(0.1f, 0.25f, 1.6f), 0.03f, paint, true);
                    Part(t, "Bed wall R", new Vector3(0.74f, 0.3f, -0.85f), new Vector3(0.1f, 0.25f, 1.6f), 0.03f, paint, true);
                    Part(t, "Tailgate", new Vector3(0f, 0.3f, -1.5f), new Vector3(1.5f, 0.25f, 0.08f), 0.03f, paint, true);
                    break;
                case VehicleClass.Hatch:
                    Part(t, "Cabin", new Vector3(0f, 0.35f, -0.28f), new Vector3(1.4f, 0.46f, 1.9f), 0.2f, glass, true);
                    Part(t, "Roof", new Vector3(0f, 0.585f, -0.28f), new Vector3(1.34f, 0.08f, 1.6f), 0.04f, paint, true);
                    break;
                default:
                    Part(t, "Cabin", new Vector3(0f, 0.34f, -0.22f), new Vector3(1.34f, 0.42f, 1.6f), 0.17f, glass, true);
                    Part(t, "Roof", new Vector3(0f, 0.55f, -0.22f), new Vector3(1.3f, 0.08f, 1.34f), 0.035f, paint, true);
                    break;
            }

            Part(t, "Mirror L", new Vector3(-0.82f, 0.3f, 0.5f), new Vector3(0.12f, 0.1f, 0.16f), 0.03f, trim, false);
            Part(t, "Mirror R", new Vector3(0.82f, 0.3f, 0.5f), new Vector3(0.12f, 0.1f, 0.16f), 0.03f, trim, false);

            if (player)
            {
                Material stripe = whitePaint;
                Part(t, "Hood stripe", new Vector3(0f, 0.189f, 0.82f), new Vector3(0.34f, 0.012f, 1.34f), 0.005f, stripe, false);
                Part(t, "Roof stripe", new Vector3(0f, 0.596f, -0.22f), new Vector3(0.34f, 0.012f, 1.3f), 0.005f, stripe, false);
                Part(t, "Trunk stripe", new Vector3(0f, 0.189f, -1.18f), new Vector3(0.34f, 0.012f, 0.6f), 0.005f, stripe, false);
                Part(t, "Spoiler wing", new Vector3(0f, 0.44f, -1.42f), new Vector3(1.56f, 0.05f, 0.4f), 0.02f, trim, true);
                Part(t, "Spoiler post L", new Vector3(-0.5f, 0.3f, -1.4f), new Vector3(0.07f, 0.26f, 0.1f), 0.02f, trim, false);
                Part(t, "Spoiler post R", new Vector3(0.5f, 0.3f, -1.4f), new Vector3(0.07f, 0.26f, 0.1f), 0.02f, trim, false);
                Part(t, "Neon L", new Vector3(-0.8f, -0.21f, 0f), new Vector3(0.04f, 0.04f, 2.3f), 0.015f, neon, false);
                Part(t, "Neon R", new Vector3(0.8f, -0.21f, 0f), new Vector3(0.04f, 0.04f, 2.3f), 0.015f, neon, false);
            }
        }

        private void BuildBus(Transform t, Material paint)
        {
            Part(t, "Body", new Vector3(0f, 0.05f, 0f), new Vector3(2.3f, 2.0f, 7.4f), 0.22f, paint, true);
            Part(t, "Roof cap", new Vector3(0f, 1.1f, 0f), new Vector3(2.2f, 0.12f, 7.2f), 0.05f, whitePaint, true);
            Part(t, "Underbody", new Vector3(0f, -0.95f, 0f), new Vector3(2.0f, 0.2f, 7.0f), 0.05f, underbody, false);
            Part(t, "Window band L", new Vector3(-1.16f, 0.5f, -0.2f), new Vector3(0.06f, 0.7f, 5.6f), 0.03f, glass, false);
            Part(t, "Window band R", new Vector3(1.16f, 0.5f, -0.2f), new Vector3(0.06f, 0.7f, 5.6f), 0.03f, glass, false);
            Part(t, "Windscreen", new Vector3(0f, 0.45f, 3.71f), new Vector3(1.9f, 0.9f, 0.06f), 0.04f, glass, false);
            Part(t, "Rear window", new Vector3(0f, 0.55f, -3.71f), new Vector3(1.7f, 0.6f, 0.06f), 0.04f, glass, false);
            Part(t, "Route sign", new Vector3(0f, 0.95f, 3.72f), new Vector3(1.2f, 0.18f, 0.05f), 0.02f, shopGlow, false);
            Part(t, "Stripe L", new Vector3(-1.16f, -0.35f, 0f), new Vector3(0.05f, 0.14f, 7.2f), 0.02f, whitePaint, false);
            Part(t, "Stripe R", new Vector3(1.16f, -0.35f, 0f), new Vector3(0.05f, 0.14f, 7.2f), 0.02f, whitePaint, false);
            Part(t, "Headlight L", new Vector3(-0.8f, -0.55f, 3.72f), new Vector3(0.4f, 0.16f, 0.06f), 0.03f, headlight, false);
            Part(t, "Headlight R", new Vector3(0.8f, -0.55f, 3.72f), new Vector3(0.4f, 0.16f, 0.06f), 0.03f, headlight, false);
            Part(t, "Taillight L", new Vector3(-0.8f, -0.45f, -3.72f), new Vector3(0.4f, 0.2f, 0.06f), 0.03f, taillight, false);
            Part(t, "Taillight R", new Vector3(0.8f, -0.45f, -3.72f), new Vector3(0.4f, 0.2f, 0.06f), 0.03f, taillight, false);
            Part(t, "Bumper F", new Vector3(0f, -0.85f, 3.7f), new Vector3(2.2f, 0.25f, 0.2f), 0.06f, trim, false);
            Part(t, "Bumper R", new Vector3(0f, -0.85f, -3.7f), new Vector3(2.2f, 0.25f, 0.2f), 0.06f, trim, false);
        }

        private void BuildTanker(Transform t, Material paint)
        {
            Part(t, "Chassis", new Vector3(0f, -0.75f, 0f), new Vector3(2.0f, 0.3f, 7.8f), 0.05f, underbody, false);
            Part(t, "Cab", new Vector3(0f, 0.1f, 3.0f), new Vector3(2.2f, 1.9f, 1.8f), 0.2f, paint, true);
            Part(t, "Cab glass", new Vector3(0f, 0.55f, 3.92f), new Vector3(1.8f, 0.7f, 0.06f), 0.04f, glass, false);
            Part(t, "Cab glass L", new Vector3(-1.12f, 0.55f, 3.3f), new Vector3(0.06f, 0.7f, 1.0f), 0.04f, glass, false);
            Part(t, "Cab glass R", new Vector3(1.12f, 0.55f, 3.3f), new Vector3(0.06f, 0.7f, 1.0f), 0.04f, glass, false);
            Part(t, "Headlight L", new Vector3(-0.7f, -0.55f, 3.92f), new Vector3(0.4f, 0.16f, 0.06f), 0.03f, headlight, false);
            Part(t, "Headlight R", new Vector3(0.7f, -0.55f, 3.92f), new Vector3(0.4f, 0.16f, 0.06f), 0.03f, headlight, false);
            Part(t, "Taillight L", new Vector3(-0.7f, -0.55f, -3.92f), new Vector3(0.4f, 0.16f, 0.06f), 0.03f, taillight, false);
            Part(t, "Taillight R", new Vector3(0.7f, -0.55f, -3.92f), new Vector3(0.4f, 0.16f, 0.06f), 0.03f, taillight, false);
            GameObject tank = Primitive(t, PrimitiveType.Cylinder, "Tank", new Vector3(0f, 0.3f, -0.85f), new Vector3(2.15f, 2.75f, 2.15f), whitePaint, true);
            tank.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            GameObject band = Primitive(t, PrimitiveType.Cylinder, "Hazard band", new Vector3(0f, 0.3f, -0.85f), new Vector3(2.19f, 0.4f, 2.19f), paint, false);
            band.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Part(t, "Hatch", new Vector3(0f, 1.42f, -0.85f), new Vector3(0.6f, 0.12f, 0.6f), 0.04f, trim, false);
            Part(t, "Bumper F", new Vector3(0f, -0.85f, 3.9f), new Vector3(2.1f, 0.25f, 0.2f), 0.06f, trim, false);
        }

        public GameObject CreateTargetMarker(Transform vehicle, float height, Crashout.Simulation.CrashoutGameSession session, uint entityId)
        {
            GameObject marker = new GameObject("Target marker");
            marker.transform.SetParent(vehicle, false);
            GameObject diamond = Part(marker.transform, "Diamond", Vector3.zero, new Vector3(0.75f, 0.75f, 0.75f), 0.1f, targetGlow, false);
            diamond.transform.localRotation = Quaternion.Euler(35f, 45f, 0f);
            Part(marker.transform, "Pointer", new Vector3(0f, -0.62f, 0f), new Vector3(0.12f, 0.5f, 0.12f), 0.04f, targetGlow, false);
            marker.AddComponent<TargetMarker>().Configure(session, entityId, vehicle, height);
            return marker;
        }

        // ------------------------------------------------------------------ building blocks

        private GameObject Part(Transform parent, string name, Vector3 localPosition, Vector3 size, float radius, Material material, bool castShadow)
        {
            GameObject part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.AddComponent<MeshFilter>().sharedMesh = ProceduralMeshes.RoundedBox(size, radius, 2);
            MeshRenderer renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return part;
        }

        private GameObject Slab(Transform parent, string name, Vector3 position, Vector3 size, float radius, Material material, bool collide,
            Vector3? euler = null, bool castShadow = true)
        {
            GameObject slab = new GameObject(name);
            slab.transform.SetParent(parent, false);
            slab.transform.position = position;
            if (euler.HasValue) slab.transform.rotation = Quaternion.Euler(euler.Value);
            slab.AddComponent<MeshFilter>().sharedMesh = ProceduralMeshes.RoundedBox(size, radius, 2);
            MeshRenderer renderer = slab.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
            if (collide) slab.AddComponent<BoxCollider>().size = size;
            return slab;
        }

        private GameObject Decal(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            return Slab(parent, name, position, size, 0.004f, material, false, null, false);
        }

        private GameObject Primitive(Transform parent, PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material, bool castShadow)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = position;
            primitive.transform.localScale = scale;
            Object.Destroy(primitive.GetComponent<Collider>());
            MeshRenderer renderer = primitive.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return primitive;
        }

        // ------------------------------------------------------------------ materials & textures

        private static Material Lit(string name, Color colour, float metallic, float smoothness, Texture map = null, float tiling = 1f,
            Color? emission = null, Texture emissionMap = null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new Material(shader) { name = name, color = colour };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            if (map != null && material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", map);
                material.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));
            }
            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                if (emissionMap != null) material.SetTexture("_EmissionMap", emissionMap);
            }
            return material;
        }

        private static Texture2D MakeGrit()
        {
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true, false) { name = "Grit", wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            System.Random random = new System.Random(7);
            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                float v = 0.9f + (float)random.NextDouble() * 0.1f;
                if (random.NextDouble() < 0.03) v *= 0.82f;
                pixels[i] = new Color(v, v, v, 1f);
            }
            texture.SetPixels(pixels);
            texture.Apply(true);
            return texture;
        }

        private static void MakeFacadeTextures(out Texture2D albedo, out Texture2D emission)
        {
            const int cells = 4;
            const int cell = 32;
            const int size = cells * cell;
            albedo = new Texture2D(size, size, TextureFormat.RGBA32, true, false) { name = "Facade", wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            emission = new Texture2D(size, size, TextureFormat.RGBA32, true, false) { name = "Facade glow", wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            Color[] colours = new Color[size * size];
            Color[] glow = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int cx = x % cell, cy = y % cell;
                    int gx = x / cell, gy = y / cell;
                    float lit = Hash(gx * 13 + 1, gy * 7 + 2);
                    bool isLit = lit < 0.4f;
                    Color wall = new Color(1f, 1f, 1f, 1f) * Mathf.Lerp(0.94f, 1f, cy / (float)cell);
                    if (cy < 3) wall *= 0.8f;
                    bool inside = cx >= 7 && cx < 25 && cy >= 8 && cy < 26;
                    bool frame = inside && (cx == 7 || cx == 24 || cy == 8 || cy == 25);
                    Color pixel = wall;
                    Color emit = Color.black;
                    if (frame)
                    {
                        pixel = new Color(0.28f, 0.28f, 0.3f, 1f);
                    }
                    else if (inside)
                    {
                        float gradient = (cy - 8) / 18f;
                        if (isLit)
                        {
                            pixel = Color.Lerp(new Color(1f, 0.74f, 0.4f), new Color(1f, 0.9f, 0.62f), gradient);
                            emit = pixel;
                        }
                        else
                        {
                            pixel = Color.Lerp(new Color(0.16f, 0.23f, 0.31f), new Color(0.42f, 0.54f, 0.66f), gradient);
                        }
                    }
                    else if (cy >= 26 && cy < 28)
                    {
                        pixel = wall * 0.88f; // lintel
                    }
                    colours[y * size + x] = pixel;
                    glow[y * size + x] = emit;
                }
            }
            albedo.SetPixels(colours);
            albedo.Apply(true);
            emission.SetPixels(glow);
            emission.Apply(true);
        }

        private static float Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }
    }
}

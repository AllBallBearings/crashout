using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Crashout.Presentation
{
    /// <summary>
    /// Procedural art direction with a realistic target: physically based materials driven by
    /// synthesized albedo / normal / mask textures, clear-coated car paint, a late-afternoon sun with
    /// soft cascaded shadows, sky-lit ambient, real-time reflections, a cloud layer, and a filmic
    /// post stack. Everything is generated at runtime, so the project needs no imported assets.
    ///
    /// The class is split across files: this one holds materials, sky, light and post-processing;
    /// CrashoutArt.City builds the junction, CrashoutArt.Vehicles the cars and CrashoutArt.Runway
    /// the approach roads and their landscapes.
    /// </summary>
    public sealed partial class CrashoutArt
    {
        public static readonly Color[] TrafficPalette =
        {
            new Color(0.08f, 0.16f, 0.32f),  // deep blue
            new Color(0.72f, 0.72f, 0.74f),  // silver
            new Color(0.55f, 0.06f, 0.07f),  // burgundy
            new Color(0.93f, 0.93f, 0.91f),  // white
            new Color(0.06f, 0.06f, 0.07f),  // black
            new Color(0.2f, 0.34f, 0.3f),    // forest green
            new Color(0.42f, 0.43f, 0.45f),  // graphite
            new Color(0.78f, 0.6f, 0.36f),   // champagne
            new Color(0.1f, 0.42f, 0.62f)    // ocean blue
        };

        private static Shader litShader;
        private static Shader complexShader;

        // Shared surfaces.
        private readonly Material asphalt;
        private readonly Material roadPainted;
        private readonly Material paving;
        private readonly Material concrete;
        private readonly Material concreteDark;
        private readonly Material ground;
        private readonly Material lanePaint;
        private readonly Material whitePaint;
        private readonly Material roofMaterial;
        private readonly Material[] facades;
        private readonly Material glassTower;
        private readonly Material shopGlass;
        private readonly Material awningMaterial;
        private readonly Material rock;
        private readonly Material bark;
        private readonly Material foliage;
        private readonly Material conifer;
        private readonly Material water;
        private readonly Material steel;
        private readonly Material darkMetal;
        private readonly Material hazard;
        private readonly Material signGreen;

        // Vehicle surfaces.
        private readonly Material glass;
        private readonly Material shatteredGlass;
        private readonly Material rubber;
        private readonly Material alloy;
        private readonly Material chrome;
        private readonly Material trim;
        private readonly Material underbody;
        private readonly Material brakeDisc;
        private readonly Material caliper;
        private readonly Material headlight;
        private readonly Material taillightBase;
        private readonly Material indicatorBase;
        private readonly Material grille;
        private readonly Material[] plates;
        private readonly Material interior;

        // Light sources and markers.
        private readonly Material lampGlow;
        private readonly Material signalRed;
        private readonly Material signalAmber;
        private readonly Material signalGreen;
        private readonly Material signalOff;
        private readonly Material targetGlow;
        private readonly Material neon;

        private readonly ProceduralTextures.SurfaceSet groundDetail;
        private Material skybox;

        public Material PlayerPaint { get; }
        public Material ShatteredGlass => shatteredGlass;
        public Light Sun { get; private set; }
        public ReflectionProbe Reflections { get; private set; }

        public CrashoutArt()
        {
            litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            complexShader = Shader.Find("Universal Render Pipeline/Complex Lit");

            ProceduralTextures.SurfaceSet asphaltSet = ProceduralTextures.Asphalt();
            ProceduralTextures.SurfaceSet roadSet = ProceduralTextures.PaintedRoad();
            ProceduralTextures.SurfaceSet pavingSet = ProceduralTextures.Paving();
            ProceduralTextures.SurfaceSet concreteSet = ProceduralTextures.Concrete();
            ProceduralTextures.SurfaceSet rockSet = ProceduralTextures.Rock();
            ProceduralTextures.SurfaceSet foliageSet = ProceduralTextures.Foliage();
            ProceduralTextures.SurfaceSet tyreSet = ProceduralTextures.Tyre();
            ProceduralTextures.SurfaceSet grilleSet = ProceduralTextures.Grille();
            groundDetail = ProceduralTextures.GroundDetail();

            asphalt = Surface("Asphalt", asphaltSet, 4f, Color.white, 1f, 0.9f);
            roadPainted = Surface("Runway road", roadSet, 1f, Color.white, 1f, 0.9f);
            paving = Surface("Paving", pavingSet, 3f, Color.white, 1f, 0.8f);
            concrete = Surface("Concrete", concreteSet, 4f, new Color(1f, 0.98f, 0.95f), 1f, 0.8f);
            concreteDark = Surface("Concrete dark", concreteSet, 4f, new Color(0.62f, 0.62f, 0.62f), 1f, 0.8f);
            ground = Surface("City ground", pavingSet, 3f, new Color(0.62f, 0.6f, 0.58f), 1f, 0.6f);
            rock = Surface("Rock", rockSet, 8f, Color.white, 1f, 1.2f);
            bark = Surface("Bark", rockSet, 1.5f, new Color(0.55f, 0.4f, 0.3f), 0.6f, 1.4f);
            foliage = Surface("Foliage", foliageSet, 2.2f, Color.white, 1f, 1f);
            conifer = Surface("Conifer", foliageSet, 1.6f, new Color(0.5f, 0.68f, 0.55f), 1f, 1f);
            rubber = Surface("Tyre", tyreSet, 1f, Color.white, 1f, 1f);
            grille = Surface("Grille", grilleSet, 0.25f, Color.white, 1f, 1f);

            lanePaint = Lit("Lane paint", new Color(0.86f, 0.64f, 0.14f), 0f, 0.35f);
            whitePaint = Lit("Road white", new Color(0.85f, 0.85f, 0.82f), 0f, 0.35f);
            roofMaterial = Surface("Roof", concreteSet, 4f, new Color(0.38f, 0.37f, 0.36f), 0.8f, 1f);

            facades = new Material[6];
            for (int i = 0; i < facades.Length; i++)
            {
                bool brick = i % 2 == 0;
                ProceduralTextures.SurfaceSet set = ProceduralTextures.Facade(brick, 200 + i * 17, out Texture2D emission);
                Color tint = brick ? Color.Lerp(Color.white, new Color(0.85f, 0.8f, 0.78f), i * 0.2f)
                    : new[] { new Color(0.95f, 0.9f, 0.82f), new Color(0.82f, 0.86f, 0.9f), new Color(0.92f, 0.82f, 0.74f) }[i / 2 % 3];
                facades[i] = Surface("Facade " + i, set, 1f, tint, 1f, 1f);
                facades[i].SetTextureScale("_BaseMap", new Vector2(1f / 7.2f, 1f / 12f));
                Emissive(facades[i], new Color(1f, 0.78f, 0.5f) * 1.1f, emission);
            }
            glassTower = Lit("Curtain wall", new Color(0.12f, 0.17f, 0.22f), 0.85f, 0.94f);
            shopGlass = Lit("Shop glass", Color.white, 0.2f, 0.95f);
            shopGlass.SetTexture("_BaseMap", ProceduralTextures.Shopfront(7, out Texture2D shopGlow));
            Emissive(shopGlass, new Color(1f, 0.74f, 0.45f) * 1.25f, shopGlow);
            awningMaterial = Lit("Awning", new Color(0.32f, 0.08f, 0.07f), 0f, 0.25f);
            water = Lit("Water", new Color(0.035f, 0.07f, 0.075f), 0.1f, 0.97f);
            Normal(water, ProceduralTextures.WaterNormal(), 0.6f);
            water.SetTextureScale("_BaseMap", new Vector2(1f / 12f, 1f / 12f));
            steel = Lit("Galvanised steel", new Color(0.62f, 0.64f, 0.66f), 0.85f, 0.55f);
            darkMetal = Lit("Dark metal", new Color(0.12f, 0.12f, 0.13f), 0.7f, 0.45f);
            hazard = Lit("Hazard stripes", Color.white, 0f, 0.4f);
            hazard.SetTexture("_BaseMap", HazardTexture());
            hazard.SetTextureScale("_BaseMap", new Vector2(0.5f, 0.5f));
            signGreen = Lit("Road sign", new Color(0.02f, 0.28f, 0.14f), 0.1f, 0.6f);
            Emissive(signGreen, new Color(0.02f, 0.06f, 0.03f));

            glass = Lit("Car glass", new Color(0.015f, 0.022f, 0.03f), 0.3f, 0.97f);
            shatteredGlass = Lit("Shattered glass", new Color(0.32f, 0.36f, 0.38f), 0.15f, 0.55f);
            Normal(shatteredGlass, ProceduralTextures.Rock(128, 5).Normal, 2f);
            shatteredGlass.SetTextureScale("_BaseMap", new Vector2(3f, 3f));
            alloy = Lit("Alloy", new Color(0.78f, 0.79f, 0.81f), 1f, 0.78f);
            chrome = Lit("Chrome", new Color(0.92f, 0.92f, 0.93f), 1f, 0.94f);
            trim = Lit("Trim", new Color(0.025f, 0.025f, 0.028f), 0f, 0.42f);
            underbody = Lit("Underbody", new Color(0.02f, 0.02f, 0.022f), 0f, 0.15f);
            brakeDisc = Lit("Brake disc", new Color(0.35f, 0.34f, 0.33f), 0.9f, 0.5f);
            caliper = Paint(new Color(0.75f, 0.05f, 0.04f), false, "Caliper");
            interior = Lit("Interior", new Color(0.03f, 0.03f, 0.03f), 0f, 0.2f);

            Texture2D lens = ProceduralTextures.LampLens();
            headlight = Lit("Headlight", new Color(0.85f, 0.88f, 0.9f), 0.2f, 0.96f);
            Emissive(headlight, new Color(5f, 4.7f, 4.1f), lens);
            taillightBase = Lit("Taillight", new Color(0.45f, 0.02f, 0.02f), 0f, 0.92f);
            Emissive(taillightBase, new Color(1.4f, 0.04f, 0.02f), lens);
            indicatorBase = Lit("Indicator", new Color(0.6f, 0.32f, 0.03f), 0f, 0.92f);
            Emissive(indicatorBase, Color.black, lens);

            plates = new Material[4];
            for (int i = 0; i < plates.Length; i++)
            {
                plates[i] = Lit("Plate " + i, Color.white, 0.2f, 0.6f);
                plates[i].SetTexture("_BaseMap", ProceduralTextures.Plate(i * 31 + 5));
            }

            lampGlow = Lit("Lamp", new Color(1f, 0.92f, 0.78f), 0f, 0.5f);
            Emissive(lampGlow, new Color(6f, 4.8f, 3.2f));
            signalRed = Lit("Signal red", new Color(0.4f, 0.02f, 0.02f), 0f, 0.9f);
            Emissive(signalRed, new Color(6f, 0.25f, 0.12f));
            signalAmber = Lit("Signal amber", new Color(0.4f, 0.25f, 0.02f), 0f, 0.9f);
            Emissive(signalAmber, new Color(5f, 2.4f, 0.2f));
            signalGreen = Lit("Signal green", new Color(0.02f, 0.3f, 0.15f), 0f, 0.9f);
            Emissive(signalGreen, new Color(0.3f, 5f, 2.4f));
            signalOff = Lit("Signal off", new Color(0.05f, 0.05f, 0.05f), 0f, 0.9f);
            targetGlow = Lit("Target", new Color(1f, 0.15f, 0.1f), 0f, 0.6f);
            Emissive(targetGlow, new Color(6f, 0.5f, 0.3f));
            neon = Lit("Neon", new Color(0.1f, 0.9f, 1f), 0f, 0.8f);
            Emissive(neon, new Color(0.3f, 3.2f, 4.6f));

            PlayerPaint = Paint(new Color(1f, 0.25f, 0.02f), true, "Crashout orange");
        }

        /// <summary>Clear-coated automotive paint; metallic flake or solid.</summary>
        public Material CreatePaint(Color colour, string name = "Paint")
        {
            float tone = colour.r * 0.3f + colour.g * 0.59f + colour.b * 0.11f;
            // Silvers, blues and greys read best as metallics; whites and blacks as solids.
            bool metallic = tone > 0.12f && tone < 0.8f;
            return Paint(colour, metallic, name);
        }

        private static Material Paint(Color colour, bool metallic, string name)
        {
            Material material = new Material(complexShader != null ? complexShader : litShader) { name = name, color = colour };
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Metallic", metallic ? 0.6f : 0.05f);
            material.SetFloat("_Smoothness", metallic ? 0.68f : 0.58f);
            if (complexShader != null)
            {
                material.SetFloat("_ClearCoat", 1f);
                material.SetFloat("_ClearCoatMask", 1f);
                material.SetFloat("_ClearCoatSmoothness", 0.94f);
                material.EnableKeyword("_CLEARCOAT");
            }
            material.enableInstancing = true;
            return material;
        }

        // ------------------------------------------------------------------ atmosphere

        public Light ApplyAtmosphere(Transform parent, bool tabletop = false)
        {
            Shader skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                skybox = new Material(skyShader) { name = "Late afternoon sky" };
                skybox.SetFloat("_SunDisk", 2f);
                skybox.SetFloat("_SunSize", 0.035f);
                skybox.SetFloat("_SunSizeConvergence", 6f);
                skybox.SetFloat("_AtmosphereThickness", 1.05f);
                skybox.SetColor("_SkyTint", new Color(0.46f, 0.52f, 0.62f));
                skybox.SetColor("_GroundColor", new Color(0.42f, 0.4f, 0.38f));
                skybox.SetFloat("_Exposure", 1.2f);
                RenderSettings.skybox = skybox;
            }

            GameObject sunObject = new GameObject("Sun");
            sunObject.transform.SetParent(parent, false);
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.84f, 0.66f);
            sun.intensity = 2.9f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sun.shadowBias = 0.02f;
            sun.shadowNormalBias = 0.35f;
            // Low, raking light from the south-west lights the runways from the side and throws long shadows.
            sunObject.transform.rotation = Quaternion.Euler(34f, 62f, 0f);
            RenderSettings.sun = sun;
            Sun = sun;

            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.35f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 1f;
            RenderSettings.fog = !tabletop;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.74f, 0.7f, 0.66f);
            RenderSettings.fogDensity = 0.0052f;
            DynamicGI.UpdateEnvironment();

            if (!tabletop) BuildCloudDome(parent);
            return sun;
        }

        /// <summary>A real-time reflection probe over the play area so paint and glass mirror the scene.</summary>
        public ReflectionProbe InstallReflections(Transform parent)
        {
            GameObject probeObject = new GameObject("Junction reflections");
            probeObject.transform.SetParent(parent, false);
            probeObject.transform.position = new Vector3(0f, 3f, -6f);
            ReflectionProbe probe = probeObject.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.resolution = 256;
            probe.hdr = true;
            probe.size = new Vector3(400f, 120f, 400f);
            probe.boxProjection = false;
            probe.importance = 1;
            probe.nearClipPlane = 0.3f;
            probe.farClipPlane = 600f;
            probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            Reflections = probe;
            return probe;
        }

        public void RefreshReflections()
        {
            if (Reflections != null) Reflections.RenderProbe();
        }

        /// <summary>Switches from the AR table to the full virtual world's sky: fog and clouds.</summary>
        public void EnableVirtualSky(Transform parent)
        {
            RenderSettings.fog = true;
            if (cloudDome == null) BuildCloudDome(parent);
        }

        private GameObject cloudDome;

        private void BuildCloudDome(Transform parent)
        {
            const int width = 1024, height = 256;
            var pixels = new Color32[width * height];
            System.Threading.Tasks.Parallel.For(0, height, y =>
            {
                for (int x = 0; x < width; x++)
                {
                    float u = x / (float)width, v = y / (float)height;
                    float density = ProceduralTextures.Fbm(u, v * 0.5f, 6, 6, 41, 0.55f);
                    float wisp = ProceduralTextures.Fbm(u * 2f, v, 12, 4, 47, 0.5f);
                    float coverage = ProceduralTextures.SmoothStep(0.48f, 0.72f, density * 0.8f + wisp * 0.3f);
                    // Thin toward the zenith and the horizon so the sky stays readable.
                    coverage *= ProceduralTextures.SmoothStep(0f, 0.25f, v) * ProceduralTextures.SmoothStep(1f, 0.6f, v);
                    float shade = Mathf.Lerp(0.72f, 1f, Mathf.Clamp01(density * 1.6f - 0.3f));
                    Color c = new Color(1f * shade, 0.93f * shade, 0.86f * shade, coverage * 0.85f);
                    pixels[y * width + x] = c;
                }
            });
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, true, false) { name = "Clouds", wrapMode = TextureWrapMode.Repeat };
            texture.wrapModeV = TextureWrapMode.Clamp;
            texture.SetPixels32(pixels);
            texture.Apply(true, true);

            var kit = new MeshKit();
            const int segments = 48, rings = 12;
            const float radius = 520f, dome = 150f;
            int start = kit.VertexCount;
            for (int r = 0; r <= rings; r++)
            {
                float t = r / (float)rings;
                float elevation = Mathf.Lerp(0.02f, Mathf.PI * 0.5f, t);
                for (int s = 0; s <= segments; s++)
                {
                    float a = s / (float)segments * Mathf.PI * 2f;
                    Vector3 p = new Vector3(Mathf.Cos(a) * Mathf.Cos(elevation) * radius, Mathf.Sin(elevation) * dome - 20f, Mathf.Sin(a) * Mathf.Cos(elevation) * radius);
                    kit.Vertex(p, -p.normalized, new Vector2(s / (float)segments * 3f, t));
                }
            }
            kit.Grid(start, rings + 1, segments + 1);
            Material cloudMaterial = new Material(Shader.Find("Sprites/Default")) { name = "Cloud layer", mainTexture = texture };
            cloudMaterial.renderQueue = 2900;
            GameObject domeObject = MeshKit.Spawn(parent, "Cloud dome", kit.ToMesh("Cloud dome"), cloudMaterial, false);
            domeObject.GetComponent<MeshRenderer>().receiveShadows = false;
            cloudDome = domeObject;
        }

        // ------------------------------------------------------------------ post-processing

        /// <summary>Installs the filmic look and returns the profile so <see cref="CinematicFx"/> can animate it.</summary>
        public VolumeProfile InstallPostProcessing(Camera camera, Transform parent, bool tabletop = false)
        {
            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            // MSAA (pipeline asset) handles geometry edges; no extra post AA pass.
            data.antialiasing = AntialiasingMode.None;
            data.requiresDepthTexture = true;
            camera.allowHDR = true;

            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Crashout look";

            Bloom bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.1f);
            bloom.intensity.Override(0.55f);
            bloom.scatter.Override(0.72f);
            bloom.tint.Override(new Color(1f, 0.93f, 0.86f));
            bloom.highQualityFiltering.Override(!tabletop);
            if (!tabletop)
            {
                bloom.dirtTexture.Override(ProceduralTextures.LensDirt());
                bloom.dirtIntensity.Override(2.2f);
            }

            Tonemapping tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.ACES);

            ColorAdjustments adjustments = profile.Add<ColorAdjustments>(true);
            adjustments.postExposure.Override(0.35f);
            adjustments.contrast.Override(14f);
            adjustments.saturation.Override(6f);

            WhiteBalance whiteBalance = profile.Add<WhiteBalance>(true);
            whiteBalance.temperature.Override(6f);
            whiteBalance.tint.Override(2f);

            // Blockbuster split: cool shadows, warm highlights.
            ShadowsMidtonesHighlights smh = profile.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(new Vector4(0.93f, 0.98f, 1.06f, -0.02f));
            smh.midtones.Override(new Vector4(1f, 0.99f, 0.97f, 0f));
            smh.highlights.Override(new Vector4(1.05f, 1f, 0.94f, 0.02f));

            Vignette vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(tabletop ? 0.12f : 0.3f);
            vignette.smoothness.Override(0.42f);
            vignette.color.Override(new Color(0.06f, 0.03f, 0.03f));

            ChromaticAberration chromatic = profile.Add<ChromaticAberration>(true);
            chromatic.intensity.Override(tabletop ? 0f : 0.06f);

            if (!tabletop)
            {
                FilmGrain grain = profile.Add<FilmGrain>(true);
                grain.type.Override(FilmGrainLookup.Thin1);
                grain.intensity.Override(0.16f);
                grain.response.Override(0.85f);

                MotionBlur motionBlur = profile.Add<MotionBlur>(true);
                motionBlur.quality.Override(MotionBlurQuality.High);
                motionBlur.intensity.Override(0.25f);
                motionBlur.clamp.Override(0.06f);

                LensDistortion lens = profile.Add<LensDistortion>(true);
                lens.intensity.Override(0f);
                lens.scale.Override(1f);

                DepthOfField depthOfField = profile.Add<DepthOfField>(true);
                depthOfField.mode.Override(DepthOfFieldMode.Off);
                depthOfField.focusDistance.Override(12f);
                depthOfField.aperture.Override(4f);
                depthOfField.focalLength.Override(55f);
            }

            GameObject volumeObject = new GameObject("Crashout look volume");
            volumeObject.transform.SetParent(parent, false);
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
            return profile;
        }

        // ------------------------------------------------------------------ material helpers

        internal static Material Lit(string name, Color colour, float metallic, float smoothness)
        {
            Material material = new Material(litShader) { name = name, color = colour };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            material.enableInstancing = true;
            return material;
        }

        /// <summary>Textured PBR surface. UVs are in metres, so tiling is one texture per <paramref name="tileMetres"/>.</summary>
        internal static Material Surface(string name, ProceduralTextures.SurfaceSet set, float tileMetres, Color tint, float smoothness = 1f, float normalScale = 1f)
        {
            Material material = Lit(name, tint, 0f, smoothness);
            material.SetTexture("_BaseMap", set.Albedo);
            material.SetTextureScale("_BaseMap", new Vector2(1f / tileMetres, 1f / tileMetres));
            if (set.Normal != null) Normal(material, set.Normal, normalScale);
            if (set.Mask != null)
            {
                material.SetTexture("_MetallicGlossMap", set.Mask);
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            return material;
        }

        internal static void Normal(Material material, Texture2D normal, float scale)
        {
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", scale);
            material.EnableKeyword("_NORMALMAP");
        }

        internal static void Emissive(Material material, Color colour, Texture emissionMap = null)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", colour);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            if (emissionMap != null) material.SetTexture("_EmissionMap", emissionMap);
        }

        internal void Detail(Material material, float tileMetres, float worldSize)
        {
            material.SetTexture("_DetailAlbedoMap", groundDetail.Albedo);
            material.SetTexture("_DetailNormalMap", groundDetail.Normal);
            material.SetFloat("_DetailAlbedoMapScale", 1f);
            material.SetFloat("_DetailNormalMapScale", 0.8f);
            material.SetTextureScale("_DetailAlbedoMap", new Vector2(worldSize / tileMetres, worldSize / tileMetres));
            material.EnableKeyword("_DETAIL_MULX2");
        }

        private static Texture2D HazardTexture()
        {
            const int size = 128;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool stripe = ((x + y) / 32) % 2 == 0;
                    float grime = 0.85f + ProceduralTextures.Hash(x, y, 3) * 0.15f;
                    Color c = stripe ? new Color(0.95f, 0.72f, 0.05f) : new Color(0.04f, 0.04f, 0.04f);
                    pixels[y * size + x] = c * grime;
                }
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, false) { name = "Hazard", wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        private static float Hash(int x, int y)
        {
            return ProceduralTextures.Hash(x, y, 0);
        }

        // ------------------------------------------------------------------ primitive helpers

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
    }
}

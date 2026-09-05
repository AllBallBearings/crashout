using System.Collections.Generic;
using Crashout.Presentation;
using Crashout.Simulation;
using Crashout.Vehicle;
using UnityEngine;
using UnityEngine.Rendering;

namespace Crashout.Bootstrap
{
    public sealed class CrashoutBootstrapper : MonoBehaviour
    {
        private static readonly Color[] TrafficColours =
        {
            new Color(0.13f, 0.55f, 0.62f),
            new Color(0.92f, 0.62f, 0.12f),
            new Color(0.72f, 0.15f, 0.11f),
            new Color(0.25f, 0.34f, 0.7f),
            new Color(0.83f, 0.82f, 0.73f),
            new Color(0.25f, 0.48f, 0.22f)
        };

        private readonly List<DamageableVehicle> vehicles = new List<DamageableVehicle>();
        private Material asphalt;
        private Material concrete;
        private Material lanePaint;
        private Material playerPaint;
        private Material glass;
        private Material rubber;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallPrototype()
        {
            if (FindFirstObjectByType<CrashoutBootstrapper>() != null) return;
            GameObject runtime = new GameObject("Crashout Prototype Runtime");
            runtime.AddComponent<CrashoutBootstrapper>();
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Time.fixedDeltaTime = 1f / 60f;
            Physics.defaultSolverIterations = 12;
            Physics.defaultSolverVelocityIterations = 4;

            CreateMaterials();
            CreateLighting();
            CreateJunction();

            CrashoutGameSession session = gameObject.AddComponent<CrashoutGameSession>();
            ArcadeVehicleController player = CreatePlayer(session);
            CreateTraffic(session);

            CrashoutCameraRig cameraRig = CreateCamera();
            CrashoutHud hud = gameObject.AddComponent<CrashoutHud>();
            cameraRig.Configure(session, player);
            hud.Configure(session, cameraRig);
            session.Configure(player, cameraRig, vehicles);
        }

        private void CreateMaterials()
        {
            asphalt = MakeMaterial("Asphalt", new Color(0.075f, 0.083f, 0.08f), 0.06f, 0.93f);
            concrete = MakeMaterial("Concrete", new Color(0.43f, 0.45f, 0.4f), 0f, 0.88f);
            lanePaint = MakeMaterial("Lane paint", new Color(0.94f, 0.82f, 0.37f), 0f, 0.65f);
            playerPaint = MakeMaterial("Crashout orange", new Color(0.96f, 0.22f, 0.045f), 0.22f, 0.28f);
            glass = MakeMaterial("Smoked glass", new Color(0.045f, 0.09f, 0.105f), 0.5f, 0.14f);
            rubber = MakeMaterial("Tyres", new Color(0.018f, 0.02f, 0.018f), 0f, 0.96f);
        }

        private void CreateLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.32f, 0.39f, 0.42f);
            RenderSettings.ambientEquatorColor = new Color(0.18f, 0.19f, 0.17f);
            RenderSettings.ambientGroundColor = new Color(0.08f, 0.065f, 0.05f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.08f, 0.095f, 0.09f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.009f;

            GameObject sunObject = new GameObject("Sun");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.85f, 0.67f);
            sun.intensity = 2.1f;
            sun.shadows = LightShadows.Soft;
            sunObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
        }

        private void CreateJunction()
        {
            CreateBox("Ground", new Vector3(0f, -0.2f, 0f), new Vector3(52f, 0.3f, 52f), concrete, true);
            CreateBox("North / South road", new Vector3(0f, 0f, 0f), new Vector3(10f, 0.08f, 52f), asphalt, true);
            CreateBox("East / West road", new Vector3(0f, 0.01f, 0f), new Vector3(52f, 0.08f, 10f), asphalt, true);

            for (float z = -23f; z <= 23f; z += 3.4f)
            {
                if (Mathf.Abs(z) < 6f) continue;
                CreateBox("Lane mark", new Vector3(0f, 0.07f, z), new Vector3(0.14f, 0.025f, 1.7f), lanePaint, false);
            }
            for (float x = -23f; x <= 23f; x += 3.4f)
            {
                if (Mathf.Abs(x) < 6f) continue;
                CreateBox("Lane mark", new Vector3(x, 0.075f, 0f), new Vector3(1.7f, 0.025f, 0.14f), lanePaint, false);
            }

            Vector3[] corners =
            {
                new Vector3(-8.8f, 0f, -8.8f), new Vector3(8.8f, 0f, -8.8f),
                new Vector3(-8.8f, 0f, 8.8f), new Vector3(8.8f, 0f, 8.8f)
            };
            Color[] colours =
            {
                new Color(0.18f, 0.22f, 0.23f), new Color(0.28f, 0.2f, 0.18f),
                new Color(0.16f, 0.25f, 0.19f), new Color(0.24f, 0.2f, 0.29f)
            };
            for (int i = 0; i < corners.Length; i++)
            {
                float height = 4.2f + i * 0.85f;
                Material facade = MakeMaterial("Facade " + i, colours[i], 0.02f, 0.78f);
                CreateBox("Corner block " + (i + 1), corners[i] + Vector3.up * (height * 0.5f), new Vector3(7f, height, 7f), facade, true);
            }

            CreateBox("Impact target", new Vector3(7f, 0.6f, -5.7f), new Vector3(3.8f, 1.2f, 0.35f), playerPaint, true);
        }

        private ArcadeVehicleController CreatePlayer(CrashoutGameSession session)
        {
            GameObject car = CreateCarVisual("Player car", playerPaint, true, out Transform[] frontPivots, out Transform[] rollPivots);
            car.transform.position = new Vector3(0f, 0.56f, -18f);
            car.transform.rotation = Quaternion.identity;
            Rigidbody body = car.AddComponent<Rigidbody>();
            BoxCollider collider = car.AddComponent<BoxCollider>();
            collider.size = new Vector3(1.55f, 0.56f, 3.15f);
            car.layer = 8;

            ArcadeVehicleController controller = car.AddComponent<ArcadeVehicleController>();
            controller.SetWheelVisuals(frontPivots, rollPivots);
            DamageableVehicle damageable = car.AddComponent<DamageableVehicle>();
            damageable.Configure(1u, true, session, 26000);
            ResettableBody resettable = car.AddComponent<ResettableBody>();
            resettable.CapturePose();
            vehicles.Add(damageable);
            return controller;
        }

        private void CreateTraffic(CrashoutGameSession session)
        {
            float[] positions = { -21f, -13f, -5f, 4f, 13f, 21f };
            uint id = 10;
            for (int directionIndex = 0; directionIndex < 2; directionIndex++)
            {
                Vector3 direction = directionIndex == 0 ? Vector3.right : Vector3.left;
                float lane = directionIndex == 0 ? -2.25f : 2.25f;
                for (int i = 0; i < positions.Length; i++)
                {
                    float x = directionIndex == 0 ? positions[i] : -positions[i];
                    Material paint = MakeMaterial("Traffic paint " + id, TrafficColours[(i + directionIndex * 2) % TrafficColours.Length], 0.15f, 0.38f);
                    GameObject car = CreateCarVisual("Traffic " + id, paint, false, out _, out _);
                    car.transform.position = new Vector3(x, 0.42f, lane);
                    car.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
                    car.layer = 8;
                    Rigidbody body = car.AddComponent<Rigidbody>();
                    BoxCollider collider = car.AddComponent<BoxCollider>();
                    collider.size = new Vector3(1.5f, 0.58f, 3f);
                    TrafficVehicle traffic = car.AddComponent<TrafficVehicle>();
                    traffic.Configure(direction, lane, 7f + (i % 3) * 0.65f, true);
                    DamageableVehicle damageable = car.AddComponent<DamageableVehicle>();
                    damageable.Configure(id, false, session, i % 5 == 0 ? 26000 : 16000);
                    ResettableBody resettable = car.AddComponent<ResettableBody>();
                    resettable.CapturePose();
                    vehicles.Add(damageable);
                    id++;
                }
            }
        }

        private CrashoutCameraRig CreateCamera()
        {
            Camera existing = Camera.main;
            GameObject cameraObject = existing == null ? new GameObject("Crashout Camera") : existing.gameObject;
            Camera camera = existing == null ? cameraObject.AddComponent<Camera>() : existing;
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.045f, 0.04f);
            cameraObject.transform.position = new Vector3(0f, 4f, -25f);
            if (cameraObject.GetComponent<AudioListener>() == null) cameraObject.AddComponent<AudioListener>();
            return cameraObject.GetComponent<CrashoutCameraRig>() ?? cameraObject.AddComponent<CrashoutCameraRig>();
        }

        private GameObject CreateCarVisual(string name, Material paint, bool player, out Transform[] frontPivots, out Transform[] rollPivots)
        {
            GameObject root = new GameObject(name);
            GameObject body = CreateVisualPrimitive(PrimitiveType.Cube, "Body", root.transform, new Vector3(0f, 0f, 0f), new Vector3(1.55f, 0.46f, 3.05f), paint);
            GameObject cabin = CreateVisualPrimitive(PrimitiveType.Cube, "Cabin", root.transform, new Vector3(0f, 0.42f, -0.12f), new Vector3(1.22f, 0.44f, 1.35f), glass);
            cabin.transform.localRotation = Quaternion.Euler(-2f, 0f, 0f);
            if (player)
            {
                CreateVisualPrimitive(PrimitiveType.Cube, "Stripe", root.transform, new Vector3(0f, 0.255f, 0.35f), new Vector3(0.2f, 0.025f, 1.82f), lanePaint);
            }

            Vector3[] wheelPositions =
            {
                new Vector3(-0.82f, -0.18f, 0.98f), new Vector3(0.82f, -0.18f, 0.98f),
                new Vector3(-0.82f, -0.18f, -0.98f), new Vector3(0.82f, -0.18f, -0.98f)
            };
            frontPivots = new Transform[2];
            rollPivots = new Transform[4];
            for (int i = 0; i < wheelPositions.Length; i++)
            {
                GameObject steerPivot = new GameObject("Wheel steer " + i);
                steerPivot.transform.SetParent(root.transform, false);
                steerPivot.transform.localPosition = wheelPositions[i];
                GameObject rollPivot = new GameObject("Wheel roll " + i);
                rollPivot.transform.SetParent(steerPivot.transform, false);
                GameObject wheel = CreateVisualPrimitive(PrimitiveType.Cylinder, "Wheel", rollPivot.transform, Vector3.zero, new Vector3(0.62f, 0.22f, 0.62f), rubber);
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                if (i < 2) frontPivots[i] = steerPivot.transform;
                rollPivots[i] = rollPivot.transform;
            }
            return root;
        }

        private GameObject CreateBox(string name, Vector3 position, Vector3 scale, Material material, bool collision)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = position;
            box.transform.localScale = scale;
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!collision) Destroy(box.GetComponent<Collider>());
            return box;
        }

        private static GameObject CreateVisualPrimitive(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject visual = GameObject.CreatePrimitive(type);
            visual.name = name;
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = localPosition;
            visual.transform.localScale = localScale;
            visual.GetComponent<MeshRenderer>().sharedMaterial = material;
            Destroy(visual.GetComponent<Collider>());
            return visual;
        }

        private static Material MakeMaterial(string name, Color colour, float metallic, float smoothness)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new Material(shader) { name = name, color = colour };
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            return material;
        }
    }
}

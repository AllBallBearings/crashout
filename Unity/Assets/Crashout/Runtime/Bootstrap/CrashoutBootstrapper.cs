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
        private readonly List<DamageableVehicle> vehicles = new List<DamageableVehicle>();
        private readonly List<GameObject> trafficObjects = new List<GameObject>();
        private CrashoutGameSession session;
        private ArcadeVehicleController player;
        private ResettableBody playerReset;
        private int scenarioIndex;
        private CrashoutArt art;
        private Transform city;
        private PhysicsMaterial rollingContact;

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

            art = new CrashoutArt();
            bool useAr = ArTabletopMode.ShouldUseAr;
            art.ApplyAtmosphere(transform, useAr);
            city = art.BuildCity(transform, useAr);

            session = gameObject.AddComponent<CrashoutGameSession>();
            player = CreatePlayer(session);

            CrashoutCameraRig cameraRig = CreateCamera();
            art.InstallPostProcessing(cameraRig.GetComponent<Camera>(), transform);
            CrashoutHud hud = gameObject.AddComponent<CrashoutHud>();
            CrashFx fx = gameObject.AddComponent<CrashFx>();
            fx.Configure(session, cameraRig);
            cameraRig.Configure(session, player);
            hud.Configure(session, cameraRig);
            session.Configure(player, cameraRig, vehicles);
            session.ScenarioChangeRequested += ChangeScenario;
            LoadScenario(0);

            if (useAr)
            {
                GameObject boardObject = new GameObject("AR Board");
                boardObject.transform.SetParent(transform, false);
                ArBoardCoordinator board = boardObject.AddComponent<ArBoardCoordinator>();
                ArTabletopMode arMode = gameObject.AddComponent<ArTabletopMode>();
                arMode.Configure(session, cameraRig, cameraRig.GetComponent<Camera>(), board, UseVirtualWorld);
                hud.ArMode = arMode;
                arMode.Begin();
            }
        }

        private ArcadeVehicleController CreatePlayer(CrashoutGameSession session)
        {
            GameObject car = art.CreateCar("Player car", art.PlayerPaint, VehicleClass.Sedan, true, out Transform[] frontPivots, out Transform[] rollPivots);
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
            playerReset = resettable;
            vehicles.Add(damageable);
            return controller;
        }

        private void ChangeScenario(int delta)
        {
            LoadScenario(scenarioIndex + delta);
        }

        private void LoadScenario(int index)
        {
            scenarioIndex = ((index % CrashScenarios.Count) + CrashScenarios.Count) % CrashScenarios.Count;
            CrashScenario scenario = CrashScenarios.Create(scenarioIndex);

            foreach (GameObject old in trafficObjects)
            {
                if (old != null) Destroy(old);
            }
            trafficObjects.Clear();
            vehicles.RemoveAll(v => v == null || !v.IsPlayerVehicle);

            playerReset.SetStartPose(scenario.PlayerStart, Quaternion.identity);

            uint targetId = 0;
            uint id = 10;
            foreach (TrafficSpawn spawn in scenario.Traffic)
            {
                DamageableVehicle vehicle = CreateTrafficVehicle(spawn, id);
                if (spawn.IsTarget) targetId = id;
                id++;
            }

            session.LoadScenario(scenario, vehicles, targetId);
            if (targetId != 0)
            {
                DamageableVehicle target = vehicles.Find(v => v.EntityId == targetId);
                if (target != null) art.CreateTargetMarker(target.transform, VehicleSpec.For(target.Class).RootHeight + VehicleSpec.For(target.Class).ColliderSize.y * 0.5f + 0.9f, session, targetId);
            }
        }

        private DamageableVehicle CreateTrafficVehicle(TrafficSpawn spawn, uint id)
        {
            // The traffic chassis rides directly on its box collider. Use rolling
            // resistance here; ordinary static friction can exceed the motor force.
            if (rollingContact == null)
            {
                rollingContact = new PhysicsMaterial("Traffic rolling contact")
                {
                    staticFriction = 0.04f,
                    dynamicFriction = 0.04f,
                    bounciness = 0.12f,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                    bounceCombine = PhysicsMaterialCombine.Maximum
                };
            }

            VehicleSpec spec = VehicleSpec.For(spawn.Class);
            Color colour = spawn.Class == VehicleClass.Bus ? new Color(0.98f, 0.74f, 0.08f)
                : spawn.Class == VehicleClass.Tanker ? new Color(0.88f, 0.14f, 0.1f)
                : CrashoutArt.TrafficPalette[(int)(id * 3) % CrashoutArt.TrafficPalette.Length];
            Material paint = art.CreatePaint(colour, "Traffic paint " + id);
            GameObject car = art.CreateCar("Traffic " + id, paint, spawn.Class, false, out _, out _);
            car.transform.SetPositionAndRotation(spawn.Position, Quaternion.LookRotation(spawn.Direction, Vector3.up));
            car.layer = 8;
            Rigidbody body = car.AddComponent<Rigidbody>();
            BoxCollider collider = car.AddComponent<BoxCollider>();
            collider.size = spec.ColliderSize;
            collider.sharedMaterial = rollingContact;
            TrafficVehicle traffic = car.AddComponent<TrafficVehicle>();
            traffic.Configure(spawn.Direction, spawn.Lane, spawn.Speed, spawn.AlongX);
            body.mass = spec.Mass;
            body.centerOfMass = new Vector3(0f, spawn.Class == VehicleClass.Bus || spawn.Class == VehicleClass.Tanker ? -0.35f : -0.12f, 0f);
            body.maxAngularVelocity = 14f;
            DamageableVehicle damageable = car.AddComponent<DamageableVehicle>();
            damageable.Configure(id, false, session, spec.ScoreCap, spawn.Class);
            ResettableBody resettable = car.AddComponent<ResettableBody>();
            resettable.CapturePose();
            traffic.ResetController();
            vehicles.Add(damageable);
            trafficObjects.Add(car);
            return damageable;
        }

        // AR is unavailable here, so swap the tabletop diorama for the full virtual city.
        private void UseVirtualWorld()
        {
            if (city != null) Destroy(city.gameObject);
            city = art.BuildCity(transform, false);
            RenderSettings.fog = true;
        }

        private void OnDestroy()
        {
            if (rollingContact != null) Destroy(rollingContact);
        }

        private CrashoutCameraRig CreateCamera()
        {
            Camera existing = Camera.main;
            GameObject cameraObject = existing == null ? new GameObject("Crashout Camera") : existing.gameObject;
            Camera camera = existing == null ? cameraObject.AddComponent<Camera>() : existing;
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = new Color(0.035f, 0.045f, 0.04f);
            cameraObject.transform.position = new Vector3(0f, 4f, -25f);
            if (cameraObject.GetComponent<AudioListener>() == null) cameraObject.AddComponent<AudioListener>();
            return cameraObject.GetComponent<CrashoutCameraRig>() ?? cameraObject.AddComponent<CrashoutCameraRig>();
        }
    }
}

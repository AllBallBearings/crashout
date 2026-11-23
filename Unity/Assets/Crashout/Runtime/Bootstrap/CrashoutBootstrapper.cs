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
        private CrashoutCameraRig cameraRig;
        private int scenarioIndex;
        private int playerCarIndex;
        // Lanes loop 34 m out (the schedules are tuned to that period), but cars vanish as they
        // drive off the board's edge, as through an invisible portal, and re-emerge from the far edge.
        private const float LoopExtent = 34f;
        private const float VisibleExtent = 25.2f;
        private CrashoutArt art;
        private Transform city;
        private BoardStyle boardStyle = BoardStyle.City;
        private bool boardTabletop;
        private Transform runwayWorld;
        private PhysicsMaterial rollingContact;
        private Destruction destruction;
        private CinematicFx cinematic;

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
            // Created before the city so buildings and street furniture can register for damage.
            destruction = gameObject.AddComponent<Destruction>();
            art.ApplyAtmosphere(transform, useAr);
            boardTabletop = useAr;
            city = art.BuildCity(transform, useAr, boardStyle);
            art.InstallReflections(transform);

            session = gameObject.AddComponent<CrashoutGameSession>();
            player = CreatePlayer(session, PlayerCarCatalog.Get(playerCarIndex), new Vector3(0f, 0.56f, -18f));

            cameraRig = CreateCamera();
            VolumeProfile look = art.InstallPostProcessing(cameraRig.GetComponent<Camera>(), transform, useAr);
            cinematic = gameObject.AddComponent<CinematicFx>();
            cinematic.Configure(session, cameraRig, look, useAr);
            CrashoutHud hud = gameObject.AddComponent<CrashoutHud>();
            CrashFx fx = gameObject.AddComponent<CrashFx>();
            CrashAudio crashAudio = gameObject.AddComponent<CrashAudio>();
            fx.Configure(session, cameraRig, cinematic);
            cameraRig.Configure(session, player);
            hud.Configure(session, cameraRig);
            session.Configure(player, cameraRig, vehicles);
            destruction.Configure(session);
            fx.Watch(destruction);
            crashAudio.Configure(session);
            crashAudio.Watch(destruction);
            session.ScenarioChangeRequested += ChangeScenario;
            session.PlayerCarChangeRequested += SelectPlayerCar;
            LoadScenario(0);

            if (useAr)
            {
                GameObject boardObject = new GameObject("AR Board");
                boardObject.transform.SetParent(transform, false);
                ArBoardCoordinator board = boardObject.AddComponent<ArBoardCoordinator>();
                ArTabletopMode arMode = gameObject.AddComponent<ArTabletopMode>();
                arMode.Configure(session, cameraRig, cameraRig.GetComponent<Camera>(), board, UseVirtualWorld, SetVirtualBackdrop);
                hud.ArMode = arMode;
                arMode.Begin();
            }
        }

        private ArcadeVehicleController CreatePlayer(CrashoutGameSession session, PlayerCarSpec carSpec, Vector3 startPos)
        {
            Material paint = art.CreatePlayerPaint(carSpec.PaintColor, carSpec.MetallicPaint, "Player paint " + carSpec.Id);
            GameObject car = art.CreateCar("Player car", paint, VehicleClass.Sedan, true,
                out Transform[] frontPivots, out Transform[] rollPivots, carSpec.AssetName, carSpec.WheelPositions);
            car.transform.position = startPos;
            car.transform.rotation = Quaternion.identity;
            Rigidbody body = car.AddComponent<Rigidbody>();
            body.interpolation = RigidbodyInterpolation.Interpolate;
            BoxCollider collider = car.AddComponent<BoxCollider>();
            collider.size = carSpec.ColliderSize;
            car.layer = 8;

            ArcadeVehicleController controller = car.AddComponent<ArcadeVehicleController>();
            controller.SetWheelVisuals(frontPivots, rollPivots);
            controller.ApplyTuning(carSpec.Mass, carSpec.TopSpeed, carSpec.MotorForce);
            DamageableVehicle damageable = car.AddComponent<DamageableVehicle>();
            damageable.Configure(1u, true, session, 26000);
            ResettableBody resettable = car.AddComponent<ResettableBody>();
            resettable.CapturePose();
            playerReset = resettable;
            vehicles.Add(damageable);
            return controller;
        }

        private void SelectPlayerCar(int index)
        {
            playerCarIndex = ((index % PlayerCarCatalog.Count) + PlayerCarCatalog.Count) % PlayerCarCatalog.Count;
            Vector3 startPos = session.Scenario != null ? session.Scenario.PlayerStart : (player != null ? player.transform.position : new Vector3(0f, 0.56f, -18f));
            if (player != null)
            {
                vehicles.RemoveAll(v => v == null || v.IsPlayerVehicle);
                player.gameObject.SetActive(false);
                Destroy(player.gameObject);
            }
            player = CreatePlayer(session, PlayerCarCatalog.Get(playerCarIndex), startPos);
            playerReset.SetStartPose(startPos, Quaternion.identity);
            session.SetPlayer(player, vehicles);
            if (cameraRig != null) cameraRig.SetPlayer(player);
            art.RefreshReflections();
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

            if (scenario.Board != boardStyle)
            {
                boardStyle = scenario.Board;
                RebuildBoard();
            }

            // Each junction brings its own approach road and landscape, far outside the board.
            if (runwayWorld != null) Destroy(runwayWorld.gameObject);
            runwayWorld = art.BuildRunway(transform, scenario.Runway, city.GetComponentInChildren<RoadSurface>());
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
            art.RefreshReflections();
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
            Color colour = spawn.Class == VehicleClass.Bus ? new Color(0.92f, 0.93f, 0.95f)
                : spawn.Class == VehicleClass.Tanker ? new Color(0.14f, 0.16f, 0.19f)
                : spawn.Class == VehicleClass.Van ? new Color(0.93f, 0.94f, 0.95f)
                : CrashoutArt.TrafficPalette[(int)(id * 5) % CrashoutArt.TrafficPalette.Length];
            Material paint = art.CreatePaint(colour, "Traffic paint " + id);
            GameObject car = art.CreateCar("Traffic " + id, paint, spawn.Class, false, out _, out _);
            car.transform.SetPositionAndRotation(spawn.Position, Quaternion.LookRotation(spawn.Direction, Vector3.up));
            car.layer = 8;
            Rigidbody body = car.AddComponent<Rigidbody>();
            body.interpolation = RigidbodyInterpolation.Interpolate;
            BoxCollider collider = car.AddComponent<BoxCollider>();
            collider.size = spec.ColliderSize;
            collider.sharedMaterial = rollingContact;
            TrafficVehicle traffic = car.AddComponent<TrafficVehicle>();
            traffic.Configure(spawn.Direction, spawn.Lane, spawn.Speed, spawn.AlongX);
            traffic.SetLoopExtent(LoopExtent, VisibleExtent);
            body.mass = spec.Mass;
            body.centerOfMass = new Vector3(0f, spawn.Class == VehicleClass.Bus || spawn.Class == VehicleClass.Tanker ? -0.35f : -0.12f, 0f);
            body.maxAngularVelocity = 14f;
            DamageableVehicle damageable = car.AddComponent<DamageableVehicle>();
            damageable.Configure(id, false, session, spec.ScoreCap, spawn.Class);
            ResettableBody resettable = car.AddComponent<ResettableBody>();
            resettable.CapturePose();
            vehicles.Add(damageable);
            trafficObjects.Add(car);
            return damageable;
        }

        // AR is unavailable here, so put the diorama on the virtual display floor under a virtual sky.
        private void UseVirtualWorld()
        {
            boardTabletop = false;
            RebuildBoard();
            SetVirtualBackdrop(true);
        }

        private void SetVirtualBackdrop(bool enabled)
        {
            art.SetVirtualBackdrop(transform, enabled);
            if (cinematic != null) cinematic.SetTabletop(!enabled);
            art.RefreshReflections();
        }

        /// <summary>Swaps the diorama on the board: the downtown block or the harbor.</summary>
        private void RebuildBoard()
        {
            destruction.Clear();
            if (city != null)
            {
                // Deactivate first so the old colliders are gone before the new board's props settle.
                city.gameObject.SetActive(false);
                Destroy(city.gameObject);
            }
            city = art.BuildCity(transform, boardTabletop, boardStyle);
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

using System;
using System.Collections;
using System.Collections.Generic;
using Crashout.Simulation;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Crashout.Presentation
{
    public enum ArStage : byte
    {
        Inactive,
        Starting,
        Searching,
        ReadyToPlace,
        Playing
    }

    /// <summary>
    /// Shows the single simulated world on a real table.
    ///
    /// The physics world is never scaled or moved. Instead the AR camera's pose is mapped
    /// into canonical world space through the board transform (position, yaw, 1:N scale)
    /// and a second camera renders the simulation on top of the camera feed. If AR is
    /// unavailable the mode hands control back to the ordinary virtual camera.
    /// </summary>
    public sealed class ArTabletopMode : MonoBehaviour
    {
        private const int ReticleLayer = 31;
        private const float ArNearClip = 0.02f;
        private const float ArFarClip = 12f;
        private const float BoardExtent = 52f;
        private const float StartupTimeoutSeconds = 6f;

        private static readonly List<ARRaycastHit> Hits = new List<ARRaycastHit>();

        private CrashoutGameSession session;
        private CrashoutCameraRig cameraRig;
        private Camera gameCamera;
        private ArBoardCoordinator board;
        private Action fallback;

        private ARSession arSession;
        private ARPlaneManager planeManager;
        private ARRaycastManager raycastManager;
        private Camera arCamera;
        private GameObject rigRoot;
        private GameObject reticle;
        private Material reticleMaterial;
        private Pose candidatePose;
        private float selectedScale = ArBoardCoordinator.DefaultPresentationScale;
        private float savedTimeScale = 1f;

        public ArStage Stage { get; private set; } = ArStage.Inactive;
        public bool IsActive => Stage != ArStage.Inactive;
        public bool IsTracking => arSession != null && ARSession.state == ARSessionState.SessionTracking;
        public float BoardWidthMetres => BoardExtent * selectedScale;

        public static bool ShouldUseAr =>
            Application.platform == RuntimePlatform.IPhonePlayer
            || Environment.GetEnvironmentVariable("CRASHOUT_FORCE_AR") == "1";

        public void Configure(CrashoutGameSession gameSession, CrashoutCameraRig rig, Camera camera, ArBoardCoordinator boardCoordinator, Action onFallback)
        {
            session = gameSession;
            cameraRig = rig;
            gameCamera = camera;
            board = boardCoordinator;
            fallback = onFallback;
        }

        public void Begin()
        {
            if (Stage != ArStage.Inactive) return;
            Stage = ArStage.Starting;
            savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            // Only the session exists until AR is known to work, so the virtual camera stays untouched on fallback.
            rigRoot = new GameObject("AR Rig");
            rigRoot.transform.SetParent(transform, false);
            GameObject sessionObject = new GameObject("AR Session");
            sessionObject.transform.SetParent(rigRoot.transform, false);
            sessionObject.SetActive(false);
            arSession = sessionObject.AddComponent<ARSession>();
            sessionObject.AddComponent<ARInputManager>();
            sessionObject.SetActive(true);
            StartCoroutine(WaitForSession());
        }

        public void PlaceBoard()
        {
            if (Stage != ArStage.ReadyToPlace) return;
            float yaw = arCamera.transform.eulerAngles.y;
            board.Place(new Pose(candidatePose.position, Quaternion.Euler(0f, yaw, 0f)), selectedScale);
            planeManager.SetTrackablesActive(false);
            planeManager.enabled = false;
            reticle.SetActive(false);
            gameCamera.enabled = true;
            Time.timeScale = savedTimeScale;
            session.ResetTurn();
            Stage = ArStage.Playing;
            SyncCamera();
        }

        public void Relocate()
        {
            if (Stage != ArStage.Playing) return;
            savedTimeScale = Time.timeScale > 0f ? Time.timeScale : savedTimeScale;
            Time.timeScale = 0f;
            gameCamera.enabled = false;
            planeManager.enabled = true;
            planeManager.SetTrackablesActive(true);
            Stage = ArStage.Searching;
        }

        public void ScaleBoard(float factor)
        {
            selectedScale = Mathf.Clamp(selectedScale * factor, ArBoardCoordinator.MinPresentationScale, ArBoardCoordinator.MaxPresentationScale);
            if (Stage == ArStage.Playing) board.SetScale(selectedScale);
        }

        private void BuildRig()
        {
            cameraRig.enabled = false;
            gameCamera.enabled = false;

            // Build the origin inactive so every component sees a fully wired XROrigin when it wakes.
            GameObject originObject = new GameObject("XR Origin");
            originObject.transform.SetParent(rigRoot.transform, false);
            originObject.SetActive(false);
            XROrigin origin = originObject.AddComponent<XROrigin>();

            GameObject offset = new GameObject("Camera Offset");
            offset.transform.SetParent(originObject.transform, false);
            GameObject cameraObject = new GameObject("AR Camera");
            cameraObject.transform.SetParent(offset.transform, false);
            cameraObject.SetActive(false);

            arCamera = cameraObject.AddComponent<Camera>();
            arCamera.clearFlags = CameraClearFlags.SolidColor;
            arCamera.backgroundColor = Color.black;
            arCamera.nearClipPlane = ArNearClip;
            arCamera.farClipPlane = ArFarClip;
            arCamera.cullingMask = 1 << ReticleLayer;
            arCamera.depth = -10f;
            cameraObject.AddComponent<ARCameraManager>();
            cameraObject.AddComponent<ARCameraBackground>();

            TrackedPoseDriver driver = cameraObject.AddComponent<TrackedPoseDriver>();
            driver.positionInput = new InputActionProperty(new InputAction("AR position", InputActionType.Value, "<HandheldARInputDevice>/devicePosition"));
            driver.rotationInput = new InputActionProperty(new InputAction("AR rotation", InputActionType.Value, "<HandheldARInputDevice>/deviceRotation"));
            driver.trackingStateInput = new InputActionProperty(new InputAction("AR tracking", InputActionType.Value, "<HandheldARInputDevice>/trackingState"));

            origin.Camera = arCamera;
            origin.CameraFloorOffsetObject = offset;
            cameraObject.SetActive(true);

            planeManager = originObject.AddComponent<ARPlaneManager>();
            planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal;
            raycastManager = originObject.AddComponent<ARRaycastManager>();
            originObject.SetActive(true);

            // Camera stack: the AR camera draws the feed and the placement reticle,
            // the game camera overlays the simulation.
            UniversalAdditionalCameraData baseData = arCamera.GetUniversalAdditionalCameraData();
            baseData.renderType = CameraRenderType.Base;
            baseData.renderPostProcessing = false;
            UniversalAdditionalCameraData overlayData = gameCamera.GetUniversalAdditionalCameraData();
            overlayData.renderType = CameraRenderType.Overlay;
            overlayData.renderPostProcessing = true;
            baseData.cameraStack.Add(gameCamera);

            BuildReticle();
        }

        private void BuildReticle()
        {
            Shader shader = Shader.Find("Sprites/Default");
            reticleMaterial = new Material(shader) { name = "Board reticle", color = new Color(0.35f, 0.95f, 1f, 0.95f) };
            reticle = new GameObject("Board reticle");
            reticle.transform.SetParent(rigRoot.transform, false);
            // Unit-sized outline; scaled to the board footprint each frame.
            const float bar = 0.012f;
            AddBar(new Vector3(0f, 0f, 0.5f), new Vector3(1f, bar, bar));
            AddBar(new Vector3(0f, 0f, -0.5f), new Vector3(1f, bar, bar));
            AddBar(new Vector3(0.5f, 0f, 0f), new Vector3(bar, bar, 1f));
            AddBar(new Vector3(-0.5f, 0f, 0f), new Vector3(bar, bar, 1f));
            AddBar(Vector3.zero, new Vector3(0.12f, bar, bar));
            AddBar(Vector3.zero, new Vector3(bar, bar, 0.12f));
            // A forward tick shows which way the road runs away from the player.
            AddBar(new Vector3(0f, 0f, 0.38f), new Vector3(bar * 2f, bar, 0.24f));
            reticle.SetActive(false);
        }

        private void AddBar(Vector3 position, Vector3 size)
        {
            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(bar.GetComponent<Collider>());
            bar.transform.SetParent(reticle.transform, false);
            bar.transform.localPosition = position;
            bar.transform.localScale = size;
            bar.layer = ReticleLayer;
            MeshRenderer renderer = bar.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = reticleMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private IEnumerator WaitForSession()
        {
            float started = Time.realtimeSinceStartup;
            yield return ARSession.CheckAvailability();
            while (ARSession.state < ARSessionState.Ready && ARSession.state != ARSessionState.Unsupported
                && Time.realtimeSinceStartup - started < StartupTimeoutSeconds)
            {
                yield return null;
            }

            if (ARSession.state < ARSessionState.Ready)
            {
                Debug.LogWarning("Crashout: AR is unavailable (" + ARSession.state + "); using the virtual camera.");
                Deactivate();
                yield break;
            }
            BuildRig();
            Stage = ArStage.Searching;
        }

        private void Deactivate()
        {
            Stage = ArStage.Inactive;
            if (rigRoot != null) Destroy(rigRoot);
            if (reticleMaterial != null) Destroy(reticleMaterial);

            Time.timeScale = savedTimeScale > 0f ? savedTimeScale : 1f;
            fallback?.Invoke();
        }

        private void OnEnable()
        {
            Application.onBeforeRender += SyncCamera;
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= SyncCamera;
        }

        private void OnDestroy()
        {
            if (Stage != ArStage.Inactive) Time.timeScale = savedTimeScale > 0f ? savedTimeScale : 1f;
            if (reticleMaterial != null) Destroy(reticleMaterial);
        }

        private void Update()
        {
            if (Stage == ArStage.Searching || Stage == ArStage.ReadyToPlace) UpdateCandidate();
            if (Stage == ArStage.Playing) board.SetTrackingState(IsTracking ? BoardTrackingState.Tracking : BoardTrackingState.Limited);
        }

        private void LateUpdate()
        {
            SyncCamera();
        }

        private void UpdateCandidate()
        {
            Vector2 centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            bool found = false;
            if (raycastManager.Raycast(centre, Hits, TrackableType.PlaneWithinPolygon))
            {
                for (int i = 0; i < Hits.Count; i++)
                {
                    ARPlane plane = planeManager.GetPlane(Hits[i].trackableId);
                    if (plane != null && plane.alignment == PlaneAlignment.HorizontalUp)
                    {
                        candidatePose = Hits[i].pose;
                        found = true;
                        break;
                    }
                }
            }

            Stage = found ? ArStage.ReadyToPlace : ArStage.Searching;
            reticle.SetActive(found);
            if (!found) return;
            reticle.transform.SetPositionAndRotation(
                candidatePose.position + Vector3.up * 0.002f,
                Quaternion.Euler(0f, arCamera.transform.eulerAngles.y, 0f));
            reticle.transform.localScale = new Vector3(BoardWidthMetres, 1f, BoardWidthMetres);
        }

        private void SyncCamera()
        {
            if (Stage != ArStage.Playing || arCamera == null || gameCamera == null || board == null) return;

            float scale = board.PresentationScale;
            Transform boardTransform = board.transform;
            gameCamera.transform.SetPositionAndRotation(
                boardTransform.InverseTransformPoint(arCamera.transform.position) / scale,
                Quaternion.Inverse(boardTransform.rotation) * arCamera.transform.rotation);

            // Reuse the device projection (it carries the lens and aspect) with clip planes in canonical units.
            Matrix4x4 projection = arCamera.projectionMatrix;
            float near = ArNearClip / scale;
            float far = ArFarClip / scale;
            projection.m22 = -(far + near) / (far - near);
            projection.m23 = -(2f * far * near) / (far - near);
            gameCamera.projectionMatrix = projection;
        }
    }
}

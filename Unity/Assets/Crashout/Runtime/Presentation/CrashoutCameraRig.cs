using Crashout.Simulation;
using Crashout.Vehicle;
using UnityEngine;

namespace Crashout.Presentation
{
    [RequireComponent(typeof(Camera))]
    public sealed class CrashoutCameraRig : MonoBehaviour
    {
        private const float RevealSeconds = 2.6f;

        [SerializeField] private CameraMode mode = CameraMode.Chase;
        [SerializeField] private Vector3 boardCenter = Vector3.zero;

        private CrashoutGameSession session;
        private ArcadeVehicleController player;
        private Camera gameCamera;
        private Vector3 velocity;
        private float trauma;
        private float revealStartedAt = -100f;
        private float orbitAngle;
        private float roll;
        private bool snapNextFrame;

        public CameraMode Mode => mode;
        /// <summary>What the shot is about (the car, or the centre of the pile-up). Read by depth of field.</summary>
        public Vector3 FocusPoint { get; private set; }
        public float FocusDistance => Vector3.Distance(transform.position, FocusPoint);
        public bool IsRevealing => session != null && session.Phase == GamePhase.ReadyCheck && Time.unscaledTime - revealStartedAt < RevealSeconds;

        private void Awake()
        {
            gameCamera = GetComponent<Camera>();
            gameCamera.fieldOfView = 58f;
            gameCamera.nearClipPlane = 0.05f;
            gameCamera.farClipPlane = 700f;
        }

        public void Configure(CrashoutGameSession gameSession, ArcadeVehicleController target)
        {
            if (session != null) session.PhaseChanged -= OnPhase;
            session = gameSession;
            player = target;
            session.PhaseChanged += OnPhase;
            BeginReveal();
        }

        private void OnDestroy()
        {
            if (session != null) session.PhaseChanged -= OnPhase;
        }

        private void OnPhase(GamePhase phase)
        {
            if (phase == GamePhase.ReadyCheck) BeginReveal();
        }

        private void BeginReveal()
        {
            revealStartedAt = Time.unscaledTime;
            snapNextFrame = true;
            orbitAngle = 0f;
        }

        public void AddShake(float amount)
        {
            trauma = Mathf.Clamp01(trauma + amount);
        }

        public void CycleMode()
        {
            mode = mode == CameraMode.Tabletop
                ? CameraMode.Chase
                : mode == CameraMode.Chase
                    ? CameraMode.FirstPerson
                    : mode == CameraMode.FirstPerson
                        ? CameraMode.Overhead
                        : CameraMode.Tabletop;
            velocity = Vector3.zero;
        }

        private void LateUpdate()
        {
            if (player == null || session == null) return;

            Transform target = player.transform;
            GamePhase phase = session.Phase;
            bool crashCamera = (phase == GamePhase.Crash || phase == GamePhase.SettleDelay || phase == GamePhase.Results)
                && mode != CameraMode.Tabletop;
            Vector3 desiredPosition;
            Vector3 lookTarget;
            float smoothTime = mode == CameraMode.FirstPerson ? 0.025f : 0.13f;

            if (crashCamera)
            {
                FramePileUp(target, out desiredPosition, out lookTarget);
                smoothTime = 0.35f;
            }
            else
            {
                switch (mode)
                {
                    case CameraMode.Tabletop:
                        desiredPosition = boardCenter + new Vector3(19f, 26f, -22f);
                        lookTarget = boardCenter;
                        break;
                    case CameraMode.FirstPerson:
                        desiredPosition = target.TransformPoint(0f, 0.38f, 0.1f);
                        lookTarget = target.position + target.forward * 15f + Vector3.up * 0.25f;
                        break;
                    case CameraMode.Overhead:
                        desiredPosition = target.position - target.forward * 3f + Vector3.up * 13.5f;
                        lookTarget = target.position + target.forward * 4f;
                        break;
                    default:
                        // Chase: stay level with the horizon on slopes, not glued to the car's pitch.
                        Vector3 flatForward = Vector3.ProjectOnPlane(target.forward, Vector3.up);
                        if (flatForward.sqrMagnitude < 0.01f) flatForward = Vector3.forward;
                        flatForward.Normalize();
                        Vector3 slopeForward = Vector3.Lerp(flatForward, target.forward, 0.6f).normalized;
                        desiredPosition = target.position - slopeForward * 6.6f + Vector3.up * 2.6f;
                        lookTarget = target.position + slopeForward * 5f + Vector3.up * 0.7f;
                        break;
                }

                if (phase == GamePhase.ReadyCheck && mode != CameraMode.Tabletop)
                {
                    // Crane reveal: start high behind the car framing the drop and the junction below, then settle in.
                    float t = Mathf.Clamp01((Time.unscaledTime - revealStartedAt) / RevealSeconds);
                    float ease = t * t * (3f - 2f * t);
                    Vector3 toJunction = boardCenter - target.position;
                    Vector3 craneLook = target.position + toJunction * 0.18f;
                    Vector3 cranePosition = target.position - Vector3.forward * 11f + Vector3.up * 7.5f + Vector3.right * 3f;
                    desiredPosition = Vector3.Lerp(cranePosition, desiredPosition, ease);
                    lookTarget = Vector3.Lerp(craneLook, lookTarget, ease);
                    smoothTime = 0.05f;
                }
            }

            FocusPoint = crashCamera ? lookTarget : target.position;
            if (snapNextFrame)
            {
                transform.position = desiredPosition;
                transform.rotation = Quaternion.LookRotation(lookTarget - desiredPosition, Vector3.up);
                velocity = Vector3.zero;
                snapNextFrame = false;
            }
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
            Quaternion desiredRotation = Quaternion.LookRotation(lookTarget - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));

            float speed = player.SpeedMetresPerSecond;
            bool driving = phase == GamePhase.Driving;

            // Lean into turns and slides a touch.
            float lateral = driving ? Vector3.Dot(player.Body.linearVelocity, target.right) : 0f;
            float targetRoll = mode == CameraMode.Chase && driving ? Mathf.Clamp(-player.SteeringInput * 1.6f - lateral * 0.35f, -4f, 4f) : 0f;
            roll = Mathf.Lerp(roll, targetRoll, 1f - Mathf.Exp(-5f * Time.unscaledDeltaTime));
            transform.rotation *= Quaternion.Euler(0f, 0f, roll);

            float targetFov = mode == CameraMode.Tabletop ? 50f
                : crashCamera ? 50f
                : 56f + Mathf.Clamp(speed, 0f, 45f) * 0.42f + (player.IsBoosting && driving ? 6f : 0f);
            gameCamera.fieldOfView = Mathf.Lerp(gameCamera.fieldOfView, targetFov, 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime));

            // Road rumble grows with speed.
            if (driving && mode != CameraMode.Tabletop && speed > 18f)
            {
                float rumble = Mathf.InverseLerp(18f, 45f, speed) * (player.WheelsGrounded > 0 ? 1f : 0.2f);
                transform.rotation *= Quaternion.Euler(
                    (Mathf.PerlinNoise(Time.time * 37f, 1.3f) - 0.5f) * 0.7f * rumble,
                    (Mathf.PerlinNoise(2.1f, Time.time * 33f) - 0.5f) * 0.5f * rumble,
                    0f);
            }

            if (trauma > 0.001f)
            {
                float shake = trauma * trauma;
                transform.rotation *= Quaternion.Euler(
                    (Mathf.PerlinNoise(Time.unscaledTime * 28f, 0.3f) - 0.5f) * 6f * shake,
                    (Mathf.PerlinNoise(0.7f, Time.unscaledTime * 28f) - 0.5f) * 6f * shake,
                    (Mathf.PerlinNoise(Time.unscaledTime * 24f, 4.1f) - 0.5f) * 4f * shake);
                trauma = Mathf.Max(0f, trauma - Time.unscaledDeltaTime * 1.3f);
            }
        }

        /// <summary>Frames the whole pile-up and slowly orbits it, faster during impact time.</summary>
        private void FramePileUp(Transform target, out Vector3 desiredPosition, out Vector3 lookTarget)
        {
            Vector3 focus = target.position;
            int count = 1;
            foreach (DamageableVehicle vehicle in session.Vehicles)
            {
                if (vehicle == null || vehicle.IsPlayerVehicle) continue;
                TrafficVehicle traffic = vehicle.GetComponent<TrafficVehicle>();
                if (traffic == null || traffic.DriveEnabled) continue;
                focus += vehicle.transform.position;
                count++;
            }
            focus /= count;
            float spread = 0f;
            foreach (DamageableVehicle vehicle in session.Vehicles)
            {
                if (vehicle == null) continue;
                TrafficVehicle traffic = vehicle.GetComponent<TrafficVehicle>();
                if (vehicle.IsPlayerVehicle || (traffic != null && !traffic.DriveEnabled))
                {
                    spread = Mathf.Max(spread, Vector3.Distance(vehicle.transform.position, focus));
                }
            }

            orbitAngle += Time.unscaledDeltaTime * (session.IsImpactTime ? 16f : 7f);
            Vector3 away = Vector3.ProjectOnPlane(transform.position - focus, Vector3.up);
            if (away.sqrMagnitude < 1f) away = Vector3.back;
            away = Quaternion.Euler(0f, Time.unscaledDeltaTime * (session.IsImpactTime ? 16f : 7f), 0f) * away.normalized;
            float distance = Mathf.Clamp(7f + spread * 1.3f, 9f, 26f);
            float lift = Mathf.Clamp(3.5f + spread * 0.8f, 4.5f, 18f);
            desiredPosition = focus + away * distance + Vector3.up * lift;
            lookTarget = focus + Vector3.up * 0.5f;

            // Never sit inside a building: pull the camera in front of whatever blocks the view.
            if (Physics.Linecast(lookTarget, desiredPosition, out RaycastHit blocker, 1 << 0, QueryTriggerInteraction.Ignore))
            {
                desiredPosition = blocker.point + blocker.normal * 0.6f;
            }
        }
    }
}

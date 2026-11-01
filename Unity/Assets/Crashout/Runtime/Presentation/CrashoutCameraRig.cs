using Crashout.Simulation;
using Crashout.Vehicle;
using UnityEngine;

namespace Crashout.Presentation
{
    [RequireComponent(typeof(Camera))]
    public sealed class CrashoutCameraRig : MonoBehaviour
    {
        [SerializeField] private CameraMode mode = CameraMode.Chase;
        [SerializeField] private Vector3 boardCenter = Vector3.zero;

        private CrashoutGameSession session;
        private ArcadeVehicleController player;
        private Camera gameCamera;
        private Vector3 velocity;
        private float trauma;

        public CameraMode Mode => mode;

        private void Awake()
        {
            gameCamera = GetComponent<Camera>();
            gameCamera.fieldOfView = 58f;
            gameCamera.nearClipPlane = 0.05f;
            gameCamera.farClipPlane = 180f;
        }

        public void Configure(CrashoutGameSession gameSession, ArcadeVehicleController target)
        {
            session = gameSession;
            player = target;
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
            bool crashCamera = (session.Phase == GamePhase.Crash || session.Phase == GamePhase.SettleDelay)
                && mode != CameraMode.Tabletop;
            Vector3 desiredPosition;
            Vector3 lookTarget;

            if (crashCamera)
            {
                // Frame the whole pile-up, not just the player's wreck.
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

                Vector3 away = Vector3.ProjectOnPlane(transform.position - focus, Vector3.up);
                if (away.sqrMagnitude < 1f) away = Vector3.back;
                away.Normalize();
                float distance = Mathf.Clamp(7f + spread * 1.3f, 9f, 26f);
                float lift = Mathf.Clamp(4.5f + spread * 0.9f, 6f, 20f);
                desiredPosition = focus + away * distance + Vector3.up * lift;
                lookTarget = focus + Vector3.up * 0.5f;

                // Never sit inside a building: pull the camera in front of whatever blocks the view.
                if (Physics.Linecast(lookTarget, desiredPosition, out RaycastHit blocker, 1 << 0, QueryTriggerInteraction.Ignore))
                {
                    desiredPosition = blocker.point + blocker.normal * 0.6f;
                }
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
                        desiredPosition = target.TransformPoint(0f, 0.73f, 0.88f);
                        lookTarget = target.position + target.forward * 15f + Vector3.up * 0.62f;
                        break;
                    case CameraMode.Overhead:
                        desiredPosition = target.position - target.forward * 3f + Vector3.up * 13.5f;
                        lookTarget = target.position + target.forward * 4f;
                        break;
                    default:
                        desiredPosition = target.position - target.forward * 6.8f + Vector3.up * 3.4f;
                        lookTarget = target.position + target.forward * 4.2f + Vector3.up * 0.5f;
                        break;
                }
            }

            float smoothTime = mode == CameraMode.FirstPerson ? 0.025f : 0.13f;
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, smoothTime);
            Quaternion desiredRotation = Quaternion.LookRotation(lookTarget - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, 1f - Mathf.Exp(-12f * Time.deltaTime));

            float speed = player.SpeedMetresPerSecond;
            float targetFov = mode == CameraMode.Tabletop ? 50f : 58f + Mathf.Clamp(speed, 0f, 24f) * 0.55f;
            gameCamera.fieldOfView = Mathf.Lerp(gameCamera.fieldOfView, targetFov, 1f - Mathf.Exp(-4f * Time.deltaTime));

            if (trauma > 0.001f)
            {
                float shake = trauma * trauma;
                transform.rotation *= Quaternion.Euler(
                    (Mathf.PerlinNoise(Time.time * 28f, 0.3f) - 0.5f) * 5f * shake,
                    (Mathf.PerlinNoise(0.7f, Time.time * 28f) - 0.5f) * 5f * shake,
                    (Mathf.PerlinNoise(Time.time * 24f, 4.1f) - 0.5f) * 3f * shake);
                trauma = Mathf.Max(0f, trauma - Time.deltaTime * 1.4f);
            }
        }
    }
}

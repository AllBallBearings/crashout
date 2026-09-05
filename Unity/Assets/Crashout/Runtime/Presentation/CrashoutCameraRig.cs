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
                float hold = Mathf.Max(4f, session.CrashElapsed - session.TimeSinceLastScore + 1.3f);
                float widen = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(hold, hold + 2.5f, session.CrashElapsed));
                Vector3 close = target.position - target.forward * 5.2f + Vector3.up * 2.9f;
                Vector3 wide = target.position - target.forward * 10f + Vector3.up * 10.5f;
                desiredPosition = Vector3.Lerp(close, wide, widen);
                lookTarget = target.position + Vector3.up * 0.45f;
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
        }
    }
}

using Crashout.Simulation;
using UnityEngine;

namespace Crashout.Presentation
{
    /// <summary>A spinning red marker floating over the scenario's target vehicle.</summary>
    public sealed class TargetMarker : MonoBehaviour
    {
        private CrashoutGameSession session;
        private Transform vehicle;
        private uint entityId;
        private float height;

        public void Configure(CrashoutGameSession gameSession, uint id, Transform target, float markerHeight)
        {
            session = gameSession;
            entityId = id;
            vehicle = target;
            height = markerHeight;
        }

        private void LateUpdate()
        {
            if (vehicle == null || session == null) return;
            bool visible = !session.TargetDown && session.Phase != GamePhase.Results;
            foreach (Renderer markerRenderer in GetComponentsInChildren<Renderer>(true))
            {
                markerRenderer.enabled = visible;
            }
            // Stay upright above the car even when it tumbles.
            transform.position = vehicle.position + Vector3.up * (height + Mathf.Sin(Time.time * 3f) * 0.12f);
            transform.rotation = Quaternion.Euler(0f, Time.time * 140f, 0f);
        }
    }
}

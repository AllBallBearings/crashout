using System.Collections.Generic;
using Crashout.Simulation;
using UnityEngine;

namespace Crashout.Presentation
{
    public sealed class PresentationSynchronizer : MonoBehaviour
    {
        private sealed class ProxyState
        {
            public Transform Transform;
            public EntitySnapshot Previous;
            public EntitySnapshot Latest;
            public float ReceivedAt;
        }

        [SerializeField] private ArBoardCoordinator board;
        [SerializeField, Range(0.05f, 0.2f)] private float interpolationBackTime = 0.1f;
        private readonly Dictionary<uint, ProxyState> proxies = new Dictionary<uint, ProxyState>();

        public void Configure(ArBoardCoordinator boardCoordinator)
        {
            board = boardCoordinator;
        }

        public void RegisterProxy(uint entityId, Transform proxy)
        {
            proxies[entityId] = new ProxyState { Transform = proxy };
        }

        public void ApplySnapshot(IReadOnlyList<EntitySnapshot> snapshot)
        {
            for (int i = 0; i < snapshot.Count; i++)
            {
                EntitySnapshot state = snapshot[i];
                if (!proxies.TryGetValue(state.EntityId, out ProxyState proxy)) continue;
                proxy.Previous = proxy.Latest.HostTick == 0 ? state : proxy.Latest;
                proxy.Latest = state;
                proxy.ReceivedAt = Time.unscaledTime;
            }
        }

        private void LateUpdate()
        {
            if (board == null || !board.IsPlaced) return;
            foreach (ProxyState proxy in proxies.Values)
            {
                if (proxy.Latest.HostTick == 0 || proxy.Transform == null) continue;
                float t = Mathf.Clamp01((Time.unscaledTime - proxy.ReceivedAt) / interpolationBackTime);
                Vector3 canonicalPosition = Vector3.Lerp(proxy.Previous.Position, proxy.Latest.Position, t);
                Quaternion canonicalRotation = Quaternion.Slerp(proxy.Previous.Rotation, proxy.Latest.Rotation, t);
                proxy.Transform.SetPositionAndRotation(
                    board.CanonicalToTabletopPosition(canonicalPosition),
                    board.CanonicalToTabletopRotation(canonicalRotation));
                proxy.Transform.localScale = Vector3.one * board.PresentationScale;
            }
        }
    }
}

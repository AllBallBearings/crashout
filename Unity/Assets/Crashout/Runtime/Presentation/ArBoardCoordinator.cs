using System;
using UnityEngine;

namespace Crashout.Presentation
{
    public enum BoardTrackingState : byte
    {
        Unavailable,
        Scanning,
        Relocalizing,
        Tracking,
        Limited
    }

    public sealed class ArBoardCoordinator : MonoBehaviour
    {
        public const float DefaultPresentationScale = 1f / 32f;

        [SerializeField, Range(1f / 40f, 1f / 24f)] private float presentationScale = DefaultPresentationScale;
        [SerializeField] private BoardTrackingState trackingState = BoardTrackingState.Unavailable;

        public float PresentationScale => presentationScale;
        public BoardTrackingState TrackingState => trackingState;
        public bool IsPlaced { get; private set; }

        public event Action<BoardTrackingState> TrackingChanged;

        public void Place(Pose tabletopPose, float selectedScale)
        {
            transform.SetPositionAndRotation(tabletopPose.position, tabletopPose.rotation);
            presentationScale = Mathf.Clamp(selectedScale, 1f / 40f, 1f / 24f);
            IsPlaced = true;
            SetTrackingState(BoardTrackingState.Tracking);
        }

        public Vector3 CanonicalToTabletopPosition(Vector3 canonicalPosition)
        {
            return transform.TransformPoint(canonicalPosition * presentationScale);
        }

        public Quaternion CanonicalToTabletopRotation(Quaternion canonicalRotation)
        {
            return transform.rotation * canonicalRotation;
        }

        public void SetTrackingState(BoardTrackingState state)
        {
            if (trackingState == state) return;
            trackingState = state;
            TrackingChanged?.Invoke(trackingState);
        }
    }
}

using UnityEngine;

namespace Crashout.Presentation
{
    /// <summary>Playback timing independent of Unity's simulation and AR tracking clocks.</summary>
    public sealed class ReplayPlaybackClock
    {
        public float Duration { get; }
        public float Position { get; private set; }
        public float Speed { get; private set; } = 1f;
        public bool IsPlaying { get; private set; }

        public ReplayPlaybackClock(float duration) { Duration = Mathf.Max(0f, duration); }

        public void Toggle()
        {
            if (Position >= Duration) Seek(0f);
            IsPlaying = !IsPlaying && Duration > 0f;
        }

        public void Pause() { IsPlaying = false; }

        public void SetSpeed(float speed)
        {
            if (speed == 0.25f || speed == 0.5f || speed == 1f || speed == 2f) Speed = speed;
        }

        public void Seek(float seconds)
        {
            if (!float.IsNaN(seconds) && !float.IsInfinity(seconds)) Position = Mathf.Clamp(seconds, 0f, Duration);
        }

        public void Advance(float seconds)
        {
            if (!IsPlaying || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            Seek(Position + Mathf.Max(0f, seconds) * Speed);
            if (Position >= Duration) Pause();
        }
    }
}

using System;
using System.Collections.Generic;
using Crashout.Simulation;
using UnityEngine;

namespace Crashout.Presentation
{
    /// <summary>
    /// One in-memory recording per turn, from launch through the final settled frame. Playback uses
    /// an unscaled clock and render-only geometry; the AR session and device camera remain live.
    /// The recording is replaced on retry/scenario change, never accumulated across turns.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class TurnReplay : MonoBehaviour
    {
        public const float SampleInterval = 1f / 20f;
        private struct Moment
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public int Score;
            public GamePhase Phase;
        }

        public readonly struct Cue
        {
            public readonly float Time;
            public readonly string Text;
            public readonly Vector3 Position;
            public Cue(float time, string text, Vector3 position) { Time = time; Text = text; Position = position; }
        }

        private CrashoutGameSession session;
        private ReplayWorld world;
        private ReplayTimeline<Moment> moments = new ReplayTimeline<Moment>();
        private readonly List<Cue> cues = new List<Cue>();
        private bool recording, finishRequested;
        private float startedAt, nextSampleAt, savedTimeScale;
        private bool savedAudioPause;
        private GameObject focus;
        private ReplayPlaybackClock clock;

        public bool IsAvailable { get; private set; }
        public bool IsActive { get; private set; }
        public bool IsPlaying => IsActive && clock != null && clock.IsPlaying;
        public float Duration { get; private set; }
        public float Position => clock != null ? clock.Position : 0f;
        public float Speed => clock != null ? clock.Speed : 1f;
        public int Score { get; private set; }
        public GamePhase RecordedPhase { get; private set; }
        public Transform Focus => focus != null ? focus.transform : null;
        public IReadOnlyList<Cue> Cues => cues;

        public void Configure(CrashoutGameSession gameSession)
        {
            session = gameSession;
            session.PhaseChanged += OnPhase;
            session.ScoreChanged += OnScore;
            session.BonusAwarded += OnBonus;
        }

        private void OnPhase(GamePhase phase)
        {
            if (phase == GamePhase.Driving)
            {
                Clear();
                world = new ReplayWorld();
                recording = true;
                startedAt = Time.unscaledTime;
                Capture(0f);
                nextSampleAt = SampleInterval;
            }
            else if (phase == GamePhase.Results && recording) finishRequested = true;
        }

        private void Capture(float time)
        {
            world.Capture(time, finishRequested);
            Transform player = session.Player.transform;
            moments.Add(time, new Moment { Position = player.position, Rotation = player.rotation, Score = session.Score, Phase = session.Phase });
            Duration = time;
        }

        private void LateUpdate()
        {
            if (recording)
            {
                float time = Time.unscaledTime - startedAt;
                if (time >= nextSampleAt || finishRequested)
                {
                    Capture(time);
                    nextSampleAt = time + SampleInterval;
                }
                if (finishRequested)
                {
                    recording = finishRequested = false;
                    IsAvailable = Duration > 0f;
                }
            }
            if (!IsActive) return;
            Advance(Time.unscaledDeltaTime);
            // Visual scripts can keep animating on the live world; only replay proxies are drawn.
            world.HideLiveWorld();
        }

        public bool Open()
        {
            if (!IsAvailable || IsActive || session.Phase != GamePhase.Results) return false;
            savedTimeScale = Time.timeScale;
            savedAudioPause = AudioListener.pause;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            IsActive = true;
            clock = new ReplayPlaybackClock(Duration);
            focus = new GameObject("Replay camera focus");
            try
            {
                world.Show();
                Seek(0f);
                clock.Toggle();
                return true;
            }
            catch
            {
                Close();
                throw;
            }
        }

        public void Close()
        {
            if (!IsActive) return;
            IsActive = false;
            clock?.Pause();
            world.Hide();
            if (focus != null) Destroy(focus);
            focus = null;
            Time.timeScale = savedTimeScale;
            AudioListener.pause = savedAudioPause;
        }

        public void TogglePlayback()
        {
            if (!IsActive) return;
            clock.Toggle();
            Seek(clock.Position);
        }

        public void Pause() { clock?.Pause(); }

        public void SetSpeed(float speed)
        {
            clock?.SetSpeed(speed);
        }

        public void Advance(float unscaledSeconds)
        {
            if (!IsActive || !IsPlaying) return;
            clock.Advance(unscaledSeconds);
            Seek(clock.Position);
        }

        public void Seek(float seconds)
        {
            if (!IsActive || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            clock.Seek(seconds);
            world.Seek(Position);
            if (!moments.Sample(Position, out Moment a, out Moment b, out float blend)) return;
            focus.transform.SetPositionAndRotation(Vector3.Lerp(a.Position, b.Position, blend), Quaternion.Slerp(a.Rotation, b.Rotation, blend));
            Score = a.Score;
            RecordedPhase = a.Phase;
        }

        private void OnScore(ScoreEvent score)
        {
            if (!recording || score.Amount <= 0 || !session.TryGetVehiclePosition(score.EntityId, out Vector3 position)) return;
            cues.Add(new Cue(Time.unscaledTime - startedAt, "+$" + score.Amount.ToString("N0"), position + Vector3.up * 1.2f));
        }

        private void OnBonus(string label, int amount, Vector3 position)
        {
            if (!recording) return;
            cues.Add(new Cue(Time.unscaledTime - startedAt, amount > 0 ? label + "  +$" + amount.ToString("N0") : label, position));
        }

        public void Clear()
        {
            Close();
            recording = finishRequested = IsAvailable = false;
            world?.Dispose();
            world = null;
            moments = new ReplayTimeline<Moment>();
            cues.Clear();
            Duration = 0f;
            clock = null;
        }

        private void OnDisable() { Clear(); }

        private void OnDestroy()
        {
            Clear();
            if (session == null) return;
            session.PhaseChanged -= OnPhase;
            session.ScoreChanged -= OnScore;
            session.BonusAwarded -= OnBonus;
        }
    }
}

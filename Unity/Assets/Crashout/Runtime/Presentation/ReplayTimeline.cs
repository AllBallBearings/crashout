using System.Collections.Generic;
using UnityEngine;

namespace Crashout.Presentation
{
    /// <summary>A sparse, seekable track. Discrete changes use the earlier key, poses interpolate.</summary>
    public sealed class ReplayTimeline<T>
    {
        public readonly struct Key
        {
            public readonly float Time;
            public readonly T Value;
            public Key(float time, T value) { Time = time; Value = value; }
        }

        private readonly List<Key> keys = new List<Key>();
        public int Count => keys.Count;
        internal IReadOnlyList<Key> Keys => keys;
        public Key Last => keys[keys.Count - 1];

        public void Add(float time, T value)
        {
            if (keys.Count > 0 && time < Last.Time) throw new System.ArgumentOutOfRangeException(nameof(time));
            if (keys.Count > 0 && time == Last.Time) keys[keys.Count - 1] = new Key(time, value);
            else keys.Add(new Key(time, value));
        }

        // Reduce cosmetic particle density on unusually long turns, retaining births and deaths.
        internal void Compact(System.Func<T, T, bool> transition)
        {
            for (int i = keys.Count - 2; i > 0; i--)
            {
                if (i % 2 == 0 || transition(keys[i - 1].Value, keys[i].Value) || transition(keys[i].Value, keys[i + 1].Value)) continue;
                keys.RemoveAt(i);
            }
        }

        public bool Sample(float time, out T earlier, out T later, out float blend)
        {
            earlier = later = default;
            blend = 0f;
            if (keys.Count == 0 || time < keys[0].Time) return false;
            int lo = 0, hi = keys.Count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (keys[mid].Time <= time) lo = mid;
                else hi = mid - 1;
            }
            Key a = keys[lo], b = keys[Mathf.Min(lo + 1, keys.Count - 1)];
            earlier = a.Value;
            later = b.Value;
            blend = b.Time > a.Time ? Mathf.Clamp01((time - a.Time) / (b.Time - a.Time)) : 0f;
            return true;
        }
    }
}

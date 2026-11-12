using UnityEngine;

namespace Crashout.Presentation
{
    /// <summary>
    /// The final mix stage, running on the listener: a cinema-style low-end lift, gentle saturation
    /// for weight, and a fast limiter so stacked impacts and explosions hit hard without clipping.
    /// <see cref="Slam"/> lets a blast momentarily push the whole mix down, the way a film mix ducks
    /// everything under its biggest moment.
    /// </summary>
    public sealed class MasterBus : MonoBehaviour
    {
        private const float Ceiling = 0.94f;

        private float sampleRate = 48000f;
        private readonly float[] lowState = new float[8];
        private float envelope;
        private float duck = 1f;
        private volatile float duckTarget = 1f;
        private volatile float duckHoldSamples;

        private void Awake()
        {
            sampleRate = AudioSettings.outputSampleRate;
        }

        /// <summary>Pushes the mix down by <paramref name="depth"/> (0..1) for a moment; it recovers by itself.</summary>
        public void Slam(float depth, float holdSeconds)
        {
            duckTarget = Mathf.Clamp(1f - depth, 0.25f, 1f);
            duckHoldSamples = holdSeconds * sampleRate;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            float shelf = 1f - Mathf.Exp(-2f * Mathf.PI * 105f / sampleRate);
            float attack = 1f - Mathf.Exp(-1f / (0.0015f * sampleRate));
            float release = 1f - Mathf.Exp(-1f / (0.16f * sampleRate));
            float duckDown = 1f - Mathf.Exp(-1f / (0.012f * sampleRate));
            float duckUp = 1f - Mathf.Exp(-1f / (0.7f * sampleRate));
            int usable = Mathf.Min(channels, lowState.Length);

            for (int frame = 0; frame < data.Length; frame += channels)
            {
                float target = duckTarget;
                if (duckHoldSamples > 0f) duckHoldSamples -= 1f;
                else target = duckTarget = 1f;
                duck += (target - duck) * (target < duck ? duckDown : duckUp);

                float peak = 0f;
                for (int c = 0; c < usable; c++)
                {
                    float x = data[frame + c] * duck;
                    // Low shelf: about +4 dB under 100 Hz.
                    lowState[c] += shelf * (x - lowState[c]);
                    x += lowState[c] * 0.55f;
                    // Soft saturation adds density before the limiter has to work.
                    x *= 1.12f;
                    float ax = x < 0f ? -x : x;
                    if (ax > 0.5f) x = (x < 0f ? -1f : 1f) * (0.5f + (ax - 0.5f) / (1f + (ax - 0.5f) * 1.3f));
                    data[frame + c] = x;
                    ax = x < 0f ? -x : x;
                    if (ax > peak) peak = ax;
                }

                envelope += (peak - envelope) * (peak > envelope ? attack : release);
                float gain = envelope > Ceiling ? Ceiling / envelope : 1f;
                for (int c = 0; c < usable; c++)
                {
                    float y = data[frame + c] * gain;
                    data[frame + c] = y > 1f ? 1f : y < -1f ? -1f : y;
                }
            }
        }
    }
}

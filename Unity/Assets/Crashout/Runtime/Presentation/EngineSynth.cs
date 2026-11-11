using UnityEngine;

namespace Crashout.Presentation
{
    /// <summary>
    /// Real-time V8 engine synthesizer running on the audio thread (OnAudioFilterRead), so the note
    /// follows RPM continuously instead of pitch-shifting a loop. The model: firing-order harmonics
    /// with a cross-plane half-order burble, per-cycle combustion jitter, load-dependent spectral tilt
    /// and drive, an exhaust resonance, induction noise, a supercharger whine under boost, overrun
    /// crackle off-throttle and a rev-limiter stutter. Main thread writes the parameters; the audio
    /// thread reads them (single floats, so tearing is not a concern).
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class EngineSynth : MonoBehaviour
    {
        private const int Harmonics = 12;
        private const int TableSize = 4096;
        private static readonly float[] Sine = BuildSine();

        // Parameters set from the main thread.
        public float Rpm = 900f;
        public float Load;
        public float Boost;
        public float Volume;
        public float Redline = 7400f;

        private float sampleRate = 48000f;
        private float rpm = 900f;
        private float load;
        private float boost;
        private float volume;
        private readonly float[] phases = new float[Harmonics + 3];
        private readonly float[] harmonicGain = new float[Harmonics];
        private float cyclePhase;
        private float jitter = 1f;
        private float low, band;
        private float intake;
        private float crackle;
        private float limiterPhase;
        private uint noiseState = 22222u;

        private static float[] BuildSine()
        {
            var table = new float[TableSize + 1];
            for (int i = 0; i <= TableSize; i++) table[i] = Mathf.Sin(i / (float)TableSize * Mathf.PI * 2f);
            return table;
        }

        private void Awake()
        {
            sampleRate = AudioSettings.outputSampleRate;
            AudioSource source = GetComponent<AudioSource>();
            // A silent constant clip keeps the filter chain running; we write the signal ourselves.
            AudioClip carrier = AudioClip.Create("Engine carrier", 1024, 1, (int)sampleRate, false);
            float[] ones = new float[1024];
            for (int i = 0; i < ones.Length; i++) ones[i] = 1f;
            carrier.SetData(ones, 0);
            source.clip = carrier;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.Play();
        }

        private float Noise()
        {
            noiseState ^= noiseState << 13;
            noiseState ^= noiseState >> 17;
            noiseState ^= noiseState << 5;
            return (noiseState & 0xffffff) / 8388607.5f - 1f;
        }

        private static float Sin(float phase)
        {
            float index = phase * TableSize;
            int i = (int)index;
            float frac = index - i;
            i &= TableSize - 1;
            return Sine[i] + (Sine[i + 1] - Sine[i]) * frac;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            float targetRpm = Mathf.Clamp(Rpm, 600f, Redline + 200f);
            float targetLoad = Mathf.Clamp01(Load);
            float targetBoost = Mathf.Clamp01(Boost);
            float targetVolume = Mathf.Clamp01(Volume);
            float rpmSmooth = 1f - Mathf.Exp(-1f / (0.035f * sampleRate));
            float paramSmooth = 1f - Mathf.Exp(-1f / (0.02f * sampleRate));
            // Spectral tilt brightens under load; recomputed once per buffer.
            float tilt = Mathf.Lerp(1.45f, 0.75f, load);
            float gain = 1f;
            for (int h = 0; h < Harmonics; h++)
            {
                harmonicGain[h] = gain;
                gain *= Mathf.Pow((h + 1f) / (h + 2f), tilt);
            }

            for (int frame = 0; frame < data.Length; frame += channels)
            {
                rpm += (targetRpm - rpm) * rpmSmooth;
                load += (targetLoad - load) * paramSmooth;
                boost += (targetBoost - boost) * paramSmooth;
                volume += (targetVolume - volume) * paramSmooth;

                float crankHz = rpm / 60f;
                float fire = crankHz * 4f; // V8: four firing events per revolution.
                cyclePhase += crankHz * 0.5f / sampleRate;
                if (cyclePhase >= 1f)
                {
                    cyclePhase -= 1f;
                    jitter = 1f + Noise() * (0.06f + (1f - load) * 0.1f);
                }

                float sum = 0f;
                for (int h = 0; h < Harmonics; h++)
                {
                    phases[h] += fire * (h + 1) / sampleRate;
                    if (phases[h] >= 1f) phases[h] -= 1f;
                    sum += Sin(phases[h]) * harmonicGain[h];
                }
                // Cross-plane burble: half and one-and-a-half order components.
                phases[Harmonics] += fire * 0.5f / sampleRate;
                if (phases[Harmonics] >= 1f) phases[Harmonics] -= 1f;
                phases[Harmonics + 1] += fire * 1.5f / sampleRate;
                if (phases[Harmonics + 1] >= 1f) phases[Harmonics + 1] -= 1f;
                sum += Sin(phases[Harmonics]) * Mathf.Lerp(0.7f, 0.35f, load) + Sin(phases[Harmonics + 1]) * 0.25f;
                sum *= jitter;

                float drive = 1.1f + load * 2.2f;
                float x = Tanh(sum * drive * 0.45f);

                // Exhaust resonance (state-variable band/low mix).
                float cutoff = Mathf.Min(0.45f, (260f + 2600f * load + rpm * 0.18f) / sampleRate * 2f * Mathf.PI);
                low += cutoff * band;
                float high = x - low - 0.9f * band;
                band += cutoff * high;
                float exhaust = low * 0.9f + band * 0.35f;

                // Induction roar.
                intake += 0.25f * (Noise() - intake);
                float induction = intake * load * (rpm / Redline) * 0.35f * (0.6f + 0.4f * Sin(phases[0]));

                // Supercharger whine under boost.
                phases[Harmonics + 2] += (1200f + boost * 3800f + rpm * 0.35f) / sampleRate;
                if (phases[Harmonics + 2] >= 1f) phases[Harmonics + 2] -= 1f;
                float whine = Sin(phases[Harmonics + 2]) * boost * 0.07f;

                // Overrun pops and crackle when lifting off at high revs.
                if (load < 0.12f && rpm > 3200f && Noise() > 0.9994f) crackle = 0.9f + Noise() * 0.3f;
                float pop = crackle * Noise();
                crackle *= 0.9965f;

                // Rev limiter.
                float limiter = 1f;
                if (rpm > Redline - 60f && load > 0.5f)
                {
                    limiterPhase += 28f / sampleRate;
                    if (limiterPhase >= 1f) limiterPhase -= 1f;
                    limiter = limiterPhase < 0.5f ? 1f : 0.35f;
                }

                float sample = (exhaust * 0.9f + induction + whine + pop * 0.6f) * limiter * volume;
                for (int c = 0; c < channels; c++)
                {
                    // The carrier is 1.0, so we replace rather than modulate it.
                    data[frame + c] = sample;
                }
            }
        }

        private static float Tanh(float x)
        {
            if (x > 3f) return 1f;
            if (x < -3f) return -1f;
            float x2 = x * x;
            return x * (27f + x2) / (27f + 9f * x2);
        }
    }
}

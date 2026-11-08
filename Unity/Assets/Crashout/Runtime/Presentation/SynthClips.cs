using UnityEngine;

namespace Crashout.Presentation
{
    /// <summary>
    /// Procedural sound effects, so the game ships audio without imported assets.
    /// Everything is generated once at startup, mono at 22.05 kHz.
    /// </summary>
    public static class SynthClips
    {
        private const int Rate = 22050;

        // ------------------------------------------------------------------ impacts

        /// <summary>Metal crunch: filtered noise, a thump and ringing panel partials.</summary>
        public static AudioClip Crunch(bool heavy, int seed)
        {
            float length = heavy ? 1.0f : 0.6f;
            float[] d = Buffer(length);
            var random = new System.Random(seed);
            float noiseCoeff = heavy ? 0.12f : 0.32f;
            float noiseDecay = heavy ? 4.5f : 8.5f;
            float thumpHz = heavy ? 46f : 72f;
            float low = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                low += noiseCoeff * (NoiseSample(random) - low);
                float noise = low * Mathf.Exp(-t * noiseDecay) * 3.2f;
                float thump = Mathf.Sin(2f * Mathf.PI * thumpHz * t) * Mathf.Exp(-t * (heavy ? 9f : 16f)) * 0.9f;
                float ring = (Mathf.Sin(2f * Mathf.PI * 190f * t) + Mathf.Sin(2f * Mathf.PI * 317f * t) * 0.7f + Mathf.Sin(2f * Mathf.PI * 471f * t) * 0.5f)
                             * Mathf.Exp(-t * 13f) * 0.16f;
                d[i] = noise + thump + ring;
            }
            return Finish(heavy ? "Crunch heavy" : "Crunch light", d, 0.9f);
        }

        /// <summary>Glass and small debris: bright, short, scattered tinks.</summary>
        public static AudioClip Glass(int seed)
        {
            float[] d = Buffer(0.55f);
            var random = new System.Random(seed);
            float low = 0f;
            float[] starts = { 0f, 0.05f + (float)random.NextDouble() * 0.05f, 0.14f + (float)random.NextDouble() * 0.08f };
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                low += 0.35f * (NoiseSample(random) - low);
                float bright = (NoiseSample(random) - low) * 0.5f;
                float sum = 0f;
                for (int k = 0; k < starts.Length; k++)
                {
                    float local = t - starts[k];
                    if (local < 0f) continue;
                    float env = Mathf.Exp(-local * (22f + k * 6f));
                    sum += (bright + Mathf.Sin(2f * Mathf.PI * (2900f + k * 700f) * local) * 0.35f) * env;
                }
                d[i] = sum;
            }
            return Finish("Glass", d, 0.7f);
        }

        public static AudioClip Explosion(int seed)
        {
            float length = 2.4f;
            float[] d = Buffer(length);
            var random = new System.Random(seed);
            float low = 0f;
            float phase = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float progress = t / length;
                float coeff = Mathf.Lerp(0.55f, 0.015f, Mathf.Sqrt(progress));
                low += coeff * (NoiseSample(random) - low);
                float attack = Mathf.Clamp01(t / 0.008f);
                float noise = low * attack * Mathf.Exp(-t * 1.5f) * 3.4f;
                float hz = Mathf.Lerp(62f, 26f, Mathf.Clamp01(t / 1.4f));
                phase += 2f * Mathf.PI * hz / Rate;
                float sub = Mathf.Sin(phase) * attack * Mathf.Exp(-t * 1.25f) * 1.0f;
                float crack = NoiseSample(random) * Mathf.Exp(-t * 38f) * 0.8f;
                d[i] = noise + sub + crack;
            }
            return Finish("Explosion", d, 0.95f);
        }

        /// <summary>Low sweep for the impact-time slow motion.</summary>
        public static AudioClip SlowMotion()
        {
            float[] d = Buffer(0.8f);
            var random = new System.Random(5);
            float low = 0f;
            float phase = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float hz = Mathf.Lerp(140f, 38f, Mathf.Clamp01(t / 0.7f));
                phase += 2f * Mathf.PI * hz / Rate;
                low += 0.05f * (NoiseSample(random) - low);
                d[i] = (Mathf.Sin(phase) * 0.8f + low * 2f) * Mathf.Exp(-t * 3.2f);
            }
            return Finish("Slow motion", d, 0.8f);
        }

        // ------------------------------------------------------------------ loops

        /// <summary>One second at 55 Hz, so it loops seamlessly; the source pitch follows speed.</summary>
        public static AudioClip EngineLoop()
        {
            float[] d = Buffer(1f);
            var random = new System.Random(11);
            float low = 0f;
            const float f0 = 55f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float sum = 0f;
                for (int h = 1; h <= 9; h++)
                {
                    sum += Mathf.Sin(2f * Mathf.PI * f0 * h * t + h * 0.7f) / Mathf.Pow(h, 0.85f);
                }
                low += 0.08f * (NoiseSample(random) - low);
                // Integer-cycle modulation keeps the loop point clean while giving the engine a rumble.
                float rumble = 0.78f + 0.22f * Mathf.Sin(2f * Mathf.PI * 11f * t);
                d[i] = (float)System.Math.Tanh(sum * 0.9f) * 0.55f * rumble + low * 0.35f;
            }
            return Finish("Engine loop", d, 0.7f, true);
        }

        public static AudioClip BoostLoop()
        {
            float[] d = Buffer(1f);
            var random = new System.Random(23);
            float slow = 0f, fast = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float x = NoiseSample(random);
                slow += 0.03f * (x - slow);
                fast += 0.22f * (x - fast);
                float band = fast - slow;
                d[i] = band * (0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 9f * t)) * 2.4f;
            }
            return Finish("Boost loop", d, 0.7f, true);
        }

        public static AudioClip AmbientLoop()
        {
            float[] d = Buffer(3f);
            var random = new System.Random(31);
            float low = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                low += 0.012f * (NoiseSample(random) - low);
                d[i] = low * (0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * t / 3f * 2f)) * 4f;
            }
            return Finish("City ambience", d, 0.5f, true);
        }

        // ------------------------------------------------------------------ interface and rewards

        public static AudioClip Click()
        {
            float[] d = Buffer(0.06f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                d[i] = Mathf.Sin(2f * Mathf.PI * 880f * t) * Mathf.Exp(-t * 75f);
            }
            return Finish("Click", d, 0.6f);
        }

        public static AudioClip Tick(float hz)
        {
            float[] d = Buffer(0.05f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                d[i] = Mathf.Sin(2f * Mathf.PI * hz * t) * Mathf.Exp(-t * 85f);
            }
            return Finish("Tick", d, 0.5f);
        }

        public static AudioClip Chime(string name, float[] hz, float spacing, float length, float decay = 5f)
        {
            float[] d = Buffer(length);
            for (int n = 0; n < hz.Length; n++)
            {
                int start = Mathf.RoundToInt(n * spacing * Rate);
                for (int i = start; i < d.Length; i++)
                {
                    float t = (i - start) / (float)Rate;
                    float env = Mathf.Exp(-t * decay) * Mathf.Clamp01(t / 0.004f);
                    d[i] += (Mathf.Sin(2f * Mathf.PI * hz[n] * t) + Mathf.Sin(2f * Mathf.PI * hz[n] * 2f * t) * 0.35f) * env * 0.5f;
                }
            }
            return Finish(name, d, 0.75f);
        }

        /// <summary>Descending sawtooth "wah" for a bad launch or no medal.</summary>
        public static AudioClip Fail(string name, float startHz, float endHz, float length)
        {
            float[] d = Buffer(length);
            float phase = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float hz = Mathf.Lerp(startHz, endHz, t / length);
                phase = (phase + hz / Rate) % 1f;
                float saw = phase * 2f - 1f;
                d[i] = saw * Mathf.Clamp01(1f - t / length) * 0.5f;
            }
            return Finish(name, d, 0.6f);
        }

        public static AudioClip Horn()
        {
            float[] d = Buffer(0.6f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Clamp01(t / 0.02f) * Mathf.Clamp01((0.6f - t) / 0.12f);
                float a = 0f, b = 0f;
                for (int h = 1; h <= 4; h++)
                {
                    a += Mathf.Sin(2f * Mathf.PI * 392f * h * t) / h;
                    b += Mathf.Sin(2f * Mathf.PI * 494f * h * t) / h;
                }
                d[i] = (a + b) * 0.35f * env;
            }
            return Finish("Horn", d, 0.7f);
        }

        /// <summary>Rising sweep for the launch.</summary>
        public static AudioClip LaunchWhoosh()
        {
            float[] d = Buffer(0.9f);
            var random = new System.Random(47);
            float slow = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float p = t / 0.9f;
                slow += Mathf.Lerp(0.02f, 0.4f, p) * (NoiseSample(random) - slow);
                float env = Mathf.Sin(Mathf.Clamp01(p) * Mathf.PI) * 0.9f;
                d[i] = slow * env * 2.6f;
            }
            return Finish("Launch whoosh", d, 0.8f);
        }

        public static AudioClip ReadyBeep()
        {
            float[] d = Buffer(0.18f);
            float phase = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float hz = Mathf.Lerp(880f, 1480f, t / 0.18f);
                phase += 2f * Mathf.PI * hz / Rate;
                d[i] = Mathf.Sin(phase) * Mathf.Exp(-t * 11f);
            }
            return Finish("Ready beep", d, 0.6f);
        }

        // ------------------------------------------------------------------ helpers

        private static float[] Buffer(float seconds)
        {
            return new float[Mathf.Max(1, Mathf.RoundToInt(seconds * Rate))];
        }

        private static float NoiseSample(System.Random random)
        {
            return (float)(random.NextDouble() * 2.0 - 1.0);
        }

        private static AudioClip Finish(string name, float[] data, float peak, bool loop = false)
        {
            if (loop)
            {
                // Crossfade the tail into the head so the loop has no click.
                int fade = Mathf.Min(data.Length / 8, Rate / 10);
                for (int i = 0; i < fade; i++)
                {
                    float mix = i / (float)fade;
                    data[i] = Mathf.Lerp(data[data.Length - fade + i], data[i], mix);
                }
                System.Array.Resize(ref data, data.Length - fade);
            }

            float max = 0.0001f;
            for (int i = 0; i < data.Length; i++) max = Mathf.Max(max, Mathf.Abs(data[i]));
            float gain = peak / max;
            for (int i = 0; i < data.Length; i++) data[i] *= gain;

            AudioClip clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}

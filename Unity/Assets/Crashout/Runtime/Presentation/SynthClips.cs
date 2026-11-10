using System;
using UnityEngine;

namespace Crashout.Presentation
{
    /// <summary>
    /// Sound design synthesized in code at 44.1 kHz, so the game ships audio without imported assets.
    /// Impacts use modal synthesis (inharmonic panel resonances) with crumple grains and a body thump;
    /// glass is granular; explosions layer a crack, a sub boom, a rolling rumble and crackle.
    /// Each Render method fills a buffer and is thread-safe; <see cref="Clip"/> wraps it on the main thread.
    /// </summary>
    public static class SynthClips
    {
        public const int Rate = 44100;
        private const float TwoPi = Mathf.PI * 2f;

        // ------------------------------------------------------------------ crashes

        /// <summary>Car-on-car impact: body thump, ringing sheet-metal modes, crumple grains and a plastic crack.</summary>
        public static float[] RenderImpact(int seed, bool heavy)
        {
            var random = new System.Random(seed);
            float length = heavy ? 1.6f : 1.1f;
            float[] d = Buffer(length);

            // Thump: a pitched-down sine with a hard transient.
            float thumpHz = heavy ? Range(random, 38f, 52f) : Range(random, 58f, 84f);
            float phase = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float hz = thumpHz * (1f + 1.4f * Mathf.Exp(-t * 30f));
                phase += TwoPi * hz / Rate;
                d[i] += Mathf.Sin(phase) * Mathf.Exp(-t * (heavy ? 7f : 12f)) * 1.1f;
            }

            // Panel modes: log-spaced, inharmonic, higher modes die faster.
            int modes = heavy ? 22 : 18;
            for (int m = 0; m < modes; m++)
            {
                float hz = Mathf.Exp(Range(random, Mathf.Log(heavy ? 95f : 160f), Mathf.Log(heavy ? 2600f : 4200f)));
                float decay = Mathf.Lerp(3.5f, 40f, Mathf.InverseLerp(100f, 4200f, hz)) * Range(random, 0.7f, 1.4f);
                float amplitude = Range(random, 0.08f, 0.3f) * (hz < 600f ? 1.4f : 1f);
                float p = Range(random, 0f, TwoPi);
                int end = Mathf.Min(d.Length, Mathf.CeilToInt(Rate * 6.5f / decay));
                float step = TwoPi * hz / Rate;
                for (int i = 0; i < end; i++)
                {
                    float t = i / (float)Rate;
                    d[i] += Mathf.Sin(p + step * i) * amplitude * Mathf.Exp(-t * decay) * Mathf.Clamp01(t * 800f);
                }
            }

            // Crumple: dozens of tiny noise bursts with bright, varying colour.
            int grains = heavy ? 150 : 90;
            float spread = heavy ? 0.55f : 0.32f;
            for (int g = 0; g < grains; g++)
            {
                float start = -Mathf.Log(1f - (float)random.NextDouble() * 0.98f) * spread * 0.35f;
                int s0 = Mathf.RoundToInt(start * Rate);
                float grainLength = Range(random, 0.002f, 0.009f);
                float gain = Range(random, 0.15f, 0.55f) * Mathf.Exp(-start * 3f);
                float colour = Range(random, 0.15f, 0.7f);
                float low = 0f;
                int n = Mathf.RoundToInt(grainLength * Rate);
                for (int i = 0; i < n && s0 + i < d.Length; i++)
                {
                    float x = Noise(random);
                    low += colour * (x - low);
                    float env = Mathf.Sin(Mathf.PI * i / n);
                    d[s0 + i] += (x - low) * gain * env;
                }
            }

            // Broadband body noise and a plastic crack.
            float lowpass = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                lowpass += 0.25f * (Noise(random) - lowpass);
                d[i] += lowpass * Mathf.Exp(-t * (heavy ? 9f : 14f)) * 0.9f;
            }
            for (int c = 0; c < 3; c++)
            {
                int s0 = Mathf.RoundToInt(Range(random, 0f, 0.05f) * Rate);
                for (int i = 0; i < 220 && s0 + i < d.Length; i++) d[s0 + i] += Noise(random) * Mathf.Exp(-i / 30f) * 0.7f;
            }
            SoftClip(d, heavy ? 1.6f : 1.4f);
            return d;
        }

        /// <summary>Glass bursting: a crack followed by a shower of high, short resonant tinkles.</summary>
        public static float[] RenderGlass(int seed)
        {
            var random = new System.Random(seed);
            float[] d = Buffer(1.3f);
            for (int i = 0; i < 260; i++) d[i] += Noise(random) * Mathf.Exp(-i / 50f) * 0.9f;
            int grains = 140;
            for (int g = 0; g < grains; g++)
            {
                float start = -Mathf.Log(1f - (float)random.NextDouble() * 0.97f) * 0.16f;
                if (g > grains * 0.85f) start += Range(random, 0.25f, 0.9f);
                int s0 = Mathf.RoundToInt(start * Rate);
                float hz = Range(random, 2400f, 9500f);
                float decay = Range(random, 40f, 160f);
                float gain = Range(random, 0.08f, 0.3f) * Mathf.Exp(-start * 1.6f);
                int n = Mathf.Min(d.Length - s0, Mathf.RoundToInt(Rate * 5f / decay));
                float step = TwoPi * hz / Rate;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)Rate;
                    d[s0 + i] += (Mathf.Sin(step * i) + Noise(random) * 0.25f) * gain * Mathf.Exp(-t * decay);
                }
            }
            HighPass(d, 0.92f);
            Normalize(d, 0.85f);
            return d;
        }

        /// <summary>Loose parts bouncing and scattering after a big hit.</summary>
        public static float[] RenderDebris(int seed)
        {
            var random = new System.Random(seed);
            float[] d = Buffer(1.6f);
            for (int b = 0; b < 26; b++)
            {
                float start = Range(random, 0.02f, 1.3f);
                int s0 = Mathf.RoundToInt(start * Rate);
                float hz = Range(random, 500f, 2600f);
                float decay = Range(random, 18f, 60f);
                float gain = Range(random, 0.1f, 0.45f) * (1f - start * 0.5f);
                int n = Mathf.Min(d.Length - s0, Mathf.RoundToInt(Rate * 5f / decay));
                float step = TwoPi * hz / Rate, step2 = TwoPi * hz * 2.31f / Rate;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)Rate;
                    d[s0 + i] += (Mathf.Sin(step * i) + 0.5f * Mathf.Sin(step2 * i) + Noise(random) * 0.3f * Mathf.Exp(-t * 300f)) * gain * Mathf.Exp(-t * decay);
                }
            }
            Normalize(d, 0.7f);
            return d;
        }

        /// <summary>Fuel or Crashbreaker blast: crack, sub boom, rolling rumble, crackling fire, debris rain.</summary>
        public static float[] RenderExplosion(int seed)
        {
            var random = new System.Random(seed);
            float length = 4.8f;
            float[] d = Buffer(length);
            float brown = 0f, lp1 = 0f, lp2 = 0f, phase = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float attack = Mathf.Clamp01(t / 0.004f);
                // Rumble: brown noise through a lowpass that closes over time.
                brown = Mathf.Clamp(brown + Noise(random) * 0.06f, -1f, 1f) * 0.998f;
                float cutoff = Mathf.Lerp(0.35f, 0.012f, Mathf.Sqrt(Mathf.Clamp01(t / 3f)));
                lp1 += cutoff * (Noise(random) - lp1);
                lp2 += cutoff * (lp1 - lp2);
                float rumble = (lp2 * 4f + brown * 0.8f) * attack * Mathf.Exp(-t * 0.9f);
                // Sub boom with a pitch drop.
                float hz = Mathf.Lerp(58f, 24f, Mathf.Clamp01(t / 1.2f));
                phase += TwoPi * hz / Rate;
                float sub = Mathf.Sin(phase) * attack * Mathf.Exp(-t * 1.6f) * 1.3f;
                // The initial supersonic crack.
                float crack = Noise(random) * Mathf.Exp(-t * 45f) * 1.2f;
                d[i] = rumble + sub + crack;
            }
            // Crackling fire and debris landing.
            for (int p = 0; p < 140; p++)
            {
                float start = Range(random, 0.2f, length - 0.2f);
                int s0 = Mathf.RoundToInt(start * Rate);
                float gain = Range(random, 0.05f, 0.35f) * Mathf.Exp(-start * 0.6f);
                int n = Mathf.RoundToInt(Range(random, 0.001f, 0.004f) * Rate);
                for (int i = 0; i < n && s0 + i < d.Length; i++) d[s0 + i] += Noise(random) * gain * (1f - i / (float)n);
            }
            for (int b = 0; b < 18; b++)
            {
                float start = Range(random, 0.6f, 2.6f);
                int s0 = Mathf.RoundToInt(start * Rate);
                float hz = Range(random, 400f, 1800f);
                for (int i = 0; i < 4000 && s0 + i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    d[s0 + i] += Mathf.Sin(TwoPi * hz * t) * Mathf.Exp(-t * 35f) * 0.12f;
                }
            }
            SoftClip(d, 1.5f);
            return d;
        }

        /// <summary>Deep "braaam" swell for the start of impact time.</summary>
        public static float[] RenderSlowMotion()
        {
            float[] d = Buffer(2.4f);
            var random = new System.Random(5);
            float lp = 0f;
            float[] phases = new float[5];
            float[] detune = { 1f, 1.005f, 0.996f, 1.498f, 0.5f };
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Clamp01(t / 0.03f) * Mathf.Exp(-t * 1.4f);
                float saw = 0f;
                for (int k = 0; k < phases.Length; k++)
                {
                    phases[k] = Mathf.Repeat(phases[k] + 55f * detune[k] / Rate, 1f);
                    saw += (phases[k] * 2f - 1f) * (k == 4 ? 1.2f : 0.6f);
                }
                float cutoff = Mathf.Lerp(0.25f, 0.015f, Mathf.Clamp01(t / 1.6f));
                lp += cutoff * (saw - lp);
                float boom = Mathf.Sin(TwoPi * Mathf.Lerp(70f, 30f, Mathf.Clamp01(t / 0.6f)) * t) * Mathf.Exp(-t * 4f);
                d[i] = (lp * 0.8f + boom * 1.2f + Noise(random) * 0.03f * Mathf.Exp(-t * 6f)) * env;
            }
            SoftClip(d, 1.3f);
            return d;
        }

        // ------------------------------------------------------------------ loops

        /// <summary>Traffic engine: an inline-four at a steady cruise, one second, loops cleanly.</summary>
        public static float[] RenderTrafficEngine(int seed)
        {
            var random = new System.Random(seed);
            float[] d = Buffer(1f);
            const float fire = 60f;
            float lp = 0f, intake = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float sum = 0f;
                for (int h = 1; h <= 10; h++) sum += Mathf.Sin(TwoPi * fire * h * t + h * 1.3f) / Mathf.Pow(h, 1.1f);
                sum += 0.4f * Mathf.Sin(TwoPi * fire * 0.5f * t);
                intake += 0.3f * (Noise(random) - intake);
                float y = (float)Math.Tanh(sum * 1.4f) + intake * 0.25f * (0.6f + 0.4f * Mathf.Sin(TwoPi * fire * t));
                lp += 0.2f * (y - lp);
                d[i] = lp;
            }
            return d;
        }

        /// <summary>Boost: a jet-like roar with a rising whistle.</summary>
        public static float[] RenderBoostLoop()
        {
            var random = new System.Random(23);
            float[] d = Buffer(2f);
            float slow = 0f, fast = 0f, band = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float x = Noise(random);
                slow += 0.02f * (x - slow);
                fast += 0.3f * (x - fast);
                band += 0.08f * ((fast - slow) - band);
                float whistle = Mathf.Sin(TwoPi * 3300f * t) * 0.08f + Mathf.Sin(TwoPi * 6600f * t) * 0.03f;
                d[i] = (fast - slow) * 1.6f + band * 2f + whistle;
            }
            return d;
        }

        public static float[] RenderWind()
        {
            var random = new System.Random(29);
            float[] d = Buffer(4f);
            float b0 = 0f, b1 = 0f, b2 = 0f, hp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float white = Noise(random);
                // Paul Kellet's economy pink noise.
                b0 = 0.99765f * b0 + white * 0.099046f;
                b1 = 0.963f * b1 + white * 0.2965164f;
                b2 = 0.57f * b2 + white * 1.0526913f;
                float pink = b0 + b1 + b2 + white * 0.1848f;
                hp += 0.05f * (pink - hp);
                float gust = 0.75f + 0.25f * Mathf.Sin(TwoPi * 0.5f * t) * Mathf.Sin(TwoPi * 0.25f * t + 1f);
                d[i] = (pink - hp) * gust;
            }
            return d;
        }

        /// <summary>Tyre roar on asphalt: low rumble with a coarse texture.</summary>
        public static float[] RenderRoad()
        {
            var random = new System.Random(31);
            float[] d = Buffer(2f);
            float brown = 0f, lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                brown = Mathf.Clamp(brown + Noise(random) * 0.04f, -1f, 1f) * 0.999f;
                lp += 0.06f * (Noise(random) - lp);
                d[i] = brown * 1.2f + lp * 0.9f;
            }
            return d;
        }

        /// <summary>Tyre squeal: a narrow, wavering resonance excited by noise.</summary>
        public static float[] RenderSqueal()
        {
            var random = new System.Random(37);
            float[] d = Buffer(2f);
            float low = 0f, band = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float hz = 1150f + 90f * Mathf.Sin(TwoPi * 3f * t) + 40f * Mathf.Sin(TwoPi * 7.5f * t);
                float f = 2f * Mathf.Sin(Mathf.PI * hz / Rate);
                float q = 0.02f;
                float x = Noise(random);
                low += f * band;
                float high = x - low - q * band;
                band += f * high;
                d[i] = band * 0.6f + Mathf.Sin(TwoPi * hz * t) * 0.15f;
            }
            return d;
        }

        /// <summary>Metal scraping along tarmac: stick-slip bursts through panel resonances.</summary>
        public static float[] RenderScrape()
        {
            var random = new System.Random(41);
            float[] d = Buffer(2f);
            float[] centres = { 900f, 1700f, 3100f, 4400f };
            float[] low = new float[4], band = new float[4];
            float stick = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                if (random.NextDouble() < 0.004) stick = Range(random, 0.5f, 1f);
                stick *= 0.9995f;
                float x = Noise(random) * (0.3f + stick);
                float sum = 0f;
                for (int k = 0; k < 4; k++)
                {
                    float f = 2f * Mathf.Sin(Mathf.PI * centres[k] / Rate);
                    low[k] += f * band[k];
                    float high = x - low[k] - 0.06f * band[k];
                    band[k] += f * high;
                    sum += band[k] * (k == 0 ? 0.5f : 0.3f);
                }
                d[i] = sum;
            }
            return d;
        }

        /// <summary>City and countryside bed: distant traffic, a breeze and the odd bird.</summary>
        public static float[] RenderAmbience()
        {
            var random = new System.Random(43);
            float[] d = Buffer(8f);
            float brown = 0f, lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                brown = Mathf.Clamp(brown + Noise(random) * 0.02f, -1f, 1f) * 0.9995f;
                lp += 0.01f * (Noise(random) - lp);
                d[i] = brown * 0.6f + lp * 1.5f * (0.8f + 0.2f * Mathf.Sin(TwoPi * t / 8f * 3f));
            }
            for (int bird = 0; bird < 7; bird++)
            {
                float start = Range(random, 0.3f, 7.3f);
                float baseHz = Range(random, 2600f, 4200f);
                int chirps = random.Next(2, 5);
                for (int c = 0; c < chirps; c++)
                {
                    int s0 = Mathf.RoundToInt((start + c * 0.13f) * Rate);
                    int n = Mathf.RoundToInt(0.07f * Rate);
                    float phase = 0f;
                    for (int i = 0; i < n && s0 + i < d.Length; i++)
                    {
                        float u = i / (float)n;
                        float hz = baseHz * (1f + 0.35f * Mathf.Sin(u * Mathf.PI)) + 600f * Mathf.Sin(TwoPi * 40f * u);
                        phase += TwoPi * hz / Rate;
                        d[s0 + i] += Mathf.Sin(phase) * Mathf.Sin(u * Mathf.PI) * 0.06f;
                    }
                }
            }
            return d;
        }

        /// <summary>Low detuned pad that underscores the approach. Four seconds, integer cycles, loops cleanly.</summary>
        public static float[] RenderTension()
        {
            float[] d = Buffer(4f);
            float[] hz = { 55f, 55.25f, 82.5f, 110f, 27.5f };
            float[] gain = { 0.5f, 0.5f, 0.3f, 0.18f, 0.6f };
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float saw = 0f;
                for (int k = 0; k < hz.Length; k++) saw += (Mathf.Repeat(hz[k] * t, 1f) * 2f - 1f) * gain[k];
                float cutoff = 0.012f + 0.01f * (0.5f + 0.5f * Mathf.Sin(TwoPi * 0.5f * t));
                lp += cutoff * (saw - lp);
                d[i] = lp;
            }
            return d;
        }

        // ------------------------------------------------------------------ one-shots

        /// <summary>Two-tone car horn (or a deep bus air horn) through a brassy formant.</summary>
        public static float[] RenderHorn(bool airHorn)
        {
            float length = airHorn ? 0.9f : 0.55f;
            float[] d = Buffer(length);
            float a = airHorn ? 220f : 415f, b = airHorn ? 277f : 494f;
            float low = 0f, band = 0f;
            float formant = airHorn ? 900f : 1900f;
            float f = 2f * Mathf.Sin(Mathf.PI * formant / Rate);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Clamp01(t / 0.015f) * Mathf.Clamp01((length - t) / 0.08f);
                float x = (float)Math.Tanh(3f * (Mathf.Sin(TwoPi * a * t) + Mathf.Sin(TwoPi * b * t)));
                low += f * band;
                float high = x - low - 0.35f * band;
                band += f * high;
                d[i] = (x * 0.35f + band * 0.5f) * env;
            }
            Normalize(d, 0.7f);
            return d;
        }

        public static float[] RenderBlowOff()
        {
            var random = new System.Random(53);
            float[] d = Buffer(0.45f);
            float low = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float x = Noise(random);
                low += 0.45f * (x - low);
                d[i] = (x - low) * Mathf.Clamp01(t / 0.01f) * Mathf.Exp(-t * 9f) * (1f + 0.3f * Mathf.Sin(TwoPi * 60f * t));
            }
            Normalize(d, 0.6f);
            return d;
        }

        public static float[] RenderBackfire(int seed)
        {
            var random = new System.Random(seed);
            float[] d = Buffer(0.18f);
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                lp += 0.3f * (Noise(random) - lp);
                d[i] = (Noise(random) * Mathf.Exp(-t * 90f) + lp * Mathf.Exp(-t * 30f) * 1.5f + Mathf.Sin(TwoPi * 90f * t) * Mathf.Exp(-t * 40f));
            }
            SoftClip(d, 1.4f);
            return d;
        }

        /// <summary>Suspension bottoming out on landing.</summary>
        public static float[] RenderThud()
        {
            var random = new System.Random(59);
            float[] d = Buffer(0.5f);
            float phase = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                phase += TwoPi * Mathf.Lerp(90f, 45f, Mathf.Clamp01(t / 0.2f)) / Rate;
                d[i] = Mathf.Sin(phase) * Mathf.Exp(-t * 11f) + Noise(random) * Mathf.Exp(-t * 60f) * 0.5f
                    + Mathf.Sin(TwoPi * 420f * t) * Mathf.Exp(-t * 30f) * 0.25f;
            }
            Normalize(d, 0.85f);
            return d;
        }

        public static float[] RenderHeartbeat()
        {
            float[] d = Buffer(0.55f);
            for (int beat = 0; beat < 2; beat++)
            {
                int s0 = Mathf.RoundToInt(beat * 0.22f * Rate);
                float gain = beat == 0 ? 1f : 0.7f;
                for (int i = 0; s0 + i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    d[s0 + i] += Mathf.Sin(TwoPi * Mathf.Lerp(62f, 40f, Mathf.Clamp01(t / 0.12f)) * t) * Mathf.Exp(-t * 18f) * gain;
                }
            }
            Normalize(d, 0.9f);
            return d;
        }

        /// <summary>Launch: tyre chirp and a rising whoosh.</summary>
        public static float[] RenderLaunch()
        {
            var random = new System.Random(47);
            float[] d = Buffer(1.1f);
            float slow = 0f, low = 0f, band = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float p = t / 1.1f;
                slow += Mathf.Lerp(0.02f, 0.35f, p) * (Noise(random) - slow);
                float whoosh = slow * Mathf.Sin(Mathf.Clamp01(p) * Mathf.PI) * 1.6f;
                float hz = 1300f + 200f * Mathf.Sin(TwoPi * 6f * t);
                float f = 2f * Mathf.Sin(Mathf.PI * hz / Rate);
                low += f * band;
                float high = Noise(random) - low - 0.03f * band;
                band += f * high;
                float chirp = band * Mathf.Exp(-t * 5f) * 0.5f;
                d[i] = whoosh + chirp;
            }
            Normalize(d, 0.8f);
            return d;
        }

        public static float[] RenderClick()
        {
            float[] d = Buffer(0.05f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                d[i] = (Mathf.Sin(TwoPi * 1800f * t) * 0.6f + Mathf.Sin(TwoPi * 3700f * t) * 0.3f) * Mathf.Exp(-t * 120f);
            }
            Normalize(d, 0.5f);
            return d;
        }

        public static float[] RenderTick(float hz)
        {
            float[] d = Buffer(0.06f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                d[i] = (Mathf.Sin(TwoPi * hz * t) + 0.3f * Mathf.Sin(TwoPi * hz * 2.76f * t)) * Mathf.Exp(-t * 70f);
            }
            Normalize(d, 0.5f);
            return d;
        }

        /// <summary>FM bell arpeggio for rewards.</summary>
        public static float[] RenderChime(float[] hz, float spacing, float length, float decay = 4f)
        {
            float[] d = Buffer(length);
            for (int n = 0; n < hz.Length; n++)
            {
                int start = Mathf.RoundToInt(n * spacing * Rate);
                for (int i = start; i < d.Length; i++)
                {
                    float t = (i - start) / (float)Rate;
                    float env = Mathf.Exp(-t * decay) * Mathf.Clamp01(t / 0.003f);
                    float modulator = Mathf.Sin(TwoPi * hz[n] * 3.5f * t) * 1.6f * Mathf.Exp(-t * 6f);
                    d[i] += (Mathf.Sin(TwoPi * hz[n] * t + modulator) + 0.25f * Mathf.Sin(TwoPi * hz[n] * 2.01f * t)) * env * 0.5f;
                }
            }
            Normalize(d, 0.7f);
            return d;
        }

        public static float[] RenderFail(float startHz, float endHz, float length)
        {
            float[] d = Buffer(length);
            float phase = 0f, lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float hz = Mathf.Lerp(startHz, endHz, t / length);
                phase = Mathf.Repeat(phase + hz / Rate, 1f);
                lp += 0.15f * ((phase * 2f - 1f) - lp);
                d[i] = lp * Mathf.Clamp01(1f - t / length) * Mathf.Clamp01(t / 0.01f);
            }
            Normalize(d, 0.55f);
            return d;
        }

        public static float[] RenderReadyBeep()
        {
            float[] d = Buffer(0.22f);
            float phase = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float hz = Mathf.Lerp(700f, 1400f, t / 0.22f);
                phase += TwoPi * hz / Rate;
                d[i] = (Mathf.Sin(phase) + 0.3f * Mathf.Sin(phase * 2f)) * Mathf.Exp(-t * 9f);
            }
            Normalize(d, 0.55f);
            return d;
        }

        // ------------------------------------------------------------------ helpers

        public static AudioClip Clip(string name, float[] data, bool loop = false)
        {
            if (loop) MakeLoop(ref data);
            Normalize(data, loop ? 0.75f : 0.95f, true);
            AudioClip clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float[] Buffer(float seconds) => new float[Mathf.Max(1, Mathf.RoundToInt(seconds * Rate))];

        private static float Noise(System.Random random) => (float)(random.NextDouble() * 2.0 - 1.0);

        private static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);

        private static void SoftClip(float[] d, float drive)
        {
            float max = 0.0001f;
            for (int i = 0; i < d.Length; i++) max = Mathf.Max(max, Mathf.Abs(d[i]));
            for (int i = 0; i < d.Length; i++) d[i] = (float)Math.Tanh(d[i] / max * drive);
        }

        private static void HighPass(float[] d, float coefficient)
        {
            float previousIn = 0f, previousOut = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float output = coefficient * (previousOut + d[i] - previousIn);
                previousIn = d[i];
                previousOut = output;
                d[i] = output;
            }
        }

        private static void Normalize(float[] d, float peak, bool onlyIfLouder = false)
        {
            float max = 0.0001f;
            for (int i = 0; i < d.Length; i++) max = Mathf.Max(max, Mathf.Abs(d[i]));
            if (onlyIfLouder && max <= peak) return;
            float gain = peak / max;
            for (int i = 0; i < d.Length; i++) d[i] *= gain;
        }

        /// <summary>Crossfades the tail into the head so a loop has no click.</summary>
        private static void MakeLoop(ref float[] data)
        {
            int fade = Mathf.Min(data.Length / 8, Rate / 8);
            for (int i = 0; i < fade; i++)
            {
                float mix = i / (float)fade;
                data[i] = data[data.Length - fade + i] * Mathf.Sqrt(1f - mix) + data[i] * Mathf.Sqrt(mix);
            }
            Array.Resize(ref data, data.Length - fade);
        }
    }
}

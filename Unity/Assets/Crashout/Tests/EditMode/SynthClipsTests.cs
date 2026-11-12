using System;
using System.Collections.Generic;
using Crashout.Presentation;
using NUnit.Framework;

namespace Crashout.Tests
{
    /// <summary>Every synthesized sound must be audible, finite and inside full scale.</summary>
    public class SynthClipsTests
    {
        private static IEnumerable<TestCaseData> Clips()
        {
            yield return Named("Impact light", () => SynthClips.RenderImpact(101, false));
            yield return Named("Impact heavy", () => SynthClips.RenderImpact(202, true));
            yield return Named("Glass", () => SynthClips.RenderGlass(303));
            yield return Named("Window pane", () => SynthClips.RenderGlass(700, true));
            yield return Named("Explosion", () => SynthClips.RenderExplosion(404));
            yield return Named("Sub drop", SynthClips.RenderSubDrop);
            yield return Named("Metal groan", () => SynthClips.RenderMetalGroan(610));
            yield return Named("Concrete hit", () => SynthClips.RenderConcreteHit(640));
            yield return Named("Rubble", () => SynthClips.RenderRubble(670));
            yield return Named("Pole clang", () => SynthClips.RenderPoleClang(730));
            yield return Named("Electric zap", SynthClips.RenderElectricZap);
            yield return Named("Wood crack", () => SynthClips.RenderWoodCrack(750));
            yield return Named("Plastic knock", () => SynthClips.RenderPlasticKnock(770));
            yield return Named("Water loop", () => SynthClips.RenderWaterLoop(811));
            yield return Named("Fire loop", () => SynthClips.RenderFireLoop(823));
            yield return Named("Car alarm A", () => SynthClips.RenderCarAlarm(0));
            yield return Named("Car alarm B", () => SynthClips.RenderCarAlarm(1));
            yield return Named("Siren", () => SynthClips.RenderSiren(829));
            yield return Named("Tinnitus", SynthClips.RenderTinnitus);
            yield return Named("Whoosh", () => SynthClips.RenderWhoosh(790));
        }

        private static TestCaseData Named(string name, Func<float[]> render)
        {
            return new TestCaseData(render).SetName("Synth_" + name.Replace(' ', '_'));
        }

        [TestCaseSource(nameof(Clips))]
        public void ClipIsAudibleAndBounded(Func<float[]> render)
        {
            float[] data = render();
            Assert.That(data.Length, Is.GreaterThan(1000));
            float peak = 0f;
            double energy = 0.0;
            foreach (float sample in data)
            {
                Assert.That(float.IsNaN(sample) || float.IsInfinity(sample), Is.False, "Non-finite sample.");
                peak = Math.Max(peak, Math.Abs(sample));
                energy += sample * sample;
            }
            Assert.That(peak, Is.GreaterThan(0.05f), "Clip is effectively silent.");
            // Clip() normalises anything hotter than 0.95, but nothing should be wildly out of range before it.
            Assert.That(peak, Is.LessThan(40f), "Clip is far outside full scale.");
            Assert.That(Math.Sqrt(energy / data.Length), Is.GreaterThan(0.002), "Clip has almost no energy.");
        }
    }
}

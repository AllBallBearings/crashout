using Crashout.Presentation;
using NUnit.Framework;

namespace Crashout.Tests
{
    public class ReplayTimelineTests
    {
        [Test]
        public void SeekingBackwardsPreservesBirthsBoundariesAndDiscreteEvents()
        {
            var track = new ReplayTimeline<int>();
            track.Add(2f, 10);
            track.Add(4f, 20);
            track.Add(6f, 30);
            Assert.That(track.Sample(1.9f, out _, out _, out _), Is.False, "An object must not appear before its birth.");
            Assert.That(track.Sample(5f, out int a, out int b, out float blend), Is.True);
            Assert.That(a, Is.EqualTo(20));
            Assert.That(b, Is.EqualTo(30));
            Assert.That(blend, Is.EqualTo(0.5f));
            track.Sample(4f, out a, out _, out blend);
            Assert.That(a, Is.EqualTo(20), "Discrete changes take effect exactly at their key.");
            Assert.That(blend, Is.Zero);
            track.Sample(2.5f, out a, out b, out blend);
            Assert.That(a, Is.EqualTo(10));
            Assert.That(b, Is.EqualTo(20));
            Assert.That(blend, Is.EqualTo(0.25f));
            track.Sample(100f, out a, out b, out blend);
            Assert.That(a, Is.EqualTo(30));
            Assert.That(b, Is.EqualTo(30));
            Assert.That(blend, Is.Zero);
        }

        [TestCase(0.25f)]
        [TestCase(0.5f)]
        [TestCase(1f)]
        [TestCase(2f)]
        public void PlaybackUsesTheSelectedSpeedAndDoesNotAdvanceWhilePaused(float speed)
        {
            var clock = new ReplayPlaybackClock(10f);
            clock.SetSpeed(speed);
            clock.Toggle();
            clock.Advance(2f);
            Assert.That(clock.Position, Is.EqualTo(2f * speed));
            clock.Pause();
            clock.Advance(100f);
            Assert.That(clock.Position, Is.EqualTo(2f * speed));
        }

        [Test]
        public void PlaybackClampsSeeksStopsAtTheEndAndRestartsOnPlay()
        {
            var clock = new ReplayPlaybackClock(10f);
            clock.Seek(-2f);
            Assert.That(clock.Position, Is.Zero);
            clock.Toggle();
            clock.Advance(20f);
            Assert.That(clock.Position, Is.EqualTo(10f));
            Assert.That(clock.IsPlaying, Is.False);
            clock.Toggle();
            Assert.That(clock.Position, Is.Zero);
            Assert.That(clock.IsPlaying, Is.True);
            clock.Seek(float.NaN);
            clock.Advance(float.PositiveInfinity);
            clock.Advance(-2f);
            Assert.That(clock.Position, Is.Zero);
            clock.SetSpeed(5f);
            Assert.That(clock.Speed, Is.EqualTo(1f));
        }

        [Test]
        public void DuplicateTimesReplaceTheKeyAndOutOfOrderTimesAreRejected()
        {
            var track = new ReplayTimeline<int>();
            Assert.That(track.Sample(0f, out _, out _, out _), Is.False);
            track.Add(0f, 1);
            track.Add(0f, 2);
            Assert.That(track.Count, Is.EqualTo(1));
            track.Sample(0f, out int value, out _, out _);
            Assert.That(value, Is.EqualTo(2));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => track.Add(-1f, 3));
        }
    }
}

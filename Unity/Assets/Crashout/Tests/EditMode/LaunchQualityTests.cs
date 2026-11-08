using Crashout.Simulation;
using NUnit.Framework;

namespace Crashout.Tests
{
    public sealed class LaunchQualityTests
    {
        [Test]
        public void GreenZoneGivesAPerfectLaunch()
        {
            Assert.That(CrashoutGameSession.LaunchQualityAt(CrashoutGameSession.LaunchZoneMin), Is.EqualTo(1f));
            Assert.That(CrashoutGameSession.LaunchQualityAt((CrashoutGameSession.LaunchZoneMin + CrashoutGameSession.LaunchZoneMax) * 0.5f), Is.EqualTo(1f));
            Assert.That(CrashoutGameSession.LaunchQualityAt(CrashoutGameSession.LaunchZoneMax), Is.EqualTo(1f));
        }

        [Test]
        public void EarlyAndLatePressesAreSlowerButNeverZero()
        {
            float early = CrashoutGameSession.LaunchQualityAt(0.1f);
            float late = CrashoutGameSession.LaunchQualityAt(1f);
            Assert.That(early, Is.LessThan(1f).And.GreaterThan(0.2f));
            Assert.That(late, Is.LessThan(1f).And.GreaterThan(0.2f));
        }

        [Test]
        public void QualityRisesTowardTheGreenZone()
        {
            Assert.That(CrashoutGameSession.LaunchQualityAt(0.6f), Is.GreaterThan(CrashoutGameSession.LaunchQualityAt(0.2f)));
            Assert.That(CrashoutGameSession.LaunchQualityAt(0.97f), Is.GreaterThan(CrashoutGameSession.LaunchQualityAt(1f)));
        }
    }
}

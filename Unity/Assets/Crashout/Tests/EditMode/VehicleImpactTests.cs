using Crashout.Vehicle;
using NUnit.Framework;
using UnityEngine;

namespace Crashout.Tests
{
    public sealed class VehicleImpactTests
    {
        [TestCase(0f)]
        [TestCase(31f)]
        [TestCase(45f)]
        public void ForwardSpeedDoesNotTurnAGentleLandingIntoACrash(float forwardSpeed)
        {
            Assert.That(VehicleImpact.NormalSpeed(new Vector3(0f, -2f, forwardSpeed), Vector3.up), Is.EqualTo(2f));
        }

        [Test]
        public void DownhillTravelIsTangentToTheRoadRatherThanAnImpact()
        {
            Vector3 normal = new Vector3(0f, 1f, 0.25f).normalized;
            Vector3 downhill = new Vector3(0f, -0.25f, 1f).normalized;
            Assert.That(VehicleImpact.NormalSpeed(downhill * 45f, normal), Is.Zero.Within(0.0001f));
            Assert.That(VehicleImpact.NormalSpeed(downhill * 45f - normal * 2f, normal), Is.EqualTo(2f).Within(0.0001f));
        }

        [Test]
        public void HeadOnHitsHardLandingsAndSideImpactsKeepTheirImpactSpeed()
        {
            Assert.That(VehicleImpact.NormalSpeed(Vector3.forward * 25f, Vector3.back), Is.EqualTo(25f));
            Assert.That(VehicleImpact.NormalSpeed(new Vector3(0f, -20f, 45f), Vector3.up), Is.EqualTo(20f));
            Assert.That(VehicleImpact.NormalSpeed(new Vector3(12f, 0f, 45f), Vector3.left), Is.EqualTo(12f));
        }
    }
}

using Crashout.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace Crashout.Tests
{
    public sealed class RunwayTests
    {
        private const float BoardLength = RunwayProfile.BoardHalfExtent * 2f;

        [Test]
        public void EveryJunctionLaunchesFromOneToTwoBoardLengthsOutAndAboveTheBoard()
        {
            for (int i = 0; i < CrashScenarios.Count; i++)
            {
                RunwayProfile runway = CrashScenarios.Create(i).Runway;
                Assert.That(runway.DistanceBeyondBoard, Is.InRange(BoardLength * 1f, BoardLength * 2.05f), runway.Title);
                Assert.That(runway.StartHeight, Is.GreaterThan(5f), runway.Title + " should start high above the board.");
                Assert.That(runway.FarZ, Is.LessThan(runway.StartZ), runway.Title + " road should continue behind the start.");
            }
        }

        [Test]
        public void RunwaysMeetTheBoardRoadFlatAndRemainDrivable()
        {
            for (int i = 0; i < CrashScenarios.Count; i++)
            {
                RunwayProfile runway = CrashScenarios.Create(i).Runway;
                Assert.That(runway.HeightAt(RunwayProfile.BoardEdgeZ), Is.EqualTo(0f).Within(0.01f), runway.Title);
                Assert.That(runway.GradeAt(RunwayProfile.BoardEdgeZ), Is.EqualTo(0f).Within(0.01f), runway.Title);
                Assert.That(runway.GradeAt(runway.StartZ), Is.EqualTo(0f).Within(0.01f), runway.Title + " start should be level.");

                float previous = runway.HeightAt(runway.FarZ);
                for (float z = runway.FarZ; z <= RunwayProfile.BoardEdgeZ; z += 0.25f)
                {
                    float h = runway.HeightAt(z);
                    Assert.That(Mathf.Abs(h - previous), Is.LessThan(0.15f), $"{runway.Title} jumps at z={z}");
                    Assert.That(Mathf.Abs(runway.GradeAt(z)), Is.LessThan(0.34f), $"{runway.Title} too steep at z={z}");
                    previous = h;
                }
            }
        }

        [Test]
        public void KickerRampLandsOutsideTheJunctionAtNormalTopSpeed()
        {
            RunwayProfile runway = CrashScenarios.Create(2).Runway;
            Assert.That(runway.HasRamp, Is.True);
            Assert.That(runway.RampEndZ, Is.LessThan(RunwayProfile.BoardEdgeZ - 10f));
            float slope = 2f * runway.RampLipHeight / runway.RampLength;
            const float speed = 31f;
            float vy = speed * slope / Mathf.Sqrt(1f + slope * slope);
            float vz = speed / Mathf.Sqrt(1f + slope * slope);
            float airtime = (vy + Mathf.Sqrt(vy * vy + 2f * 9.81f * runway.RampLipHeight)) / 9.81f;
            float landing = runway.RampEndZ + vz * airtime;
            Assert.That(landing, Is.LessThan(-10f), "A normal-speed jump should touch down before the stop line.");
        }

        [Test]
        public void ArrivalTakesLongerFromTheRunwayAndBoostShortensIt()
        {
            RunwayProfile runway = CrashScenarios.Create(0).Runway;
            float design = CrashoutGameSession.DesignArrivalSeconds;
            float cruise = ApproachPlanner.SecondsToJunction(runway, runway.StartZ, 5f, false, 1.3f);
            float flatOut = ApproachPlanner.SecondsToJunction(runway, runway.StartZ, 5f, true, 1.3f + 3.2f);
            Assert.That(design, Is.InRange(1.2f, 3f));
            Assert.That(cruise, Is.GreaterThan(design + 2f));
            Assert.That(flatOut, Is.LessThan(cruise));
            Assert.That(cruise, Is.LessThan(12f), "The approach should build tension, not drag.");
        }

        [Test]
        public void DownhillAcceleratesTheApproach()
        {
            RunwayProfile hill = CrashScenarios.Create(0).Runway;
            RunwayProfile flat = RunwayProfile.Flat(hill.StartZ);
            float downhill = ApproachPlanner.SecondsToJunction(hill, hill.StartZ, 5f, false, 0f);
            float level = ApproachPlanner.SecondsToJunction(flat, hill.StartZ, 5f, false, 0f);
            Assert.That(downhill, Is.LessThan(level));
        }
    }
}

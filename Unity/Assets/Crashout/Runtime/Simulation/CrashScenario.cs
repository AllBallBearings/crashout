using System.Collections.Generic;
using Crashout.Vehicle;
using UnityEngine;

namespace Crashout.Simulation
{
    public enum CrashMedal : byte
    {
        None,
        Bronze,
        Silver,
        Gold
    }

    public struct TrafficSpawn
    {
        public VehicleClass Class;
        public Vector3 Position;
        public Vector3 Direction;
        public float Lane;
        public float Speed;
        public bool AlongX;
        public bool IsTarget;
    }

    /// <summary>
    /// One crash junction: a seeded traffic pattern, a marked target and medal targets,
    /// in the spirit of Burnout's Crash Mode. Positions are in canonical world metres.
    /// </summary>
    public sealed class CrashScenario
    {
        public int Index;
        public string Name;
        public string Brief;
        public int Bronze;
        public int Silver;
        public int Gold;
        public int TargetBonus = 25000;
        /// <summary>The approach road the player launches from, far outside the board.</summary>
        public RunwayProfile Runway = RunwayProfile.Flat(-22f);
        public Vector3 PlayerStart => Runway.PlayerStart;
        public readonly List<TrafficSpawn> Traffic = new List<TrafficSpawn>();

        public CrashMedal MedalFor(int score)
        {
            if (score >= Gold) return CrashMedal.Gold;
            if (score >= Silver) return CrashMedal.Silver;
            if (score >= Bronze) return CrashMedal.Bronze;
            return CrashMedal.None;
        }

        public int NextTarget(int score)
        {
            if (score < Bronze) return Bronze;
            if (score < Silver) return Silver;
            if (score < Gold) return Gold;
            return Gold;
        }
    }

    public static class CrashScenarios
    {
        public const int Count = 3;

        // Right-hand traffic. East-west road lanes sit at z = -3.75, -1.25 (eastbound)
        // and 1.25, 3.75 (westbound); north-south lanes mirror that on x.
        private const float EastInner = -1.25f;
        private const float EastOuter = -3.75f;
        private const float WestInner = 1.25f;
        private const float WestOuter = 3.75f;

        public static CrashScenario Create(int index)
        {
            int wrapped = ((index % Count) + Count) % Count;
            CrashScenario scenario = wrapped == 0 ? CrossTraffic() : wrapped == 1 ? RushHour() : TankerRun();
            scenario.Index = wrapped;
            return scenario;
        }

        private static CrashScenario CrossTraffic()
        {
            var s = new CrashScenario
            {
                Name = "CROSS TRAFFIC",
                Brief = "Hit the pickup for the target bonus.",
                Bronze = 80000,
                Silver = 150000,
                Gold = 250000
            };
            Lane(s, true, 1f, EastInner, 9f, At(-23f, VehicleClass.Sedan), At(-14f, VehicleClass.Van), At(-6f, VehicleClass.Hatch, true), At(2.5f, VehicleClass.Sedan));
            Lane(s, true, 1f, EastOuter, 10.5f, At(-21f, VehicleClass.Hatch), At(-12.5f, VehicleClass.Sedan), At(-4f, VehicleClass.Pickup), At(5f, VehicleClass.Van), At(13f, VehicleClass.Sedan));
            Lane(s, true, -1f, WestInner, 9.5f, At(23f, VehicleClass.Van), At(14f, VehicleClass.Sedan), At(6f, VehicleClass.Hatch), At(-2.5f, VehicleClass.Pickup));
            Lane(s, true, -1f, WestOuter, 11.5f, At(21f, VehicleClass.Sedan), At(9.5f, VehicleClass.Bus), At(-2f, VehicleClass.Hatch), At(-10f, VehicleClass.Sedan));
            // Launch from the brow of a hill nearly two board-lengths out and drop onto the junction.
            s.Runway = new RunwayProfile { Style = RunwayStyle.Hilltop, Title = "HILLTOP DROP", StartZ = -124f, FarZ = -190f }
                .Point(-190f, 16f)
                .Point(-112f, 16f, 20f)
                .Point(-50f, 0f, 26f)
                .Point(RunwayProfile.BoardEdgeZ, 0f);
            return s;
        }

        private static CrashScenario RushHour()
        {
            var s = new CrashScenario
            {
                Name = "RUSH HOUR",
                Brief = "Packed lanes and two buses. Find the weak point.",
                Bronze = 120000,
                Silver = 220000,
                Gold = 360000
            };
            Lane(s, true, 1f, EastInner, 10f, At(-23f, VehicleClass.Hatch), At(-16f, VehicleClass.Sedan), At(-9f, VehicleClass.Van), At(-2f, VehicleClass.Sedan), At(5f, VehicleClass.Hatch));
            Lane(s, true, 1f, EastOuter, 12f, At(-22f, VehicleClass.Sedan), At(-12f, VehicleClass.Bus), At(0f, VehicleClass.Pickup, true), At(8f, VehicleClass.Sedan), At(15f, VehicleClass.Van));
            Lane(s, true, -1f, WestInner, 10.5f, At(23f, VehicleClass.Pickup), At(16f, VehicleClass.Hatch), At(9f, VehicleClass.Sedan), At(2f, VehicleClass.Van), At(-5f, VehicleClass.Sedan));
            Lane(s, true, -1f, WestOuter, 13f, At(22f, VehicleClass.Van), At(12f, VehicleClass.Sedan), At(2f, VehicleClass.Bus), At(-10f, VehicleClass.Hatch));
            // An elevated expressway crosses the river on a cable-stayed span, then ramps down into town.
            s.Runway = new RunwayProfile { Style = RunwayStyle.Overpass, Title = "THE OVERPASS", StartZ = -118f, FarZ = -230f }
                .Point(-230f, 9f)
                .Point(-72f, 9f, 22f)
                .Point(-34f, 0f, 16f)
                .Point(RunwayProfile.BoardEdgeZ, 0f);
            return s;
        }

        private static CrashScenario TankerRun()
        {
            var s = new CrashScenario
            {
                Name = "TANKER RUN",
                Brief = "A fuel tanker is on the road. Light it up.",
                Bronze = 140000,
                Silver = 240000,
                Gold = 380000
            };
            Lane(s, true, 1f, EastInner, 9.5f, At(-22f, VehicleClass.Sedan), At(-13f, VehicleClass.Hatch), At(-5f, VehicleClass.Van));
            Lane(s, true, 1f, EastOuter, 11f, At(-20f, VehicleClass.Pickup), At(-9f, VehicleClass.Sedan), At(1f, VehicleClass.Van), At(10f, VehicleClass.Hatch));
            Lane(s, true, -1f, WestInner, 10f, At(22f, VehicleClass.Sedan), At(13f, VehicleClass.Tanker, true), At(0f, VehicleClass.Hatch), At(-8f, VehicleClass.Pickup));
            Lane(s, true, -1f, WestOuter, 12f, At(20f, VehicleClass.Bus), At(6f, VehicleClass.Sedan), At(-5f, VehicleClass.Van));
            // Southbound cross traffic keeps the player's own approach busy.
            Lane(s, false, -1f, -1.25f, 9f, At(22f, VehicleClass.Hatch), At(12f, VehicleClass.Sedan));
            Lane(s, false, -1f, -3.75f, 11f, At(16f, VehicleClass.Van));
            // Out of a mountain tunnel, down a steep pass and off a kicker ramp into the junction.
            s.Runway = new RunwayProfile { Style = RunwayStyle.MountainPass, Title = "MOUNTAIN PASS", StartZ = -129f, FarZ = -168f }
                .Point(-168f, 13f)
                .Point(-120f, 13f, 16f)
                .Point(-76f, 0f, 22f)
                .Point(RunwayProfile.BoardEdgeZ, 0f)
                .WithRamp(-62f, 16f, 0.7f);
            return s;
        }

        private struct Slot
        {
            public float Along;
            public VehicleClass Class;
            public bool Target;
        }

        private static Slot At(float along, VehicleClass vehicleClass, bool target = false)
        {
            return new Slot { Along = along, Class = vehicleClass, Target = target };
        }

        private static void Lane(CrashScenario scenario, bool alongX, float sign, float lane, float speed, params Slot[] slots)
        {
            Vector3 direction = alongX ? new Vector3(sign, 0f, 0f) : new Vector3(0f, 0f, sign);
            foreach (Slot slot in slots)
            {
                VehicleSpec spec = VehicleSpec.For(slot.Class);
                Vector3 position = alongX
                    ? new Vector3(slot.Along, spec.RootHeight, lane)
                    : new Vector3(lane, spec.RootHeight, slot.Along);
                scenario.Traffic.Add(new TrafficSpawn
                {
                    Class = slot.Class,
                    Position = position,
                    Direction = direction,
                    Lane = lane,
                    Speed = speed,
                    AlongX = alongX,
                    IsTarget = slot.Target
                });
            }
        }
    }
}

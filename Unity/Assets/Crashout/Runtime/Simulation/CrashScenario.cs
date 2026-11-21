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

    /// <summary>Which diorama stands on the board: the downtown block or the low-rise harbor.</summary>
    public enum BoardStyle : byte
    {
        City,
        Port
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
        public BoardStyle Board = BoardStyle.City;
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
        public const int Count = 4;

        // Right-hand traffic. East-west road lanes sit at z = -3.75, -1.25 (eastbound)
        // and 1.25, 3.75 (westbound); north-south lanes mirror that on x.
        private const float EastInner = -1.25f;
        private const float EastOuter = -3.75f;
        private const float WestInner = 1.25f;
        private const float WestOuter = 3.75f;

        public static CrashScenario Create(int index)
        {
            int wrapped = ((index % Count) + Count) % Count;
            CrashScenario scenario = wrapped == 0 ? CrossTraffic() : wrapped == 1 ? RushHour() : wrapped == 2 ? TankerRun() : HarborRun();
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
            // Slots are where each car is when the player has 1.73 s to go (CrashoutGameSession.DesignArrivalSeconds);
            // one car per lane is timed to fill the player's column (x = 1.5) as the player crosses it.
            // Cars are 3.7-4.7 m long, so slots sit at least 11 m apart: any closer and they brake for
            // each other and drift off schedule.
            Lane(s, true, 1f, EastInner, 9f, At(-25.3f, VehicleClass.Van), At(-14.3f, VehicleClass.Sedan), At(-3.3f, VehicleClass.Hatch), At(7.7f, VehicleClass.Sedan));
            Lane(s, true, 1f, EastOuter, 10.5f, At(-27.2f, VehicleClass.Hatch), At(-16.2f, VehicleClass.Pickup, true), At(-5.2f, VehicleClass.Sedan), At(6f, VehicleClass.Van), At(17f, VehicleClass.Sedan));
            Lane(s, true, -1f, WestInner, 9.5f, At(29.7f, VehicleClass.Sedan), At(18.7f, VehicleClass.Van), At(7.7f, VehicleClass.Hatch), At(-3.3f, VehicleClass.Pickup));
            Lane(s, true, -1f, WestOuter, 11.5f, At(22.9f, VehicleClass.Sedan), At(9.9f, VehicleClass.Bus), At(-2.1f, VehicleClass.Hatch), At(-13.1f, VehicleClass.Sedan));
            // Launch from the brow of a hill nearly two board-lengths out and drop onto the junction.
            s.Runway = new RunwayProfile { Style = RunwayStyle.Hilltop, Title = "HILLTOP DROP", StartZ = -124f, FarZ = -157f }
                .Point(-157f, 16f)
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
            s.Runway = new RunwayProfile { Style = RunwayStyle.Overpass, Title = "THE OVERPASS", StartZ = -118f, FarZ = -174f }
                .Point(-174f, 9f)
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
            s.Runway = new RunwayProfile { Style = RunwayStyle.MountainPass, Title = "MOUNTAIN PASS", StartZ = -129f, FarZ = -148.5f }
                .Point(-148.5f, 13f)
                .Point(-120f, 13f, 16f)
                .Point(-76f, 0f, 22f)
                .Point(RunwayProfile.BoardEdgeZ, 0f)
                .WithRamp(-62f, 16f, 0.7f);
            return s;
        }

        private static CrashScenario HarborRun()
        {
            var s = new CrashScenario
            {
                Name = "HARBOR RUN",
                Brief = "Off the ferry and over the bridge. Hit the tanker by the fuel depot.",
                Bronze = 110000,
                Silver = 200000,
                Gold = 330000,
                Board = BoardStyle.Port
            };
            Lane(s, true, 1f, EastInner, 9.5f, At(-22f, VehicleClass.Sedan), At(-13f, VehicleClass.Van), At(-4f, VehicleClass.Hatch), At(6f, VehicleClass.Pickup));
            Lane(s, true, 1f, EastOuter, 11f, At(-20f, VehicleClass.Hatch), At(-9f, VehicleClass.Tanker, true), At(4f, VehicleClass.Sedan), At(13f, VehicleClass.Van));
            Lane(s, true, -1f, WestInner, 10f, At(22f, VehicleClass.Pickup), At(13f, VehicleClass.Sedan), At(3f, VehicleClass.Van), At(-7f, VehicleClass.Hatch));
            Lane(s, true, -1f, WestOuter, 12f, At(20f, VehicleClass.Bus), At(7f, VehicleClass.Sedan), At(-4f, VehicleClass.Hatch), At(-13f, VehicleClass.Sedan));
            Lane(s, false, -1f, -1.25f, 9f, At(21f, VehicleClass.Van), At(11f, VehicleClass.Sedan));
            // Off a ferry's car deck, down the linkspan, along the pier and over a half-raised bridge leaf.
            s.Runway = new RunwayProfile { Style = RunwayStyle.Pier, Title = "FERRY RAMP", StartZ = -126f, FarZ = -139f }
                .Point(-139f, 8f)
                .Point(-118f, 8f, 12f)
                .Point(-90f, 0f, 14f)
                .Point(RunwayProfile.BoardEdgeZ, 0f)
                .WithRamp(-66f, 14f, 0.75f);
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

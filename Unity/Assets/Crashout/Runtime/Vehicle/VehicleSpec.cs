using UnityEngine;

namespace Crashout.Vehicle
{
    public enum VehicleClass : byte
    {
        Sedan,
        Van,
        Pickup,
        Hatch,
        Bus,
        Tanker
    }

    /// <summary>Physical and scoring description of each vehicle class.</summary>
    public sealed class VehicleSpec
    {
        public VehicleClass Class;
        public Vector3 ColliderSize;
        public Vector3 BodySize;
        public float Mass;
        public int ScoreCap;
        /// <summary>Height of the body centre above the road surface.</summary>
        public float RootHeight;
        public float WheelRadius;
        public Vector3[] WheelPositions;
        /// <summary>Crashbreaker charge granted the first time this class is wrecked.</summary>
        public float CrashbreakerCharge;
        public bool Explosive;

        private static readonly VehicleSpec[] Specs = BuildSpecs();

        public static VehicleSpec For(VehicleClass vehicleClass)
        {
            return Specs[(int)vehicleClass];
        }

        private static VehicleSpec[] BuildSpecs()
        {
            Vector3[] carWheels =
            {
                new Vector3(-0.78f, -0.18f, 1.3f), new Vector3(0.78f, -0.18f, 1.3f),
                new Vector3(-0.78f, -0.18f, -1.3f), new Vector3(0.78f, -0.18f, -1.3f)
            };
            Vector3[] busWheels =
            {
                new Vector3(-1.05f, -0.9f, 2.4f), new Vector3(1.05f, -0.9f, 2.4f),
                new Vector3(-1.05f, -0.9f, -2.4f), new Vector3(1.05f, -0.9f, -2.4f)
            };
            Vector3[] tankerWheels =
            {
                new Vector3(-1.05f, -1f, 3f), new Vector3(1.05f, -1f, 3f),
                new Vector3(-1.05f, -1f, -1.6f), new Vector3(1.05f, -1f, -1.6f),
                new Vector3(-1.05f, -1f, -3f), new Vector3(1.05f, -1f, -3f)
            };
            var carSize = new Vector3(1.7f, 0.58f, 4.15f);
            Vector3[] Wheels(float track, float axle) => new[]
            {
                new Vector3(-track, -0.18f, axle), new Vector3(track, -0.18f, axle),
                new Vector3(-track, -0.18f, -axle), new Vector3(track, -0.18f, -axle)
            };
            return new[]
            {
                new VehicleSpec { Class = VehicleClass.Sedan, ColliderSize = carSize, BodySize = new Vector3(1.82f, 1.42f, 4.35f), Mass = 1400f, ScoreCap = 16000, RootHeight = 0.42f, WheelRadius = 0.31f, WheelPositions = carWheels, CrashbreakerCharge = 0.1f },
                new VehicleSpec { Class = VehicleClass.Van, ColliderSize = new Vector3(1.83f, 0.7f, 4.45f), BodySize = new Vector3(1.95f, 2.1f, 4.65f), Mass = 2100f, ScoreCap = 22000, RootHeight = 0.42f, WheelRadius = 0.34f, WheelPositions = Wheels(0.85f, 1.42f), CrashbreakerCharge = 0.14f },
                new VehicleSpec { Class = VehicleClass.Pickup, ColliderSize = new Vector3(1.86f, 0.58f, 4.65f), BodySize = new Vector3(1.98f, 1.78f, 4.85f), Mass = 1900f, ScoreCap = 20000, RootHeight = 0.42f, WheelRadius = 0.36f, WheelPositions = Wheels(0.87f, 1.5f), CrashbreakerCharge = 0.12f },
                new VehicleSpec { Class = VehicleClass.Hatch, ColliderSize = new Vector3(1.6f, 0.58f, 3.65f), BodySize = new Vector3(1.74f, 1.47f, 3.85f), Mass = 1100f, ScoreCap = 14000, RootHeight = 0.42f, WheelRadius = 0.3f, WheelPositions = Wheels(0.75f, 1.17f), CrashbreakerCharge = 0.09f },
                new VehicleSpec { Class = VehicleClass.Bus, ColliderSize = new Vector3(2.3f, 2.3f, 7.4f), BodySize = new Vector3(2.3f, 2.6f, 7.4f), Mass = 7500f, ScoreCap = 42000, RootHeight = 1.45f, WheelRadius = 0.5f, WheelPositions = busWheels, CrashbreakerCharge = 0.3f },
                new VehicleSpec { Class = VehicleClass.Tanker, ColliderSize = new Vector3(2.3f, 2.5f, 8f), BodySize = new Vector3(2.3f, 2.9f, 8f), Mass = 9000f, ScoreCap = 48000, RootHeight = 1.55f, WheelRadius = 0.5f, WheelPositions = tankerWheels, CrashbreakerCharge = 0.35f, Explosive = true }
            };
        }
    }
}

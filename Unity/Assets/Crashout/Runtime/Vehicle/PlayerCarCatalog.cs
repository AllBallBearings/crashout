using UnityEngine;

namespace Crashout.Vehicle
{
    /// <summary>Definition of one selectable player vehicle (asset, paint, dimensions, physics tuning).</summary>
    public sealed class PlayerCarSpec
    {
        public string Id;
        public string DisplayName;
        public string ShortLabel;
        public string AssetName;
        public Color PaintColor;
        public bool MetallicPaint;
        public Vector3 ColliderSize;
        public Vector3 FirstPersonEyeOffset;
        public float Mass;
        public float TopSpeed;
        public float MotorForce;
        public float WheelRadius;
        public float Track;
        public float FrontAxle;
        public float RearAxle;

        public Vector3[] WheelPositions => new[]
        {
            new Vector3(-Track, -0.18f, FrontAxle),
            new Vector3(Track, -0.18f, FrontAxle),
            new Vector3(-Track, -0.18f, RearAxle),
            new Vector3(Track, -0.18f, RearAxle),
        };
    }

    /// <summary>The four selectable player vehicles available at the start of every round.</summary>
    public static class PlayerCarCatalog
    {
        public static readonly PlayerCarSpec[] All =
        {
            new PlayerCarSpec
            {
                Id = "gtr",
                DisplayName = "GT-R COUPE",
                ShortLabel = "GT-R",
                AssetName = "Player",
                PaintColor = new Color(0.76f, 0.06f, 0.08f),
                MetallicPaint = true,
                ColliderSize = new Vector3(1.82f, 0.56f, 4.35f),
                FirstPersonEyeOffset = new Vector3(-0.34f, 0.69f, 0.36f),
                Mass = 1420f,
                TopSpeed = 41f,
                MotorForce = 8600f,
                WheelRadius = 0.31f,
                Track = 0.81f,
                FrontAxle = 1.34f,
                RearAxle = -1.32f,
            },
            new PlayerCarSpec
            {
                Id = "muscle",
                DisplayName = "70S MUSCLE",
                ShortLabel = "MUSCLE",
                AssetName = "PlayerMuscle",
                PaintColor = new Color(0.88f, 0.30f, 0.05f),
                MetallicPaint = true,
                ColliderSize = new Vector3(1.88f, 0.56f, 4.46f),
                FirstPersonEyeOffset = new Vector3(-0.34f, 0.69f, 0.32f),
                Mass = 1640f,
                TopSpeed = 40f,
                MotorForce = 9600f,
                WheelRadius = 0.32f,
                Track = 0.83f,
                FrontAxle = 1.36f,
                RearAxle = -1.34f,
            },
            new PlayerCarSpec
            {
                Id = "bmw",
                DisplayName = "M4 GT",
                ShortLabel = "M4 GT",
                AssetName = "PlayerBmw",
                PaintColor = new Color(0.76f, 0.74f, 0.05f),
                MetallicPaint = true,
                ColliderSize = new Vector3(1.84f, 0.56f, 4.38f),
                FirstPersonEyeOffset = new Vector3(-0.34f, 0.70f, 0.36f),
                Mass = 1480f,
                TopSpeed = 42.5f,
                MotorForce = 8900f,
                WheelRadius = 0.31f,
                Track = 0.81f,
                FrontAxle = 1.35f,
                RearAxle = -1.33f,
            },
            new PlayerCarSpec
            {
                Id = "suv",
                DisplayName = "TACTICAL SUV",
                ShortLabel = "SUV",
                AssetName = "PlayerSuv",
                PaintColor = new Color(0.05f, 0.055f, 0.065f),
                MetallicPaint = false,
                ColliderSize = new Vector3(1.96f, 0.72f, 4.58f),
                FirstPersonEyeOffset = new Vector3(-0.38f, 1.04f, 0.50f),
                Mass = 2250f,
                TopSpeed = 36f,
                MotorForce = 11400f,
                WheelRadius = 0.39f,
                Track = 0.86f,
                FrontAxle = 1.44f,
                RearAxle = -1.40f,
            },
        };

        public static int Count => All.Length;

        public static PlayerCarSpec Get(int index)
        {
            int wrapped = ((index % All.Length) + All.Length) % All.Length;
            return All[wrapped];
        }
    }
}

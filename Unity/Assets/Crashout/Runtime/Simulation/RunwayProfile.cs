using System.Collections.Generic;
using UnityEngine;

namespace Crashout.Simulation
{
    public enum RunwayStyle : byte
    {
        Hilltop,
        Overpass,
        MountainPass
    }

    /// <summary>
    /// The approach road that carries the player from far outside the board, usually from high
    /// above it, down onto the junction. Canonical metres: the centre line is x = 0 and the player
    /// drives towards +z. Heights are the road's rise above the board road. Grade changes blend
    /// through parabolic vertical curves (as real roads do) so a car arriving at speed stays planted.
    /// </summary>
    public sealed class RunwayProfile
    {
        /// <summary>The south edge of the 52 m board.</summary>
        public const float BoardEdgeZ = -26f;
        public const float BoardHalfExtent = 26f;
        public const float RoadHalfWidth = 5f;
        /// <summary>Top of the board road slab; the runway surface sits at this height plus its rise.</summary>
        public const float RoadSurfaceY = 0.04f;
        /// <summary>The player's lane on the northbound side of the road.</summary>
        public const float PlayerLaneX = 1.5f;
        /// <summary>Height of the player's chassis root above the road base.</summary>
        public const float PlayerRootHeight = 0.56f;

        private readonly List<Vector2> points = new List<Vector2>();
        private readonly List<float> curveLengths = new List<float>();

        public RunwayStyle Style;
        /// <summary>Shown on the HUD while waiting to launch, e.g. "HILLTOP DROP".</summary>
        public string Title = "";
        /// <summary>Where the player's car starts (z).</summary>
        public float StartZ = -22f;
        /// <summary>How far back the road is drawn behind the start.</summary>
        public float FarZ = -60f;
        public float RampStartZ;
        public float RampLength;
        public float RampLipHeight;

        public bool HasRamp => RampLength > 0.01f && RampLipHeight > 0.01f;
        public float RampEndZ => RampStartZ + RampLength;
        public float DistanceBeyondBoard => BoardEdgeZ - StartZ;
        public float StartHeight => HeightAt(StartZ);
        public int PointCount => points.Count;

        public Vector3 PlayerStart => new Vector3(PlayerLaneX, HeightAt(StartZ) + PlayerRootHeight, StartZ);

        public float MaxHeight
        {
            get
            {
                float max = 0f;
                foreach (Vector2 p in points) max = Mathf.Max(max, p.y);
                return max;
            }
        }

        /// <summary>A flat road from the board edge; used by tests and as a fallback.</summary>
        public static RunwayProfile Flat(float startZ)
        {
            return new RunwayProfile { StartZ = startZ, FarZ = startZ - 20f, Title = "" }
                .Point(startZ - 20f, 0f)
                .Point(BoardEdgeZ, 0f);
        }

        /// <summary>Adds a grade break at (z, height). Points must be added in increasing z.</summary>
        public RunwayProfile Point(float z, float height, float verticalCurveLength = 0f)
        {
            points.Add(new Vector2(z, height));
            curveLengths.Add(Mathf.Max(0f, verticalCurveLength));
            return this;
        }

        public RunwayProfile WithRamp(float startZ, float length, float lipHeight)
        {
            RampStartZ = startZ;
            RampLength = length;
            RampLipHeight = lipHeight;
            return this;
        }

        public float HeightAt(float z)
        {
            Evaluate(z, out float height, out _);
            return height;
        }

        /// <summary>dh/dz: negative while the road falls towards the junction.</summary>
        public float GradeAt(float z)
        {
            Evaluate(z, out _, out float grade);
            return grade;
        }

        /// <summary>Rise of the kicker ramp above the road at z (zero off the ramp).</summary>
        public float RampHeightAt(float z)
        {
            if (!HasRamp || z < RampStartZ || z > RampEndZ) return 0f;
            float t = (z - RampStartZ) / RampLength;
            return RampLipHeight * t * t;
        }

        /// <summary>The road surface's world height at z, including the ramp.</summary>
        public float SurfaceAt(float z)
        {
            return RoadSurfaceY + HeightAt(z) + RampHeightAt(z);
        }

        public static bool IsOnBoard(Vector3 position)
        {
            return Mathf.Abs(position.x) < BoardHalfExtent && Mathf.Abs(position.z) < BoardHalfExtent;
        }

        private void Evaluate(float z, out float height, out float grade)
        {
            int n = points.Count;
            if (n == 0)
            {
                height = 0f;
                grade = 0f;
                return;
            }

            // Inside a vertical curve the grade changes linearly, so the height is a parabola.
            for (int i = 0; i < n; i++)
            {
                float length = curveLengths[i];
                if (length <= 0f) continue;
                float startZ = points[i].x - length * 0.5f;
                float endZ = points[i].x + length * 0.5f;
                if (z < startZ || z > endZ) continue;
                float gradeIn = i == 0 ? 0f : SegmentGrade(i - 1);
                float gradeOut = i == n - 1 ? 0f : SegmentGrade(i);
                float x = z - startZ;
                height = points[i].y - gradeIn * length * 0.5f + gradeIn * x + (gradeOut - gradeIn) / (2f * length) * x * x;
                grade = gradeIn + (gradeOut - gradeIn) * x / length;
                return;
            }

            if (z <= points[0].x)
            {
                height = points[0].y;
                grade = 0f;
                return;
            }
            if (z >= points[n - 1].x)
            {
                height = points[n - 1].y;
                grade = 0f;
                return;
            }
            for (int i = 0; i < n - 1; i++)
            {
                if (z > points[i + 1].x) continue;
                float g = SegmentGrade(i);
                height = points[i].y + g * (z - points[i].x);
                grade = g;
                return;
            }
            height = points[n - 1].y;
            grade = 0f;
        }

        private float SegmentGrade(int i)
        {
            float dz = points[i + 1].x - points[i].x;
            return dz <= 0.0001f ? 0f : (points[i + 1].y - points[i].y) / dz;
        }
    }

    /// <summary>
    /// Predicts when the player will reach the junction, using a one-dimensional copy of the
    /// arcade car (motor, boost, drag, top-speed governor and gravity along the runway grade).
    /// Traffic is phase-locked to this estimate so the junction converges as the player arrives.
    /// </summary>
    public static class ApproachPlanner
    {
        public const float JunctionZ = 0f;
        private const float Mass = 1320f;
        private const float MotorForce = 6200f;
        private const float MaximumSpeed = 31f;
        private const float BoostForceScale = 2.1f;
        private const float BoostSpeedScale = 1.45f;
        private const float CruiseSpeed = 20f;
        private const float LinearDrag = 0.06f + 0.02f;
        private const float OverspeedDrag = 1.2f;
        private const float Step = 1f / 30f;
        private const int MaximumSteps = 600;

        /// <summary>
        /// Seconds to the junction from z at the given speed, assuming the current inputs are held:
        /// throttle held or not, and how many seconds of boost remain to be used.
        /// </summary>
        public static float SecondsToJunction(RunwayProfile runway, float z, float speed, bool throttleHeld, float boostSeconds)
        {
            float t = 0f;
            speed = Mathf.Max(0f, speed);
            for (int i = 0; i < MaximumSteps && z < JunctionZ; i++)
            {
                bool boosting = boostSeconds > 0f;
                if (boosting) boostSeconds -= Step;
                float limit = boosting ? MaximumSpeed * BoostSpeedScale : MaximumSpeed;
                float acceleration = 0f;
                bool throttle = throttleHeld || boosting || speed < CruiseSpeed;
                if (throttle && speed < limit) acceleration += MotorForce * (boosting ? BoostForceScale : 1f) / Mass;
                if (speed > limit) acceleration -= OverspeedDrag * (speed - limit);

                float grade = runway != null ? runway.GradeAt(z) : 0f;
                float slope = 1f / Mathf.Sqrt(1f + grade * grade);
                acceleration -= 9.81f * grade * slope;
                acceleration -= LinearDrag * speed;

                speed = Mathf.Max(0.5f, speed + acceleration * Step);
                z += speed * slope * Step;
                t += Step;
            }
            if (z < JunctionZ) t += (JunctionZ - z) / Mathf.Max(1f, speed);
            return t;
        }
    }
}

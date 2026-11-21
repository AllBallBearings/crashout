using UnityEngine;

namespace Crashout.Presentation
{
    /// <summary>Plastic crush zones plus chassis bending/torsion. Inputs are contact-normal speed and impulse/mass.</summary>
    public sealed class VehicleDamageShape
    {
        public float FrontCrush { get; private set; }
        public float RearCrush { get; private set; }
        public float LeftCrush { get; private set; }
        public float RightCrush { get; private set; }
        public float Bend { get; private set; }
        public float Twist { get; private set; }
        public float Strain { get; private set; }
        public float SideImpactZ { get; private set; }
        // A destroyed nose is common; a chassis separating through its passenger cell is exceptional.
        public bool ShouldFracture => Strain >= 2.8f && (Mathf.Abs(Bend) + Mathf.Abs(Twist) > 0.35f || (FrontCrush > 0.55f && RearCrush > 0.55f));

        public void Contact(Vector3 point, Vector3 push, float normalSpeed, float deltaVelocity, float halfLength, float softness, float pressureStep = 0f)
        {
            if (normalSpeed < 2.2f && pressureStep <= 0f) return;
            float coupling = Mathf.Clamp01(deltaVelocity / Mathf.Max(1f, normalSpeed * 0.25f));
            float severity = Mathf.Clamp((normalSpeed - 2.2f) * 0.012f * coupling + deltaVelocity * 0.025f, 0f, 0.65f) * softness;
            severity += Mathf.Clamp(pressureStep, 0f, 0.035f) * softness;
            float longitudinal = Mathf.Abs(push.z), lateral = Mathf.Abs(push.x);
            if (longitudinal > 0.35f)
            {
                if (point.z > 0f) FrontCrush = Mathf.Min(0.62f, FrontCrush + severity * longitudinal);
                else RearCrush = Mathf.Min(0.62f, RearCrush + severity * longitudinal);
            }
            if (lateral > 0.35f)
            {
                SideImpactZ = Mathf.Lerp(SideImpactZ, point.z, Mathf.Clamp01(severity * 4f));
                if (point.x > 0f) RightCrush = Mathf.Min(0.5f, RightCrush + severity * lateral);
                else LeftCrush = Mathf.Min(0.5f, LeftCrush + severity * lateral);
            }
            float lever = Mathf.Clamp(point.z / Mathf.Max(0.5f, halfLength), -1f, 1f);
            Bend = Mathf.Clamp(Bend + (push.x * lever - push.z * point.x) * severity * 0.45f, -0.5f, 0.5f);
            Twist = Mathf.Clamp(Twist + (push.y * point.x - push.x * point.y) * severity * 0.26f, -0.5f, 0.5f);
            if (normalSpeed >= 10f || pressureStep > 0f) Strain += severity * (0.5f + Mathf.Clamp01(deltaVelocity / 16f));
        }

        public Vector3 Map(Vector3 point, float halfLength, float halfWidth)
        {
            float z = Mathf.Clamp(point.z / Mathf.Max(0.5f, halfLength), -1f, 1f);
            // Collapse crush zones beyond the occupant cell with a positive slope. The old
            // quadratic map inverted the last part of a badly crushed nose and swallowed metal.
            float front = Mathf.Max(0f, z - 0.25f), rear = Mathf.Max(0f, -z - 0.25f);
            float side = point.x > 0f ? RightCrush : LeftCrush;
            float local = Mathf.Exp(-Mathf.Pow((point.z - SideImpactZ) / Mathf.Max(0.7f, halfLength * 0.65f), 2f));
            point.x -= Mathf.Sign(point.x) * side * Mathf.Max(0f, Mathf.Abs(point.x) - halfWidth * 0.2f) * local;
            point.z -= halfLength * (FrontCrush * front - RearCrush * rear);
            point.x += Bend * halfLength * z * z;
            float angle = Twist * z, c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            return new Vector3(point.x * c - point.y * s, point.x * s + point.y * c, point.z);
        }

        /// <summary>
        /// A compressed sheet stores its excess arc length in alternating plastic folds.
        /// For a triangular fold, sqrt(lambda² + (4A / wavelength)²) = 1: reducing projected
        /// length does not simply scale the metal away. The coarse collision cage uses Map.
        /// Both faces of a thin panel share the same folding direction so gauge stays finite.
        /// </summary>
        public Vector3 MapSurface(Vector3 point, Vector3 normal, float halfLength, float halfWidth)
        {
            Vector3 folded = point;
            float end = Mathf.Max(0f, Mathf.Abs(point.z) - halfLength * 0.25f);
            float crush = point.z >= 0f ? FrontCrush : RearCrush;
            if (end > 0f && crush > 0f)
            {
                const float wavelength = 0.48f;
                float amplitude = FoldAmplitude(crush, wavelength);
                float envelope = Mathf.Clamp01(end / 0.18f);
                float wave = TriangleWave(end / wavelength + point.x * 0.13f);
                // Hood/roof fold upwards; side skins fold about the lateral axis.
                Vector3 axis = Mathf.Abs(normal.y) > Mathf.Abs(normal.x)
                    ? Vector3.up : Vector3.right * (point.x >= 0f ? 1f : -1f);
                folded += axis * (amplitude * wave * envelope);
            }
            float side = point.x >= 0f ? RightCrush : LeftCrush;
            if (side > 0f)
            {
                float local = Mathf.Exp(-Mathf.Pow((point.z - SideImpactZ) / Mathf.Max(0.7f, halfLength * 0.65f), 2f));
                float edge = Mathf.Clamp01((Mathf.Abs(point.x) / halfWidth - 0.2f) / 0.3f);
                float amplitude = FoldAmplitude(side * local, 0.38f) * edge;
                folded.y += amplitude * TriangleWave((Mathf.Abs(point.x) - halfWidth * 0.2f) / 0.38f + point.z * 0.17f);
            }
            // Apply the cage displacement at the original point, preserving the fold's arc length.
            return Map(point, halfLength, halfWidth) + folded - point;
        }

        public static float FoldAmplitude(float compression, float wavelength)
        {
            float lambda = 1f - Mathf.Clamp01(compression);
            return wavelength * 0.25f * Mathf.Sqrt(Mathf.Max(0f, 1f - lambda * lambda));
        }

        private static float TriangleWave(float phase) => 1f - 4f * Mathf.Abs(Mathf.Repeat(phase + 0.25f, 1f) - 0.5f);
    }
}

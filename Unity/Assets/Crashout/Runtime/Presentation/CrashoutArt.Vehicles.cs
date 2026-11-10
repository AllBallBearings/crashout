using System.Collections.Generic;
using Crashout.Vehicle;
using UnityEngine;
using UnityEngine.Rendering;

namespace Crashout.Presentation
{
    /// <summary>
    /// Vehicles. Car bodies are lofted from side, plan and section profiles (wheel arches, shoulder
    /// tumblehome, raked glass), clear-coat painted, with lamp lenses, plates, mirrors, seams and
    /// real wheels (tread, spoked alloy rims, discs). Physics colliders are unchanged; this is visual.
    /// Parts of a car are merged per material into a few meshes the deformer can dent.
    /// </summary>
    public sealed partial class CrashoutArt
    {
        private readonly Dictionary<string, Mesh> wheelMeshes = new Dictionary<string, Mesh>();

        /// <summary>Profiles for one passenger-car shape. Heights are metres above the road.</summary>
        private sealed class CarDesign
        {
            public float Length = 3.15f;
            public float Ground;
            public float WheelRadius = 0.22f;
            public float TyreWidth = 0.19f;
            public float Track = 0.69f;
            public float[] TopZ, TopH;
            public float[] BottomZ, BottomH;
            public float[] WidthZ, WidthW;
            public float RearBase, RearTop, FrontTop, FrontBase;
            public float Belt = 0.62f, Roof = 0.93f, RoofRatio = 0.8f;
            public bool PaintedUpper;
            public bool Bed;
            public bool Sport;
            public float Exponent = 3.6f;

            public float Half => Length * 0.5f;
            public float Top(float z) => Keys(TopZ, TopH, z);
            public float Bottom(float z) => Keys(BottomZ, BottomH, z);
            public float Width(float z) => Keys(WidthZ, WidthW, z);
        }

        private static float Keys(float[] zs, float[] values, float z)
        {
            if (z <= zs[0]) return values[0];
            for (int i = 1; i < zs.Length; i++)
            {
                if (z > zs[i]) continue;
                float t = Mathf.InverseLerp(zs[i - 1], zs[i], z);
                t = t * t * (3f - 2f * t);
                return Mathf.Lerp(values[i - 1], values[i], t);
            }
            return values[values.Length - 1];
        }

        private static CarDesign DesignFor(VehicleClass vehicleClass, bool player)
        {
            float h = 1.575f;
            var d = new CarDesign
            {
                Ground = -VehicleSpec.For(vehicleClass).ColliderSize.y * 0.5f,
                BottomZ = new[] { -h, -1.3f, 1.3f, h },
                BottomH = new[] { 0.21f, 0.15f, 0.14f, 0.19f },
                WidthZ = new[] { -h, -1.42f, -0.9f, 0.9f, 1.35f, h },
                WidthW = new[] { 0.68f, 0.77f, 0.79f, 0.79f, 0.77f, 0.67f }
            };
            if (player)
            {
                h = 1.6f;
                d.Length = 3.2f;
                d.Ground = -0.405f;
                d.WheelRadius = 0.27f;
                d.TyreWidth = 0.23f;
                d.Track = 0.71f;
                d.Sport = true;
                d.TopZ = new[] { -h, -1.55f, -1.42f, -1.12f, 0.45f, 0.72f, 1.3f, 1.52f, h };
                d.TopH = new[] { 0.44f, 0.6f, 0.66f, 0.65f, 0.6f, 0.56f, 0.49f, 0.42f, 0.3f };
                d.BottomZ = new[] { -h, -1.3f, 1.3f, h };
                d.BottomH = new[] { 0.2f, 0.12f, 0.11f, 0.13f };
                d.WidthZ = new[] { -h, -1.45f, -0.95f, -0.45f, 0.5f, 1.3f, h };
                d.WidthW = new[] { 0.7f, 0.8f, 0.83f, 0.8f, 0.79f, 0.77f, 0.66f };
                d.RearBase = -1.14f; d.RearTop = -0.5f; d.FrontTop = -0.08f; d.FrontBase = 0.48f;
                d.Belt = 0.6f; d.Roof = 0.87f; d.RoofRatio = 0.74f;
                return d;
            }

            switch (vehicleClass)
            {
                case VehicleClass.Hatch:
                    d.TopZ = new[] { -h, -1.5f, -1.38f, 0.45f, 0.65f, 1.2f, 1.47f, h };
                    d.TopH = new[] { 0.5f, 0.62f, 0.64f, 0.63f, 0.6f, 0.55f, 0.48f, 0.36f };
                    d.RearBase = -1.38f; d.RearTop = -1.12f; d.FrontTop = 0f; d.FrontBase = 0.47f;
                    d.Roof = 0.97f;
                    break;
                case VehicleClass.Van:
                    d.TopZ = new[] { -h, -1.52f, 0.8f, 1.0f, 1.3f, 1.5f, h };
                    d.TopH = new[] { 0.55f, 0.66f, 0.66f, 0.64f, 0.58f, 0.52f, 0.4f };
                    d.RearBase = -1.56f; d.RearTop = -1.5f; d.FrontTop = 0.34f; d.FrontBase = 0.88f;
                    d.Belt = 0.66f; d.Roof = 1.28f; d.RoofRatio = 0.92f; d.PaintedUpper = true;
                    break;
                case VehicleClass.Pickup:
                    d.TopZ = new[] { -h, -1.5f, -0.36f, -0.3f, 0.55f, 0.75f, 1.25f, 1.48f, h };
                    d.TopH = new[] { 0.42f, 0.44f, 0.44f, 0.64f, 0.64f, 0.62f, 0.6f, 0.53f, 0.4f };
                    d.RearBase = -0.31f; d.RearTop = -0.28f; d.FrontTop = 0.14f; d.FrontBase = 0.6f;
                    d.Roof = 1.0f; d.RoofRatio = 0.86f; d.Bed = true;
                    d.WidthW = new[] { 0.76f, 0.78f, 0.79f, 0.79f, 0.78f, 0.7f };
                    break;
                default:
                    d.TopZ = new[] { -h, -1.53f, -1.4f, -1.05f, -0.95f, 0.5f, 0.7f, 1.25f, 1.48f, h };
                    d.TopH = new[] { 0.42f, 0.58f, 0.63f, 0.64f, 0.63f, 0.63f, 0.6f, 0.56f, 0.5f, 0.36f };
                    d.RearBase = -1.0f; d.RearTop = -0.58f; d.FrontTop = 0.02f; d.FrontBase = 0.52f;
                    break;
            }
            return d;
        }

        // ------------------------------------------------------------------ entry point

        public GameObject CreateCar(string name, Material paint, VehicleClass vehicleClass, bool player, out Transform[] frontPivots, out Transform[] rollPivots)
        {
            VehicleSpec spec = VehicleSpec.For(vehicleClass);
            GameObject root = new GameObject(name);
            Transform t = root.transform;
            var parts = new Dictionary<Material, MeshKit>();
            MeshKit Kit(Material m)
            {
                if (!parts.TryGetValue(m, out MeshKit kit)) parts[m] = kit = new MeshKit();
                return kit;
            }

            Material tail = new Material(taillightBase) { name = "Taillight " + name };
            Material indicator = new Material(indicatorBase) { name = "Indicator " + name };
            Material plate = plates[Mathf.Abs(name.GetHashCode()) % plates.Length];
            var detachables = new List<GameObject>();
            float ground, wheelRadius, tyreWidth, track;
            float halfLength;

            if (vehicleClass == VehicleClass.Bus)
            {
                ground = -spec.ColliderSize.y * 0.5f;
                wheelRadius = 0.5f; tyreWidth = 0.32f; track = 1.0f; halfLength = 3.75f;
                BuildBus(t, Kit, paint, tail, indicator, plate, ground, detachables);
            }
            else if (vehicleClass == VehicleClass.Tanker)
            {
                ground = -spec.ColliderSize.y * 0.5f;
                wheelRadius = 0.5f; tyreWidth = 0.32f; track = 1.0f; halfLength = 4f;
                BuildTanker(t, Kit, paint, tail, indicator, plate, ground, detachables);
            }
            else
            {
                CarDesign design = DesignFor(vehicleClass, player);
                ground = design.Ground;
                wheelRadius = design.WheelRadius; tyreWidth = design.TyreWidth; track = design.Track; halfLength = design.Half;
                BuildCarBody(t, Kit, design, paint, tail, indicator, plate, player, detachables);
            }

            // Merge per material. Paint first so it is the root's first renderer.
            var deformables = new List<MeshFilter>();
            var glassRenderers = new List<Renderer>();
            var ordered = new List<Material>(parts.Keys);
            ordered.Sort((a, b) => (a == paint ? 0 : 1).CompareTo(b == paint ? 0 : 1));
            foreach (Material material in ordered)
            {
                MeshKit kit = parts[material];
                bool emissive = material == headlight || material == tail || material == indicator;
                GameObject piece = MeshKit.Spawn(t, material == paint ? "Body" : material.name, kit.ToMesh(name + " " + material.name), material, !emissive && material != glass);
                deformables.Add(piece.GetComponent<MeshFilter>());
                if (material == glass) glassRenderers.Add(piece.GetComponent<Renderer>());
            }

            // Wheels: a steering pivot (also carries the caliper) and a rolling pivot.
            Vector3[] wheelPositions = spec.WheelPositions;
            frontPivots = new Transform[2];
            rollPivots = new Transform[wheelPositions.Length];
            bool heavy = vehicleClass == VehicleClass.Bus || vehicleClass == VehicleClass.Tanker;
            Mesh tyre = TyreMesh(wheelRadius, tyreWidth);
            Mesh rim = RimMesh(wheelRadius, tyreWidth, player ? 10 : heavy ? 8 : 5, heavy);
            Mesh disc = DiscMesh(wheelRadius, tyreWidth);
            for (int i = 0; i < wheelPositions.Length; i++)
            {
                bool left = wheelPositions[i].x < 0f;
                Vector3 position = new Vector3(left ? -track : track, ground + wheelRadius, wheelPositions[i].z);
                GameObject steerPivot = new GameObject("Wheel steer " + i);
                steerPivot.transform.SetParent(t, false);
                steerPivot.transform.localPosition = position;
                GameObject rollPivot = new GameObject("Wheel roll " + i);
                rollPivot.transform.SetParent(steerPivot.transform, false);

                Quaternion outward = left ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
                GameObject tyreObject = MeshKit.Spawn(rollPivot.transform, "Tyre", tyre, rubber);
                tyreObject.transform.localRotation = outward;
                GameObject rimObject = MeshKit.Spawn(rollPivot.transform, "Rim", rim, heavy ? steel : alloy);
                rimObject.transform.localRotation = outward;
                GameObject discObject = MeshKit.Spawn(rollPivot.transform, "Disc", disc, brakeDisc, false);
                discObject.transform.localRotation = outward;
                if (player && i < 4)
                {
                    GameObject calliper = Part(steerPivot.transform, "Caliper", new Vector3(left ? -0.02f : 0.02f, wheelRadius * 0.38f, -wheelRadius * 0.3f), new Vector3(0.07f, 0.12f, 0.16f), 0.025f, caliper, false);
                    calliper.transform.localRotation = Quaternion.Euler(-35f, 0f, 0f);
                }
                if (i < 2) frontPivots[i] = steerPivot.transform;
                rollPivots[i] = rollPivot.transform;
            }

            VehicleLook look = root.AddComponent<VehicleLook>();
            look.Configure(paint, tail, indicator, glassRenderers.ToArray(), shatteredGlass, halfLength);
            VehicleDeformer deformer = root.AddComponent<VehicleDeformer>();
            deformer.Configure(deformables, detachables, look, halfLength, heavy ? 0.45f : 1f);
            return root;
        }

        // ------------------------------------------------------------------ passenger cars

        private delegate MeshKit KitFor(Material material);

        private void BuildCarBody(Transform t, KitFor kit, CarDesign d, Material paint, Material tail, Material indicator, Material plate, bool player, List<GameObject> detachables)
        {
            float g = d.Ground;
            float half = d.Half;
            float[] wheelZ = { 0.98f, -0.98f };
            float archRadius = d.WheelRadius + 0.065f;

            // The lower body: superellipse sections between the floor (raised into wheel arches) and the shoulder line.
            const int stations = 46, ring = 28;
            kit(paint).Loft(stations, ring, (s, r) =>
            {
                float u = s / (float)(stations - 1);
                float z = -half + d.Length * (1f - Mathf.Cos(u * Mathf.PI)) * 0.5f;
                float top = d.Top(z);
                float bottom = d.Bottom(z);
                foreach (float wz in wheelZ)
                {
                    float dz = z - wz;
                    if (Mathf.Abs(dz) < archRadius) bottom = Mathf.Max(bottom, d.WheelRadius + Mathf.Sqrt(archRadius * archRadius - dz * dz));
                }
                bottom = Mathf.Min(bottom, top - 0.06f);
                float width = d.Width(z);
                float endPinch = ProceduralTextures.SmoothStep(0f, 1f, Mathf.Min(1f, (half - Mathf.Abs(z)) / 0.06f));
                width = Mathf.Lerp(width * 0.86f, width, endPinch);
                float mid = (top + bottom) * 0.5f, extent = (top - bottom) * 0.5f;
                float angle = r / (float)ring * Mathf.PI * 2f;
                float cx = Mathf.Cos(angle), sy = Mathf.Sin(angle);
                float e = 2f / d.Exponent;
                float y = mid + extent * Mathf.Sign(sy) * Mathf.Pow(Mathf.Abs(sy), e);
                float shoulder = 1f - 0.07f * Mathf.Clamp01((y - mid) / Mathf.Max(0.01f, extent));
                float x = width * shoulder * Mathf.Sign(cx) * Mathf.Pow(Mathf.Abs(cx), e);
                return new Vector3(x, g + y, z);
            }, true, true);

            // Glasshouse with tumblehome, then a painted roof skin and pillars over it.
            Material upper = d.PaintedUpper ? paint : glass;
            const int glassStations = 28, glassRing = 16;
            kit(upper).Loft(glassStations, glassRing, (s, r) => GreenPoint(d, Mathf.Lerp(d.RearBase, d.FrontBase, s / (float)(glassStations - 1)), r / (float)(glassRing - 1) * Mathf.PI, 0f), true, true);

            if (!d.PaintedUpper)
            {
                const int roofStations = 16, arc = 12;
                float r0 = d.RearTop - 0.05f, r1 = d.FrontTop + 0.06f;
                kit(paint).Loft(roofStations, arc * 2, (s, r) =>
                {
                    float z = Mathf.Lerp(r0, r1, s / (float)(roofStations - 1));
                    bool outer = r < arc;
                    int k = outer ? r : arc * 2 - 1 - r;
                    float theta = Mathf.Lerp(0.2f, 0.8f, k / (float)(arc - 1)) * Mathf.PI;
                    return GreenPoint(d, z, theta, outer ? 0.014f : 0.002f);
                }, true, true);

                for (int side = -1; side <= 1; side += 2)
                {
                    Pillar(kit(paint), d, side, d.FrontBase - 0.02f, d.FrontTop + 0.02f, 0.03f);
                    Pillar(kit(paint), d, side, d.RearBase + 0.02f, d.RearTop - 0.02f, d.Bed ? 0.03f : 0.05f);
                    float bz = Mathf.Lerp(d.RearTop, d.FrontTop, 0.48f);
                    Vector3 bLow = GreenPoint(d, bz, side > 0 ? 0.02f : Mathf.PI - 0.02f, 0.006f);
                    Vector3 bHigh = GreenPoint(d, bz, side > 0 ? 0.3f * Mathf.PI : 0.7f * Mathf.PI, 0.006f);
                    kit(trim).Cylinder(bLow, bHigh, 0.035f, 0.03f, 6);
                }
            }
            else
            {
                // Vans: glass set into the painted upper body.
                float wz0 = d.FrontBase - 0.02f, wz1 = d.FrontTop + 0.03f;
                Vector3 a = new Vector3(0f, g + d.Belt + 0.04f, wz0);
                Vector3 b = new Vector3(0f, g + d.Roof - 0.06f, wz1);
                Quaternion rake = Quaternion.LookRotation(b - a, Vector3.forward);
                kit(glass).Box((a + b) * 0.5f + Vector3.forward * 0.012f, new Vector3(d.Width(0.5f) * 1.7f, 0.02f, Vector3.Distance(a, b)), rake);
                for (int side = -1; side <= 1; side += 2)
                {
                    WindowPanel(kit(glass), d, side, 0.12f, 0.7f);
                    WindowPanel(kit(glass), d, side, -1.35f, -0.15f);
                }
            }

            // Pickup bed: floor, walls and tailgate.
            if (d.Bed)
            {
                float bedTop = g + 0.74f, floor = g + 0.44f;
                kit(trim).Box(new Vector3(0f, floor + 0.01f, -0.95f), new Vector3(1.38f, 0.02f, 1.2f), Quaternion.identity);
                for (int side = -1; side <= 1; side += 2)
                {
                    kit(paint).Box(new Vector3(side * 0.73f, (bedTop + floor) * 0.5f, -0.94f), new Vector3(0.07f, bedTop - floor, 1.24f), Quaternion.identity);
                    kit(chrome).Box(new Vector3(side * 0.73f, bedTop + 0.01f, -0.94f), new Vector3(0.08f, 0.02f, 1.24f), Quaternion.identity);
                }
                kit(paint).Box(new Vector3(0f, (bedTop + floor) * 0.5f, -0.34f), new Vector3(1.52f, bedTop - floor, 0.06f), Quaternion.identity);
                kit(paint).Box(new Vector3(0f, (bedTop + floor) * 0.5f - 0.02f, -1.555f), new Vector3(1.5f, bedTop - floor - 0.02f, 0.06f), Quaternion.identity);
            }

            // Dark chassis behind the arches so you never see through the car.
            kit(underbody).Box(new Vector3(0f, g + 0.3f, 0f), new Vector3(d.Width(0f) * 2f - 0.34f, 0.36f, d.Length - 0.5f), Quaternion.identity);

            // Lamps.
            float frontZ = half - 0.09f, rearZ = -half + 0.05f;
            for (int side = -1; side <= 1; side += 2)
            {
                float hy = g + d.Top(frontZ) - 0.07f;
                float hx = side * (d.Width(frontZ) - (d.Sport ? 0.24f : 0.21f));
                kit(headlight).Box(new Vector3(hx, hy, frontZ), new Vector3(d.Sport ? 0.36f : 0.32f, d.Sport ? 0.07f : 0.1f, 0.12f), Quaternion.Euler(-12f, side * -8f, 0f));
                kit(indicator).Box(new Vector3(side * (d.Width(frontZ) - 0.06f), hy - 0.02f, frontZ - 0.05f), new Vector3(0.07f, 0.06f, 0.12f), Quaternion.Euler(0f, side * -30f, 0f));
                float ty = g + d.Top(rearZ) - 0.08f;
                kit(tail).Box(new Vector3(side * (d.Width(rearZ) - 0.22f), ty, rearZ), new Vector3(0.36f, 0.1f, 0.08f), Quaternion.Euler(0f, side * 6f, 0f));
                kit(indicator).Box(new Vector3(side * (d.Width(rearZ) - 0.06f), ty, rearZ + 0.04f), new Vector3(0.06f, 0.1f, 0.1f), Quaternion.Euler(0f, side * 30f, 0f));

                // Mirror on a stalk.
                float mz = d.FrontBase - 0.12f;
                Vector3 mirror = new Vector3(side * (d.Width(mz) + 0.07f), g + d.Belt + 0.07f, mz);
                kit(paint).Box(mirror, new Vector3(0.1f, 0.09f, 0.15f), Quaternion.Euler(0f, side * 8f, 0f));
                kit(glass).Box(mirror + new Vector3(0f, 0f, -0.077f), new Vector3(0.08f, 0.07f, 0.005f), Quaternion.identity);
                kit(trim).Box(new Vector3(side * (d.Width(mz) + 0.01f), g + d.Belt + 0.05f, mz), new Vector3(0.08f, 0.03f, 0.05f), Quaternion.identity);

                // Door handles and panel seams.
                foreach (float hz in new[] { 0.12f, -0.62f })
                {
                    if (d.Bed && hz < -0.3f) continue;
                    kit(chrome).Box(new Vector3(side * (d.Width(hz) - 0.002f), g + d.Belt - 0.07f, hz), new Vector3(0.025f, 0.025f, 0.12f), Quaternion.identity);
                }
                foreach (float seam in new[] { d.FrontBase - 0.06f, Mathf.Lerp(d.RearTop, d.FrontTop, 0.48f), d.RearBase + 0.08f })
                {
                    float low = d.Bottom(seam) + 0.05f, high = d.Belt - 0.02f;
                    if (high <= low) continue;
                    kit(trim).Box(new Vector3(side * (d.Width(seam) - 0.004f), g + (low + high) * 0.5f, seam), new Vector3(0.012f, high - low, 0.008f), Quaternion.identity);
                }

                // Side skirt between the arches.
                kit(trim).Box(new Vector3(side * (d.Width(0f) - 0.03f), g + d.Bottom(0f) + 0.035f, 0f), new Vector3(0.05f, 0.07f, 1.96f - archRadius * 2f), Quaternion.identity);
            }

            // Grille, bumpers (detachable), plates, exhaust.
            float noseY = g + d.Top(half - 0.02f);
            kit(grille).Box(new Vector3(0f, noseY - 0.1f, half - 0.015f), new Vector3(d.Sport ? 0.5f : 0.7f, 0.12f, 0.05f), Quaternion.identity);
            if (d.Sport) kit(grille).Box(new Vector3(0f, g + 0.2f, half - 0.02f), new Vector3(1.1f, 0.1f, 0.06f), Quaternion.identity);
            detachables.Add(Bumper(t, "Front bumper", new Vector3(0f, g + 0.25f, half - 0.05f), new Vector3(d.Width(half - 0.1f) * 2f - 0.04f, 0.17f, 0.18f), paint, plate, true, g));
            detachables.Add(Bumper(t, "Rear bumper", new Vector3(0f, g + 0.3f, -half + 0.05f), new Vector3(d.Width(-half + 0.1f) * 2f - 0.04f, 0.17f, 0.18f), paint, plate, false, g));
            int pipes = d.Sport ? 2 : 1;
            for (int p = 0; p < pipes; p++)
            {
                float px = d.Sport ? (p == 0 ? -0.32f : 0.32f) : 0.42f;
                kit(chrome).Cylinder(new Vector3(px, g + 0.16f, -half + 0.12f), new Vector3(px, g + 0.16f, -half - 0.04f), 0.045f, 0.05f, 10);
                kit(trim).Cylinder(new Vector3(px, g + 0.16f, -half - 0.035f), new Vector3(px, g + 0.16f, -half - 0.042f), 0.036f, 0.036f, 10);
            }

            if (player)
            {
                // Rear wing, racing stripes, splitter and diffuser.
                for (int side = -1; side <= 1; side += 2)
                {
                    kit(trim).Box(new Vector3(side * 0.5f, g + 0.74f, -1.43f), new Vector3(0.05f, 0.16f, 0.12f), Quaternion.Euler(10f, 0f, 0f));
                }
                kit(paint).Box(new Vector3(0f, g + 0.83f, -1.46f), new Vector3(1.5f, 0.035f, 0.32f), Quaternion.Euler(-6f, 0f, 0f));
                kit(trim).Box(new Vector3(0f, g + 0.83f, -1.46f), new Vector3(1.52f, 0.02f, 0.06f), Quaternion.Euler(-6f, 0f, 0f));
                kit(trim).Box(new Vector3(0f, g + 0.1f, half - 0.08f), new Vector3(1.45f, 0.025f, 0.22f), Quaternion.identity);
                kit(trim).Box(new Vector3(0f, g + 0.13f, -half + 0.12f), new Vector3(1.3f, 0.05f, 0.25f), Quaternion.identity);
                TopStripe(kit(whitePaint), d, -0.17f, -0.05f);
                TopStripe(kit(whitePaint), d, 0.05f, 0.17f);
                // A hint of underglow.
                for (int side = -1; side <= 1; side += 2)
                {
                    kit(neon).Box(new Vector3(side * 0.7f, g + 0.1f, 0f), new Vector3(0.03f, 0.03f, 1.8f), Quaternion.identity);
                }
            }
        }

        /// <summary>A point on the glasshouse surface at z and section angle theta (0 = right belt, pi = left belt).</summary>
        private static Vector3 GreenPoint(CarDesign d, float z, float theta, float offset)
        {
            float height;
            if (z < d.RearTop)
            {
                float t = Mathf.Clamp01((z - d.RearBase) / Mathf.Max(0.01f, d.RearTop - d.RearBase));
                height = Mathf.Lerp(d.Belt, d.Roof, Mathf.Pow(Mathf.Sin(t * Mathf.PI * 0.5f), 0.8f));
            }
            else if (z > d.FrontTop)
            {
                float t = Mathf.Clamp01((d.FrontBase - z) / Mathf.Max(0.01f, d.FrontBase - d.FrontTop));
                height = Mathf.Lerp(d.Belt, d.Roof, Mathf.Pow(Mathf.Sin(t * Mathf.PI * 0.5f), 0.75f));
            }
            else
            {
                height = d.Roof + 0.015f * Mathf.Sin(Mathf.InverseLerp(d.RearTop, d.FrontTop, z) * Mathf.PI);
            }
            float baseY = d.Belt - 0.035f;
            float baseHalf = d.Width(z) - 0.06f;
            float topHalf = baseHalf * d.RoofRatio;
            float e = 2f / 3.2f;
            float c = Mathf.Cos(theta), s = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(theta)), e);
            float x = Mathf.Lerp(baseHalf, topHalf, s) * Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), e);
            float y = baseY + (height - baseY) * s;
            Vector3 point = new Vector3(x, d.Ground + y, z);
            if (offset != 0f)
            {
                Vector3 centre = new Vector3(0f, d.Ground + baseY, z);
                Vector3 outward = (point - centre);
                outward.z = 0f;
                point += outward.normalized * offset;
            }
            return point;
        }

        private static void Pillar(MeshKit kit, CarDesign d, int side, float fromZ, float toZ, float radius)
        {
            Vector3 previous = Vector3.zero;
            const int steps = 6;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float z = Mathf.Lerp(fromZ, toZ, t);
                float theta = Mathf.Lerp(0.03f, 0.3f, t) * Mathf.PI;
                if (side < 0) theta = Mathf.PI - theta;
                Vector3 p = GreenPoint(d, z, theta, 0.008f);
                if (i > 0) kit.Cylinder(previous, p, radius, radius, 6, false);
                previous = p;
            }
        }

        private static void WindowPanel(MeshKit kit, CarDesign d, int side, float z0, float z1)
        {
            float theta = side > 0 ? 0.16f * Mathf.PI : 0.84f * Mathf.PI;
            float z = (z0 + z1) * 0.5f;
            Vector3 low = GreenPoint(d, z, side > 0 ? 0.05f : Mathf.PI - 0.05f, 0.006f);
            Vector3 high = GreenPoint(d, z, theta, 0.006f);
            Vector3 centre = (low + high) * 0.5f;
            centre.y += 0.08f;
            kit.Box(centre, new Vector3(0.01f, Mathf.Abs(high.y - low.y) * 0.8f + 0.15f, z1 - z0), Quaternion.Euler(0f, 0f, side * -4f));
        }

        private void TopStripe(MeshKit kit, CarDesign d, float x0, float x1)
        {
            float half = d.Half;
            const int steps = 40;
            kit.Loft(steps, 4, (s, r) =>
            {
                float z = Mathf.Lerp(-half + 0.05f, half - 0.06f, s / (float)(steps - 1));
                bool inGlass = z > d.RearBase && z < d.FrontBase;
                float topY;
                if (inGlass)
                {
                    // Over the roof the stripe sits on the paint; across the screens it tucks under the glass.
                    bool onRoof = z > d.RearTop - 0.04f && z < d.FrontTop + 0.05f;
                    topY = GreenPoint(d, z, Mathf.PI * 0.5f, onRoof ? 0.018f : -0.04f).y;
                }
                else
                {
                    topY = d.Ground + d.Top(z) + 0.004f;
                }
                float x = r == 0 || r == 3 ? x0 : x1;
                float y = r < 2 ? topY + 0.001f : topY - 0.004f;
                return new Vector3(x, y, z);
            }, false, false);
        }

        private GameObject Bumper(Transform t, string name, Vector3 centre, Vector3 size, Material paint, Material plate, bool front, float ground)
        {
            GameObject bumper = new GameObject(name);
            bumper.transform.SetParent(t, false);
            bumper.transform.localPosition = centre;
            var body = new MeshKit();
            body.Append(ProceduralMeshes.RoundedBox(size, 0.07f, 2), Matrix4x4.identity);
            MeshKit.Spawn(bumper.transform, "Cover", body.ToMesh(name), paint);
            var lower = new MeshKit();
            lower.Box(new Vector3(0f, -size.y * 0.55f, front ? 0.02f : -0.02f), new Vector3(size.x - 0.2f, 0.05f, size.z), Quaternion.identity);
            MeshKit.Spawn(bumper.transform, "Lip", lower.ToMesh(name + " lip"), trim);
            var plateKit = new MeshKit();
            float facing = front ? 1f : -1f;
            plateKit.Panel(new Vector3(0f, front ? 0.01f : 0.12f, facing * (size.z * 0.5f + 0.006f)), new Vector3(0f, 0f, facing), Vector3.up, 0.36f, 0.11f);
            MeshKit.Spawn(bumper.transform, "Plate", plateKit.ToMesh(name + " plate"), plate, false);
            return bumper;
        }

        // ------------------------------------------------------------------ heavy vehicles

        private void BuildBus(Transform t, KitFor kit, Material paint, Material tail, Material indicator, Material plate, float g, List<GameObject> detachables)
        {
            float bottom = g + 0.36f, top = g + 2.6f;
            Vector3 size = new Vector3(2.3f, top - bottom, 7.4f);
            Vector3 centre = new Vector3(0f, (top + bottom) * 0.5f, 0f);
            kit(paint).Append(ProceduralMeshes.RoundedBox(size, 0.2f, 3), Matrix4x4.Translate(centre));
            // Livery band, skirt and roof pod.
            kit(whitePaint).Box(new Vector3(0f, g + 1.0f, 0f), new Vector3(2.32f, 0.16f, 7.2f), Quaternion.identity);
            kit(trim).Box(new Vector3(0f, g + 0.42f, 0f), new Vector3(2.33f, 0.14f, 7.25f), Quaternion.identity);
            kit(whitePaint).Append(ProceduralMeshes.RoundedBox(new Vector3(1.4f, 0.28f, 2.6f), 0.12f, 2), Matrix4x4.Translate(new Vector3(0f, top + 0.12f, -0.8f)));
            // Side windows with pillars.
            for (int side = -1; side <= 1; side += 2)
            {
                kit(glass).Box(new Vector3(side * 1.152f, g + 1.75f, -0.3f), new Vector3(0.02f, 0.95f, 6.0f), Quaternion.identity);
                for (float z = -3.2f; z <= 2.7f; z += 1.18f)
                {
                    kit(paint).Box(new Vector3(side * 1.162f, g + 1.75f, z), new Vector3(0.02f, 0.97f, 0.1f), Quaternion.identity);
                }
                // Wheel wells.
                foreach (float wz in new[] { 2.4f, -2.4f })
                {
                    kit(underbody).Box(new Vector3(side * 1.153f, g + 0.62f, wz), new Vector3(0.012f, 0.7f, 1.3f), Quaternion.identity);
                }
                // Mirrors on long arms.
                kit(trim).Cylinder(new Vector3(side * 1.1f, g + 2.2f, 3.6f), new Vector3(side * 1.45f, g + 2.1f, 3.85f), 0.025f, 0.025f, 6);
                kit(trim).Box(new Vector3(side * 1.48f, g + 1.85f, 3.86f), new Vector3(0.08f, 0.4f, 0.18f), Quaternion.identity);
                // Lamps.
                kit(headlight).Box(new Vector3(side * 0.82f, g + 0.72f, 3.71f), new Vector3(0.36f, 0.16f, 0.05f), Quaternion.identity);
                kit(indicator).Box(new Vector3(side * 1.06f, g + 0.72f, 3.69f), new Vector3(0.12f, 0.12f, 0.05f), Quaternion.identity);
                kit(tail).Box(new Vector3(side * 0.88f, g + 0.95f, -3.71f), new Vector3(0.3f, 0.32f, 0.05f), Quaternion.identity);
                kit(indicator).Box(new Vector3(side * 0.88f, g + 0.68f, -3.71f), new Vector3(0.2f, 0.12f, 0.05f), Quaternion.identity);
            }
            // Front and rear glass, doors and route sign.
            kit(glass).Box(new Vector3(0f, g + 1.75f, 3.705f), new Vector3(2.05f, 1.15f, 0.02f), Quaternion.Euler(-4f, 0f, 0f));
            kit(glass).Box(new Vector3(0f, g + 1.95f, -3.705f), new Vector3(1.8f, 0.7f, 0.02f), Quaternion.identity);
            kit(glass).Box(new Vector3(1.155f, g + 1.3f, 2.85f), new Vector3(0.02f, 1.8f, 0.95f), Quaternion.identity);
            kit(trim).Box(new Vector3(1.165f, g + 1.3f, 2.85f), new Vector3(0.02f, 1.8f, 0.04f), Quaternion.identity);
            kit(signalAmber).Box(new Vector3(0f, g + 2.45f, 3.71f), new Vector3(1.4f, 0.22f, 0.04f), Quaternion.identity);
            kit(grille).Box(new Vector3(0f, g + 0.95f, 3.71f), new Vector3(1.1f, 0.3f, 0.04f), Quaternion.identity);
            detachables.Add(Bumper(t, "Front bumper", new Vector3(0f, g + 0.48f, 3.72f), new Vector3(2.28f, 0.26f, 0.22f), trim, plate, true, g));
            detachables.Add(Bumper(t, "Rear bumper", new Vector3(0f, g + 0.48f, -3.72f), new Vector3(2.28f, 0.26f, 0.22f), trim, plate, false, g));
        }

        private void BuildTanker(Transform t, KitFor kit, Material paint, Material tail, Material indicator, Material plate, float g, List<GameObject> detachables)
        {
            // Chassis rails and fenders.
            kit(underbody).Box(new Vector3(0f, g + 0.62f, -0.1f), new Vector3(1.1f, 0.3f, 7.9f), Quaternion.identity);
            foreach (float wz in new[] { -1.6f, -3f })
            {
                kit(trim).Box(new Vector3(0f, g + 1.08f, wz), new Vector3(2.3f, 0.05f, 1.2f), Quaternion.identity);
            }

            // Cab-over tractor.
            Vector3 cab = new Vector3(0f, g + 1.75f, 3.08f);
            kit(paint).Append(ProceduralMeshes.RoundedBox(new Vector3(2.3f, 2.1f, 1.7f), 0.16f, 3), Matrix4x4.Translate(cab));
            kit(paint).Append(ProceduralMeshes.RoundedBox(new Vector3(2.0f, 0.5f, 1.2f), 0.15f, 2), Matrix4x4.Translate(cab + new Vector3(0f, 1.25f, -0.25f)));
            kit(glass).Box(cab + new Vector3(0f, 0.45f, 0.86f), new Vector3(2.0f, 0.85f, 0.02f), Quaternion.Euler(-6f, 0f, 0f));
            kit(chrome).Box(cab + new Vector3(0f, -0.55f, 0.86f), new Vector3(1.5f, 0.6f, 0.03f), Quaternion.identity);
            kit(grille).Box(cab + new Vector3(0f, -0.55f, 0.875f), new Vector3(1.36f, 0.5f, 0.01f), Quaternion.identity);
            for (int side = -1; side <= 1; side += 2)
            {
                kit(glass).Box(cab + new Vector3(side * 1.152f, 0.45f, 0.25f), new Vector3(0.02f, 0.7f, 0.9f), Quaternion.identity);
                kit(headlight).Box(cab + new Vector3(side * 0.85f, -0.75f, 0.86f), new Vector3(0.36f, 0.16f, 0.05f), Quaternion.identity);
                kit(indicator).Box(cab + new Vector3(side * 1.08f, -0.75f, 0.84f), new Vector3(0.1f, 0.14f, 0.05f), Quaternion.identity);
                // Exhaust stacks and fuel tanks.
                kit(chrome).Cylinder(new Vector3(side * 1.0f, g + 1.0f, 2.1f), new Vector3(side * 1.0f, g + 3.55f, 2.1f), 0.09f, 0.09f, 12);
                kit(chrome).Cylinder(new Vector3(side * 1.02f, g + 0.75f, 1.0f), new Vector3(side * 1.02f, g + 0.75f, 2.0f), 0.28f, 0.28f, 16);
                kit(trim).Cylinder(new Vector3(side * 1.1f, g + 2.4f, 3.95f), new Vector3(side * 1.4f, g + 2.4f, 4.0f), 0.025f, 0.025f, 6);
                kit(trim).Box(new Vector3(side * 1.43f, g + 2.15f, 4.0f), new Vector3(0.08f, 0.45f, 0.18f), Quaternion.identity);
                kit(tail).Box(new Vector3(side * 0.85f, g + 0.75f, -3.98f), new Vector3(0.3f, 0.18f, 0.05f), Quaternion.identity);
                kit(indicator).Box(new Vector3(side * 0.55f, g + 0.75f, -3.98f), new Vector3(0.16f, 0.14f, 0.05f), Quaternion.identity);
            }

            // Polished stainless tank with elliptical heads and a hazard band.
            float tankY = g + 1.62f, radius = 1.06f, front = 1.75f, back = -3.85f;
            var profile = new List<Vector2>();
            for (int i = 0; i <= 8; i++)
            {
                float a = i / 8f * Mathf.PI * 0.5f;
                profile.Add(new Vector2(Mathf.Sin(a) * radius, back + (1f - Mathf.Cos(a)) * 0.45f));
            }
            for (int i = 0; i <= 8; i++)
            {
                float a = i / 8f * Mathf.PI * 0.5f;
                profile.Add(new Vector2(Mathf.Cos(a) * radius, front - 0.45f + Mathf.Sin(a) * 0.45f));
            }
            Material polished = Lit("Polished tank", new Color(0.86f, 0.87f, 0.88f), 1f, 0.86f);
            kit(polished).LatheX(new Vector3(0f, tankY, 0f), profile, 32, Quaternion.Euler(0f, -90f, 0f));
            var band = new List<Vector2> { new Vector2(radius + 0.01f, -1.6f), new Vector2(radius + 0.012f, -1.55f), new Vector2(radius + 0.012f, -0.95f), new Vector2(radius + 0.01f, -0.9f) };
            kit(paint).LatheX(new Vector3(0f, tankY, 0f), band, 32, Quaternion.Euler(0f, -90f, 0f));
            kit(trim).Box(new Vector3(0f, tankY + radius + 0.05f, -1f), new Vector3(0.45f, 0.08f, 4.8f), Quaternion.identity);
            for (int h = 0; h < 3; h++)
            {
                kit(steel).Cylinder(new Vector3(0f, tankY + radius - 0.02f, -2.7f + h * 1.4f), new Vector3(0f, tankY + radius + 0.14f, -2.7f + h * 1.4f), 0.25f, 0.25f, 14);
            }
            // Hazard placard on the rear head.
            kit(signalRed).Box(new Vector3(0f, tankY - 0.2f, back - 0.02f), new Vector3(0.35f, 0.35f, 0.02f), Quaternion.Euler(0f, 0f, 45f));
            detachables.Add(Bumper(t, "Front bumper", new Vector3(0f, g + 0.55f, 3.95f), new Vector3(2.3f, 0.3f, 0.2f), chrome, plate, true, g));
            detachables.Add(Bumper(t, "Rear bumper", new Vector3(0f, g + 0.5f, -3.98f), new Vector3(2.2f, 0.2f, 0.16f), trim, plate, false, g));
        }

        // ------------------------------------------------------------------ wheels

        private Mesh TyreMesh(float radius, float width)
        {
            string key = "tyre" + radius.ToString("0.000") + width.ToString("0.000");
            if (wheelMeshes.TryGetValue(key, out Mesh cached)) return cached;
            float rim = radius * 0.68f;
            var profile = new List<Vector2>
            {
                new Vector2(rim, -width * 0.42f), new Vector2(radius * 0.84f, -width * 0.5f), new Vector2(radius * 0.96f, -width * 0.48f),
                new Vector2(radius, -width * 0.36f), new Vector2(radius, width * 0.36f), new Vector2(radius * 0.96f, width * 0.48f),
                new Vector2(radius * 0.84f, width * 0.5f), new Vector2(rim, width * 0.42f)
            };
            var kit = new MeshKit();
            kit.LatheX(Vector3.zero, profile, 32, Quaternion.identity);
            Mesh mesh = kit.ToMesh("Tyre");
            wheelMeshes[key] = mesh;
            return mesh;
        }

        private Mesh RimMesh(float radius, float width, int spokes, bool heavy)
        {
            string key = "rim" + radius.ToString("0.000") + width.ToString("0.000") + spokes;
            if (wheelMeshes.TryGetValue(key, out Mesh cached)) return cached;
            float rim = radius * 0.68f;
            var kit = new MeshKit();
            var barrel = new List<Vector2>
            {
                new Vector2(rim * 0.97f, -width * 0.4f), new Vector2(rim * 0.97f, width * 0.36f),
                new Vector2(rim * 1.03f, width * 0.43f), new Vector2(rim * 0.95f, width * 0.46f), new Vector2(rim * 0.9f, width * 0.42f)
            };
            kit.LatheX(Vector3.zero, barrel, 28, Quaternion.identity);
            kit.Cylinder(new Vector3(-width * 0.42f, 0f, 0f), new Vector3(-width * 0.38f, 0f, 0f), rim * 0.97f, rim * 0.97f, 20);
            float face = width * 0.36f;
            if (heavy)
            {
                // Steel truck wheel: dished disc with hand holes.
                kit.Cylinder(new Vector3(face - 0.04f, 0f, 0f), new Vector3(face, 0f, 0f), rim * 0.92f, rim * 0.92f, 24);
                kit.Cylinder(new Vector3(face, 0f, 0f), new Vector3(face + 0.04f, 0f, 0f), rim * 0.3f, rim * 0.22f, 16);
            }
            else
            {
                for (int s = 0; s < spokes; s++)
                {
                    float angle = s * 360f / spokes;
                    Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.right);
                    float spokeWidth = spokes > 6 ? rim * 0.09f : rim * 0.17f;
                    kit.Box(rotation * new Vector3(face, 0f, rim * 0.52f), new Vector3(0.03f, spokeWidth, rim * 0.82f), rotation * Quaternion.Euler(0f, 0f, 0f));
                }
                kit.Cylinder(new Vector3(face - 0.02f, 0f, 0f), new Vector3(face + 0.025f, 0f, 0f), rim * 0.26f, rim * 0.2f, 16);
            }
            for (int n = 0; n < 5; n++)
            {
                Vector3 offset = Quaternion.AngleAxis(n * 72f, Vector3.right) * new Vector3(0f, 0f, rim * 0.17f);
                kit.Cylinder(new Vector3(face + 0.02f, 0f, 0f) + offset, new Vector3(face + 0.04f, 0f, 0f) + offset, rim * 0.035f, rim * 0.03f, 6);
            }
            Mesh mesh = kit.ToMesh("Rim");
            wheelMeshes[key] = mesh;
            return mesh;
        }

        private Mesh DiscMesh(float radius, float width)
        {
            string key = "disc" + radius.ToString("0.000") + width.ToString("0.000");
            if (wheelMeshes.TryGetValue(key, out Mesh cached)) return cached;
            var kit = new MeshKit();
            kit.Cylinder(new Vector3(-0.02f, 0f, 0f), new Vector3(0.02f, 0f, 0f), radius * 0.56f, radius * 0.56f, 24);
            Mesh mesh = kit.ToMesh("Brake disc");
            wheelMeshes[key] = mesh;
            return mesh;
        }

        // ------------------------------------------------------------------ target marker

        public GameObject CreateTargetMarker(Transform vehicle, float height, Crashout.Simulation.CrashoutGameSession session, uint entityId)
        {
            GameObject marker = new GameObject("Target marker");
            marker.transform.SetParent(vehicle, false);
            var kit = new MeshKit();
            // A downward chevron over a halo ring.
            kit.Cylinder(new Vector3(0f, 0.55f, 0f), new Vector3(0f, -0.05f, 0f), 0.42f, 0.02f, 4);
            kit.Cylinder(new Vector3(0f, 0.85f, 0f), new Vector3(0f, 0.55f, 0f), 0.16f, 0.16f, 4);
            MeshKit.Spawn(marker.transform, "Chevron", kit.ToMesh("Target chevron"), targetGlow, false);
            var halo = new MeshKit();
            var ring = new List<Vector2> { new Vector2(0.62f, -0.02f), new Vector2(0.7f, 0f), new Vector2(0.62f, 0.02f), new Vector2(0.54f, 0f), new Vector2(0.62f, -0.02f) };
            halo.LatheX(Vector3.zero, ring, 32, Quaternion.Euler(0f, 0f, 90f));
            GameObject haloObject = MeshKit.Spawn(marker.transform, "Halo", halo.ToMesh("Target halo"), targetGlow, false);
            haloObject.transform.localPosition = new Vector3(0f, -0.4f, 0f);
            marker.AddComponent<TargetMarker>().Configure(session, entityId, vehicle, height);
            return marker;
        }
    }
}

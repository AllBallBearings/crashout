using System.Collections.Generic;
using Crashout.Vehicle;
using UnityEngine;
using UnityEngine.Rendering;

namespace Crashout.Presentation
{
    /// <summary>
    /// Profiled vehicle shells with open wheel arches, fitted glazing, interiors and separate panels.
    /// Materials are batched within each structural section; panels and wheels can detach independently.
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
            public float FrontAxle = 1.3f, RearAxle = -1.3f;

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
                return RefineDesign(d, vehicleClass, true);
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
            return RefineDesign(d, vehicleClass, false);
        }

        private static CarDesign RefineDesign(CarDesign d, VehicleClass kind, bool player)
        {
            Vector3 size = player ? new Vector3(1.86f, 1.23f, 4.25f) : VehicleSpec.For(kind).BodySize;
            float sx = size.x / (d.Width(0f) * 2f), sy = size.y / d.Roof, sz = size.z / d.Length;
            for (int i = 0; i < d.TopZ.Length; i++) { d.TopZ[i] *= sz; d.TopH[i] *= sy; }
            for (int i = 0; i < d.BottomZ.Length; i++) { d.BottomZ[i] *= sz; d.BottomH[i] *= sy; }
            for (int i = 0; i < d.WidthZ.Length; i++) { d.WidthZ[i] *= sz; d.WidthW[i] *= sx; }
            d.Length = size.z; d.Belt *= sy; d.Roof = size.y;
            d.WheelRadius = player ? 0.31f : VehicleSpec.For(kind).WheelRadius;
            d.FrontAxle = VehicleSpec.For(kind).WheelPositions[0].z; d.RearAxle = VehicleSpec.For(kind).WheelPositions[2].z;
            d.Track = size.x * 0.5f - 0.12f; d.TyreWidth = player ? 0.23f : 0.21f;
            d.RearBase = -size.z * 0.33f; d.RearTop = -size.z * 0.19f;
            d.FrontTop = size.z * 0.08f; d.FrontBase = size.z * 0.29f; d.RoofRatio = 0.82f;
            if (kind == VehicleClass.Hatch && !player) { d.RearBase = -size.z * 0.45f; d.RearTop = -size.z * 0.34f; }
            if (d.PaintedUpper) { d.RearBase = -size.z * 0.49f; d.RearTop = -size.z * 0.47f; d.FrontTop = size.z * 0.2f; d.FrontBase = size.z * 0.39f; d.RoofRatio = 0.94f; }
            if (d.Bed) { d.RearBase = -size.z * 0.15f; d.RearTop = -size.z * 0.13f; d.FrontTop = size.z * 0.15f; d.FrontBase = size.z * 0.3f; }
            return d;
        }

        // ------------------------------------------------------------------ entry point

        public GameObject CreateCar(string name, Material paint, VehicleClass vehicleClass, bool player, out Transform[] frontPivots, out Transform[] rollPivots)
        {
            return CreateCar(name, paint, vehicleClass, player, out frontPivots, out rollPivots, null, null);
        }

        public GameObject CreateCar(string name, Material paint, VehicleClass vehicleClass, bool player, out Transform[] frontPivots, out Transform[] rollPivots, string assetOverride, Vector3[] customWheelPositions)
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
            var deformables = new List<MeshFilter>();
            float ground, wheelRadius, tyreWidth, track;
            float halfLength;

            string assetName = !string.IsNullOrEmpty(assetOverride) ? assetOverride : (player ? "Player" : vehicleClass.ToString());
            VehicleBodyAsset asset = VehicleBodyAsset.Load(assetName);
            if (asset != null)
            {
                ground = asset.Ground; wheelRadius = asset.WheelRadius; tyreWidth = asset.TyreWidth;
                track = asset.Track; halfLength = asset.HalfLength;
                BuildVehicleAsset(t, asset, paint, tail, indicator, plate, detachables, deformables);
            }
            else if (vehicleClass == VehicleClass.Bus)
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
            Vector3[] wheelPositions = customWheelPositions ?? spec.WheelPositions;
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
                detachables.Add(steerPivot);
            }

            VehicleLook look = root.AddComponent<VehicleLook>();
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
                if (renderer.sharedMaterial == glass && !glassRenderers.Contains(renderer)) glassRenderers.Add(renderer);
            look.Configure(paint, tail, indicator, glassRenderers.ToArray(), shatteredGlass, halfLength);
            foreach (GameObject part in detachables)
            {
                if (part.name.StartsWith("Wheel steer")) continue; // Shared wheel meshes stay rigid.
                foreach (MeshFilter filter in part.GetComponentsInChildren<MeshFilter>())
                    if (!deformables.Contains(filter)) deformables.Add(filter);
            }
            VehicleDeformer deformer = root.AddComponent<VehicleDeformer>();
            deformer.Configure(deformables, detachables, look, halfLength, heavy ? 0.45f : 1f);
            return root;
        }

        // ------------------------------------------------------------------ passenger cars

        private delegate MeshKit KitFor(Material material);

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
            for (int side = -1; side <= 1; side += 2)
                Skin(kit(paint), 80, 10, (u, v) =>
                {
                    float z = Mathf.Lerp(-3.7f, 3.7f, u), low = 0.36f;
                    foreach (float axle in new[] { -2.4f, 2.4f })
                    {
                        float dz = z - axle;
                        if (Mathf.Abs(dz) < 0.56f) low = Mathf.Max(low, 0.5f + Mathf.Sqrt(0.56f * 0.56f - dz * dz));
                    }
                    return new Vector3(side * (1.15f - 0.055f * Mathf.Pow(v, 5f)), g + Mathf.Lerp(low, 2.6f, v), z);
                }, Vector3.right * side);
            Skin(kit(paint), 35, 17, (u, v) => new Vector3((v * 2f - 1f) * 1.1f, top + 0.07f * Mathf.Sin(v * Mathf.PI), Mathf.Lerp(-3.7f, 3.7f, u)), Vector3.up);
            for (int end = -1; end <= 1; end += 2)
                kit(paint).Append(ProceduralMeshes.RoundedBox(new Vector3(2.25f, top - bottom, 0.12f), 0.04f), Matrix4x4.Translate(new Vector3(0f, centre.y, end * 3.65f)));
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
            for (int side = -1; side <= 1; side += 2)
                Skin(kit(paint), 32, 10, (u, v) =>
                {
                    float z = Mathf.Lerp(2.23f, 3.93f, u), dz = z - 3f, low = 0.7f;
                    if (Mathf.Abs(dz) < 0.57f) low = Mathf.Max(low, 0.5f + Mathf.Sqrt(0.57f * 0.57f - dz * dz));
                    return new Vector3(side * (1.15f - 0.08f * v * v), g + Mathf.Lerp(low, 2.8f, v), z);
                }, Vector3.right * side);
            for (int end = -1; end <= 1; end += 2)
                kit(paint).Append(ProceduralMeshes.RoundedBox(new Vector3(2.2f, 2.1f, 0.12f), 0.04f), Matrix4x4.Translate(cab + Vector3.forward * (end * 0.79f)));
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

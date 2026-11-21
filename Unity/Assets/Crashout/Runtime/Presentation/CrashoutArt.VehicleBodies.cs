using System;
using System.Collections.Generic;
using UnityEngine;

namespace Crashout.Presentation
{
    public sealed partial class CrashoutArt
    {
        private static Mesh PanelShell(MeshKit skin, string name)
        {
            Mesh mesh = skin.ToMesh(name);
            Vector3[] front = mesh.vertices, normals = mesh.normals;
            Vector2[] uv = mesh.uv; int[] triangles = mesh.triangles;
            int count = front.Length;
            var points = new Vector3[count * 2]; var ns = new Vector3[count * 2]; var uvs = new Vector2[count * 2];
            var faces = new int[triangles.Length * 2];
            for (int i = 0; i < count; i++)
            {
                points[i] = front[i]; points[count + i] = front[i] - normals[i] * 0.009f;
                ns[i] = normals[i]; ns[count + i] = -normals[i]; uvs[i] = uvs[count + i] = uv[i];
            }
            System.Array.Copy(triangles, faces, triangles.Length);
            for (int i = 0; i < triangles.Length; i += 3)
            {
                faces[triangles.Length + i] = triangles[i] + count;
                faces[triangles.Length + i + 1] = triangles[i + 2] + count;
                faces[triangles.Length + i + 2] = triangles[i + 1] + count;
            }
            mesh.Clear(); mesh.vertices = points; mesh.normals = ns; mesh.uv = uvs; mesh.triangles = faces;
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            return mesh;
        }

        // Open panel surfaces: wheel openings belong to the wings, never the full chassis section.
        private static void Skin(MeshKit kit, int rows, int columns, Func<float, float, Vector3> point, Vector3 outward)
        {
            int start = kit.VertexCount;
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < columns; c++)
            {
                float u = r / (float)(rows - 1), v = c / (float)(columns - 1);
                Vector3 along = point(Mathf.Min(1f, u + 0.001f), v) - point(Mathf.Max(0f, u - 0.001f), v);
                Vector3 across = point(u, Mathf.Min(1f, v + 0.001f)) - point(u, Mathf.Max(0f, v - 0.001f));
                Vector3 normal = Vector3.Cross(along, across).normalized;
                if (Vector3.Dot(normal, outward) < 0f) normal = -normal;
                kit.Vertex(point(u, v), normal, new Vector2(u, v));
            }
            kit.Grid(start, rows, columns);
        }

        private static Vector3 CabinPoint(CarDesign d, float z, float across, float lift = 0f)
        {
            float roofBlend = z < d.RearTop ? Mathf.InverseLerp(d.RearBase, d.RearTop, z)
                : z > d.FrontTop ? 1f - Mathf.InverseLerp(d.FrontTop, d.FrontBase, z) : 1f;
            float width = d.Width(z) * Mathf.Lerp(0.94f, d.RoofRatio, roofBlend);
            float y = Mathf.Lerp(d.Belt, d.Roof, roofBlend) + 0.025f * (1f - across * across) * roofBlend;
            return new Vector3(across * width, d.Ground + y + lift, z);
        }

        private void BuildCarBody(Transform t, KitFor kit, CarDesign d, Material paint, Material tail, Material indicator, Material plate, bool player, List<GameObject> detachables)
        {
            float g = d.Ground, half = d.Half;
            float arch = d.WheelRadius + 0.055f;
            GameObject Panel(string name, Material material, MeshKit shape)
            {
                GameObject part = new GameObject(name); part.transform.SetParent(t, false);
                MeshKit.Spawn(part.transform, name + " skin", PanelShell(shape, name), material);
                detachables.Add(part); return part;
            }
            void Deck(string name, float z0, float z1)
            {
                if (z1 <= z0) return;
                var mesh = new MeshKit();
                Skin(mesh, 20, 13, (u, v) =>
                {
                    float z = Mathf.Lerp(z0, z1, u), x = v * 2f - 1f;
                    return new Vector3(x * d.Width(z) * 0.92f, g + d.Top(z) + 0.025f * (1f - x * x), z);
                }, Vector3.up);
                Panel(name, paint, mesh);
            }
            Deck("Hood", d.FrontBase + 0.015f, half - 0.04f);
            if (!d.Bed) Deck(d.PaintedUpper ? "Rear deck" : "Trunk lid", -half + 0.04f, d.RearBase - 0.015f);

            for (int side = -1; side <= 1; side += 2)
            {
                var doors = new GameObject[2];
                float SideBottom(float z)
                {
                    float bottom = d.Bottom(z);
                    foreach (float axle in new[] { d.FrontAxle, d.RearAxle })
                    {
                        float dz = z - axle;
                        if (Mathf.Abs(dz) < arch) bottom = Mathf.Max(bottom, d.WheelRadius + Mathf.Sqrt(arch * arch - dz * dz));
                    }
                    return Mathf.Min(bottom, d.Top(z) - 0.035f);
                }
                Vector3 SidePoint(float z, float v)
                {
                    float y = Mathf.Lerp(SideBottom(z), d.Top(z) - 0.02f, v);
                    float width = d.Width(z) - 0.025f * Mathf.Sin(v * Mathf.PI) - 0.035f * Mathf.Pow(1f - v, 3f);
                    return new Vector3(side * width, g + y, z);
                }
                // Fender/quarter panel and two individually removable door skins.
                float rearDoor = d.Bed ? d.RearBase : Mathf.Max(d.RearBase, d.RearAxle + arch);
                float frontDoor = Mathf.Min(d.FrontBase, d.FrontAxle - arch);
                float midDoor = Mathf.Lerp(rearDoor, frontDoor, 0.48f);
                float[] breaks = { -half, rearDoor, midDoor, frontDoor, half };
                for (int segment = 0; segment < 4; segment++)
                {
                    float from = breaks[segment] + (segment > 0 ? 0.004f : 0f), to = breaks[segment + 1] - 0.004f;
                    var panel = new MeshKit();
                    Skin(panel, segment == 0 || segment == 3 ? 26 : 12, 7, (u, v) => SidePoint(Mathf.Lerp(from, to, u), v), Vector3.right * side);
                    if (segment == 1 || segment == 2)
                    {
                        GameObject door = Panel((side > 0 ? "Right " : "Left ") + (segment == 2 ? "front door" : "rear door"), paint, panel);
                        doors[segment - 1] = door;
                        float hz = from + (to - from) * 0.22f;
                        Part(door.transform, "Recessed handle", new Vector3(side * (d.Width(hz) + 0.004f), g + d.Belt - 0.12f, hz), new Vector3(0.018f, 0.035f, 0.15f), 0.012f, trim, false);
                    }
                    else
                    {
                        Mesh wing = panel.ToMesh("Wing"); kit(paint).Append(wing, Matrix4x4.identity); UnityEngine.Object.Destroy(wing);
                    }
                }
                // Deep wheel tubs and a narrow rolled arch lip follow the tyre instead of a flat black patch.
                foreach (float axle in new[] { d.FrontAxle, d.RearAxle })
                {
                    Skin(kit(underbody), 23, 3, (u, v) =>
                    {
                        float a = u * Mathf.PI, z = axle + Mathf.Cos(a) * arch;
                        return new Vector3(side * (d.Track - 0.1f + v * 0.19f), g + d.WheelRadius + Mathf.Sin(a) * arch, z);
                    }, Vector3.right * -side);
                    Vector3 last = Vector3.zero;
                    for (int i = 0; i <= 24; i++)
                    {
                        float a = i / 24f * Mathf.PI, z = axle + Mathf.Cos(a) * arch;
                        Vector3 p = new Vector3(side * (d.Width(z) + 0.001f), g + d.WheelRadius + Mathf.Sin(a) * arch, z);
                        if (i > 0) kit(paint).Cylinder(last, p, 0.013f, 0.013f, 5, false);
                        last = p;
                    }
                }
                // Tapered side glass, window seals, and slim structural pillars.
                float split = Mathf.Lerp(d.RearTop, d.FrontTop, 0.48f);
                int windowIndex = 0;
                foreach (var span in new[] { new Vector2(d.RearBase + 0.035f, split - 0.035f), new Vector2(split + 0.035f, d.FrontBase - 0.035f) })
                {
                    var window = new MeshKit();
                    Skin(window, 16, 5, (u, v) =>
                    {
                        float z = Mathf.Lerp(span.x, span.y, u);
                        Vector3 bottom = new Vector3(side * d.Width(z) * 0.943f, g + d.Belt + 0.014f, z);
                        Vector3 top = CabinPoint(d, z, side, 0.004f);
                        return Vector3.Lerp(bottom, top, 0.025f + v * 0.95f);
                    }, Vector3.right * side);
                    MeshKit.Spawn(doors[windowIndex++].transform, "Door window", window.ToMesh("Fitted side window"), d.PaintedUpper && span.x < 0f ? paint : glass, false);
                }
                foreach (float z in new[] { d.RearBase, split, d.FrontBase })
                {
                    float topZ = z == d.RearBase ? d.RearTop : z == d.FrontBase ? d.FrontTop : z;
                    Vector3 low = new Vector3(side * d.Width(z) * 0.95f, g + d.Belt, z);
                    Vector3 high = CabinPoint(d, topZ, side);
                    kit(z == split ? trim : paint).Cylinder(low, high, z == split ? 0.025f : 0.035f, 0.028f, 8);
                }
                Vector3 mirror = new Vector3(side * (d.Width(d.FrontBase) + 0.1f), g + d.Belt + 0.045f, d.FrontBase - 0.12f);
                var mirrorSkin = new MeshKit(); mirrorSkin.Append(ProceduralMeshes.RoundedBox(new Vector3(0.15f, 0.11f, 0.24f), 0.045f), Matrix4x4.Translate(mirror));
                GameObject mirrorPart = Panel(side > 0 ? "Right mirror" : "Left mirror", paint, mirrorSkin);
                Part(mirrorPart.transform, "Mirror glass", mirror + Vector3.back * 0.12f, new Vector3(0.12f, 0.075f, 0.006f), 0.002f, chrome, false);
            }
            var roof = new MeshKit();
            Skin(roof, 18, 15, (u, v) => CabinPoint(d, Mathf.Lerp(d.RearTop, d.FrontTop, u), v * 2f - 1f, 0.008f), Vector3.up);
            Panel("Roof panel", paint, roof);
            foreach (bool front in new[] { false, true })
            {
                float low = front ? d.FrontBase : d.RearBase, high = front ? d.FrontTop : d.RearTop;
                Skin(kit(glass), 16, 15, (u, v) => CabinPoint(d, Mathf.Lerp(low + (high - low) * 0.025f, high, u), (v * 2f - 1f) * 0.965f, 0.002f), front ? Vector3.forward : Vector3.back);
                for (int side = -1; side <= 1; side += 2)
                    kit(trim).Cylinder(CabinPoint(d, low, side), CabinPoint(d, high, side), 0.012f, 0.012f, 6, false);
            }
            // Interior and structure become visible through glazing and when panels tear off.
            kit(underbody).Box(new Vector3(0f, g + 0.25f, 0f), new Vector3(d.Width(0f) * 1.6f, 0.17f, d.Length - 0.55f), Quaternion.identity);
            kit(interior).Box(new Vector3(0f, g + d.Belt - 0.1f, d.FrontBase - 0.15f), new Vector3(d.Width(0f) * 1.65f, 0.14f, 0.27f), Quaternion.identity);
            for (int side = -1; side <= 1; side += 2)
            {
                kit(interior).Append(ProceduralMeshes.RoundedBox(new Vector3(0.48f, 0.48f, 0.16f), 0.055f), Matrix4x4.TRS(new Vector3(side * 0.43f, g + 0.72f, -0.1f), Quaternion.Euler(-12f, 0f, 0f), Vector3.one));
                kit(interior).Box(new Vector3(side * 0.43f, g + 0.46f, 0.06f), new Vector3(0.48f, 0.12f, 0.52f), Quaternion.identity);
                kit(underbody).Box(new Vector3(side * 0.42f, g + 0.18f, 0f), new Vector3(0.095f, 0.15f, d.Length - 0.5f), Quaternion.identity);
            }
            if (d.Bed)
            {
                float length = d.RearBase + half - 0.1f, z = (-half + d.RearBase) * 0.5f;
                kit(trim).Box(new Vector3(0f, g + 0.48f, z), new Vector3(1.55f, 0.035f, length), Quaternion.identity);
                for (int side = -1; side <= 1; side += 2)
                    kit(paint).Box(new Vector3(side * 0.88f, g + 0.73f, z), new Vector3(0.09f, 0.48f, length), Quaternion.identity);
            }
            foreach (int end in new[] { -1, 1 })
                Skin(kit(paint), 6, 15, (u, v) =>
                {
                    float z = end * (half - 0.035f), x = v * 2f - 1f;
                    return new Vector3(x * d.Width(z), g + Mathf.Lerp(0.3f, d.Top(z) - 0.01f, u), z);
                }, Vector3.forward * end);
            for (int side = -1; side <= 1; side += 2)
            {
                float y = g + d.Top(half - 0.12f) - 0.06f;
                kit(trim).Append(ProceduralMeshes.RoundedBox(new Vector3(0.43f, 0.145f, 0.12f), 0.04f), Matrix4x4.Translate(new Vector3(side * 0.59f, y, half - 0.045f)));
                kit(headlight).Box(new Vector3(side * 0.59f, y + 0.015f, half + 0.018f), new Vector3(0.36f, 0.035f, 0.01f), Quaternion.identity);
                kit(tail).Box(new Vector3(side * 0.59f, g + d.Top(-half + 0.12f) - 0.06f, -half - 0.012f), new Vector3(0.4f, 0.055f, 0.035f), Quaternion.identity);
                kit(indicator).Box(new Vector3(side * 0.81f, y, half - 0.03f), new Vector3(0.04f, 0.06f, 0.07f), Quaternion.identity);
            }
            kit(grille).Box(new Vector3(0f, g + 0.4f, half - 0.015f), new Vector3(d.Sport ? 1.05f : 0.72f, 0.18f, 0.06f), Quaternion.identity);
            detachables.Add(Bumper(t, "Front bumper", new Vector3(0f, g + 0.3f, half - 0.015f), new Vector3(d.Width(half) * 2f, 0.22f, 0.19f), paint, plate, true, g));
            detachables.Add(Bumper(t, "Rear bumper", new Vector3(0f, g + 0.32f, -half + 0.015f), new Vector3(d.Width(-half) * 2f, 0.23f, 0.19f), paint, plate, false, g));

            GameObject cabin = new GameObject("Cabin contact"); cabin.transform.SetParent(t, false); cabin.layer = 8;
            BoxCollider box = cabin.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, g + (d.Belt + d.Roof) * 0.5f, (d.RearTop + d.FrontTop) * 0.5f);
            box.size = new Vector3(d.Width(0f) * d.RoofRatio * 1.85f, d.Roof - d.Belt, d.FrontTop - d.RearTop + 0.3f);
        }
    }
}

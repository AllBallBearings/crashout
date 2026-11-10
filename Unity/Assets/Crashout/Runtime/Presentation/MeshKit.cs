using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Crashout.Presentation
{
    /// <summary>
    /// A small mesh construction kit: lofts, lathes, ribbons, tubes and boxes with metre-scaled UVs,
    /// plus mesh combining. Triangles are wound so their geometric normal agrees with the supplied
    /// vertex normals, so callers never think about winding order.
    /// </summary>
    public sealed class MeshKit
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<Color> colours = new List<Color>();
        private readonly List<int> triangles = new List<int>();

        public int VertexCount => vertices.Count;

        public int Vertex(Vector3 position, Vector3 normal, Vector2 uv)
        {
            vertices.Add(position);
            normals.Add(normal.sqrMagnitude > 1e-8f ? normal.normalized : Vector3.up);
            uvs.Add(uv);
            colours.Add(Color.white);
            return vertices.Count - 1;
        }

        /// <summary>Adds a triangle, flipping it if needed so it faces along its vertex normals.</summary>
        public void Triangle(int a, int b, int c)
        {
            Vector3 geometric = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            Vector3 shading = normals[a] + normals[b] + normals[c];
            if (Vector3.Dot(geometric, shading) < 0f)
            {
                triangles.Add(a); triangles.Add(c); triangles.Add(b);
            }
            else
            {
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
            }
        }

        public void Quad(int a, int b, int c, int d)
        {
            Triangle(a, b, c);
            Triangle(a, c, d);
        }

        /// <summary>Joins a grid of rows × columns vertices (row-major) into quads.</summary>
        public void Grid(int start, int rows, int columns, bool wrapColumns = false)
        {
            int columnQuads = wrapColumns ? columns : columns - 1;
            for (int r = 0; r < rows - 1; r++)
            {
                for (int c = 0; c < columnQuads; c++)
                {
                    int c1 = (c + 1) % columns;
                    Quad(start + r * columns + c, start + r * columns + c1, start + (r + 1) * columns + c1, start + (r + 1) * columns + c);
                }
            }
        }

        /// <summary>Axis-aligned (then rotated) box with flat faces and metre UVs.</summary>
        public void Box(Vector3 centre, Vector3 size, Quaternion rotation)
        {
            Vector3 h = size * 0.5f;
            Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };
            for (int axis = 0; axis < 3; axis++)
            {
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    Vector3 n = axes[axis] * sign;
                    Vector3 u = axes[(axis + 1) % 3];
                    Vector3 v = axes[(axis + 2) % 3];
                    int s = vertices.Count;
                    for (int j = 0; j < 2; j++)
                    {
                        for (int i = 0; i < 2; i++)
                        {
                            Vector3 local = Vector3.Scale(n + u * (i * 2 - 1) + v * (j * 2 - 1), h);
                            Vector2 uv = new Vector2(Vector3.Dot(local, u), Vector3.Dot(local, v));
                            Vertex(centre + rotation * local, rotation * n, uv);
                        }
                    }
                    Quad(s, s + 1, s + 3, s + 2);
                }
            }
        }

        /// <summary>
        /// A strip between two edge polylines (same count). Normals follow the surface and are turned
        /// to agree with <paramref name="outwardHint"/>. U runs 0..1 across, V is distance along × vScale.
        /// </summary>
        public void Strip(IList<Vector3> left, IList<Vector3> right, Vector3 outwardHint, float vScale, float uWidth = 1f)
        {
            int count = Mathf.Min(left.Count, right.Count);
            if (count < 2) return;
            int start = vertices.Count;
            float along = 0f;
            for (int i = 0; i < count; i++)
            {
                if (i > 0) along += Vector3.Distance((left[i] + right[i]) * 0.5f, (left[i - 1] + right[i - 1]) * 0.5f);
                Vector3 tangent = (left[Mathf.Min(count - 1, i + 1)] + right[Mathf.Min(count - 1, i + 1)]) - (left[Mathf.Max(0, i - 1)] + right[Mathf.Max(0, i - 1)]);
                Vector3 across = right[i] - left[i];
                Vector3 n = Vector3.Cross(tangent, across);
                if (Vector3.Dot(n, outwardHint) < 0f) n = -n;
                Vertex(left[i], n, new Vector2(0f, along * vScale));
                Vertex(right[i], n, new Vector2(uWidth, along * vScale));
            }
            for (int i = 0; i < count - 1; i++)
            {
                int a = start + i * 2;
                Quad(a, a + 1, a + 3, a + 2);
            }
        }

        /// <summary>A single quad facing <paramref name="normal"/> with 0..1 UVs that read correctly to a viewer.</summary>
        public void Panel(Vector3 centre, Vector3 normal, Vector3 up, float width, float height)
        {
            normal.Normalize();
            Vector3 right = Vector3.Cross(normal, up).normalized;
            Vector3 trueUp = Vector3.Cross(right, normal).normalized * -1f;
            if (Vector3.Dot(trueUp, up) < 0f) trueUp = -trueUp;
            Vector3 r = right * (width * 0.5f), u = trueUp * (height * 0.5f);
            int a = Vertex(centre - r - u, normal, new Vector2(0f, 0f));
            int b = Vertex(centre + r - u, normal, new Vector2(1f, 0f));
            int c = Vertex(centre + r + u, normal, new Vector2(1f, 1f));
            int d = Vertex(centre - r + u, normal, new Vector2(0f, 1f));
            Quad(a, b, c, d);
        }

        /// <summary>
        /// Lofts closed cross-sections. <paramref name="section"/> returns the point for station s
        /// (0..stations-1) and ring index r (0..ringCount-1); the ring is closed automatically.
        /// End caps are fans to the section centroids.
        /// </summary>
        public void Loft(int stations, int ringCount, Func<int, int, Vector3> section, bool capStart, bool capEnd, float uvScale = 1f)
        {
            int start = vertices.Count;
            var points = new Vector3[stations, ringCount];
            for (int s = 0; s < stations; s++)
            {
                for (int r = 0; r < ringCount; r++) points[s, r] = section(s, r);
            }

            float[] along = new float[stations];
            for (int s = 1; s < stations; s++)
            {
                Vector3 a = Vector3.zero, b = Vector3.zero;
                for (int r = 0; r < ringCount; r++) { a += points[s - 1, r]; b += points[s, r]; }
                along[s] = along[s - 1] + Vector3.Distance(a, b) / ringCount;
            }

            for (int s = 0; s < stations; s++)
            {
                Vector3 centroid = Vector3.zero;
                for (int r = 0; r < ringCount; r++) centroid += points[s, r];
                centroid /= ringCount;
                float around = 0f;
                for (int r = 0; r <= ringCount; r++)
                {
                    int ri = r % ringCount;
                    Vector3 p = points[s, ri];
                    if (r > 0) around += Vector3.Distance(points[s, (r - 1) % ringCount], p);
                    Vector3 prev = points[s, (ri - 1 + ringCount) % ringCount];
                    Vector3 next = points[s, (ri + 1) % ringCount];
                    Vector3 sPrev = points[Mathf.Max(0, s - 1), ri];
                    Vector3 sNext = points[Mathf.Min(stations - 1, s + 1), ri];
                    Vector3 n = Vector3.Cross(next - prev, sNext - sPrev);
                    Vector3 outward = p - centroid;
                    if (Vector3.Dot(n, outward) < 0f) n = -n;
                    if (n.sqrMagnitude < 1e-10f) n = outward;
                    Vertex(p, n, new Vector2(around, along[s]) * uvScale);
                }
            }
            Grid(start, stations, ringCount + 1);

            if (capStart) Cap(points, 0, ringCount, -1f);
            if (capEnd) Cap(points, stations - 1, ringCount, 1f);
        }

        private void Cap(Vector3[,] points, int station, int ringCount, float direction)
        {
            Vector3 centroid = Vector3.zero;
            for (int r = 0; r < ringCount; r++) centroid += points[station, r];
            centroid /= ringCount;
            int neighbour = Mathf.Clamp(station - (int)direction, 0, points.GetLength(0) - 1);
            Vector3 inner = Vector3.zero;
            for (int r = 0; r < ringCount; r++) inner += points[neighbour, r];
            inner /= ringCount;
            Vector3 n = (centroid - inner).sqrMagnitude > 1e-8f ? (centroid - inner).normalized : Vector3.forward * direction;
            int c = Vertex(centroid, n, Vector2.zero);
            int first = vertices.Count;
            for (int r = 0; r < ringCount; r++) Vertex(points[station, r], n, new Vector2(points[station, r].x, points[station, r].y));
            for (int r = 0; r < ringCount; r++) Triangle(c, first + r, first + (r + 1) % ringCount);
        }

        /// <summary>Revolves a profile (radius, axial offset) around the local X axis. Used for wheels and tanks.</summary>
        public void LatheX(Vector3 centre, IList<Vector2> profile, int segments, Quaternion rotation, float uvAround = 1f)
        {
            int start = vertices.Count;
            for (int p = 0; p < profile.Count; p++)
            {
                Vector2 prev = profile[Mathf.Max(0, p - 1)], next = profile[Mathf.Min(profile.Count - 1, p + 1)];
                Vector2 tangent = next - prev; // (dRadius, dAxis)
                for (int s = 0; s <= segments; s++)
                {
                    float a = s / (float)segments * Mathf.PI * 2f;
                    Vector3 radial = new Vector3(0f, Mathf.Cos(a), Mathf.Sin(a));
                    Vector3 position = radial * profile[p].x + Vector3.right * profile[p].y;
                    // Normal is perpendicular to the profile tangent in the (axis, radial) plane.
                    Vector3 n = radial * tangent.y - Vector3.right * tangent.x;
                    if (n.sqrMagnitude < 1e-8f) n = radial;
                    Vertex(centre + rotation * position, rotation * n, new Vector2(s / (float)segments * uvAround, p / (float)(profile.Count - 1)));
                }
            }
            Grid(start, profile.Count, segments + 1);
        }

        /// <summary>A capped cylinder between two points.</summary>
        public void Cylinder(Vector3 from, Vector3 to, float radiusFrom, float radiusTo, int segments, bool caps = true)
        {
            Vector3 axis = to - from;
            float length = axis.magnitude;
            if (length < 1e-5f) return;
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, axis / length);
            int start = vertices.Count;
            float slope = (radiusFrom - radiusTo) / length;
            for (int ring = 0; ring < 2; ring++)
            {
                float radius = ring == 0 ? radiusFrom : radiusTo;
                for (int s = 0; s <= segments; s++)
                {
                    float a = s / (float)segments * Mathf.PI * 2f;
                    Vector3 radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    Vector3 local = radial * radius + Vector3.up * (ring * length);
                    Vector3 n = (radial + Vector3.up * slope).normalized;
                    Vertex(from + rotation * local, rotation * n, new Vector2(s / (float)segments * Mathf.PI * 2f * Mathf.Max(radiusFrom, radiusTo), ring * length));
                }
            }
            Grid(start, 2, segments + 1);
            if (!caps) return;
            for (int end = 0; end < 2; end++)
            {
                float radius = end == 0 ? radiusFrom : radiusTo;
                if (radius < 1e-4f) continue;
                Vector3 n = rotation * (end == 0 ? Vector3.down : Vector3.up);
                Vector3 centre = end == 0 ? from : to;
                int c = Vertex(centre, n, Vector2.zero);
                int first = vertices.Count;
                for (int s = 0; s < segments; s++)
                {
                    float a = s / (float)segments * Mathf.PI * 2f;
                    Vector3 local = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                    Vertex(centre + rotation * local, n, new Vector2(local.x, local.z));
                }
                for (int s = 0; s < segments; s++) Triangle(c, first + s, first + (s + 1) % segments);
            }
        }

        /// <summary>A lumpy sphere for foliage and boulders: an icosphere displaced by value noise.</summary>
        public void Blob(Vector3 centre, Vector3 radii, int subdivisions, float roughness, int seed)
        {
            var points = new List<Vector3>();
            var faces = new List<int>();
            Icosphere(points, faces, subdivisions);
            int start = vertices.Count;
            foreach (Vector3 p in points)
            {
                float n = ProceduralTextures.Value(p.x * 2.3f + 11f, p.y * 2.3f + p.z * 1.7f + 5f, 0, seed);
                float n2 = ProceduralTextures.Value(p.z * 5f + 3f, p.x * 5f - p.y * 3f, 0, seed + 3);
                float k = 1f + (n - 0.5f) * roughness + (n2 - 0.5f) * roughness * 0.4f;
                Vector3 position = Vector3.Scale(p * k, radii);
                Vertex(centre + position, Vector3.Scale(p, new Vector3(1f / radii.x, 1f / radii.y, 1f / radii.z)),
                    new Vector2(Mathf.Atan2(p.z, p.x) * radii.x, p.y * radii.y));
            }
            for (int i = 0; i < faces.Count; i += 3) Triangle(start + faces[i], start + faces[i + 1], start + faces[i + 2]);
        }

        private static void Icosphere(List<Vector3> points, List<int> faces, int subdivisions)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            Vector3[] seed =
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            };
            foreach (Vector3 p in seed) points.Add(p.normalized);
            int[] f =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };
            faces.AddRange(f);
            for (int s = 0; s < subdivisions; s++)
            {
                var cache = new Dictionary<long, int>();
                var next = new List<int>();
                for (int i = 0; i < faces.Count; i += 3)
                {
                    int a = faces[i], b = faces[i + 1], c = faces[i + 2];
                    int ab = Midpoint(points, cache, a, b), bc = Midpoint(points, cache, b, c), ca = Midpoint(points, cache, c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                faces.Clear();
                faces.AddRange(next);
            }
        }

        private static int Midpoint(List<Vector3> points, Dictionary<long, int> cache, int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (cache.TryGetValue(key, out int index)) return index;
            points.Add(((points[a] + points[b]) * 0.5f).normalized);
            cache[key] = points.Count - 1;
            return points.Count - 1;
        }

        /// <summary>Appends another mesh transformed by <paramref name="matrix"/>.</summary>
        public void Append(Mesh mesh, Matrix4x4 matrix)
        {
            var v = mesh.vertices;
            var n = mesh.normals;
            var uv = mesh.uv;
            var t = mesh.triangles;
            int start = vertices.Count;
            Matrix4x4 normalMatrix = matrix.inverse.transpose;
            for (int i = 0; i < v.Length; i++)
            {
                vertices.Add(matrix.MultiplyPoint3x4(v[i]));
                normals.Add(n.Length > i ? normalMatrix.MultiplyVector(n[i]).normalized : Vector3.up);
                uvs.Add(uv.Length > i ? uv[i] : Vector2.zero);
                colours.Add(Color.white);
            }
            for (int i = 0; i < t.Length; i++) triangles.Add(start + t[i]);
        }

        public Mesh ToMesh(string name, bool recalculateNormals = false)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            if (recalculateNormals) mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        // ------------------------------------------------------------------ scene helpers

        public static GameObject Spawn(Transform parent, string name, Mesh mesh, Material material, bool castShadows = true, bool collide = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            if (collide) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }
    }

    /// <summary>Gathers many small pieces per material and emits one combined renderer per material.</summary>
    public sealed class BatchedMeshes
    {
        private readonly Dictionary<Material, MeshKit> kits = new Dictionary<Material, MeshKit>();
        private readonly Dictionary<Material, bool> shadows = new Dictionary<Material, bool>();

        public MeshKit For(Material material, bool castShadows = true)
        {
            if (!kits.TryGetValue(material, out MeshKit kit))
            {
                kit = new MeshKit();
                kits[material] = kit;
                shadows[material] = castShadows;
            }
            return kit;
        }

        public void Emit(Transform parent, string name)
        {
            foreach (KeyValuePair<Material, MeshKit> entry in kits)
            {
                if (entry.Value.VertexCount == 0) continue;
                MeshKit.Spawn(parent, name + " (" + entry.Key.name + ")", entry.Value.ToMesh(name), entry.Key, shadows[entry.Key]);
            }
            kits.Clear();
        }
    }
}

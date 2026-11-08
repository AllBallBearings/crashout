using System.Collections.Generic;
using UnityEngine;

namespace Crashout.Presentation
{
    /// <summary>
    /// Generates beveled boxes so every prop catches highlights on its edges.
    /// UVs are laid out in metres so a single material tiling fits any box size.
    /// </summary>
    public static class ProceduralMeshes
    {
        private static readonly Dictionary<(Vector3Int, int), Mesh> Cache = new Dictionary<(Vector3Int, int), Mesh>();

        public static Mesh RoundedBox(Vector3 size, float radius, int segments = 3)
        {
            radius = Mathf.Min(radius, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.5f - 0.001f);
            var key = (new Vector3Int(Mathf.RoundToInt(size.x * 1000f), Mathf.RoundToInt(size.y * 1000f), Mathf.RoundToInt(size.z * 1000f)),
                Mathf.RoundToInt(radius * 1000f) * 16 + segments);
            if (Cache.TryGetValue(key, out Mesh cached) && cached != null) return cached;

            Vector3 half = size * 0.5f;
            Vector3 inner = half - Vector3.one * radius;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            // Six faces, each a (segments*2+2)^2 grid pushed out onto the rounded surface.
            Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };
            int grid = segments * 2 + 2;
            for (int axis = 0; axis < 3; axis++)
            {
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    Vector3 n = axes[axis] * sign;
                    Vector3 u = axes[(axis + 1) % 3];
                    Vector3 v = axes[(axis + 2) % 3];
                    int start = vertices.Count;
                    for (int j = 0; j < grid; j++)
                    {
                        for (int i = 0; i < grid; i++)
                        {
                            float a = i / (float)(grid - 1) * 2f - 1f;
                            float b = j / (float)(grid - 1) * 2f - 1f;
                            // Cluster samples near the edges where the curvature lives.
                            a = Mathf.Sign(a) * Mathf.Pow(Mathf.Abs(a), 0.7f);
                            b = Mathf.Sign(b) * Mathf.Pow(Mathf.Abs(b), 0.7f);
                            Vector3 p = Vector3.Scale(n + u * a + v * b, half);
                            Vector3 clamped = new Vector3(
                                Mathf.Clamp(p.x, -inner.x, inner.x),
                                Mathf.Clamp(p.y, -inner.y, inner.y),
                                Mathf.Clamp(p.z, -inner.z, inner.z));
                            Vector3 offset = p - clamped;
                            Vector3 normal = offset.sqrMagnitude < 1e-8f ? n : offset.normalized;
                            Vector3 position = clamped + normal * radius;
                            vertices.Add(position);
                            normals.Add(normal);
                            uvs.Add(FaceUv(position, axis));
                        }
                    }
                    for (int j = 0; j < grid - 1; j++)
                    {
                        for (int i = 0; i < grid - 1; i++)
                        {
                            int a0 = start + j * grid + i;
                            int a1 = a0 + 1;
                            int a2 = a0 + grid;
                            int a3 = a2 + 1;
                            // Unity front faces wind so that cross(b - a, c - a) points at the viewer.
                            // (u, v, n) is right-handed on + faces and left-handed on - faces.
                            bool flip = Vector3.Dot(Vector3.Cross(u, v), n) < 0f;
                            if (!flip)
                            {
                                triangles.Add(a0); triangles.Add(a1); triangles.Add(a2);
                                triangles.Add(a1); triangles.Add(a3); triangles.Add(a2);
                            }
                            else
                            {
                                triangles.Add(a0); triangles.Add(a2); triangles.Add(a1);
                                triangles.Add(a1); triangles.Add(a2); triangles.Add(a3);
                            }
                        }
                    }
                }
            }

            var mesh = new Mesh { name = "RoundedBox " + size.x.ToString("0.##") + "x" + size.y.ToString("0.##") + "x" + size.z.ToString("0.##") };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            Cache[key] = mesh;
            return mesh;
        }

        // UVs follow the face a vertex was generated on, so bevels never switch projection.
        private static Vector2 FaceUv(Vector3 p, int axis)
        {
            if (axis == 1) return new Vector2(p.x, p.z);
            if (axis == 0) return new Vector2(p.z, p.y);
            return new Vector2(p.x, p.y);
        }
    }
}

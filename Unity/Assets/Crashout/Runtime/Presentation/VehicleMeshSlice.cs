using System.Collections.Generic;
using UnityEngine;

namespace Crashout.Presentation
{
    /// <summary>Clips actual triangles at a chassis tear, retaining interpolated normals and UVs.</summary>
    public sealed class VehicleMeshSlice
    {
        public readonly List<Vector3> Vertices = new List<Vector3>();
        public readonly List<Vector3> Normals = new List<Vector3>();
        public readonly List<Vector2> Uvs = new List<Vector2>();
        public readonly List<int> Triangles = new List<int>();
        public readonly List<Color> Colours = new List<Color>();
        private readonly List<Vertex> tearEdges = new List<Vertex>();
        private struct Vertex { public Vector3 P, N; public Vector2 U; public Color C; }

        public static VehicleMeshSlice Cut(Vector3[] vertices, Vector3[] normals, Vector2[] uvs, int[] triangles, float cutZ, bool front, Color[] colours = null)
        {
            var result = new VehicleMeshSlice();
            var input = new Vertex[3];
            var output = new List<Vertex>(4);
            float Distance(Vertex v) => (v.P.z - cutZ - 0.045f * Mathf.Sin(v.P.x * 19f + v.P.y * 11f)) * (front ? 1f : -1f);
            for (int i = 0; i < triangles.Length; i += 3)
            {
                output.Clear();
                for (int k = 0; k < 3; k++)
                {
                    int index = triangles[i + k];
                    input[k] = new Vertex { P = vertices[index], N = normals.Length > index ? normals[index] : Vector3.up, U = uvs.Length > index ? uvs[index] : Vector2.zero,
                        C = colours != null && colours.Length > index ? colours[index] : Color.clear };
                }
                for (int k = 0; k < 3; k++)
                {
                    Vertex a = input[k], b = input[(k + 1) % 3];
                    float da = Distance(a), db = Distance(b);
                    if (da >= 0f) output.Add(a);
                    if ((da >= 0f) == (db >= 0f)) continue;
                    float t = da / (da - db);
                    var edge = new Vertex { P = Vector3.LerpUnclamped(a.P, b.P, t), N = Vector3.LerpUnclamped(a.N, b.N, t).normalized, U = Vector2.LerpUnclamped(a.U, b.U, t), C = Color.LerpUnclamped(a.C, b.C, t) };
                    output.Add(edge); result.tearEdges.Add(edge);
                }
                int start = result.Vertices.Count;
                foreach (Vertex v in output) { result.Vertices.Add(v.P); result.Normals.Add(v.N); result.Uvs.Add(v.U); result.Colours.Add(v.C); }
                for (int k = 1; k + 1 < output.Count; k++)
                { result.Triangles.Add(start); result.Triangles.Add(start + k); result.Triangles.Add(start + k + 1); }
            }
            return result;
        }

        public void CurlTornEdges(bool front)
        {
            for (int i = 0; i + 1 < tearEdges.Count; i += 2)
            {
                Vertex a = tearEdges[i], b = tearEdges[i + 1];
                int start = Vertices.Count;
                Vector3 Curl(Vertex v)
                {
                    float jag = 0.025f + 0.018f * Mathf.Sin(v.P.x * 31f + v.P.y * 23f);
                    return v.P + v.N * 0.013f + Vector3.forward * ((front ? -1f : 1f) * jag);
                }
                Vertices.Add(a.P); Vertices.Add(b.P); Vertices.Add(Curl(b)); Vertices.Add(Curl(a));
                for (int k = 0; k < 4; k++)
                {
                    Normals.Add(k == 0 || k == 3 ? a.N : b.N);
                    Uvs.Add(k == 0 || k == 3 ? a.U : b.U); Colours.Add(new Color(1f, 1f, 0f, 0f));
                }
                Triangles.Add(start); Triangles.Add(start + 1); Triangles.Add(start + 2);
                Triangles.Add(start); Triangles.Add(start + 2); Triangles.Add(start + 3);
                // A ragged lip remains visible from the inside of the torn body too.
                Triangles.Add(start + 2); Triangles.Add(start + 1); Triangles.Add(start);
                Triangles.Add(start + 3); Triangles.Add(start + 2); Triangles.Add(start);
            }
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name, indexFormat = Vertices.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.SetVertices(Vertices); mesh.SetNormals(Normals); mesh.SetUVs(0, Uvs); mesh.SetTriangles(Triangles, 0);
            mesh.SetColors(Colours);
            if (Vertices.Count > 0 && Triangles.Count > 0) { mesh.RecalculateBounds(); mesh.RecalculateTangents(); }
            return mesh;
        }
    }
}

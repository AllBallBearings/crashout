using System.Collections.Generic;
using Crashout.Simulation;
using UnityEngine;

namespace Crashout.Presentation
{
    /// <summary>One open road surface from the launch road through the junction, with no slab end faces.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public sealed class RoadSurface : MonoBehaviour
    {
        private Mesh mesh;

        public void Configure(Material asphalt, RunwayProfile runway = null)
        {
            GetComponent<MeshRenderer>().sharedMaterial = asphalt;
            gameObject.isStatic = true;
            SetRunway(runway);
        }

        public void SetRunway(RunwayProfile runway)
        {
            RoadSurfaceGeometry geometry = RoadSurfaceGeometry.Build(runway);
            Mesh next = new Mesh { name = "Continuous runway and junction road" };
            next.vertices = geometry.Vertices;
            next.normals = geometry.Normals;
            next.uv = geometry.Uvs;
            next.triangles = geometry.Triangles;
            next.RecalculateBounds();
            next.RecalculateTangents();
            GetComponent<MeshFilter>().sharedMesh = next;
            GetComponent<MeshCollider>().sharedMesh = next;
            if (mesh != null) Destroy(mesh);
            mesh = next;
        }

        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }
    }

    /// <summary>Welded vertices at the board edge and intersection; UVs stay in world metres.</summary>
    public sealed class RoadSurfaceGeometry
    {
        public Vector3[] Vertices { get; private set; }
        public Vector3[] Normals { get; private set; }
        public Vector2[] Uvs { get; private set; }
        public int[] Triangles { get; private set; }

        public static RoadSurfaceGeometry Build(RunwayProfile runway = null)
        {
            var zs = new List<float>();
            if (runway != null)
                for (float z = runway.FarZ; z < RunwayProfile.BoardEdgeZ; z += 1f) zs.Add(z);
            zs.Add(RunwayProfile.BoardEdgeZ);
            zs.Add(-RunwayProfile.RoadHalfWidth);
            zs.Add(RunwayProfile.RoadHalfWidth);
            zs.Add(RunwayProfile.BoardHalfExtent);

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            int Add(float x, float y, float z, Vector3 normal)
            {
                vertices.Add(new Vector3(x, y, z));
                normals.Add(normal);
                uvs.Add(new Vector2(x, z));
                return vertices.Count - 1;
            }
            void Quad(int a, int b, int c, int d)
            {
                triangles.Add(a); triangles.Add(d); triangles.Add(b);
                triangles.Add(b); triangles.Add(d); triangles.Add(c);
            }
            foreach (float z in zs)
            {
                bool approach = runway != null && z < RunwayProfile.BoardEdgeZ;
                float y = RunwayProfile.RoadSurfaceY + (approach ? runway.HeightAt(z) : 0f);
                Vector3 normal = approach ? new Vector3(0f, 1f, -runway.GradeAt(z)).normalized : Vector3.up;
                Add(-RunwayProfile.RoadHalfWidth, y, z, normal);
                Add(RunwayProfile.RoadHalfWidth, y, z, normal);
            }
            for (int row = 0; row + 1 < zs.Count; row++)
            {
                int a = row * 2;
                Quad(a, a + 1, a + 3, a + 2);
            }

            // Share the junction's four corner vertices with the east/west arms. No overlapping slabs.
            int south = (zs.Count - 3) * 2, north = south + 2;
            int westSouth = Add(-RunwayProfile.BoardHalfExtent, RunwayProfile.RoadSurfaceY, -RunwayProfile.RoadHalfWidth, Vector3.up);
            int westNorth = Add(-RunwayProfile.BoardHalfExtent, RunwayProfile.RoadSurfaceY, RunwayProfile.RoadHalfWidth, Vector3.up);
            Quad(westSouth, south, north, westNorth);
            int eastSouth = Add(RunwayProfile.BoardHalfExtent, RunwayProfile.RoadSurfaceY, -RunwayProfile.RoadHalfWidth, Vector3.up);
            int eastNorth = Add(RunwayProfile.BoardHalfExtent, RunwayProfile.RoadSurfaceY, RunwayProfile.RoadHalfWidth, Vector3.up);
            Quad(south + 1, eastSouth, eastNorth, north + 1);

            return new RoadSurfaceGeometry
            {
                Vertices = vertices.ToArray(), Normals = normals.ToArray(), Uvs = uvs.ToArray(), Triangles = triangles.ToArray()
            };
        }
    }
}

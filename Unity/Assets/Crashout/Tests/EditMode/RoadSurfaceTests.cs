using System.Collections.Generic;
using Crashout.Presentation;
using Crashout.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace Crashout.Tests
{
    public sealed class RoadSurfaceTests
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void BoardSeamAndIntersectionUseSharedEdgesRatherThanCollidingSlabEnds(int index)
        {
            RoadSurfaceGeometry road = RoadSurfaceGeometry.Build(CrashScenarios.Create(index).Runway);
            var edges = new Dictionary<(int, int), int>();
            void Count(int a, int b)
            {
                var edge = a < b ? (a, b) : (b, a);
                edges.TryGetValue(edge, out int count);
                edges[edge] = count + 1;
            }
            for (int i = 0; i < road.Triangles.Length; i += 3)
            {
                int a = road.Triangles[i], b = road.Triangles[i + 1], c = road.Triangles[i + 2];
                Count(a, b); Count(b, c); Count(c, a);
                Vector3 face = Vector3.Cross(road.Vertices[b] - road.Vertices[a], road.Vertices[c] - road.Vertices[a]).normalized;
                Assert.That(face.y, Is.GreaterThan(0.9f), "Roads must not present vertical faces to the chassis.");
            }
            int Find(float x, float z)
            {
                int found = -1;
                for (int i = 0; i < road.Vertices.Length; i++)
                {
                    Vector3 v = road.Vertices[i];
                    if (Mathf.Abs(v.x - x) > 0.001f || Mathf.Abs(v.z - z) > 0.001f) continue;
                    Assert.That(found, Is.EqualTo(-1), "Duplicate vertices split the road at a join.");
                    found = i;
                }
                Assert.That(found, Is.GreaterThanOrEqualTo(0));
                return found;
            }
            void Joined(int a, int b)
            {
                var edge = a < b ? (a, b) : (b, a);
                Assert.That(edges[edge], Is.EqualTo(2), "The seam must have a road triangle on each side.");
            }
            Joined(Find(-5f, -26f), Find(5f, -26f));
            Joined(Find(-5f, -5f), Find(-5f, 5f));
            Joined(Find(5f, -5f), Find(5f, 5f));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void RoadHeightAndAsphaltCoordinatesContinueOntoTheBoard(int index)
        {
            RunwayProfile runway = CrashScenarios.Create(index).Runway;
            RoadSurfaceGeometry road = RoadSurfaceGeometry.Build(runway);
            for (int i = 0; i < road.Vertices.Length; i++)
            {
                Vector3 v = road.Vertices[i];
                float height = RunwayProfile.RoadSurfaceY + (v.z < RunwayProfile.BoardEdgeZ ? runway.HeightAt(v.z) : 0f);
                Assert.That(v.y, Is.EqualTo(height).Within(0.0001f));
                Assert.That(road.Uvs[i], Is.EqualTo(new Vector2(v.x, v.z)), "Asphalt scale and alignment must continue across the seam.");
            }
            Assert.That(road.Vertices[0].z, Is.EqualTo(runway.FarZ));
        }

        [Test]
        public void BoardWithoutARunwayStillHasACompleteFlatCrossRoad()
        {
            RoadSurfaceGeometry road = RoadSurfaceGeometry.Build();
            Assert.That(road.Vertices.Length, Is.EqualTo(12));
            Assert.That(road.Triangles.Length, Is.EqualTo(30));
            foreach (Vector3 v in road.Vertices) Assert.That(v.y, Is.EqualTo(RunwayProfile.RoadSurfaceY));
        }
    }
}

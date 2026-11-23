using System.IO;
using Crashout.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Crashout.Tests
{
    public sealed class VehicleBodyAssetTests
    {
        [TestCase("Player")]
        [TestCase("PlayerMuscle")]
        [TestCase("PlayerBmw")]
        [TestCase("PlayerSuv")]
        [TestCase("Sedan")]
        [TestCase("Hatch")]
        [TestCase("Van")]
        [TestCase("Pickup")]
        [TestCase("Bus")]
        [TestCase("Tanker")]
        public void FinishedAssetsHaveFiniteGeometryCorrectWindingAndSeparatePanels(string name)
        {
            var asset = VehicleBodyAsset.Read(File.ReadAllBytes(Path.Combine("Assets/Crashout/Resources/Vehicles", name + ".bytes")));
            Assert.That(asset.HalfLength, Is.InRange(1.8f, 4.1f));
            int vertices = 0, triangles = 0, backwards = 0, measured = 0;
            bool hoodOrRoof = false, body = false, glass = false;
            foreach (var section in asset.Sections)
            {
                vertices += section.Vertices.Length; triangles += section.Triangles.Length / 3;
                hoodOrRoof |= section.Part == "Hood" || section.Part == "Roof panel";
                body |= section.Part == "Body"; glass |= section.Material == "glass";
                Assert.That(section.Normals.Length, Is.EqualTo(section.Vertices.Length));
                Assert.That(section.Uvs.Length, Is.EqualTo(section.Vertices.Length));
                for (int v = 0; v < section.Vertices.Length; v++)
                {
                    Vector3 p = section.Vertices[v];
                    Assert.That(float.IsNaN(p.x) || float.IsInfinity(p.x) || float.IsNaN(p.y) || float.IsInfinity(p.y) || float.IsNaN(p.z) || float.IsInfinity(p.z), Is.False);
                    Assert.That(Mathf.Abs(p.x), Is.LessThan(1.6f));
                    Assert.That(Mathf.Abs(p.z), Is.LessThan(asset.HalfLength + 0.2f));
                }
                for (int t = 0; t < section.Triangles.Length; t += 3)
                {
                    int a = section.Triangles[t], b = section.Triangles[t + 1], c = section.Triangles[t + 2];
                    Vector3 geometric = Vector3.Cross(section.Vertices[b] - section.Vertices[a], section.Vertices[c] - section.Vertices[a]);
                    Vector3 shading = section.Normals[a] + section.Normals[b] + section.Normals[c];
                    // Coordinate conversion and winding reversal must preserve matching face normals.
                    float alignment = Vector3.Dot(geometric.normalized, shading.normalized);
                    if (geometric.sqrMagnitude < 1e-14f || Mathf.Abs(alignment) < 0.2f) continue;
                    measured++; if (alignment < 0f) backwards++;
                }
            }
            Assert.That(hoodOrRoof && body && glass, Is.True);
            Assert.That(vertices, Is.InRange(10000, 50000));
            Assert.That(triangles, Is.LessThan(80000));
            Assert.That(backwards / (float)measured, Is.LessThan(0.04f), "Export has inward-facing exterior panels.");
        }

        [Test]
        public void CorruptAssetHeadersAreRejectedBeforeAllocatingMeshes()
        {
            Assert.Throws<InvalidDataException>(() => VehicleBodyAsset.Read(new byte[] { 1, 2, 3, 4 }));
        }

        [Test]
        public void TornSectionsKeepDamageColoursAndGainVisibleRaggedEdges()
        {
            var vertices = new[] { new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(0, 0, 1) };
            var normals = new[] { Vector3.up, Vector3.up, Vector3.up };
            var colours = new[] { new Color(.8f, .2f, 0, 0), new Color(.8f, .2f, 0, 0), new Color(.8f, .2f, 0, 0) };
            var cut = VehicleMeshSlice.Cut(vertices, normals, new Vector2[3], new[] { 0, 2, 1 }, 0, true, colours);
            int before = cut.Vertices.Count;
            foreach (Color colour in cut.Colours) Assert.That(colour.r, Is.EqualTo(.8f).Within(.0001f));
            cut.CurlTornEdges(true);
            Assert.That(cut.Vertices.Count, Is.GreaterThan(before));
            Assert.That(cut.Colours.Count, Is.EqualTo(cut.Vertices.Count));
            Assert.That(cut.Colours[before].r, Is.EqualTo(1f));
        }
    }
}

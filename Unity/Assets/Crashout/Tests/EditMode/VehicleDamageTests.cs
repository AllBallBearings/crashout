using Crashout.Presentation;
using Crashout.Vehicle;
using NUnit.Framework;
using UnityEngine;

namespace Crashout.Tests
{
    public sealed class VehicleDamageTests
    {
        [Test]
        public void OpposingHitsCompressBothEndsWithoutInvertingTheChassis()
        {
            var shape = new VehicleDamageShape();
            Vector3 front = new Vector3(0f, 0f, 2f), rear = -front;
            shape.Contact(front, Vector3.back, 25f, 10f, 2f, 1f);
            shape.Contact(rear, Vector3.forward, 25f, 10f, 2f, 1f);
            Assert.That(shape.FrontCrush, Is.GreaterThan(0f)); Assert.That(shape.RearCrush, Is.GreaterThan(0f));
            float length = shape.Map(front, 2f, 0.9f).z - shape.Map(rear, 2f, 0.9f).z;
            Assert.That(length, Is.InRange(1.4f, 3.9f));
            for (int i = 0; i < 20; i++) shape.Contact(front, Vector3.back, 40f, 30f, 2f, 1f);
            Assert.That(shape.Map(front, 2f, 0.9f).z, Is.GreaterThan(shape.Map(rear, 2f, 0.9f).z));
        }

        [Test]
        public void HighSideContactBendsAndTwistsTheChassis()
        {
            var shape = new VehicleDamageShape();
            shape.Contact(new Vector3(0.9f, 0.8f, 1.4f), Vector3.left, 24f, 12f, 2f, 1f);
            Assert.That(Mathf.Abs(shape.Bend), Is.GreaterThan(0.05f));
            Assert.That(Mathf.Abs(shape.Twist), Is.GreaterThan(0.05f));
            Assert.That(shape.RightCrush, Is.GreaterThan(0f)); Assert.That(shape.LeftCrush, Is.Zero);
            Assert.That(shape.Map(new Vector3(0.9f, 0.4f, 1.5f), 2f, 0.9f), Is.Not.EqualTo(new Vector3(0.9f, 0.4f, 1.5f)));
        }

        [Test]
        public void LoadedOpposingContactsCanCrushAStationaryCar()
        {
            var shape = new VehicleDamageShape();
            for (int i = 0; i < 40; i++)
            {
                shape.Contact(Vector3.forward * 2f, Vector3.back, 0f, 0.1f, 2f, 1f, 0.025f);
                shape.Contact(Vector3.back * 2f, Vector3.forward, 0f, 0.1f, 2f, 1f, 0.025f);
            }
            Assert.That(shape.FrontCrush + shape.RearCrush, Is.GreaterThan(0.8f));
        }

        [Test]
        public void GentleContactsStayIntactAndSevereRepeatedLoadsCanTearTheStructure()
        {
            var shape = new VehicleDamageShape();
            for (int i = 0; i < 100; i++) shape.Contact(Vector3.forward * 2f, Vector3.back, 1f, 0.01f, 2f, 1f);
            Assert.That(shape.Strain, Is.Zero); Assert.That(shape.FrontCrush, Is.Zero); Assert.That(shape.ShouldFracture, Is.False);
            shape.Contact(Vector3.forward * 2f, Vector3.back, 35f, 30f, 2f, 1f);
            shape.Contact(Vector3.back * 2f, Vector3.forward, 35f, 30f, 2f, 1f);
            Assert.That(shape.ShouldFracture, Is.False, "Crushed ends alone must not instantly sever the passenger cell.");
            shape.Contact(Vector3.forward * 2f, Vector3.back, 40f, 35f, 2f, 1f);
            shape.Contact(Vector3.back * 2f, Vector3.forward, 40f, 35f, 2f, 1f);
            Assert.That(shape.ShouldFracture, Is.True);
        }

        [Test]
        public void CompressedSheetKeepsItsArcLengthByFoldingInsteadOfShrinking()
        {
            var shape = new VehicleDamageShape();
            shape.Contact(new Vector3(0, 0, 2f), Vector3.back, 35f, 30f, 2f, 1f);
            const float from = 0.9f, to = 1.86f;
            float length = 0f, minHeight = float.MaxValue, maxHeight = float.MinValue;
            Vector3 last = shape.MapSurface(new Vector3(0, 0.5f, from), Vector3.up, 2f, 0.9f);
            for (int i = 1; i <= 960; i++)
            {
                Vector3 p = shape.MapSurface(new Vector3(0, 0.5f, Mathf.Lerp(from, to, i / 960f)), Vector3.up, 2f, 0.9f);
                length += Vector3.Distance(p, last); last = p;
                minHeight = Mathf.Min(minHeight, p.y); maxHeight = Mathf.Max(maxHeight, p.y);
            }
            Assert.That(length, Is.EqualTo(to - from).Within(0.015f));
            Assert.That(maxHeight - minHeight, Is.GreaterThan(0.15f), "Excess sheet metal must visibly buckle.");
        }

        [Test]
        public void ThinPanelsKeepTheirGaugeAndTheCrushCageNeverInverts()
        {
            var shape = new VehicleDamageShape();
            for (int i = 0; i < 8; i++) shape.Contact(Vector3.forward * 2f, Vector3.back, 35f, 30f, 2f, 1f);
            Vector3 a = shape.MapSurface(new Vector3(0, 0.5f, 1.5f), Vector3.up, 2f, 0.9f);
            Vector3 b = shape.MapSurface(new Vector3(0, 0.4975f, 1.5f), Vector3.down, 2f, 0.9f);
            Assert.That(Vector3.Distance(a, b), Is.EqualTo(0.0025f).Within(0.00001f));
            float last = -10f;
            for (int i = 0; i <= 100; i++)
            {
                float z = shape.Map(new Vector3(0, 0, i * 0.02f), 2f, 0.9f).z;
                Assert.That(z, Is.GreaterThan(last)); last = z;
            }
        }

        [Test]
        public void UndamagedPanelsKeepTheirSurfaceAndOppositeNormalsDoNotSeparateTheSkin()
        {
            var shape = new VehicleDamageShape(); Vector3 p = new Vector3(0.5f, 0.7f, 1.4f);
            Assert.That(shape.MapSurface(p, Vector3.up, 2f, 0.9f), Is.EqualTo(p));
            shape.Contact(Vector3.forward * 2, Vector3.back, 25f, 12f, 2f, 1f);
            Assert.That(shape.MapSurface(p, Vector3.up, 2f, 0.9f), Is.EqualTo(shape.MapSurface(p, Vector3.down, 2f, 0.9f)));
        }

        [Test]
        public void AHighSpeedLightSplinterCannotCrushTheCarLikeAConcreteWall()
        {
            var splinter = new VehicleDamageShape(); var wall = new VehicleDamageShape();
            splinter.Contact(Vector3.forward * 2f, Vector3.back, 40f, 0.01f, 2f, 1f);
            wall.Contact(Vector3.forward * 2f, Vector3.back, 40f, 30f, 2f, 1f);
            Assert.That(splinter.FrontCrush, Is.LessThan(0.002f));
            Assert.That(wall.FrontCrush, Is.GreaterThan(0.5f));
        }

        [Test]
        public void HeavyStructureResistsMoreCrushFromTheSameImpact()
        {
            var car = new VehicleDamageShape(); var truck = new VehicleDamageShape();
            car.Contact(Vector3.forward * 2f, Vector3.back, 25f, 8f, 2f, 1f);
            truck.Contact(Vector3.forward * 2f, Vector3.back, 25f, 8f, 2f, 0.45f);
            Assert.That(truck.FrontCrush, Is.LessThan(car.FrontCrush));
        }

        [Test]
        public void ANewDamageStateRestoresEveryPointExactly()
        {
            var shape = new VehicleDamageShape();
            foreach (var point in new[] { new Vector3(1f, 0.5f, 2f), new Vector3(-1f, 0f, -2f), Vector3.zero })
                Assert.That(shape.Map(point, 2f, 0.9f), Is.EqualTo(point));
        }

        [Test]
        public void TearingTrianglesPreservesBothHalvesAndTheirTotalSurfaceArea()
        {
            var v = new[] { new Vector3(-1f, 0f, -1f), new Vector3(1f, 0f, -1f), new Vector3(0f, 0f, 1f) };
            var n = new[] { Vector3.up, Vector3.up, Vector3.up };
            var uv = new[] { Vector2.zero, Vector2.right, Vector2.up };
            var tri = new[] { 0, 2, 1 };
            VehicleMeshSlice front = VehicleMeshSlice.Cut(v, n, uv, tri, 0f, true), rear = VehicleMeshSlice.Cut(v, n, uv, tri, 0f, false);
            float Area(VehicleMeshSlice mesh)
            {
                float area = 0f;
                for (int i = 0; i < mesh.Triangles.Count; i += 3)
                {
                    Vector3 a = mesh.Vertices[mesh.Triangles[i]], b = mesh.Vertices[mesh.Triangles[i + 1]], c = mesh.Vertices[mesh.Triangles[i + 2]];
                    area += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                }
                return area;
            }
            Assert.That(Area(front), Is.GreaterThan(0f)); Assert.That(Area(rear), Is.GreaterThan(0f));
            Assert.That(Area(front) + Area(rear), Is.EqualTo(2f).Within(0.0001f));
            foreach (Vector3 p in front.Vertices) Assert.That(p.z, Is.GreaterThanOrEqualTo(-0.046f));
            foreach (Vector3 p in rear.Vertices) Assert.That(p.z, Is.LessThanOrEqualTo(0.046f));
            Assert.That(front.Uvs.Count, Is.EqualTo(front.Vertices.Count));
            Assert.That(rear.Normals.Count, Is.EqualTo(rear.Vertices.Count));
        }

        [TestCase(VehicleClass.Sedan)]
        [TestCase(VehicleClass.Hatch)]
        [TestCase(VehicleClass.Pickup)]
        [TestCase(VehicleClass.Van)]
        [TestCase(VehicleClass.Bus)]
        [TestCase(VehicleClass.Tanker)]
        public void EveryVehicleHasRoadVehicleProportionsAndCollidersThatFitItsBody(VehicleClass kind)
        {
            VehicleSpec spec = VehicleSpec.For(kind);
            Assert.That(spec.BodySize.z / spec.BodySize.x, Is.GreaterThan(2f));
            Assert.That(spec.ColliderSize.x, Is.LessThanOrEqualTo(spec.BodySize.x));
            Assert.That(spec.ColliderSize.z, Is.LessThanOrEqualTo(spec.BodySize.z));
            foreach (Vector3 wheel in spec.WheelPositions)
                Assert.That(Mathf.Abs(wheel.z) + spec.WheelRadius, Is.LessThan(spec.BodySize.z * 0.5f));
        }
    }
}

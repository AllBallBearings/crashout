using System.Collections;
using Crashout.Presentation;
using Crashout.Simulation;
using Crashout.Vehicle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Crashout.Tests
{
    public sealed class VehicleDestructionTests
    {
        [UnityTest, Timeout(60000)]
        public IEnumerator OpposingSevereImpactsTearTheCarAndRetryRestoresItsMeshesPartsAndPhysics()
        {
            yield return null;
            var session = Object.FindFirstObjectByType<CrashoutGameSession>();
            Assert.That(session, Is.Not.Null);
            session.ResetTurn(); yield return new WaitForFixedUpdate();
            var car = session.Player; var deform = car.GetComponent<VehicleDeformer>();
            float mass = car.Body.mass; Vector3 size = car.GetComponent<BoxCollider>().size;
            var meshes = car.GetComponentsInChildren<MeshFilter>();
            var vertices = new Vector3[meshes.Length][];
            for (int i = 0; i < meshes.Length; i++) vertices[i] = meshes[i].sharedMesh.vertices;
            GameObject front = GameObject.CreatePrimitive(PrimitiveType.Cube), rear = GameObject.CreatePrimitive(PrimitiveType.Cube);
            front.transform.position = new Vector3(300f, 20f, 3f); rear.transform.position = new Vector3(300f, 20f, -3f);
            front.transform.localScale = rear.transform.localScale = new Vector3(8f, 8f, 0.3f);
            bool gravity = car.Body.useGravity;
            try
            {
                session.Launch(0.2f); car.Body.useGravity = false;
                car.Body.position = new Vector3(300f, 20f, 0f); car.Body.rotation = Quaternion.identity; car.Body.linearVelocity = Vector3.forward * 35f;
                for (int i = 0; i < 60 && deform.DentCount == 0; i++) yield return new WaitForFixedUpdate();
                Assert.That(deform.DentCount, Is.GreaterThan(0));
                for (int i = 0; i < 4; i++) yield return new WaitForFixedUpdate();
                for (int hit = 0; hit < 6 && !deform.IsFractured; hit++)
                {
                    int previousDents = deform.DentCount;
                    car.Body.position = new Vector3(300f, 20f, 0f); car.Body.rotation = Quaternion.identity; car.Body.angularVelocity = Vector3.zero;
                    car.Body.linearVelocity = (hit % 2 == 0 ? Vector3.back : Vector3.forward) * 40f;
                    for (int i = 0; i < 90 && !deform.IsFractured && deform.DentCount == previousDents; i++) yield return new WaitForFixedUpdate();
                    for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
                }
                Assert.That(deform.IsFractured, Is.True, "The opposing crush zones did not separate under extreme load.");
                var wreck = Object.FindFirstObjectByType<VehicleWreckPiece>();
                Assert.That(wreck, Is.Not.Null); Assert.That(wreck.GetComponent<Rigidbody>().mass, Is.GreaterThan(0f));
                Assert.That(car.Body.mass, Is.LessThan(mass));
                Assert.That(session.Phase, Is.Not.EqualTo(GamePhase.Driving), "A structurally failed player must enter the crash phase.");
                session.ResetTurn(); yield return null;
                Assert.That(deform.IsFractured, Is.False); Assert.That(deform.DetachedPartCount, Is.Zero);
                Assert.That(car.Body.mass, Is.EqualTo(mass).Within(0.01f)); Assert.That(car.GetComponent<BoxCollider>().size, Is.EqualTo(size));
                Assert.That(Object.FindFirstObjectByType<VehicleWreckPiece>(), Is.Null);
                for (int i = 0; i < meshes.Length; i++) Assert.That(meshes[i].sharedMesh.vertices, Is.EqualTo(vertices[i]), "Retry left damaged geometry behind.");
            }
            finally
            {
                car.Body.useGravity = gravity; Object.Destroy(front); Object.Destroy(rear); session.ResetTurn();
            }
        }
    }
}

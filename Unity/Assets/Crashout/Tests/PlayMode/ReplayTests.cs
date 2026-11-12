using System.Collections;
using Crashout.Presentation;
using Crashout.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Crashout.Tests
{
    public class ReplayTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator AWholeTurnCanBeRewoundWithoutChangingTheLiveWorld()
        {
            yield return null;
            var session = Object.FindFirstObjectByType<CrashoutGameSession>();
            Assert.That(session, Is.Not.Null);
            session.RequestScenarioChange((CrashScenarios.Count - session.Scenario.Index) % CrashScenarios.Count);
            yield return null;
            session.ResetTurn();
            yield return new WaitForFixedUpdate();
            TurnReplay replay = session.Replay;
            Vector3 start = session.Player.transform.position;
            Assert.That(replay.Open(), Is.False, "No replay before a completed turn.");
            // A prop that changes appearance and is destroyed before Results must still rewind.
            var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            probe.name = "Replay test prop";
            probe.transform.position = new Vector3(200f, 0f, 0f);
            probe.AddComponent<Rigidbody>().isKinematic = true;
            Mesh probeMesh = probe.GetComponent<MeshFilter>().mesh;
            Vector3 originalVertex = probeMesh.vertices[0];
            session.Launch(1f);
            yield return new WaitForSecondsRealtime(0.15f);
            probe.transform.position += Vector3.right * 2f;
            Vector3[] vertices = probeMesh.vertices;
            vertices[0] += Vector3.up;
            probeMesh.vertices = vertices;
            ReplayMeshRevision.Changed(probe.GetComponent<MeshFilter>());
            yield return new WaitForSecondsRealtime(0.15f);
            float changedAt = replay.Duration;
            Object.Destroy(probe);
            yield return new WaitForSecondsRealtime(0.15f);
            float goneAt = replay.Duration;
            float deadline = Time.realtimeSinceStartup + 55f;
            while (session.Phase != GamePhase.Results && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null; // The final render sample follows the Results event.
            Assert.That(session.Phase, Is.EqualTo(GamePhase.Results));
            Assert.That(replay.IsAvailable, Is.True);
            Assert.That(replay.Duration, Is.GreaterThan(3f));
            int score = session.Score;
            uint tick = session.HostTick;
            Vector3 end = session.Player.transform.position;
            bool previousAudioPause = AudioListener.pause;
            float previousScale = Time.timeScale;
            int scoreEvents = 0;
            System.Action<ScoreEvent> countScore = _ => scoreEvents++;
            session.ScoreChanged += countScore;
            try
            {
                Assert.That(replay.Open(), Is.True);
                replay.Pause();
                var propProxy = GameObject.Find("Replay: Replay test prop");
                Assert.That(propProxy, Is.Not.Null, "Destroyed geometry was not retained.");
                replay.Seek(0f);
                Assert.That(propProxy.GetComponent<Renderer>().enabled, Is.True);
                Assert.That(propProxy.GetComponent<MeshFilter>().sharedMesh.vertices[0], Is.EqualTo(originalVertex));
                replay.Seek(changedAt);
                Assert.That(propProxy.GetComponent<MeshFilter>().sharedMesh.vertices[0], Is.EqualTo(originalVertex + Vector3.up));
                replay.Seek(goneAt);
                Assert.That(propProxy.GetComponent<Renderer>().enabled, Is.False);
                replay.Seek(0f);
                Assert.That(propProxy.GetComponent<Renderer>().enabled, Is.True);
                Assert.That(Vector3.Distance(replay.Focus.position, start), Is.LessThan(0.2f));
                replay.Seek(replay.Duration);
                Assert.That(Vector3.Distance(replay.Focus.position, end), Is.LessThan(0.2f));
                Assert.That(replay.Score, Is.EqualTo(score));
                replay.Seek(0f);
                Assert.That(Vector3.Distance(replay.Focus.position, start), Is.LessThan(0.2f));
                Assert.That(Vector3.Distance(session.Player.transform.position, end), Is.LessThan(0.01f));
                Assert.That(session.Player.GetComponentInChildren<Renderer>().forceRenderingOff, Is.True);

                foreach (float speed in new[] { 0.25f, 0.5f, 1f, 2f })
                {
                    replay.Pause();
                    replay.Seek(0f);
                    replay.SetSpeed(speed);
                    replay.TogglePlayback();
                    replay.Advance(0.5f);
                    Assert.That(replay.Position, Is.EqualTo(0.5f * speed).Within(0.0001f));
                }
                replay.Pause();
                float pausedAt = replay.Position;
                yield return null;
                yield return null;
                Assert.That(replay.Position, Is.EqualTo(pausedAt));
                Assert.That(session.HostTick, Is.EqualTo(tick));
                Assert.That(session.Score, Is.EqualTo(score));
                Assert.That(scoreEvents, Is.Zero);

                // Moving the camera while paused must not change the recording or the simulation.
                Camera.main.transform.position += Vector3.right;
                yield return null;
                Assert.That(replay.Position, Is.EqualTo(pausedAt));
                replay.Seek(100000f);
                Assert.That(replay.Position, Is.EqualTo(replay.Duration));
                replay.TogglePlayback();
                Assert.That(replay.Position, Is.Zero, "Play at the end restarts the recording.");
                replay.Advance(100000f);
                Assert.That(replay.IsPlaying, Is.False);

                replay.Close();
                Assert.That(Time.timeScale, Is.EqualTo(previousScale));
                Assert.That(AudioListener.pause, Is.EqualTo(previousAudioPause));
                Assert.That(session.Player.GetComponentInChildren<Renderer>().forceRenderingOff, Is.False);
                Assert.That(replay.IsAvailable, Is.True, "Results may reopen the same replay.");
                Assert.That(replay.Open(), Is.True);
                session.ResetTurn();
                Assert.That(replay.IsActive || replay.IsAvailable, Is.False);
                Assert.That(Time.timeScale, Is.EqualTo(previousScale));
                Assert.That(session.Phase, Is.EqualTo(GamePhase.ReadyCheck));
            }
            finally
            {
                session.ScoreChanged -= countScore;
                replay.Close();
                session.ResetTurn();
                if (probe != null) Object.Destroy(probe);
                Object.Destroy(probeMesh);
            }
        }
    }
}

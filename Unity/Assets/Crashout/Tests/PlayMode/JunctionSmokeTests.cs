using System.Collections;
using System.IO;
using Crashout.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace Crashout.Tests
{
    public class JunctionSmokeTests
    {
        [UnityTest]
        public IEnumerator JunctionRendersAndKeyboardDriveScoresATrafficHit()
        {
            yield return null;
            var session = Object.FindFirstObjectByType<CrashoutGameSession>();
            Assert.That(session, Is.Not.Null, "Prototype did not bootstrap.");
            // Other tests may have left a different junction loaded; start from the first.
            if (session.Scenario != null && session.Scenario.Index != 0)
            {
                session.RequestScenarioChange((CrashScenarios.Count - session.Scenario.Index) % CrashScenarios.Count);
                yield return new WaitForFixedUpdate();
            }
            Assert.That(GraphicsSettings.currentRenderPipeline?.name, Is.EqualTo("CrashoutPipeline"));
            var camera = Camera.main;
            Assert.That(camera, Is.Not.Null);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var target = new RenderTexture(640, 360, 24);
            var frame = new Texture2D(640, 360, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                yield return new WaitForSeconds(0.5f);
                RenderTexture.active = target;
                frame.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                frame.Apply();
                int pink = 0, orange = 0;
                foreach (var c in frame.GetPixels32())
                {
                    if (c.r > 180 && c.b > 180 && c.g < 50) pink++;
                    if (c.r > 80 && c.r > 1.5f * c.g && c.g > 1.5f * c.b) orange++;
                }
                string capture = System.Environment.GetEnvironmentVariable("CRASHOUT_TEST_FRAME");
                if (!string.IsNullOrEmpty(capture)) File.WriteAllBytes(capture, frame.EncodeToPNG());
                Assert.That(pink, Is.LessThan(100), "Error shader visible in camera frame.");
                Assert.That(orange, Is.GreaterThan(150), "No colored vehicle visible in the junction.");
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                Object.Destroy(frame);
                Object.Destroy(target);
            }

            var keyboard = InputSystem.AddDevice<Keyboard>();
            uint trafficHit = 0;
            void OnScore(ScoreEvent e) { if (e.EntityId >= 10 && e.Amount > 0) trafficHit = e.EntityId; }
            var previousFocus = InputSystem.settings.backgroundBehavior;
#if UNITY_EDITOR
            var previousRouting = InputSystem.settings.editorInputBehaviorInPlayMode;
#endif
            try
            {
                // A batch-mode editor has no focused Game window.
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
                session.ResetTurn();
                session.Launch(1f);
                Vector3 start = session.Player.Body.position;
                float maxSpeed = 0f, maxYaw = 0f;
                session.ScoreChanged += OnScore;
                // The approach now starts on a runway one to two board-lengths out, so allow time to get there.
                for (int tick = 0; tick < 900 && trafficHit == 0; tick++)
                {
                    // Inject key state on the test device, not driver commands or forces.
                    // A lane change: steer right, then straighten up so the car stays on the runway.
                    InputState.Change(keyboard, tick >= 35 && tick < 50 ? new KeyboardState(Key.W, Key.D)
                        : tick >= 50 && tick < 65 ? new KeyboardState(Key.W, Key.A)
                        : new KeyboardState(Key.W));
                    keyboard.MakeCurrent();
                    yield return new WaitForFixedUpdate();
                    maxSpeed = Mathf.Max(maxSpeed, session.Player.SpeedMetresPerSecond);
                    if (session.Phase == GamePhase.Driving)
                        maxYaw = Mathf.Max(maxYaw, Mathf.Abs(Mathf.DeltaAngle(0f, session.Player.Body.rotation.eulerAngles.y)));
                }
                var end = session.Player.Body.position;
                Debug.Log($"Crashout smoke: phase {session.Phase} timeScale {Time.timeScale} scenario {session.Scenario?.Name} {start} -> {end}; peak {maxSpeed:F1} m/s; yaw {maxYaw:F1} degrees; traffic {trafficHit}; damage score {session.Score}.");
                Assert.That(end.z - start.z, Is.GreaterThan(3f), "W did not move the car forward.");
                Assert.That(maxSpeed, Is.GreaterThan(3f));
                Assert.That(maxYaw, Is.GreaterThan(1f), "D did not steer the car.");
                Assert.That(trafficHit, Is.Not.Zero, "No scored contact with traffic.");
                Assert.That(session.Score, Is.Positive);
            }
            finally
            {
                session.ScoreChanged -= OnScore;
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior = previousFocus;
#if UNITY_EDITOR
                InputSystem.settings.editorInputBehaviorInPlayMode = previousRouting;
#endif
            }
        }
    }
}

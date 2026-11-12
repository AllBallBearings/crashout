using System.Collections;
using System.IO;
using Crashout.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Crashout.Tests
{
    /// <summary>
    /// Art review helper, not a regression test: renders key moments of every junction to PNG.
    /// Runs only when CRASHOUT_CAPTURE_DIR is set, e.g.
    /// CRASHOUT_CAPTURE_DIR=/tmp/frames Unity -batchmode -runTests -testPlatform PlayMode -testFilter CaptureFrames
    /// </summary>
    public class CaptureFrames
    {
        private const int Width = 1280;
        private const int Height = 720;

        [UnityTest, Timeout(180000)]
        public IEnumerator CaptureEveryJunction([Values(0, 1, 2)] int scenarioIndex)
        {
            string directory = System.Environment.GetEnvironmentVariable("CRASHOUT_CAPTURE_DIR");
            if (string.IsNullOrEmpty(directory))
            {
                Assert.Ignore("Set CRASHOUT_CAPTURE_DIR to capture frames.");
                yield break;
            }
            Directory.CreateDirectory(directory);
            yield return null;
            var session = Object.FindFirstObjectByType<CrashoutGameSession>();
            Assert.That(session, Is.Not.Null);
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var previousFocus = InputSystem.settings.backgroundBehavior;
#if UNITY_EDITOR
            var previousRouting = InputSystem.settings.editorInputBehaviorInPlayMode;
#endif
            try
            {
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
                session.RequestScenarioChange((scenarioIndex - session.Scenario.Index + CrashScenarios.Count) % CrashScenarios.Count);
                yield return new WaitForFixedUpdate();
                string prefix = Path.Combine(directory, scenarioIndex + "-" + session.Scenario.Name.Replace(' ', '-').ToLowerInvariant());

                yield return new WaitForSecondsRealtime(0.4f);
                yield return Capture(prefix + "-a-reveal.png");
                yield return new WaitForSecondsRealtime(2.6f);
                yield return Capture(prefix + "-b-ready.png");

                session.Launch(1f);
                float launchedAt = Time.time;
                bool midShot = false, nearShot = false;
                for (int tick = 0; tick < 900 && session.Phase == GamePhase.Driving; tick++)
                {
                    InputState.Change(keyboard, new KeyboardState(Key.W, Key.LeftShift));
                    keyboard.MakeCurrent();
                    yield return new WaitForFixedUpdate();
                    float z = session.Player.Body.position.z;
                    if (!midShot && z > session.Runway.StartZ * 0.55f) { midShot = true; yield return Capture(prefix + "-c-approach.png"); }
                    if (!nearShot && z > -24f) { nearShot = true; yield return Capture(prefix + "-d-arrival.png"); }
                }
                InputState.Change(keyboard, new KeyboardState());
                Debug.Log($"[Capture] {session.Scenario.Name}: reached junction in {Time.time - launchedAt:F2}s; phase {session.Phase}");

                yield return new WaitForSecondsRealtime(0.5f);
                yield return Capture(prefix + "-e-impact.png");
                yield return new WaitForSecondsRealtime(1.5f);
                session.TryCrashbreaker();
                yield return new WaitForSecondsRealtime(0.35f);
                yield return Capture(prefix + "-f-crashbreaker.png");
                float deadline = Time.realtimeSinceStartup + 30f;
                while (session.Phase != GamePhase.Results && Time.realtimeSinceStartup < deadline) yield return null;
                yield return new WaitForSecondsRealtime(1f);
                Debug.Log($"[Capture] {session.Scenario.Name}: score {session.Score}, wrecked {session.WreckedCount}, best chain {session.BestChain}, medal {session.Medal}");
                yield return Capture(prefix + "-g-results.png");
                yield return CaptureOverview(prefix + "-h-overview.png", session.Runway.FarZ);
            }
            finally
            {
                InputSystem.settings.backgroundBehavior = previousFocus;
#if UNITY_EDITOR
                InputSystem.settings.editorInputBehaviorInPlayMode = previousRouting;
#endif
                InputSystem.RemoveDevice(keyboard);
            }
        }

        /// <summary>
        /// HUD review helper: screenshots of the real game view (IMGUI included) at each phase. Needs a
        /// visible editor, so it is skipped in batch mode. Runs only when CRASHOUT_HUD_DIR is set, e.g.
        /// CRASHOUT_HUD_DIR=/tmp/hud Unity -runTests -testPlatform PlayMode -testFilter CaptureHud
        /// </summary>
        [UnityTest, Timeout(180000)]
        public IEnumerator CaptureHud()
        {
            string directory = System.Environment.GetEnvironmentVariable("CRASHOUT_HUD_DIR");
            if (string.IsNullOrEmpty(directory) || Application.isBatchMode)
            {
                Assert.Ignore("Set CRASHOUT_HUD_DIR and run with a visible editor to capture the HUD.");
                yield break;
            }
            Directory.CreateDirectory(directory);
            yield return null;
            var session = Object.FindFirstObjectByType<CrashoutGameSession>();
            Assert.That(session, Is.Not.Null);
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var previousFocus = InputSystem.settings.backgroundBehavior;
#if UNITY_EDITOR
            var previousRouting = InputSystem.settings.editorInputBehaviorInPlayMode;
#endif
            try
            {
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
                session.RequestScenarioChange((0 - session.Scenario.Index + CrashScenarios.Count) % CrashScenarios.Count);
                yield return new WaitForSecondsRealtime(3.2f);
                yield return Screenshot(Path.Combine(directory, "hud-a-ready.png"));

                session.Launch(1f);
                bool midShot = false;
                for (int tick = 0; tick < 900 && session.Phase == GamePhase.Driving; tick++)
                {
                    InputState.Change(keyboard, new KeyboardState(Key.W, Key.LeftShift));
                    keyboard.MakeCurrent();
                    yield return new WaitForFixedUpdate();
                    if (!midShot && session.Player.Body.position.z > session.Runway.StartZ * 0.45f)
                    {
                        midShot = true;
                        yield return Screenshot(Path.Combine(directory, "hud-b-driving.png"));
                    }
                }
                yield return new WaitForSecondsRealtime(0.7f);
                yield return Screenshot(Path.Combine(directory, "hud-c-impact.png"));
                // Steer the wreck so the aftertouch read-out shows.
                for (int tick = 0; tick < 50; tick++)
                {
                    InputState.Change(keyboard, new KeyboardState(Key.A, Key.W));
                    keyboard.MakeCurrent();
                    yield return new WaitForFixedUpdate();
                }
                yield return Screenshot(Path.Combine(directory, "hud-d-aftertouch.png"));
                InputState.Change(keyboard, new KeyboardState());
                yield return new WaitForSecondsRealtime(1.2f);
                yield return Screenshot(Path.Combine(directory, "hud-e-crashbreaker.png"));
                session.TryCrashbreaker();
                float deadline = Time.realtimeSinceStartup + 30f;
                while (session.Phase != GamePhase.Results && Time.realtimeSinceStartup < deadline) yield return null;
                yield return new WaitForSecondsRealtime(2.6f);
                yield return Screenshot(Path.Combine(directory, "hud-f-results.png"));
            }
            finally
            {
                InputSystem.settings.backgroundBehavior = previousFocus;
#if UNITY_EDITOR
                InputSystem.settings.editorInputBehaviorInPlayMode = previousRouting;
#endif
                InputSystem.RemoveDevice(keyboard);
            }
        }

        private static IEnumerator Screenshot(string path)
        {
            yield return new WaitForEndOfFrame();
            Texture2D frame = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(path, frame.EncodeToPNG());
            Object.Destroy(frame);
        }

        /// <summary>The whole diorama, board and runway arm, from high off its south-east corner.</summary>
        private static IEnumerator CaptureOverview(string path, float farZ)
        {
            Camera camera = Camera.main;
            var rig = camera.GetComponent<Crashout.Presentation.CrashoutCameraRig>();
            if (rig != null) rig.enabled = false;
            Vector3 focus = new Vector3(0f, 0f, (farZ + RunwayProfile.BoardHalfExtent) * 0.5f);
            camera.transform.SetPositionAndRotation(focus + new Vector3(95f, 85f, -40f), Quaternion.LookRotation(focus - (focus + new Vector3(95f, 85f, -40f)), Vector3.up));
            try
            {
                yield return Capture(path);
            }
            finally
            {
                if (rig != null) rig.enabled = true;
            }
        }

        private static IEnumerator Capture(string path)
        {
            Camera camera = Camera.main;
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var frame = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            try
            {
                // WaitForEndOfFrame never resumes in a batch-mode editor; let the player loop render one frame instead.
                camera.targetTexture = target;
                yield return null;
                yield return null;
                RenderTexture.active = target;
                frame.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                frame.Apply();
                File.WriteAllBytes(path, frame.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                Object.Destroy(frame);
                Object.Destroy(target);
            }
        }
    }
}

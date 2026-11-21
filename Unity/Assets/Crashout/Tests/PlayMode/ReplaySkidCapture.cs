using System.Collections;
using System.IO;
using Crashout.Presentation;
using Crashout.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Crashout.Tests
{
    /// <summary>
    /// Art review helper, not a regression test: brakes hard into the junction to lay skid marks, then
    /// renders the same views live and in the replay so the replay's skid marks can be compared with
    /// the live ones. Runs only when CRASHOUT_SKIDS_DIR is set.
    /// </summary>
    public class ReplaySkidCapture
    {
        private const int Width = 1280;
        private const int Height = 720;

        [UnityTest, Timeout(180000)]
        public IEnumerator CaptureSkidsLiveAndInReplay()
        {
            string directory = System.Environment.GetEnvironmentVariable("CRASHOUT_SKIDS_DIR");
            if (string.IsNullOrEmpty(directory))
            {
                Assert.Ignore("Set CRASHOUT_SKIDS_DIR to capture the replay skid comparison.");
                yield break;
            }
            Directory.CreateDirectory(directory);
            yield return null;
            var session = Object.FindFirstObjectByType<CrashoutGameSession>();
            Assert.That(session, Is.Not.Null);
            session.RequestScenarioChange((0 - session.Scenario.Index + CrashScenarios.Count) % CrashScenarios.Count);
            session.ResetTurn();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            var keyboard = InputSystem.AddDevice<Keyboard>();
            var previousFocus = InputSystem.settings.backgroundBehavior;
#if UNITY_EDITOR
            var previousRouting = InputSystem.settings.editorInputBehaviorInPlayMode;
#endif
            Camera camera = Camera.main;
            var rig = camera.GetComponent<CrashoutCameraRig>();
            TurnReplay replay = session.Replay;
            try
            {
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
                session.Launch(1f);
                // Full throttle down the hill, then stamp on the brake and swerve onto the board.
                bool shotWhileDriving = false;
                for (int tick = 0; tick < 900 && session.Phase == GamePhase.Driving; tick++)
                {
                    if (!shotWhileDriving && session.Player.Body.position.z > -30f)
                    {
                        shotWhileDriving = true;
                        Debug.Log("[Skids] while driving: " + DescribeTrails(session));
                        if (rig != null) rig.enabled = false;
                        float fov = camera.fieldOfView;
                        camera.fieldOfView = 50f;
                        yield return Shoot(camera, Path.Combine(directory, "view0-0-live-while-driving.png"), new Vector3(14f, 22f, -58f), new Vector3(1f, 0f, -38f));
                        camera.fieldOfView = fov;
                        if (rig != null) rig.enabled = true;
                    }
                    float z = session.Player.Body.position.z;
                    KeyboardState keys = z < -58f ? new KeyboardState(Key.W, Key.LeftShift)
                        : z < -34f ? new KeyboardState(Key.S, Key.D)
                        : new KeyboardState(Key.W, Key.A);
                    InputState.Change(keyboard, keys);
                    keyboard.MakeCurrent();
                    yield return new WaitForFixedUpdate();
                }
                InputState.Change(keyboard, new KeyboardState());
                float deadline = Time.realtimeSinceStartup + 45f;
                while (session.Phase != GamePhase.Results && Time.realtimeSinceStartup < deadline) yield return null;
                yield return null;
                Assert.That(replay.IsAvailable, Is.True, "No replay was recorded.");

                Debug.Log("[Skids] at results: " + DescribeTrails(session));
                if (rig != null) rig.enabled = false;
                camera.fieldOfView = 50f;
                Vector3[] eyes = { new Vector3(14f, 22f, -58f), new Vector3(-16f, 30f, -6f) };
                Vector3[] targets = { new Vector3(1f, 0f, -38f), new Vector3(0f, 0f, 2f) };
                for (int v = 0; v < eyes.Length; v++) yield return Shoot(camera, Path.Combine(directory, $"view{v}-a-live.png"), eyes[v], targets[v]);

                Assert.That(replay.Open(), Is.True);
                replay.Pause();
                replay.Seek(replay.Duration);
                yield return null;
                if (rig != null) rig.enabled = false;
                int ribbons = 0, ribbonVertices = 0;
                foreach (MeshFilter filter in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
                {
                    if (!filter.name.StartsWith("Replay: Skid marks") || filter.sharedMesh == null) continue;
                    Renderer renderer = filter.GetComponent<Renderer>();
                    if (renderer == null || !renderer.enabled) continue;
                    ribbons++;
                    ribbonVertices += filter.sharedMesh.vertexCount;
                }
                Debug.Log($"[Skids] replay ribbons visible={ribbons} vertices={ribbonVertices} duration={replay.Duration:F1}s");
                for (int v = 0; v < eyes.Length; v++) yield return Shoot(camera, Path.Combine(directory, $"view{v}-b-replay-end.png"), eyes[v], targets[v]);
                replay.Seek(replay.Duration * 0.55f);
                yield return null;
                yield return Shoot(camera, Path.Combine(directory, "view0-c-replay-midway.png"), eyes[0], targets[0]);
                replay.Close();
                Assert.That(ribbons, Is.GreaterThan(0), "The replay shows no skid marks.");
            }
            finally
            {
                if (rig != null) rig.enabled = true;
                InputSystem.settings.backgroundBehavior = previousFocus;
#if UNITY_EDITOR
                InputSystem.settings.editorInputBehaviorInPlayMode = previousRouting;
#endif
                InputSystem.RemoveDevice(keyboard);
            }
        }

        private static string DescribeTrails(CrashoutGameSession session)
        {
            var parts = new System.Collections.Generic.List<string>();
            foreach (SkidMarks marks in Object.FindObjectsByType<SkidMarks>(FindObjectsSortMode.None))
            {
                Mesh mesh = marks.GetComponent<MeshFilter>().sharedMesh;
                parts.Add($"{marks.name} strips={marks.StripCount} points={marks.PointCount} vertices={(mesh != null ? mesh.vertexCount : 0)}");
            }
            return string.Join(" | ", parts);
        }

        private static IEnumerator Shoot(Camera camera, string path, Vector3 position, Vector3 target)
        {
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position, Vector3.up));
            var texture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var frame = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            try
            {
                camera.targetTexture = texture;
                yield return null;
                yield return null;
                camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position, Vector3.up));
                yield return null;
                RenderTexture.active = texture;
                frame.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                frame.Apply();
                File.WriteAllBytes(path, frame.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                Object.Destroy(frame);
                Object.Destroy(texture);
            }
        }
    }
}

using System.Collections;
using System.IO;
using Crashout.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Crashout.Tests
{
    /// <summary>
    /// Art review helper, not a regression test: renders every junction's board from where people
    /// sit and stand round a 1:32 table, to check that the junction is not hidden behind scenery.
    /// Runs only when CRASHOUT_SEATS_DIR is set, e.g.
    /// CRASHOUT_SEATS_DIR=/tmp/seats Unity -batchmode -runTests -testPlatform PlayMode -testFilter SeatViewCapture
    /// </summary>
    public class SeatViewCapture
    {
        private const int Width = 1280;
        private const int Height = 720;

        // Table positions converted to world metres at 1:32.
        private const float SeatedEye = 14.4f;    // phone 45 cm above the table
        private const float SeatedBack = 38.8f;   // 40 cm back from the board edge
        private const float StandingEye = 28.8f;  // phone 90 cm above the table

        [UnityTest, Timeout(180000)]
        public IEnumerator CaptureSeats([Values(0, 1, 2, 3)] int scenarioIndex)
        {
            string directory = System.Environment.GetEnvironmentVariable("CRASHOUT_SEATS_DIR");
            if (string.IsNullOrEmpty(directory))
            {
                Assert.Ignore("Set CRASHOUT_SEATS_DIR to capture the table seat views.");
                yield break;
            }
            Directory.CreateDirectory(directory);
            yield return null;
            var session = Object.FindFirstObjectByType<CrashoutGameSession>();
            Assert.That(session, Is.Not.Null);
            session.RequestScenarioChange((scenarioIndex - session.Scenario.Index + CrashScenarios.Count) % CrashScenarios.Count);
            yield return new WaitForFixedUpdate();
            yield return new WaitForSecondsRealtime(1.5f);

            Camera camera = Camera.main;
            var rig = camera.GetComponent<Crashout.Presentation.CrashoutCameraRig>();
            float fieldOfView = camera.fieldOfView;
            if (rig != null) rig.enabled = false;
            camera.fieldOfView = 55f;
            string prefix = Path.Combine(directory, scenarioIndex + "-" + session.Scenario.Name.Replace(' ', '-').ToLowerInvariant());
            try
            {
                yield return Shoot(camera, prefix + "-seated-west.png", new Vector3(-SeatedBack, SeatedEye, -8f));
                yield return Shoot(camera, prefix + "-seated-north.png", new Vector3(6f, SeatedEye, SeatedBack));
                yield return Shoot(camera, prefix + "-seated-east.png", new Vector3(SeatedBack, SeatedEye, 8f));
                yield return Shoot(camera, prefix + "-seated-southeast.png", new Vector3(34f, SeatedEye, -34f));
                yield return Shoot(camera, prefix + "-standing-northwest.png", new Vector3(-33f, StandingEye, 33f));
            }
            finally
            {
                camera.fieldOfView = fieldOfView;
                if (rig != null) rig.enabled = true;
            }
        }

        private static IEnumerator Shoot(Camera camera, string path, Vector3 position)
        {
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(-position, Vector3.up));
            var texture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var frame = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            try
            {
                // WaitForEndOfFrame never resumes in a batch-mode editor; let the player loop render instead.
                camera.targetTexture = texture;
                yield return null;
                yield return null;
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

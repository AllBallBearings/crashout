using System.Collections;
using System.IO;
using Crashout.Presentation;
using Crashout.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Crashout.Tests
{
    /// <summary>
    /// Design review helper, not a regression test: rebuilds the downtown board with each building
    /// height concept and renders it from the same table seats. Runs only when CRASHOUT_HEIGHTS_DIR
    /// is set, e.g. CRASHOUT_HEIGHTS_DIR=/tmp/heights Unity -runTests -testPlatform PlayMode -testFilter HeightConceptCapture
    /// </summary>
    public class HeightConceptCapture
    {
        private const int Width = 1280;
        private const int Height = 720;
        private const float SeatedEye = 14.4f;
        private const float SeatedBack = 38.8f;
        private const float StandingEye = 28.8f;

        [UnityTest, Timeout(300000)]
        public IEnumerator CaptureConcepts()
        {
            string directory = System.Environment.GetEnvironmentVariable("CRASHOUT_HEIGHTS_DIR");
            if (string.IsNullOrEmpty(directory))
            {
                Assert.Ignore("Set CRASHOUT_HEIGHTS_DIR to capture the building height concepts.");
                yield break;
            }
            Directory.CreateDirectory(directory);
            yield return null;
            var session = Object.FindFirstObjectByType<CrashoutGameSession>();
            Assert.That(session, Is.Not.Null);
            CityHeights original = CityHeights.Active;
            Camera camera = Camera.main;
            var rig = camera.GetComponent<CrashoutCameraRig>();
            float fieldOfView = camera.fieldOfView;
            try
            {
                for (int i = 0; i < CityHeights.Concepts.Length; i++)
                {
                    CityHeights.Active = CityHeights.Concepts[i];
                    // The board only rebuilds when its style changes: hop to the harbor and back.
                    yield return GoTo(session, 3);
                    yield return GoTo(session, 0);
                    yield return new WaitForSecondsRealtime(1.2f);

                    if (rig != null) rig.enabled = false;
                    camera.fieldOfView = 55f;
                    string prefix = Path.Combine(directory, i + "-" + CityHeights.Active.Name);
                    yield return Shoot(camera, prefix + "-a-seated-west.png", new Vector3(-SeatedBack, SeatedEye, -8f), Vector3.zero);
                    yield return Shoot(camera, prefix + "-b-seated-north.png", new Vector3(6f, SeatedEye, SeatedBack), Vector3.zero);
                    yield return Shoot(camera, prefix + "-c-seated-southeast.png", new Vector3(34f, SeatedEye, -34f), Vector3.zero);
                    yield return Shoot(camera, prefix + "-d-standing-northwest.png", new Vector3(-33f, StandingEye, 33f), Vector3.zero);
                    yield return Shoot(camera, prefix + "-e-driver-arriving.png", new Vector3(1.5f, 2.2f, -40f), new Vector3(1.5f, 1.2f, 0f));
                    camera.fieldOfView = fieldOfView;
                    if (rig != null) rig.enabled = true;
                }
            }
            finally
            {
                CityHeights.Active = original;
                camera.fieldOfView = fieldOfView;
                if (rig != null) rig.enabled = true;
            }
        }

        private static IEnumerator GoTo(CrashoutGameSession session, int index)
        {
            if (session.Scenario.Index != index)
            {
                session.RequestScenarioChange((index - session.Scenario.Index + CrashScenarios.Count) % CrashScenarios.Count);
            }
            yield return new WaitForFixedUpdate();
            yield return null;
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

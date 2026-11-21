using System.Collections;
using Crashout.Presentation;
using Crashout.Simulation;
using Crashout.Vehicle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Crashout.Tests
{
    public sealed class RunwayContactTests
    {
        [UnityTest, Timeout(90000)]
        public IEnumerator DrivingThroughTheGantryAndOntoTheBoardKeepsTheCarIntact([Values(0, 1, 2, 3)] int scenarioIndex, [Values(false, true)] bool boost)
        {
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
                yield return new WaitForFixedUpdate();
                session.ResetTurn();
                yield return new WaitForFixedUpdate();
                var damage = session.Player.GetComponent<DamageableVehicle>();
                var deformer = session.Player.GetComponent<VehicleDeformer>();
                Assert.That(deformer, Is.Not.Null);
                session.Launch(boost ? 1f : 0.2f);
                float speedBeforeSeam = 0f;
                for (int tick = 0; tick < 900 && session.Player.Body.position.z < -20f; tick++)
                {
                    InputState.Change(keyboard, boost ? new KeyboardState(Key.W, Key.LeftShift) : new KeyboardState(Key.W));
                    keyboard.MakeCurrent();
                    yield return new WaitForFixedUpdate();
                    string location = $"{session.Scenario.Name}, boost={boost}, z={session.Player.Body.position.z:F1}";
                    Assert.That(damage.Damage, Is.Zero, "Road contact damaged the chassis: " + location);
                    Assert.That(deformer.DentCount, Is.Zero, "Road contact deformed or detached bodywork: " + location);
                    Assert.That(session.Phase, Is.EqualTo(GamePhase.Driving), "The approach must remain drivable: " + location);
                    if (session.Player.Body.position.z < -28f) speedBeforeSeam = session.Player.SpeedMetresPerSecond;
                    if (session.Player.Body.position.z > -24f)
                        Assert.That(session.Player.SpeedMetresPerSecond, Is.GreaterThan(speedBeforeSeam * 0.8f), "The join scrubbed off speed: " + location);
                }
                Assert.That(session.Player.Body.position.z, Is.GreaterThanOrEqualTo(-20f), "Car stalled on the approach or board seam.");
                Assert.That(session.Player.PropulsionEnabled, Is.True);
            }
            finally
            {
                InputSystem.settings.backgroundBehavior = previousFocus;
#if UNITY_EDITOR
                InputSystem.settings.editorInputBehaviorInPlayMode = previousRouting;
#endif
                InputSystem.RemoveDevice(keyboard);
                session.ResetTurn();
            }
        }
    }
}

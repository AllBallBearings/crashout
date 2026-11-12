using System.Collections;
using Crashout.Presentation;
using Crashout.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Crashout.Tests
{
    public class DestructionTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator ABlastDamagesTheCityAndAResetRepairsIt()
        {
            yield return null;
            var session = Object.FindFirstObjectByType<CrashoutGameSession>();
            Assert.That(session, Is.Not.Null, "Prototype did not bootstrap.");
            Destruction city = Destruction.Instance;
            Assert.That(city, Is.Not.Null, "No destruction director.");

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
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();
                Assert.That(city.ScarCount + city.BrokenWindowCount + city.BrokenPropCount, Is.Zero, $"The city should start intact (scars {city.ScarCount}, windows {city.BrokenWindowCount}, props {city.BrokenPropCount}).");

                session.Launch(1f);
                for (int tick = 0; tick < 900 && session.Phase == GamePhase.Driving; tick++)
                {
                    InputState.Change(keyboard, new KeyboardState(Key.W, Key.LeftShift));
                    keyboard.MakeCurrent();
                    yield return new WaitForFixedUpdate();
                }
                InputState.Change(keyboard, new KeyboardState());
                Assert.That(session.Phase, Is.Not.EqualTo(GamePhase.Driving), "Player never crashed.");

                yield return new WaitForSecondsRealtime(2f);
                Assert.That(session.TryCrashbreaker(), Is.True, "Crashbreaker was not available.");
                yield return new WaitForSecondsRealtime(1.5f);
                int damage = city.ScarCount + city.BrokenWindowCount + city.BrokenPropCount;
                Assert.That(damage, Is.GreaterThan(0), "A Crashbreaker in the junction left the city untouched.");
                Assert.That(city.BrokenWindowCount, Is.GreaterThan(0), "No windows were blown out.");

                session.ResetTurn();
                yield return null;
                yield return null;
                Assert.That(city.ScarCount + city.BrokenWindowCount + city.BrokenPropCount, Is.Zero, $"Reset did not repair the city (scars {city.ScarCount}, windows {city.BrokenWindowCount}, props {city.BrokenPropCount}).");
                Assert.That(Time.timeScale, Is.EqualTo(1f).Within(0.001f));
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

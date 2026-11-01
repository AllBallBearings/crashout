using System.Collections;
using Crashout.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Crashout.Tests
{
    public class CrashModeTests
    {
        [UnityTest]
        public IEnumerator EveryScenarioProducesAChainReactionWithCrashbreaker([Values(0, 1, 2)] int scenarioIndex)
        {
            yield return null;
            var session = Object.FindFirstObjectByType<CrashoutGameSession>();
            Assert.That(session, Is.Not.Null, "Prototype did not bootstrap.");
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
                Assert.That(session.Scenario.Index, Is.EqualTo(scenarioIndex));

                for (int tick = 0; tick < 700 && session.Phase == GamePhase.Driving; tick++)
                {
                    InputState.Change(keyboard, new KeyboardState(Key.W, Key.LeftShift));
                    keyboard.MakeCurrent();
                    yield return new WaitForFixedUpdate();
                }
                Assert.That(session.Phase, Is.Not.EqualTo(GamePhase.Driving), "Player never crashed.");

                // Let the wreck run, then spend the Crashbreaker like a player would.
                InputState.Change(keyboard, new KeyboardState());
                yield return new WaitForSecondsRealtime(1.2f);
                bool fired = session.TryCrashbreaker();

                float deadline = Time.realtimeSinceStartup + 40f;
                while (session.Phase != GamePhase.Results && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }

                Debug.Log($"[CrashMode] {session.Scenario.Name}: ${session.Score:N0} wrecked={session.WreckedCount} chain={session.BestChain} " +
                    $"bonus=${session.BonusScore:N0} breakers={session.CrashbreakersUsed} fired={fired} target={session.TargetDown} medal={session.Medal} phase={session.Phase}");
                Assert.That(session.Phase, Is.EqualTo(GamePhase.Results), "Run never settled.");
                Assert.That(session.WreckedCount, Is.GreaterThanOrEqualTo(2), "No chain reaction.");
                Assert.That(Time.timeScale, Is.EqualTo(1f).Within(0.001f), "Impact time leaked.");
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
    }
}

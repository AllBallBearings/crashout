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
                session.Launch(1f);

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

        [UnityTest]
        public IEnumerator TrafficHoldsUntilLaunchAndALateSlowStartStillFindsTraffic()
        {
            yield return null;
            var session = Object.FindFirstObjectByType<CrashoutGameSession>();
            Assert.That(session, Is.Not.Null, "Prototype did not bootstrap.");
            if (session.Scenario.Index != 0)
            {
                session.RequestScenarioChange((CrashScenarios.Count - session.Scenario.Index) % CrashScenarios.Count);
                yield return new WaitForFixedUpdate();
            }
            session.ResetTurn();
            yield return new WaitForFixedUpdate();
            Assert.That(session.Phase, Is.EqualTo(GamePhase.ReadyCheck));

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
                var before = new System.Collections.Generic.List<Vector3>();
                foreach (var v in session.Vehicles) before.Add(v.transform.position);

                // A player who dawdles must not lose the action: nothing moves until launch.
                yield return new WaitForSecondsRealtime(2.5f);
                for (int i = 0; i < before.Count; i++)
                {
                    // Vehicles may settle a few centimetres onto the road, but must not travel.
                    Vector3 delta = session.Vehicles[i].transform.position - before[i];
                    delta.y = 0f;
                    Assert.That(delta.magnitude, Is.LessThan(0.15f), $"Vehicle {session.Vehicles[i].name} (id {session.Vehicles[i].EntityId}) drifted {delta} before launch.");
                }
                Assert.That(session.Phase, Is.EqualTo(GamePhase.ReadyCheck));

                // A bad launch is slower but traffic is still clocked from this moment.
                session.Launch(0.2f);
                Assert.That(session.Phase, Is.EqualTo(GamePhase.Driving));
                for (int tick = 0; tick < 900 && session.Phase == GamePhase.Driving; tick++)
                {
                    InputState.Change(keyboard, new KeyboardState(Key.W));
                    keyboard.MakeCurrent();
                    yield return new WaitForFixedUpdate();
                }
                Assert.That(session.Phase, Is.Not.EqualTo(GamePhase.Driving), "Slow start never reached traffic.");

                InputState.Change(keyboard, new KeyboardState());
                float deadline = Time.realtimeSinceStartup + 40f;
                while (session.Phase != GamePhase.Results && Time.realtimeSinceStartup < deadline) yield return null;
                Debug.Log($"[CrashMode] slow start: ${session.Score:N0} wrecked={session.WreckedCount}");
                Assert.That(session.WreckedCount, Is.GreaterThanOrEqualTo(2), "A slow start should still find traffic.");
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

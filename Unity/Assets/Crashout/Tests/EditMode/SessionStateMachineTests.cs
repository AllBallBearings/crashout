using Crashout.Simulation;
using NUnit.Framework;

namespace Crashout.Tests
{
    public sealed class SessionStateMachineTests
    {
        [Test]
        public void HostRejectsOutOfOrderPhaseTransitions()
        {
            SessionStateMachine state = new SessionStateMachine();
            Assert.That(state.TryAdvance(GamePhase.Crash), Is.False);
            Assert.That(state.Phase, Is.EqualTo(GamePhase.Lobby));
        }

        [Test]
        public void PrototypeTurnUsesOneContinuousWorldState()
        {
            SessionStateMachine state = new SessionStateMachine();
            state.StartPrototypeTurn();
            uint sequenceBeforeCrash = state.Sequence;

            Assert.That(state.TryAdvance(GamePhase.Crash), Is.True);
            Assert.That(state.HostTick, Is.EqualTo(0));
            Assert.That(state.Sequence, Is.EqualTo(sequenceBeforeCrash + 1));
        }
    }
}

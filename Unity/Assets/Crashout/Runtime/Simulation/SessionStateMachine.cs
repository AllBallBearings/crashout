using System;

namespace Crashout.Simulation
{
    public sealed class SessionStateMachine
    {
        public GamePhase Phase { get; private set; } = GamePhase.Lobby;
        public uint Sequence { get; private set; }
        public uint HostTick { get; private set; }

        public event Action<GamePhase, uint, uint> PhaseChanged;

        public void Tick()
        {
            HostTick++;
        }

        public bool TryAdvance(GamePhase next)
        {
            if (!CanAdvance(Phase, next))
            {
                return false;
            }

            Phase = next;
            Sequence++;
            PhaseChanged?.Invoke(Phase, HostTick, Sequence);
            return true;
        }

        public void StartPrototypeTurn()
        {
            Phase = GamePhase.Driving;
            Sequence++;
            PhaseChanged?.Invoke(Phase, HostTick, Sequence);
        }

        public void ResetToDriving()
        {
            Phase = GamePhase.Reset;
            Sequence++;
            PhaseChanged?.Invoke(Phase, HostTick, Sequence);
            Phase = GamePhase.Driving;
            Sequence++;
            PhaseChanged?.Invoke(Phase, HostTick, Sequence);
        }

        public static bool CanAdvance(GamePhase from, GamePhase to)
        {
            switch (from)
            {
                case GamePhase.Lobby: return to == GamePhase.BoardAlignment || to == GamePhase.ReadyCheck;
                case GamePhase.BoardAlignment: return to == GamePhase.ReadyCheck;
                case GamePhase.ReadyCheck: return to == GamePhase.Countdown;
                case GamePhase.Countdown: return to == GamePhase.Driving;
                case GamePhase.Driving: return to == GamePhase.Crash;
                case GamePhase.Crash: return to == GamePhase.SettleDelay;
                case GamePhase.SettleDelay: return to == GamePhase.Results;
                case GamePhase.Results: return to == GamePhase.Reset || to == GamePhase.MatchResults;
                case GamePhase.Reset: return to == GamePhase.Countdown || to == GamePhase.Driving;
                default: return false;
            }
        }
    }
}

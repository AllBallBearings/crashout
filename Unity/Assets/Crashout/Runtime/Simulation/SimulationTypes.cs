using System;
using UnityEngine;

namespace Crashout.Simulation
{
    public enum GamePhase : byte
    {
        Lobby,
        BoardAlignment,
        ReadyCheck,
        Countdown,
        Driving,
        Crash,
        SettleDelay,
        Results,
        Reset,
        MatchResults
    }

    public enum CameraMode : byte
    {
        Tabletop,
        Chase,
        FirstPerson,
        Overhead
    }

    [Serializable]
    public struct DriverInputCommand
    {
        public ulong ClientId;
        public uint TurnId;
        public uint TargetTick;
        public uint Sequence;
        [Range(-1f, 1f)] public float Steering;
        [Range(0f, 1f)] public float Throttle;
        [Range(0f, 1f)] public float Brake;
        public bool Reverse;
    }

    [Serializable]
    public struct EntitySnapshot
    {
        public uint HostTick;
        public uint EntityId;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 LinearVelocity;
        public Vector3 AngularVelocity;
        public float Damage;
        public bool PropulsionEnabled;
        public bool Sleeping;
    }

    [Serializable]
    public struct ScoreEvent
    {
        public uint HostTick;
        public uint EntityId;
        public int Amount;
        public int Total;
        public int ChainCount;
        public float Impulse;
        public float RelativeSpeed;
    }
}

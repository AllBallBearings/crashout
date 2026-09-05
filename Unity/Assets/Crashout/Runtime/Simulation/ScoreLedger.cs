using System;
using System.Collections.Generic;

namespace Crashout.Simulation
{
    public sealed class ScoreLedger
    {
        private readonly Dictionary<uint, int> entityTotals = new Dictionary<uint, int>();
        private readonly Dictionary<ulong, uint> recentContacts = new Dictionary<ulong, uint>();
        private readonly HashSet<uint> chainEntities = new HashSet<uint>();

        public int Total { get; private set; }
        public int ChainCount => chainEntities.Count;
        public int HighestImpactScore { get; private set; }
        public uint HighestImpactEntity { get; private set; }

        public void Reset()
        {
            entityTotals.Clear();
            recentContacts.Clear();
            chainEntities.Clear();
            Total = 0;
            HighestImpactScore = 0;
            HighestImpactEntity = 0;
        }

        public bool TryScore(
            uint hostTick,
            uint entityId,
            uint otherEntityId,
            float impulse,
            float relativeSpeed,
            int entityCap,
            out ScoreEvent scoreEvent)
        {
            scoreEvent = default;
            if (impulse < 1.8f || relativeSpeed < 1.4f || entityCap <= 0)
            {
                return false;
            }

            ulong contactKey = MakeContactKey(entityId, otherEntityId);
            if (recentContacts.TryGetValue(contactKey, out uint lastTick) && hostTick - lastTick < 24)
            {
                return false;
            }
            recentContacts[contactKey] = hostTick;

            int alreadyAwarded = entityTotals.TryGetValue(entityId, out int value) ? value : 0;
            int remaining = Math.Max(0, entityCap - alreadyAwarded);
            if (remaining == 0)
            {
                return false;
            }

            chainEntities.Add(entityId);
            int baseDamage = (int)Math.Round(impulse * 58f + relativeSpeed * 125f);
            int chainBonus = Math.Max(0, ChainCount - 1) * 225;
            int amount = Math.Min(remaining, baseDamage + chainBonus);
            entityTotals[entityId] = alreadyAwarded + amount;
            Total += amount;

            if (amount > HighestImpactScore)
            {
                HighestImpactScore = amount;
                HighestImpactEntity = entityId;
            }

            scoreEvent = new ScoreEvent
            {
                HostTick = hostTick,
                EntityId = entityId,
                Amount = amount,
                Total = Total,
                ChainCount = ChainCount,
                Impulse = impulse,
                RelativeSpeed = relativeSpeed
            };
            return true;
        }

        private static ulong MakeContactKey(uint first, uint second)
        {
            uint low = Math.Min(first, second);
            uint high = Math.Max(first, second);
            return ((ulong)low << 32) | high;
        }
    }
}

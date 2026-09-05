using Crashout.Simulation;
using NUnit.Framework;

namespace Crashout.Tests
{
    public sealed class ScoreLedgerTests
    {
        [Test]
        public void RestingContactDoesNotFarmScore()
        {
            ScoreLedger ledger = new ScoreLedger();
            Assert.That(ledger.TryScore(100, 10, 1, 8f, 6f, 20000, out _), Is.True);
            Assert.That(ledger.TryScore(101, 10, 1, 8f, 6f, 20000, out _), Is.False);
        }

        [Test]
        public void EntityScoreNeverExceedsItsCap()
        {
            ScoreLedger ledger = new ScoreLedger();
            Assert.That(ledger.TryScore(100, 10, 1, 200f, 80f, 5000, out ScoreEvent score), Is.True);
            Assert.That(score.Total, Is.EqualTo(5000));
            Assert.That(ledger.TryScore(200, 10, 2, 200f, 80f, 5000, out _), Is.False);
        }

        [Test]
        public void DistinctVehiclesIncreaseTheChain()
        {
            ScoreLedger ledger = new ScoreLedger();
            ledger.TryScore(100, 10, 1, 8f, 6f, 20000, out _);
            ledger.TryScore(130, 11, 10, 8f, 6f, 20000, out ScoreEvent second);
            Assert.That(second.ChainCount, Is.EqualTo(2));
            Assert.That(second.Amount, Is.GreaterThan(0));
        }
    }
}

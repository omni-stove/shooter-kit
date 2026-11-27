using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class NeutralGuardStateTests
    {
        [Test]
        public void ItStartsNeutral()
        {
            var state = new NeutralGuardState();

            Assert.That(state.Phase, Is.EqualTo(NeutralGuardPhase.Neutral));
            Assert.That(state.IsHostile, Is.False);
            Assert.That(state.IsWarning, Is.False);
        }

        [Test]
        public void AWarningZoneMovesItToWarningAndClearingTheWarningMovesItBack()
        {
            var state = new NeutralGuardState();

            Assert.That(state.Observe(true, false), Is.True);
            Assert.That(state.Phase, Is.EqualTo(NeutralGuardPhase.Warning));
            Assert.That(state.Observe(true, false), Is.False, "the same phase is not a change");

            Assert.That(state.Observe(false, false), Is.True, "leaving the zone or holstering clears the warning");
            Assert.That(state.Phase, Is.EqualTo(NeutralGuardPhase.Neutral));
        }

        [Test]
        public void AHostileZoneTurnsItHostileForGood()
        {
            var state = new NeutralGuardState();
            state.Observe(true, false);

            Assert.That(state.Observe(false, true), Is.True);
            Assert.That(state.Phase, Is.EqualTo(NeutralGuardPhase.Hostile));

            Assert.That(state.Observe(false, false), Is.False);
            Assert.That(state.Observe(true, false), Is.False);
            Assert.That(state.Phase, Is.EqualTo(NeutralGuardPhase.Hostile), "hostility never goes back");
        }

        [Test]
        public void BeingShotTurnsItHostileRightAwayEvenWhileNeutral()
        {
            var state = new NeutralGuardState();

            Assert.That(state.Provoke(), Is.True);
            Assert.That(state.IsHostile, Is.True);
            Assert.That(state.Provoke(), Is.False, "a second shot is not a change");
            Assert.That(state.Observe(false, false), Is.False);
            Assert.That(state.IsHostile, Is.True);
        }

        [Test]
        public void BeingShotDuringAWarningSkipsTheCountdown()
        {
            var state = new NeutralGuardState();
            state.Observe(true, false);

            Assert.That(state.Provoke(), Is.True);
            Assert.That(state.Phase, Is.EqualTo(NeutralGuardPhase.Hostile));
        }
    }
}

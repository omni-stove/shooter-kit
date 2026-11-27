using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class WeaponJamStateTests
    {
        // 0.25 刻みにするのは、float の足し引きで誤差が出ず、確率と秒数をそのまま比べられるため。
        private static WeaponJamState Create() => new WeaponJamState(new JamSpec(0.25f, 0.25f, 1f, 0.75f, 1f));

        private static WeaponJamState Jammed()
        {
            var state = Create();
            Assert.That(state.RegisterShot(0f), Is.True);
            return state;
        }

        [Test]
        public void ItStartsClearWithTheFirstShotChance()
        {
            var state = Create();

            Assert.That(state.IsJammed, Is.False);
            Assert.That(state.IsClearing, Is.False);
            Assert.That(state.ClearRemainingSeconds, Is.EqualTo(0f));
            Assert.That(state.SustainedShots, Is.EqualTo(0));
            Assert.That(state.NextShotChance, Is.EqualTo(0.25f));
            Assert.Throws<ArgumentNullException>(() => new WeaponJamState(null));
        }

        [Test]
        public void EachSustainedShotAddsChanceUpToTheMaximum()
        {
            var state = Create();

            Assert.That(state.RegisterShot(0.9f), Is.False);
            Assert.That(state.SustainedShots, Is.EqualTo(1));
            Assert.That(state.NextShotChance, Is.EqualTo(0.5f));

            Assert.That(state.RegisterShot(0.9f), Is.False);
            Assert.That(state.NextShotChance, Is.EqualTo(0.75f));

            Assert.That(state.RegisterShot(0.9f), Is.False);
            Assert.That(state.SustainedShots, Is.EqualTo(3));
            Assert.That(state.NextShotChance, Is.EqualTo(0.75f), "0.25 + 3 x 0.25 = 1 is held at the 0.75 maximum");
        }

        [Test]
        public void ARollBelowTheChanceJamsAndARollAtItDoesNot()
        {
            var state = Create();

            Assert.That(state.RegisterShot(0.25f), Is.False, "the chance is 0.25, so a roll of exactly 0.25 fires");
            Assert.That(state.IsJammed, Is.False);

            Assert.That(state.RegisterShot(0.49f), Is.True, "the second shot's chance is 0.5");
            Assert.That(state.IsJammed, Is.True);
            Assert.That(state.IsClearing, Is.False);
            Assert.Throws<InvalidOperationException>(() => state.RegisterShot(0.9f));
        }

        [Test]
        public void ARollOutsideTheUnitRangeIsRefused()
        {
            var state = Create();

            Assert.Throws<ArgumentOutOfRangeException>(() => state.RegisterShot(1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.RegisterShot(-0.25f));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.RegisterShot(float.NaN));
            Assert.That(state.SustainedShots, Is.EqualTo(0));
        }

        [Test]
        public void ResetSecondsWithoutShootingDropTheSustainedShots()
        {
            var state = Create();
            state.RegisterShot(0.9f);
            state.RegisterShot(0.9f);

            state.Tick(0.5f);
            Assert.That(state.SustainedShots, Is.EqualTo(2));
            Assert.That(state.NextShotChance, Is.EqualTo(0.75f));

            state.Tick(0.5f);
            Assert.That(state.SustainedShots, Is.EqualTo(0));
            Assert.That(state.NextShotChance, Is.EqualTo(0.25f));

            Assert.That(state.RegisterShot(0.3f), Is.False, "after the reset the shot is back to the 0.25 chance");
            Assert.That(state.SustainedShots, Is.EqualTo(1));
        }

        [Test]
        public void ClearingTakesTheClearSecondsExactly()
        {
            var state = Create();
            Assert.That(state.TryStartClear(), Is.False, "nothing to clear");

            Assert.That(state.RegisterShot(0f), Is.True);
            Assert.That(state.TryStartClear(), Is.True);
            Assert.That(state.IsClearing, Is.True);
            Assert.That(state.ClearRemainingSeconds, Is.EqualTo(1f));
            Assert.That(state.TryStartClear(), Is.False, "already clearing");

            Assert.That(state.Tick(0.5f), Is.False);
            Assert.That(state.IsJammed, Is.True);
            Assert.That(state.ClearRemainingSeconds, Is.EqualTo(0.5f));

            Assert.That(state.Tick(0.5f), Is.True);
            Assert.That(state.IsJammed, Is.False);
            Assert.That(state.IsClearing, Is.False);
            Assert.That(state.ClearRemainingSeconds, Is.EqualTo(0f));
            Assert.That(state.Tick(0.5f), Is.False, "it reports the clear only once");
        }

        [Test]
        public void ALongTickFinishesTheClear()
        {
            var state = Jammed();
            state.TryStartClear();

            Assert.That(state.Tick(3f), Is.True);
            Assert.That(state.IsJammed, Is.False);
            Assert.That(state.ClearRemainingSeconds, Is.EqualTo(0f));
        }

        [Test]
        public void TickingWhileJammedButNotClearingLeavesItJammed()
        {
            var state = Jammed();

            Assert.That(state.Tick(5f), Is.False);
            Assert.That(state.IsJammed, Is.True);
            Assert.That(state.ClearRemainingSeconds, Is.EqualTo(0f));
        }

        [Test]
        public void CancelingKeepsTheJamAndTheNextClearStartsOver()
        {
            var state = Jammed();
            Assert.That(state.CancelClear(), Is.False, "not clearing yet");

            state.TryStartClear();
            state.Tick(0.5f);
            Assert.That(state.CancelClear(), Is.True);

            Assert.That(state.IsJammed, Is.True);
            Assert.That(state.IsClearing, Is.False);
            Assert.That(state.ClearRemainingSeconds, Is.EqualTo(0f));
            Assert.That(state.CancelClear(), Is.False);
            Assert.That(state.Tick(1f), Is.False);
            Assert.That(state.IsJammed, Is.True);

            Assert.That(state.TryStartClear(), Is.True);
            Assert.That(state.ClearRemainingSeconds, Is.EqualTo(1f));
        }

        [Test]
        public void ANegativeOrNonFiniteTickIsRefused()
        {
            var state = Create();

            Assert.Throws<ArgumentOutOfRangeException>(() => state.Tick(-0.25f));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Tick(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Tick(float.PositiveInfinity));
        }
    }
}

using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    // 秒数は 2 の累乗の分数にして、足し合わせの丸めで敵対する tick がずれないようにする。
    public sealed class WeaponFreeZoneStateTests
    {
        [Test]
        public void ItStartsOutsideWithoutAWarning()
        {
            var state = new WeaponFreeZoneState(WeaponFreeZoneSpec.Default);

            Assert.That(state.Inside, Is.False);
            Assert.That(state.Warning, Is.False);
            Assert.That(state.IsHostile, Is.False);
            Assert.That(state.ShouldHolster, Is.False);
            Assert.That(state.BlocksDraw, Is.False);
            Assert.That(state.Status, Is.EqualTo(WeaponFreeZoneStatus.Outside));
        }

        [Test]
        public void ANullSpecOrABadTimeIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new WeaponFreeZoneState(null));
            var state = new WeaponFreeZoneState(WeaponFreeZoneSpec.Default);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Tick(-1f, true, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Tick(float.NaN, true, true));
        }

        [Test]
        public void WarnOnlyWarnsWhileArmedInsideAndNeverTurnsHostile()
        {
            var state = new WeaponFreeZoneState(new WeaponFreeZoneSpec(WeaponFreeZoneReaction.WarnOnly, 1f));

            Assert.That(state.Tick(0f, true, true), Is.False);
            Assert.That(state.Warning, Is.True);
            Assert.That(state.SecondsToHostile, Is.Zero, "a warn-only zone has no countdown");

            Assert.That(state.Tick(100f, true, true), Is.False);
            Assert.That(state.Warning, Is.True);
            Assert.That(state.IsHostile, Is.False);
            Assert.That(state.Status, Is.EqualTo(new WeaponFreeZoneStatus(true, WeaponFreeZoneReaction.WarnOnly, true, 0f)));
        }

        [Test]
        public void WarnThenHostileCountsDownFromTheTickItWarnsAndTurnsHostileWhenTheTimeIsUp()
        {
            var state = new WeaponFreeZoneState(new WeaponFreeZoneSpec(WeaponFreeZoneReaction.WarnThenHostile, 2f));

            Assert.That(state.Tick(0.5f, true, true), Is.False);
            Assert.That(state.Warning, Is.True);
            Assert.That(state.SecondsToHostile, Is.EqualTo(2f), "the tick that starts the warning does not count");

            Assert.That(state.Tick(1.5f, true, true), Is.False);
            Assert.That(state.SecondsToHostile, Is.EqualTo(0.5f));
            Assert.That(state.Status, Is.EqualTo(new WeaponFreeZoneStatus(true, WeaponFreeZoneReaction.WarnThenHostile, true, 0.5f)));

            Assert.That(state.Tick(0.5f, true, true), Is.True, "turning hostile is reported on the tick it happens");
            Assert.That(state.IsHostile, Is.True);
            Assert.That(state.Warning, Is.False, "there is nothing left to warn about");
            Assert.That(state.SecondsToHostile, Is.Zero);

            Assert.That(state.Tick(1f, true, true), Is.False, "it only turns hostile once");
        }

        [Test]
        public void AZeroSecondCountdownTurnsHostileOnTheFirstArmedTick()
        {
            var state = new WeaponFreeZoneState(new WeaponFreeZoneSpec(WeaponFreeZoneReaction.WarnThenHostile, 0f));

            Assert.That(state.Tick(0f, true, true), Is.True);
            Assert.That(state.IsHostile, Is.True);
        }

        [Test]
        public void HolsteringClearsTheWarningAndRestartsTheCountdown()
        {
            var state = new WeaponFreeZoneState(new WeaponFreeZoneSpec(WeaponFreeZoneReaction.WarnThenHostile, 2f));
            state.Tick(0f, true, true);
            state.Tick(1.5f, true, true);

            state.Tick(0.25f, true, false);
            Assert.That(state.Inside, Is.True);
            Assert.That(state.Warning, Is.False);
            Assert.That(state.WarnedSeconds, Is.Zero);
            Assert.That(state.Status, Is.EqualTo(new WeaponFreeZoneStatus(true, WeaponFreeZoneReaction.WarnThenHostile, false, 0f)));

            state.Tick(0f, true, true);
            Assert.That(state.SecondsToHostile, Is.EqualTo(2f), "drawing again starts a fresh countdown");
            Assert.That(state.Tick(1.5f, true, true), Is.False);
            Assert.That(state.IsHostile, Is.False);
        }

        [Test]
        public void LeavingClearsTheWarning()
        {
            var state = new WeaponFreeZoneState(new WeaponFreeZoneSpec(WeaponFreeZoneReaction.WarnThenHostile, 2f));
            state.Tick(0f, true, true);
            state.Tick(1f, true, true);

            state.Tick(1f, false, true);

            Assert.That(state.Inside, Is.False);
            Assert.That(state.Warning, Is.False);
            Assert.That(state.IsHostile, Is.False);
            Assert.That(state.Status, Is.EqualTo(WeaponFreeZoneStatus.Outside));
        }

        [Test]
        public void HostilityStaysAfterLeavingAndHolsteringAndDoesNotWarnAgain()
        {
            var state = new WeaponFreeZoneState(new WeaponFreeZoneSpec(WeaponFreeZoneReaction.WarnThenHostile, 0.5f));
            state.Tick(0f, true, true);
            state.Tick(0.5f, true, true);
            Assert.That(state.IsHostile, Is.True);

            state.Tick(0f, false, false);
            state.Leave();
            state.Tick(0f, true, true);

            Assert.That(state.IsHostile, Is.True);
            Assert.That(state.Warning, Is.False);
        }

        [Test]
        public void ArmedEntryIntoAnAutoHolsterZoneAsksToHolsterOnlyOnTheEnteringTick()
        {
            var state = new WeaponFreeZoneState(new WeaponFreeZoneSpec(WeaponFreeZoneReaction.AutoHolster));

            state.Tick(0f, true, true);
            Assert.That(state.ShouldHolster, Is.True);
            Assert.That(state.Warning, Is.False, "auto holster has no warning step");
            Assert.That(state.IsHostile, Is.False);

            state.Tick(0.25f, true, true);
            Assert.That(state.ShouldHolster, Is.False, "drawing again inside is allowed");

            state.Tick(0f, false, true);
            state.Tick(0f, true, false);
            Assert.That(state.ShouldHolster, Is.False, "an unarmed entry has nothing to holster");

            state.Tick(100f, true, true);
            Assert.That(state.IsHostile, Is.False);
        }

        [Test]
        public void ABlockDrawZoneBlocksOnlyWhileInsideAndNeverWarns()
        {
            var state = new WeaponFreeZoneState(new WeaponFreeZoneSpec(WeaponFreeZoneReaction.BlockDraw));

            state.Tick(0f, true, false);
            Assert.That(state.BlocksDraw, Is.True);
            state.Tick(100f, true, true);
            Assert.That(state.BlocksDraw, Is.True);
            Assert.That(state.Warning, Is.False);
            Assert.That(state.IsHostile, Is.False);
            Assert.That(state.ShouldHolster, Is.False);

            state.Tick(0f, false, false);
            Assert.That(state.BlocksDraw, Is.False);
        }

        [Test]
        public void LeaveClearsEverythingButHostility()
        {
            var state = new WeaponFreeZoneState(new WeaponFreeZoneSpec(WeaponFreeZoneReaction.AutoHolster));
            state.Tick(0f, true, true);

            state.Leave();

            Assert.That(state.Inside, Is.False);
            Assert.That(state.Armed, Is.False);
            Assert.That(state.ShouldHolster, Is.False);
            Assert.That(state.Status, Is.EqualTo(WeaponFreeZoneStatus.Outside));
        }
    }
}

using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class WeaponFreeZoneTypesTests
    {
        [Test]
        public void TheDefaultSpecWarnsThenTurnsHostileAfterFiveSeconds()
        {
            Assert.That(WeaponFreeZoneSpec.Default.Reaction, Is.EqualTo(WeaponFreeZoneReaction.WarnThenHostile));
            Assert.That(WeaponFreeZoneSpec.Default.HostileAfterSeconds, Is.EqualTo(5f));
            Assert.That(WeaponFreeZoneSpec.Default.Warns, Is.True);
            Assert.That(WeaponFreeZoneSpec.Default.TurnsHostile, Is.True);
        }

        [TestCase(WeaponFreeZoneReaction.WarnOnly, true, false)]
        [TestCase(WeaponFreeZoneReaction.WarnThenHostile, true, true)]
        [TestCase(WeaponFreeZoneReaction.AutoHolster, false, false)]
        [TestCase(WeaponFreeZoneReaction.BlockDraw, false, false)]
        public void OnlyTheWarningReactionsWarnAndOnlyOneTurnsHostile(WeaponFreeZoneReaction reaction, bool warns, bool turnsHostile)
        {
            var spec = new WeaponFreeZoneSpec(reaction, 2f);

            Assert.That(spec.Reaction, Is.EqualTo(reaction));
            Assert.That(spec.HostileAfterSeconds, Is.EqualTo(2f));
            Assert.That(spec.Warns, Is.EqualTo(warns));
            Assert.That(spec.TurnsHostile, Is.EqualTo(turnsHostile));
        }

        [Test]
        public void TheSpecRejectsAnUnknownReactionAndBadSeconds()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new WeaponFreeZoneSpec((WeaponFreeZoneReaction)9));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WeaponFreeZoneSpec(hostileAfterSeconds: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WeaponFreeZoneSpec(hostileAfterSeconds: float.NaN));
            Assert.That(new WeaponFreeZoneSpec(hostileAfterSeconds: 0f).HostileAfterSeconds, Is.Zero, "0 turns hostile at once");
        }

        [Test]
        public void AWarningStatusKeepsTheSecondsLeftBeforeHostile()
        {
            var status = new WeaponFreeZoneStatus(true, WeaponFreeZoneReaction.WarnThenHostile, true, 3.5f);

            Assert.That(status.Inside, Is.True);
            Assert.That(status.Reaction, Is.EqualTo(WeaponFreeZoneReaction.WarnThenHostile));
            Assert.That(status.Warning, Is.True);
            Assert.That(status.SecondsToHostile, Is.EqualTo(3.5f));
        }

        [Test]
        public void SecondsThatMeanNothingAreZeroSoTheSameStateIsEqual()
        {
            var outside = new WeaponFreeZoneStatus(false, WeaponFreeZoneReaction.WarnThenHostile, true, 3f);
            var notWarning = new WeaponFreeZoneStatus(true, WeaponFreeZoneReaction.WarnThenHostile, false, 3f);
            var warnOnly = new WeaponFreeZoneStatus(true, WeaponFreeZoneReaction.WarnOnly, true, 3f);

            Assert.That(outside.Warning, Is.False, "no warning outside the zone");
            Assert.That(outside.SecondsToHostile, Is.Zero);
            Assert.That(notWarning.SecondsToHostile, Is.Zero);
            Assert.That(warnOnly.Warning, Is.True);
            Assert.That(warnOnly.SecondsToHostile, Is.Zero, "a warning that never turns hostile has no countdown");
            Assert.That(outside == new WeaponFreeZoneStatus(false, WeaponFreeZoneReaction.WarnThenHostile, false, 0f), Is.True);
            Assert.That(WeaponFreeZoneStatus.Outside.Inside, Is.False);
            Assert.That(WeaponFreeZoneStatus.Outside != notWarning, Is.True);
        }

        [Test]
        public void TheStatusRejectsAnUnknownReactionAndBadSeconds()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new WeaponFreeZoneStatus(true, (WeaponFreeZoneReaction)9, false, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WeaponFreeZoneStatus(true, WeaponFreeZoneReaction.WarnThenHostile, true, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WeaponFreeZoneStatus(true, WeaponFreeZoneReaction.WarnThenHostile, true, float.PositiveInfinity));
        }
    }
}

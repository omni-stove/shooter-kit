using System;
using GameKit.Core;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class MatchSetupLogTests
    {
        [Test]
        public void FormatsRulesChanged()
        {
            Assert.That(
                MatchSetupLog.RulesChanged(MatchObjective.None, MatchObjective.Domination),
                Is.EqualTo("[Match] rules changed objective from=None to=Domination"));
            Assert.That(
                MatchSetupLog.RulesChanged(MatchObjective.Domination, MatchObjective.Domination),
                Is.EqualTo("[Match] rules changed objective from=Domination to=Domination"));
        }

        [Test]
        public void FormatsTheLoadoutAndEachWeapon()
        {
            var rifle = new WeaponSpec(
                "00000000-0000-4000-8000-000000000001", "Rifle", WeaponCategory.AssaultRifle, FireMode.Auto,
                30, 90, 0.1f, 2f, 25, 100f, new DamageFalloff(20f, 60f, 0.5f));
            var handgun = new WeaponSpec(
                "00000000-0000-4000-8000-000000000002", "Handgun", WeaponCategory.Handgun, FireMode.Semi,
                12, 48, 0.2f, 1.5f, 34, 60f, DamageFalloff.None);
            var rule = new LoadoutRule(new[] { new[] { WeaponCategory.AssaultRifle }, new[] { WeaponCategory.Handgun } });
            var loadout = new ServerLoadout(rule, new[] { rifle, handgun }, 0.4f);

            Assert.That(MatchSetupLog.Loadout("DefaultLoadout", loadout), Is.EqualTo("[Match] loadout name=DefaultLoadout slots=2 switch=0.40s"));
            Assert.That(MatchSetupLog.Loadout(null, loadout), Is.EqualTo("[Match] loadout name=- slots=2 switch=0.40s"));
            Assert.That(MatchSetupLog.Weapon(0, rifle), Is.EqualTo(
                "[Match] weapon slot=0 name=Rifle id=00000000-0000-4000-8000-000000000001 category=AssaultRifle mode=Auto"
                + " damage=25 range=100.0m falloff=20.0-60.0m*0.50 fireInterval=0.10s magazine=30 reserve=90 reload=2.00s"));
            Assert.That(MatchSetupLog.Weapon(1, handgun), Is.EqualTo(
                "[Match] weapon slot=1 name=Handgun id=00000000-0000-4000-8000-000000000002 category=Handgun mode=Semi"
                + " damage=34 range=60.0m falloff=none fireInterval=0.20s magazine=12 reserve=48 reload=1.50s"));
        }

        [Test]
        public void FormatsTheRulesWithTheMatchTag()
        {
            var rules = GameRules.Default.WithFriendlyFire(FriendlyFire.On).WithRegenPerSecond(5f);

            Assert.That(
                MatchSetupLog.Rules("Default", rules),
                Is.EqualTo(
                    "[Match] rules name=Default friendlyFire=On respawnMode=Individual waveInterval=10.00s regenDelay=0.00s regenPerSecond=5.00"
                    + " deathBody=Vanish deathCamera=None infiniteReserve=false reserveMultiplier=1.00 destruction=Off"));
            Assert.That(MatchSetupLog.Rules(" ", rules), Does.StartWith(MatchLog.Tag + " rules name=- "));
            Assert.That(MatchSetupLog.LoadoutCatalog("Catalog", 12), Is.EqualTo("[Match] loadout catalog name=Catalog weapons=12"));
            Assert.That(MatchSetupLog.LoadoutCatalog(null, 0), Is.EqualTo("[Match] loadout catalog name=- weapons=0"));
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            Assert.Throws<ArgumentNullException>(() => MatchSetupLog.Loadout("DefaultLoadout", null));
            Assert.Throws<ArgumentNullException>(() => MatchSetupLog.Weapon(0, null));
            Assert.Throws<ArgumentNullException>(() => MatchSetupLog.Rules("Default", null));
        }
    }
}

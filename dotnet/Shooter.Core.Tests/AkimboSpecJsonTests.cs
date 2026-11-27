using System;
using GameKit.Core;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class AkimboSpecJsonTests
    {
        private const string _id = "5d2c8a1e-7b3f-4e9a-8c6d-1f0e2b4a9c37";

        private static string Pistol(string extra)
        {
            return "{ \"id\": \"" + _id + "\", \"category\": \"Handgun\", \"ammo\": { \"magazineSize\": 8, \"reserveAmmo\": 24, \"reloadSeconds\": 1.5 }"
                + (extra.Length == 0 ? string.Empty : ", " + extra)
                + " }";
        }

        [Test]
        public void AWeaponWithoutAkimboHasNoAkimboPart()
        {
            var spec = WeaponSpecJson.Parse(Pistol(string.Empty), "pistol.json");

            Assert.That(spec.Akimbo, Is.Null);
            Assert.That(spec.IsAkimbo, Is.False);
        }

        [Test]
        public void AnEmptyAkimboTakesTheDefaults()
        {
            var akimbo = WeaponSpecJson.Parse(Pistol("\"akimbo\": {}"), "pistol.json").Akimbo;

            Assert.That(akimbo, Is.Not.Null);
            Assert.That(akimbo.SpreadMultiplier, Is.EqualTo(1.5f));
            Assert.That(akimbo.BlocksAds, Is.True);
            Assert.That(akimbo.ReloadSecondsMultiplier, Is.EqualTo(1f));
            Assert.That(akimbo.MoveSpeedMultiplier, Is.EqualTo(1f));
            Assert.That(akimbo.OffhandShotDelaySeconds, Is.EqualTo(0.05f));
        }

        [Test]
        public void TheAkimboItemsAreRead()
        {
            var spec = WeaponSpecJson.Parse(
                Pistol("\"akimbo\": { \"spreadMultiplier\": 2, \"blocksAds\": false, \"reloadSecondsMultiplier\": 1.25, \"moveSpeedMultiplier\": 0.9, \"offhandShotDelaySeconds\": 0.1 }"),
                "pistol.json");

            Assert.That(spec.IsAkimbo, Is.True);
            Assert.That(spec.Akimbo.SpreadMultiplier, Is.EqualTo(2f));
            Assert.That(spec.Akimbo.BlocksAds, Is.False);
            Assert.That(spec.Akimbo.ReloadSecondsMultiplier, Is.EqualTo(1.25f));
            Assert.That(spec.Akimbo.MoveSpeedMultiplier, Is.EqualTo(0.9f));
            Assert.That(spec.Akimbo.OffhandShotDelaySeconds, Is.EqualTo(0.1f));
        }

        [Test]
        public void AkimboRoundTripsThroughWrite()
        {
            var spec = WeaponSpecJson.Parse(
                Pistol("\"akimbo\": { \"spreadMultiplier\": 2, \"blocksAds\": false, \"reloadSecondsMultiplier\": 1.25, \"moveSpeedMultiplier\": 0.9, \"offhandShotDelaySeconds\": 0.1 }"),
                "pistol.json");

            var text = WeaponSpecJson.Write(spec);
            var again = WeaponSpecJson.Parse(text, "pistol.json");

            Assert.That(text, Does.Contain("\"akimbo\""));
            Assert.That(again.Akimbo.SpreadMultiplier, Is.EqualTo(2f));
            Assert.That(again.Akimbo.BlocksAds, Is.False);
            Assert.That(again.Akimbo.ReloadSecondsMultiplier, Is.EqualTo(1.25f));
            Assert.That(again.Akimbo.MoveSpeedMultiplier, Is.EqualTo(0.9f));
            Assert.That(again.Akimbo.OffhandShotDelaySeconds, Is.EqualTo(0.1f));
            Assert.That(WeaponSpecJson.Write(again), Is.EqualTo(text));
        }

        [Test]
        public void AWeaponWithoutAkimboIsWrittenWithoutTheItem()
        {
            var spec = WeaponSpecJson.Parse(Pistol(string.Empty), "pistol.json");

            Assert.That(WeaponSpecJson.Write(spec), Does.Not.Contain("akimbo"));
        }

        [Test]
        public void AnUnknownAkimboItemIsRejected()
        {
            var e = Assert.Throws<ServerDataJsonException>(
                () => WeaponSpecJson.Parse(Pistol("\"akimbo\": { \"spread\": 2 }"), "pistol.json"));

            Assert.That(e.Item, Is.EqualTo("akimbo.spread"));
        }

        [TestCase("\"spreadMultiplier\": -1", "akimbo.spreadMultiplier")]
        [TestCase("\"reloadSecondsMultiplier\": 0", "akimbo.reloadSecondsMultiplier")]
        [TestCase("\"moveSpeedMultiplier\": -0.5", "akimbo.moveSpeedMultiplier")]
        [TestCase("\"offhandShotDelaySeconds\": -0.01", "akimbo.offhandShotDelaySeconds")]
        public void AnInvalidAkimboValueIsRejectedAtItsItem(string item, string path)
        {
            var e = Assert.Throws<ServerDataJsonException>(
                () => WeaponSpecJson.Parse(Pistol("\"akimbo\": { " + item + " }"), "pistol.json"));

            Assert.That(e.Item, Is.EqualTo(path));
        }

        [Test]
        public void AShieldCannotBeAkimbo()
        {
            var e = Assert.Throws<ServerDataJsonException>(
                () => WeaponSpecJson.Parse(Pistol("\"shield\": {}, \"akimbo\": {}"), "pistol.json"));

            Assert.That(e.Item, Is.EqualTo("akimbo"));
        }

        [Test]
        public void AReloadMultiplierThatMakesTheReloadInfiniteIsRejected()
        {
            var e = Assert.Throws<ServerDataJsonException>(
                () => WeaponSpecJson.Parse(Pistol("\"akimbo\": { \"reloadSecondsMultiplier\": 3e38 }"), "pistol.json"));

            Assert.That(e.Item, Is.EqualTo("akimbo"));
        }

        [Test]
        public void AReloadMultiplierThatMakesTheReloadZeroIsRejected()
        {
            var e = Assert.Throws<ServerDataJsonException>(
                () => WeaponSpecJson.Parse(
                    "{ \"id\": \"" + _id + "\", \"category\": \"Handgun\","
                    + " \"ammo\": { \"magazineSize\": 8, \"reserveAmmo\": 24, \"reloadSeconds\": 0.25, \"reloadStyle\": \"PerRound\", \"reloadSecondsPerRound\": 0.5 },"
                    + " \"akimbo\": { \"reloadSecondsMultiplier\": 1e-45 } }",
                    "pistol.json"));

            Assert.That(e.Item, Is.EqualTo("akimbo"));
        }

        [Test]
        public void AReloadMultiplierThatMakesThePerRoundSecondsZeroIsRejected()
        {
            var e = Assert.Throws<ArgumentException>(() => new WeaponSpec(
                _id,
                null,
                WeaponCategory.Handgun,
                ammo: new AmmoSpec(8, 24, 0f, reloadStyle: ReloadStyle.PerRound, reloadSecondsPerRound: 0.5f),
                akimbo: new AkimboSpec(reloadSecondsMultiplier: float.Epsilon)));

            Assert.That(e.ParamName, Is.EqualTo("akimbo"));
        }

        [Test]
        public void ASmallReloadMultiplierThatKeepsTheReloadPositiveIsAccepted()
        {
            var spec = new WeaponSpec(
                _id,
                null,
                WeaponCategory.Handgun,
                ammo: new AmmoSpec(8, 24, 0f, reloadStyle: ReloadStyle.PerRound, reloadSecondsPerRound: 0.5f),
                akimbo: new AkimboSpec(reloadSecondsMultiplier: 1e-30f));

            Assert.That(new AkimboHands(spec).Right.Spec.Ammo.ReloadSecondsPerRound, Is.EqualTo(0.5f * 1e-30f));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(-1f)]
        public void TheSpecRejectsABadSpreadMultiplier(float value)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AkimboSpec(spreadMultiplier: value));
        }

        [Test]
        public void TheSpecDefaultsMatchTheConstants()
        {
            var akimbo = AkimboSpec.Default;

            Assert.That(akimbo.SpreadMultiplier, Is.EqualTo(1.5f));
            Assert.That(akimbo.BlocksAds, Is.True);
            Assert.That(akimbo.ReloadSecondsMultiplier, Is.EqualTo(1f));
            Assert.That(akimbo.MoveSpeedMultiplier, Is.EqualTo(1f));
            Assert.That(akimbo.OffhandShotDelaySeconds, Is.EqualTo(0.05f));
        }

        [Test]
        public void WithFireIntervalAndWithAmmoKeepTheAkimboPart()
        {
            var spec = new WeaponSpec(_id, null, WeaponCategory.Handgun, new TriggerSpec(FireMode.Semi, 0.2f), akimbo: AkimboSpec.Default);

            Assert.That(spec.WithFireInterval(0f).Akimbo, Is.SameAs(AkimboSpec.Default));
            Assert.That(spec.WithAmmo(new AmmoSpec(6, 12, 2f)).Akimbo, Is.SameAs(AkimboSpec.Default));
            Assert.That(spec.WithAmmo(new AmmoSpec(6, 12, 2f)).MagazineSize, Is.EqualTo(6));
        }
    }
}

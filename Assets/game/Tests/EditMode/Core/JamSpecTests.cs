using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class JamSpecTests
    {
        private const string _rifleId = "5d3a1c7e-9b2f-4e6a-8c4d-2f7e1a9b3c50";

        private const string _jammingRifle = @"{
  ""id"": """ + _rifleId + @""",
  ""category"": ""AssaultRifle"",
  ""ammo"": {
    ""magazineSize"": 30,
    ""jam"": {
      ""chancePerShot"": 0.02,
      ""chancePerSustainedShot"": 0.01,
      ""sustainedResetSeconds"": 0.75,
      ""maxChance"": 0.25,
      ""clearSeconds"": 1.5
    }
  }
}";

        private static string Rifle(string ammo)
        {
            return "{\"id\": \"" + _rifleId + "\", \"category\": \"AssaultRifle\", \"ammo\": " + ammo + "}";
        }

        [Test]
        public void DefaultsAreTheDefaultConstants()
        {
            var spec = new JamSpec();

            Assert.That(spec.ChancePerShot, Is.EqualTo(0.01f));
            Assert.That(spec.ChancePerSustainedShot, Is.EqualTo(0.005f));
            Assert.That(spec.SustainedResetSeconds, Is.EqualTo(0.5f));
            Assert.That(spec.MaxChance, Is.EqualTo(0.15f));
            Assert.That(spec.ClearSeconds, Is.EqualTo(1.2f));
            Assert.That(JamSpec.Default.ChancePerShot, Is.EqualTo(JamSpec.DefaultChancePerShot));
        }

        [Test]
        public void EdgeValuesAreAccepted()
        {
            var never = new JamSpec(0f, 0f, 0f, 0f, 0.01f);
            var always = new JamSpec(1f, 1f, 10f, 1f, 5f);

            Assert.That(never.ChancePerShot, Is.EqualTo(0f));
            Assert.That(never.MaxChance, Is.EqualTo(0f));
            Assert.That(never.SustainedResetSeconds, Is.EqualTo(0f));
            Assert.That(always.ChancePerShot, Is.EqualTo(1f));
            Assert.That(always.MaxChance, Is.EqualTo(1f));
        }

        [TestCase(-0.01f, 0.005f, 0.5f, 0.15f, 1.2f, "chancePerShot")]
        [TestCase(1.01f, 0.005f, 0.5f, 1f, 1.2f, "chancePerShot")]
        [TestCase(float.NaN, 0.005f, 0.5f, 0.15f, 1.2f, "chancePerShot")]
        [TestCase(0.01f, -0.01f, 0.5f, 0.15f, 1.2f, "chancePerSustainedShot")]
        [TestCase(0.01f, 1.5f, 0.5f, 0.15f, 1.2f, "chancePerSustainedShot")]
        [TestCase(0.01f, 0.005f, -0.1f, 0.15f, 1.2f, "sustainedResetSeconds")]
        [TestCase(0.01f, 0.005f, float.PositiveInfinity, 0.15f, 1.2f, "sustainedResetSeconds")]
        [TestCase(0.2f, 0.005f, 0.5f, 0.1f, 1.2f, "maxChance")]
        [TestCase(0.01f, 0.005f, 0.5f, 1.1f, 1.2f, "maxChance")]
        [TestCase(0.01f, 0.005f, 0.5f, float.NaN, 1.2f, "maxChance")]
        [TestCase(0.01f, 0.005f, 0.5f, 0.15f, 0f, "clearSeconds")]
        [TestCase(0.01f, 0.005f, 0.5f, 0.15f, -1f, "clearSeconds")]
        [TestCase(0.01f, 0.005f, 0.5f, 0.15f, float.PositiveInfinity, "clearSeconds")]
        public void OutOfRangeValuesAreRejectedByName(
            float chancePerShot, float chancePerSustainedShot, float sustainedResetSeconds, float maxChance, float clearSeconds, string param)
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(
                () => new JamSpec(chancePerShot, chancePerSustainedShot, sustainedResetSeconds, maxChance, clearSeconds));

            Assert.That(e.ParamName, Is.EqualTo(param));
        }

        [Test]
        public void AmmoKeepsTheJamAndHasNoneByDefault()
        {
            var jam = new JamSpec(0.02f);

            Assert.That(new AmmoSpec(jam: jam).Jam, Is.SameAs(jam));
            Assert.That(new AmmoSpec().Jam, Is.Null);
            Assert.That(AmmoSpec.Default.Jam, Is.Null);
        }

        [Test]
        public void HeatAmmoRejectsAJam()
        {
            var e = Assert.Throws<ArgumentException>(
                () => new AmmoSpec(kind: AmmoKind.Heat, heat: new HeatSpec(0.1f, 1f, 0f, 1f), jam: JamSpec.Default));

            Assert.That(e.ParamName, Is.EqualTo("jam"));
        }

        [Test]
        public void TheJamIsReadFromTheAmmo()
        {
            var jam = WeaponSpecJson.Parse(_jammingRifle, "Rifle.json").Ammo.Jam;

            Assert.That(jam, Is.Not.Null);
            Assert.That(jam.ChancePerShot, Is.EqualTo(0.02f));
            Assert.That(jam.ChancePerSustainedShot, Is.EqualTo(0.01f));
            Assert.That(jam.SustainedResetSeconds, Is.EqualTo(0.75f));
            Assert.That(jam.MaxChance, Is.EqualTo(0.25f));
            Assert.That(jam.ClearSeconds, Is.EqualTo(1.5f));
        }

        [Test]
        public void TheJamReadsBackFromWhatIsWritten()
        {
            var written = WeaponSpecJson.Write(WeaponSpecJson.Parse(_jammingRifle, "Rifle.json"));
            var reread = WeaponSpecJson.Parse(written, "again.json");

            Assert.That(written, Does.Contain("\"jam\""));
            Assert.That(reread.Ammo.Jam.ChancePerShot, Is.EqualTo(0.02f));
            Assert.That(reread.Ammo.Jam.ChancePerSustainedShot, Is.EqualTo(0.01f));
            Assert.That(reread.Ammo.Jam.SustainedResetSeconds, Is.EqualTo(0.75f));
            Assert.That(reread.Ammo.Jam.MaxChance, Is.EqualTo(0.25f));
            Assert.That(reread.Ammo.Jam.ClearSeconds, Is.EqualTo(1.5f));
            Assert.That(WeaponSpecJson.Write(reread), Is.EqualTo(written));
        }

        [Test]
        public void AnEmptyJamTakesTheDefaults()
        {
            var jam = WeaponSpecJson.Parse(Rifle("{\"jam\": {}}"), "Rifle.json").Ammo.Jam;

            Assert.That(jam.ChancePerShot, Is.EqualTo(0.01f));
            Assert.That(jam.ChancePerSustainedShot, Is.EqualTo(0.005f));
            Assert.That(jam.SustainedResetSeconds, Is.EqualTo(0.5f));
            Assert.That(jam.MaxChance, Is.EqualTo(0.15f));
            Assert.That(jam.ClearSeconds, Is.EqualTo(1.2f));
        }

        [Test]
        public void AWeaponWithoutAJamHasNoneAndWritesNone()
        {
            var read = WeaponSpecJson.Parse(Rifle("{\"magazineSize\": 30}"), "Rifle.json");

            Assert.That(read.Ammo.Jam, Is.Null);
            Assert.That(WeaponSpecJson.Write(read), Does.Not.Contain("\"jam\""));
            Assert.That(WeaponSpecJson.Parse(WeaponSpecJson.Write(read), "again.json").Ammo.Jam, Is.Null);
        }

        [TestCase("{\"jam\": {\"chancePerShot\": 1.5}}", "ammo.jam.chancePerShot")]
        [TestCase("{\"jam\": {\"chancePerShot\": 0.2, \"maxChance\": 0.1}}", "ammo.jam.maxChance")]
        [TestCase("{\"jam\": {\"clearSeconds\": 0}}", "ammo.jam.clearSeconds")]
        [TestCase("{\"jam\": {\"chance\": 0.1}}", "ammo.jam.chance")]
        [TestCase("{\"jam\": 0.1}", "ammo.jam")]
        [TestCase("{\"kind\": \"Heat\", \"heat\": {\"heatPerShot\": 0.1, \"coolPerSecond\": 1, \"coolDelaySeconds\": 0, \"overheatSeconds\": 1}, \"jam\": {}}", "ammo.jam")]
        public void ProblemsNameTheJamItem(string ammo, string item)
        {
            var e = Assert.Throws<ServerDataJsonException>(() => WeaponSpecJson.Parse(Rifle(ammo), "bad.json"));

            Assert.That(e.Item, Is.EqualTo(item));
            Assert.That(e.Message, Does.Contain("bad.json"));
        }

        [Test]
        public void JamIsAnAmmoItem()
        {
            Assert.That(WeaponSpecJson.AmmoItemNames, Does.Contain("jam"));
        }
    }
}

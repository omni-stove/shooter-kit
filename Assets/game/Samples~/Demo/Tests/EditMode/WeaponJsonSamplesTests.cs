using System.Linq;
using System;
using System.IO;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEditor;

namespace Shooter.Tests.EditMode
{
    public sealed class WeaponJsonSamplesTests
    {
        private string _folder;

        private static string SamplesFolder => DemoSample.Root + "/Weapons/Json";

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/__WeaponJsonSamplesTests_" + Guid.NewGuid().ToString("N");
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(_folder))
            {
                AssetDatabase.DeleteAsset(_folder);
            }
        }

        [Test]
        public void ImportingTheSamplesReproducesTheBuiltInWeapons()
        {
            var imported = WeaponJsonImporter.ImportFrom(SamplesFolder, _folder, null);

            Assert.That(imported.Count(), Is.EqualTo(System.IO.Directory.GetFiles(ShooterKitRoot.PhysicalPath(SamplesFolder), "*.json").Length));
            AssertSameSpec(Load("Launcher").ToSpec(), WeaponAssetBuilder.LauncherSpec());
        }

        [TestCase("ReferenceJson")]
        [TestCase("RealisticJson")]
        public void ImportingTheReferenceWeaponsKeepsWhatEachIsModelledOn(string folder)
        {
            var imported = WeaponJsonImporter.ImportFrom(DemoSample.Root + "/Weapons/" + folder, _folder, null);

            Assert.That(imported.Count, Is.EqualTo(32));
            foreach (var definition in imported)
            {
                Assert.That(definition.Description, Is.Not.Empty, definition.name);
                Assert.That(definition.ToSpec().Description, Is.EqualTo(definition.Description), definition.name);
            }
        }

        [Test]
        public void TheChargeRifleSampleChargesBeforeItFires()
        {
            WeaponJsonImporter.ImportFrom(SamplesFolder, _folder, null);

            var charge = Load("ChargeRifle").ToSpec().Trigger.Charge;
            Assert.That(charge, Is.Not.Null);
            Assert.That(charge.MinChargeFraction, Is.GreaterThan(0f));
            Assert.That(charge.FiresBelowMin, Is.False);
            Assert.That(charge.FullChargeDamageMultiplier, Is.GreaterThan(1f));
        }

        [Test]
        public void TheRicochetPistolBouncesOffShallowSurfaces()
        {
            WeaponJsonImporter.ImportFrom(SamplesFolder, _folder, null);

            var spec = Load("RicochetPistol").ToSpec();

            Assert.That(spec.NameKey, Is.EqualTo("weapon.ricochetPistol"));
            Assert.That(spec.Round.Delivery, Is.EqualTo(RoundDelivery.Hitscan));
            Assert.That(spec.Round.Ricochet, Is.Not.Null);
            Assert.That(spec.Round.Ricochet.MaxBounces, Is.EqualTo(2));
            Assert.That(spec.Round.Ricochet.MaxGrazingAngleDegrees, Is.EqualTo(25f));
            Assert.That(spec.Round.Ricochet.DamageMultiplierPerBounce, Is.EqualTo(0.7f));
        }

        [Test]
        public void TheCommittedRicochetPistolMatchesItsJson()
        {
            var committed = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(DemoSample.Root + "/Weapons/RicochetPistol.asset");
            Assume.That(committed, Is.Not.Null, $"run {nameof(WeaponAssetBuilder)}.Build first");

            WeaponJsonImporter.ImportFrom(SamplesFolder, _folder, null);

            AssertSameFields(Load("RicochetPistol"), committed);
        }

        [Test]
        public void TheMinigunSampleSpinsUpBeforeItFires()
        {
            WeaponJsonImporter.ImportFrom(SamplesFolder, _folder, null);
            var spec = Load("Minigun").ToSpec();
            var spinUp = spec.Trigger.SpinUp;
            Assert.That(spinUp, Is.Not.Null);
            Assert.That(spinUp.StartIntervalMultiplier, Is.GreaterThan(1f), "the first shots are slower than the full rate");

            var trigger = TriggerModels.For(spec);

            Assert.That(trigger.MinShotSpacingSeconds, Is.EqualTo(spec.FireInterval));
            Assert.That(trigger.Press(), Is.EqualTo(0));
            Assert.That(trigger.Tick(spinUp.SpinUpSeconds / 2f), Is.EqualTo(0));
            Assert.That(trigger.Tick(spinUp.SpinUpSeconds), Is.EqualTo(1));
        }

        [TestCase("Shotgun", 0.04f, 0f, 0.5f, 0.04f, 1.5f)]
        [TestCase("SlugCannon", 0.06f, 0f, 0.5f, 0.06f, 1.8f)]
        [TestCase("Minigun", 0.002f, 0.001f, 0.5f, 0.05f, 2.5f)]
        public void ImportingTheJammingSamplesKeepsTheirJam(
            string name, float chancePerShot, float chancePerSustainedShot, float sustainedResetSeconds, float maxChance, float clearSeconds)
        {
            WeaponJsonImporter.ImportFrom(SamplesFolder, _folder, null);

            var definition = Load(name);
            Assert.That(definition.Jam.Enabled, Is.True, name);
            var jam = definition.ToSpec().Ammo.Jam;
            Assert.That(jam, Is.Not.Null, name);
            Assert.That(jam.ChancePerShot, Is.EqualTo(chancePerShot));
            Assert.That(jam.ChancePerSustainedShot, Is.EqualTo(chancePerSustainedShot));
            Assert.That(jam.SustainedResetSeconds, Is.EqualTo(sustainedResetSeconds));
            Assert.That(jam.MaxChance, Is.EqualTo(maxChance));
            Assert.That(jam.ClearSeconds, Is.EqualTo(clearSeconds));
        }

        [TestCase("BurstRifle")]
        [TestCase("Dmr")]
        [TestCase("RicochetPistol")]
        [TestCase("Laser")]
        [TestCase("Flamethrower")]
        public void ImportingTheOtherSamplesLeavesThemWithoutAJam(string name)
        {
            WeaponJsonImporter.ImportFrom(SamplesFolder, _folder, null);

            var definition = Load(name);
            Assert.That(definition.Jam.Enabled, Is.False, name);
            Assert.That(definition.ToSpec().Ammo.Jam, Is.Null, name);
        }

        [TestCase("Shotgun")]
        [TestCase("SlugCannon")]
        [TestCase("Minigun")]
        public void TheCommittedJammingWeaponsMatchTheirJson(string name)
        {
            var committed = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(DemoSample.Root + "/Weapons/" + name + ".asset");
            Assume.That(committed, Is.Not.Null, $"run {nameof(WeaponAssetBuilder)}.Build first");

            WeaponJsonImporter.ImportFrom(SamplesFolder, _folder, null);
            var imported = Load(name).ToSpec().Ammo.Jam;

            Assert.That(committed.Jam.Enabled, Is.True, $"{name}.asset has no jam; rerun {nameof(WeaponAssetBuilder)}.Build");
            var jam = committed.ToSpec().Ammo.Jam;
            Assert.That(jam.ChancePerShot, Is.EqualTo(imported.ChancePerShot), name);
            Assert.That(jam.ChancePerSustainedShot, Is.EqualTo(imported.ChancePerSustainedShot), name);
            Assert.That(jam.SustainedResetSeconds, Is.EqualTo(imported.SustainedResetSeconds), name);
            Assert.That(jam.MaxChance, Is.EqualTo(imported.MaxChance), name);
            Assert.That(jam.ClearSeconds, Is.EqualTo(imported.ClearSeconds), name);
        }

        [Test]
        public void ImportingTheSamplesMatchesTheCommittedAssetFields()
        {
            var committedMachete = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(WeaponAssetBuilder.MachetePath);
            Assume.That(committedMachete, Is.Not.Null, $"run {nameof(WeaponAssetBuilder)}.Build first");

            WeaponJsonImporter.ImportFrom(SamplesFolder, _folder, null);

            AssertSameFields(Load("Machete"), committedMachete);
        }

        private WeaponDefinition Load(string name)
        {
            return AssetDatabase.LoadAssetAtPath<WeaponDefinition>(_folder + "/" + name + ".asset");
        }

        private static void AssertSameSpec(WeaponSpec actual, WeaponSpec expected)
        {
            Assert.That(actual.Id, Is.EqualTo(expected.Id));
            Assert.That(actual.NameKey, Is.EqualTo(expected.NameKey));
            Assert.That(actual.Category, Is.EqualTo(expected.Category));
            Assert.That(actual.Weight, Is.EqualTo(expected.Weight));
            Assert.That(actual.MoveSpeedMultiplier, Is.EqualTo(expected.MoveSpeedMultiplier));
            Assert.That(actual.FireMode, Is.EqualTo(expected.FireMode));
            Assert.That(actual.MagazineSize, Is.EqualTo(expected.MagazineSize));
            Assert.That(actual.ReserveAmmo, Is.EqualTo(expected.ReserveAmmo));
            Assert.That(actual.FireInterval, Is.EqualTo(expected.FireInterval));
            Assert.That(actual.ReloadSeconds, Is.EqualTo(expected.ReloadSeconds));
            Assert.That(actual.Damage, Is.EqualTo(expected.Damage));
            Assert.That(actual.Range, Is.EqualTo(expected.Range));
            Assert.That(actual.Falloff.StartDistance, Is.EqualTo(expected.Falloff.StartDistance));
            Assert.That(actual.Falloff.EndDistance, Is.EqualTo(expected.Falloff.EndDistance));
            Assert.That(actual.Falloff.MinMultiplier, Is.EqualTo(expected.Falloff.MinMultiplier));
            Assert.That(actual.ExplosionRadius, Is.EqualTo(expected.ExplosionRadius));
            Assert.That(actual.AdsFieldOfView, Is.EqualTo(expected.AdsFieldOfView));
            Assert.That(actual.BlindFireSpreadDegrees, Is.EqualTo(expected.BlindFireSpreadDegrees));
            Assert.That(actual.ActiveReloadWindowStart, Is.EqualTo(expected.ActiveReloadWindowStart));
            Assert.That(actual.ActiveReloadWindowLength, Is.EqualTo(expected.ActiveReloadWindowLength));
            Assert.That(actual.ActiveReloadFailSeconds, Is.EqualTo(expected.ActiveReloadFailSeconds));
            Assert.That(actual.RecoilPitchDegrees, Is.EqualTo(expected.RecoilPitchDegrees));
            Assert.That(actual.RecoilYawDegrees, Is.EqualTo(expected.RecoilYawDegrees));
            Assert.That(actual.RecoilRecoveryPerSecond, Is.EqualTo(expected.RecoilRecoveryPerSecond));
            Assert.That(actual.HipSpreadDegrees, Is.EqualTo(expected.HipSpreadDegrees));
            Assert.That(actual.AdsSpreadDegrees, Is.EqualTo(expected.AdsSpreadDegrees));
            Assert.That(actual.MoveSpreadDegrees, Is.EqualTo(expected.MoveSpreadDegrees));
            Assert.That(actual.AdsMoveSpreadDegrees, Is.EqualTo(expected.AdsMoveSpreadDegrees));
            Assert.That(actual.RecoilRecoveryDelaySeconds, Is.EqualTo(expected.RecoilRecoveryDelaySeconds));
        }

        private static void AssertSameFields(WeaponDefinition actual, WeaponDefinition expected)
        {
            Assert.That(EditorJsonUtility.ToJson(actual), Is.EqualTo(EditorJsonUtility.ToJson(expected)), expected.name);
        }
    }
}

using System;
using System.IO;
using Shooter.Core;
using Shooter.Editor;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    public sealed class JamSmokeRunTests
    {
        private const string _weaponId = "07e1b9a8-f64c-4474-bc38-dbf14d66b135";

        [Test]
        public void ReportsUnderItsOwnTagAndMenu()
        {
            Assert.That(JamSmokeRun.LogTag, Is.EqualTo("[JamSmokeRun]"));
            Assert.That(JamSmokeRun.MenuPath, Is.EqualTo("Shooter/Smoke/Run Jam Smoke"));
            Assert.That(JamSmokeRun.WeaponJson, Is.EqualTo("Shotgun.json"));
        }

        [Test]
        public void JamsOnTheThirdShotAndNotOnTheShotAfterClearing()
        {
            Assert.That(JamSmokeRun.Rolls, Is.EqualTo(new[] { 0.99f, 0.99f, 0f, 0.99f }));
            Assert.That(JamSmokeRun.JamShot, Is.EqualTo(3));
            Assert.That(JamSmokeRun.Rolls[JamSmokeRun.Rolls.Count - 1], Is.Not.Zero, "the last roll repeats, so it must not jam");
        }

        [Test]
        public void OnlyTheZeroRollJamsTheSmokeWeapon()
        {
            var path = ShooterKitRoot.PhysicalPath(WeaponAssetBuilder.JsonFolder + "/" + JamSmokeRun.WeaponJson);
            var spec = WeaponJsonImporter.ParseSpec(File.ReadAllText(path), JamSmokeRun.WeaponJson);

            Assert.That(spec.Id, Is.EqualTo(_weaponId));
            Assert.That(spec.Ammo.Jam, Is.Not.Null);
            for (var i = 0; i < JamSmokeRun.Rolls.Count; i++)
            {
                var roll = JamSmokeRun.Rolls[i];
                Assert.That(roll < spec.Ammo.Jam.MaxChance, Is.EqualTo(i == JamSmokeRun.JamShot - 1), $"roll {i + 1} = {roll}");
            }
        }

        [Test]
        public void IndexOfJamFindsTheFirstZeroAndRefusesRollsWithoutOne()
        {
            Assert.That(JamSmokeRun.IndexOfJam(new[] { 0f }), Is.EqualTo(0));
            Assert.That(JamSmokeRun.IndexOfJam(new[] { 0.5f, 0f, 0f }), Is.EqualTo(1));
            Assert.Throws<ArgumentException>(() => JamSmokeRun.IndexOfJam(new[] { 0.5f, 0.9f }));
            Assert.Throws<ArgumentNullException>(() => JamSmokeRun.IndexOfJam(null));
        }

        [Test]
        public void ExpectsTheJammedClearStartedAndClearedLinesInOrder()
        {
            var lines = JamSmokeRun.ExpectedLines(_weaponId, 0, 0.04f, 1.5f);

            Assert.That(lines, Is.EqualTo(new[]
            {
                "[Jam] weapon=" + _weaponId + " jammed sustained=0 chance=0.040",
                "[Jam] weapon=" + _weaponId + " clear started seconds=1.50",
                "[Jam] weapon=" + _weaponId + " cleared",
            }));
        }

        [Test]
        public void AcceptsTheExpectedLinesAmongOtherLogLines()
        {
            var expected = JamSmokeRun.ExpectedLines(_weaponId, 0, 0.04f, 1.5f);
            var seen = new[] { "[Combat] shot", expected[0], "[Combat] dry", expected[1], expected[2], "[Combat] shot" };

            Assert.That(JamSmokeRun.CheckLines(seen, expected), Is.Null);
        }

        [Test]
        public void RefusesMissingExtraOrReorderedLines()
        {
            var expected = JamSmokeRun.ExpectedLines(_weaponId, 0, 0.04f, 1.5f);

            Assert.That(
                JamSmokeRun.CheckLines(new[] { expected[0], expected[1] }, expected),
                Does.StartWith("expected 3 [Jam] line(s) but saw 2"));
            Assert.That(
                JamSmokeRun.CheckLines(new[] { expected[0], expected[1], expected[2], expected[0] }, expected),
                Does.StartWith("expected 3 [Jam] line(s) but saw 4"));
            Assert.That(
                JamSmokeRun.CheckLines(new[] { expected[1], expected[0], expected[2] }, expected),
                Is.EqualTo($"[Jam] line 1 was '{expected[1]}', expected '{expected[0]}'"));
            Assert.That(
                JamSmokeRun.CheckLines(new[] { expected[0], WeaponJamLog.ClearCanceled(_weaponId, 1f), expected[2] }, expected),
                Does.StartWith("[Jam] line 2 was "));
            Assert.Throws<ArgumentNullException>(() => JamSmokeRun.CheckLines(null, expected));
            Assert.Throws<ArgumentNullException>(() => JamSmokeRun.CheckLines(expected, null));
        }

        [Test]
        public void TheSequenceRollRepeatsItsLastValue()
        {
            var roll = new JamSmokeRun.SequenceRoll(new[] { 0.5f, 0f, 0.9f });

            Assert.That(roll.Draw(), Is.EqualTo(0.5f));
            Assert.That(roll.Draw(), Is.EqualTo(0f));
            Assert.That(roll.Draw(), Is.EqualTo(0.9f));
            Assert.That(roll.Draw(), Is.EqualTo(0.9f));
            Assert.That(roll.Calls, Is.EqualTo(4));
        }

        [Test]
        public void TheSequenceRollRefusesValuesOutsideTheRollRange()
        {
            Assert.Throws<ArgumentException>(() => new JamSmokeRun.SequenceRoll(Array.Empty<float>()));
            Assert.Throws<ArgumentException>(() => new JamSmokeRun.SequenceRoll(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new JamSmokeRun.SequenceRoll(new[] { 1f }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new JamSmokeRun.SequenceRoll(new[] { -0.1f }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new JamSmokeRun.SequenceRoll(new[] { float.NaN }));
        }
    }
}

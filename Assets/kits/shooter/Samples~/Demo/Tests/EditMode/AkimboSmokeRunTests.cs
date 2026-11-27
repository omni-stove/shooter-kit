using System;
using Shooter.Editor;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    public sealed class AkimboSmokeRunTests
    {
        [Test]
        public void ReportsUnderItsOwnTagAndMenuAndOpensThePlayground()
        {
            Assert.That(AkimboSmokeRun.LogTag, Is.EqualTo("[AkimboSmokeRun]"));
            Assert.That(AkimboSmokeRun.MenuPath, Is.EqualTo("Shooter/Smoke/Run Akimbo Smoke"));
            Assert.That(AkimboSmokeRun.ScenePath, Is.EqualTo(BuildSceneList.PlaygroundScene));
        }

        [Test]
        public void ExpectsARightThenALeftShotAndThenBothHandsReloading()
        {
            Assert.That(
                AkimboSmokeRun.ExpectedLines(7, 6),
                Is.EqualTo(new[]
                {
                    "[Combat] akimbo fire hand=right ammo=7",
                    "[Combat] akimbo fire hand=left ammo=6",
                    "[Combat] akimbo reload hand=right",
                    "[Combat] akimbo reload hand=left",
                }));
        }

        [Test]
        public void AcceptsTheExpectedLinesWithOtherCombatLinesBetweenThem()
        {
            var expected = AkimboSmokeRun.ExpectedLines(7, 7);
            var seen = new[]
            {
                "[Combat] weapon added index=2 id=c5171e80-7b9c-44f0-bdd5-3e4fdd67f585 name=weapon.real.pistol45 attachments=none",
                expected[0],
                expected[1],
                "[Combat] reserve ammo=48",
                expected[2],
                expected[3],
            };

            Assert.That(AkimboSmokeRun.CheckLines(seen, expected), Is.Null);
        }

        [Test]
        public void RejectsAMissingOrOutOfOrderLine()
        {
            var expected = AkimboSmokeRun.ExpectedLines(7, 7);

            Assert.That(
                AkimboSmokeRun.CheckLines(new[] { expected[0], expected[2], expected[3] }, expected),
                Does.StartWith("never saw '[Combat] akimbo fire hand=left ammo=7' after 1 of the 4"));
            Assert.That(
                AkimboSmokeRun.CheckLines(new[] { expected[1], expected[0], expected[2], expected[3] }, expected),
                Does.StartWith("never saw '[Combat] akimbo fire hand=left ammo=7' after 1 of the 4"));
            Assert.That(
                AkimboSmokeRun.CheckLines(new[] { expected[0], expected[1], expected[2] }, expected),
                Does.StartWith("never saw '[Combat] akimbo reload hand=left' after 3 of the 4"));
            Assert.That(
                AkimboSmokeRun.CheckLines(new[] { expected[0], "[Combat] akimbo fire hand=left ammo=6", expected[2], expected[3] }, expected),
                Does.StartWith("never saw '[Combat] akimbo fire hand=left ammo=7'"));
            Assert.That(AkimboSmokeRun.CheckLines(Array.Empty<string>(), expected), Does.StartWith("never saw '[Combat] akimbo fire hand=right ammo=7' after 0 of the 4"));
            Assert.Throws<ArgumentNullException>(() => AkimboSmokeRun.CheckLines(null, expected));
            Assert.Throws<ArgumentNullException>(() => AkimboSmokeRun.CheckLines(expected, null));
        }
    }
}

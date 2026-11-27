using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class JamRollTests
    {
        [Test]
        public void SeedZeroFollowsTheMinstdSequence()
        {
            var roll = new JamRoll(0);

            // 状態 1 → 48271 → 48271^2 mod (2^31 - 1) = 182605794。値は (状態 - 1) の上位 24 ビット / 2^24。
            Assert.That(roll.Draw(), Is.EqualTo(377f / 16777216f));
            Assert.That(roll.Draw(), Is.EqualTo(1426607f / 16777216f));
        }

        [Test]
        public void SeedsThatFoldToTheSameStateGiveTheSameSequence()
        {
            var a = new JamRoll(0);
            var b = new JamRoll(2147483646);

            for (var i = 0; i < 5; i++)
            {
                Assert.That(b.Draw(), Is.EqualTo(a.Draw()), "draw " + i);
            }
        }

        [Test]
        public void ValuesStayInTheHalfOpenUnitRange()
        {
            foreach (var seed in new[] { 0, 1, -1, int.MinValue, int.MaxValue, 12345 })
            {
                var roll = new JamRoll(seed);
                for (var i = 0; i < 10000; i++)
                {
                    var value = roll.Draw();
                    Assert.That(value >= 0f && value < 1f, Is.True, "seed " + seed + " draw " + i + " gave " + value);
                }
            }
        }

        [Test]
        public void TheSameSeedGivesTheSameSequence()
        {
            var a = new JamRoll(987654321);
            var b = new JamRoll(987654321);

            for (var i = 0; i < 20; i++)
            {
                Assert.That(b.Draw(), Is.EqualTo(a.Draw()), "draw " + i);
            }
        }

        [Test]
        public void TheSeedForAnIdIsFnv1a()
        {
            // FNV-1a (32 ビット) の "a" の値は 0xE40C292C。
            Assert.That(JamRoll.SeedFor("a"), Is.EqualTo(unchecked((int)0xE40C292Cu)));
            Assert.That(
                JamRoll.SeedFor("00000000-0000-4000-8000-000000000001"),
                Is.EqualTo(JamRoll.SeedFor("00000000-0000-4000-8000-000000000001")));
            Assert.That(
                JamRoll.SeedFor("00000000-0000-4000-8000-000000000001"),
                Is.Not.EqualTo(JamRoll.SeedFor("00000000-0000-4000-8000-000000000002")));
            Assert.Throws<ArgumentException>(() => JamRoll.SeedFor(null));
            Assert.Throws<ArgumentException>(() => JamRoll.SeedFor(string.Empty));
        }

        [Test]
        public void ForWeaponSeedsFromTheId()
        {
            const string id = "00000000-0000-4000-8000-000000000001";
            var fromId = JamRoll.ForWeapon(id);
            var fromSeed = new JamRoll(JamRoll.SeedFor(id));

            for (var i = 0; i < 5; i++)
            {
                Assert.That(fromId.Draw(), Is.EqualTo(fromSeed.Draw()), "draw " + i);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    public sealed class VipHudFormatterTests
    {
        private const int _vipId = 3;

        private static readonly string[] _expectedLocalVip = { "あなたが VIP: 脱出地点へ向かう", "ラウンド 1 - 2" };
        private static readonly string[] _expectedDefender = { "VIP を脱出させよ", "ラウンド 1 - 2" };
        private static readonly string[] _expectedAttacker = { "VIP を倒せ", "ラウンド 2 - 1" };
        private static readonly string[] _expectedDefenderEscaping = { "VIP を脱出させよ", "VIP 脱出中 42%", "ラウンド 1 - 2" };
        private static readonly string[] _expectedAttackerEscaping = { "VIP を倒せ", "VIP 脱出中 42%", "ラウンド 2 - 1" };
        private static readonly string[] _expectedWaitingDefender = { "VIP を脱出させよ", "VIP の選出待ち", "ラウンド 1 - 2" };
        private static readonly string[] _expectedBotsOnly = { "VIP 脱出中 42%" };
        private static readonly string[] _expectedScores = { "BRAVO 2", "ALPHA 1" };

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
        }

        [TearDown]
        public void TearDown()
        {
            GameSignals.ResetAll();
        }

        [Test]
        public void TheVipStatusIsFormattedByTheVipFormatter()
        {
            var status = Status(new VipStatus(_vipId, 0f, Side.Alpha, false), Side.Alpha);

            Assert.That(ModeHudFormatters.For(status), Is.InstanceOf<VipHudFormatter>());
        }

        [Test]
        public void TheDefendersAndTheAttackersAreGivenDifferentObjectives()
        {
            var defender = Status(new VipStatus(_vipId, 0f, Side.Alpha, false), Side.Alpha);
            var attacker = Status(new VipStatus(_vipId, 0f, Side.Alpha, false), Side.Bravo);

            Assert.That(Lines(defender), Is.EqualTo(_expectedDefender));
            Assert.That(Lines(attacker), Is.EqualTo(_expectedAttacker));
            Assert.That(_expectedDefender[0], Is.Not.EqualTo(_expectedAttacker[0]));
        }

        [Test]
        public void TheLocalVipIsToldTheyAreTheVipInsteadOfTheEscortObjective()
        {
            var status = Status(new VipStatus(_vipId, 0f, Side.Alpha, true), Side.Alpha);

            Assert.That(Lines(status), Is.EqualTo(_expectedLocalVip));
        }

        [Test]
        public void BothSidesSeeHowFarTheVipHasEscaped()
        {
            var detail = new VipStatus(_vipId, 0.425f, Side.Alpha, false);

            Assert.That(Lines(Status(detail, Side.Alpha)), Is.EqualTo(_expectedDefenderEscaping));
            Assert.That(Lines(Status(detail, Side.Bravo)), Is.EqualTo(_expectedAttackerEscaping));
        }

        [Test]
        public void WithNoVipChosenTheHudSaysItIsWaiting()
        {
            var status = Status(new VipStatus(null, 0.5f, Side.Alpha, false), Side.Alpha);

            Assert.That(Lines(status), Is.EqualTo(_expectedWaitingDefender));
        }

        [Test]
        public void WithNoLocalPlayerOnlyTheEscapeIsShown()
        {
            var status = new ModeStatus(60f, null, null, null, false, null, new VipStatus(_vipId, 0.425f, Side.Alpha, null));

            Assert.That(Lines(status), Is.EqualTo(_expectedBotsOnly));
            Assert.That(VipHudFormatter.RoundLine(status), Is.Null);
            Assert.Throws<ArgumentNullException>(() => VipHudFormatter.HudLines(null, null));
            Assert.Throws<ArgumentNullException>(() => VipHudFormatter.RoundLine(null));
        }

        [Test]
        public void TheEscapePercentReachesOneHundredOnlyWhenTheVipHasEscaped()
        {
            Assert.That(VipHudFormatter.ExitPercent(0f), Is.EqualTo(0));
            Assert.That(VipHudFormatter.ExitPercent(float.NaN), Is.EqualTo(0));
            Assert.That(VipHudFormatter.ExitPercent(0.425f), Is.EqualTo(42));
            Assert.That(VipHudFormatter.ExitPercent(0.999f), Is.EqualTo(99));
            Assert.That(VipHudFormatter.ExitPercent(1f), Is.EqualTo(100));
        }

        [Test]
        public void TheRoundWinsUseTheSharedModeFormat()
        {
            var status = Status(new VipStatus(_vipId, 0f, Side.Alpha, false), Side.Bravo);
            var formatter = ModeHudFormatters.For(status);

            var scores = formatter.Scores(status);

            Assert.That(scores.Select(line => Localizer.Resolve(line.Text)), Is.EqualTo(_expectedScores));
            Assert.That(scores[0].IsOwn, Is.True);
            Assert.That(scores[0].IsLeading, Is.True);
            Assert.That(Localizer.Resolve(formatter.Time(status)), Is.EqualTo("01:30"));
        }

        [Test]
        public void TheResultNamesTheWinningSide()
        {
            var formatter = new VipHudFormatter();
            var detail = new VipStatus(_vipId, 0f, Side.Alpha, false);

            Assert.That(Localizer.Resolve(formatter.Result(new ModeStatus(0f, null, null, null, true, Side.Bravo, detail))), Is.EqualTo("BRAVO の勝ち"));
            Assert.That(formatter.Result(new ModeStatus(10f, null, null, null, false, null, detail)), Is.EqualTo(default(LocalizedText)));
        }

        // ALPHA が 1 勝、BRAVO が 2 勝した 3 ラウンド目の様子。
        private static ModeStatus Status(VipStatus detail, Side ownSide)
        {
            var scores = new[] { new SideScore(Side.Alpha, 1d), new SideScore(Side.Bravo, 2d) };
            return new ModeStatus(90f, scores, ownSide, null, false, null, detail);
        }

        private static IEnumerable<string> Lines(ModeStatus status)
        {
            var lines = new List<LocalizedText>();
            ModeHudFormatters.For(status).AppendLines(status, lines);
            return lines.Select(Localizer.Resolve);
        }
    }
}

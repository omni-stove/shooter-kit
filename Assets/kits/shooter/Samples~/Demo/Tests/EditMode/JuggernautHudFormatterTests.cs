using System;
using System.Collections.Generic;
using System.Linq;
using GameKit.Core;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    public sealed class JuggernautHudFormatterTests
    {
        private const int _localId = 0;
        private const int _otherId = 4;

        private static readonly string[] _expectedBeforeChosen = { "ジャガーノート選出まで 3 秒" };
        private static readonly string[] _expectedWaiting = { "ジャガーノートの選出待ち" };
        private static readonly string[] _expectedLocalJuggernaut = { "リスポーンまで 2 秒", "あなたがジャガーノート: キルを重ねて点を稼ぐ", "自分の点 7" };
        private static readonly string[] _expectedOtherJuggernaut = { "ジャガーノート: Ann", "ジャガーノートを倒すと次のジャガーノートになる", "自分の点 3" };
        private static readonly string[] _expectedUnlistedJuggernaut = { "ジャガーノート: Player 4", "ジャガーノートを倒すと次のジャガーノートになる" };
        private static readonly string[] _expectedBotsOnly = { "ジャガーノート: Player 4" };
        private static readonly string[] _expectedScores = { "ALPHA 7", "BRAVO 3" };

        private static readonly Side _localSide = new Side(0);
        private static readonly Side _otherSide = new Side(1);

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
        public void TheCountdownShowsUntilTheJuggernautIsChosen()
        {
            var lines = JuggernautHudFormatter.HudLines(new JuggernautStatus(null, 2.2f, false), null);

            Assert.That(lines.Select(Localizer.Resolve), Is.EqualTo(_expectedBeforeChosen));
            Assert.Throws<ArgumentNullException>(() => JuggernautHudFormatter.HudLines(null, null));
        }

        [Test]
        public void WithNoOneToChooseTheHudSaysItIsWaitingInsteadOfZeroSeconds()
        {
            var lines = JuggernautHudFormatter.HudLines(new JuggernautStatus(null, 0f, null), null);

            Assert.That(lines.Select(Localizer.Resolve), Is.EqualTo(_expectedWaiting));
        }

        [Test]
        public void TheLocalJuggernautIsToldTheyAreTheJuggernaut()
        {
            var status = Status(new JuggernautStatus(_localId, 0f, true), _localSide, 1.5f, 7d, 3d);

            Assert.That(ModeHudFormatters.For(status), Is.InstanceOf<JuggernautHudFormatter>());
            Assert.That(Lines(status), Is.EqualTo(_expectedLocalJuggernaut));
        }

        [Test]
        public void OtherPlayersSeeTheJuggernautsScoreboardName()
        {
            GameSignals.Scoreboard.Publish(new[]
            {
                new ScoreboardEntry(_localId, ScoreboardName.LocalPlayer, Team.Alpha, 0, 0, 0, ScoreboardConnection.None, true),
                new ScoreboardEntry(_otherId, ScoreboardName.Literal("Ann"), Team.Alpha, 0, 0, 0, ScoreboardConnection.None, false),
            });

            var status = Status(new JuggernautStatus(_otherId, 0f, false), _otherSide, null, 7d, 3d);

            Assert.That(Lines(status), Is.EqualTo(_expectedOtherJuggernaut));
        }

        [Test]
        public void AJuggernautMissingFromTheScoreboardIsNamedByTheirNumber()
        {
            var unlisted = new ModeStatus(60f, null, null, null, false, null, new JuggernautStatus(_otherId, 0f, false));
            var botsOnly = new ModeStatus(60f, null, null, null, false, null, new JuggernautStatus(_otherId, 0f, null));

            Assert.That(Lines(unlisted), Is.EqualTo(_expectedUnlistedJuggernaut));
            Assert.That(Lines(botsOnly), Is.EqualTo(_expectedBotsOnly));
            Assert.That(
                Localizer.Resolve(JuggernautHudFormatter.PlayerName(_otherId, new[] { Entry(_otherId, ScoreboardName.Literal(string.Empty)) })),
                Is.EqualTo("Player 4"),
                "an empty scoreboard name falls back to the number");
        }

        [Test]
        public void TheScoresAndTimeUseTheSharedModeFormat()
        {
            var status = Status(new JuggernautStatus(_localId, 0f, true), _localSide, null, 7d, 3d);
            var formatter = ModeHudFormatters.For(status);

            var scores = formatter.Scores(status);

            Assert.That(scores.Select(line => Localizer.Resolve(line.Text)), Is.EqualTo(_expectedScores));
            Assert.That(scores[0].IsOwn, Is.True);
            Assert.That(scores[0].IsLeading, Is.True);
            Assert.That(Localizer.Resolve(formatter.Time(status)), Is.EqualTo("02:00"));
            Assert.That(JuggernautHudFormatter.OwnScore(status), Is.EqualTo(7));
            Assert.That(JuggernautHudFormatter.OwnScore(new ModeStatus(60f, null, null, null, false, null, null)), Is.Null);
            Assert.Throws<ArgumentNullException>(() => JuggernautHudFormatter.OwnScore(null));
        }

        [Test]
        public void TheResultNamesTheWinningSide()
        {
            var formatter = new JuggernautHudFormatter();
            var detail = new JuggernautStatus(_localId, 0f, true);

            Assert.That(Localizer.Resolve(formatter.Result(new ModeStatus(0f, null, null, null, true, _otherSide, detail))), Is.EqualTo("BRAVO の勝ち"));
            Assert.That(Localizer.Resolve(formatter.Result(new ModeStatus(0f, null, null, null, true, null, detail))), Is.EqualTo("引き分け"));
            Assert.That(formatter.Result(new ModeStatus(10f, null, null, null, false, null, detail)), Is.EqualTo(default(LocalizedText)));
        }

        private static ModeStatus Status(JuggernautStatus detail, Side ownSide, float? respawnSeconds, double localScore, double otherScore)
        {
            var scores = new[] { new SideScore(_localSide, localScore), new SideScore(_otherSide, otherScore) };
            return new ModeStatus(120f, scores, ownSide, respawnSeconds, false, null, detail);
        }

        private static ScoreboardEntry Entry(int playerId, ScoreboardName name)
        {
            return new ScoreboardEntry(playerId, name, Team.Alpha, 0, 0, 0, ScoreboardConnection.None, false);
        }

        private static IEnumerable<string> Lines(ModeStatus status)
        {
            var lines = new List<LocalizedText>();
            ModeHudFormatters.For(status).AppendLines(status, lines);
            return lines.Select(Localizer.Resolve);
        }
    }
}

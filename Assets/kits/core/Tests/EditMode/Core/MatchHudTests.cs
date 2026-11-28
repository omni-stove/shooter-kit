using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace GameKit.Core.Tests
{
    // Unity の EditMode テストと dotnet/GameKit.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class MatchHudTests
    {
        private static readonly Type[] _movedWithTheHud =
        {
            typeof(MatchHud),
            typeof(MatchHudField),
            typeof(HudNotice),
            typeof(HudNoticeKind),
            typeof(IHudNoticeSource),
            typeof(MatchLog),
            typeof(HeldPlayer),
            typeof(PingBoard),
            typeof(PingLog),
            typeof(PingMarker),
            typeof(PingPlacement),
            typeof(SharedPing),
            typeof(SharedPingChange),
            typeof(SharedPingChangeKind),
            typeof(SharedPingMarkers),
            typeof(TeamPingRelay),
        };

        private MatchHud _hud;
        private List<MatchHudField> _changes;

        [SetUp]
        public void SetUp()
        {
            _hud = new MatchHud();
            _changes = new List<MatchHudField>();
            _hud.Changed += _changes.Add;
        }

        [Test]
        public void TheGenreNeutralHudLogAndPingsLiveInTheGameKitCore()
        {
            foreach (var type in _movedWithTheHud)
            {
                Assert.That(type.Assembly.GetName().Name, Is.EqualTo("GameKit.Core"), type.Name);
                Assert.That(type.Namespace, Is.EqualTo("GameKit.Core"), type.Name);
                Assert.That(type.IsPublic, Is.True, type.Name);
            }
        }

        [Test]
        public void StartsWithNothingShown()
        {
            Assert.That(_hud.CurrentHealth, Is.Null);
            Assert.That(_hud.MaxHealth, Is.Null);
            Assert.That(_hud.HealthFraction, Is.Null);
            Assert.That(_hud.IsHealthLow, Is.False);
            Assert.That(_hud.HasTime, Is.False);
            Assert.That(_hud.Notice.IsNone, Is.True);
            Assert.That(_hud.FirstScoreTeam, Is.Null);
            Assert.That(_hud.SecondScoreTeam, Is.Null);
            Assert.That(_hud.HasResult, Is.False);
            Assert.That(_hud.SoloResult, Is.Null);
            Assert.That(_hud.RestartKey, Is.Empty);
        }

        [Test]
        public void BoundHealthFollowsDamageAndNotifiesOncePerChange()
        {
            var health = new Health(100);

            _hud.BindHealth(health);
            health.TakeDamage(80);
            health.TakeDamage(0);

            Assert.That(_hud.CurrentHealth, Is.EqualTo(20));
            Assert.That(_hud.MaxHealth, Is.EqualTo(100));
            Assert.That(_hud.HealthFraction, Is.EqualTo(0.2f));
            Assert.That(_hud.IsHealthLow, Is.True);
            Assert.That(_changes, Is.EqualTo(new[] { MatchHudField.Health, MatchHudField.Health }));
        }

        [Test]
        public void BoundMatchShowsTheTimeAndTheSoloResult()
        {
            var match = new Match(5f, 1, 0);

            _hud.BindMatch(match);
            match.Start();
            match.Tick(1.5f);
            match.ReportKill(0, 1);

            Assert.That(_hud.RemainingSeconds, Is.EqualTo(4));
            Assert.That(_hud.SoloResult, Is.EqualTo(MatchOutcome.Won));
            Assert.That(_changes, Does.Contain(MatchHudField.Time));
            Assert.That(_changes, Does.Contain(MatchHudField.Result));
        }

        [Test]
        public void NoticeScoresAndResultNotifyTheirOwnField()
        {
            _hud.SetRespawnNotice(RespawnMode.Wave, 3);
            _hud.SetScores(Team.Bravo, 2, 5);
            _hud.SetResult(Team.Bravo);

            Assert.That(_hud.Notice, Is.EqualTo(HudNotice.Respawn(3)));
            Assert.That(_hud.FirstScoreTeam, Is.EqualTo(Team.Bravo));
            Assert.That(_hud.FirstScore, Is.EqualTo(5));
            Assert.That(_hud.SecondScoreTeam, Is.EqualTo(Team.Alpha));
            Assert.That(_hud.SecondScore, Is.EqualTo(2));
            Assert.That(_hud.Winner, Is.EqualTo(Team.Bravo));
            Assert.That(_changes, Is.EqualTo(new[] { MatchHudField.Notice, MatchHudField.Score, MatchHudField.Result }));
        }

        [Test]
        public void UnbindAllKeepsTheValuesButStopsFollowing()
        {
            var health = new Health(100);
            _hud.BindHealth(health);
            _changes.Clear();

            _hud.UnbindAll();
            health.TakeDamage(10);

            Assert.That(_hud.CurrentHealth, Is.EqualTo(100));
            Assert.That(_changes, Is.Empty);
        }

        [Test]
        public void RejectsBadValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _hud.SetHealth(-1, 100));
            Assert.Throws<ArgumentOutOfRangeException>(() => _hud.SetHealth(1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => _hud.SetScores(Team.Alpha, -1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => _hud.SetScores(Team.Alpha, 0, -1));
        }
    }
}

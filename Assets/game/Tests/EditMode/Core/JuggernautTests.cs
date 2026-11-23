using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class JuggernautTests
    {
        private const float _startDelaySeconds = 5f;
        private const float _timeLimitSeconds = 60f;
        private const float _respawnSeconds = 2f;
        private const int _scorePerKillAsJuggernaut = 1;
        private const int _scorePerJuggernautKill = 3;
        private const int _scoreLimit = 5;

        [Test]
        public void NobodyIsTheJuggernautUntilTheStartDelayEnds()
        {
            var juggernaut = Started(3, PickFirst);
            var changes = Record(juggernaut);
            var session = juggernaut.Session;

            session.Tick(_startDelaySeconds - 0.5f);
            session.ReportKill(1, 0);

            Assert.That(juggernaut.HasJuggernaut, Is.False);
            Assert.That(juggernaut.JuggernautId, Is.Null);
            Assert.That(juggernaut.SecondsUntilChosen, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(juggernaut.ReportFall(0, 1), Is.False, "nobody takes over before the first juggernaut is chosen");
            Assert.That(juggernaut.GetScore(1), Is.Zero, "kills before the first juggernaut score nothing");
            Assert.That(changes, Is.Empty);
        }

        [Test]
        public void TheStartDelayChoosesTheJuggernautFromThePicker()
        {
            var first = Started(3, PickFirst);
            var last = Started(3, PickLast);
            var changes = Record(last);

            first.Session.Tick(_startDelaySeconds);
            last.Session.Tick(_startDelaySeconds);

            Assert.That(first.JuggernautId, Is.EqualTo((int?)0));
            Assert.That(last.JuggernautId, Is.EqualTo((int?)2));
            Assert.That(last.IsJuggernaut(2), Is.True);
            Assert.That(last.IsJuggernaut(0), Is.False);
            Assert.That(last.SecondsUntilChosen, Is.Zero);
            Assert.That(changes.Count, Is.EqualTo(1));
            Assert.That(changes[0].JuggernautId, Is.EqualTo(2));
            Assert.That(changes[0].PreviousId, Is.Null);
            Assert.That(changes[0].Reason, Is.EqualTo(JuggernautChangeReason.Chosen));
        }

        [Test]
        public void ManySmallTicksReachTheChoice()
        {
            var juggernaut = Started(2, PickFirst);

            for (var i = 0; i < 50; i++)
            {
                juggernaut.Session.Tick(_startDelaySeconds / 50f);
            }

            Assert.That(juggernaut.JuggernautId, Is.EqualTo((int?)0));
        }

        [Test]
        public void WithoutAPickerTheJuggernautIsOneOfThePlayers()
        {
            var juggernaut = new Juggernaut(Spec(), _respawnSeconds);
            for (var id = 0; id < 4; id++)
            {
                juggernaut.Session.Join(id);
            }

            juggernaut.Session.Start();
            juggernaut.Session.Tick(_startDelaySeconds);

            Assert.That(juggernaut.JuggernautId.HasValue, Is.True);
            Assert.That(juggernaut.JuggernautId.Value, Is.InRange(0, 3));
        }

        [Test]
        public void APickerOutsideThePlayersIsRejected()
        {
            var juggernaut = Started(3, count => count);

            Assert.Throws<InvalidOperationException>(() => juggernaut.Session.Tick(_startDelaySeconds));
        }

        [Test]
        public void APlayerWhoJoinsAnEmptyMatchIsChosenOnTheNextTick()
        {
            var juggernaut = new Juggernaut(Spec(), _respawnSeconds, PickFirst);
            var changes = Record(juggernaut);
            juggernaut.Session.Start();
            juggernaut.Session.Tick(_startDelaySeconds);
            Assert.That(juggernaut.HasJuggernaut, Is.False, "nobody is there to choose");

            juggernaut.Session.Join(7);
            juggernaut.Session.Tick(0.1f);

            Assert.That(juggernaut.JuggernautId, Is.EqualTo((int?)7));
            Assert.That(changes.Count, Is.EqualTo(1));
            Assert.That(changes[0].Reason, Is.EqualTo(JuggernautChangeReason.Chosen));
        }

        // 確認方法の手順: A (0 番) がジャガーノートの B (1 番) を倒すと、ジャガーノートが A に替わり、A に倒した点が入る。
        [Test]
        public void KillingTheJuggernautHandsItToTheKillerAndScoresForThem()
        {
            const int a = 0;
            const int b = 1;
            var juggernaut = Started(2, PickLast);
            var changes = Record(juggernaut);
            var session = juggernaut.Session;
            session.Tick(_startDelaySeconds);
            Assert.That(juggernaut.JuggernautId, Is.EqualTo((int?)b));

            session.ReportKill(a, b);
            Assert.That(juggernaut.ReportFall(b, a), Is.True);

            Assert.That(juggernaut.JuggernautId, Is.EqualTo((int?)a));
            Assert.That(juggernaut.IsJuggernaut(b), Is.False);
            Assert.That(juggernaut.GetScore(a), Is.EqualTo(_scorePerJuggernautKill));
            Assert.That(juggernaut.GetScore(b), Is.Zero);
            Assert.That(session.IsDown(b), Is.True, "the old juggernaut waits to respawn");
            Assert.That(changes.Count, Is.EqualTo(2));
            Assert.That(changes[1].JuggernautId, Is.EqualTo(a));
            Assert.That(changes[1].PreviousId, Is.EqualTo((int?)b));
            Assert.That(changes[1].Reason, Is.EqualTo(JuggernautChangeReason.TakenOver));

            var lines = new[]
            {
                JuggernautLog.Changed(changes[0]),
                JuggernautLog.Changed(changes[1]),
                JuggernautLog.Score(a, juggernaut.GetScore(a)),
            };
            foreach (var line in lines)
            {
                TestContext.Out.WriteLine(line);
            }

            Assert.That(lines[0], Is.EqualTo("[Mode] juggernaut change player=1 from=- reason=chosen"));
            Assert.That(lines[1], Is.EqualTo("[Mode] juggernaut change player=0 from=1 reason=taken-over"));
            Assert.That(lines[2], Is.EqualTo("[Mode] juggernaut score player=0 score=3"));
        }

        [Test]
        public void TheJuggernautScoresForEachKillAndStaysTheJuggernaut()
        {
            var juggernaut = Chosen(3);
            var changes = Record(juggernaut);
            var session = juggernaut.Session;

            session.ReportKill(0, 1);
            Assert.That(juggernaut.ReportFall(1, 0), Is.False, "the victim was not the juggernaut");
            session.ReportKill(0, 2);
            Assert.That(juggernaut.ReportFall(2, 0), Is.False);

            Assert.That(juggernaut.JuggernautId, Is.EqualTo((int?)0));
            Assert.That(juggernaut.GetScore(0), Is.EqualTo(2 * _scorePerKillAsJuggernaut));
            Assert.That(session.GetKills(0), Is.EqualTo(2));
            Assert.That(changes, Is.Empty);
        }

        [Test]
        public void KillsBetweenOtherPlayersScoreNothing()
        {
            var juggernaut = Chosen(3);
            var session = juggernaut.Session;

            session.ReportKill(1, 2);
            juggernaut.ReportFall(2, 1);

            Assert.That(session.GetKills(1), Is.EqualTo(1));
            Assert.That(juggernaut.GetScore(1), Is.Zero);
            Assert.That(juggernaut.JuggernautId, Is.EqualTo((int?)0));
        }

        [Test]
        public void AJuggernautWhoDiesWithoutAKillerIsReplacedByAnotherPlayer()
        {
            var juggernaut = Chosen(3);
            var changes = Record(juggernaut);
            var session = juggernaut.Session;

            session.ReportDeath(0);
            Assert.That(juggernaut.ReportFall(0, null), Is.True);

            Assert.That(juggernaut.JuggernautId, Is.EqualTo((int?)1), "the picker chooses from the others, not the fallen juggernaut");
            Assert.That(changes.Count, Is.EqualTo(1));
            Assert.That(changes[0].PreviousId, Is.EqualTo((int?)0));
            Assert.That(changes[0].Reason, Is.EqualTo(JuggernautChangeReason.Repicked));
            Assert.That(juggernaut.GetScore(0) + juggernaut.GetScore(1) + juggernaut.GetScore(2), Is.Zero);
            Assert.That(JuggernautLog.Changed(changes[0]), Is.EqualTo("[Mode] juggernaut change player=1 from=0 reason=repicked"));
        }

        [Test]
        public void AJuggernautWhoKillsThemselvesIsReplacedToo()
        {
            var juggernaut = Chosen(3, PickLast, expected: 2);
            var session = juggernaut.Session;

            session.ReportKill(2, 2);
            Assert.That(juggernaut.ReportFall(2, 2), Is.True);

            Assert.That(juggernaut.JuggernautId, Is.EqualTo((int?)1), "the last of the others");
            Assert.That(juggernaut.GetScore(2), Is.Zero, "killing yourself scores nothing");
        }

        [Test]
        public void TheRepickPrefersPlayersWhoAreStanding()
        {
            var juggernaut = Chosen(3);
            var session = juggernaut.Session;
            session.ReportKill(2, 1);
            Assert.That(session.IsDown(1), Is.True);

            session.ReportDeath(0);
            juggernaut.ReportFall(0, null);

            Assert.That(juggernaut.JuggernautId, Is.EqualTo((int?)2));
        }

        [Test]
        public void TheOnlyPlayerStaysTheJuggernautAfterFalling()
        {
            var juggernaut = Chosen(1);

            juggernaut.Session.ReportDeath(0);

            Assert.That(juggernaut.ReportFall(0, null), Is.False);
            Assert.That(juggernaut.JuggernautId, Is.EqualTo((int?)0));
        }

        [Test]
        public void AJuggernautWhoLeavesIsReplacedAtOnce()
        {
            var juggernaut = Chosen(3);
            var changes = Record(juggernaut);

            juggernaut.RemovePlayer(0);

            Assert.That(juggernaut.JuggernautId, Is.EqualTo((int?)1));
            Assert.That(changes.Count, Is.EqualTo(1));
            Assert.That(changes[0].PreviousId, Is.EqualTo((int?)0));
            Assert.That(changes[0].Reason, Is.EqualTo(JuggernautChangeReason.Left));
        }

        [Test]
        public void AJuggernautRemovedFromTheSessionIsReplacedOnTheNextTick()
        {
            var juggernaut = Chosen(3);
            var changes = Record(juggernaut);

            juggernaut.Session.RemovePlayer(0);
            juggernaut.Session.Tick(0.1f);

            Assert.That(juggernaut.JuggernautId, Is.EqualTo((int?)1));
            Assert.That(changes.Count, Is.EqualTo(1));
            Assert.That(changes[0].Reason, Is.EqualTo(JuggernautChangeReason.Left));
        }

        [Test]
        public void ReachingTheScoreLimitWins()
        {
            var juggernaut = Chosen(2, PickLast, expected: 1);
            var session = juggernaut.Session;

            session.ReportKill(0, 1);
            juggernaut.ReportFall(1, 0);
            session.Tick(_respawnSeconds);
            session.ReportKill(0, 1);
            juggernaut.ReportFall(1, 0);
            Assert.That(juggernaut.GetScore(0), Is.EqualTo(_scorePerJuggernautKill + _scorePerKillAsJuggernaut));
            Assert.That(session.State, Is.EqualTo(GameModeState.Playing));

            session.Tick(_respawnSeconds);
            session.ReportKill(0, 1);

            Assert.That(session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(juggernaut.GetScore(0), Is.EqualTo(_scoreLimit));
            Assert.That(session.Winner, Is.EqualTo((Side?)session.GetSide(0)));
            Assert.That(juggernaut.WinnerId, Is.EqualTo((int?)0));
            Assert.That(juggernaut.ReportFall(1, 0), Is.False, "nothing changes after the end");
            Assert.That(JuggernautLog.Ended(juggernaut.WinnerId, juggernaut.GetScore(0)), Is.EqualTo("[Mode] juggernaut end winner=0 score=5"));
        }

        [Test]
        public void TheHighestScoreWinsWhenTimeRunsOut()
        {
            var juggernaut = Chosen(3);
            var session = juggernaut.Session;

            session.ReportKill(1, 0);
            juggernaut.ReportFall(0, 1);
            session.Tick(_timeLimitSeconds);

            Assert.That(session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(juggernaut.WinnerId, Is.EqualTo((int?)1));
            Assert.That(juggernaut.GetScore(1), Is.EqualTo(_scorePerJuggernautKill));
        }

        [Test]
        public void ATieAtTheTopWhenTimeRunsOutIsADraw()
        {
            var juggernaut = Chosen(3);
            var session = juggernaut.Session;

            session.Tick(_timeLimitSeconds);

            Assert.That(session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(session.Winner, Is.Null);
            Assert.That(juggernaut.WinnerId, Is.Null);
            Assert.That(JuggernautLog.Ended(null, 0), Is.EqualTo("[Mode] juggernaut end winner=draw"));
        }

        [Test]
        public void TheMatchUsesTheConfiguredTimeLimit()
        {
            var spec = new JuggernautSpec(timeLimitSeconds: 90f);
            var juggernaut = new Juggernaut(spec, _respawnSeconds, PickFirst);

            Assert.That(juggernaut.Session.TimeLimitSeconds, Is.EqualTo(90f));
            Assert.That(juggernaut.Session.ScoreLimit, Is.EqualTo((double?)JuggernautSpec.DefaultScoreLimit));
            Assert.That(juggernaut.SecondsUntilChosen, Is.EqualTo(JuggernautSpec.DefaultStartDelaySeconds));
        }

        [Test]
        public void ResetClearsTheJuggernautAndWaitsAgain()
        {
            var juggernaut = Chosen(3);
            juggernaut.Session.ReportKill(1, 0);
            juggernaut.ReportFall(0, 1);

            juggernaut.Reset();

            Assert.That(juggernaut.HasJuggernaut, Is.False);
            Assert.That(juggernaut.SecondsUntilChosen, Is.EqualTo(_startDelaySeconds));
            Assert.That(juggernaut.Session.State, Is.EqualTo(GameModeState.Waiting));
            Assert.That(juggernaut.GetScore(1), Is.Zero);

            juggernaut.Session.Start();
            juggernaut.Session.Tick(_startDelaySeconds);
            Assert.That(juggernaut.JuggernautId, Is.EqualTo((int?)0));
        }

        [Test]
        public void UnknownPlayersAreRejected()
        {
            var juggernaut = Chosen(2);

            Assert.Throws<ArgumentNullException>(() => new Juggernaut(null, _respawnSeconds));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Juggernaut(Spec(), -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => juggernaut.ReportFall(9, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => juggernaut.ReportFall(0, 9));
            Assert.Throws<ArgumentOutOfRangeException>(() => juggernaut.IsJuggernaut(9));
            Assert.Throws<ArgumentOutOfRangeException>(() => juggernaut.GetScore(9));
            Assert.Throws<ArgumentOutOfRangeException>(() => juggernaut.RemovePlayer(9));
        }

        [Test]
        public void TheStatusCopiesTheJuggernautAndTheLocalPlayersRole()
        {
            var juggernaut = Started(3, PickLast);
            var before = JuggernautStatus.Of(juggernaut, 2);

            juggernaut.Session.Tick(_startDelaySeconds);
            var asJuggernaut = JuggernautStatus.Of(juggernaut, 2);
            var asOther = JuggernautStatus.Of(juggernaut, 0);
            var botsOnly = JuggernautStatus.Of(juggernaut, null);

            Assert.That(before.JuggernautId, Is.Null);
            Assert.That(before.SecondsUntilChosen, Is.EqualTo(_startDelaySeconds));
            Assert.That(before.IsLocalPlayerJuggernaut, Is.EqualTo((bool?)false));
            Assert.That(asJuggernaut.JuggernautId, Is.EqualTo((int?)2));
            Assert.That(asJuggernaut.SecondsUntilChosen, Is.Zero);
            Assert.That(asJuggernaut.IsLocalPlayerJuggernaut, Is.EqualTo((bool?)true));
            Assert.That(asOther.IsLocalPlayerJuggernaut, Is.EqualTo((bool?)false));
            Assert.That(botsOnly.IsLocalPlayerJuggernaut, Is.Null);
            Assert.That(before.JuggernautId, Is.Null, "the earlier status keeps its values");
            Assert.That(new JuggernautStatus(4, 3f, null).SecondsUntilChosen, Is.Zero, "a chosen juggernaut has no wait");
            Assert.Throws<ArgumentNullException>(() => JuggernautStatus.Of(null, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new JuggernautStatus(null, -1f, null));
        }

        [Test]
        public void TheLogLinesNameThePlayersAndTheReason()
        {
            Assert.That(JuggernautLog.Changed(new JuggernautChange(3, null, JuggernautChangeReason.Chosen)), Is.EqualTo("[Mode] juggernaut change player=3 from=- reason=chosen"));
            Assert.That(JuggernautLog.Changed(new JuggernautChange(1, 3, JuggernautChangeReason.TakenOver)), Is.EqualTo("[Mode] juggernaut change player=1 from=3 reason=taken-over"));
            Assert.That(JuggernautLog.Changed(new JuggernautChange(2, 1, JuggernautChangeReason.Repicked)), Is.EqualTo("[Mode] juggernaut change player=2 from=1 reason=repicked"));
            Assert.That(JuggernautLog.Changed(new JuggernautChange(0, 2, JuggernautChangeReason.Left)), Is.EqualTo("[Mode] juggernaut change player=0 from=2 reason=left"));
            Assert.That(JuggernautLog.Score(4, 12), Is.EqualTo("[Mode] juggernaut score player=4 score=12"));
            Assert.That(JuggernautLog.Body(2, 300, 1.1f), Is.EqualTo("[Mode] juggernaut body player=2 max-health=300 speed=1.1"));
            Assert.That(JuggernautLog.Body(0, 300, null), Is.EqualTo("[Mode] juggernaut body player=0 max-health=300 speed=-"));
            Assert.That(JuggernautLog.Restored(2, 100, 1f), Is.EqualTo("[Mode] juggernaut restore player=2 max-health=100 speed=1"));
            Assert.That(JuggernautLog.BotTarget(1, 3, false), Is.EqualTo("[Mode] juggernaut bot-target player=1 target=3 role=hunter"));
            Assert.That(JuggernautLog.BotTarget(3, 0, true), Is.EqualTo("[Mode] juggernaut bot-target player=3 target=0 role=juggernaut"));
            Assert.Throws<ArgumentOutOfRangeException>(() => JuggernautLog.ReasonText((JuggernautChangeReason)99));
        }

        private static JuggernautSpec Spec()
        {
            return new JuggernautSpec(
                JuggernautSpec.DefaultJuggernautMaxHealth,
                JuggernautSpec.DefaultJuggernautSpeedMultiplier,
                _scorePerKillAsJuggernaut,
                _scorePerJuggernautKill,
                _scoreLimit,
                _timeLimitSeconds,
                _startDelaySeconds);
        }

        private static Juggernaut Started(int players, Func<int, int> pickIndex)
        {
            var juggernaut = new Juggernaut(Spec(), _respawnSeconds, pickIndex);
            for (var id = 0; id < players; id++)
            {
                juggernaut.Session.Join(id);
            }

            juggernaut.Session.Start();
            return juggernaut;
        }

        // 既定では最初のジャガーノートは 0 番になる。
        private static Juggernaut Chosen(int players, Func<int, int> pickIndex = null, int expected = 0)
        {
            var juggernaut = Started(players, pickIndex ?? PickFirst);
            juggernaut.Session.Tick(_startDelaySeconds);
            Assert.That(juggernaut.JuggernautId, Is.EqualTo((int?)expected));
            return juggernaut;
        }

        private static List<JuggernautChange> Record(Juggernaut juggernaut)
        {
            var changes = new List<JuggernautChange>();
            juggernaut.Changed += changes.Add;
            return changes;
        }

        private static int PickFirst(int count) => 0;

        private static int PickLast(int count) => count - 1;
    }
}

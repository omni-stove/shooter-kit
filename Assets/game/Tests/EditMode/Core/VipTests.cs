using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class VipTests
    {
        private const int _alphaOne = 0;
        private const int _alphaTwo = 1;
        private const int _bravoOne = 10;
        private const int _bravoTwo = 11;
        private const int _bravoThree = 12;

        private const float _roundSeconds = 60f;
        private const float _exitHoldSeconds = 3f;

        private static readonly int[] _expectedFirstPick = { _bravoOne };
        private static readonly int[] _expectedLastPick = { _bravoTwo };
        private static readonly int[] _expectedRepick = { _bravoOne, _bravoTwo };
        private static readonly int[] _expectedRepickStanding = { _bravoOne, _bravoThree };
        private static readonly int[] _expectedDownedVipThenNextRound = { _bravoOne, _alphaOne };
        private static readonly int[] _expectedJoinerPick = { _bravoOne, _bravoThree };

        private static readonly string[] _expectedEscapeLog =
        {
            "[Vip] chosen round=1 vip=10",
            "[Vip] round=1 attacker=0 defender=1",
            "[Vip] escaped vip=10",
            "[Vip] round_end round=1 winner=1 role=defender reason=Escaped wins=0:0,1:1",
            "[Vip] swap attacker=1 defender=0",
            "[Vip] chosen round=2 vip=0",
            "[Vip] round=2 attacker=1 defender=0",
        };

        private static readonly string[] _expectedVipDownLog =
        {
            "[Vip] chosen round=1 vip=10",
            "[Vip] round=1 attacker=0 defender=1",
            "[Vip] round_end round=1 winner=0 role=attacker reason=VipDown wins=0:1,1:0",
            "[Vip] swap attacker=1 defender=0",
            "[Vip] chosen round=2 vip=0",
            "[Vip] round=2 attacker=1 defender=0",
        };

        private static readonly string[] _expectedMatchLog =
        {
            "[Vip] chosen round=1 vip=10",
            "[Vip] round=1 attacker=0 defender=1",
            "[Vip] round_end round=1 winner=0 role=attacker reason=TimeUp wins=0:1,1:0",
            "[Vip] swap attacker=1 defender=0",
            "[Vip] chosen round=2 vip=0",
            "[Vip] round=2 attacker=1 defender=0",
            "[Vip] round_end round=2 winner=1 role=attacker reason=TimeUp wins=0:1,1:1",
            "[Vip] swap attacker=0 defender=1",
            "[Vip] chosen round=3 vip=10",
            "[Vip] round=3 attacker=0 defender=1",
            "[Vip] round_end round=3 winner=0 role=attacker reason=TimeUp wins=0:2,1:1",
            "[Vip] ended winner=0",
        };

        [Test]
        public void RejectsMissingSpec()
        {
            Assert.Throws<ArgumentNullException>(() => new Vip(null));
            Assert.Throws<ArgumentNullException>(() => VipStatus.Of(null, null));
            Assert.Throws<ArgumentNullException>(() => VipLog.SettingsLog(null));
            Assert.Throws<ArgumentNullException>(() => VipLog.StatusLog(null));
        }

        [Test]
        public void ChoosesTheVipFromTheDefendersWhenTheRoundStarts()
        {
            var game = Create(pick: count => 0);
            var chosen = new List<int>();
            game.VipChosen += chosen.Add;

            game.Start();

            Assert.That(game.Phase, Is.EqualTo(RoundPhase.Live));
            Assert.That(game.Attacker, Is.EqualTo(Side.Alpha));
            Assert.That(game.Defender, Is.EqualTo(Side.Bravo));
            Assert.That(game.VipId, Is.EqualTo(_bravoOne));
            Assert.That(game.IsVip(_bravoOne), Is.True);
            Assert.That(game.IsVip(_alphaOne), Is.False);
            Assert.That(chosen, Is.EqualTo(_expectedFirstPick));
            Assert.That(game.RoundRemaining, Is.EqualTo(_roundSeconds));
        }

        [Test]
        public void PickerChoosesAmongDefendersSortedById()
        {
            var counts = new List<int>();
            var game = Create(pick: count =>
            {
                counts.Add(count);
                return count - 1;
            });
            var chosen = new List<int>();
            game.VipChosen += chosen.Add;

            game.Start();

            Assert.That(counts, Is.EqualTo(new[] { 2 }));
            Assert.That(game.VipId, Is.EqualTo(_bravoTwo));
            Assert.That(chosen, Is.EqualTo(_expectedLastPick));
        }

        [Test]
        public void DefaultPickerChoosesADefender()
        {
            var game = Create();

            game.Start();

            Assert.That(game.VipId, Is.AnyOf(_bravoOne, _bravoTwo));
        }

        [Test]
        public void PickerOutOfRangeThrows()
        {
            var game = Create(pick: count => count);

            Assert.Throws<InvalidOperationException>(() => game.Start());
        }

        [Test]
        public void DoesNotChooseBeforeTheMatchStarts()
        {
            var game = Create(pick: count => 0);
            var chosen = new List<int>();
            game.VipChosen += chosen.Add;

            game.AddPlayer(_bravoThree, Side.Bravo);

            Assert.That(game.VipId, Is.Null);
            Assert.That(chosen, Is.Empty);
        }

        [Test]
        public void VipStayingAtAnExitForTheHoldSecondsWinsTheRoundForTheDefenders()
        {
            var game = Create(pick: count => 0);
            var lines = Record(game);
            game.Start();
            game.SetAtExit(_bravoOne, true);

            game.Tick(2f);

            Assert.That(game.ExitProgress, Is.EqualTo(2f));
            Assert.That(game.ExitFraction, Is.EqualTo(2f / 3f).Within(1e-6f));
            Assert.That(game.RoundRemaining, Is.EqualTo(58f));
            Assert.That(game.LastRoundEnd, Is.Null);

            game.Tick(1f);

            Assert.That(game.LastRoundEnd, Is.EqualTo(VipRoundEnd.Escaped));
            Assert.That(game.LastRoundWinner, Is.EqualTo(Side.Bravo));
            Assert.That(game.GetRoundWins(Side.Bravo), Is.EqualTo(1));
            Assert.That(game.GetRoundWins(Side.Alpha), Is.EqualTo(0));
            Assert.That(lines, Is.EqualTo(_expectedEscapeLog));
        }

        [Test]
        public void EscapingInTheMiddleOfALongTickStillWins()
        {
            var game = Create(pick: count => 0);
            var escaped = new List<int>();
            game.VipEscaped += escaped.Add;
            game.Start();
            game.SetAtExit(_bravoOne, true);

            game.Tick(10f);

            Assert.That(escaped, Is.EqualTo(_expectedFirstPick));
            Assert.That(game.LastRoundEnd, Is.EqualTo(VipRoundEnd.Escaped));
        }

        [Test]
        public void LeavingTheExitStartsTheHoldOver()
        {
            var game = Create(pick: count => 0);
            game.Start();
            game.SetAtExit(_bravoOne, true);
            game.Tick(2f);

            game.SetAtExit(_bravoOne, false);
            game.Tick(1f);

            Assert.That(game.ExitProgress, Is.Zero);
            Assert.That(game.ExitFraction, Is.Zero);

            game.SetAtExit(_bravoOne, true);
            game.Tick(2f);

            Assert.That(game.ExitProgress, Is.EqualTo(2f));
            Assert.That(game.LastRoundEnd, Is.Null);
        }

        [Test]
        public void OnlyTheVipAtAnExitCounts()
        {
            var game = Create(pick: count => 0);
            game.Start();
            game.SetAtExit(_bravoTwo, true);
            game.SetAtExit(_alphaOne, true);

            game.Tick(5f);

            Assert.That(game.ExitProgress, Is.Zero);
            Assert.That(game.LastRoundEnd, Is.Null);
        }

        [Test]
        public void SettingAtExitForAnUnknownPlayerThrows()
        {
            var game = Create(pick: count => 0);
            game.Start();

            Assert.Throws<ArgumentOutOfRangeException>(() => game.SetAtExit(99, true));
        }

        [Test]
        public void ZeroHoldSecondsEscapesOnTheFirstTickAtTheExit()
        {
            var game = Create(pick: count => 0, exitHoldSeconds: 0f);
            game.Start();
            game.SetAtExit(_bravoOne, true);

            game.Tick(0f);

            Assert.That(game.LastRoundEnd, Is.EqualTo(VipRoundEnd.Escaped));
            Assert.That(game.LastRoundWinner, Is.EqualTo(Side.Bravo));
        }

        [Test]
        public void KillingTheVipWinsTheRoundForTheAttackers()
        {
            var game = Create(pick: count => 0);
            var lines = Record(game);
            game.Start();

            game.ReportKill(_alphaOne, _bravoOne);
            game.Tick(0f);

            Assert.That(game.LastRoundEnd, Is.EqualTo(VipRoundEnd.VipDown));
            Assert.That(game.LastRoundWinner, Is.EqualTo(Side.Alpha));
            Assert.That(game.GetRoundWins(Side.Alpha), Is.EqualTo(1));
            Assert.That(lines, Is.EqualTo(_expectedVipDownLog));
        }

        [Test]
        public void VipDyingWithoutAKillerAlsoLosesTheRound()
        {
            var game = Create(pick: count => 0);
            game.Start();

            game.ReportDeath(_bravoOne);
            game.Tick(0f);

            Assert.That(game.LastRoundEnd, Is.EqualTo(VipRoundEnd.VipDown));
            Assert.That(game.LastRoundWinner, Is.EqualTo(Side.Alpha));
        }

        [Test]
        public void VipGoingDownBeatsFinishingTheHoldInTheSameTick()
        {
            var game = Create(pick: count => 0);
            game.Start();
            game.SetAtExit(_bravoOne, true);
            game.Tick(2.5f);

            game.ReportKill(_alphaOne, _bravoOne);
            game.Tick(1f);

            Assert.That(game.LastRoundEnd, Is.EqualTo(VipRoundEnd.VipDown));
            Assert.That(game.LastRoundWinner, Is.EqualTo(Side.Alpha));
        }

        [Test]
        public void VipGoingDownBeatsTheLastAttackerGoingDown()
        {
            var game = Create(pick: count => 0);
            game.Start();

            game.ReportKill(_bravoTwo, _alphaOne);
            game.ReportKill(_bravoTwo, _alphaTwo);
            game.ReportKill(_alphaTwo, _bravoOne);
            game.Tick(0f);

            Assert.That(game.LastRoundEnd, Is.EqualTo(VipRoundEnd.VipDown));
            Assert.That(game.LastRoundWinner, Is.EqualTo(Side.Alpha));
        }

        [Test]
        public void KillingAnEscortDoesNotEndTheRound()
        {
            var game = Create(pick: count => 0);
            game.Start();

            game.ReportKill(_alphaOne, _bravoTwo);
            game.Tick(1f);

            Assert.That(game.LastRoundEnd, Is.Null);
            Assert.That(game.RoundNumber, Is.EqualTo(1));
        }

        [Test]
        public void TimeRunningOutWinsTheRoundForTheAttackers()
        {
            var game = Create(pick: count => 0);
            game.Start();

            game.Tick(59f);

            Assert.That(game.LastRoundEnd, Is.Null);

            game.Tick(1f);

            Assert.That(game.LastRoundEnd, Is.EqualTo(VipRoundEnd.TimeUp));
            Assert.That(game.LastRoundWinner, Is.EqualTo(Side.Alpha));
            Assert.That(game.RoundNumber, Is.EqualTo(2));
            Assert.That(game.RoundRemaining, Is.EqualTo(_roundSeconds));
        }

        [Test]
        public void HoldThatCannotFinishBeforeTheClockLosesOnTime()
        {
            var game = Create(pick: count => 0);
            game.Start();
            game.Tick(58f);
            game.SetAtExit(_bravoOne, true);

            game.Tick(3f);

            Assert.That(game.LastRoundEnd, Is.EqualTo(VipRoundEnd.TimeUp));
            Assert.That(game.LastRoundWinner, Is.EqualTo(Side.Alpha));
        }

        [Test]
        public void EliminatingTheAttackersWinsTheRoundForTheDefenders()
        {
            var game = Create(pick: count => 0);
            game.Start();

            game.ReportKill(_bravoOne, _alphaOne);
            game.ReportKill(_bravoTwo, _alphaTwo);
            game.Tick(0f);

            Assert.That(game.LastRoundEnd, Is.EqualTo(VipRoundEnd.AttackersEliminated));
            Assert.That(game.LastRoundWinner, Is.EqualTo(Side.Bravo));
        }

        [Test]
        public void SwapsSidesEachRoundAndEndsAtWinsToWin()
        {
            var game = Create(pick: count => 0);
            var lines = Record(game);
            var ended = new List<Side>();
            game.Ended += ended.Add;
            game.Start();

            game.Tick(_roundSeconds);
            game.Tick(_roundSeconds);
            game.Tick(_roundSeconds);

            Assert.That(lines, Is.EqualTo(_expectedMatchLog));
            Assert.That(game.Phase, Is.EqualTo(RoundPhase.Ended));
            Assert.That(game.Winner, Is.EqualTo(Side.Alpha));
            Assert.That(ended, Is.EqualTo(new[] { Side.Alpha }));

            game.SetAtExit(_bravoOne, true);
            game.Tick(_roundSeconds);

            Assert.That(game.RoundNumber, Is.EqualTo(3));
            Assert.That(game.GetRoundWins(Side.Alpha), Is.EqualTo(2));
        }

        [Test]
        public void ZeroSwapEveryKeepsTheSameDefenders()
        {
            var game = Create(pick: count => 0, swapEveryRounds: 0);
            var swaps = 0;
            game.SidesSwapped += () => swaps++;
            game.Start();

            game.Tick(_roundSeconds);

            Assert.That(swaps, Is.Zero);
            Assert.That(game.RoundNumber, Is.EqualTo(2));
            Assert.That(game.Defender, Is.EqualTo(Side.Bravo));
            Assert.That(game.VipId, Is.EqualTo(_bravoOne));
        }

        [Test]
        public void VipLeavingChoosesAnotherDefender()
        {
            var game = Create(pick: count => 0);
            var chosen = new List<int>();
            game.VipChosen += chosen.Add;
            game.Start();
            game.SetAtExit(_bravoOne, true);
            game.Tick(2f);

            game.RemovePlayer(_bravoOne);

            Assert.That(game.VipId, Is.EqualTo(_bravoTwo));
            Assert.That(game.ExitProgress, Is.Zero);
            Assert.That(chosen, Is.EqualTo(_expectedRepick));

            game.Tick(1f);

            Assert.That(game.LastRoundEnd, Is.Null);
        }

        [Test]
        public void VipLeavingChoosesOnlyAStandingDefender()
        {
            var game = Create(pick: count => 0);
            game.AddPlayer(_bravoThree, Side.Bravo);
            var chosen = new List<int>();
            game.VipChosen += chosen.Add;
            game.Start();
            game.ReportKill(_alphaOne, _bravoTwo);

            game.RemovePlayer(_bravoOne);

            Assert.That(game.VipId, Is.EqualTo(_bravoThree));
            Assert.That(chosen, Is.EqualTo(_expectedRepickStanding));
        }

        [Test]
        public void VipLeavingAfterGoingDownStillLosesTheRound()
        {
            var game = Create(pick: count => 0);
            var chosen = new List<int>();
            game.VipChosen += chosen.Add;
            game.Start();
            game.ReportKill(_alphaOne, _bravoOne);

            game.RemovePlayer(_bravoOne);
            game.Tick(0f);

            Assert.That(game.LastRoundEnd, Is.EqualTo(VipRoundEnd.VipDown));
            Assert.That(game.LastRoundWinner, Is.EqualTo(Side.Alpha));
            Assert.That(chosen, Is.EqualTo(_expectedDownedVipThenNextRound));
        }

        [Test]
        public void LastDefenderLeavingWaitsForTheNextDefenderToJoin()
        {
            var game = Create(pick: count => 0);
            var chosen = new List<int>();
            game.VipChosen += chosen.Add;
            game.Start();
            game.RemovePlayer(_bravoTwo);

            game.RemovePlayer(_bravoOne);
            game.Tick(1f);

            Assert.That(game.VipId, Is.Null);
            Assert.That(game.LastRoundEnd, Is.Null);

            game.AddPlayer(_bravoThree, Side.Bravo);

            Assert.That(game.VipId, Is.EqualTo(_bravoThree));
            Assert.That(chosen, Is.EqualTo(_expectedJoinerPick));
        }

        [Test]
        public void VipLeavingWithEveryOtherDefenderDownLosesTheRound()
        {
            var game = Create(pick: count => 0);
            game.Start();
            game.ReportKill(_alphaOne, _bravoTwo);

            game.RemovePlayer(_bravoOne);
            game.Tick(0f);

            Assert.That(game.LastRoundEnd, Is.EqualTo(VipRoundEnd.DefendersEliminated));
            Assert.That(game.LastRoundWinner, Is.EqualTo(Side.Alpha));
        }

        [Test]
        public void VipMovedToTheAttackersThroughTheModeIsReplacedOnTheNextTick()
        {
            var game = Create(pick: count => 0);
            game.Start();

            game.Mode.ChangeSide(_bravoOne, Side.Alpha);
            game.Tick(0f);

            Assert.That(game.VipId, Is.EqualTo(_bravoTwo));
            Assert.That(game.LastRoundEnd, Is.Null);
        }

        [Test]
        public void StatusCarriesTheVipTheExitProgressTheDefendersAndTheLocalRole()
        {
            var game = Create(pick: count => 0);
            game.Start();
            game.SetAtExit(_bravoOne, true);
            game.Tick(1.5f);

            var asVip = game.Status(_bravoOne);
            var detail = (VipStatus)asVip.Detail;

            Assert.That(detail.VipId, Is.EqualTo(_bravoOne));
            Assert.That(detail.ExitFraction, Is.EqualTo(0.5f));
            Assert.That(detail.DefenderSide, Is.EqualTo(Side.Bravo));
            Assert.That(detail.IsLocalVip, Is.True);
            Assert.That(asVip.RemainingSeconds, Is.EqualTo(58.5f));
            Assert.That(asVip.OwnSide, Is.EqualTo(Side.Bravo));
            Assert.That(asVip.IsEnded, Is.False);
            Assert.That(asVip.SideScores, Is.EqualTo(new[] { new SideScore(Side.Alpha, 0d), new SideScore(Side.Bravo, 0d) }));
            Assert.That(((VipStatus)game.Status(_alphaOne).Detail).IsLocalVip, Is.False);
            Assert.That(((VipStatus)game.Status(null).Detail).IsLocalVip, Is.Null);
            Assert.That(((VipStatus)game.Status(99).Detail).IsLocalVip, Is.Null);
            Assert.That(VipLog.StatusLog(detail), Is.EqualTo("[Vip] status vip=10 exit=0.5 defender=1 local=vip"));
        }

        [Test]
        public void StatusRejectsOutOfRangeValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new VipStatus(_bravoOne, 1.5f, Side.Bravo, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VipStatus(_bravoOne, -0.1f, Side.Bravo, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VipStatus(_bravoOne, float.NaN, Side.Bravo, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VipStatus(_bravoOne, 0f, new Side(2), null));

            var nobody = new VipStatus(null, 0.5f, Side.Alpha, null);

            Assert.That(nobody.ExitFraction, Is.Zero);
            Assert.That(VipLog.StatusLog(nobody), Is.EqualTo("[Vip] status vip=- exit=0 defender=0 local=-"));
        }

        [Test]
        public void ResetGoesBackToTheFirstRoundWithoutAVip()
        {
            var game = Create(pick: count => 0);
            game.Start();
            game.Tick(_roundSeconds);

            game.Reset();

            Assert.That(game.Phase, Is.EqualTo(RoundPhase.Waiting));
            Assert.That(game.RoundNumber, Is.Zero);
            Assert.That(game.VipId, Is.Null);
            Assert.That(game.LastRoundEnd, Is.Null);
            Assert.That(game.LastRoundWinner, Is.Null);
            Assert.That(game.Defender, Is.EqualTo(Side.Bravo));
            Assert.That(game.GetRoundWins(Side.Alpha), Is.Zero);
            Assert.That(game.RoundRemaining, Is.EqualTo(_roundSeconds));

            game.Start();

            Assert.That(game.VipId, Is.EqualTo(_bravoOne));
        }

        [Test]
        public void ResetFromTheEscapeHandlerStopsTheTick()
        {
            var game = Create(pick: count => 0);
            game.VipEscaped += _ => game.Reset();
            game.Start();
            game.SetAtExit(_bravoOne, true);

            game.Tick(5f);

            Assert.That(game.Phase, Is.EqualTo(RoundPhase.Waiting));
            Assert.That(game.GetRoundWins(Side.Bravo), Is.Zero);
            Assert.That(game.LastRoundEnd, Is.Null);
        }

        [Test]
        public void SettingsLogWritesEveryTunedValue()
        {
            var spec = new VipSpec(vipMaxHealth: 250, roundSeconds: 90f, winsToWin: 3, exitHoldSeconds: 2.5f, swapEveryRounds: 2);

            Assert.That(
                VipLog.SettingsLog(spec),
                Is.EqualTo("[Vip] settings vipHealth=250 winsToWin=3 round=90s exitHold=2.5s swapEvery=2"));
        }

        [Test]
        public void RoundEndLogRejectsAnUndefinedReason()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => VipLog.RoundEndLog(1, Side.Alpha, Side.Bravo, (VipRoundEnd)99, 0, 0));
        }

        private static Vip Create(
            Func<int, int> pick = null,
            float exitHoldSeconds = _exitHoldSeconds,
            int swapEveryRounds = 1)
        {
            var spec = new VipSpec(
                roundSeconds: _roundSeconds,
                winsToWin: 2,
                exitHoldSeconds: exitHoldSeconds,
                swapEveryRounds: swapEveryRounds);
            var game = new Vip(spec, pick);
            game.AddPlayer(_alphaOne, Side.Alpha);
            game.AddPlayer(_alphaTwo, Side.Alpha);
            game.AddPlayer(_bravoOne, Side.Bravo);
            game.AddPlayer(_bravoTwo, Side.Bravo);
            return game;
        }

        // シーンの Runner が出す行と同じものを、イベントから VipLog で作って並べる。
        private static List<string> Record(Vip game)
        {
            var lines = new List<string>();
            game.VipChosen += playerId => lines.Add(VipLog.ChosenLog(game.RoundNumber, playerId));
            game.RoundStarted += round => lines.Add(VipLog.RoundLog(round, game.Attacker, game.Defender));
            game.VipEscaped += playerId => lines.Add(VipLog.EscapedLog(playerId));
            game.RoundEnded += (winner, reason) => lines.Add(VipLog.RoundEndLog(
                game.RoundNumber, winner, game.Defender, reason, game.GetRoundWins(Side.Alpha), game.GetRoundWins(Side.Bravo)));
            game.SidesSwapped += () => lines.Add(VipLog.SwapLog(game.Attacker, game.Defender));
            game.Ended += winner => lines.Add(VipLog.EndedLog(winner));
            return lines;
        }
    }
}

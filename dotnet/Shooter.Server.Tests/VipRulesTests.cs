using System;
using System.IO;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    public sealed class VipRulesTests
    {
        private const int _alphaFirst = 1;
        private const int _alphaSecond = 2;
        private const int _bravoFirst = 3;
        private const int _bravoSecond = 4;
        private const int _lethal = 1000;
        private const int _vipMaxHealth = 250;
        private const float _roundSeconds = 2f;
        private const int _winsToWin = 2;
        private const float _exitHoldSeconds = 1f;
        private const float _exitRadius = 3f;
        private const string _north = "exit-north";
        private const string _south = "exit-south";

        // 1 tick は 1/60 秒。留まる 1 秒は 60 tick なので、30 tick は半ばで、62 tick で必ず越える。ラウンドの 2 秒は 125 tick で必ず越える
        private const int _halfExit = 30;
        private const int _moreThanExit = 62;
        private const int _moreThanRound = 125;

        private static readonly Vector3 _alphaSpawn = new Vector3(-20f, 1f, -20f);
        private static readonly Vector3 _bravoSpawn = new Vector3(20f, 1f, 20f);
        private static readonly Vector3 _northAt = new Vector3(0f, 0f, 30f);
        private static readonly Vector3 _southAt = new Vector3(0f, 0f, -30f);

        private StringWriter _log;
        private MatchHost _host;
        private VipRules _rules;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
        }

        [TearDown]
        public void TearDown()
        {
            _rules?.Stop();
            _log.Dispose();
        }

        [Test]
        public void TheObjectivePlaysInRoundsAndKeepsTheSessionFromEndingFirst()
        {
            var settings = Settings();
            var rules = new VipRules(settings, new MatchHost(_log), PickFirst);

            Assert.That(rules.Objective, Is.EqualTo(MatchObjective.Vip));
            Assert.That(rules.PlaysInRounds, Is.True);
            Assert.That(rules.KeepsMatchScore, Is.True);
            Assert.That(rules.HoldsFire, Is.False, "a round starts live; vip.json has no preparation");
            Assert.That(rules.RemainingSeconds, Is.EqualTo(_roundSeconds));
            Assert.That(rules.CurrentRoundPhase(), Is.EqualTo(RoundPhase.Waiting));
            Assert.That(rules.DecideWinner(Team.Bravo), Is.Null, "no round is won yet");
            Assert.That(rules.Exits, Is.Empty);

            var spec = rules.CreateSessionSpec(GameRules.Default, settings);
            Assert.That(spec.ScoreLimit, Is.EqualTo(int.MaxValue));
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(float.MaxValue));
            Assert.That(spec.RespawnsByRound, Is.True);
            var objective = (IMatchObjectiveRules)rules;
            Assert.That(objective.AreHostile(_alphaFirst, Team.Alpha, _bravoFirst, Team.Bravo), Is.True);
            Assert.That(objective.AreHostile(_alphaFirst, Team.Alpha, _alphaSecond, Team.Alpha), Is.False);
        }

        // 確認方法の手順: 脱出地点の無いマップは、起動でも次の試合のマップの確かめでも断り、断った行が出る
        [Test]
        public void AMapWithoutExtractionPointsIsRefused()
        {
            var rules = new VipRules(Settings(), new MatchHost(_log), PickFirst);
            var map = TestMaps.Data("NetTest", TestRules.Points());

            var used = Assert.Throws<InvalidOperationException>(() => rules.UseMap(map, map.Points));
            Assert.That(used.Message, Does.StartWith("cannot start the Vip match on map NetTest: it has no extraction points"));
            var checkedMap = Assert.Throws<InvalidOperationException>(() => rules.CheckMap(map, map.Points));
            Assert.That(checkedMap.Message, Does.StartWith("cannot change to map NetTest for the Vip match: it has no extraction points"));
            Assert.Throws<InvalidOperationException>(() => rules.Start(), "Start needs a map");
            Assert.That(_log.ToString(), Does.Contain("[Vip] map=NetTest refused: it has no extraction points for the vip to escape to"));
        }

        [Test]
        public void AMapWithMoreExtractionPointsThanASnapshotHoldsIsRefused()
        {
            var rules = new VipRules(Settings(), new MatchHost(_log), PickFirst);
            var exits = new NetMapExtractionPoint[SnapshotMessage.MaxObjectiveItems];
            for (var i = 0; i < exits.Length; i++)
            {
                exits[i] = new NetMapExtractionPoint("exit-" + i.ToString("00", System.Globalization.CultureInfo.InvariantCulture), new Vector3(i * 10f, 0f, 0f), 1f);
            }

            var map = TestMaps.Data("NetTest", new NetMapPoints(TestRules.Points().SpawnPoints, Array.Empty<ControlPointArea>(), extractionPoints: exits));

            var e = Assert.Throws<InvalidOperationException>(() => rules.CheckMap(map, map.Points));
            Assert.That(e.Message, Does.EndWith("it has 32 extraction points but at most 31 fit in a snapshot"));
            Assert.That(_log.ToString(), Does.Contain("[Vip] map=NetTest refused: it has 32 extraction points but at most 31 fit in a snapshot"));
        }

        [Test]
        public void StartingLogsTheTuningAndTheExitsInIdOrder()
        {
            _host = new MatchHost(_log);
            _rules = new VipRules(Settings(), _host, PickFirst);
            _host.Begin(_rules, Plain(), Settings());

            _rules.Start();
            _rules.Start();

            Assert.That(_rules.Exits, Has.Count.EqualTo(2));
            Assert.That(_rules.Exits[0].Id, Is.EqualTo(_north));
            Assert.That(_rules.Exits[1].Id, Is.EqualTo(_south));
            Assert.That(_log.ToString(), Is.EqualTo(
                "[Vip] settings vipHealth=250 winsToWin=2 round=2s exitHold=1s swapEvery=1" + Environment.NewLine
                + "[Vip] net exits=exit-north,exit-south" + Environment.NewLine));
        }

        // 確認方法の手順: 試合が始まると守る側 (Bravo) から VIP が選ばれ、体力の上限が VIP のものになり、選ばれた行が出る
        [Test]
        public void TheFirstRoundChoosesTheVipFromTheDefendersAndRaisesTheirHealth()
        {
            Begin();

            Assert.That(_rules.Game.Attacker, Is.EqualTo(Side.Alpha));
            Assert.That(_rules.Game.Defender, Is.EqualTo(Side.Bravo));
            Assert.That(_rules.Game.VipId, Is.EqualTo((int?)_bravoFirst));
            Assert.That(_rules.CurrentRoundPhase(), Is.EqualTo(RoundPhase.Live));
            Assert.That(_host.Session.GetMaxHealth(_bravoFirst), Is.EqualTo(_vipMaxHealth));
            Assert.That(_host.Session.GetHealth(_bravoFirst), Is.EqualTo(_vipMaxHealth), "the vip starts full");
            Assert.That(_host.Session.GetMaxHealth(_bravoSecond), Is.EqualTo(_host.MaxHealth));
            Assert.That(_host.Session.GetMaxHealth(_alphaFirst), Is.EqualTo(_host.MaxHealth));
            var log = _log.ToString();
            Assert.That(log, Does.Contain("[Vip] chosen round=1 vip=3 maxHealth=250"));
            Assert.That(log, Does.Contain("[Vip] round=1 attacker=0 defender=1"));
            Assert.That(log, Does.Contain("[Vip] live round=1"));

            var snapshot = new MatchSnapshot();
            _rules.FillMatch(ref snapshot);
            Assert.That(snapshot.Objective, Is.EqualTo(MatchObjective.Vip));
            Assert.That(snapshot.RoundNumber, Is.EqualTo(1));
            Assert.That(snapshot.RoundsToWin, Is.EqualTo(_winsToWin));
            Assert.That(snapshot.AlphaPoints, Is.Zero);
            Assert.That(snapshot.BravoPoints, Is.Zero);
            Assert.That(snapshot.HasLastRound, Is.False);
            Assert.That(snapshot.PlantFraction, Is.Zero);
        }

        [Test]
        public void TheSnapshotCarriesTheVipAndTheExtractionPoints()
        {
            Begin();
            _host.Place(_bravoFirst, new Vector3(5f, 1f, 6f));

            var items = Items();
            _rules.FillObjectiveItems(items);

            Assert.That(items.Count, Is.EqualTo(3));
            Assert.That(items[0].ItemId, Is.EqualTo(VipRules.VipItemId));
            Assert.That(items[0].Present, Is.EqualTo(ObjectiveItemFields.All));
            Assert.That(items[0].Kind, Is.EqualTo(ObjectiveItemKind.Vip));
            Assert.That(items[0].Team, Is.EqualTo(Team.Bravo), "the item names the defending team");
            Assert.That(items[0].HasOwner, Is.True);
            Assert.That(items[0].OwnerId, Is.EqualTo(_bravoFirst));
            Assert.That(items[0].Position, Is.EqualTo(new Vector3(5f, 1f, 6f)));
            Assert.That(items[0].State, Is.EqualTo(ObjectiveItemState.Carried));
            Assert.That(items[0].HasCarrier, Is.True);
            Assert.That(items[0].CarrierId, Is.EqualTo(_bravoFirst));

            Assert.That(items[1].ItemId, Is.EqualTo(VipRules.FirstExitItemId));
            Assert.That(items[1].Kind, Is.EqualTo(ObjectiveItemKind.ExtractionPoint));
            Assert.That(items[1].Position, Is.EqualTo(_northAt));
            Assert.That(items[1].Radius, Is.EqualTo(_exitRadius));
            Assert.That(items[1].Opened, Is.True);
            Assert.That(items[1].HasOwner, Is.False);
            Assert.That(items[1].State, Is.EqualTo(ObjectiveItemState.Home));
            Assert.That(items[1].HasCarrier, Is.False);
            Assert.That(items[2].ItemId, Is.EqualTo(VipRules.FirstExitItemId + 1));
            Assert.That(items[2].Position, Is.EqualTo(_southAt));
        }

        [Test]
        public void BeforeTheMatchPlaysOnlyTheExtractionPointsAreSent()
        {
            _host = new MatchHost(_log);
            _rules = new VipRules(Settings(), _host, PickFirst);
            _host.Begin(_rules, Plain(), Settings());
            _rules.Start();

            var items = Items();
            _rules.FillObjectiveItems(items);

            Assert.That(items.Count, Is.EqualTo(2));
            Assert.That(items[0].Kind, Is.EqualTo(ObjectiveItemKind.ExtractionPoint));
            Assert.That(items[0].ItemId, Is.EqualTo(VipRules.FirstExitItemId));
            Assert.That(_rules.BotGoal(_bravoFirst), Is.Null);
        }

        // 確認方法の手順: VIP が脱出地点に留まり切ると守る側がラウンドを取り、脱出の行と守る側の勝ちの行が出る
        [Test]
        public void TheVipEscapingWinsTheRoundForTheDefenders()
        {
            Begin();
            _host.Place(_bravoFirst, _northAt);

            _host.Steps(_halfExit);

            Assert.That(_rules.Game.Phase, Is.EqualTo(RoundPhase.Live));
            var midway = new MatchSnapshot();
            _rules.FillMatch(ref midway);
            Assert.That(midway.PlantFraction, Is.EqualTo(0.5f).Within(0.02f), "the exit progress is sent");

            _host.Steps(_moreThanExit - _halfExit);

            var log = _log.ToString();
            Assert.That(log, Does.Contain("[Vip] escaped vip=3"));
            Assert.That(log, Does.Contain("[Vip] round_end round=1 winner=1 role=defender reason=Escaped wins=0:0,1:1"));
            Assert.That(log, Does.Contain("[Vip] body restored player=3"));
            Assert.That(_host.AlphaScore, Is.Zero);
            Assert.That(_host.BravoScore, Is.EqualTo(1));
            Assert.That(_host.Session.GetMaxHealth(_bravoFirst), Is.EqualTo(_host.MaxHealth), "the round end gives the body back");

            var snapshot = new MatchSnapshot();
            _rules.FillMatch(ref snapshot);
            Assert.That(snapshot.RoundNumber, Is.EqualTo(2));
            Assert.That(snapshot.BravoPoints, Is.EqualTo(1));
            Assert.That(snapshot.HasLastRound, Is.True);
            Assert.That(snapshot.LastRoundEnd, Is.EqualTo(RoundEndReason.Defused));
            Assert.That(snapshot.HasLastRoundWinner, Is.True);
            Assert.That(snapshot.LastRoundWinner, Is.EqualTo(Team.Bravo));
            Assert.That(snapshot.PlantFraction, Is.Zero, "the new round starts from no progress");
        }

        [Test]
        public void LeavingTheExitStartsTheProgressOver()
        {
            Begin();
            _host.Place(_bravoFirst, _northAt);
            _host.Steps(_halfExit);

            _host.Place(_bravoFirst, Vector3.Zero);
            _host.Steps(1);

            Assert.That(_rules.Game.ExitProgress, Is.Zero);
            Assert.That(_rules.Game.RoundNumber, Is.EqualTo(1));
        }

        [Test]
        public void ADefenderWhoIsNotTheVipCannotEscape()
        {
            Begin();
            _host.Place(_bravoSecond, _northAt);

            _host.Steps(_moreThanExit);

            Assert.That(_rules.Game.RoundNumber, Is.EqualTo(1));
            Assert.That(_log.ToString(), Does.Not.Contain("[Vip] escaped"));
        }

        // 確認方法の手順: 攻める側が VIP を倒すと攻める側がラウンドを取り、入れ替えた次のラウンドで Alpha から VIP が選ばれる
        [Test]
        public void TheVipFallingWinsTheRoundForTheAttackers()
        {
            Begin();

            _host.Kill(_alphaFirst, _bravoFirst);
            _host.Steps(1);

            var log = _log.ToString();
            Assert.That(log, Does.Contain("[Vip] round_end round=1 winner=0 role=attacker reason=VipDown wins=0:1,1:0"));
            Assert.That(log, Does.Contain("[Vip] body restored player=3"));
            Assert.That(_host.AlphaScore, Is.EqualTo(1));
            Assert.That(_host.BravoScore, Is.Zero);
            Assert.That(_host.Session.IsAlive(_bravoFirst), Is.True, "everyone respawns for the next round");
            Assert.That(_host.Session.GetMaxHealth(_bravoFirst), Is.EqualTo(_host.MaxHealth));
            Assert.That(_rules.Game.VipId, Is.EqualTo((int?)_alphaFirst));
            Assert.That(_host.Session.GetMaxHealth(_alphaFirst), Is.EqualTo(_vipMaxHealth));
            Assert.That(_host.Session.GetHealth(_alphaFirst), Is.EqualTo(_vipMaxHealth));

            var snapshot = new MatchSnapshot();
            _rules.FillMatch(ref snapshot);
            Assert.That(snapshot.LastRoundEnd, Is.EqualTo(RoundEndReason.Detonated));
            Assert.That(snapshot.LastRoundWinner, Is.EqualTo(Team.Alpha));
        }

        [Test]
        public void KillingAnotherDefenderDoesNotEndTheRound()
        {
            Begin();

            _host.Kill(_alphaFirst, _bravoSecond);
            _host.Steps(1);

            Assert.That(_rules.Game.RoundNumber, Is.EqualTo(1));
            Assert.That(_rules.Game.VipId, Is.EqualTo((int?)_bravoFirst));
        }

        [Test]
        public void TheVipWhoIsDeadIsSentAsDropped()
        {
            Begin(winsToWin: 1);
            _host.Place(_bravoFirst, new Vector3(2f, 1f, 3f));

            _host.Kill(_alphaFirst, _bravoFirst);

            var items = Items();
            _rules.FillObjectiveItems(items);
            Assert.That(items[0].Kind, Is.EqualTo(ObjectiveItemKind.Vip));
            Assert.That(items[0].OwnerId, Is.EqualTo(_bravoFirst));
            Assert.That(items[0].State, Is.EqualTo(ObjectiveItemState.Dropped));
            Assert.That(items[0].HasCarrier, Is.False);
            Assert.That(items[0].Position, Is.EqualTo(new Vector3(2f, 1f, 3f)));
        }

        [Test]
        public void TheTimeRunningOutWinsTheRoundForTheAttackers()
        {
            Begin();

            _host.Steps(_moreThanRound);

            Assert.That(_log.ToString(), Does.Contain("[Vip] round_end round=1 winner=0 role=attacker reason=TimeUp wins=0:1,1:0"));
            Assert.That(_host.AlphaScore, Is.EqualTo(1));
            var snapshot = new MatchSnapshot();
            _rules.FillMatch(ref snapshot);
            Assert.That(snapshot.LastRoundEnd, Is.EqualTo(RoundEndReason.TimeUp));
            Assert.That(_rules.RemainingSeconds, Is.GreaterThan(0f).And.LessThanOrEqualTo(_roundSeconds), "the next round counts its own time");
        }

        [Test]
        public void TheRoundEndSwapsTheSidesAndTheVipComesFromTheNewDefenders()
        {
            Begin();

            _host.Steps(_moreThanRound);

            Assert.That(_log.ToString(), Does.Contain("[Vip] swap attacker=1 defender=0"));
            Assert.That(_log.ToString(), Does.Contain("[Vip] round=2 attacker=1 defender=0"));
            Assert.That(_log.ToString(), Does.Contain("[Vip] chosen round=2 vip=1 maxHealth=250"));
            Assert.That(_rules.Game.Attacker, Is.EqualTo(Side.Bravo));
            Assert.That(_rules.Game.Defender, Is.EqualTo(Side.Alpha));
            Assert.That(_rules.Game.VipId, Is.EqualTo((int?)_alphaFirst));
            Assert.That(_host.Session.GetMaxHealth(_alphaFirst), Is.EqualTo(_vipMaxHealth));
            Assert.That(_host.Session.GetMaxHealth(_bravoFirst), Is.EqualTo(_host.MaxHealth));

            var items = Items();
            _rules.FillObjectiveItems(items);
            Assert.That(items[0].OwnerId, Is.EqualTo(_alphaFirst));
            Assert.That(items[0].Team, Is.EqualTo(Team.Alpha));
            Assert.That(items[1].Team, Is.EqualTo(Team.Alpha));
        }

        [Test]
        public void WithoutSwappingTheSameTeamDefendsAgain()
        {
            Begin(swapEveryRounds: 0);

            _host.Steps(_moreThanRound);

            Assert.That(_rules.Game.Defender, Is.EqualTo(Side.Bravo));
            Assert.That(_rules.Game.VipId, Is.EqualTo((int?)_bravoFirst));
            Assert.That(_log.ToString(), Does.Not.Contain("[Vip] swap"));
            Assert.That(_host.Session.GetMaxHealth(_bravoFirst), Is.EqualTo(_vipMaxHealth), "the vip chosen again gets the body again");
        }

        [Test]
        public void ReachingTheWinsToWinEndsTheMatch()
        {
            Begin();
            _host.Steps(_moreThanRound);
            Assert.That(_rules.Game.VipId, Is.EqualTo((int?)_alphaFirst));

            _host.Place(_alphaFirst, _southAt);
            _host.Steps(_moreThanExit);

            Assert.That(_host.Ended, Is.True);
            Assert.That(_host.Winner, Is.EqualTo((Team?)Team.Alpha));
            Assert.That(_rules.CurrentRoundPhase(), Is.EqualTo(RoundPhase.Ended));
            Assert.That(_rules.RemainingSeconds, Is.Zero);
            Assert.That(_host.AlphaScore, Is.EqualTo(2));
            Assert.That(_host.BravoScore, Is.Zero);
            var log = _log.ToString();
            Assert.That(log, Does.Contain("[Vip] round_end round=2 winner=0 role=defender reason=Escaped wins=0:2,1:0"));
            Assert.That(log, Does.Contain("[Vip] ended winner=0"));
            Assert.That(_host.Session.GetMaxHealth(_alphaFirst), Is.EqualTo(_host.MaxHealth));
        }

        [Test]
        public void AVipWhoLeavesIsReplacedFromTheDefenders()
        {
            Begin();

            _host.Leave(_bravoFirst);

            Assert.That(_rules.Game.VipId, Is.EqualTo((int?)_bravoSecond));
            Assert.That(_host.Session.GetMaxHealth(_bravoSecond), Is.EqualTo(_vipMaxHealth));
            var log = _log.ToString();
            Assert.That(log, Does.Contain("[Vip] chosen round=1 vip=4 maxHealth=250"));
            Assert.That(log, Does.Not.Contain("[Vip] body restored player=3"), "the body of a player who left is discarded");
        }

        [Test]
        public void APlayerWhoChangesTeamBetweenMatchesMovesToTheNewSide()
        {
            Begin(winsToWin: 1);
            _host.Steps(_moreThanRound);
            Assert.That(_host.Ended, Is.True);

            Assert.That(_host.Session.ChangeTeam(_bravoFirst, Team.Alpha), Is.True);
            _rules.PlayerJoined(_bravoFirst);

            Assert.That(_rules.Game.Mode.GetSide(_bravoFirst), Is.EqualTo(Side.Alpha));
            Assert.That(_rules.Game.VipId, Is.Null, "no vip is chosen after the match");
            Assert.That(_host.Session.GetMaxHealth(_bravoFirst), Is.EqualTo(_host.MaxHealth));
        }

        [Test]
        public void BotsHeadForTheExitOrTheVip()
        {
            Begin();
            var vipAt = new Vector3(20f, 0f, 20f);
            _host.Place(_bravoFirst, vipAt);
            _host.Place(_bravoSecond, new Vector3(25f, 0f, 25f));
            _host.Place(_alphaFirst, new Vector3(-20f, 0f, -20f));

            Assert.That(_rules.BotGoal(_bravoFirst), Is.EqualTo((Vector3?)_northAt), "the vip heads for the nearest exit");
            Assert.That(_rules.BotGoal(_bravoSecond), Is.EqualTo((Vector3?)vipAt), "a defender follows the vip");
            Assert.That(_rules.BotGoal(_alphaFirst), Is.EqualTo((Vector3?)vipAt), "an attacker chases the vip");

            _host.Place(_bravoFirst, vipAt + new Vector3(1f, 0f, 0f));
            Assert.That(_rules.BotGoal(_alphaFirst), Is.EqualTo((Vector3?)vipAt), "the attacker is still far from the held goal");

            _host.Place(_alphaFirst, new Vector3(18f, 0f, 18f));
            _host.Place(_bravoFirst, new Vector3(0f, 0f, -20f));
            Assert.That(_rules.BotGoal(_bravoFirst), Is.EqualTo((Vector3?)_southAt), "the nearest exit changes with the vip");
            Assert.That(_rules.BotGoal(_alphaFirst), Is.EqualTo((Vector3?)new Vector3(0f, 0f, -20f)), "the vip moved farther than the bot is from the goal");
            Assert.That(_rules.BotGoal(99), Is.Null, "a player outside the match has no goal");
        }

        [Test]
        public void BotsHaveNoGoalWhileTheVipIsDown()
        {
            Begin(winsToWin: 1);

            _host.Kill(_alphaFirst, _bravoFirst);

            Assert.That(_rules.BotGoal(_alphaSecond), Is.Null);
        }

        [Test]
        public void ANextMatchRestoresTheBodyAndChoosesAgain()
        {
            Begin();
            _host.Steps(_moreThanRound);
            _host.Place(_alphaFirst, _southAt);
            _host.Steps(_moreThanExit);
            Assert.That(_host.Ended, Is.True);

            _host.Session.Restart();

            Assert.That(_rules.Game.VipId, Is.Null);
            Assert.That(_rules.Game.Phase, Is.EqualTo(RoundPhase.Waiting));
            Assert.That(_host.AlphaScore, Is.Zero);
            Assert.That(_host.BravoScore, Is.Zero);
            Assert.That(_log.ToString(), Does.Contain("[Vip] restart"));

            _host.Steps(1);

            Assert.That(_rules.Game.Defender, Is.EqualTo(Side.Bravo));
            Assert.That(_rules.Game.VipId, Is.EqualTo((int?)_bravoFirst));
            Assert.That(_host.Session.GetMaxHealth(_bravoFirst), Is.EqualTo(_vipMaxHealth));
            Assert.That(_host.Session.GetMaxHealth(_alphaFirst), Is.EqualTo(_host.MaxHealth));
        }

        [Test]
        public void StoppingRestoresTheVipBody()
        {
            Begin();

            _rules.Stop();

            Assert.That(_host.Session.GetMaxHealth(_bravoFirst), Is.EqualTo(_host.MaxHealth));
            Assert.That(_log.ToString(), Does.Contain("[Vip] body restored player=3"));
        }

        [TestCase(VipRoundEnd.Escaped, RoundEndReason.Defused)]
        [TestCase(VipRoundEnd.VipDown, RoundEndReason.Detonated)]
        [TestCase(VipRoundEnd.TimeUp, RoundEndReason.TimeUp)]
        [TestCase(VipRoundEnd.AttackersEliminated, RoundEndReason.AttackersEliminated)]
        [TestCase(VipRoundEnd.DefendersEliminated, RoundEndReason.DefendersEliminated)]
        [TestCase(VipRoundEnd.Finished, RoundEndReason.BothEliminated)]
        public void EachRoundEndIsSentAsTheNearestRoundEndReason(VipRoundEnd reason, RoundEndReason sent)
        {
            Assert.That(VipRules.EndReason(reason), Is.EqualTo(sent));
        }

        [Test]
        public void AnUndefinedRoundEndIsRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => VipRules.EndReason((VipRoundEnd)99));
        }

        [Test]
        public void EventsWithoutASessionAreIgnored()
        {
            var rules = new VipRules(Settings(), new MatchHost(_log), PickFirst);

            Assert.DoesNotThrow(() => rules.PlayerJoined(1), "there is no Session yet");
            Assert.DoesNotThrow(() => rules.PlayerKilled(1, 2));
            Assert.DoesNotThrow(() => rules.PlayerLeft(2));
            Assert.That(rules.Tick(true, 1f), Is.False, "the rules do not run before Start");
            Assert.That(rules.Game.HasPlayer(1), Is.False);
        }

        [Test]
        public void TheConstructorRefusesMissingParts()
        {
            Assert.Throws<ArgumentNullException>(() => new VipRules(null, new MatchHost(_log)));
            Assert.Throws<ArgumentNullException>(() => new VipRules(Settings(), null));
            var rules = new VipRules(Settings(), new MatchHost(_log));
            Assert.Throws<ArgumentNullException>(() => rules.CreateSessionSpec(null, Settings()));
            Assert.Throws<ArgumentNullException>(() => rules.CreateSessionSpec(GameRules.Default, null));
            Assert.Throws<ArgumentNullException>(() => rules.CheckMap(null, Points()));
            Assert.Throws<ArgumentNullException>(() => rules.UseMap(Map(), null));
            Assert.Throws<ArgumentNullException>(() => rules.FillControlPoints(null));
            Assert.Throws<ArgumentNullException>(() => rules.FillObjectiveItems(null));
        }

        // 守る側のいちばん前の人を選ぶ。候補は Id の昇順に並ぶので、Bravo なら 3 番、Alpha なら 1 番
        private static int PickFirst(int count) => 0;

        private static SnapshotList<ObjectiveItemSnapshot> Items()
        {
            return new SnapshotList<ObjectiveItemSnapshot>(SnapshotMessage.MaxObjectiveItems, "objective item");
        }

        private static NetMatchSettings Settings(int winsToWin = _winsToWin, int swapEveryRounds = 1)
        {
            var spec = new VipSpec(_vipMaxHealth, _roundSeconds, winsToWin, _exitHoldSeconds, swapEveryRounds);
            return new NetMatchSettings(respawnSeconds: 1f, vip: spec);
        }

        // 書き出しの順に左右されず Id の順 (north, south) に並ぶことも確かめるため、逆に並べる
        private static NetMapPoints Points()
        {
            return new NetMapPoints(
                new[] { new SpawnPoint(_alphaSpawn, Team.Alpha), new SpawnPoint(_bravoSpawn, Team.Bravo) },
                Array.Empty<ControlPointArea>(),
                extractionPoints: new[]
                {
                    new NetMapExtractionPoint(_south, _southAt, _exitRadius),
                    new NetMapExtractionPoint(_north, _northAt, _exitRadius),
                });
        }

        private static ServerMapData Map() => TestMaps.Data("NetTest", Points());

        private static GameRules Plain()
        {
            return GameRules.Default
                .WithActions(ActionRules.Default.WithDowned(Downed.Off))
                .WithSpawnProtectionSeconds(0f)
                .WithIdleKickSeconds(0f)
                .WithRegenPerSecond(0f);
        }

        private void Begin(int winsToWin = _winsToWin, int swapEveryRounds = 1)
        {
            var settings = Settings(winsToWin, swapEveryRounds);
            _host = new MatchHost(_log);
            _rules = new VipRules(settings, _host, PickFirst);
            _host.Begin(_rules, Plain(), settings);
            _rules.Start();
            _host.Join(_alphaFirst, Team.Alpha);
            _host.Join(_alphaSecond, Team.Alpha);
            _host.Join(_bravoFirst, Team.Bravo);
            _host.Join(_bravoSecond, Team.Bravo);
            _host.Steps(1);
        }

        // 目的だけを確かめるので、MatchRulesModule が目的を回す順 (UseMap の後に Session を作り、Session の Tick の後に目的の Tick、
        // 目的が終えたら DecideWinner の勝者で Session を Finish) と出来事の渡し方をここでなぞる (JuggernautRulesTests と同じ)
        private sealed class MatchHost : IMatchObjectiveHost
        {
            private const float _tickSeconds = 1f / 60f;

            private VipRules _rules;
            private long _tick;

            public MatchHost(TextWriter log)
            {
                Log = log;
                Bodies = new ServerBodies(NetMotorSettings.Default.Stance);
            }

            public TeamDeathmatchSession Session { get; private set; }

            public ServerBodies Bodies { get; }

            public TextWriter Log { get; }

            public int MaxHealth { get; private set; }

            public bool Ended { get; private set; }

            public Team? Winner { get; private set; }

            public int AlphaScore { get; private set; }

            public int BravoScore { get; private set; }

            public void Begin(VipRules rules, GameRules gameRules, NetMatchSettings settings)
            {
                _rules = rules;
                MaxHealth = settings.MaxHealth;
                var map = Map();
                rules.UseMap(map, map.Points);
                Session = new TeamDeathmatchSession(
                    rules.CreateSessionSpec(gameRules, settings),
                    settings.MaxHealth,
                    map.Points.SpawnPoints,
                    new SpawnSelector(settings.MinEnemySpawnDistance),
                    PositionOf,
                    null,
                    settings.ReconnectGraceSeconds);
                Session.Spawned += (playerId, point) => Bodies.Teleport(playerId, point.Position, 0f, _tick);
                Session.Killed += (killerId, victimId, _) => _rules.PlayerKilled(killerId, victimId);
                Session.Restarted += () =>
                {
                    Ended = false;
                    Winner = null;
                    _rules.MatchRestarted();
                };
                Session.Ended += winner =>
                {
                    Ended = true;
                    Winner = _rules.DecideWinner(winner);
                    _rules.MatchEnded();
                };
            }

            public void Join(int playerId, Team team)
            {
                Bodies.Add(playerId);
                Session.Join(playerId, "device" + playerId, team);
                _rules.PlayerJoined(playerId);
            }

            // MatchRulesModule.HandlePlayerLeft と同じく、目的に知らせてから Session から抜く
            public void Leave(int playerId)
            {
                _rules.PlayerLeft(playerId);
                Session.Leave(playerId);
                Bodies.Remove(playerId);
            }

            public void Place(int playerId, Vector3 position)
            {
                Assert.That(Bodies.TryGet(playerId, out var body), Is.True);
                body.SetPose(position, 0f, PlayerStance.Standing);
            }

            public void Kill(int attackerId, int victimId)
            {
                Assert.That(Session.ApplyDamage(attackerId, victimId, _lethal), Is.EqualTo(DamageOutcome.Killed));
            }

            public void Steps(int count)
            {
                for (var i = 0; i < count; i++)
                {
                    _tick++;
                    Session.Tick(_tickSeconds);
                    if (_rules.Tick(Session.State == GameModeState.Playing, _tickSeconds))
                    {
                        Session.Finish(_rules.DecideWinner(Session.Winner));
                    }
                }
            }

            public void SetMatchScores(int alpha, int bravo)
            {
                AlphaScore = alpha;
                BravoScore = bravo;
            }

            public void ClearDowned()
            {
            }

            private Vector3 PositionOf(int playerId)
            {
                return Bodies.TryGet(playerId, out var body) ? body.Position : Vector3.Zero;
            }
        }
    }
}

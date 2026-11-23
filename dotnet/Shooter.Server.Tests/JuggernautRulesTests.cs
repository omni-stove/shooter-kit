using System;
using System.IO;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    public sealed class JuggernautRulesTests
    {
        private const int _alphaFirst = 1;
        private const int _alphaSecond = 2;
        private const int _bravoFirst = 3;
        private const int _bravoSecond = 4;
        private const int _lethal = 1000;
        private const int _juggernautMaxHealth = 250;
        private const float _juggernautSpeed = 1.25f;
        private const int _scorePerKillAsJuggernaut = 1;
        private const int _scorePerJuggernautKill = 3;
        private const int _scoreLimit = 5;
        private const float _timeLimitSeconds = 60f;
        private const float _startDelaySeconds = 1f;
        private const float _respawnSeconds = 1f;

        // 1 tick は 1/60 秒。選ぶまでの 1 秒と出直しの 1 秒は 60 tick で、61 tick と 70 tick で必ず越える
        private const int _ticksToChoose = 61;
        private const int _moreThanRespawn = 70;

        private StringWriter _log;
        private MatchHost _host;
        private JuggernautRules _rules;

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
        public void TheObjectiveKeepsItsScoreAndKeepsTheSessionFromEndingFirst()
        {
            var settings = Settings();
            var rules = new JuggernautRules(settings, new MatchHost(_log), PickFirst);

            Assert.That(rules.Objective, Is.EqualTo(MatchObjective.Juggernaut));
            Assert.That(rules.PlaysInRounds, Is.False);
            Assert.That(rules.KeepsMatchScore, Is.True);
            Assert.That(rules.HoldsFire, Is.False);
            Assert.That(rules.CurrentRoundPhase(), Is.Null);
            Assert.That(rules.RemainingSeconds, Is.EqualTo(_timeLimitSeconds));
            Assert.That(rules.DecideWinner(Team.Bravo), Is.Null, "one player wins, not a team");
            Assert.That(rules.Tick(false, 1f), Is.False);
            Assert.That(rules.Juggernaut.Session.State, Is.EqualTo(GameModeState.Waiting), "the start delay does not run before the match plays");

            var spec = rules.CreateSessionSpec(GameRules.Default, settings);
            Assert.That(spec.ScoreLimit, Is.EqualTo(int.MaxValue));
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(float.MaxValue));
            Assert.That(spec.RespawnSeconds, Is.EqualTo(_respawnSeconds));
            Assert.That(spec.Rules.FriendlyFire, Is.EqualTo(FriendlyFire.On), "teammates can take the juggernaut over");
            Assert.That(rules.AreHostile(_alphaFirst, Team.Alpha, _alphaSecond, Team.Alpha), Is.True);
            Assert.That(rules.AreHostile(_alphaFirst, Team.Alpha, _alphaFirst, Team.Alpha), Is.False);
            Assert.That(((IMatchObjectiveRules)rules).AreAllies(_alphaFirst, Team.Alpha, _alphaSecond, Team.Alpha), Is.False);
        }

        [Test]
        public void StartingLogsTheRespawnAndTheJuggernautBody()
        {
            var rules = new JuggernautRules(Settings(), new MatchHost(_log), PickFirst);

            rules.Start();
            rules.Start();
            _rules = rules;

            Assert.That(_log.ToString(), Is.EqualTo(
                "[Juggernaut] net respawn=1 body maxHealth=250 speedMultiplier=1.25" + Environment.NewLine));
        }

        [Test]
        public void NobodyIsTheJuggernautUntilTheStartDelayEnds()
        {
            Begin(_ticksToChoose - 2);

            Assert.That(_rules.Juggernaut.JuggernautId, Is.Null);
            Assert.That(_rules.BotGoal(_bravoFirst), Is.Null);
            var items = Items();
            _rules.FillObjectiveItems(items);
            Assert.That(items.Count, Is.Zero);
            var snapshot = new MatchSnapshot();
            _rules.FillMatch(ref snapshot);
            Assert.That(snapshot.Objective, Is.EqualTo(MatchObjective.Juggernaut));
            Assert.That(snapshot.ObjectiveRemainingSeconds, Is.EqualTo(1));
            Assert.That(snapshot.AlphaPoints, Is.EqualTo(_scoreLimit));
            Assert.That(snapshot.BravoPoints, Is.Zero);
            Assert.That(_host.Session.GetMaxHealth(_alphaFirst), Is.EqualTo(_host.MaxHealth));
        }

        [Test]
        public void BeforeTheMatchPlaysTheSnapshotCountsDownTheWholeDelay()
        {
            var settings = new NetMatchSettings(juggernaut: new JuggernautSpec(startDelaySeconds: 2.5f));
            var rules = new JuggernautRules(settings, new MatchHost(_log), PickFirst);
            var snapshot = new MatchSnapshot();

            rules.FillMatch(ref snapshot);

            Assert.That(snapshot.ObjectiveRemainingSeconds, Is.EqualTo(3));
        }

        [Test]
        public void TheStartDelayChoosesTheJuggernautAndGivesThemTheBody()
        {
            Begin();
            _host.Place(_alphaFirst, new Vector3(3f, 1f, -4f));

            Assert.That(_rules.Juggernaut.JuggernautId, Is.EqualTo((int?)_alphaFirst));
            Assert.That(_host.Session.GetMaxHealth(_alphaFirst), Is.EqualTo(_juggernautMaxHealth));
            Assert.That(_host.Session.GetHealth(_alphaFirst), Is.EqualTo(_juggernautMaxHealth), "the juggernaut starts full");
            Assert.That(_host.Speed(_alphaFirst), Is.EqualTo(_juggernautSpeed));
            Assert.That(_host.Speed(_alphaSecond), Is.EqualTo(1f));
            Assert.That(_host.Session.GetMaxHealth(_alphaSecond), Is.EqualTo(_host.MaxHealth));
            Assert.That(_log.ToString(), Does.Contain("[Juggernaut] change player=1 from=- reason=chosen"));
            Assert.That(_log.ToString(), Does.Contain("[Juggernaut] juggernaut body player=1 maxHealth=250 speedMultiplier=1.25"));

            var snapshot = new MatchSnapshot();
            _rules.FillMatch(ref snapshot);
            Assert.That(snapshot.ObjectiveRemainingSeconds, Is.Zero);

            var items = Items();
            _rules.FillObjectiveItems(items);
            Assert.That(items.Count, Is.EqualTo(1));
            Assert.That(items[0].ItemId, Is.EqualTo(JuggernautRules.ItemId));
            Assert.That(items[0].Present, Is.EqualTo(ObjectiveItemFields.All));
            Assert.That(items[0].Kind, Is.EqualTo(ObjectiveItemKind.Juggernaut));
            Assert.That(items[0].Team, Is.EqualTo(Team.Alpha));
            Assert.That(items[0].HasOwner, Is.True);
            Assert.That(items[0].OwnerId, Is.EqualTo(_alphaFirst));
            Assert.That(items[0].State, Is.EqualTo(ObjectiveItemState.Carried));
            Assert.That(items[0].HasCarrier, Is.True);
            Assert.That(items[0].CarrierId, Is.EqualTo(_alphaFirst));
            Assert.That(items[0].Position, Is.EqualTo(new Vector3(3f, 1f, -4f)));
            Assert.That(items[0].Radius, Is.Zero);
        }

        // 確認方法の手順: 3 番がジャガーノートの 1 番を倒すと、ジャガーノートが 3 番に替わり、3 番に倒した点が入り、1 番の体が戻る
        [Test]
        public void KillingTheJuggernautHandsTheBodyAndTheScoreToTheKiller()
        {
            Begin();

            _host.Kill(_bravoFirst, _alphaFirst);

            Assert.That(_rules.Juggernaut.JuggernautId, Is.EqualTo((int?)_bravoFirst));
            Assert.That(_rules.GetScore(_bravoFirst), Is.EqualTo(_scorePerJuggernautKill));
            Assert.That(_rules.GetScore(_alphaFirst), Is.Zero);
            Assert.That(_host.Session.GetMaxHealth(_bravoFirst), Is.EqualTo(_juggernautMaxHealth));
            Assert.That(_host.Session.GetHealth(_bravoFirst), Is.EqualTo(_juggernautMaxHealth));
            Assert.That(_host.Speed(_bravoFirst), Is.EqualTo(_juggernautSpeed));
            Assert.That(_host.Session.GetMaxHealth(_alphaFirst), Is.EqualTo(_host.MaxHealth));
            Assert.That(_host.Speed(_alphaFirst), Is.EqualTo(1f));
            Assert.That(_host.AlphaScore, Is.EqualTo(_scorePerJuggernautKill), "the leading score");
            Assert.That(_host.BravoScore, Is.EqualTo(_scorePerJuggernautKill), "the juggernaut's score");

            var log = _log.ToString();
            Assert.That(log, Does.Contain("[Juggernaut] change player=3 from=1 reason=taken-over"));
            Assert.That(log, Does.Contain("[Juggernaut] body restored player=1"));
            Assert.That(log, Does.Contain("[Juggernaut] juggernaut body player=3 maxHealth=250 speedMultiplier=1.25"));
            Assert.That(log, Does.Contain("[Juggernaut] score leading=3 juggernaut=3"));

            var killer = new PlayerSnapshot { PlayerId = _bravoFirst, Kills = 9 };
            _rules.FillPlayer(_bravoFirst, ref killer);
            Assert.That(killer.Kills, Is.EqualTo(1));
            var victim = new PlayerSnapshot { PlayerId = _alphaFirst };
            _rules.FillPlayer(_alphaFirst, ref victim);
            Assert.That(victim.Deaths, Is.EqualTo(1));
        }

        [Test]
        public void ATeammateCanTakeTheJuggernautOver()
        {
            Begin();

            _host.Kill(_alphaSecond, _alphaFirst);

            Assert.That(_rules.Juggernaut.JuggernautId, Is.EqualTo((int?)_alphaSecond));
            Assert.That(_rules.GetScore(_alphaSecond), Is.EqualTo(_scorePerJuggernautKill));
        }

        [Test]
        public void OnlyTheJuggernautsKillsAndKillsOfTheJuggernautScore()
        {
            Begin();

            _host.Kill(_bravoFirst, _alphaSecond);
            Assert.That(_rules.GetScore(_bravoFirst), Is.Zero, "a kill between hunters scores nothing");

            _host.Kill(_alphaFirst, _bravoSecond);
            Assert.That(_rules.GetScore(_alphaFirst), Is.EqualTo(_scorePerKillAsJuggernaut));
            Assert.That(_rules.Juggernaut.JuggernautId, Is.EqualTo((int?)_alphaFirst), "the juggernaut stays after a kill");
            Assert.That(_host.AlphaScore, Is.EqualTo(_scorePerKillAsJuggernaut));
            Assert.That(_host.BravoScore, Is.EqualTo(_scorePerKillAsJuggernaut));
        }

        [Test]
        public void AJuggernautDownedByNobodyInTheMatchIsRepicked()
        {
            Begin();

            _rules.PlayerKilled(99, _alphaFirst);

            Assert.That(_rules.Juggernaut.JuggernautId, Is.EqualTo((int?)_alphaSecond));
            Assert.That(_log.ToString(), Does.Contain("[Juggernaut] change player=2 from=1 reason=repicked"));
            Assert.That(_host.Speed(_alphaFirst), Is.EqualTo(1f));
            Assert.That(_host.Speed(_alphaSecond), Is.EqualTo(_juggernautSpeed));
        }

        // 確認方法の手順: ジャガーノートの 3 番が点の上限に届くと試合が終わり、勝者の行が出る。勝ったかはチームでなく人で決まる
        [Test]
        public void ReachingTheScoreLimitEndsTheMatchWithOneWinner()
        {
            Begin();
            _host.Kill(_bravoFirst, _alphaFirst);
            _host.Steps(_moreThanRespawn);
            _host.Kill(_bravoFirst, _alphaFirst);
            _host.Kill(_bravoFirst, _alphaSecond);
            Assert.That(_rules.GetScore(_bravoFirst), Is.EqualTo(_scoreLimit));

            _host.Steps(1);

            Assert.That(_host.Ended, Is.True);
            Assert.That(_host.Winner, Is.Null, "the snapshot names no winning team");
            Assert.That(_rules.WinnerId, Is.EqualTo((int?)_bravoFirst));
            Assert.That(_rules.IsWinningPlayer(_bravoFirst, Team.Bravo, null), Is.True);
            Assert.That(_rules.IsWinningPlayer(_bravoSecond, Team.Bravo, Team.Bravo), Is.False, "the winner's teammate did not win");
            Assert.That(_rules.IsWinningPlayer(_alphaFirst, Team.Alpha, Team.Alpha), Is.False);
            Assert.That(_host.AlphaScore, Is.EqualTo(_scoreLimit));
            Assert.That(_host.BravoScore, Is.EqualTo(_scoreLimit));
            Assert.That(_log.ToString(), Does.Contain("[Juggernaut] winner player=3 score=5"));

            _host.Leave(_bravoFirst);
            Assert.That(_rules.WinnerId, Is.EqualTo((int?)_bravoFirst), "leaving after the end keeps the winner");
        }

        [Test]
        public void AtTheTimeLimitTheHighestScoreWins()
        {
            Begin(timeLimitSeconds: 3f);
            _host.Kill(_alphaFirst, _bravoFirst);

            _host.Steps(60 * 3);

            Assert.That(_host.Ended, Is.True);
            Assert.That(_rules.WinnerId, Is.EqualTo((int?)_alphaFirst));
            Assert.That(_log.ToString(), Does.Contain("[Juggernaut] winner player=1 score=1"));
        }

        [Test]
        public void ATimeLimitWithoutPointsIsADraw()
        {
            Begin(timeLimitSeconds: 3f);

            _host.Steps(60 * 3);

            Assert.That(_host.Ended, Is.True);
            Assert.That(_rules.WinnerId, Is.Null);
            Assert.That(_rules.IsWinningPlayer(_alphaFirst, Team.Alpha, null), Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Juggernaut] winner=draw"));
        }

        [Test]
        public void AJuggernautWhoLeavesIsReplacedAtOnce()
        {
            Begin();

            _host.Leave(_alphaFirst);

            Assert.That(_rules.Juggernaut.JuggernautId, Is.EqualTo((int?)_alphaSecond));
            Assert.That(_host.Session.GetMaxHealth(_alphaSecond), Is.EqualTo(_juggernautMaxHealth));
            Assert.That(_host.Speed(_alphaSecond), Is.EqualTo(_juggernautSpeed));
            var log = _log.ToString();
            Assert.That(log, Does.Contain("[Juggernaut] change player=2 from=1 reason=left"));
            Assert.That(log, Does.Not.Contain("[Juggernaut] body restored player=1"), "the body of a player who left is discarded");
            Assert.That(_rules.GetScore(_alphaFirst), Is.Zero);
        }

        [Test]
        public void APlayerWhoComesBackKeepsTheirScoreAndKills()
        {
            Begin();
            _host.Kill(_bravoFirst, _alphaFirst);
            _host.Leave(_bravoFirst);

            _host.Join(_bravoFirst, Team.Bravo);

            Assert.That(_rules.GetScore(_bravoFirst), Is.EqualTo(_scorePerJuggernautKill));
            var row = new PlayerSnapshot { PlayerId = _bravoFirst };
            _rules.FillPlayer(_bravoFirst, ref row);
            Assert.That(row.Kills, Is.EqualTo(1));
            Assert.That(_rules.Juggernaut.IsJuggernaut(_bravoFirst), Is.False, "coming back does not return the juggernaut");
        }

        [Test]
        public void BotsHeadForTheJuggernautAndHoldTheGoalWhileItMovesLittle()
        {
            Begin();
            _host.Place(_alphaFirst, new Vector3(10f, 0f, 0f));

            Assert.That(_rules.BotGoal(_bravoFirst), Is.EqualTo((Vector3?)new Vector3(10f, 0f, 0f)));
            Assert.That(_rules.BotGoal(_alphaFirst), Is.Null, "the juggernaut has no goal");

            _host.Place(_alphaFirst, new Vector3(11f, 0f, 0f));
            Assert.That(_rules.BotGoal(_bravoFirst), Is.EqualTo((Vector3?)new Vector3(10f, 0f, 0f)), "the bot is still far from the held goal");

            _host.Place(_bravoFirst, new Vector3(10f, 0f, 0.5f));
            Assert.That(_rules.BotGoal(_bravoFirst), Is.EqualTo((Vector3?)new Vector3(11f, 0f, 0f)), "the juggernaut moved farther than the bot is from the goal");

            _host.Kill(_bravoSecond, _alphaFirst);
            _host.Place(_bravoSecond, new Vector3(-5f, 0f, 0f));
            Assert.That(_rules.BotGoal(_bravoFirst), Is.EqualTo((Vector3?)new Vector3(-5f, 0f, 0f)), "a new juggernaut is a new goal");
            Assert.That(_rules.BotGoal(_bravoSecond), Is.Null);
        }

        [Test]
        public void ANextMatchRestoresTheBodyAndChoosesAgain()
        {
            Begin(timeLimitSeconds: 3f);
            _host.Kill(_bravoFirst, _alphaFirst);
            _host.Steps(60 * 3);
            Assert.That(_host.Ended, Is.True);

            _host.Session.Restart();

            Assert.That(_rules.Juggernaut.JuggernautId, Is.Null);
            Assert.That(_rules.WinnerId, Is.Null);
            Assert.That(_rules.GetScore(_bravoFirst), Is.Zero);
            Assert.That(_host.Speed(_bravoFirst), Is.EqualTo(1f));
            Assert.That(_host.Session.GetMaxHealth(_bravoFirst), Is.EqualTo(_host.MaxHealth));
            Assert.That(_host.AlphaScore, Is.Zero);
            Assert.That(_host.BravoScore, Is.Zero);
            Assert.That(_log.ToString(), Does.Contain("[Juggernaut] restart"));

            _host.Steps(_ticksToChoose);

            Assert.That(_rules.Juggernaut.JuggernautId, Is.EqualTo((int?)_alphaFirst));
            Assert.That(_host.Speed(_alphaFirst), Is.EqualTo(_juggernautSpeed));
        }

        [Test]
        public void StoppingRestoresTheJuggernautBody()
        {
            Begin();

            _rules.Stop();

            Assert.That(_host.Speed(_alphaFirst), Is.EqualTo(1f));
            Assert.That(_host.Session.GetMaxHealth(_alphaFirst), Is.EqualTo(_host.MaxHealth));
            Assert.That(_log.ToString(), Does.Contain("[Juggernaut] body restored player=1"));
        }

        [Test]
        public void APickOutsideThePlayersIsRefused()
        {
            _host = new MatchHost(_log);
            _rules = new JuggernautRules(Settings(), _host, count => count);
            _host.Begin(_rules, Plain(), Settings());
            _rules.Start();
            _host.Join(_alphaFirst, Team.Alpha);
            _host.Join(_bravoFirst, Team.Bravo);

            Assert.Throws<InvalidOperationException>(() => _host.Steps(_ticksToChoose));
        }

        [Test]
        public void EventsWithoutASessionAreIgnored()
        {
            var rules = new JuggernautRules(Settings(), new MatchHost(_log), PickFirst);

            Assert.DoesNotThrow(() => rules.PlayerJoined(1), "there is no Session yet");
            Assert.DoesNotThrow(() => rules.PlayerKilled(1, 2));
            Assert.DoesNotThrow(() => rules.PlayerLeft(2));
            Assert.That(rules.Juggernaut.Session.PlayerCount, Is.Zero);
            Assert.That(rules.GetScore(1), Is.Zero);
            Assert.That(rules.IsWinningPlayer(1, Team.Alpha, Team.Alpha), Is.False);
        }

        [Test]
        public void TheConstructorRefusesMissingParts()
        {
            Assert.Throws<ArgumentNullException>(() => new JuggernautRules(null, new MatchHost(_log)));
            Assert.Throws<ArgumentNullException>(() => new JuggernautRules(Settings(), null));
            var rules = new JuggernautRules(Settings(), new MatchHost(_log));
            Assert.Throws<ArgumentNullException>(() => rules.CreateSessionSpec(null, Settings()));
            Assert.Throws<ArgumentNullException>(() => rules.CreateSessionSpec(GameRules.Default, null));
            Assert.Throws<ArgumentNullException>(() => rules.FillControlPoints(null));
            Assert.Throws<ArgumentNullException>(() => rules.FillObjectiveItems(null));
        }

        // 人数のいちばん前の人を選ぶ。立っている人は Id の昇順に並ぶので、1 番・2 番の順に選ぶ
        private static int PickFirst(int count) => 0;

        private static SnapshotList<ObjectiveItemSnapshot> Items()
        {
            return new SnapshotList<ObjectiveItemSnapshot>(SnapshotMessage.MaxObjectiveItems, "objective item");
        }

        private static NetMatchSettings Settings(float timeLimitSeconds = _timeLimitSeconds)
        {
            var spec = new JuggernautSpec(
                _juggernautMaxHealth,
                _juggernautSpeed,
                _scorePerKillAsJuggernaut,
                _scorePerJuggernautKill,
                _scoreLimit,
                timeLimitSeconds,
                _startDelaySeconds);
            return new NetMatchSettings(respawnSeconds: _respawnSeconds, juggernaut: spec);
        }

        private static GameRules Plain()
        {
            return GameRules.Default
                .WithActions(ActionRules.Default.WithDowned(Downed.Off))
                .WithSpawnProtectionSeconds(0f)
                .WithIdleKickSeconds(0f)
                .WithRegenPerSecond(0f);
        }

        private void Begin(int ticks = _ticksToChoose, float timeLimitSeconds = _timeLimitSeconds)
        {
            var settings = Settings(timeLimitSeconds);
            _host = new MatchHost(_log);
            _rules = new JuggernautRules(settings, _host, PickFirst);
            _host.Begin(_rules, Plain(), settings);
            _rules.Start();
            _host.Join(_alphaFirst, Team.Alpha);
            _host.Join(_alphaSecond, Team.Alpha);
            _host.Join(_bravoFirst, Team.Bravo);
            _host.Join(_bravoSecond, Team.Bravo);
            _host.Steps(ticks);
        }

        // 目的だけを確かめるので、MatchRulesModule が目的を回す順 (Session の Tick の後に目的の Tick、
        // 目的が終えたら DecideWinner の勝者で Session を Finish) と出来事の渡し方をここでなぞる (GuardianRulesTests と同じ)
        private sealed class MatchHost : IMatchObjectiveHost
        {
            private const float _tickSeconds = 1f / 60f;

            private JuggernautRules _rules;
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

            public void Begin(JuggernautRules rules, GameRules gameRules, NetMatchSettings settings)
            {
                _rules = rules;
                MaxHealth = settings.MaxHealth;
                Session = new TeamDeathmatchSession(
                    rules.CreateSessionSpec(gameRules, settings),
                    settings.MaxHealth,
                    TestRules.Points().SpawnPoints,
                    new SpawnSelector(settings.MinEnemySpawnDistance),
                    PositionOf,
                    null,
                    settings.ReconnectGraceSeconds,
                    areAllies: (a, b) => ((IMatchObjectiveRules)_rules).AreAllies(a, Session.GetTeam(a), b, Session.GetTeam(b)));
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

            public float Speed(int playerId)
            {
                Assert.That(Bodies.TryGet(playerId, out var body), Is.True);
                return body.MoveSpeedMultiplier;
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

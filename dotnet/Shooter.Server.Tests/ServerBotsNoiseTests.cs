using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using GameKit.Net;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // 移動・ルール・戦闘・Bot を MatchServer と同じ順で載せ、ドアの蹴り・爆薬の音 (ServerNoises) を Bot が聞いて調べに行くかを確かめる
    public sealed class ServerBotsNoiseTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private static readonly DateTime _time = new DateTime(2026, 11, 25, 9, 0, 0, DateTimeKind.Utc);

        // 床の上面は y=0。人 (Alpha) は x=-6、Bot (Bravo) は x=6 に出る。音は人の 2 m 前 (Bot から約 10 m) で鳴らす。
        // Bot の見える距離は 5 m にし、人を見つけて交戦に入らないようにする
        private static readonly Vector3 _noiseAt = new Vector3(-4f, 0f, 0f);

        private StringWriter _log;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerHost _host;
        private ServerBots _bots;
        private ServerNoises _noises;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _transport = new RecordingTransport();
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null && _host.State == ServerHostState.Running)
            {
                _host.Stop();
            }

            _transport.Dispose();
            _log.Dispose();
        }

        [Test]
        public void ABotOutOfSightHearsAKickOnceAndSearchesWhereItWas()
        {
            Start();
            Join(1);
            Step(0.2f);
            var bot = _bots.BotIds.Single();

            _noises.Emit(1, new SoundStimulus(SoundCueKind.Clatter, _noiseAt, BreachSpec.DefaultKickSoundRadius, false));
            Step(0.3f);

            var heard = "[Bot] noise player=" + Text(bot) + " kind=Clatter by=1 x=-4.00 y=0.00 z=0.00";
            Assert.That(_log.ToString().Split(heard).Length - 1, Is.EqualTo(1), "one kick is heard once: " + _log);
            Assert.That(_bots.TryGetAgent(bot, out var agent), Is.True);
            Assert.That(agent.Brain.State, Is.EqualTo(EnemyState.Search));
        }

        [Test]
        public void ABotBeyondTheRadiusDoesNotHearIt()
        {
            Start();
            Join(1);
            Step(0.2f);
            var bot = _bots.BotIds.Single();

            _noises.Emit(1, new SoundStimulus(SoundCueKind.Clatter, _noiseAt, 5f, false));
            Step(0.3f);

            Assert.That(_log.ToString(), Does.Not.Contain("[Bot] noise player="));
            Assert.That(_bots.TryGetAgent(bot, out var agent), Is.True);
            Assert.That(agent.Brain.State, Is.Not.EqualTo(EnemyState.Search));
        }

        [Test]
        public void AChargeWhosePlanterLeftIsHeardByEveryBot()
        {
            Start(4);
            Join(1);
            Step(0.2f);

            _noises.Emit(PlayerIds.None, new SoundStimulus(SoundCueKind.Explosion, _noiseAt, BreachSpec.DefaultChargeSoundRadius, false));
            Step(0.1f);

            foreach (var bot in _bots.BotIds)
            {
                Assert.That(_log.ToString(), Does.Contain("[Bot] noise player=" + Text(bot) + " kind=Explosion by=none x=-4.00 y=0.00 z=0.00"));
            }
        }

        [Test]
        public void ABotDoesNotHearTheKickOfItsOwnTeam()
        {
            // 4 人に満たすので、人と同じ Alpha に Bot が 1 人、Bravo に 2 人入る
            Start(4);
            Join(1);
            Step(0.2f);
            var teammate = _bots.BotIds.Single(id => _match.TryGetPlayer(id, out var p) && p.Team == Team.Alpha);
            var enemy = _bots.BotIds.First(id => _match.TryGetPlayer(id, out var p) && p.Team == Team.Bravo);

            _noises.Emit(1, new SoundStimulus(SoundCueKind.Clatter, _noiseAt, BreachSpec.DefaultKickSoundRadius, false));
            Step(0.1f);

            Assert.That(_log.ToString(), Does.Not.Contain("[Bot] noise player=" + Text(teammate) + " "));
            Assert.That(_log.ToString(), Does.Contain("[Bot] noise player=" + Text(enemy) + " kind=Clatter by=1"));
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static NetBotSpec SightOf(float range)
        {
            var defaults = NetBotSpec.Default;
            return new NetBotSpec(
                new EnemySpec(range, Math.Min(range, defaults.Brain.AttackRange), defaults.Brain.LoseSightSeconds, defaults.Brain.SearchSeconds, defaults.Brain.ReactionSeconds),
                defaults.Input,
                defaults.NavSampleRadius,
                defaults.RepathSeconds,
                defaults.RepathDistance,
                defaults.StuckSeconds,
                defaults.StuckProgress);
        }

        private void Start(int bots = 2)
        {
            var actions = ActionRules.Default.WithDowned(Downed.Off);
            var rules = GameRules.Default.WithActions(actions).WithSpawnProtectionSeconds(0f).WithIdleKickSeconds(0f).WithRegenPerSecond(0f);
            var data = new ServerRulesData("TeamDeathmatch", TestRules.File(rules), PingSpec.Default, "memory", null, bots: SightOf(5f));
            var map = BotTestMaps.Flat(BotTestMaps.Points(new Vector3(-6f, 1.1f, 0f), new Vector3(6f, 1.1f, 0f)));
            _match = new ServerMatch(NetMatchSettings.DefaultRestartDelaySeconds, _log, new ServerEventWriter(false, TextWriter.Null, _log), () => _time);
            var options = new NetLaunchOptions(
                NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort, NetTransportKind.Udp, null, null, null, bots, 0, 0, 0, 0);
            var context = new ServerContext(options, "1.0.0", _log, map, _match, new ServerMessenger(_transport), new StubControl(), data);
            var movement = new ServerMovement(ServerModules.MotorSettingsFrom(data));
            var matchRules = new MatchRulesModule();
            var combat = new ServerCombat(matchRules, movement);
            _noises = new ServerNoises();
            _bots = new ServerBots(matchRules, movement, combat, noises: _noises);
            _host = new ServerHost();
            _host.Register(movement);
            _host.Register(matchRules);
            _host.Register(combat);
            _host.Register(_bots);
            _host.Start(context);
        }

        private void Join(int id)
        {
            var connection = new ConnectionId((ulong)id);
            _match.Join(connection, "device" + id);
            _host.OnConnected(connection);
        }

        private void Step(float seconds)
        {
            for (var elapsed = 0f; elapsed < seconds; elapsed += _tickSeconds)
            {
                _match.Tick(_tickSeconds);
                _host.Tick(_tickSeconds);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // 移動・ルール・戦闘・しまう動作を同じホストで動かし、要求したしまう・構えるが武器の秒数で進み、その間の射撃とリロードを断り、
    // 倒れる・持ち替えると構えに戻り、スナップショットに載ることを確かめる
    public sealed class ServerHolsterTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;

        private static readonly DateTime _time = new DateTime(2026, 11, 23, 9, 0, 0, DateTimeKind.Utc);
        private static readonly Vector3 _alphaSpawn = new Vector3(-5f, 1.6f, 0f);
        private static readonly Vector3 _bravoSpawn = new Vector3(5f, 1.6f, 0f);
        private static readonly ShotRejection[] _threeHolsteredRejections = { ShotRejection.Holstered, ShotRejection.Holstered, ShotRejection.Holstered };

        private StringWriter _log;
        private StringWriter _json;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerContext _context;
        private ServerHost _host;
        private MatchRulesModule _rules;
        private ServerCombat _combat;
        private ServerCheatSignals _signals;
        private ServerHolster _holster;
        private List<ShotRejection> _rejected;
        private int _accepted;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _json = new StringWriter();
            _transport = new RecordingTransport();
            _rejected = new List<ShotRejection>();
            _accepted = 0;
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
            _json.Dispose();
        }

        [Test]
        public void HolsteringTakesTheDrawSecondsOfAWeaponWithoutItsOwnHolsterSeconds()
        {
            StartWithTwo();

            Request(1, HolsterAction.Holster);

            Assert.That(_holster.PhaseOf(1), Is.EqualTo(HolsterPhase.Holstering));
            Step(0.3f);
            Assert.That(_holster.PhaseOf(1), Is.EqualTo(HolsterPhase.Holstering), "the rifle draws in the 0.4 s switch time");
            Assert.That(_holster.LoweringOf(1), Is.InRange(0.6f, 0.95f));
            Step(0.15f);

            Assert.That(_holster.PhaseOf(1), Is.EqualTo(HolsterPhase.Holstered));
            Assert.That(_holster.LoweringOf(1), Is.EqualTo(1f));
            Assert.That(_log.ToString(), Does.Contain("[Holster] server began player=1 phase=Holstering seconds=0.40 weapon=" + TestRules.RifleId));
            Assert.That(_log.ToString(), Does.Contain("[Holster] server settled player=1 phase=Holstered"));
        }

        [Test]
        public void TheStartIsLoggedAndEveryoneBeginsDrawn()
        {
            StartWithTwo();

            Assert.That(_log.ToString(), Does.Contain("[Holster] server ready"));
            Assert.That(_holster.PhaseOf(1), Is.EqualTo(HolsterPhase.Drawn));
            Assert.That(_holster.IsLowered(2), Is.False);
            Assert.That(_combat.IsHolstered, Is.Not.Null, "the module plugs itself into the combat");
        }

        [Test]
        public void TheHolsterAndDrawSecondsOfTheWeaponAreUsed()
        {
            Start(HolsterRules(), SlowHolsterRifle());
            Join(1);
            Join(2);
            Step(0.2f);

            Request(1, HolsterAction.Holster);
            Step(0.9f);
            Assert.That(_holster.PhaseOf(1), Is.EqualTo(HolsterPhase.Holstering));
            Step(0.15f);
            Assert.That(_holster.PhaseOf(1), Is.EqualTo(HolsterPhase.Holstered));

            Request(1, HolsterAction.Draw);
            Step(0.15f);
            Assert.That(_holster.PhaseOf(1), Is.EqualTo(HolsterPhase.Drawing));
            Step(0.15f);

            Assert.That(_holster.PhaseOf(1), Is.EqualTo(HolsterPhase.Drawn));
            Assert.That(_log.ToString(), Does.Contain("phase=Holstering seconds=1.00"));
            Assert.That(_log.ToString(), Does.Contain("phase=Drawing seconds=0.25"));
        }

        [Test]
        public void DrawingHalfwayThroughHolsteringReturnsFromTheCurrentLowering()
        {
            StartWithTwo();
            Request(1, HolsterAction.Holster);
            Step(0.1f);
            var lowered = _holster.LoweringOf(1);

            Request(1, HolsterAction.Draw);

            Assert.That(_holster.PhaseOf(1), Is.EqualTo(HolsterPhase.Drawing));
            Assert.That(_holster.LoweringOf(1), Is.LessThan(lowered));
            Step(0.15f);
            Assert.That(_holster.PhaseOf(1), Is.EqualTo(HolsterPhase.Drawn), "only the lowered part is drawn back");
        }

        [Test]
        public void FiringIsRefusedWhileHolsteringHolsteredAndDrawing()
        {
            StartWithTwo();
            var ammo = Ammo(1);

            Request(1, HolsterAction.Holster);
            FireEast(1);
            Step(0.5f);
            FireEast(1);
            Request(1, HolsterAction.Draw);
            FireEast(1);

            Assert.That(_rejected, Is.EqualTo(_threeHolsteredRejections));
            Assert.That(_accepted, Is.Zero);
            Assert.That(Ammo(1), Is.EqualTo(ammo), "a refused shot uses no round");
            Assert.That(_log.ToString(), Does.Contain("reason=Holstered"));
        }

        [Test]
        public void FiringWorksAgainOnceDrawn()
        {
            StartWithTwo();
            Request(1, HolsterAction.Holster);
            Step(0.5f);
            Request(1, HolsterAction.Draw);
            Step(0.5f);

            FireEast(1);

            Assert.That(_holster.PhaseOf(1), Is.EqualTo(HolsterPhase.Drawn));
            Assert.That(_rejected, Is.Empty);
            Assert.That(_accepted, Is.EqualTo(1));
        }

        [Test]
        public void TheOtherPlayerCanStillFireWhileOneIsHolstered()
        {
            StartWithTwo();
            Request(1, HolsterAction.Holster);
            Step(0.5f);

            FireEast(2);

            Assert.That(_rejected, Is.Empty);
            Assert.That(_accepted, Is.EqualTo(1));
        }

        [Test]
        public void ReloadingIsRefusedWhileHolsteredAndHolsteringCancelsAReload()
        {
            StartWithTwo();
            FireEast(1);
            Reload(1);
            Assert.That(_combat.WeaponsOf(1).Sync().IsReloading, Is.True, "a drawn weapon reloads");

            Request(1, HolsterAction.Holster);
            Assert.That(_combat.WeaponsOf(1).Sync().IsReloading, Is.False, "holstering stops the reload");
            Step(0.5f);
            Reload(1);

            Assert.That(_combat.WeaponsOf(1).Sync().IsReloading, Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Combat] ignored Reload player=1 reason=Holstered"));
        }

        [Test]
        public void DyingPutsTheWeaponBackToDrawn()
        {
            StartWithTwo();
            Request(1, HolsterAction.Holster);
            Step(0.5f);

            Kill(1);

            Assert.That(_holster.PhaseOf(1), Is.EqualTo(HolsterPhase.Drawn));
            Assert.That(_holster.IsLowered(1), Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Holster] server reset player=1 to=Drawn reason=Died"));
        }

        [Test]
        public void ADeadPlayerCannotHolster()
        {
            StartWithTwo();
            Kill(1);

            Assert.That(_holster.Request(1, HolsterAction.Holster), Is.EqualTo(ServerHolster.Refusal.Dead));
            Assert.That(_holster.PhaseOf(1), Is.EqualTo(HolsterPhase.Drawn));
            Assert.That(_log.ToString(), Does.Contain("[Holster] server refused player=1 action=Holster reason=Dead"));
        }

        [Test]
        public void SwitchingWeaponsWhileHolsteredDrawsWithTheSwitch()
        {
            StartWithTwo();
            Request(1, HolsterAction.Holster);
            Step(0.5f);

            _host.OnMessage(new ConnectionId(1), new SwitchWeaponRequestMessage { WeaponId = TestRules.HandgunId });
            Tick();

            Assert.That(_holster.PhaseOf(1), Is.EqualTo(HolsterPhase.Drawn));
            Assert.That(_combat.WeaponsOf(1).IsSwitching, Is.True, "the switch time raises the handgun");
            Assert.That(_log.ToString(), Does.Contain("[Holster] server reset player=1 to=Drawn reason=Switched"));
        }

        [Test]
        public void HolsteringDuringASwitchIsRefused()
        {
            StartWithTwo();
            _host.OnMessage(new ConnectionId(1), new SwitchWeaponRequestMessage { WeaponId = TestRules.HandgunId });

            Assert.That(_holster.Request(1, HolsterAction.Holster), Is.EqualTo(ServerHolster.Refusal.Switching));
            Assert.That(_holster.PhaseOf(1), Is.EqualTo(HolsterPhase.Drawn));
        }

        [Test]
        public void ARepeatedRequestChangesNothingAndIsNotLogged()
        {
            StartWithTwo();

            Assert.That(_holster.Request(1, HolsterAction.Draw), Is.EqualTo(ServerHolster.Refusal.Unchanged), "already drawn");
            Assert.That(_holster.Request(1, HolsterAction.Holster), Is.EqualTo(ServerHolster.Refusal.None));
            Assert.That(_holster.Request(1, HolsterAction.Holster), Is.EqualTo(ServerHolster.Refusal.Unchanged), "already holstering");

            Assert.That(_log.ToString(), Does.Not.Contain("[Holster] server refused"));
        }

        [Test]
        public void TheSnapshotCarriesThePhaseAndLoweringOfEachPlayer()
        {
            StartWithTwo();
            Request(1, HolsterAction.Holster);
            Step(0.1f);
            var snapshot = SnapshotOfTwo();

            Assert.That(_match.TryGetPlayer(2, out var receiver), Is.True);
            _holster.Contribute(snapshot, receiver);

            Assert.That(snapshot.Players[0].HolsterPhase, Is.EqualTo(HolsterPhase.Holstering));
            Assert.That(snapshot.Players[0].HolsterLowering, Is.EqualTo(_holster.LoweringOf(1)));
            Assert.That(snapshot.Players[0].HolsterLowering, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(snapshot.Players[1].HolsterPhase, Is.EqualTo(HolsterPhase.Drawn));
            Assert.That(snapshot.Players[1].HolsterLowering, Is.Zero);
        }

        [Test]
        public void AHolsteredPlayerIsSentFullyLowered()
        {
            StartWithTwo();
            Request(1, HolsterAction.Holster);
            Step(0.5f);
            var snapshot = SnapshotOfTwo();

            Assert.That(_match.TryGetPlayer(1, out var receiver), Is.True);
            _holster.Contribute(snapshot, receiver);

            Assert.That(snapshot.Players[0].HolsterPhase, Is.EqualTo(HolsterPhase.Holstered));
            Assert.That(snapshot.Players[0].HolsterLowering, Is.EqualTo(1f));
        }

        [Test]
        public void AFullyDrawnPlayerIsForgotten()
        {
            StartWithTwo();
            Request(1, HolsterAction.Holster);
            Step(0.5f);
            Request(1, HolsterAction.Draw);
            Step(0.5f);
            var snapshot = SnapshotOfTwo();

            Assert.That(_match.TryGetPlayer(1, out var receiver), Is.True);
            _holster.Contribute(snapshot, receiver);

            Assert.That(_holster.IsLowered(1), Is.False);
            Assert.That(snapshot.Players[0].HolsterPhase, Is.EqualTo(HolsterPhase.Drawn));
            Assert.That(_log.ToString(), Does.Contain("[Holster] server settled player=1 phase=Drawn"));
        }

        [Test]
        public void AMessageFromAConnectionNotInTheMatchIsIgnored()
        {
            StartWithTwo();

            _host.OnMessage(new ConnectionId(9), new HolsterRequestMessage { Action = HolsterAction.Holster });

            Assert.That(_holster.PhaseOf(9), Is.EqualTo(HolsterPhase.Drawn));
            Assert.That(_holster.PhaseOf(1), Is.EqualTo(HolsterPhase.Drawn));
        }

        [Test]
        public void StoppingUnplugsTheModuleFromTheCombat()
        {
            StartWithTwo();
            Request(1, HolsterAction.Holster);
            Step(0.5f);

            _host.Stop();

            Assert.That(_combat.IsHolstered, Is.Null);
        }

        [Test]
        public void TheModuleNeedsItsParts()
        {
            var rules = new MatchRulesModule();
            var movement = new ServerMovement(ServerModules.MotorSettingsFrom(TestRules.Data()));
            var combat = new ServerCombat(rules, movement);

            Assert.Throws<ArgumentNullException>(() => new ServerHolster(null, movement));
            Assert.Throws<ArgumentNullException>(() => new ServerHolster(combat, null));
        }

        private static GameRules HolsterRules()
        {
            var actions = ActionRules.Default.WithDowned(Downed.Off);
            return GameRules.Default.WithActions(actions).WithSpawnProtectionSeconds(0f).WithIdleKickSeconds(0f).WithRegenPerSecond(0f);
        }

        // しまうのに 1 秒、構えるのに 0.25 秒かかる小銃
        private static WeaponSpec SlowHolsterRifle()
        {
            var rifle = TestRules.Rifle();
            return new WeaponSpec(
                rifle.Id, rifle.NameKey, rifle.Category, rifle.Trigger, rifle.Ammo, rifle.Round, rifle.Impact, rifle.Aim,
                drawSeconds: 0.25f, holsterSeconds: 1f);
        }

        private void StartWithTwo()
        {
            Start(HolsterRules());
            Join(1);
            Join(2);
            Step(0.2f);
        }

        private void Start(GameRules rules, WeaponSpec rifle = null)
        {
            var colliders = new List<WorldCollider>
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, Vector3.Zero, new Vector3(60f, 0.5f, 60f), Quaternion.Identity),
            };
            var points = new NetMapPoints(new[] { new SpawnPoint(_alphaSpawn, Team.Alpha), new SpawnPoint(_bravoSpawn, Team.Bravo) }, Array.Empty<ControlPointArea>());
            var map = new ServerMapData("NetTest", new WorldMap(colliders), TestMaps.NavMesh(), "memory", points);
            var data = new ServerRulesData("TeamDeathmatch", TestRules.File(rules, rifle: rifle), PingSpec.Default, "memory", null);
            _match = new ServerMatch(NetMatchSettings.DefaultRestartDelaySeconds, _log, new ServerEventWriter(true, _json, _log), () => _time);
            _context = new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "1.0.0",
                _log,
                map,
                _match,
                new ServerMessenger(_transport),
                new StubControl(),
                data);
            _signals = new ServerCheatSignals();
            _signals.ShotRejected += (_, reason) => _rejected.Add(reason);
            _signals.ShotAccepted += (_, _, _, _) => _accepted++;
            var movement = new ServerMovement(ServerModules.MotorSettingsFrom(data), signals: _signals);
            _rules = new MatchRulesModule();
            _combat = new ServerCombat(_rules, movement, null, _signals);
            _holster = new ServerHolster(_combat, movement);
            _host = new ServerHost();
            _host.Register(movement);
            _host.Register(_rules);
            _host.Register(_combat);
            _host.Register(_holster);
            _host.Start(_context);
        }

        // MatchServer と同じく、試合に加えてからモジュールに接続を知らせる
        private void Join(int id)
        {
            var connection = new ConnectionId((ulong)id);
            _match.Join(connection, "device" + id);
            _host.OnConnected(connection);
        }

        private void Request(int id, HolsterAction action)
        {
            _host.OnMessage(new ConnectionId((ulong)id), new HolsterRequestMessage { Action = action });
            Tick();
        }

        private void Reload(int id)
        {
            _host.OnMessage(new ConnectionId((ulong)id), new WeaponActionRequestMessage { Action = WeaponAction.Reload });
            Tick();
        }

        private int Ammo(int id)
        {
            return _combat.WeaponsOf(id).Sync().AmmoInMagazine;
        }

        private void Kill(int id)
        {
            _combat.DealDamage(null, id, NetMatchSettings.DefaultMaxHealth, TestRules.RifleId, HitZone.Body, null);
            Assert.That(_rules.Session.IsAlive(id), Is.False, _log.ToString());
        }

        // 連射の間隔を空けるため、撃った後に 1 tick ではなく少し進める
        private void FireEast(int id)
        {
            Assert.That(_context.Bodies.TryGet(id, out var body), Is.True);
            _host.OnMessage(new ConnectionId((ulong)id), new FireRequestMessage
            {
                WeaponId = TestRules.RifleId,
                Origin = body.EyePosition,
                Direction = Vector3.UnitX,
                ClientTick = (int)_host.TickCount,
                InterpolationAlpha = 0f,
                ShotIndex = 0,
                ShotGeneration = 0,
            });
            Step(0.15f);
        }

        private static SnapshotMessage SnapshotOfTwo()
        {
            var snapshot = new SnapshotMessage();
            snapshot.Clear();
            for (var id = 1; id <= 2; id++)
            {
                ref var row = ref snapshot.Players.Add();
                row.PlayerId = id;
                row.WeaponId = TestRules.RifleId;
            }

            return snapshot;
        }

        private void Step(float seconds)
        {
            for (var elapsed = 0f; elapsed < seconds; elapsed += _tickSeconds)
            {
                Tick();
            }
        }

        private void Tick()
        {
            _match.Tick(_tickSeconds);
            _host.Tick(_tickSeconds);
        }
    }
}

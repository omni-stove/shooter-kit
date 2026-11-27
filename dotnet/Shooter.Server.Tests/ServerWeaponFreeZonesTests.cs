using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using GameKit.Net;
using GameKit.World;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // 移動・ルール・戦闘・しまう動作を同じホストで動かし、マップの武器を出してはいけない場所に入ると自動でしまわせ、
    // 構えられない場所では構えるのを断り、出ると構えられ、警告の状態だけを本人のスナップショットに載せることを確かめる
    public sealed class ServerWeaponFreeZonesTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;

        private static readonly DateTime _time = new DateTime(2026, 11, 23, 9, 0, 0, DateTimeKind.Utc);
        private static readonly Vector3 _alphaSpawn = new Vector3(-5f, 1.6f, 0f);
        private static readonly Vector3 _bravoSpawn = new Vector3(5f, 1.6f, 0f);
        private static readonly Vector3 _open = new Vector3(0f, 1f, 0f);
        private static readonly Vector3 _inLobby = new Vector3(13f, 1f, 13f);
        private static readonly Vector3 _inVault = new Vector3(-13f, 1f, 13f);
        private static readonly Vector3 _inGate = new Vector3(13f, 1f, -13f);
        private static readonly ShotRejection[] _oneHolsteredRejection = { ShotRejection.Holstered };
        private static readonly HolsterPhase[] _onlyDrawn = { HolsterPhase.Drawn };

        private StringWriter _log;
        private StringWriter _json;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerContext _context;
        private ServerHost _host;
        private MatchRulesModule _rules;
        private ServerMovement _movement;
        private ServerCombat _combat;
        private ServerHolster _holster;
        private List<ShotRejection> _rejected;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _json = new StringWriter();
            _transport = new RecordingTransport();
            _rejected = new List<ShotRejection>();
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
        public void TheZonesOfTheMapAreLoadedAndHostilityIsReadAsAWarning()
        {
            StartWithTwo();

            Assert.That(_holster.Zones.Count, Is.EqualTo(3));
            Assert.That(_log.ToString(), Does.Contain("[WeaponFreeZone] server map=NetTest zones=3"));
            Assert.That(_log.ToString(), Does.Contain("[WeaponFreeZone] server zone=Lobby reaction=AutoHolster online=AutoHolster"));
            Assert.That(_log.ToString(), Does.Contain("[WeaponFreeZone] server zone=Vault reaction=BlockDraw online=BlockDraw"));
            Assert.That(_log.ToString(), Does.Contain("[WeaponFreeZone] server zone=Gate reaction=WarnThenHostile online=WarnOnly"));
        }

        [Test]
        public void EnteringAnAutoHolsterZoneArmedHolstersTheWeapon()
        {
            StartWithTwo();

            MoveTo(1, _inLobby);

            Assert.That(_holster.PhaseOf(1), Is.EqualTo(HolsterPhase.Holstering));
            Step(0.5f);
            Assert.That(_holster.PhaseOf(1), Is.EqualTo(HolsterPhase.Holstered));
            Assert.That(_holster.PhaseOf(2), Is.EqualTo(HolsterPhase.Drawn), "only the one who entered holsters");
            Assert.That(_log.ToString(), Does.Contain("[WeaponFreeZone] server zone=Lobby player=1 entered armed=true"));
            Assert.That(_log.ToString(), Does.Contain("[WeaponFreeZone] server zone=Lobby player=1 autoHolster=began"));
        }

        [Test]
        public void FiringIsRefusedRightAfterTheZoneHolsters()
        {
            StartWithTwo();
            MoveTo(1, _inLobby);

            FireEast(1);

            Assert.That(_rejected, Is.EqualTo(_oneHolsteredRejection));
        }

        [Test]
        public void DrawingAgainInsideAnAutoHolsterZoneIsAllowedAndDoesNotHolsterAgain()
        {
            StartWithTwo();
            MoveTo(1, _inLobby);
            Step(0.5f);

            Assert.That(_holster.Request(1, HolsterAction.Draw), Is.EqualTo(ServerHolster.Refusal.None));
            Step(0.5f);

            Assert.That(_holster.PhaseOf(1), Is.EqualTo(HolsterPhase.Drawn), "the zone holsters on entering, not while staying");
        }

        [Test]
        public void EnteringAnAutoHolsterZoneHolsteredChangesNothing()
        {
            StartWithTwo();
            Request(1, HolsterAction.Holster);
            Step(0.5f);

            MoveTo(1, _inLobby);

            Assert.That(_holster.PhaseOf(1), Is.EqualTo(HolsterPhase.Holstered));
            Assert.That(_log.ToString(), Does.Contain("[WeaponFreeZone] server zone=Lobby player=1 entered armed=false"));
            Assert.That(_log.ToString(), Does.Not.Contain("autoHolster="));
        }

        [Test]
        public void DrawingIsRefusedInsideABlockDrawZone()
        {
            StartWithTwo();
            Request(1, HolsterAction.Holster);
            Step(0.5f);
            MoveTo(1, _inVault);

            Assert.That(_holster.Request(1, HolsterAction.Draw), Is.EqualTo(ServerHolster.Refusal.WeaponFreeZone));
            FireEast(1);

            Assert.That(_holster.PhaseOf(1), Is.EqualTo(HolsterPhase.Holstered));
            Assert.That(_holster.Zones.DrawBlockedBy(1), Is.EqualTo("Vault"));
            Assert.That(_rejected, Is.EqualTo(_oneHolsteredRejection));
            Assert.That(_log.ToString(), Does.Contain("[Holster] server refused player=1 action=Draw reason=WeaponFreeZone"));
        }

        [Test]
        public void LeavingABlockDrawZoneAllowsDrawingAgain()
        {
            StartWithTwo();
            Request(1, HolsterAction.Holster);
            Step(0.5f);
            MoveTo(1, _inVault);

            MoveTo(1, _open);

            Assert.That(_holster.Zones.DrawBlockedBy(1), Is.Null);
            Assert.That(_holster.Request(1, HolsterAction.Draw), Is.EqualTo(ServerHolster.Refusal.None));
            Step(0.5f);
            Assert.That(_holster.PhaseOf(1), Is.EqualTo(HolsterPhase.Drawn));
            Assert.That(_log.ToString(), Does.Contain("[WeaponFreeZone] server zone=Vault player=1 exited"));
        }

        [Test]
        public void EnteringABlockDrawZoneDrawnKeepsTheWeaponOut()
        {
            StartWithTwo();

            MoveTo(1, _inVault);
            FireEast(1);

            Assert.That(_holster.PhaseOf(1), Is.EqualTo(HolsterPhase.Drawn), "like offline, the zone only stops drawing");
            Assert.That(_rejected, Is.Empty);
        }

        [Test]
        public void SwitchingWeaponsInsideABlockDrawZoneStaysHolstered()
        {
            StartWithTwo();
            Request(1, HolsterAction.Holster);
            Step(0.5f);
            MoveTo(1, _inVault);

            _host.OnMessage(new ConnectionId(1), new SwitchWeaponRequestMessage { WeaponId = TestRules.HandgunId });
            Step(0.5f);

            Assert.That(_holster.PhaseOf(1), Is.EqualTo(HolsterPhase.Holstered));
            Assert.That(_log.ToString(), Does.Not.Contain("[Holster] server reset player=1 to=Drawn reason=Switched"));
        }

        [Test]
        public void AWarnThenHostileZoneOnlyWarnsAndNeverTurnsHostile()
        {
            StartWithTwo();

            MoveTo(1, _inGate);
            Step(2f);

            var status = _holster.Zones.StatusOf(1);
            Assert.That(status.Inside, Is.True);
            Assert.That(status.Reaction, Is.EqualTo(WeaponFreeZoneReaction.WarnOnly));
            Assert.That(status.Warning, Is.True, "the 1 s to hostility has passed, but online the zone keeps warning");
            Assert.That(status.SecondsToHostile, Is.Zero);
            Assert.That(_holster.PhaseOf(1), Is.EqualTo(HolsterPhase.Drawn));
            Assert.That(_log.ToString(), Does.Contain("[WeaponFreeZone] server zone=Gate player=1 warning"));
        }

        [Test]
        public void HolsteringInsideAWarningZoneClearsTheWarning()
        {
            StartWithTwo();
            MoveTo(1, _inGate);

            Request(1, HolsterAction.Holster);
            Step(0.5f);

            Assert.That(_holster.Zones.StatusOf(1).Warning, Is.False);
            Assert.That(_holster.Zones.StatusOf(1).Inside, Is.True);
            Assert.That(_log.ToString(), Does.Contain("[WeaponFreeZone] server zone=Gate player=1 warning cleared reason=unarmed"));
        }

        [Test]
        public void TheSnapshotCarriesTheZoneOnlyToThePlayerInside()
        {
            StartWithTwo();
            MoveTo(1, _inGate);
            var snapshot = new SnapshotMessage();
            snapshot.Clear();
            snapshot.HasOwner = true;

            Assert.That(_match.TryGetPlayer(1, out var inside), Is.True);
            _holster.Contribute(snapshot, inside);
            Assert.That(snapshot.Owner.WeaponFreeZone, Is.EqualTo(new WeaponFreeZoneStatus(true, WeaponFreeZoneReaction.WarnOnly, true, 0f)));

            Assert.That(_match.TryGetPlayer(2, out var outside), Is.True);
            _holster.Contribute(snapshot, outside);
            Assert.That(snapshot.Owner.WeaponFreeZone, Is.EqualTo(WeaponFreeZoneStatus.Outside), "the warning of the previous receiver is not left behind");
        }

        [Test]
        public void ADeadPlayerIsOutsideEveryZone()
        {
            StartWithTwo();
            MoveTo(1, _inGate);

            Kill(1);
            Tick();

            Assert.That(_holster.Zones.StatusOf(1), Is.EqualTo(WeaponFreeZoneStatus.Outside));
            Assert.That(_log.ToString(), Does.Contain("[WeaponFreeZone] server zone=Gate player=1 exited"));
        }

        [Test]
        public void AMapWithoutZonesChangesNothing()
        {
            Start(NetMapWeaponFreeZones.None);
            Join(1);
            Join(2);
            Step(0.2f);

            MoveTo(1, _inLobby);

            Assert.That(_holster.Zones.Count, Is.Zero);
            Assert.That(_holster.PhaseOf(1), Is.EqualTo(HolsterPhase.Drawn));
            Assert.That(_log.ToString(), Does.Contain("[WeaponFreeZone] server map=NetTest zones=0"));
        }

        [Test]
        public void OnlyTheHostileReactionIsReadAsAWarningOnline()
        {
            var hostile = new WeaponFreeZoneSpec(WeaponFreeZoneReaction.WarnThenHostile, 3f);
            var block = new WeaponFreeZoneSpec(WeaponFreeZoneReaction.BlockDraw);

            Assert.That(ServerWeaponFreeZones.OnlineSpec(hostile).Reaction, Is.EqualTo(WeaponFreeZoneReaction.WarnOnly));
            Assert.That(ServerWeaponFreeZones.OnlineSpec(hostile).HostileAfterSeconds, Is.EqualTo(3f));
            Assert.That(ServerWeaponFreeZones.OnlineSpec(block), Is.SameAs(block));
            Assert.Throws<ArgumentNullException>(() => ServerWeaponFreeZones.OnlineSpec(null));
        }

        [Test]
        public void OverlappingZonesSendTheWarningFirstAndBlockDrawingIfEitherBlocks()
        {
            var zones = new ServerWeaponFreeZones();
            zones.Load("Overlap", new NetMapWeaponFreeZones(new[]
            {
                new MapWeaponFreeZone("Quiet", new Vector3(-5f, -1f, -5f), new Vector3(5f, 4f, 5f), new WeaponFreeZoneSpec(WeaponFreeZoneReaction.BlockDraw)),
                new MapWeaponFreeZone("Loud", new Vector3(-2f, -1f, -2f), new Vector3(2f, 4f, 2f), new WeaponFreeZoneSpec(WeaponFreeZoneReaction.WarnOnly)),
            }), TextWriter.Null);

            Assert.That(zones.Update(1, Vector3.Zero, true, _tickSeconds), Is.Null, "neither zone holsters");

            Assert.That(zones.StatusOf(1).Reaction, Is.EqualTo(WeaponFreeZoneReaction.WarnOnly));
            Assert.That(zones.StatusOf(1).Warning, Is.True);
            Assert.That(zones.DrawBlockedBy(1), Is.EqualTo("Quiet"));

            zones.Update(1, new Vector3(4f, 0f, 4f), true, _tickSeconds);
            Assert.That(zones.StatusOf(1).Reaction, Is.EqualTo(WeaponFreeZoneReaction.BlockDraw), "only the outer zone is left");

            zones.Update(1, null, false, _tickSeconds);
            Assert.That(zones.StatusOf(1), Is.EqualTo(WeaponFreeZoneStatus.Outside));
            Assert.That(zones.DrawBlockedBy(1), Is.Null);
        }

        [Test]
        public void TheAutoHolsterZoneAnswersOnlyOnTheTickOfEntering()
        {
            var zones = new ServerWeaponFreeZones();
            zones.Load("One", new NetMapWeaponFreeZones(new[]
            {
                new MapWeaponFreeZone("Lobby", new Vector3(-2f, -1f, -2f), new Vector3(2f, 4f, 2f), new WeaponFreeZoneSpec(WeaponFreeZoneReaction.AutoHolster)),
            }), TextWriter.Null);

            Assert.That(zones.Update(1, new Vector3(5f, 0f, 0f), true, _tickSeconds), Is.Null, "outside");
            Assert.That(zones.Update(1, Vector3.Zero, true, _tickSeconds), Is.EqualTo("Lobby"));
            Assert.That(zones.Update(1, Vector3.Zero, true, _tickSeconds), Is.Null, "staying inside");
            Assert.That(zones.Update(2, Vector3.Zero, false, _tickSeconds), Is.Null, "entering holstered");
        }

        [Test]
        public void ArmedIsCountedWithTheHolsterTuning()
        {
            var zones = new ServerWeaponFreeZones(new HolsterSpec(armedPhases: _onlyDrawn));

            Assert.That(zones.Holster.IsArmedPhase(HolsterPhase.Drawing), Is.False);
            Assert.That(new ServerWeaponFreeZones().Holster, Is.SameAs(HolsterSpec.Default));
        }

        private static GameRules ZoneRules()
        {
            var actions = ActionRules.Default.WithDowned(Downed.Off);
            return GameRules.Default.WithActions(actions).WithSpawnProtectionSeconds(0f).WithIdleKickSeconds(0f).WithRegenPerSecond(0f);
        }

        private static NetMapWeaponFreeZones ThreeZones()
        {
            return new NetMapWeaponFreeZones(new[]
            {
                new MapWeaponFreeZone("Lobby", new Vector3(10f, -1f, 10f), new Vector3(16f, 4f, 16f), new WeaponFreeZoneSpec(WeaponFreeZoneReaction.AutoHolster)),
                new MapWeaponFreeZone("Vault", new Vector3(-16f, -1f, 10f), new Vector3(-10f, 4f, 16f), new WeaponFreeZoneSpec(WeaponFreeZoneReaction.BlockDraw)),
                new MapWeaponFreeZone(
                    "Gate", new Vector3(10f, -1f, -16f), new Vector3(16f, 4f, -10f), new WeaponFreeZoneSpec(WeaponFreeZoneReaction.WarnThenHostile, 1f)),
            });
        }

        private void StartWithTwo()
        {
            Start(ThreeZones());
            Join(1);
            Join(2);
            Step(0.2f);
        }

        private void Start(NetMapWeaponFreeZones zones)
        {
            var colliders = new List<WorldCollider>
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, Vector3.Zero, new Vector3(60f, 0.5f, 60f), Quaternion.Identity),
            };
            var points = new NetMapPoints(new[] { new SpawnPoint(_alphaSpawn, Team.Alpha), new SpawnPoint(_bravoSpawn, Team.Bravo) }, Array.Empty<ControlPointArea>());
            var map = new ServerMapData("NetTest", new WorldMap(colliders), TestMaps.NavMesh(), "memory", points, weaponFreeZones: zones);
            var data = new ServerRulesData("TeamDeathmatch", TestRules.File(ZoneRules()), PingSpec.Default, "memory", null);
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
            var signals = new ServerCheatSignals();
            signals.ShotRejected += (_, reason) => _rejected.Add(reason);
            _movement = new ServerMovement(ServerModules.MotorSettingsFrom(data), signals: signals);
            _rules = new MatchRulesModule();
            _combat = new ServerCombat(_rules, _movement, null, signals);
            _holster = new ServerHolster(_combat, _movement);
            _host = new ServerHost();
            _host.Register(_movement);
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

        private void MoveTo(int id, Vector3 position)
        {
            Assert.That(_movement.Teleport(id, position), Is.True);
            Tick();
        }

        private void Request(int id, HolsterAction action)
        {
            _host.OnMessage(new ConnectionId((ulong)id), new HolsterRequestMessage { Action = action });
            Tick();
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

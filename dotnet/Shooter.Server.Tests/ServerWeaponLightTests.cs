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
    // 移動・ルール・戦闘・しまう動作・ウェポンライトを同じホストで動かし、ライトの付いた武器だけが点き、付いていない頼みは断ってログに出し、
    // 持ち替え・しまう・ライトを外す・倒れると消え、点灯がスナップショットに載ることを確かめる
    public sealed class ServerWeaponLightTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private const string _rifleItemId = "3f8a1c2d-4e5b-4a6c-9d7e-8f0a1b2c3d4e";
        private const string _handgunItemId = "4a9b2d3e-5f6c-4b7d-8e8f-9a0b1c2d3e4f";
        private const string _lightItemId = "5b0c3e4f-6a7d-4c8e-9f9a-0b1c2d3e4f5a";

        private static readonly string[] _lightSlots = { "light" };

        private static readonly DateTime _time = new DateTime(2026, 11, 23, 9, 0, 0, DateTimeKind.Utc);
        private static readonly Vector3 _alphaSpawn = new Vector3(-5f, 1.6f, 0f);
        private static readonly Vector3 _bravoSpawn = new Vector3(5f, 1.6f, 0f);
        private static readonly string[] _light = { _lightItemId };

        private StringWriter _log;
        private StringWriter _json;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerContext _context;
        private ServerHost _host;
        private MatchRulesModule _rules;
        private ServerCombat _combat;
        private ServerHolster _holster;
        private ServerWeaponLight _weaponLight;
        private ItemCatalog _items;

        private static string Lit(int playerId, string weaponId) => "[Combat] weaponLight server player=" + playerId + " weapon=" + weaponId + " on=true";

        private static string Off(int playerId, string weaponId, string reason) =>
            "[Combat] weaponLight server player=" + playerId + " weapon=" + weaponId + " on=false reason=" + reason;

        private static string Refused(int playerId, string weaponId, string reason) =>
            "[Combat] weaponLight server refused player=" + playerId + " weapon=" + weaponId + " reason=" + reason;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _json = new StringWriter();
            _transport = new RecordingTransport();
            _items = Items();
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
        public void TheStartIsLoggedAndEveryoneBeginsWithTheLightOff()
        {
            StartWithTwo();

            Assert.That(_log.ToString(), Does.Contain("[Combat] weaponLight server ready"));
            Assert.That(_weaponLight.IsOn(1), Is.False);
            Assert.That(_weaponLight.IsOn(2), Is.False);
        }

        [Test]
        public void AWeaponWithALightTurnsOn()
        {
            StartWithTwo();

            Request(1, true);

            Assert.That(_weaponLight.IsOn(1), Is.True);
            Assert.That(_weaponLight.LitWeaponIdOf(1), Is.EqualTo(TestRules.RifleId));
            Assert.That(_weaponLight.LightOf(1).Range, Is.EqualTo(30f), "the light comes from the attached item");
            Assert.That(_log.ToString(), Does.Contain(Lit(1, TestRules.RifleId)));
        }

        [Test]
        public void AWeaponWithoutALightIsRefusedAndLogged()
        {
            StartWithTwo();

            Assert.That(_weaponLight.Request(2, true), Is.EqualTo(ServerWeaponLight.Refusal.NoLight));

            Assert.That(_weaponLight.IsOn(2), Is.False);
            Assert.That(_weaponLight.LightOf(2), Is.Null);
            Assert.That(_log.ToString(), Does.Contain(Refused(2, TestRules.RifleId, "NoLight")));
            Assert.That(_log.ToString(), Does.Not.Contain(Lit(2, TestRules.RifleId)));
        }

        [Test]
        public void ARefusalSentAsAMessageIsLoggedToo()
        {
            StartWithTwo();

            Request(2, true);

            Assert.That(_weaponLight.IsOn(2), Is.False);
            Assert.That(_log.ToString(), Does.Contain(Refused(2, TestRules.RifleId, "NoLight")));
        }

        [Test]
        public void SwitchingWeaponsTurnsTheLightOff()
        {
            StartWithTwo();
            Request(1, true);

            _host.OnMessage(new ConnectionId(1), new SwitchWeaponRequestMessage { WeaponId = TestRules.HandgunId });
            Tick();

            Assert.That(_weaponLight.IsOn(1), Is.False);
            Assert.That(_log.ToString(), Does.Contain(Off(1, TestRules.RifleId, "Switched")));
        }

        // 持ち替えて戻っても、オフラインと同じく点いたまま戻らない
        [Test]
        public void SwitchingBackDoesNotTurnTheLightOnAgain()
        {
            StartWithTwo();
            Request(1, true);
            _host.OnMessage(new ConnectionId(1), new SwitchWeaponRequestMessage { WeaponId = TestRules.HandgunId });
            Step(0.5f);

            _host.OnMessage(new ConnectionId(1), new SwitchWeaponRequestMessage { WeaponId = TestRules.RifleId });
            Step(0.5f);

            Assert.That(_combat.WeaponsOf(1).CurrentSpec.Id, Is.EqualTo(TestRules.RifleId));
            Assert.That(_weaponLight.IsOn(1), Is.False);
        }

        [Test]
        public void TheHandgunWithoutALightIsRefused()
        {
            StartWithTwo();
            _host.OnMessage(new ConnectionId(1), new SwitchWeaponRequestMessage { WeaponId = TestRules.HandgunId });
            Step(0.5f);

            Assert.That(_weaponLight.Request(1, true), Is.EqualTo(ServerWeaponLight.Refusal.NoLight));
            Assert.That(_log.ToString(), Does.Contain(Refused(1, TestRules.HandgunId, "NoLight")));
        }

        [Test]
        public void HolsteringTurnsTheLightOff()
        {
            StartWithTwo();
            Request(1, true);

            _host.OnMessage(new ConnectionId(1), new HolsterRequestMessage { Action = HolsterAction.Holster });
            Tick();

            Assert.That(_holster.PhaseOf(1), Is.EqualTo(HolsterPhase.Holstering));
            Assert.That(_weaponLight.IsOn(1), Is.False, "the light goes off as soon as holstering starts");
            Assert.That(_log.ToString(), Does.Contain(Off(1, TestRules.RifleId, "Holstered")));
        }

        [Test]
        public void TurningOnWhileHolsteredIsRefused()
        {
            StartWithTwo();
            _host.OnMessage(new ConnectionId(1), new HolsterRequestMessage { Action = HolsterAction.Holster });
            Step(0.5f);

            Assert.That(_weaponLight.Request(1, true), Is.EqualTo(ServerWeaponLight.Refusal.Holstered));
            Assert.That(_weaponLight.IsOn(1), Is.False);
            Assert.That(_log.ToString(), Does.Contain(Refused(1, TestRules.RifleId, "Holstered")));
        }

        [Test]
        public void RemovingTheLightTurnsItOff()
        {
            StartWithTwo();
            Request(1, true);

            Assert.That(_combat.WeaponsOf(1).SetAttachments(TestRules.RifleId, WeaponAttachments.None, _items), Is.True);
            Tick();

            Assert.That(_weaponLight.IsOn(1), Is.False);
            Assert.That(_log.ToString(), Does.Contain(Off(1, TestRules.RifleId, "NoLight")));
        }

        [Test]
        public void DyingTurnsTheLightOff()
        {
            StartWithTwo();
            Request(1, true);

            Kill(1);

            Assert.That(_weaponLight.IsOn(1), Is.False);
            Assert.That(_log.ToString(), Does.Contain(Off(1, TestRules.RifleId, "Died")));
        }

        [Test]
        public void ADeadPlayerCannotTurnTheLightOn()
        {
            StartWithTwo();
            Kill(1);

            Assert.That(_weaponLight.Request(1, true), Is.EqualTo(ServerWeaponLight.Refusal.Dead));
            Assert.That(_weaponLight.IsOn(1), Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Combat] weaponLight server refused player=1"));
            Assert.That(_log.ToString(), Does.Contain("reason=Dead"));
        }

        [Test]
        public void AskingForOffTurnsTheLightOff()
        {
            StartWithTwo();
            Request(1, true);

            Request(1, false);

            Assert.That(_weaponLight.IsOn(1), Is.False);
            Assert.That(_log.ToString(), Does.Contain(Off(1, TestRules.RifleId, "Requested")));
        }

        [Test]
        public void ARepeatedRequestChangesNothingAndIsNotLogged()
        {
            StartWithTwo();

            Assert.That(_weaponLight.Request(1, false), Is.EqualTo(ServerWeaponLight.Refusal.Unchanged), "already off");
            Assert.That(_weaponLight.Request(1, true), Is.EqualTo(ServerWeaponLight.Refusal.None));
            Assert.That(_weaponLight.Request(1, true), Is.EqualTo(ServerWeaponLight.Refusal.Unchanged), "already on");

            Assert.That(_log.ToString(), Does.Not.Contain("[Combat] weaponLight server refused"));
            Assert.That(Occurrences(_log.ToString(), Lit(1, TestRules.RifleId)), Is.EqualTo(1));
        }

        [Test]
        public void RefusalsInQuickSuccessionWriteOneLine()
        {
            StartWithTwo();

            for (var i = 0; i < 5; i++)
            {
                _weaponLight.Request(2, true);
            }

            Assert.That(Occurrences(_log.ToString(), Refused(2, TestRules.RifleId, "NoLight")), Is.EqualTo(1));

            Step(1.1f);
            _weaponLight.Request(2, true);

            Assert.That(Occurrences(_log.ToString(), Refused(2, TestRules.RifleId, "NoLight")), Is.EqualTo(2));
        }

        [Test]
        public void TheSnapshotCarriesTheLightOfEachPlayer()
        {
            StartWithTwo();
            Request(1, true);
            var snapshot = SnapshotOfTwo();

            Assert.That(_match.TryGetPlayer(2, out var receiver), Is.True);
            _weaponLight.Contribute(snapshot, receiver);

            Assert.That(snapshot.Players[0].WeaponLightOn, Is.True);
            Assert.That(snapshot.Players[1].WeaponLightOn, Is.False);
        }

        [Test]
        public void ARowWithoutAWeaponIsNotLit()
        {
            StartWithTwo();
            Request(1, true);
            var snapshot = SnapshotOfTwo();
            snapshot.Players[0].WeaponId = "";

            _weaponLight.Contribute(snapshot, null);

            Assert.That(snapshot.Players[0].WeaponLightOn, Is.False);
        }

        [Test]
        public void TheSnapshotIsOffAgainOnceTheLightIsTurnedOff()
        {
            StartWithTwo();
            Request(1, true);
            Request(1, false);
            var snapshot = SnapshotOfTwo();

            _weaponLight.Contribute(snapshot, null);

            Assert.That(snapshot.Players[0].WeaponLightOn, Is.False);
        }

        [Test]
        public void AMessageFromAConnectionNotInTheMatchIsIgnored()
        {
            StartWithTwo();

            _host.OnMessage(new ConnectionId(9), new WeaponLightRequestMessage { On = true });

            Assert.That(_weaponLight.IsOn(9), Is.False);
            Assert.That(_weaponLight.IsOn(1), Is.False);
        }

        [Test]
        public void StoppingForgetsTheLights()
        {
            StartWithTwo();
            Request(1, true);

            _host.Stop();

            Assert.That(_weaponLight.IsOn(1), Is.False);
        }

        [Test]
        public void TheModuleNeedsItsParts()
        {
            var rules = new MatchRulesModule();
            var movement = new ServerMovement(ServerModules.MotorSettingsFrom(TestRules.Data()));
            var combat = new ServerCombat(rules, movement);
            var holster = new ServerHolster(combat, movement);

            Assert.Throws<ArgumentNullException>(() => new ServerWeaponLight(null, holster));
            Assert.Throws<ArgumentNullException>(() => new ServerWeaponLight(combat, null));
        }

        private static int Occurrences(string text, string part)
        {
            var count = 0;
            for (var at = text.IndexOf(part, StringComparison.Ordinal); at >= 0; at = text.IndexOf(part, at + part.Length, StringComparison.Ordinal))
            {
                count++;
            }

            return count;
        }

        private static GameRules Rules()
        {
            var actions = ActionRules.Default.WithDowned(Downed.Off);
            return GameRules.Default.WithActions(actions).WithSpawnProtectionSeconds(0f).WithIdleKickSeconds(0f).WithRegenPerSecond(0f);
        }

        // 小銃にだけ light の取り付け口があり、射程 30 m のライトが付く。拳銃には付かない
        private static ItemCatalog Items()
        {
            var specs = new List<ItemSpec>
            {
                new ItemSpec(_rifleItemId, "weapon.rifle", weaponId: TestRules.RifleId, attachmentSlots: _lightSlots),
                new ItemSpec(_handgunItemId, "weapon.handgun", weaponId: TestRules.HandgunId),
                new ItemSpec(_lightItemId, "item.weaponLight", attachment: new AttachmentSpec("light", light: new WeaponLightSpec(range: 30f))),
            };
            return new ItemCatalog(specs, InventoryRule.Default);
        }

        // 1 にだけ小銃のライトを付ける。2 は同じ小銃でもライトが無い
        private void StartWithTwo()
        {
            Start();
            Join(1);
            Join(2);
            Step(0.2f);
            Assert.That(_combat.WeaponsOf(1).SetAttachments(TestRules.RifleId, _light, _items), Is.True, _log.ToString());
        }

        private void Start()
        {
            var colliders = new List<WorldCollider>
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, Vector3.Zero, new Vector3(60f, 0.5f, 60f), Quaternion.Identity),
            };
            var points = new NetMapPoints(new[] { new SpawnPoint(_alphaSpawn, Team.Alpha), new SpawnPoint(_bravoSpawn, Team.Bravo) }, Array.Empty<ControlPointArea>());
            var map = new ServerMapData("NetTest", new WorldMap(colliders), TestMaps.NavMesh(), "memory", points);
            var data = new ServerRulesData("TeamDeathmatch", TestRules.File(Rules()), PingSpec.Default, "memory", null, items: _items);
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
            var movement = new ServerMovement(ServerModules.MotorSettingsFrom(data));
            _rules = new MatchRulesModule();
            _combat = new ServerCombat(_rules, movement);
            _holster = new ServerHolster(_combat, movement);
            _weaponLight = new ServerWeaponLight(_combat, _holster);
            _host = new ServerHost();
            _host.Register(movement);
            _host.Register(_rules);
            _host.Register(_combat);
            _host.Register(_holster);
            _host.Register(_weaponLight);
            _host.Start(_context);
        }

        // MatchServer と同じく、試合に加えてからモジュールに接続を知らせる
        private void Join(int id)
        {
            var connection = new ConnectionId((ulong)id);
            _match.Join(connection, "device" + id);
            _host.OnConnected(connection);
        }

        private void Request(int id, bool on)
        {
            _host.OnMessage(new ConnectionId((ulong)id), new WeaponLightRequestMessage { On = on });
            Tick();
        }

        private void Kill(int id)
        {
            _combat.DealDamage(null, id, NetMatchSettings.DefaultMaxHealth, TestRules.RifleId, HitZone.Body, null);
            Assert.That(_rules.Session.IsAlive(id), Is.False, _log.ToString());
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

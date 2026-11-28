using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using GameKit.Core;
using GameKit.Net;
using GameKit.World;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // 二丁持ちの武器を、撃った手の弾倉・手ごとの連射の枠・左右のリロード・本人のスナップショットの左手の弾数で確かめる
    public sealed class ServerAkimboTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private const float _fireInterval = 0.25f;
        private const float _reloadSeconds = 1.5f;
        private const float _switchSeconds = 0.4f;
        private static readonly DateTime _time = new DateTime(2026, 11, 23, 9, 0, 0, DateTimeKind.Utc);
        private static readonly Vector3 _alphaSpawn = new Vector3(-5f, 1.6f, 0f);
        private static readonly Vector3 _bravoSpawn = new Vector3(5f, 1.6f, 0f);

        // 撃つ相手のいない向き (Bravo は +x 側にいる)
        private static readonly Vector3 _away = new Vector3(0f, 0f, 1f);

        private StringWriter _log;
        private StringWriter _json;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerContext _context;
        private ServerHost _host;
        private ServerCombat _combat;
        private ServerCheatSignals _signals;
        private List<ShotRejection> _rejected;
        private int _shotIndex;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _json = new StringWriter();
            _transport = new RecordingTransport();
            _rejected = new List<ShotRejection>();
            _shotIndex = 0;
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

        // 弾倉 8・予備弾 24 の二丁拳銃。並びの 1 番目に置けるよう、TestRules の小銃の Id を借りる
        private static WeaponSpec AkimboPistols() => new WeaponSpec(
            TestRules.RifleId,
            "weapon.akimboPistols",
            WeaponCategory.Handgun,
            new TriggerSpec(FireMode.Semi, _fireInterval),
            new AmmoSpec(8, 24, _reloadSeconds),
            new RoundSpec(range: 40f),
            new ImpactSpec(10),
            akimbo: AkimboSpec.Default);

        [Test]
        public void ThreeLeftHandShotsLeaveFiveInTheLeftMagazineAndEightInTheRight()
        {
            StartMatch(AkimboPistols());

            for (var i = 0; i < 3; i++)
            {
                Fire(1, AkimboHand.Left);
            }

            var weapon = Snapshot(1).Owner.Weapon;
            Assert.That(weapon.OffhandAmmoInMagazine, Is.EqualTo(5), _log.ToString());
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(8));
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(24));
            Assert.That(weapon.IsOffhandReloading, Is.False);
            Assert.That(_rejected, Is.Empty);
            Assert.That(_log.ToString(), Does.Contain("[Combat] akimbo fire player=1 weapon=" + TestRules.RifleId + " hand=left right=8 left=5"));
        }

        [Test]
        public void ALeftHandShotWithAWeaponWithoutAnAkimboPartIsRefusedAndUsesNoRound()
        {
            StartMatch(null);

            Fire(1, AkimboHand.Left);

            var weapon = Snapshot(1).Owner.Weapon;
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(30), "no round is used");
            Assert.That(weapon.OffhandAmmoInMagazine, Is.Zero, "a weapon held in one hand sends no left magazine");
            Assert.That(_rejected, Is.EqualTo(new[] { ShotRejection.NotAkimbo }));
            Assert.That(_log.ToString(), Does.Contain("[Combat] rejected shot player=1 weapon=" + TestRules.RifleId + " hand=left reason=NotAkimbo"));

            // 断った左の手は連射の枠を使わないので、続けて右の手で撃てる
            Fire(1, AkimboHand.Right);

            Assert.That(Snapshot(1).Owner.Weapon.AmmoInMagazine, Is.EqualTo(29));
            Assert.That(_rejected, Has.Count.EqualTo(1));
        }

        [Test]
        public void TheSameHandIsRefusedWhileItCoolsDownButTheOtherHandFires()
        {
            StartMatch(AkimboPistols());

            // 連射の枠は ShotBudget.DefaultBurst 発までまとめて届いても通すので、その次の弾で間隔を超える
            for (var i = 0; i < ShotBudget.DefaultBurst + 1; i++)
            {
                Fire(1, AkimboHand.Right);
            }

            Assert.That(_rejected, Is.EqualTo(new[] { ShotRejection.FireRateExceeded }));

            Fire(1, AkimboHand.Left);

            var weapon = Snapshot(1).Owner.Weapon;
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(8 - ShotBudget.DefaultBurst));
            Assert.That(weapon.OffhandAmmoInMagazine, Is.EqualTo(7));
            Assert.That(_rejected, Has.Count.EqualTo(1), "the left hand has its own budget");
        }

        [Test]
        public void ReloadLoadsBothHandsThatAreNotFullFromTheSharedReserve()
        {
            StartMatch(AkimboPistols());
            Fire(1, AkimboHand.Right);
            Fire(1, AkimboHand.Right);
            Fire(1, AkimboHand.Left);

            Act(1, WeaponAction.Reload);

            var reloading = Snapshot(1).Owner.Weapon;
            Assert.That(reloading.IsReloading, Is.True);
            Assert.That(reloading.IsOffhandReloading, Is.True);
            var log = _log.ToString();
            Assert.That(log, Does.Contain("[Combat] akimbo reload start player=1 weapon=" + TestRules.RifleId + " hand=right auto=false ammo=6 reserve=24"));
            Assert.That(log, Does.Contain("[Combat] akimbo reload start player=1 weapon=" + TestRules.RifleId + " hand=left auto=false ammo=7 reserve=24"));

            Step(_reloadSeconds + 0.1f);

            var loaded = Snapshot(1).Owner.Weapon;
            Assert.That(loaded.AmmoInMagazine, Is.EqualTo(8));
            Assert.That(loaded.OffhandAmmoInMagazine, Is.EqualTo(8));
            Assert.That(loaded.ReserveAmmo, Is.EqualTo(21));
            Assert.That(loaded.IsReloading, Is.False);
            Assert.That(loaded.IsOffhandReloading, Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Combat] akimbo reloaded player=1 weapon=" + TestRules.RifleId + " hand=left ammo=8"));
        }

        [Test]
        public void ReloadLeavesAFullHandAlone()
        {
            StartMatch(AkimboPistols());
            Fire(1, AkimboHand.Right);

            Act(1, WeaponAction.Reload);

            var weapon = Snapshot(1).Owner.Weapon;
            Assert.That(weapon.IsReloading, Is.True);
            Assert.That(weapon.IsOffhandReloading, Is.False);
            Assert.That(_log.ToString(), Does.Not.Contain("hand=left auto="));
        }

        [Test]
        public void AutoReloadLoadsOnlyTheEmptyHandAndTheOtherHandKeepsFiring()
        {
            StartMatch(AkimboPistols());

            for (var i = 0; i < 8; i++)
            {
                Fire(1, AkimboHand.Right);
                Step(_fireInterval);
            }

            var weapon = Snapshot(1).Owner.Weapon;
            Assert.That(weapon.AmmoInMagazine, Is.Zero, _log.ToString());
            Assert.That(weapon.IsReloading, Is.True);
            Assert.That(weapon.IsOffhandReloading, Is.False);
            Assert.That(weapon.OffhandAmmoInMagazine, Is.EqualTo(8));
            Assert.That(_log.ToString(), Does.Contain("[Combat] akimbo reload start player=1 weapon=" + TestRules.RifleId + " hand=right auto=true ammo=0 reserve=24"));

            Fire(1, AkimboHand.Left);

            Assert.That(Snapshot(1).Owner.Weapon.OffhandAmmoInMagazine, Is.EqualTo(7));
            Assert.That(_rejected, Is.Empty);
        }

        [Test]
        public void SwitchingAwayAndBackKeepsTheMagazinesOfBothHands()
        {
            StartMatch(AkimboPistols());
            Fire(1, AkimboHand.Left);
            Fire(1, AkimboHand.Left);
            Fire(1, AkimboHand.Right);

            Switch(1, TestRules.HandgunId);
            Step(1f);
            Assert.That(Snapshot(1).Owner.Weapon.OffhandAmmoInMagazine, Is.Zero, "the handgun is held in one hand");
            Switch(1, TestRules.RifleId);
            Step(1f);

            var weapon = Snapshot(1).Owner.Weapon;
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(7));
            Assert.That(weapon.OffhandAmmoInMagazine, Is.EqualTo(6));
        }

        [Test]
        public void ARightHandShotFromAnOlderClientFiresTheRightHand()
        {
            StartMatch(AkimboPistols());

            // 3 以前の版の要求は手を運ばないので、Hand は既定の Right のまま届く
            var eye = Body(1).EyePosition;
            _host.OnMessage(new ConnectionId(1UL), new FireRequestMessage
            {
                WeaponId = TestRules.RifleId,
                Origin = eye,
                Direction = _away,
                ClientTick = (int)_host.TickCount,
                ShotIndex = _shotIndex++,
            });

            var weapon = Snapshot(1).Owner.Weapon;
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(7));
            Assert.That(weapon.OffhandAmmoInMagazine, Is.EqualTo(8));
        }

        [Test]
        public void TheBotFiresTheRightHandFirstAndTheLeftHandOnceTheRightIsEmpty()
        {
            using var log = new StringWriter();
            var weapons = new ServerPlayerWeapons(1, log, () => 0d);
            weapons.Equip(new[] { AkimboPistols(), TestRules.Handgun() }, _switchSeconds, GameRules.Default.WithReloadMode(ReloadMode.Manual));

            Assert.That(weapons.HoldsAkimbo, Is.True);
            Assert.That(weapons.HandForBot(), Is.EqualTo(AkimboHand.Right));
            for (var i = 0; i < 8; i++)
            {
                Assert.That(weapons.TryFire(weapons.HandForBot()), Is.EqualTo(ShotRejection.None));
            }

            Assert.That(weapons.Sync().AmmoInMagazine, Is.Zero);
            Assert.That(weapons.HandForBot(), Is.EqualTo(AkimboHand.Left));
            Assert.That(weapons.TryFire(weapons.HandForBot()), Is.EqualTo(ShotRejection.None));

            // ボットのリロードは撃ち切った右の手だけを込め、左の手で撃ち続ける
            Assert.That(weapons.TryBotReload(), Is.True);
            var sync = weapons.Sync();
            Assert.That(sync.IsReloading, Is.True);
            Assert.That(sync.IsOffhandReloading, Is.False);
            Assert.That(sync.OffhandAmmoInMagazine, Is.EqualTo(7));
            Assert.That(weapons.TryFire(AkimboHand.Right), Is.EqualTo(ShotRejection.Reloading));
            Assert.That(weapons.TryFire(AkimboHand.Left), Is.EqualTo(ShotRejection.None));
        }

        [Test]
        public void ALeftHandShotOfAOneHandedWeaponIsRefusedByTheWeapons()
        {
            using var log = new StringWriter();
            var weapons = new ServerPlayerWeapons(1, log, () => 0d);
            weapons.Equip(new[] { TestRules.Rifle(), TestRules.Handgun() }, _switchSeconds, GameRules.Default);

            Assert.That(weapons.HoldsAkimbo, Is.False);
            Assert.That(weapons.TryFire(AkimboHand.Left), Is.EqualTo(ShotRejection.NotAkimbo));
            Assert.That(weapons.Sync().AmmoInMagazine, Is.EqualTo(30));
            Assert.That(weapons.HandForBot(), Is.EqualTo(AkimboHand.Right));
        }

        [Test]
        public void ReplacingTheAkimboWeaponHandsOverTheRightMagazineAndPutsTheLeftOneInTheReserve()
        {
            using var log = new StringWriter();
            var weapons = new ServerPlayerWeapons(1, log, () => 0d);
            weapons.Equip(new[] { AkimboPistols(), TestRules.Handgun() }, _switchSeconds, GameRules.Default);
            weapons.TryFire(AkimboHand.Right);
            weapons.TryFire(AkimboHand.Right);
            weapons.TryFire(AkimboHand.Left);

            var replaced = weapons.Replace(TestRules.RifleId, TestRules.Rifle(), _switchSeconds, GameRules.Default);

            Assert.That(replaced, Is.Not.Null);
            Assert.That(replaced.Spec.IsAkimbo, Is.True);
            Assert.That(replaced.AmmoInMagazine, Is.EqualTo(6));
            Assert.That(replaced.ReserveAmmo, Is.EqualTo(24 + 7));
            Assert.That(weapons.HoldsAkimbo, Is.False, "the new rifle is held in one hand");
            Assert.That(weapons.Sync().OffhandAmmoInMagazine, Is.Zero);
        }

        [Test]
        public void PickingUpADroppedAkimboWeaponFillsTheLeftHandFromTheReserve()
        {
            using var log = new StringWriter();
            var weapons = new ServerPlayerWeapons(1, log, () => 0d);
            weapons.Equip(new[] { TestRules.Handgun() }, _switchSeconds, GameRules.Default);

            Assert.That(weapons.Give(AkimboPistols(), _switchSeconds, GameRules.Default), Is.True);
            Assert.That(weapons.SetAmmo(TestRules.RifleId, 6, 31), Is.True);
            Assert.That(weapons.TrySwitch(TestRules.RifleId), Is.True);
            weapons.Tick(1f);

            var sync = weapons.Sync();
            Assert.That(sync.AmmoInMagazine, Is.EqualTo(6));
            Assert.That(sync.OffhandAmmoInMagazine, Is.EqualTo(8));
            Assert.That(sync.ReserveAmmo, Is.EqualTo(23));
            Assert.That(weapons.SetAmmo("not-held", 1, 1), Is.False);
        }

        [Test]
        public void DroppingAndGivingTheAkimboWeaponAgainStartsWithFullHands()
        {
            using var log = new StringWriter();
            var weapons = new ServerPlayerWeapons(1, log, () => 0d);
            weapons.Equip(new[] { AkimboPistols(), TestRules.Handgun() }, _switchSeconds, GameRules.Default);
            weapons.TryFire(AkimboHand.Left);

            Assert.That(weapons.Drop(TestRules.RifleId, out var dropped), Is.True);
            Assert.That(dropped.IsAkimbo, Is.True);
            Assert.That(weapons.HoldsAkimbo, Is.False);

            Assert.That(weapons.Give(AkimboPistols(), _switchSeconds, GameRules.Default), Is.True);
            Assert.That(weapons.TrySwitch(TestRules.RifleId), Is.True);
            weapons.Tick(1f);

            var sync = weapons.Sync();
            Assert.That(weapons.HoldsAkimbo, Is.True);
            Assert.That(sync.AmmoInMagazine, Is.EqualTo(8));
            Assert.That(sync.OffhandAmmoInMagazine, Is.EqualTo(8));
        }

        [Test]
        public void ReserveAmmoPickupsAndCratesFillTheSharedReserve()
        {
            using var log = new StringWriter();
            var weapons = new ServerPlayerWeapons(1, log, () => 0d);
            weapons.Equip(new[] { AkimboPistols(), TestRules.Handgun() }, _switchSeconds, GameRules.Default);
            for (var i = 0; i < 3; i++)
            {
                weapons.TryFire(AkimboHand.Right);
                weapons.TryFire(AkimboHand.Left);
            }

            Assert.That(weapons.TryReload(), Is.True);
            weapons.Tick(_reloadSeconds + 0.1f);
            Assert.That(weapons.Sync().ReserveAmmo, Is.EqualTo(18));

            Assert.That(weapons.AddReserveAmmo(TestRules.RifleId, 2), Is.EqualTo(2));
            Assert.That(weapons.PickupStates()[0].ReserveAmmo, Is.EqualTo(20));
            Assert.That(weapons.TopUpReserveAmmo(), Is.EqualTo(4), "only the akimbo weapon has room");
            Assert.That(weapons.Sync().ReserveAmmo, Is.EqualTo(24));
            Assert.That(weapons.AddReserveAmmo("not-held", 2), Is.Zero);
        }

        [Test]
        public void HolsteringTheAkimboWeaponStopsTheReloadOfBothHands()
        {
            using var log = new StringWriter();
            var weapons = new ServerPlayerWeapons(1, log, () => 0d);
            weapons.Equip(new[] { AkimboPistols(), TestRules.Handgun() }, _switchSeconds, GameRules.Default);
            weapons.TryFire(AkimboHand.Right);
            weapons.TryFire(AkimboHand.Left);
            Assert.That(weapons.TryReload(), Is.True);

            Assert.That(weapons.TrySwitch(TestRules.HandgunId), Is.True);
            weapons.Tick(_reloadSeconds + 0.1f);
            Assert.That(weapons.TrySwitch(TestRules.RifleId), Is.True);
            weapons.Tick(1f);

            var sync = weapons.Sync();
            Assert.That(sync.AmmoInMagazine, Is.EqualTo(7), "the reload stopped when the weapon was put away");
            Assert.That(sync.OffhandAmmoInMagazine, Is.EqualTo(7));
            Assert.That(sync.IsReloading, Is.False);
            Assert.That(sync.IsOffhandReloading, Is.False);
            Assert.That(sync.ReserveAmmo, Is.EqualTo(24));
        }

        [Test]
        public void CancellingTheReloadStopsBothHands()
        {
            using var log = new StringWriter();
            var weapons = new ServerPlayerWeapons(1, log, () => 0d);
            weapons.Equip(new[] { AkimboPistols(), TestRules.Handgun() }, _switchSeconds, GameRules.Default);
            weapons.TryFire(AkimboHand.Right);
            weapons.TryFire(AkimboHand.Left);
            Assert.That(weapons.TryReload(), Is.True);

            Assert.That(weapons.CancelReload(), Is.True);
            Assert.That(weapons.CancelReload(), Is.False);

            var sync = weapons.Sync();
            Assert.That(sync.IsReloading, Is.False);
            Assert.That(sync.IsOffhandReloading, Is.False);
        }

        // 1 番は Alpha、2 番は Bravo に出る。2 人そろえて試合を始め、出現まで進める
        private void StartMatch(WeaponSpec rifle)
        {
            Start(GameRules.Default.WithSpawnProtectionSeconds(0f).WithIdleKickSeconds(0f), rifle);
            Join(1);
            Join(2);
            Step(0.2f);
        }

        private void Start(GameRules rules, WeaponSpec rifle)
        {
            var colliders = new List<WorldCollider>
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, Vector3.Zero, new Vector3(60f, 0.5f, 60f), Quaternion.Identity),
            };
            var points = new NetMapPoints(new[] { new SpawnPoint(_alphaSpawn, Team.Alpha), new SpawnPoint(_bravoSpawn, Team.Bravo) }, Array.Empty<ControlPointArea>());
            var map = new ServerMapData("NetTest", new WorldMap(colliders), TestMaps.NavMesh(), "memory", points);
            var data = new ServerRulesData("TeamDeathmatch", TestRules.File(rules, rifle: rifle), PingSpec.Default, "memory", null, null);
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
            var movement = new ServerMovement(ServerModules.MotorSettingsFrom(data), signals: _signals);
            var matchRules = new MatchRulesModule();
            _combat = new ServerCombat(matchRules, movement, null, _signals);
            _host = new ServerHost();
            _host.Register(movement);
            _host.Register(matchRules);
            _host.Register(_combat);
            _host.Start(_context);
        }

        private void Join(int id)
        {
            var connection = new ConnectionId((ulong)id);
            _match.Join(connection, "device" + id);
            _host.OnConnected(connection);
        }

        private ServerBody Body(int id)
        {
            Assert.That(_context.Bodies.TryGet(id, out var body), Is.True);
            return body;
        }

        private void Fire(int shooter, AkimboHand hand)
        {
            _host.OnMessage(new ConnectionId((ulong)shooter), new FireRequestMessage
            {
                WeaponId = Snapshot(shooter).Owner.Weapon.EquippedIndex == 0 ? TestRules.RifleId : TestRules.HandgunId,
                Origin = Body(shooter).EyePosition,
                Direction = _away,
                ClientTick = (int)_host.TickCount,
                InterpolationAlpha = 0f,
                ShotIndex = _shotIndex++,
                ShotGeneration = 0,
                Hand = hand,
            });
        }

        private void Act(int id, WeaponAction action)
        {
            _host.OnMessage(new ConnectionId((ulong)id), new WeaponActionRequestMessage { Action = action });
        }

        private void Switch(int id, string weaponId)
        {
            _host.OnMessage(new ConnectionId((ulong)id), new SwitchWeaponRequestMessage { WeaponId = weaponId });
        }

        private SnapshotMessage Snapshot(int receiverId)
        {
            var snapshot = new SnapshotMessage();
            foreach (var player in _match.Players)
            {
                snapshot.Players.Add().PlayerId = player.PlayerId;
            }

            Assert.That(_match.TryGetPlayer(receiverId, out var receiver), Is.True);
            _combat.Contribute(snapshot, receiver);
            return snapshot;
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

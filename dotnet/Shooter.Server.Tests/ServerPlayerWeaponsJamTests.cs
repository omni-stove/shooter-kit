using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Server.Tests
{
    // 弾詰まりを固定の乱数で起こし、射撃を断る・リロードの要求で解除する・持ち替えで解除をやめる・配る値を確かめる
    public sealed class ServerPlayerWeaponsJamTests
    {
        private const int _playerId = 1;
        private const int _matchSeed = 7;
        private const float _switchSeconds = 0.4f;

        // 0.5 刻みで解除を進めるので、float の引き算で誤差が出ず、残りの秒数をそのまま比べられる
        private const float _clearSeconds = 1f;

        private static readonly GameRules _jamOn = GameRules.Default.WithActions(ActionRules.Default.WithJam(WeaponJam.On));

        private StringWriter _log;
        private double _now;
        private int _seed;
        private List<int> _seeds;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _now = 100d;
            _seed = _matchSeed;
            _seeds = new List<int>();
        }

        [TearDown]
        public void TearDown()
        {
            _log.Dispose();
        }

        // 1 発目の確率は 0.5。0.25 を引くと詰まり、0.75 を引くと詰まらない
        private static WeaponSpec Rifle() => new WeaponSpec(
            TestRules.RifleId,
            "weapon.rifle",
            WeaponCategory.AssaultRifle,
            trigger: new TriggerSpec(FireMode.Auto, 0.1f),
            ammo: new AmmoSpec(30, 90, 2f, jam: new JamSpec(0.5f, 0f, 0f, 0.5f, _clearSeconds)));

        private ServerPlayerWeapons Weapons(params float[] rolls)
        {
            return new ServerPlayerWeapons(_playerId, _log, () => _now, () => _seed, seed =>
            {
                _seeds.Add(seed);
                return new FixedJamRoll(rolls);
            });
        }

        [Test]
        public void AJammedShotFiresAndTheNextOneIsRefused()
        {
            var weapons = Weapons(0.25f);
            weapons.Equip(new[] { Rifle() }, _switchSeconds, _jamOn);

            Assert.That(weapons.TryFire(), Is.EqualTo(ShotRejection.None));

            Assert.That(weapons.IsJammed, Is.True);
            Assert.That(weapons.Sync().IsJammed, Is.True);
            Assert.That(weapons.Sync().JamClearEndsAt, Is.Zero);
            Assert.That(_log.ToString(), Does.Contain(
                "[Combat] player=1 [Jam] weapon=" + TestRules.RifleId + " jammed sustained=0 chance=0.500" + Environment.NewLine));

            Assert.That(weapons.TryFire(), Is.EqualTo(ShotRejection.Jammed));
            Assert.That(weapons.Sync().AmmoInMagazine, Is.EqualTo(29));
        }

        [Test]
        public void AShotThatDoesNotJamLogsNothing()
        {
            var weapons = Weapons(0.75f);
            weapons.Equip(new[] { Rifle() }, _switchSeconds, _jamOn);

            Assert.That(weapons.TryFire(), Is.EqualTo(ShotRejection.None));

            Assert.That(weapons.IsJammed, Is.False);
            Assert.That(_log.ToString(), Does.Not.Contain("[Jam]"));
        }

        [Test]
        public void TheReloadRequestStartsTheClearAndSendsWhenItEnds()
        {
            var weapons = Weapons(0.25f);
            weapons.Equip(new[] { Rifle() }, _switchSeconds, _jamOn);
            weapons.TryFire();
            _now = 102.5d;

            Assert.That(weapons.TryReload(), Is.True);

            var sync = weapons.Sync();
            Assert.That(sync.IsJammed, Is.True);
            Assert.That(sync.JamClearEndsAt, Is.EqualTo(103.5d));
            Assert.That(sync.IsReloading, Is.False, "the magazine is not reloaded");
            Assert.That(sync.AmmoInMagazine, Is.EqualTo(29));
            Assert.That(_log.ToString(), Does.Contain("[Combat] player=1 [Jam] weapon=" + TestRules.RifleId + " clear started seconds=1.00"));
            Assert.That(weapons.TryReload(), Is.False, "already clearing");

            _now = 103d;
            weapons.Tick(0.5f);
            Assert.That(weapons.Sync().JamClearEndsAt, Is.EqualTo(103.5d), "the end time stays the same while clearing");
        }

        [Test]
        public void TheWeaponFiresOnceTheClearEnds()
        {
            var weapons = Weapons(0.25f, 0.75f);
            weapons.Equip(new[] { Rifle() }, _switchSeconds, _jamOn);
            weapons.TryFire();
            weapons.TryReload();

            weapons.Tick(0.5f);
            Assert.That(weapons.IsJammed, Is.True);
            Assert.That(weapons.TryFire(), Is.EqualTo(ShotRejection.Jammed));

            weapons.Tick(0.5f);

            Assert.That(weapons.IsJammed, Is.False);
            Assert.That(weapons.Sync().IsJammed, Is.False);
            Assert.That(weapons.Sync().JamClearEndsAt, Is.Zero);
            Assert.That(_log.ToString(), Does.Contain("[Combat] player=1 [Jam] weapon=" + TestRules.RifleId + " cleared" + Environment.NewLine));
            Assert.That(weapons.TryFire(), Is.EqualTo(ShotRejection.None));
            Assert.That(weapons.IsJammed, Is.False, "the second roll (0.75) does not jam");
            Assert.That(weapons.Sync().AmmoInMagazine, Is.EqualTo(28));
        }

        [Test]
        public void SwitchingAwayStopsTheClearAndTheWeaponComesBackJammed()
        {
            var weapons = Weapons(0.25f);
            weapons.Equip(new[] { Rifle(), TestRules.Handgun() }, _switchSeconds, _jamOn);
            weapons.TryFire();
            weapons.TryReload();
            weapons.Tick(0.5f);

            Assert.That(weapons.TrySwitch(TestRules.HandgunId), Is.True);

            Assert.That(_log.ToString(), Does.Contain("[Combat] player=1 [Jam] weapon=" + TestRules.RifleId + " clear canceled remaining=0.50"));
            weapons.Tick(1f);
            Assert.That(weapons.Sync().IsJammed, Is.False, "the handgun is not jammed");
            Assert.That(weapons.TryFire(), Is.EqualTo(ShotRejection.None));

            Assert.That(weapons.TrySwitch(TestRules.RifleId), Is.True);
            weapons.Tick(1f);

            var sync = weapons.Sync();
            Assert.That(sync.IsJammed, Is.True, "the jam stays through the switch");
            Assert.That(sync.JamClearEndsAt, Is.Zero, "the clear does not go on while holstered");
            Assert.That(weapons.TryFire(), Is.EqualTo(ShotRejection.Jammed));
            Assert.That(_log.ToString(), Does.Not.Contain(" cleared"));

            _now = 110d;
            Assert.That(weapons.TryReload(), Is.True);
            Assert.That(weapons.Sync().JamClearEndsAt, Is.EqualTo(111d), "the clear starts over");
        }

        [Test]
        public void WithTheRuleOffTheWeaponNeverJams()
        {
            var weapons = Weapons(0f);
            weapons.Equip(new[] { Rifle() }, _switchSeconds, GameRules.Default);

            for (var shot = 1; shot <= 10; shot++)
            {
                Assert.That(weapons.TryFire(), Is.EqualTo(ShotRejection.None), "shot " + shot);
            }

            Assert.That(weapons.IsJammed, Is.False);
            Assert.That(weapons.Sync().IsJammed, Is.False);
            Assert.That(weapons.TryReload(), Is.True, "the reload request reloads");
            Assert.That(weapons.Sync().JamClearEndsAt, Is.Zero);
            Assert.That(_log.ToString(), Does.Not.Contain("[Jam]"));
        }

        [Test]
        public void EachWeaponDrawsFromTheMatchPlayerAndWeaponSeed()
        {
            var weapons = Weapons(0.75f);

            weapons.Equip(new[] { Rifle(), TestRules.Handgun() }, _switchSeconds, _jamOn);

            Assert.That(_seeds, Is.EqualTo(new[]
            {
                ServerPlayerWeapons.JamSeed(_matchSeed, _playerId, TestRules.RifleId),
                ServerPlayerWeapons.JamSeed(_matchSeed, _playerId, TestRules.HandgunId),
            }));
        }

        [Test]
        public void RespawningKeepsDrawingFromTheSameRollUntilTheMatchChanges()
        {
            var weapons = Weapons(0.75f, 0.25f);
            weapons.Equip(new[] { Rifle() }, _switchSeconds, _jamOn);
            Assert.That(weapons.TryFire(), Is.EqualTo(ShotRejection.None));
            Assert.That(weapons.IsJammed, Is.False);

            weapons.Equip(new[] { Rifle() }, _switchSeconds, _jamOn);

            Assert.That(_seeds, Has.Count.EqualTo(1), "the respawn reuses the roll");
            Assert.That(weapons.TryFire(), Is.EqualTo(ShotRejection.None));
            Assert.That(weapons.IsJammed, Is.True, "the second roll (0.25) is used, not the first one again");

            _seed = _matchSeed + 1;
            weapons.Equip(new[] { Rifle() }, _switchSeconds, _jamOn);

            Assert.That(_seeds, Has.Count.EqualTo(2));
            Assert.That(_seeds[1], Is.EqualTo(ServerPlayerWeapons.JamSeed(_matchSeed + 1, _playerId, TestRules.RifleId)));
            Assert.That(weapons.IsJammed, Is.False, "the new match hands out a fresh weapon");
        }

        [Test]
        public void TheJamSeedDependsOnTheMatchThePlayerAndTheWeapon()
        {
            var seed = ServerPlayerWeapons.JamSeed(_matchSeed, _playerId, TestRules.RifleId);

            Assert.That(ServerPlayerWeapons.JamSeed(_matchSeed, _playerId, TestRules.RifleId), Is.EqualTo(seed));
            Assert.That(ServerPlayerWeapons.JamSeed(_matchSeed + 1, _playerId, TestRules.RifleId), Is.Not.EqualTo(seed));
            Assert.That(ServerPlayerWeapons.JamSeed(_matchSeed, _playerId + 1, TestRules.RifleId), Is.Not.EqualTo(seed));
            Assert.That(ServerPlayerWeapons.JamSeed(_matchSeed, _playerId, TestRules.HandgunId), Is.Not.EqualTo(seed));
        }

        [Test]
        public void WithoutAMatchSeedTheWeaponsStillJam()
        {
            var weapons = new ServerPlayerWeapons(_playerId, _log, () => _now);
            var alwaysJams = new WeaponSpec(
                TestRules.RifleId,
                "weapon.rifle",
                WeaponCategory.AssaultRifle,
                trigger: new TriggerSpec(FireMode.Auto, 0.1f),
                ammo: new AmmoSpec(30, 90, 2f, jam: new JamSpec(1f, 0f, 0f, 1f, _clearSeconds)));
            weapons.Equip(new[] { alwaysJams }, _switchSeconds, _jamOn);

            Assert.That(weapons.TryFire(), Is.EqualTo(ShotRejection.None));

            Assert.That(weapons.IsJammed, Is.True);
        }

        private sealed class FixedJamRoll : IJamRoll
        {
            private readonly float[] _values;
            private int _calls;

            public FixedJamRoll(float[] values)
            {
                _values = values;
            }

            // 列を使い切ったら最後の値を返し続ける
            public float Draw()
            {
                var value = _values[Math.Min(_calls, _values.Length - 1)];
                _calls++;
                return value;
            }
        }
    }
}

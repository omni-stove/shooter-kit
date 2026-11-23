using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class WeaponJamTests
    {
        private const string _rifleId = "00000000-0000-4000-8000-000000000001";

        // 0.25 刻みにするのは、float の足し引きで誤差が出ず、確率と秒数をそのまま比べられるため。
        private const float _fireInterval = 0.25f;

        private static readonly GameRules _jamOn = GameRules.Default.WithActions(ActionRules.Default.WithJam(WeaponJam.On));

        private static WeaponSpec Spec(JamSpec jam, int magazine = 10, int reserve = 20)
        {
            return new WeaponSpec(
                _rifleId,
                null,
                WeaponCategory.AssaultRifle,
                trigger: new TriggerSpec(FireMode.Auto, _fireInterval),
                ammo: new AmmoSpec(magazine, reserve, 2f, jam: jam));
        }

        // 1 発目から必ず詰まる (確率 1)。
        private static JamSpec AlwaysJams(float clearSeconds = 1f) => new JamSpec(1f, 0f, 0f, 1f, clearSeconds);

        private static JamSpec Rising() => new JamSpec(0.25f, 0.25f, 1f, 0.75f, 1f);

        private sealed class FixedJamRoll : IJamRoll
        {
            private readonly float[] _values;

            public FixedJamRoll(params float[] values)
            {
                _values = values;
            }

            public int Calls { get; private set; }

            // 列を使い切ったら最後の値を返し続ける。
            public float Next()
            {
                var value = _values[Math.Min(Calls, _values.Length - 1)];
                Calls++;
                return value;
            }
        }

        [Test]
        public void TheFirstShotFiresAndJamsAndTheSecondIsRefused()
        {
            var weapon = new Weapon(Spec(Rising()), _jamOn, new FixedJamRoll(0f));
            var order = new List<string>();
            weapon.Fired += () => order.Add("fired");
            weapon.Jammed += () => order.Add("jammed");

            Assert.That(weapon.TryFire(), Is.True);
            Assert.That(order, Is.EqualTo(new[] { "fired", "jammed" }));
            Assert.That(weapon.IsJammed, Is.True);
            Assert.That(weapon.IsClearingJam, Is.False);
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(9));

            weapon.Tick(_fireInterval);
            Assert.That(weapon.TryFire(), Is.False);
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(9));
            Assert.That(order.Count, Is.EqualTo(2));
        }

        [Test]
        public void SustainedFireRaisesTheChanceUntilItJams()
        {
            var roll = new FixedJamRoll(0.6f);
            var weapon = new Weapon(Spec(Rising()), _jamOn, roll);
            var jams = 0;
            weapon.Jammed += () => jams++;

            // 確率は 0.25、0.5、0.75 と上がり、0.6 は 3 発目で下回る。
            for (var shot = 1; shot <= 2; shot++)
            {
                Assert.That(weapon.TryFire(), Is.True, "shot " + shot);
                Assert.That(weapon.IsJammed, Is.False, "shot " + shot);
                weapon.Tick(_fireInterval);
            }

            Assert.That(weapon.TryFire(), Is.True);
            Assert.That(weapon.IsJammed, Is.True);
            Assert.That(jams, Is.EqualTo(1));
            Assert.That(roll.Calls, Is.EqualTo(3), "one roll per fired shot");
        }

        [Test]
        public void TheChanceIsHeldAtTheMaximum()
        {
            var weapon = new Weapon(Spec(new JamSpec(0.25f, 0.25f, 10f, 0.5f, 1f)), _jamOn, new FixedJamRoll(0.5f));

            for (var shot = 1; shot <= 8; shot++)
            {
                Assert.That(weapon.TryFire(), Is.True, "shot " + shot);
                weapon.Tick(_fireInterval);
            }

            Assert.That(weapon.IsJammed, Is.False, "0.5 never falls below the 0.5 maximum");
            Assert.That(weapon.Jam.SustainedShots, Is.EqualTo(8));
            Assert.That(weapon.Jam.NextShotChance, Is.EqualTo(0.5f));
        }

        [Test]
        public void PausingForTheResetSecondsDropsTheSustainedShots()
        {
            var weapon = new Weapon(Spec(Rising()), _jamOn, new FixedJamRoll(0.6f));
            weapon.TryFire();
            weapon.Tick(_fireInterval);
            weapon.TryFire();
            Assert.That(weapon.Jam.NextShotChance, Is.EqualTo(0.75f));

            weapon.Tick(1f);

            Assert.That(weapon.Jam.SustainedShots, Is.EqualTo(0));
            Assert.That(weapon.TryFire(), Is.True);
            Assert.That(weapon.IsJammed, Is.False, "the third shot after the pause is back to the 0.25 chance");
            Assert.That(weapon.Jam.SustainedShots, Is.EqualTo(1));
        }

        [Test]
        public void ClearingForTheClearSecondsLetsItFireAndReloadAgain()
        {
            var weapon = new Weapon(Spec(AlwaysJams(1f)), _jamOn, new FixedJamRoll(0f));
            var started = 0;
            var cleared = 0;
            weapon.JamClearStarted += () => started++;
            weapon.JamCleared += () => cleared++;
            Assert.That(weapon.TryStartClearJam(), Is.False, "nothing to clear");

            weapon.TryFire();
            Assert.That(weapon.TryStartReload(), Is.False, "a jammed weapon does not reload");
            Assert.That(weapon.IsReloading, Is.False);

            Assert.That(weapon.TryStartClearJam(), Is.True);
            Assert.That(started, Is.EqualTo(1));
            Assert.That(weapon.IsClearingJam, Is.True);
            Assert.That(weapon.JamClearRemainingSeconds, Is.EqualTo(1f));
            Assert.That(weapon.TryStartClearJam(), Is.False, "already clearing");
            Assert.That(started, Is.EqualTo(1));

            weapon.Tick(0.5f);
            Assert.That(weapon.TryFire(), Is.False);
            Assert.That(weapon.JamClearRemainingSeconds, Is.EqualTo(0.5f));
            Assert.That(cleared, Is.EqualTo(0));

            weapon.Tick(0.5f);
            Assert.That(cleared, Is.EqualTo(1));
            Assert.That(weapon.IsJammed, Is.False);
            Assert.That(weapon.IsClearingJam, Is.False);
            Assert.That(weapon.JamClearRemainingSeconds, Is.EqualTo(0f));
            Assert.That(weapon.TryStartReload(), Is.True);
        }

        [Test]
        public void FiringRightAfterTheClearWorks()
        {
            var weapon = new Weapon(Spec(AlwaysJams(1f)), _jamOn, new FixedJamRoll(0f));
            weapon.TryFire();
            weapon.TryStartClearJam();

            weapon.Tick(1f);

            Assert.That(weapon.TryFire(), Is.True);
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(8));
        }

        [Test]
        public void SwitchingAwayCancelsTheClearAndLeavesItJammed()
        {
            var weapon = new Weapon(Spec(AlwaysJams(1f)), _jamOn, new FixedJamRoll(0f));
            var cleared = 0;
            weapon.JamCleared += () => cleared++;
            Assert.That(weapon.CancelClearJam(), Is.False, "nothing to cancel");
            weapon.TryFire();
            weapon.TryStartClearJam();
            weapon.Tick(0.5f);

            Assert.That(weapon.CancelClearJam(), Is.True);

            Assert.That(weapon.IsJammed, Is.True);
            Assert.That(weapon.IsClearingJam, Is.False);
            Assert.That(weapon.JamClearRemainingSeconds, Is.EqualTo(0f));
            weapon.Tick(1f);
            Assert.That(weapon.IsJammed, Is.True);
            Assert.That(cleared, Is.EqualTo(0));
            Assert.That(weapon.TryFire(), Is.False);

            Assert.That(weapon.TryStartClearJam(), Is.True);
            Assert.That(weapon.JamClearRemainingSeconds, Is.EqualTo(1f), "the clear starts over");
        }

        [Test]
        public void WithTheRuleOffItNeverJams()
        {
            var roll = new FixedJamRoll(0f);
            var weapon = new Weapon(Spec(AlwaysJams()), GameRules.Default, roll);

            Assert.That(GameRules.Default.Actions.Jam, Is.EqualTo(WeaponJam.Off));
            Assert.That(weapon.Jam, Is.Null);
            for (var shot = 1; shot <= 10; shot++)
            {
                Assert.That(weapon.TryFire(), Is.True, "shot " + shot);
                weapon.Tick(_fireInterval);
            }

            Assert.That(weapon.IsJammed, Is.False);
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(0));
            Assert.That(roll.Calls, Is.EqualTo(0));
            Assert.That(weapon.TryStartClearJam(), Is.False);
            Assert.That(weapon.CancelClearJam(), Is.False);
            Assert.That(weapon.TryStartReload(), Is.True);
        }

        [Test]
        public void WithoutAmmoJamItNeverJamsEvenWithTheRuleOn()
        {
            var roll = new FixedJamRoll(0f);
            var weapon = new Weapon(Spec(null), _jamOn, roll);

            Assert.That(weapon.Jam, Is.Null);
            for (var shot = 1; shot <= 10; shot++)
            {
                Assert.That(weapon.TryFire(), Is.True, "shot " + shot);
                weapon.Tick(_fireInterval);
            }

            Assert.That(weapon.IsJammed, Is.False);
            Assert.That(roll.Calls, Is.EqualTo(0));
        }

        [Test]
        public void TheSavedAmmoConstructorAlsoJams()
        {
            var weapon = new Weapon(Spec(AlwaysJams()), 4, 6, _jamOn, new FixedJamRoll(0f));

            Assert.That(weapon.TryFire(), Is.True);
            Assert.That(weapon.IsJammed, Is.True);
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(3));
        }

        [Test]
        public void TheSameSeedJamsOnTheSameShots()
        {
            var spec = Spec(new JamSpec(0.3f, 0f, 0f, 0.3f, _fireInterval), magazine: 100);
            var expected = new List<int>();
            var reference = new JamRoll(12345);
            for (var shot = 0; shot < 60; shot++)
            {
                if (reference.Next() < 0.3f)
                {
                    expected.Add(shot);
                }
            }

            var first = JamShots(new Weapon(spec, _jamOn, new JamRoll(12345)), 60);
            var second = JamShots(new Weapon(spec, _jamOn, new JamRoll(12345)), 60);

            Assert.That(expected, Is.Not.Empty);
            Assert.That(first, Is.EqualTo(expected));
            Assert.That(second, Is.EqualTo(expected));
        }

        [Test]
        public void WithoutARollTheSeedComesFromTheWeaponId()
        {
            var spec = Spec(new JamSpec(0.3f, 0f, 0f, 0.3f, _fireInterval), magazine: 100);
            var expected = JamShots(new Weapon(spec, _jamOn, JamRoll.ForWeapon(_rifleId)), 60);

            Assert.That(JamShots(new Weapon(spec, _jamOn), 60), Is.EqualTo(expected));
            Assert.That(JamShots(new Weapon(spec, 100, 0, _jamOn), 60), Is.EqualTo(expected));
        }

        [Test]
        public void ServerWeaponRefusesAJammedShot()
        {
            var weapon = new ServerWeapon(Spec(AlwaysJams(1f)), _jamOn, new FixedJamRoll(0f));
            var changes = 0;
            weapon.Changed += () => changes++;

            Assert.That(weapon.TryFire(), Is.EqualTo(ShotRejection.None));
            Assert.That(weapon.IsJammed, Is.True);
            Assert.That(changes, Is.EqualTo(1));

            Assert.That(weapon.TryFire(), Is.EqualTo(ShotRejection.Jammed));
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(9));
            Assert.That(weapon.TryStartReload(), Is.False);
            Assert.That(changes, Is.EqualTo(1), "a refused shot changes nothing");

            Assert.That(weapon.TryStartClearJam(), Is.True);
            Assert.That(weapon.IsClearingJam, Is.True);
            Assert.That(weapon.JamClearRemainingSeconds, Is.EqualTo(1f));
            Assert.That(changes, Is.EqualTo(2));
            Assert.That(weapon.TryStartClearJam(), Is.False);
            Assert.That(changes, Is.EqualTo(2));

            weapon.Tick(0.5f);
            Assert.That(weapon.JamClearRemainingSeconds, Is.EqualTo(0.5f));
            Assert.That(changes, Is.EqualTo(2), "the remaining seconds count down on both sides without a resync");

            weapon.Tick(0.5f);
            Assert.That(weapon.IsJammed, Is.False);
            Assert.That(changes, Is.EqualTo(3));
            Assert.That(weapon.TryFire(), Is.EqualTo(ShotRejection.None));
        }

        [Test]
        public void ServerWeaponCancelingTheClearIsAChange()
        {
            var weapon = new ServerWeapon(Spec(AlwaysJams(1f)), _jamOn, new FixedJamRoll(0f));
            weapon.TryFire();
            weapon.TryStartClearJam();
            var changes = 0;
            weapon.Changed += () => changes++;

            Assert.That(weapon.CancelClearJam(), Is.True);
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(weapon.IsJammed, Is.True);
            Assert.That(weapon.IsClearingJam, Is.False);
            Assert.That(weapon.JamClearRemainingSeconds, Is.EqualTo(0f));
            Assert.That(weapon.CancelClearJam(), Is.False);
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void ServerWeaponJammingOnTheLastRoundReloadsAutomaticallyOnlyAfterTheClear()
        {
            var rules = _jamOn.WithReloadMode(ReloadMode.Auto);
            var weapon = new ServerWeapon(Spec(AlwaysJams(1f), magazine: 1, reserve: 5), rules, new FixedJamRoll(0f));
            var automatic = new List<bool>();
            weapon.ReloadStarted += isAutomatic => automatic.Add(isAutomatic);

            Assert.That(weapon.TryFire(), Is.EqualTo(ShotRejection.None));
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(0));
            Assert.That(weapon.IsReloading, Is.False);
            Assert.That(weapon.TryFire(), Is.EqualTo(ShotRejection.Jammed), "the jam is the reason, not the empty magazine");
            Assert.That(automatic, Is.Empty);

            weapon.TryStartClearJam();
            weapon.Tick(1f);

            Assert.That(weapon.IsJammed, Is.False);
            Assert.That(weapon.IsReloading, Is.True);
            Assert.That(automatic, Is.EqualTo(new[] { true }));
        }

        [Test]
        public void ServerWeaponKeepsDrawingFromTheSameRollAcrossARefill()
        {
            var roll = new FixedJamRoll(0.9f, 0f);
            var weapon = new ServerWeapon(Spec(new JamSpec(0.5f, 0f, 0f, 0.5f, 1f)), _jamOn, roll);

            Assert.That(weapon.TryFire(), Is.EqualTo(ShotRejection.None));
            Assert.That(weapon.IsJammed, Is.False);

            weapon.Refill();
            Assert.That(weapon.TryFire(), Is.EqualTo(ShotRejection.None));

            Assert.That(weapon.IsJammed, Is.True, "the second roll (0) is used, not the first one again");
            Assert.That(roll.Calls, Is.EqualTo(2));

            weapon.Refill();
            Assert.That(weapon.IsJammed, Is.False, "a refill hands out a fresh weapon");
        }

        [Test]
        public void ServerWeaponWithTheRuleOffNeverRefusesForAJam()
        {
            var weapon = new ServerWeapon(Spec(AlwaysJams()), GameRules.Default, new FixedJamRoll(0f));

            for (var shot = 1; shot <= 10; shot++)
            {
                Assert.That(weapon.TryFire(), Is.EqualTo(ShotRejection.None), "shot " + shot);
            }

            Assert.That(weapon.IsJammed, Is.False);
            Assert.That(weapon.TryStartClearJam(), Is.False);
        }

        // 詰まった発の番号を返す。詰まったらすぐ解除を始め、連射間隔と同じ解除の秒数で次の発までに解除し終える。
        private static List<int> JamShots(Weapon weapon, int shots)
        {
            var jammed = new List<int>();
            for (var shot = 0; shot < shots; shot++)
            {
                Assert.That(weapon.TryFire(), Is.True, "shot " + shot);
                if (weapon.IsJammed)
                {
                    jammed.Add(shot);
                    Assert.That(weapon.TryStartClearJam(), Is.True);
                }

                weapon.Tick(_fireInterval);
            }

            return jammed;
        }
    }
}

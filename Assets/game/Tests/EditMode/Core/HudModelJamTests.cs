using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class HudModelJamTests
    {
        private const string _rifleId = "00000000-0000-4000-8000-000000000001";

        // 解除を 1 秒にして 0.25 秒刻みで進めるのは、float の足し引きで誤差が出ず、残りの割合をそのまま比べられるため。
        private const float _clearSeconds = 1f;

        private static readonly GameRules _jamOn = GameRules.Default.WithActions(ActionRules.Default.WithJam(WeaponJam.On));

        private static List<HudField> Record(HudModel model)
        {
            var changes = new List<HudField>();
            model.Changed += changes.Add;
            return changes;
        }

        private static WeaponSpec Spec(JamSpec jam)
        {
            return new WeaponSpec(
                _rifleId,
                null,
                WeaponCategory.AssaultRifle,
                trigger: new TriggerSpec(FireMode.Auto, 0.25f),
                ammo: new AmmoSpec(10, 20, 2f, jam: jam));
        }

        // 1 発目から必ず詰まる (確率 1)。
        private static Weapon AlwaysJamming()
        {
            return new Weapon(Spec(new JamSpec(1f, 0f, 0f, 1f, _clearSeconds)), _jamOn);
        }

        private static int Count(List<HudField> changes, HudField field)
        {
            return changes.FindAll(changed => changed == field).Count;
        }

        [Test]
        public void StartsNotJammed()
        {
            var model = new HudModel();

            Assert.That(model.IsJammed, Is.False);
            Assert.That(model.IsClearingJam, Is.False);
            Assert.That(model.JamClearRemaining, Is.Null);
        }

        [Test]
        public void SetJamAndClearJamReportOnlyChanges()
        {
            var model = new HudModel();
            var changes = Record(model);

            model.SetJam();
            model.SetJam();
            Assert.That(model.IsJammed, Is.True);
            Assert.That(model.IsClearingJam, Is.False);
            Assert.That(model.JamClearRemaining, Is.Null);

            model.SetJam(0.75f);
            model.SetJam(0.75f);
            Assert.That(model.IsJammed, Is.True);
            Assert.That(model.IsClearingJam, Is.True);
            Assert.That(model.JamClearRemaining, Is.EqualTo(0.75f));

            model.SetJam(0.5f);
            Assert.That(model.JamClearRemaining, Is.EqualTo(0.5f));

            model.ClearJam();
            model.ClearJam();
            Assert.That(model.IsJammed, Is.False);
            Assert.That(model.IsClearingJam, Is.False);
            Assert.That(model.JamClearRemaining, Is.Null);

            Assert.That(changes, Is.EqualTo(new[] { HudField.Jam, HudField.Jam, HudField.Jam, HudField.Jam }));
        }

        [Test]
        public void SetJamRejectsAShareOutsideZeroToOne()
        {
            var model = new HudModel();

            Assert.Throws<ArgumentOutOfRangeException>(() => model.SetJam(1.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => model.SetJam(-0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => model.SetJam(float.NaN));
            Assert.That(model.IsJammed, Is.False);
        }

        [Test]
        public void FollowsTheWeaponFromJammedThroughClearingToCleared()
        {
            var weapon = AlwaysJamming();
            var model = new HudModel();
            model.BindWeapon(weapon);
            var changes = Record(model);

            Assert.That(weapon.TryFire(), Is.True);
            Assert.That(model.IsJammed, Is.True);
            Assert.That(model.IsClearingJam, Is.False);
            Assert.That(Count(changes, HudField.Jam), Is.EqualTo(1), "jammed");

            Assert.That(weapon.TryStartClearJam(), Is.True);
            Assert.That(model.IsJammed, Is.True);
            Assert.That(model.IsClearingJam, Is.True);
            Assert.That(model.JamClearRemaining, Is.EqualTo(1f));
            Assert.That(Count(changes, HudField.Jam), Is.EqualTo(2), "clear started");

            weapon.Tick(0.25f);
            model.RefreshWeaponGauges();
            Assert.That(model.JamClearRemaining, Is.EqualTo(0.75f));
            Assert.That(Count(changes, HudField.Jam), Is.EqualTo(3), "the remaining share went down");

            model.RefreshWeaponGauges();
            Assert.That(Count(changes, HudField.Jam), Is.EqualTo(3), "nothing changed between refreshes");

            weapon.Tick(0.75f);
            Assert.That(model.IsJammed, Is.False);
            Assert.That(model.IsClearingJam, Is.False);
            Assert.That(model.JamClearRemaining, Is.Null);
            Assert.That(Count(changes, HudField.Jam), Is.EqualTo(4), "cleared");
        }

        [Test]
        public void ACancelledClearIsJammedAgainAfterTheNextRefresh()
        {
            var weapon = AlwaysJamming();
            var model = new HudModel();
            model.BindWeapon(weapon);
            weapon.TryFire();
            weapon.TryStartClearJam();

            weapon.CancelClearJam();
            model.RefreshWeaponGauges();

            Assert.That(model.IsJammed, Is.True);
            Assert.That(model.IsClearingJam, Is.False);
            Assert.That(model.JamClearRemaining, Is.Null);
        }

        [Test]
        public void SwitchingToAWeaponThatIsNotJammedClearsTheJam()
        {
            var jammed = AlwaysJamming();
            var model = new HudModel();
            model.BindWeapon(jammed);
            jammed.TryFire();
            Assert.That(model.IsJammed, Is.True);

            model.BindWeapon(new Weapon(Spec(null), _jamOn));
            Assert.That(model.IsJammed, Is.False);

            model.BindWeapon(jammed);
            Assert.That(model.IsJammed, Is.True, "the jam stays with the weapon it happened to");

            model.BindWeapon(null);
            Assert.That(model.IsJammed, Is.False);
        }

        [Test]
        public void AnUnboundWeaponNoLongerReportsItsJam()
        {
            var weapon = AlwaysJamming();
            var model = new HudModel();
            model.BindWeapon(weapon);
            model.BindWeapon(null);
            var changes = Record(model);

            weapon.TryFire();
            weapon.TryStartClearJam();
            weapon.Tick(_clearSeconds);

            Assert.That(changes, Is.Empty);

            model.BindWeapon(weapon);
            model.UnbindAll();
            changes.Clear();
            weapon.TryFire();

            Assert.That(changes, Is.Empty, "UnbindAll stops the reports too");
        }

        [Test]
        public void AWeaponWithTheJamRuleOffNeverShowsAJam()
        {
            var weapon = new Weapon(Spec(new JamSpec(1f, 0f, 0f, 1f, _clearSeconds)), GameRules.Default);
            var model = new HudModel();
            model.BindWeapon(weapon);
            var changes = Record(model);

            weapon.TryFire();
            model.RefreshWeaponGauges();

            Assert.That(model.IsJammed, Is.False);
            Assert.That(changes, Has.No.Member(HudField.Jam));
        }
    }
}

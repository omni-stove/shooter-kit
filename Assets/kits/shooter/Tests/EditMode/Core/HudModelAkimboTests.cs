using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class HudModelAkimboTests
    {
        private const string _pistolId = "5c2e8a17-9d43-4f6b-a1e0-3b7d6c9f2e84";
        private const string _rifleId = "00000000-0000-4000-8000-000000000001";
        private const int _magazine = 8;
        private const int _reserve = 32;
        private const float _fireInterval = 0.25f;

        private static AkimboHands NewHands()
        {
            return new AkimboHands(new WeaponSpec(
                _pistolId,
                "weapon.pistol",
                WeaponCategory.Handgun,
                new TriggerSpec(FireMode.Semi, _fireInterval),
                new AmmoSpec(_magazine, _reserve, 1.5f),
                akimbo: AkimboSpec.Default));
        }

        private static Weapon NewRifle()
        {
            return new Weapon(new WeaponSpec(_rifleId, "weapon.rifle", WeaponCategory.AssaultRifle, FireMode.Auto, 30, 90, 0f, 1f, 25, 100f, DamageFalloff.None));
        }

        // 撃つたびに撃ち方の間隔だけ進め、同じ手で続けて撃てるようにする。
        private static void Fire(AkimboHands hands, AkimboHand hand, int times)
        {
            for (var i = 0; i < times; i++)
            {
                Assert.That(hands.TryFire(hand), Is.True, hand + " shot " + (i + 1));
                hands.Tick(_fireInterval);
            }
        }

        private static List<HudField> Record(HudModel model)
        {
            var changes = new List<HudField>();
            model.Changed += changes.Add;
            return changes;
        }

        [Test]
        public void StartsWithoutAnOffhand()
        {
            var model = new HudModel();

            Assert.That(model.OffhandMagazineAmmo, Is.Null);
            Assert.That(model.IsOffhandReloading, Is.False);
            Assert.That(model.IsAkimbo, Is.False);
        }

        [Test]
        public void SettingBothHandsAtOnceSendsOneAmmoChange()
        {
            var model = new HudModel();
            var changes = Record(model);

            model.SetAmmo(6, 24, false, false, 3, true);

            Assert.That(changes, Is.EqualTo(new[] { HudField.Ammo }));
            Assert.That(model.MagazineAmmo, Is.EqualTo(6));
            Assert.That(model.ReserveAmmo, Is.EqualTo(24));
            Assert.That(model.OffhandMagazineAmmo, Is.EqualTo(3));
            Assert.That(model.IsOffhandReloading, Is.True);

            model.SetAmmo(6, 24, false, false, 3, true);
            Assert.That(changes, Has.Count.EqualTo(1), "the same values send nothing");

            model.SetAmmo(30, 90, true, false, null, true);
            Assert.That(changes, Has.Count.EqualTo(2));
            Assert.That(model.OffhandMagazineAmmo, Is.Null);
            Assert.That(model.IsOffhandReloading, Is.False, "no left magazine keeps no left reload");
            Assert.That(model.IsAkimbo, Is.False);
        }

        [Test]
        public void SettingBothHandsRejectsANegativeLeftMagazine()
        {
            var model = new HudModel();

            Assert.Throws<ArgumentOutOfRangeException>(() => model.SetAmmo(6, 24, false, false, -1, false));
            Assert.That(model.MagazineAmmo, Is.Null);
        }

        [Test]
        public void BindingTheHandsShowsBothMagazinesAndTheSharedReserveAtOnce()
        {
            var model = new HudModel();
            var changes = Record(model);

            model.BindAkimbo(NewHands());

            Assert.That(model.IsAkimbo, Is.True);
            Assert.That(model.MagazineAmmo, Is.EqualTo(8));
            Assert.That(model.OffhandMagazineAmmo, Is.EqualTo(8));
            Assert.That(model.ReserveAmmo, Is.EqualTo(32));
            Assert.That(model.WeaponSpec.Id, Is.EqualTo(_pistolId));
            Assert.That(model.WeaponSpec.IsAkimbo, Is.True, "the name comes from the akimbo weapon, not the hand's spec");
            Assert.That(changes, Is.EqualTo(new[] { HudField.Ammo, HudField.WeaponName }));
        }

        [Test]
        public void FiringTheRightHandThreeTimesLeavesRightFiveLeftEightReserveThirtyTwo()
        {
            var model = new HudModel();
            var hands = NewHands();
            model.BindAkimbo(hands);
            var changes = Record(model);

            Fire(hands, AkimboHand.Right, 3);

            Assert.That(model.MagazineAmmo, Is.EqualTo(5));
            Assert.That(model.OffhandMagazineAmmo, Is.EqualTo(8));
            Assert.That(model.ReserveAmmo, Is.EqualTo(32));
            Assert.That(changes, Is.EqualTo(new[] { HudField.Ammo, HudField.Ammo, HudField.Ammo }), "one notice per shot");
        }

        [Test]
        public void FiringTheLeftHandLowersOnlyTheOffhand()
        {
            var model = new HudModel();
            var hands = NewHands();
            model.BindAkimbo(hands);
            var changes = Record(model);

            Fire(hands, AkimboHand.Left, 2);

            Assert.That(model.MagazineAmmo, Is.EqualTo(8));
            Assert.That(model.OffhandMagazineAmmo, Is.EqualTo(6));
            Assert.That(model.ReserveAmmo, Is.EqualTo(32));
            Assert.That(changes, Is.EqualTo(new[] { HudField.Ammo, HudField.Ammo }));
        }

        [Test]
        public void TheLeftReloadIsShownOnTheOffhandAndRefillsFromTheSharedReserve()
        {
            var model = new HudModel();
            var hands = NewHands();
            model.BindAkimbo(hands);
            Fire(hands, AkimboHand.Left, _magazine);
            Assert.That(model.OffhandMagazineAmmo, Is.EqualTo(0));

            Assert.That(hands.TryStartReload(AkimboHand.Left), Is.True);
            model.RefreshWeaponGauges();
            Assert.That(model.IsOffhandReloading, Is.True, "starting a hand's reload has no event, so the per-frame refresh reads it");
            Assert.That(model.IsReloading, Is.False, "the right hand is not reloading");

            hands.Tick(hands.ReloadSeconds);

            Assert.That(model.IsOffhandReloading, Is.False);
            Assert.That(model.OffhandMagazineAmmo, Is.EqualTo(8));
            Assert.That(model.MagazineAmmo, Is.EqualTo(8));
            Assert.That(model.ReserveAmmo, Is.EqualTo(24), "both hands draw from one reserve");
        }

        [Test]
        public void SwitchingToASingleWeaponShowsOneMagazineAgain()
        {
            var model = new HudModel();
            var hands = NewHands();
            model.BindAkimbo(hands);
            Fire(hands, AkimboHand.Left, 1);

            model.BindWeapon(NewRifle());

            Assert.That(model.IsAkimbo, Is.False);
            Assert.That(model.OffhandMagazineAmmo, Is.Null);
            Assert.That(model.IsOffhandReloading, Is.False);
            Assert.That(model.MagazineAmmo, Is.EqualTo(30));
            Assert.That(model.ReserveAmmo, Is.EqualTo(90));
            Assert.That(model.WeaponSpec.Id, Is.EqualTo(_rifleId));
        }

        [Test]
        public void TheUnboundHandsNoLongerChangeTheModel()
        {
            var model = new HudModel();
            var hands = NewHands();
            model.BindAkimbo(hands);
            model.BindWeapon(NewRifle());
            var changes = Record(model);

            Fire(hands, AkimboHand.Right, 1);
            Fire(hands, AkimboHand.Left, 1);

            Assert.That(model.MagazineAmmo, Is.EqualTo(30));
            Assert.That(model.OffhandMagazineAmmo, Is.Null);
            Assert.That(changes, Is.Empty);
        }

        [Test]
        public void UnbindAllReleasesTheHands()
        {
            var model = new HudModel();
            var hands = NewHands();
            model.BindAkimbo(hands);
            model.UnbindAll();
            var changes = Record(model);

            Fire(hands, AkimboHand.Left, 1);

            Assert.That(model.OffhandMagazineAmmo, Is.EqualTo(8), "the last values stay until the HUD binds again");
            Assert.That(changes, Is.Empty);
        }

        [Test]
        public void BindingNoHandsClearsTheAmmoAndTheWeaponName()
        {
            var model = new HudModel();
            model.BindAkimbo(NewHands());

            model.BindAkimbo(null);

            Assert.That(model.MagazineAmmo, Is.Null);
            Assert.That(model.OffhandMagazineAmmo, Is.Null);
            Assert.That(model.ReserveAmmo, Is.Null);
            Assert.That(model.WeaponSpec, Is.Null);
        }

        [Test]
        public void RebindingTheSameWeaponByNewHandsKeepsTheName()
        {
            var model = new HudModel();
            model.BindAkimbo(NewHands());
            var changes = Record(model);

            model.BindAkimbo(NewHands());

            Assert.That(changes, Is.Empty, "the same numbers and the same name need no redraw");
        }

        [Test]
        public void SetOffhandAmmoWritesOnlyTheLeftHand()
        {
            var model = new HudModel();
            model.SetAmmo(5, 32, false);
            var changes = Record(model);

            model.SetOffhandAmmo(8, true);

            Assert.That(model.MagazineAmmo, Is.EqualTo(5));
            Assert.That(model.ReserveAmmo, Is.EqualTo(32));
            Assert.That(model.OffhandMagazineAmmo, Is.EqualTo(8));
            Assert.That(model.IsOffhandReloading, Is.True);

            model.SetAmmo(4, 32, false);
            Assert.That(model.OffhandMagazineAmmo, Is.EqualTo(8), "writing the right hand keeps the left hand");

            model.ClearOffhandAmmo();
            Assert.That(model.OffhandMagazineAmmo, Is.Null);
            Assert.That(model.IsOffhandReloading, Is.False);
            Assert.That(model.MagazineAmmo, Is.EqualTo(4));

            model.SetOffhandAmmo(3, false);
            model.ClearAmmo();
            Assert.That(model.OffhandMagazineAmmo, Is.Null, "clearing the ammo clears both hands");
            Assert.That(changes, Is.EqualTo(new[] { HudField.Ammo, HudField.Ammo, HudField.Ammo, HudField.Ammo, HudField.Ammo }));
        }

        [Test]
        public void SetOffhandAmmoRejectsANegativeMagazine()
        {
            var model = new HudModel();

            Assert.Throws<ArgumentOutOfRangeException>(() => model.SetOffhandAmmo(-1, false));
        }
    }
}

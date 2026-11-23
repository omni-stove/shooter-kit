using System.Collections.Generic;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Edit Mode では Awake・Update が走らないので、引き金は PullTrigger / PullTriggers に押した状態を渡し、時間は Tick で進める。
    public sealed class AkimboControllerTests
    {
        private const string PistolId = "3d6f1a2b-7c84-4e59-9a10-5b2c8e4f7d61";
        private const string PlainPistolId = "8a2e5c71-4b39-4d06-8f1a-6c9d2e7b3a54";
        private const string PistolItemId = "c41e7d92-5a36-4f80-b2d9-7e0a1c6b5f38";
        private const string MagazineItemId = "6f2b9a04-d7e1-4c53-8a6f-3b9e2d1c7a05";
        private const int Magazine = 8;
        private const int Reserve = 16;
        private const float FireInterval = 0.2f;
        private const float ReloadSeconds = 1.5f;
        private const float HipSpreadDegrees = 2f;

        private GameObject _camera;
        private WeaponController _controller;
        private InputAction _attack;
        private InputAction _reload;
        private readonly List<Object> _created = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _camera = new GameObject("Camera", typeof(Camera));
            _controller = _camera.AddComponent<WeaponController>();
            _attack = new InputAction("Attack", InputActionType.Button);
            _reload = new InputAction("Reload", InputActionType.Button);
        }

        [TearDown]
        public void TearDown()
        {
            _attack.Dispose();
            _reload.Dispose();
            Object.DestroyImmediate(_camera);
            foreach (var created in _created)
            {
                Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        [Test]
        public void AnAkimboWeaponHoldsTwoHandsWithFullMagazines()
        {
            Hold(Pistol(AkimboSpec.Default));

            var hands = _controller.Akimbo;
            Assert.That(_controller.IsAkimbo, Is.True);
            Assert.That(hands, Is.Not.Null);
            Assert.That(hands.Spec.Id, Is.EqualTo(PistolId));
            Assert.That(hands.AmmoInMagazine(AkimboHand.Right), Is.EqualTo(Magazine));
            Assert.That(hands.AmmoInMagazine(AkimboHand.Left), Is.EqualTo(Magazine));
            Assert.That(hands.ReserveAmmo, Is.EqualTo(Reserve));
        }

        [Test]
        public void AWeaponWithoutTheAkimboPartHasNoHands()
        {
            Hold(Pistol(null));

            Assert.That(_controller.IsAkimbo, Is.False);
            Assert.That(_controller.Akimbo, Is.Null);
            Assert.That(_controller.PullTrigger(AkimboHand.Left, true, true), Is.False, "the left trigger does nothing on a single weapon");
            Assert.That(_controller.Fire(AkimboHand.Left), Is.False);
            Assert.That(_controller.Weapon.AmmoInMagazine, Is.EqualTo(Magazine));
        }

        [Test]
        public void TheDefinitionCarriesTheAkimboPart()
        {
            var definition = Definition(Pistol(new AkimboSpec(2f, false, 1.2f, 0.9f, 0.1f)));

            Assert.That(definition.Akimbo.Enabled, Is.True);
            var akimbo = definition.ToSpec().Akimbo;
            Assert.That(akimbo, Is.Not.Null);
            Assert.That(akimbo.SpreadMultiplier, Is.EqualTo(2f));
            Assert.That(akimbo.BlocksAds, Is.False);
            Assert.That(akimbo.ReloadSecondsMultiplier, Is.EqualTo(1.2f));
            Assert.That(akimbo.MoveSpeedMultiplier, Is.EqualTo(0.9f));
            Assert.That(akimbo.OffhandShotDelaySeconds, Is.EqualTo(0.1f));

            var plain = Definition(Pistol(null));
            Assert.That(plain.Akimbo.Enabled, Is.False);
            Assert.That(plain.ToSpec().Akimbo, Is.Null);
        }

        [Test]
        public void TheRightTriggerFiresTheRightHandAndTheLeftTriggerTheLeftHand()
        {
            Hold(Pistol(AkimboSpec.Default));
            var fired = new List<AkimboHand>();
            _controller.FiredHand += (_, hand) => fired.Add(hand);

            LogAssert.Expect(LogType.Log, "[Combat] akimbo fire hand=right ammo=7");
            Assert.That(_controller.PullTrigger(AkimboHand.Right, true, true), Is.True);
            LogAssert.Expect(LogType.Log, "[Combat] akimbo fire hand=left ammo=7");
            Assert.That(_controller.PullTrigger(AkimboHand.Left, true, true), Is.True);

            var hands = _controller.Akimbo;
            Assert.That(hands.AmmoInMagazine(AkimboHand.Right), Is.EqualTo(7));
            Assert.That(hands.AmmoInMagazine(AkimboHand.Left), Is.EqualTo(7));
            Assert.That(fired, Is.EqualTo(new[] { AkimboHand.Right, AkimboHand.Left }));
            Assert.That(_controller.ShotCount, Is.EqualTo(2));
        }

        [Test]
        public void EachHandHasItsOwnCooldown()
        {
            Hold(Pistol(AkimboSpec.Default));

            Assert.That(_controller.PullTrigger(AkimboHand.Right, true, true), Is.True);
            Assert.That(_controller.PullTrigger(AkimboHand.Right, true, true), Is.False, "the right hand is still cooling down");
            Assert.That(_controller.PullTrigger(AkimboHand.Left, true, true), Is.True, "the left hand did not fire yet");

            _controller.Tick(FireInterval);
            Assert.That(_controller.PullTrigger(AkimboHand.Right, true, true), Is.True);
            Assert.That(_controller.Akimbo.AmmoInMagazine(AkimboHand.Right), Is.EqualTo(6));
            Assert.That(_controller.Akimbo.AmmoInMagazine(AkimboHand.Left), Is.EqualTo(7));
        }

        [Test]
        public void EmptyingTheRightHandLeavesTheLeftHandFiring()
        {
            Hold(Pistol(AkimboSpec.Default));

            for (var i = 0; i < Magazine; i++)
            {
                Assert.That(_controller.PullTrigger(AkimboHand.Right, true, true), Is.True, "shot " + (i + 1));
                _controller.Tick(FireInterval);
            }

            var hands = _controller.Akimbo;
            Assert.That(hands.AmmoInMagazine(AkimboHand.Right), Is.EqualTo(0));
            Assert.That(hands.AmmoInMagazine(AkimboHand.Left), Is.EqualTo(8));
            Assert.That(_controller.PullTrigger(AkimboHand.Right, true, true), Is.False);
            Assert.That(_controller.PullTrigger(AkimboHand.Left, true, true), Is.True, "the left hand still has rounds");
            Assert.That(hands.AmmoInMagazine(AkimboHand.Left), Is.EqualTo(7));
        }

        [Test]
        public void AutoReloadStartsOnlyTheEmptiedHand()
        {
            Assert.That(GameRules.Default.ReloadMode, Is.EqualTo(ReloadMode.Auto));
            Hold(Pistol(AkimboSpec.Default));
            var started = new List<Weapon>();
            _controller.ReloadStarted += started.Add;
            var hands = _controller.Akimbo;

            _controller.PullTrigger(AkimboHand.Left, true, true);
            for (var i = 0; i < Magazine - 1; i++)
            {
                _controller.PullTrigger(AkimboHand.Right, true, true);
                _controller.Tick(FireInterval);
            }

            Assert.That(hands.IsHandReloading(AkimboHand.Right), Is.False, "a round is still in the right magazine");
            LogAssert.Expect(LogType.Log, "[Combat] akimbo fire hand=right ammo=0");
            LogAssert.Expect(LogType.Log, "[Combat] akimbo reload hand=right");
            Assert.That(_controller.PullTrigger(AkimboHand.Right, true, true), Is.True);

            Assert.That(hands.IsHandReloading(AkimboHand.Right), Is.True);
            Assert.That(hands.IsHandReloading(AkimboHand.Left), Is.False, "the left hand has rounds and keeps firing");
            Assert.That(started, Is.EqualTo(new[] { hands.Right }));

            _controller.Tick(FireInterval);
            Assert.That(_controller.PullTrigger(AkimboHand.Left, true, true), Is.True, "the left hand fires while the right reloads");

            _controller.Tick(ReloadSeconds);
            Assert.That(hands.AmmoInMagazine(AkimboHand.Right), Is.EqualTo(8));
            Assert.That(hands.AmmoInMagazine(AkimboHand.Left), Is.EqualTo(6));
            Assert.That(hands.ReserveAmmo, Is.EqualTo(8), "the reserve is shared, so the right reload took 8 of 16");
        }

        [Test]
        public void TheReloadButtonReloadsEveryHandThatIsNotFull()
        {
            _controller.Rules = GameRules.Default.WithReloadMode(ReloadMode.Manual);
            Hold(Pistol(AkimboSpec.Default));
            var hands = _controller.Akimbo;

            _controller.PullTrigger(AkimboHand.Right, true, true);
            LogAssert.Expect(LogType.Log, "[Combat] akimbo reload hand=right");
            Assert.That(_controller.PressReload(), Is.True);
            Assert.That(hands.IsHandReloading(AkimboHand.Right), Is.True);
            Assert.That(hands.IsHandReloading(AkimboHand.Left), Is.False, "a full hand does not reload");

            _controller.Tick(ReloadSeconds);
            _controller.PullTrigger(AkimboHand.Right, true, true);
            _controller.PullTrigger(AkimboHand.Left, true, true);
            LogAssert.Expect(LogType.Log, "[Combat] akimbo reload hand=right");
            LogAssert.Expect(LogType.Log, "[Combat] akimbo reload hand=left");
            Assert.That(_controller.StartReload(), Is.True);
            Assert.That(hands.IsHandReloading(AkimboHand.Right), Is.True);
            Assert.That(hands.IsHandReloading(AkimboHand.Left), Is.True);
            Assert.That(_controller.PullTrigger(AkimboHand.Left, true, true), Is.False, "a reloading hand does not fire");

            _controller.Tick(ReloadSeconds);
            Assert.That(hands.AmmoInMagazine(AkimboHand.Right), Is.EqualTo(8));
            Assert.That(hands.AmmoInMagazine(AkimboHand.Left), Is.EqualTo(8));
            Assert.That(hands.ReserveAmmo, Is.EqualTo(Reserve - 3));
        }

        [Test]
        public void TheReloadSecondsComeFromTheAkimboPart()
        {
            _controller.Rules = GameRules.Default.WithReloadMode(ReloadMode.Manual);
            Hold(Pistol(new AkimboSpec(reloadSecondsMultiplier: 2f)));
            var hands = _controller.Akimbo;

            _controller.PullTrigger(AkimboHand.Left, true, true);
            Assert.That(_controller.StartReload(), Is.True);
            _controller.Tick(ReloadSeconds);
            Assert.That(hands.IsHandReloading(AkimboHand.Left), Is.True, "the hand reloads for 1.5 x 2 = 3 seconds");

            _controller.Tick(ReloadSeconds);
            Assert.That(hands.IsHandReloading(AkimboHand.Left), Is.False);
            Assert.That(hands.AmmoInMagazine(AkimboHand.Left), Is.EqualTo(8));
        }

        [Test]
        public void PressingBothTriggersInOneFrameDelaysTheLeftHand()
        {
            Hold(Pistol(new AkimboSpec(offhandShotDelaySeconds: 0.1f)));
            var hands = _controller.Akimbo;

            Assert.That(_controller.PullTriggers(true, true, true, true), Is.True);
            Assert.That(hands.AmmoInMagazine(AkimboHand.Right), Is.EqualTo(7));
            Assert.That(hands.AmmoInMagazine(AkimboHand.Left), Is.EqualTo(8), "the left shot waits");
            Assert.That(_controller.IsOffhandShotDelayed, Is.True);

            _controller.Tick(0.05f);
            Assert.That(_controller.PullTriggers(true, false, true, false), Is.False);
            Assert.That(hands.AmmoInMagazine(AkimboHand.Left), Is.EqualTo(8), "0.05 of the 0.1 seconds have passed");

            _controller.Tick(0.05f);
            Assert.That(_controller.PullTriggers(true, false, false, false), Is.True, "the delayed press fires even after the button is let go");
            Assert.That(hands.AmmoInMagazine(AkimboHand.Left), Is.EqualTo(7));
            Assert.That(_controller.IsOffhandShotDelayed, Is.False);
        }

        [Test]
        public void ANoDelayAkimboPartFiresBothHandsInTheSameFrame()
        {
            Hold(Pistol(new AkimboSpec(offhandShotDelaySeconds: 0f)));
            var hands = _controller.Akimbo;

            Assert.That(_controller.PullTriggers(true, true, true, true), Is.True);
            Assert.That(hands.AmmoInMagazine(AkimboHand.Right), Is.EqualTo(7));
            Assert.That(hands.AmmoInMagazine(AkimboHand.Left), Is.EqualTo(7));
            Assert.That(_controller.IsOffhandShotDelayed, Is.False);
        }

        [Test]
        public void PressingOnlyTheLeftTriggerIsNotDelayed()
        {
            Hold(Pistol(AkimboSpec.Default));

            Assert.That(_controller.PullTriggers(false, false, true, true), Is.True);
            Assert.That(_controller.Akimbo.AmmoInMagazine(AkimboHand.Left), Is.EqualTo(7));
            Assert.That(_controller.Akimbo.AmmoInMagazine(AkimboHand.Right), Is.EqualTo(8));
        }

        [Test]
        public void SwitchingAwayKeepsEachHandsMagazineAndDropsTheDelayedShot()
        {
            _controller.Rules = GameRules.Default.WithReloadMode(ReloadMode.Manual);
            var akimbo = Definition(Pistol(AkimboSpec.Default));
            var plain = Definition(Pistol(null, PlainPistolId));
            _controller.Initialize(_camera.transform, _attack, _reload, null, null, new[] { akimbo, plain });
            var hands = _controller.Akimbo;

            _controller.PullTrigger(AkimboHand.Left, true, true);
            _controller.Tick(FireInterval);
            _controller.PullTriggers(true, true, true, true);
            Assert.That(_controller.IsOffhandShotDelayed, Is.True);

            Assert.That(_controller.SelectNext(), Is.True);
            _controller.Tick(1f);
            Assert.That(_controller.IsAkimbo, Is.False);
            Assert.That(_controller.IsOffhandShotDelayed, Is.False);
            Assert.That(_controller.PullTrigger(AkimboHand.Left, true, true), Is.False);

            Assert.That(_controller.SelectPrevious(), Is.True);
            _controller.Tick(1f);
            Assert.That(_controller.Akimbo, Is.SameAs(hands));
            Assert.That(hands.AmmoInMagazine(AkimboHand.Right), Is.EqualTo(7));
            Assert.That(hands.AmmoInMagazine(AkimboHand.Left), Is.EqualTo(7), "the delayed left shot was dropped by the switch");
        }

        [Test]
        public void SnapshotAndRestoreUseTheRightHandAndTheSharedReserve()
        {
            _controller.Rules = GameRules.Default.WithReloadMode(ReloadMode.Manual);
            Hold(Pistol(AkimboSpec.Default));
            _controller.PullTrigger(AkimboHand.Right, true, true);
            _controller.StartReload();
            _controller.Tick(ReloadSeconds);
            _controller.Tick(FireInterval);
            _controller.PullTrigger(AkimboHand.Right, true, true);

            var saved = _controller.SnapshotAmmo();
            Assert.That(saved.Count, Is.EqualTo(1));
            Assert.That(saved[0].WeaponId, Is.EqualTo(PistolId));
            Assert.That(saved[0].AmmoInMagazine, Is.EqualTo(7));
            Assert.That(saved[0].ReserveAmmo, Is.EqualTo(Reserve - 1));

            _controller.RestoreLoadout(new[] { new WeaponAmmo(PistolId, 3, 5) }, PistolId);
            var restored = _controller.Akimbo;
            Assert.That(restored.AmmoInMagazine(AkimboHand.Right), Is.EqualTo(3));
            Assert.That(restored.AmmoInMagazine(AkimboHand.Left), Is.EqualTo(3), "a save holds one magazine, so both hands start from it");
            Assert.That(restored.ReserveAmmo, Is.EqualTo(5));
        }

        [Test]
        public void DroppingTheWeaponMovesTheLeftMagazineIntoItsReserve()
        {
            _controller.Rules = GameRules.Default.WithReloadMode(ReloadMode.Manual);
            var akimbo = Definition(Pistol(AkimboSpec.Default));
            var plain = Definition(Pistol(null, PlainPistolId));
            _controller.Initialize(_camera.transform, _attack, _reload, null, null, new[] { akimbo, plain });
            _controller.PullTrigger(AkimboHand.Right, true, true);
            _controller.PullTrigger(AkimboHand.Left, true, true);
            _controller.Tick(FireInterval);
            _controller.PullTrigger(AkimboHand.Left, true, true);

            Assert.That(_controller.DropWeapon(PistolId, out var dropped), Is.True);
            Assert.That(dropped.Ammo.AmmoInMagazine, Is.EqualTo(7));
            Assert.That(dropped.Ammo.ReserveAmmo, Is.EqualTo(Reserve + 6));
            Assert.That(_controller.IsAkimbo, Is.False);
        }

        [Test]
        public void PickingTheDroppedWeaponBackUpStartsTheLeftHandEmpty()
        {
            _controller.Rules = GameRules.Default.WithReloadMode(ReloadMode.Manual);
            var akimbo = Definition(Pistol(AkimboSpec.Default));
            var plain = Definition(Pistol(null, PlainPistolId));
            _controller.Initialize(_camera.transform, _attack, _reload, null, null, new[] { akimbo, plain });
            _controller.PullTrigger(AkimboHand.Right, true, true);
            _controller.PullTrigger(AkimboHand.Left, true, true);

            Assert.That(_controller.DropWeapon(PistolId, out var dropped), Is.True);
            Assert.That(dropped.Ammo.AmmoInMagazine, Is.EqualTo(7));
            Assert.That(dropped.Ammo.ReserveAmmo, Is.EqualTo(23), "the left magazine's 7 rounds went into the reserve");

            Assert.That(_controller.AddWeapon(akimbo, dropped.Ammo), Is.True);
            Assert.That(_controller.Select(PistolId), Is.True);
            _controller.Tick(1f);
            var hands = _controller.Akimbo;
            Assert.That(hands, Is.Not.Null);
            Assert.That(hands.AmmoInMagazine(AkimboHand.Right), Is.EqualTo(7));
            Assert.That(hands.AmmoInMagazine(AkimboHand.Left), Is.EqualTo(0), "the left rounds are already in the reserve, so they are not counted twice");
            Assert.That(hands.ReserveAmmo, Is.EqualTo(23));
        }

        [Test]
        public void DroppingAndPickingUpWithAnAmmoPoolKeepsTheLeftMagazineRounds()
        {
            _controller.Rules = GameRules.Default.WithReloadMode(ReloadMode.Manual);
            var akimbo = Definition(Pistol(AkimboSpec.Default));
            var plain = Definition(Pistol(null, PlainPistolId));
            var pool = new FakePool(PistolId, 16, 100);
            _controller.Initialize(_camera.transform, _attack, _reload, null, null, new[] { akimbo, plain });
            _controller.UseAmmoPools(spec => spec.Id == PistolId ? pool : null);
            _controller.PullTrigger(AkimboHand.Right, true, true);
            _controller.PullTrigger(AkimboHand.Left, true, true);
            Assert.That(pool.Count, Is.EqualTo(16));

            Assert.That(_controller.DropWeapon(PistolId, out var dropped), Is.True);
            Assert.That(dropped.Ammo.AmmoInMagazine, Is.EqualTo(7));
            Assert.That(dropped.Ammo.ReserveAmmo, Is.EqualTo(0), "the left magazine's 7 rounds went back into the pool");
            Assert.That(pool.Count, Is.EqualTo(23));

            Assert.That(_controller.AddWeapon(akimbo, dropped.Ammo), Is.True);
            Assert.That(_controller.Select(PistolId), Is.True);
            _controller.Tick(1f);
            Assert.That(pool.Count, Is.EqualTo(23), "picking it back up does not change the pool");
        }

        [Test]
        public void PickingUpAWeaponWithoutAmmoFillsBothHands()
        {
            var akimbo = Definition(Pistol(AkimboSpec.Default));
            var plain = Definition(Pistol(null, PlainPistolId));
            _controller.Initialize(_camera.transform, _attack, _reload, null, null, new[] { plain });

            Assert.That(_controller.AddWeapon(akimbo), Is.True);
            Assert.That(_controller.Select(PistolId), Is.True);
            _controller.Tick(1f);
            var hands = _controller.Akimbo;
            Assert.That(hands, Is.Not.Null);
            Assert.That(hands.AmmoInMagazine(AkimboHand.Right), Is.EqualTo(Magazine));
            Assert.That(hands.AmmoInMagazine(AkimboHand.Left), Is.EqualTo(Magazine));
            Assert.That(hands.ReserveAmmo, Is.EqualTo(Reserve));
        }

        [Test]
        public void ShrinkingTheMagazineMovesBothHandsExcessIntoTheReserve()
        {
            _controller.Rules = GameRules.Default.WithReloadMode(ReloadMode.Manual);
            _controller.AttachmentCatalog = MagazineCatalog();
            Hold(Pistol(AkimboSpec.Default));
            var attached = new Dictionary<string, IReadOnlyList<string>> { [PistolId] = new[] { MagazineItemId } };
            _controller.RestoreLoadout(new[] { new WeaponAmmo(PistolId, 12, Reserve) }, PistolId, attached);
            var before = _controller.Akimbo;
            Assert.That(before.AmmoInMagazine(AkimboHand.Right), Is.EqualTo(12));
            Assert.That(before.AmmoInMagazine(AkimboHand.Left), Is.EqualTo(12));

            Assert.That(_controller.SetAttachments(PistolId, new string[0]), Is.True);

            var hands = _controller.Akimbo;
            Assert.That(hands.Spec.MagazineSize, Is.EqualTo(Magazine));
            Assert.That(hands.AmmoInMagazine(AkimboHand.Right), Is.EqualTo(8));
            Assert.That(hands.AmmoInMagazine(AkimboHand.Left), Is.EqualTo(8));
            Assert.That(hands.ReserveAmmo, Is.EqualTo(24), "4 rounds from each hand go back into the reserve of 16");
        }

        [Test]
        public void TheAkimboSpreadMultiplierWidensTheCone()
        {
            var akimbo = Definition(Pistol(new AkimboSpec(spreadMultiplier: 1.5f)));
            var plain = Definition(Pistol(null, PlainPistolId));
            _controller.Initialize(_camera.transform, _attack, _reload, null, null, new[] { akimbo, plain });
            var hands = _controller.Akimbo;

            var single = _controller.CurrentConeDegrees(plain.ToSpec());
            Assert.That(single, Is.EqualTo(HipSpreadDegrees).Within(1e-4f));
            Assert.That(_controller.CurrentConeDegrees(_controller.Weapon.Spec), Is.EqualTo(3f).Within(1e-4f));
            Assert.That(_controller.CurrentConeDegrees(hands.Left.Spec), Is.EqualTo(3f).Within(1e-4f), "the shot is planned with the hand's spec");
            Assert.That(_controller.CrosshairConeDegrees, Is.EqualTo(3f).Within(1e-4f));
        }

        [Test]
        public void TheAkimboMoveSpeedMultiplierSlowsTheCarrier()
        {
            var akimbo = Definition(Pistol(new AkimboSpec(moveSpeedMultiplier: 0.8f)));
            var plain = Definition(Pistol(null, PlainPistolId));
            _controller.Initialize(_camera.transform, _attack, _reload, null, null, new[] { akimbo, plain });

            Assert.That(_controller.AkimboMoveSpeedMultiplier, Is.EqualTo(0.8f));
            var held = _controller.CarrySpeedMultiplier(CarryLoadSpec.Default);

            Assert.That(_controller.SelectNext(), Is.True);
            _controller.Tick(1f);
            Assert.That(_controller.AkimboMoveSpeedMultiplier, Is.EqualTo(1f));
            var single = _controller.CarrySpeedMultiplier(CarryLoadSpec.Default);

            Assert.That(held, Is.EqualTo(single * 0.8f).Within(1e-5f));
        }

        [Test]
        public void TheUnarmedControllerHasNoHands()
        {
            Hold(Pistol(AkimboSpec.Default));

            Assert.That(_controller.Disarm(), Is.True);
            Assert.That(_controller.Akimbo, Is.Null);
            Assert.That(_controller.Fire(AkimboHand.Right), Is.False);
            Assert.That(_controller.AkimboMoveSpeedMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void AimDownSightsRefusesWhileHoldingAnAdsBlockingAkimboWeapon()
        {
            var body = new GameObject("Body");
            _created.Add(body);
            var eye = new GameObject("Eye").transform;
            eye.SetParent(body.transform, false);
            var cameraObject = new GameObject("AdsCamera");
            cameraObject.transform.SetParent(eye, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            var look = body.AddComponent<FirstPersonLook>();
            var ads = body.AddComponent<AimDownSights>();
            var aim = new InputAction("Aim", InputActionType.Button);
            try
            {
                ads.Initialize(camera, look, aim);
                var blocking = Pistol(AkimboSpec.Default);
                var allowing = Pistol(new AkimboSpec(blocksAds: false));
                var held = blocking;
                ads.WeaponSource = () => held;

                ads.Tick(true, true, ads.TransitionSeconds);
                Assert.That(ads.IsAiming, Is.False, "the aim button fires the left hand instead");
                Assert.That(camera.fieldOfView, Is.EqualTo(60f).Within(1e-4f));

                held = allowing;
                LogAssert.Expect(LogType.Log, "[View] ads=true");
                ads.Tick(true, false, 0f);
                Assert.That(ads.IsAiming, Is.True, "an akimbo part that allows ADS aims while the button is held");

                held = Pistol(null);
                Assert.That(held.IsAkimbo, Is.False);
                ads.Tick(true, false, 0f);
                Assert.That(ads.IsAiming, Is.True);
            }
            finally
            {
                aim.Dispose();
            }
        }

        private sealed class FakePool : IAmmoPool
        {
            private readonly int _max;

            public FakePool(string id, int count, int max)
            {
                Id = id;
                Count = count;
                _max = max;
            }

            public event System.Action Changed;

            public string Id { get; }

            public int Count { get; private set; }

            public int Room => _max - Count;

            public int Take(int count)
            {
                var taken = System.Math.Min(count, Count);
                Count -= taken;
                Changed?.Invoke();
                return taken;
            }

            public int Put(int count)
            {
                var put = System.Math.Min(count, Room);
                Count += put;
                Changed?.Invoke();
                return put;
            }
        }

        private static ItemCatalog MagazineCatalog()
        {
            var pistolItem = new ItemSpec(PistolItemId, "weapon.pistol", weaponId: PistolId, attachmentSlots: new[] { "magazine" });
            var magazine = new ItemSpec(MagazineItemId, "item.extendedMagazine", attachment: new AttachmentSpec("magazine", magazineSizeBonus: 4));
            return new ItemCatalog(new[] { pistolItem, magazine }, InventoryRule.Default);
        }

        [Test]
        public void AJammedRightHandIsClearedByReloadAndFiresAgain()
        {
            HoldJamming();

            Assert.That(_controller.PullTrigger(AkimboHand.Right, true, true), Is.True);
            Assert.That(_controller.JamStatus.IsJammed, Is.True, "the right hand jammed");
            Assert.That(_controller.Akimbo.Right.IsJammed, Is.True);
            Assert.That(_controller.Akimbo.Left.IsJammed, Is.False);

            Assert.That(_controller.PressReload(), Is.True, "R clears the jammed hand");
            Assert.That(_controller.Akimbo.Right.IsClearingJam, Is.True);
            _controller.Tick(TestJam.ClearSeconds);

            Assert.That(_controller.JamStatus.IsJammed, Is.False);
            Assert.That(_controller.Akimbo.Right.IsJammed, Is.False);
            Assert.That(_controller.PullTrigger(AkimboHand.Right, true, true), Is.True, "the cleared hand fires again");
        }

        [Test]
        public void TheJamRollsOfTheControllerDecideTheJamOfEachHand()
        {
            HoldJamming();

            Assert.That(_controller.PullTrigger(AkimboHand.Right, true, true), Is.True);
            Assert.That(_controller.Akimbo.Left.IsJammed, Is.False, "firing the right hand does not jam the left");
            Assert.That(_controller.PullTrigger(AkimboHand.Left, true, true), Is.True);
            Assert.That(_controller.Akimbo.Right.IsJammed, Is.True, "the always-jam roll set on the controller reaches the right hand");
            Assert.That(_controller.Akimbo.Left.IsJammed, Is.True, "and the left hand");
        }

        private void HoldJamming()
        {
            var definition = Definition(Pistol(AkimboSpec.Default));
            definition.Jam.Set(TestJam.HalfChance());
            _controller.Rules = TestJam.JamOn;
            _controller.JamRolls = _ => new FixedJamRoll(0f);
            _controller.Initialize(_camera.transform, _attack, _reload, definition);
        }

        private void Hold(WeaponSpec spec)
        {
            _controller.Initialize(_camera.transform, _attack, _reload, Definition(spec));
        }

        private WeaponDefinition Definition(WeaponSpec spec)
        {
            var definition = ScriptableObject.CreateInstance<WeaponDefinition>();
            definition.name = spec.IsAkimbo ? "AkimboPistol" : "Pistol";
            WeaponAssets.Configure(definition, spec);
            _created.Add(definition);
            return definition;
        }

        private static WeaponSpec Pistol(AkimboSpec akimbo, string id = PistolId)
        {
            return new WeaponSpec(
                id,
                "weapon.pistol",
                WeaponCategory.Handgun,
                new TriggerSpec(FireMode.Semi, FireInterval),
                new AmmoSpec(Magazine, Reserve, ReloadSeconds),
                impact: new ImpactSpec(30),
                aim: new AimSpec(HipSpreadDegrees),
                akimbo: akimbo);
        }
    }
}

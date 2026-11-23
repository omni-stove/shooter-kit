using System;
using System.Collections.Generic;
using System.Linq;
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
    // Edit Mode では Update が走らないので、ボタンと時間は Tick で渡す。秒数は 2 の累乗の分数にして、丸めで落ち着く時刻がずれないようにする。
    public sealed class HolsterControllerTests
    {
        private const float RifleDrawSeconds = 0.25f;
        private const float RifleHolsterSeconds = 0.5f;
        private const float HandgunDrawSeconds = 0.125f;

        private readonly List<Object> _created = new List<Object>();
        private readonly List<HolsterChange> _changes = new List<HolsterChange>();

        private GameObject _body;
        private Damageable _damageable;
        private WeaponController _weapon;
        private HolsterController _holster;
        private InputAction _attack;
        private InputAction _reload;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            CombatEvents.ClearSubscribers();
            CombatEvents.HolsterChanged += _changes.Add;

            _body = new GameObject("Body");
            _damageable = _body.AddComponent<Damageable>();
            _damageable.Initialize("Player", Damageable.DefaultMaxHealth);
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.SetParent(_body.transform, false);
            camera.transform.localPosition = new Vector3(0f, 1.6f, 0f);

            var rifle = Definition("Rifle", WithTimes(TestWeapons.RifleSpec(), RifleDrawSeconds, RifleHolsterSeconds));
            var handgun = Definition("Handgun", WithTimes(TestWeapons.HandgunSpec(), HandgunDrawSeconds, 0f));
            _attack = new InputAction("Attack", InputActionType.Button);
            _reload = new InputAction("Reload", InputActionType.Button);
            _weapon = camera.gameObject.AddComponent<WeaponController>();
            _weapon.Initialize(camera.transform, _attack, _reload, null, null, new[] { rifle, handgun });

            _holster = _body.AddComponent<HolsterController>();
            _holster.Spec = HolsterSpec.Default;
            _holster.Initialize(_weapon, _damageable);
        }

        [TearDown]
        public void TearDown()
        {
            _attack.Dispose();
            _reload.Dispose();
            Object.DestroyImmediate(_body);
            foreach (var created in _created)
            {
                Object.DestroyImmediate(created);
            }

            _created.Clear();
            _changes.Clear();
            CombatEvents.ClearSubscribers();
            GameSignals.ResetAll();
        }

        [Test]
        public void InitializingHooksIntoTheWeaponAndPublishesTheDrawnState()
        {
            Assert.That(_holster.IsInitialized, Is.True);
            Assert.That(_weapon.Modifiers, Does.Contain(_holster));
            Assert.That(_holster.Phase, Is.EqualTo(HolsterPhase.Drawn));
            Assert.That(_holster.BlocksFire, Is.False);
            Assert.That(_holster.BlocksReload, Is.False);
            Assert.That(GameSignals.Holster.Value, Is.EqualTo(new HolsterStatus(HolsterPhase.Drawn, 0f, true)));
        }

        [Test]
        public void InitializingWithoutAWeaponThrows()
        {
            Assert.Throws<ArgumentNullException>(() => _holster.Initialize(null, _damageable));
            Assert.Throws<ArgumentNullException>(() => _holster.Configure(null));
        }

        [Test]
        public void TheHolsterKeyLowersTheWeaponOverTheHolsterSecondsAndThenHolstersIt()
        {
            LogAssert.Expect(LogType.Log, "[Holster] holder=Body began=Holstering seconds=0.50");
            Press();

            Assert.That(_holster.Phase, Is.EqualTo(HolsterPhase.Holstering));
            Assert.That(_holster.RemainingSeconds, Is.EqualTo(RifleHolsterSeconds));
            Assert.That(_weapon.HolsterLowering, Is.Zero);

            Advance(0.25f);
            Assert.That(_holster.Phase, Is.EqualTo(HolsterPhase.Holstering));
            Assert.That(_holster.Lowering, Is.EqualTo(0.5f));
            Assert.That(_weapon.HolsterLowering, Is.EqualTo(0.5f));
            Assert.That(_weapon.SwitchLowering, Is.EqualTo(0.5f));
            Assert.That(_weapon.IsHolstered, Is.False, "the weapon is only claimed once it is fully away");
            Assert.That(GameSignals.Holster.Value, Is.EqualTo(new HolsterStatus(HolsterPhase.Holstering, 0.5f, false)));
            Assert.That(_changes, Is.Empty, "the in-between steps are read from GameSignals, not raised");

            LogAssert.Expect(LogType.Log, "[Holster] holder=Body settled=Holstered");
            Advance(0.25f);
            Assert.That(_holster.Phase, Is.EqualTo(HolsterPhase.Holstered));
            Assert.That(_weapon.HolsterLowering, Is.EqualTo(1f));
            Assert.That(_weapon.IsHolstered, Is.True);
            Assert.That(_weapon.Holster.IsHeldBy(_holster), Is.True);
            Assert.That(GameSignals.Holster.Value, Is.EqualTo(new HolsterStatus(HolsterPhase.Holstered, 1f, false)));
            Assert.That(_changes.Count, Is.EqualTo(1));
            Assert.That(_changes[0].Holder, Is.SameAs(_body));
            Assert.That(_changes[0].Phase, Is.EqualTo(HolsterPhase.Holstered));
        }

        [Test]
        public void PressingAgainDrawsOverTheDrawSecondsAndReleasesTheWeaponAtTheStart()
        {
            HolsterFully();

            LogAssert.Expect(LogType.Log, "[Holster] holder=Body began=Drawing seconds=0.25");
            Press();
            Assert.That(_holster.Phase, Is.EqualTo(HolsterPhase.Drawing));
            Assert.That(_weapon.IsHolstered, Is.False, "drawing releases the claim right away");
            Assert.That(_holster.RemainingSeconds, Is.EqualTo(RifleDrawSeconds));

            Advance(0.125f);
            Assert.That(_weapon.HolsterLowering, Is.EqualTo(0.5f));
            Assert.That(_weapon.Fire(), Is.False, "it cannot fire while drawing");

            LogAssert.Expect(LogType.Log, "[Holster] holder=Body settled=Drawn");
            Advance(0.125f);
            Assert.That(_holster.Phase, Is.EqualTo(HolsterPhase.Drawn));
            Assert.That(_weapon.HolsterLowering, Is.Zero);
            Assert.That(_changes.Count, Is.EqualTo(2));
            Assert.That(_changes[1].Phase, Is.EqualTo(HolsterPhase.Drawn));
            Assert.That(_weapon.Fire(), Is.True);
        }

        [Test]
        public void PressingWhileHolsteringReturnsFromTheCurrentLowering()
        {
            Press();
            Advance(0.25f);

            Press();
            Assert.That(_holster.Phase, Is.EqualTo(HolsterPhase.Drawing));
            Assert.That(_holster.RemainingSeconds, Is.EqualTo(RifleDrawSeconds * 0.5f));
            Assert.That(_changes, Is.Empty, "it never settled holstered");
        }

        [Test]
        public void ItCannotFireOrReloadUntilItIsDrawnAgain()
        {
            var full = _weapon.Weapon.AmmoInMagazine;
            Assert.That(_weapon.Fire(), Is.True);

            Press();
            Assert.That(_weapon.Fire(), Is.False, "holstering blocks fire");
            Assert.That(_weapon.StartReload(), Is.False, "holstering blocks reload");

            Advance(RifleHolsterSeconds);
            Assert.That(_weapon.Fire(), Is.False, "holstered blocks fire");
            Assert.That(_weapon.StartReload(), Is.False, "holstered blocks reload");
            Assert.That(_weapon.Weapon.AmmoInMagazine, Is.EqualTo(full - 1));
        }

        [Test]
        public void HolsteringCancelsAReloadInProgress()
        {
            Assert.That(_weapon.Fire(), Is.True);
            Assert.That(_weapon.StartReload(), Is.True);

            Press();
            _weapon.Tick(0f);
            Assert.That(_weapon.Weapon.IsReloading, Is.False);
        }

        [Test]
        public void TheFireButtonOnlyDrawsAndDoesNotFireUntilReleased()
        {
            HolsterFully();
            var ammo = _weapon.Weapon.AmmoInMagazine;

            _holster.Tick(0f, false, true, true);
            Assert.That(_holster.Phase, Is.EqualTo(HolsterPhase.Drawing));
            Assert.That(_weapon.PullTrigger(true, true), Is.False);

            HoldAttack(RifleDrawSeconds);
            Assert.That(_holster.Phase, Is.EqualTo(HolsterPhase.Drawn));
            Assert.That(_holster.IsWaitingForAttackRelease, Is.True);
            Assert.That(_weapon.PullTrigger(true, false), Is.False, "holding the button through the draw does not fire");
            Assert.That(_weapon.Weapon.AmmoInMagazine, Is.EqualTo(ammo));

            _holster.Tick(0f, false, false, false);
            Assert.That(_holster.IsWaitingForAttackRelease, Is.False);
            Assert.That(_weapon.PullTrigger(true, true), Is.True);
        }

        [Test]
        public void TheFireButtonDoesNotFireEvenWhenTheDrawTakesNoTime()
        {
            var instant = Definition("Instant", WithTimes(TestWeapons.RifleSpec(), 0f, 0f));
            _weapon.Initialize(_weapon.Aim, _attack, _reload, null, null, new[] { instant });
            _holster.Initialize(_weapon, _damageable);

            Press();
            Assert.That(_holster.Phase, Is.EqualTo(HolsterPhase.Holstered), "zero seconds settles at once");

            _holster.Tick(0f, false, true, true);
            Assert.That(_holster.Phase, Is.EqualTo(HolsterPhase.Drawn));
            Assert.That(_weapon.PullTrigger(true, true), Is.False);
        }

        [Test]
        public void TheFireButtonDoesNothingWhileDrawn()
        {
            _holster.Tick(0f, false, true, true);
            Assert.That(_holster.Phase, Is.EqualTo(HolsterPhase.Drawn));
            Assert.That(_holster.IsWaitingForAttackRelease, Is.False);
            Assert.That(_weapon.PullTrigger(true, true), Is.True);
        }

        [Test]
        public void AWeaponWithNoHolsterSecondsTakesItsDrawSecondsToHolster()
        {
            Assert.That(_weapon.Select(1), Is.True);
            _weapon.Tick(1f);
            _holster.Tick(0f, false, false, false);

            Assert.That(_holster.Holster(), Is.True);
            Assert.That(_holster.RemainingSeconds, Is.EqualTo(HandgunDrawSeconds));
        }

        [Test]
        public void SwitchingWeaponsWhileHolsteredDrawsTheNewWeapon()
        {
            HolsterFully();

            Assert.That(_weapon.SelectNext(), Is.True);
            _holster.Tick(0f, false, false, false);
            Assert.That(_holster.Phase, Is.EqualTo(HolsterPhase.Drawing));
            Assert.That(_weapon.IsHolstered, Is.False);
            Assert.That(_holster.RemainingSeconds, Is.EqualTo(HandgunDrawSeconds), "it takes the new weapon's draw seconds");

            Advance(HandgunDrawSeconds);
            Assert.That(_holster.Phase, Is.EqualTo(HolsterPhase.Drawn));
            Assert.That(_changes[_changes.Count - 1].Phase, Is.EqualTo(HolsterPhase.Drawn));

            // 持ち替えの時間 (しまう 0.5 秒 + 構える 0.125 秒) は Loadout が進めるので、それが済めば撃てる。
            Advance(1f);
            Assert.That(_weapon.Fire(), Is.True);
        }

        [Test]
        public void SwitchingWeaponsWhileDrawnDoesNotHolster()
        {
            Assert.That(_weapon.SelectNext(), Is.True);
            _holster.Tick(0f, false, false, false);
            Assert.That(_holster.Phase, Is.EqualTo(HolsterPhase.Drawn));
            Assert.That(_changes, Is.Empty);
        }

        [Test]
        public void DyingReturnsToDrawnAndReleasesTheWeapon()
        {
            HolsterFully();
            _changes.Clear();

            LogAssert.Expect(LogType.Log, "[Holster] holder=Body reset=Drawn reason=died");
            _damageable.Kill();

            Assert.That(_holster.Phase, Is.EqualTo(HolsterPhase.Drawn));
            Assert.That(_weapon.IsHolstered, Is.False);
            Assert.That(_weapon.HolsterLowering, Is.Zero);
            Assert.That(GameSignals.Holster.Value, Is.EqualTo(new HolsterStatus(HolsterPhase.Drawn, 0f, true)));
            Assert.That(_changes.Count, Is.EqualTo(1));
            Assert.That(_changes[0].Phase, Is.EqualTo(HolsterPhase.Drawn));

            Assert.That(_holster.Holster(), Is.False, "the dead cannot holster");
            Press();
            Assert.That(_holster.Phase, Is.EqualTo(HolsterPhase.Drawn), "the key is not read while dead");
        }

        [Test]
        public void DyingWhileDrawnChangesNothing()
        {
            _damageable.Kill();
            Assert.That(_holster.Phase, Is.EqualTo(HolsterPhase.Drawn));
            Assert.That(_changes, Is.Empty);
        }

        [Test]
        public void WhileDrawIsBlockedItCannotBeDrawnByAnyMeans()
        {
            HolsterFully();
            var zone = new object();
            Assert.That(_holster.BlockDraw.Claim(zone), Is.True);
            Assert.That(_holster.IsDrawBlocked, Is.True);

            Assert.That(_holster.Draw(), Is.False);
            Press();
            _holster.Tick(0f, false, true, true);
            Assert.That(_weapon.SelectNext(), Is.True);
            Advance(1f);
            Assert.That(_holster.Phase, Is.EqualTo(HolsterPhase.Holstered));
            Assert.That(_weapon.IsHolstered, Is.True);
            Assert.That(_weapon.Fire(), Is.False);

            Assert.That(_holster.BlockDraw.Release(zone), Is.True);
            Assert.That(_holster.Draw(), Is.True);
            Assert.That(_holster.Phase, Is.EqualTo(HolsterPhase.Drawing));
        }

        [Test]
        public void BlockingDrawDoesNotStopHolstering()
        {
            _holster.BlockDraw.Claim(this);
            Assert.That(_holster.Holster(), Is.True);
            Advance(RifleHolsterSeconds);
            Assert.That(_holster.Phase, Is.EqualTo(HolsterPhase.Holstered));
        }

        [Test]
        public void TheHolsteredMoveSpeedMultiplierReachesTheWeapon()
        {
            _holster.Spec = new HolsterSpec(0.5f);
            Assert.That(_weapon.HolsterMoveSpeedMultiplier, Is.EqualTo(1f));

            Press();
            Advance(0.25f);
            Assert.That(_weapon.HolsterMoveSpeedMultiplier, Is.EqualTo(1f), "only while fully holstered");

            Advance(0.25f);
            Assert.That(_weapon.HolsterMoveSpeedMultiplier, Is.EqualTo(0.5f));
            Assert.That(_weapon.CarrySpeedMultiplier(CarryLoadSpec.Default), Is.EqualTo(0.5f * WeaponOnlyCarryMultiplier()).Within(1e-6f));

            Press();
            Assert.That(_weapon.HolsterMoveSpeedMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void AnotherHolderCountsAsArmedAsTheSpecSays()
        {
            var ladder = new object();
            _weapon.Holster.Claim(ladder);
            Assert.That(_holster.IsHeldByOthers, Is.True);
            Advance(0f);
            Assert.That(GameSignals.Holster.Value.Value.IsArmed, Is.False, "by default another holder makes it unarmed");

            _holster.Spec = new HolsterSpec(armedWhileHeldByOthers: true);
            Assert.That(GameSignals.Holster.Value.Value.IsArmed, Is.True);

            _weapon.Holster.Release(ladder);
            _holster.Spec = HolsterSpec.Default;
            Assert.That(GameSignals.Holster.Value.Value.IsArmed, Is.True);
        }

        [Test]
        public void ItsOwnClaimIsNotCountedAsAnotherHolder()
        {
            HolsterFully();
            Assert.That(_holster.IsHeldByOthers, Is.False);
        }

        [Test]
        public void HolsterAndDrawCanBeCalledFromOutside()
        {
            Assert.That(_holster.Draw(), Is.False, "already drawn");
            Assert.That(_holster.Holster(), Is.True);
            Assert.That(_holster.Holster(), Is.False, "already holstering");
            Assert.That(_holster.Draw(), Is.True);
            Assert.That(_holster.Phase, Is.EqualTo(HolsterPhase.Drawing));
        }

        // 近接武器の振りは FireBlocker と IsHolstered しか見ないので、modifier で止めた分も IsMeleeWeaponBlocked に出ることを確かめる。
        [Test]
        public void MeleeSwingsAreBlockedWhileHolsteringHolsteredDrawingAndUntilTheAttackIsReleased()
        {
            Assert.That(_weapon.IsMeleeWeaponBlocked, Is.False);

            Press();
            Assert.That(_weapon.IsMeleeWeaponBlocked, Is.True, "holstering");

            Advance(RifleHolsterSeconds);
            Assert.That(_weapon.IsMeleeWeaponBlocked, Is.True, "holstered");

            _holster.Tick(0f, false, true, true);
            Assert.That(_holster.Phase, Is.EqualTo(HolsterPhase.Drawing));
            Assert.That(_weapon.IsMeleeWeaponBlocked, Is.True, "drawing");

            HoldAttack(RifleDrawSeconds);
            Assert.That(_holster.Phase, Is.EqualTo(HolsterPhase.Drawn));
            _holster.Tick(0f, false, false, true);
            Assert.That(_weapon.IsMeleeWeaponBlocked, Is.True, "the press that drew must not swing");

            _holster.Tick(0f, false, false, false);
            Assert.That(_weapon.IsMeleeWeaponBlocked, Is.False);
        }

        // 盾の殴りは近接の振りとは別の経路 (PullShieldTrigger) なので、構え直しの押下で殴らないことをここでも確かめる。
        [Test]
        public void ShieldBashesAreNotRequestedWhileHolsteredOrByThePressThatDrew()
        {
            var rifle = TestWeapons.RifleSpec();
            var shield = new WeaponSpec(
                rifle.Id, rifle.NameKey, rifle.Category, rifle.Trigger, rifle.Ammo, rifle.Round, rifle.Impact, rifle.Aim,
                rifle.AmmoTypes, rifle.ShotNoiseRadius, rifle.Weight, rifle.MoveSpeedMultiplier, rifle.Description,
                RifleDrawSeconds, RifleHolsterSeconds, shield: RiotShieldSpec.Default);
            Object.DestroyImmediate(_holster);
            _weapon.Initialize(_weapon.transform, _attack, _reload, null, null, new[] { Definition("Shield", shield) });
            _holster = _body.AddComponent<HolsterController>();
            _holster.Spec = HolsterSpec.Default;
            _holster.Initialize(_weapon, _damageable);
            var bashes = 0;
            _weapon.ShieldBashRequested += _ => bashes++;

            HolsterFully();
            _holster.Tick(0f, false, true, true);
            _weapon.PullTrigger(true, true);
            Assert.That(bashes, Is.EqualTo(0), "the press that started drawing");

            HoldAttack(RifleDrawSeconds);
            _weapon.PullTrigger(true, true);
            Assert.That(bashes, Is.EqualTo(0), "the press that finished drawing");

            _holster.Tick(0f, false, false, false);
            _weapon.PullTrigger(true, true);
            Assert.That(bashes, Is.EqualTo(1));
        }

        [Test]
        public void TheInstallerIsRegisteredOnceWithThePlayerActions()
        {
            Assert.That(PlayerActions.Installers.Count(i => i is HolsterActionInstaller), Is.EqualTo(1));
            Assert.That(PlayerActions.Installers.OfType<HolsterActionInstaller>().Single().Order, Is.EqualTo(HolsterActionInstaller.InstallOrder));
        }

        private void HolsterFully()
        {
            Press();
            Advance(RifleHolsterSeconds);
            Assert.That(_holster.Phase, Is.EqualTo(HolsterPhase.Holstered));
        }

        private void Press()
        {
            _holster.Tick(0f, true, false, false);
        }

        private void Advance(float seconds)
        {
            _weapon.Tick(seconds);
            _holster.Tick(seconds, false, false, false);
        }

        private void HoldAttack(float seconds)
        {
            _weapon.Tick(seconds);
            _holster.Tick(seconds, false, false, true);
        }

        private float WeaponOnlyCarryMultiplier()
        {
            var multiplier = _weapon.HolsterMoveSpeedMultiplier;
            _weapon.HolsterMoveSpeedMultiplier = 1f;
            var weaponOnly = _weapon.CarrySpeedMultiplier(CarryLoadSpec.Default);
            _weapon.HolsterMoveSpeedMultiplier = multiplier;
            return weaponOnly;
        }

        private WeaponDefinition Definition(string name, WeaponSpec spec)
        {
            var definition = ScriptableObject.CreateInstance<WeaponDefinition>();
            definition.name = name;
            WeaponAssets.Configure(definition, spec);
            _created.Add(definition);
            return definition;
        }

        private static WeaponSpec WithTimes(WeaponSpec spec, float drawSeconds, float holsterSeconds)
        {
            return new WeaponSpec(
                spec.Id,
                spec.NameKey,
                spec.Category,
                spec.Trigger,
                spec.Ammo,
                spec.Round,
                spec.Impact,
                spec.Aim,
                spec.AmmoTypes,
                spec.ShotNoiseRadius,
                spec.Weight,
                spec.MoveSpeedMultiplier,
                spec.Description,
                drawSeconds,
                holsterSeconds);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class WeaponLightActionInstallerTests
    {
        private readonly List<Object> _created = new List<Object>();
        private readonly List<InputAction> _actions = new List<InputAction>();

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var action in _actions)
            {
                action.Dispose();
            }

            _actions.Clear();
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
            GameSignals.ResetAll();
        }

        [Test]
        public void TheInstallerIsRegisteredOnceWithThePlayerActionsAfterTheHolster()
        {
            Assert.That(PlayerActions.Installers.Count(i => i is WeaponLightActionInstaller), Is.EqualTo(1));
            Assert.That(
                PlayerActions.Installers.OfType<WeaponLightActionInstaller>().Single().Order,
                Is.EqualTo(WeaponLightActionInstaller.InstallOrder));
            Assert.That(WeaponLightActionInstaller.InstallOrder, Is.EqualTo(560));
            Assert.That(WeaponLightActionInstaller.InstallOrder, Is.GreaterThan(HolsterActionInstaller.InstallOrder));

            var installers = PlayerActions.Installers.ToList();
            Assert.That(
                installers.FindIndex(i => i is WeaponLightActionInstaller),
                Is.GreaterThan(installers.FindIndex(i => i is HolsterActionInstaller)));
        }

        [Test]
        public void AMovingBodyWithAWeaponGetsAnInitializedLightThatPicksUpItsHolster()
        {
            var body = Body("Player", moves: true, out var weapon, out var damageable);
            var holster = body.AddComponent<HolsterController>();
            holster.Spec = HolsterSpec.Default;
            holster.Initialize(weapon, damageable);

            new WeaponLightActionInstaller().Install(body, GameRules.Default);

            var light = body.GetComponent<WeaponLightController>();
            Assert.That(light, Is.Not.Null);
            Assert.That(light.IsInitialized, Is.True);
            Assert.That(light.Weapon, Is.SameAs(weapon));
            Assert.That(light.Holster, Is.SameAs(holster));
            Assert.That(light.IsOn, Is.False);
            Assert.That(GameSignals.WeaponLight.Value, Is.EqualTo((WeaponLightStatus?)new WeaponLightStatus(false, false)), "the rifle here has no light fitted");
        }

        [Test]
        public void InstallingTwiceKeepsOneLight()
        {
            var body = Body("Player", moves: true, out _, out _);
            var installer = new WeaponLightActionInstaller();

            installer.Install(body, GameRules.Default);
            installer.Install(body, GameRules.Default);

            Assert.That(body.GetComponents<WeaponLightController>(), Has.Length.EqualTo(1));
        }

        [Test]
        public void ABodyThatDoesNotMoveOrHasNoWeaponGetsNoLight()
        {
            var target = Body("Target", moves: false, out _, out _);
            var unarmed = Track(new GameObject("Unarmed"));
            unarmed.AddComponent<PlayerController>();

            var installer = new WeaponLightActionInstaller();
            installer.Install(target, GameRules.Default);
            installer.Install(unarmed, GameRules.Default);

            Assert.That(target.GetComponent<WeaponLightController>(), Is.Null);
            Assert.That(unarmed.GetComponent<WeaponLightController>(), Is.Null);
            Assert.That(GameSignals.WeaponLight.HasValue, Is.False);
        }

        [Test]
        public void InstallingNeedsABodyAndRules()
        {
            var installer = new WeaponLightActionInstaller();
            var body = Body("Player", moves: true, out _, out _);

            Assert.Throws<ArgumentNullException>(() => installer.Install(null, GameRules.Default));
            Assert.Throws<ArgumentNullException>(() => installer.Install(body, null));
        }

        private GameObject Body(string name, bool moves, out WeaponController weapon, out Damageable damageable)
        {
            var body = Track(new GameObject(name));
            damageable = body.AddComponent<Damageable>();
            damageable.Initialize("Player", Damageable.DefaultMaxHealth);
            if (moves)
            {
                body.AddComponent<PlayerController>();
            }

            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.SetParent(body.transform, false);
            camera.transform.localPosition = new Vector3(0f, 1.6f, 0f);

            weapon = camera.gameObject.AddComponent<WeaponController>();
            weapon.Initialize(
                camera.transform,
                Action("Attack"),
                Action("Reload"),
                null,
                null,
                new[] { Definition("Rifle", TestWeapons.RifleSpec()) });
            return body;
        }

        private InputAction Action(string name)
        {
            var action = new InputAction(name, InputActionType.Button);
            _actions.Add(action);
            return action;
        }

        private WeaponDefinition Definition(string name, WeaponSpec spec)
        {
            var definition = Track(ScriptableObject.CreateInstance<WeaponDefinition>());
            definition.name = name;
            WeaponAssets.Configure(definition, spec);
            return definition;
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}

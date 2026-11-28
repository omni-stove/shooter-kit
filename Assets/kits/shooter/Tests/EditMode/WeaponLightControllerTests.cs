using System;
using System.Collections.Generic;
using GameKit.Core;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Edit Mode では Update が走らないので、ライトのボタンは Tick で渡す。
    public sealed class WeaponLightControllerTests
    {
        private const string RifleItemId = "39cb64db-b31c-478c-9784-9ecf8006a0f1";
        private const string HandgunItemId = "b5b5df13-2868-4b0a-b2c0-fbe6a055a4fa";
        private const string LightItemId = "f3a7c2e9-58b1-4d06-9e4a-2c7b8d1f6a35";
        private const string LightVisualId = "testLight";

        private static readonly string RifleOn = "[Combat] weaponLight id=Body weapon=" + TestWeapons.RifleId + " on=true";
        private static readonly string RifleOff = "[Combat] weaponLight id=Body weapon=" + TestWeapons.RifleId + " on=false";

        private readonly List<Object> _created = new List<Object>();

        private GameObject _body;
        private Damageable _damageable;
        private WeaponController _weapon;
        private HolsterController _holster;
        private WeaponLightController _light;
        private InputAction _attack;
        private InputAction _reload;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            CombatEvents.ClearSubscribers();

            _body = new GameObject("Body");
            _damageable = _body.AddComponent<Damageable>();
            _damageable.Initialize("Player", Damageable.DefaultMaxHealth);
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.SetParent(_body.transform, false);
            camera.transform.localPosition = new Vector3(0f, 1.6f, 0f);

            _attack = new InputAction("Attack", InputActionType.Button);
            _reload = new InputAction("Reload", InputActionType.Button);
            _weapon = camera.gameObject.AddComponent<WeaponController>();
            _weapon.AttachmentCatalog = LightItems();
            _weapon.Initialize(
                camera.transform, _attack, _reload, null, null,
                new[] { Definition("Rifle", TestWeapons.RifleSpec()), Definition("Handgun", TestWeapons.HandgunSpec()) });
            Assert.That(_weapon.SetAttachments(TestWeapons.RifleId, new[] { LightItemId }), Is.True);

            _holster = _body.AddComponent<HolsterController>();
            _holster.Spec = HolsterSpec.Default;
            _holster.Initialize(_weapon, _damageable);

            _light = _body.AddComponent<WeaponLightController>();
            _light.Catalog = Track(ScriptableObject.CreateInstance<WeaponLightCatalog>());
            _light.Initialize(_weapon, _damageable);
        }

        [TearDown]
        public void TearDown()
        {
            _attack.Dispose();
            _reload.Dispose();
            Object.DestroyImmediate(_body);
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
            CombatEvents.ClearSubscribers();
            GameSignals.ResetAll();
        }

        [Test]
        public void InitializingPublishesThatTheRifleHasALightThatIsOff()
        {
            Assert.That(_light.IsInitialized, Is.True);
            Assert.That(_light.Holster, Is.SameAs(_holster), "the holster on the same body is found without passing it");
            Assert.That(_light.IsOn, Is.False);
            Assert.That(_light.CurrentLight.Range, Is.EqualTo(30f));
            Assert.That(GameSignals.WeaponLight.Value, Is.EqualTo((WeaponLightStatus?)new WeaponLightStatus(true, false)));
            Assert.That(_light.Beam, Is.Null, "nothing is built until it is lit");
        }

        [Test]
        public void InitializingWithoutAWeaponThrows()
        {
            Assert.Throws<ArgumentNullException>(() => _light.Initialize(null, _damageable));
        }

        [Test]
        public void TheLightKeyLightsTheRifleAndPressingAgainTurnsItOff()
        {
            LogAssert.Expect(LogType.Log, RifleOn);
            Press();

            Assert.That(_light.IsOn, Is.True);
            Assert.That(_light.LitWeaponId, Is.EqualTo(TestWeapons.RifleId));
            Assert.That(GameSignals.WeaponLight.Value, Is.EqualTo((WeaponLightStatus?)new WeaponLightStatus(true, true)));
            Assert.That(_light.Beam, Is.Not.Null);
            Assert.That(_light.Beam.IsOn, Is.True);
            Assert.That(_light.Beam.Visual.activeSelf, Is.True);
            Assert.That(_light.Beam.BuiltLight.type, Is.EqualTo(LightType.Spot));
            Assert.That(_light.Beam.BuiltLight.range, Is.EqualTo(30f));
            Assert.That(_light.Beam.BuiltLight.spotAngle, Is.EqualTo(40f));
            Assert.That(_light.Beam.BuiltLight.intensity, Is.EqualTo(2f));
            Assert.That(_light.Beam.transform.parent, Is.SameAs(_weapon.Aim), "without a weapon model it shines from in front of the aim");

            LogAssert.Expect(LogType.Log, RifleOff);
            Press();

            Assert.That(_light.IsOn, Is.False);
            Assert.That(_light.LitWeaponId, Is.Null);
            Assert.That(GameSignals.WeaponLight.Value, Is.EqualTo((WeaponLightStatus?)new WeaponLightStatus(true, false)));
            Assert.That(_light.Beam.IsOn, Is.False);
            Assert.That(_light.Beam.Visual.activeSelf, Is.False);
        }

        [Test]
        public void AWeaponWithoutALightIsNotLit()
        {
            SelectAndSettle(TestWeapons.HandgunId);
            Assert.That(GameSignals.WeaponLight.Value, Is.EqualTo((WeaponLightStatus?)new WeaponLightStatus(false, false)));

            LogAssert.Expect(LogType.Log, "[Combat] weaponLight id=Body weapon=" + TestWeapons.HandgunId + " not-lit reason=no-light");
            Press();

            Assert.That(_light.IsOn, Is.False);
            Assert.That(_light.Beam, Is.Null);
            Assert.That(GameSignals.WeaponLight.Value, Is.EqualTo((WeaponLightStatus?)new WeaponLightStatus(false, false)));
        }

        [Test]
        public void SwitchingWeaponsTurnsItOffAndItStaysOffWhenSwitchingBack()
        {
            Press();

            LogAssert.Expect(LogType.Log, RifleOff + " cause=Switched");
            SelectAndSettle(TestWeapons.HandgunId);
            Assert.That(_light.IsOn, Is.False);
            Assert.That(_light.Beam.IsOn, Is.False);
            Assert.That(GameSignals.WeaponLight.Value, Is.EqualTo((WeaponLightStatus?)new WeaponLightStatus(false, false)));

            SelectAndSettle(TestWeapons.RifleId);
            Assert.That(_light.IsOn, Is.False, "coming back to the rifle does not light it again");
            Assert.That(_light.Beam.IsOn, Is.False);
            Assert.That(GameSignals.WeaponLight.Value, Is.EqualTo((WeaponLightStatus?)new WeaponLightStatus(true, false)));
        }

        [Test]
        public void HolsteringTurnsItOffAndDrawingDoesNotLightItAgain()
        {
            Press();

            LogAssert.Expect(LogType.Log, RifleOff + " cause=Holstered");
            _holster.Tick(0f, true, false, false);
            _light.Tick(false);
            Assert.That(_light.IsOn, Is.False, "it goes off as soon as the weapon starts going away");
            Assert.That(_light.Beam.IsOn, Is.False);
            Assert.That(GameSignals.WeaponLight.Value, Is.EqualTo((WeaponLightStatus?)new WeaponLightStatus(true, false)));

            Advance(1f);
            Assert.That(_holster.Phase, Is.EqualTo(HolsterPhase.Holstered));
            LogAssert.Expect(LogType.Log, "[Combat] weaponLight id=Body weapon=" + TestWeapons.RifleId + " not-lit reason=holstered");
            Press();
            Assert.That(_light.IsOn, Is.False, "a holstered weapon cannot be lit");

            _holster.Tick(0f, true, false, false);
            Advance(1f);
            Assert.That(_holster.Phase, Is.EqualTo(HolsterPhase.Drawn));
            Assert.That(_light.IsOn, Is.False, "drawing again leaves it off");
            Assert.That(GameSignals.WeaponLight.Value, Is.EqualTo((WeaponLightStatus?)new WeaponLightStatus(true, false)));
        }

        [Test]
        public void WithoutAHolsterControllerTheWeaponBeingPutAwayTurnsItOff()
        {
            Object.DestroyImmediate(_holster);
            _light.Initialize(_weapon, _damageable);
            Assert.That(_light.Holster, Is.Null);
            Press();

            var ladder = new object();
            LogAssert.Expect(LogType.Log, RifleOff + " cause=Holstered");
            Assert.That(_weapon.Holster.Claim(ladder), Is.True);
            _light.Tick(false);
            Assert.That(_light.IsOn, Is.False);

            _weapon.Holster.Release(ladder);
            _light.Tick(false);
            Assert.That(_light.IsOn, Is.False);
        }

        [Test]
        public void TakingTheLightOffTurnsItOffAtOnce()
        {
            Press();

            LogAssert.Expect(LogType.Log, RifleOff + " cause=NoLight");
            Assert.That(_weapon.SetAttachments(TestWeapons.RifleId, Array.Empty<string>()), Is.True);

            Assert.That(_light.IsOn, Is.False, "the attachment change is followed without waiting for the next frame");
            Assert.That(_light.CurrentLight, Is.Null);
            Assert.That(_light.Beam.IsOn, Is.False);
            Assert.That(GameSignals.WeaponLight.Value, Is.EqualTo((WeaponLightStatus?)new WeaponLightStatus(false, false)));
        }

        [Test]
        public void DyingTurnsItOffAndTheDeadCannotLightIt()
        {
            Press();

            LogAssert.Expect(LogType.Log, RifleOff + " cause=Died");
            _damageable.Kill();

            Assert.That(_light.IsOn, Is.False);
            Assert.That(_light.Beam.IsOn, Is.False);
            Assert.That(GameSignals.WeaponLight.Value, Is.EqualTo((WeaponLightStatus?)new WeaponLightStatus(true, false)));

            LogAssert.Expect(LogType.Log, "[Combat] weaponLight id=Body weapon=" + TestWeapons.RifleId + " not-lit reason=died");
            Assert.That(_light.Toggle(), Is.False);
            Assert.That(_light.IsOn, Is.False);
        }

        [Test]
        public void ItIsReadAsTheBodysLightStatusSource()
        {
            var source = _body.GetComponent<IWeaponLightStatusSource>();
            Assert.That(source, Is.SameAs(_light));
            Assert.That(source.WeaponLightStatus, Is.EqualTo(new WeaponLightStatus(true, false)));

            Press();
            Assert.That(source.WeaponLightStatus, Is.EqualTo(new WeaponLightStatus(true, true)));

            SelectAndSettle(TestWeapons.HandgunId);
            Assert.That(source.WeaponLightStatus, Is.EqualTo(WeaponLightStatus.None));
        }

        [Test]
        public void APrefabInTheCatalogIsWhatShines()
        {
            var prefab = Track(new GameObject("TestWeaponLight"));
            var catalog = Track(ScriptableObject.CreateInstance<WeaponLightCatalog>());
            catalog.SetLights(new[] { new WeaponLightEntry(LightVisualId, prefab) });
            _light.Catalog = catalog;

            Press();

            Assert.That(_light.Beam.UsesPrefab, Is.True);
            Assert.That(_light.Beam.Visual.name, Is.EqualTo("TestWeaponLight"));
            Assert.That(_light.Beam.Visual.activeSelf, Is.True);
        }

        private void Press()
        {
            _light.Tick(true);
        }

        private void Advance(float seconds)
        {
            _weapon.Tick(seconds);
            _holster.Tick(seconds, false, false, false);
            _light.Tick(false);
        }

        private void SelectAndSettle(string weaponId)
        {
            Assert.That(_weapon.Select(weaponId), Is.True);
            _weapon.Tick(1f);
            _holster.Tick(0f, false, false, false);
            _light.Tick(false);
            Assert.That(_weapon.Weapon.Spec.Id, Is.EqualTo(weaponId));
        }

        private static ItemCatalog LightItems()
        {
            return new ItemCatalog(
                new[]
                {
                    ItemSpecs.Create(RifleItemId, TestWeapons.RifleNameKey, width: 4, height: 2, weaponId: TestWeapons.RifleId, attachmentSlots: new[] { "light" }),
                    ItemSpecs.Create(HandgunItemId, TestWeapons.HandgunNameKey, width: 2, height: 1, weaponId: TestWeapons.HandgunId),
                    ItemSpecs.Create(
                        LightItemId,
                        "item.weaponLight",
                        attachment: new AttachmentSpec("light", light: new WeaponLightSpec(30f, 40f, 2f, LightVisualId))),
                },
                InventoryRule.Default);
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

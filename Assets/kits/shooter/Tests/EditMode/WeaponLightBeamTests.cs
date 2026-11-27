using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class WeaponLightBeamTests
    {
        private const string VisualId = "testLight";

        private readonly List<Object> _created = new List<Object>();

        private Transform _aim;
        private WeaponLightSpec _spec;

        [SetUp]
        public void SetUp()
        {
            _aim = Track(new GameObject("Aim")).transform;
            _spec = new WeaponLightSpec(30f, 40f, 2f, VisualId);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void AnEmptyCatalogBuildsASpotLightFromTheSpec()
        {
            var beam = Track(WeaponLightBeam.Create(WeaponSockets.None, _aim, _spec, EmptyCatalog()));

            Assert.That(beam.UsesPrefab, Is.False);
            Assert.That(beam.BuiltLight, Is.Not.Null);
            Assert.That(beam.BuiltLight.name, Is.EqualTo(WeaponLightBeam.BuiltLightName));
            Assert.That(beam.BuiltLight.type, Is.EqualTo(LightType.Spot));
            Assert.That(beam.BuiltLight.range, Is.EqualTo(30f));
            Assert.That(beam.BuiltLight.spotAngle, Is.EqualTo(40f));
            Assert.That(beam.BuiltLight.intensity, Is.EqualTo(2f));
            Assert.That(beam.Visual, Is.SameAs(beam.BuiltLight.gameObject));
            Assert.That(beam.VisualId, Is.EqualTo(VisualId));
            Assert.That(beam.Spec, Is.SameAs(_spec));
        }

        [Test]
        public void NoCatalogOrAnUnknownVisualIdAlsoBuildsASpotLight()
        {
            var noCatalog = Track(WeaponLightBeam.Create(WeaponSockets.None, _aim, _spec, null));
            Assert.That(noCatalog.UsesPrefab, Is.False);
            Assert.That(noCatalog.BuiltLight.type, Is.EqualTo(LightType.Spot));

            var catalog = CatalogWith("otherLight", Prefab("OtherLight"));
            var unknown = Track(WeaponLightBeam.Create(WeaponSockets.None, _aim, _spec, catalog));
            Assert.That(unknown.UsesPrefab, Is.False);
            Assert.That(unknown.BuiltLight.range, Is.EqualTo(30f));

            var emptyPrefab = Track(WeaponLightBeam.Create(WeaponSockets.None, _aim, _spec, CatalogWith(VisualId, null)));
            Assert.That(emptyPrefab.UsesPrefab, Is.False, "an entry without a prefab falls back to the spot light");
        }

        [Test]
        public void APrefabInTheCatalogIsUsedInsteadOfTheSpotLight()
        {
            var prefab = Prefab("TestWeaponLight");
            var beam = Track(WeaponLightBeam.Create(WeaponSockets.None, _aim, _spec, CatalogWith(VisualId, prefab)));

            Assert.That(beam.UsesPrefab, Is.True);
            Assert.That(beam.BuiltLight, Is.Null);
            Assert.That(beam.Visual, Is.Not.SameAs(prefab), "the prefab is copied, not moved");
            Assert.That(beam.Visual.name, Is.EqualTo("TestWeaponLight"));
            Assert.That(beam.Visual.transform.parent, Is.SameAs(beam.transform));
            Assert.That(beam.Visual.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(beam.transform.Find(WeaponLightBeam.BuiltLightName), Is.Null);
        }

        [Test]
        public void TheVisualIdPassedInPicksThePrefabOverTheSpecs()
        {
            var prefab = Prefab("OtherLight");
            var beam = Track(WeaponLightBeam.Create(WeaponSockets.None, _aim, _spec, "otherLight", CatalogWith("otherLight", prefab)));

            Assert.That(beam.UsesPrefab, Is.True);
            Assert.That(beam.VisualId, Is.EqualTo("otherLight"));
        }

        [Test]
        public void ItStartsOffAndOnlyTogglesTheVisual()
        {
            var beam = Track(WeaponLightBeam.Create(WeaponSockets.None, _aim, _spec, EmptyCatalog()));
            Assert.That(beam.IsOn, Is.False);
            Assert.That(beam.Visual.activeSelf, Is.False);

            Assert.That(beam.SetOn(true), Is.True);
            Assert.That(beam.IsOn, Is.True);
            Assert.That(beam.Visual.activeSelf, Is.True);
            Assert.That(beam.SetOn(true), Is.False, "already on");

            Assert.That(beam.SetOn(false), Is.True);
            Assert.That(beam.Visual.activeSelf, Is.False);
        }

        [Test]
        public void WithoutAMuzzleSocketItSitsAtTheDefaultMuzzleOffsetOfTheAim()
        {
            var beam = Track(WeaponLightBeam.Create(WeaponSockets.None, _aim, _spec, EmptyCatalog()));

            Assert.That(beam.name, Is.EqualTo(WeaponLightBeam.ObjectName));
            Assert.That(beam.transform.parent, Is.SameAs(_aim));
            Assert.That(beam.transform.localPosition, Is.EqualTo(new Vector3(0.15f, -0.12f, 0.6f)));
            Assert.That(beam.transform.localRotation, Is.EqualTo(Quaternion.identity));
        }

        [Test]
        public void AMuzzleSocketOnTheWeaponModelIsWhereItShines()
        {
            var model = Track(new GameObject("Model")).transform;
            var muzzle = new GameObject(WeaponSockets.MuzzleName).transform;
            muzzle.SetParent(model, false);
            muzzle.localPosition = new Vector3(0f, 0.05f, 0.7f);

            var beam = Track(WeaponLightBeam.Create(new WeaponSockets(model), _aim, _spec, EmptyCatalog()));

            Assert.That(beam.transform.parent, Is.SameAs(muzzle));
            Assert.That(beam.transform.localPosition, Is.EqualTo(Vector3.zero));

            Assert.That(beam.Place(WeaponSockets.None, _aim), Is.True);
            Assert.That(beam.transform.parent, Is.SameAs(_aim), "a new model without the socket moves it back to the aim");
            Assert.That(beam.Place(WeaponSockets.None, null), Is.False);
            Assert.That(beam.transform.parent, Is.SameAs(_aim), "nowhere to go keeps it where it was");
        }

        [Test]
        public void ConfiguringAnotherSpecRebuildsTheLightAndKeepsItLit()
        {
            var catalog = EmptyCatalog();
            var beam = Track(WeaponLightBeam.Create(WeaponSockets.None, _aim, _spec, catalog));
            beam.SetOn(true);
            var first = beam.BuiltLight;

            beam.Configure(_spec, VisualId, catalog);
            Assert.That(beam.BuiltLight, Is.SameAs(first), "the same spec keeps the light");

            beam.Configure(new WeaponLightSpec(10f, 20f, 1f), null, catalog);
            Assert.That(beam.BuiltLight, Is.Not.SameAs(first));
            Assert.That(beam.BuiltLight.range, Is.EqualTo(10f));
            Assert.That(beam.BuiltLight.spotAngle, Is.EqualTo(20f));
            Assert.That(beam.BuiltLight.intensity, Is.EqualTo(1f));
            Assert.That(beam.Visual.activeSelf, Is.True);
            Assert.That(beam.transform.childCount, Is.EqualTo(1), "the old light is gone");
        }

        [Test]
        public void ANullSpecThrows()
        {
            Assert.Throws<ArgumentNullException>(() => WeaponLightBeam.Create(WeaponSockets.None, _aim, null, EmptyCatalog()));
            Assert.Throws<ArgumentNullException>(() => WeaponLightBeam.Create(WeaponSockets.None, _aim, null, VisualId, EmptyCatalog()));
            Assert.Throws<ArgumentNullException>(() => WeaponLightBeam.BuildSpot(_aim, null));
        }

        private WeaponLightCatalog EmptyCatalog()
        {
            return Track(ScriptableObject.CreateInstance<WeaponLightCatalog>());
        }

        private WeaponLightCatalog CatalogWith(string id, GameObject prefab)
        {
            var catalog = EmptyCatalog();
            catalog.SetLights(new[] { new WeaponLightEntry(id, prefab) });
            return catalog;
        }

        private GameObject Prefab(string name)
        {
            var prefab = Track(new GameObject(name));
            prefab.AddComponent<Light>().type = LightType.Spot;
            return prefab;
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created is Component component ? component.gameObject : (Object)created);
            return created;
        }
    }
}

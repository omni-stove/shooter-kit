using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class BodyWeaponModelsTests
    {
        private const float Tolerance = 1e-4f;
        private const string AkimboPistolId = "5b81c3e9-2f47-4a6d-9e05-7c3a1d8f6b42";
        private const string PistolId = "e2a7d4c6-8b19-4f53-a0c8-3d6e9b2f1a75";

        private readonly List<Object> _created = new List<Object>();
        private readonly List<Renderer> _renderers = new List<Renderer>();
        private Transform _right;
        private Transform _left;
        private int _defaultGuns;
        private BodyWeaponModels _guns;

        [SetUp]
        public void SetUp()
        {
            var root = Track(new GameObject("Root")).transform;
            _right = new GameObject("Gun").transform;
            _right.SetParent(root, false);
            _left = new GameObject("LeftHandGun").transform;
            _left.SetParent(root, false);
            _defaultGuns = 0;
            _renderers.Clear();
            _guns = new BodyWeaponModels(_right, _left, CreateDefaultGun, _renderers);
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
        public void TheDefaultGunIsMadeInTheRightHandAtOnce()
        {
            Assert.That(_defaultGuns, Is.EqualTo(1));
            Assert.That(_right.Find("DefaultGun"), Is.Not.Null);
            Assert.That(_right.Find("DefaultGun").gameObject.activeSelf, Is.True);
            Assert.That(_left.childCount, Is.EqualTo(0));
            Assert.That(_guns.WeaponId, Is.Null);
        }

        [Test]
        public void AWeaponModelReplacesTheDefaultGunAndCountsItsRenderers()
        {
            var pistol = Track(PartPrefab("Pistol"));
            var catalog = Catalog(new WeaponExtrasAssets(PistolId, null, null, null) { PickupModel = pistol });

            _guns.Show(PistolId, Spec(PistolId, null), catalog);

            Assert.That(_guns.WeaponId, Is.EqualTo(PistolId));
            Assert.That(_guns.Model, Is.Not.Null);
            Assert.That(_guns.Model.name, Is.EqualTo("PistolPrefab"));
            Assert.That(_right.Find("DefaultGun").gameObject.activeSelf, Is.False);
            Assert.That(_renderers.Count, Is.EqualTo(2), "the default box and the pistol");
            Assert.That(_guns.LeftHandModel, Is.Null, "a weapon that is not akimbo leaves the left hand empty");
        }

        [Test]
        public void AnAkimboWeaponWithoutModelsMirrorsANewDefaultGunInTheLeftHand()
        {
            _guns.Show(AkimboPistolId, Spec(AkimboPistolId, AkimboSpec.Default), null);

            Assert.That(_defaultGuns, Is.EqualTo(2), "one more default gun for the left hand");
            Assert.That(_guns.LeftHandModel.parent, Is.SameAs(_left));
            Assert.That(_guns.LeftHandModel.name, Is.EqualTo(BodyWeaponModels.LeftHandGunModelName));
            Assert.That(_guns.LeftHandModel.Find("DefaultGun"), Is.Not.Null);
            Assert.That(_guns.LeftHandModel.localScale.x, Is.EqualTo(-1f).Within(Tolerance));
        }

        [Test]
        public void SwitchingToAPlainWeaponRemovesTheLeftGunAndItsRenderers()
        {
            _guns.Show(AkimboPistolId, Spec(AkimboPistolId, AkimboSpec.Default), null);
            Assert.That(_renderers.Count, Is.EqualTo(2));

            _guns.Show(PistolId, Spec(PistolId, null), null);

            Assert.That(_left.childCount, Is.EqualTo(0));
            Assert.That(_guns.LeftHandModel, Is.Null);
            Assert.That(_renderers.Count, Is.EqualTo(1), "only the right default gun is left");
        }

        [Test]
        public void ForgetClearsTheShownIdButKeepsTheModels()
        {
            _guns.Show(AkimboPistolId, Spec(AkimboPistolId, AkimboSpec.Default), null);

            _guns.Forget();

            Assert.That(_guns.WeaponId, Is.Null);
            Assert.That(_guns.LeftHandModel, Is.Not.Null);
        }

        [Test]
        public void ASpecOfAnotherWeaponIsRejected()
        {
            Assert.That(() => _guns.Show(PistolId, Spec(AkimboPistolId, AkimboSpec.Default), null), Throws.ArgumentException);
            Assert.That(_guns.WeaponId, Is.Null);
            Assert.That(_left.childCount, Is.EqualTo(0));
        }

        [Test]
        public void AnEmptyIdHoldsNothing()
        {
            _guns.Show(AkimboPistolId, Spec(AkimboPistolId, AkimboSpec.Default), null);

            _guns.Show("", null, null);

            Assert.That(_guns.WeaponId, Is.Null);
            Assert.That(_guns.Model, Is.Null);
            Assert.That(_guns.LeftHandModel, Is.Null);
            Assert.That(_right.Find("DefaultGun").gameObject.activeSelf, Is.True, "the owner hides the hand when nothing is held");
        }

        [Test]
        public void TheHeldBodyGunSitsAtTheHipAndLowersWithTheHolster()
        {
            var held = PlayerMockPose.HeldBodyGun(0f, 1f);
            var crouched = PlayerMockPose.HeldBodyGun(0f, 0.5f);
            var lowered = PlayerMockPose.HeldBodyGun(1f, 1f);

            Assert.That(Vector3.Distance(held.Position, new Vector3(0.25f, 1.15f, 0.3f)), Is.LessThan(Tolerance));
            Assert.That(Quaternion.Angle(held.Rotation, Quaternion.identity), Is.LessThan(0.01f));
            Assert.That(Vector3.Distance(crouched.Position, new Vector3(0.25f, 0.575f, 0.3f)), Is.LessThan(Tolerance));
            Assert.That(Vector3.Distance(lowered.Position, new Vector3(0.25f, 0.85f, 0.15f)), Is.LessThan(Tolerance));
            Assert.That(Quaternion.Angle(lowered.Rotation, Quaternion.Euler(60f, 0f, 0f)), Is.LessThan(0.01f));
            Assert.That(() => PlayerMockPose.HeldBodyGun(0f, 0f), Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }

        private Transform CreateDefaultGun(Transform hand)
        {
            _defaultGuns++;
            var gun = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(gun.GetComponent<Collider>());
            gun.name = "DefaultGun";
            gun.transform.SetParent(hand, false);
            _renderers.Add(gun.GetComponent<Renderer>());
            return gun.transform;
        }

        private WeaponExtrasCatalog Catalog(params WeaponExtrasAssets[] weapons)
        {
            var catalog = Track(ScriptableObject.CreateInstance<WeaponExtrasCatalog>());
            catalog.SetWeapons(weapons);
            return catalog;
        }

        private T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }

        private static GameObject PartPrefab(string partName)
        {
            var root = new GameObject(partName + "Prefab");
            var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            model.name = partName;
            model.transform.SetParent(root.transform, false);
            return root;
        }

        private static WeaponSpec Spec(string id, AkimboSpec akimbo)
        {
            return new WeaponSpec(
                id,
                "weapon.pistol",
                WeaponCategory.Handgun,
                new TriggerSpec(FireMode.Semi, 0.2f),
                new AmmoSpec(8, 16, 1.5f),
                impact: new ImpactSpec(30),
                aim: new AimSpec(2f),
                akimbo: akimbo);
        }
    }
}

using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class AkimboBodyTests
    {
        private const float Tolerance = 1e-4f;
        private const string AkimboPistolId = "5b81c3e9-2f47-4a6d-9e05-7c3a1d8f6b42";
        private const string PistolId = "e2a7d4c6-8b19-4f53-a0c8-3d6e9b2f1a75";

        private GameObject _player;
        private Transform _camera;
        private PlayerMockBody _mock;
        private readonly List<Object> _created = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _player = new GameObject("Player");
            var eye = new GameObject("Eye").transform;
            eye.SetParent(_player.transform, false);
            _camera = new GameObject("Camera").transform;
            _camera.SetParent(eye, false);
            _camera.gameObject.AddComponent<Camera>();
            _mock = _player.AddComponent<PlayerMockBody>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_mock != null)
            {
                _mock.Dismantle();
            }

            Object.DestroyImmediate(_player);
            foreach (var created in _created)
            {
                Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        [Test]
        public void AnAkimboWeaponIdPutsOneGunUnderTheLeftHand()
        {
            UseCatalog(new WeaponExtrasAssets(AkimboPistolId, null, null, null));
            _mock.Build(_camera);

            _mock.ShowBodyWeapon(AkimboPistolId, Spec(AkimboPistolId, AkimboSpec.Default));

            Assert.That(_mock.LeftHandGun.parent, Is.SameAs(_mock.BodyRoot));
            Assert.That(_mock.LeftHandGun.childCount, Is.EqualTo(1));
            Assert.That(_mock.LeftHandGunModel, Is.Not.Null);
            Assert.That(_mock.LeftHandGunModel.name, Is.EqualTo(PlayerMockBody.LeftHandGunModelName));
            Assert.That(_mock.LeftHandGunModel.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(_mock.BodyWeaponId, Is.EqualTo(AkimboPistolId));
        }

        [Test]
        public void APlainWeaponLeavesTheLeftHandEmpty()
        {
            UseCatalog(new WeaponExtrasAssets(PistolId, null, null, null));
            _mock.Build(_camera);

            _mock.ShowBodyWeapon(PistolId, Spec(PistolId, null));

            Assert.That(_mock.LeftHandGun.childCount, Is.EqualTo(0));
            Assert.That(_mock.LeftHandGunModel, Is.Null);
        }

        [Test]
        public void AWeaponWithoutASpecIsHeldInOneHand()
        {
            UseCatalog(new WeaponExtrasAssets(AkimboPistolId, null, null, null));
            _mock.Build(_camera);

            _mock.ShowBodyWeapon(AkimboPistolId, null);

            Assert.That(_mock.LeftHandGun.childCount, Is.EqualTo(0));
        }

        [Test]
        public void SwitchingToAPlainWeaponRemovesTheLeftHandGun()
        {
            UseCatalog(new WeaponExtrasAssets(AkimboPistolId, null, null, null), new WeaponExtrasAssets(PistolId, null, null, null));
            _mock.Build(_camera);
            _mock.ShowBodyWeapon(AkimboPistolId, Spec(AkimboPistolId, AkimboSpec.Default));
            var leftRenderer = _mock.LeftHandGunModel.GetComponentInChildren<Renderer>();

            _mock.ShowBodyWeapon(PistolId, Spec(PistolId, null));

            Assert.That(_mock.LeftHandGun.childCount, Is.EqualTo(0));
            Assert.That(_mock.LeftHandGunModel, Is.Null);
            Assert.That(leftRenderer == null, Is.True, "the left gun is destroyed, not just hidden");

            _mock.ShowBodyWeapon(null, null);
            Assert.That(_mock.LeftHandGun.childCount, Is.EqualTo(0), "holding nothing leaves the left hand empty");
        }

        [Test]
        public void ShowingTheSameAkimboWeaponAgainKeepsOneLeftGun()
        {
            UseCatalog(new WeaponExtrasAssets(AkimboPistolId, null, null, null));
            _mock.Build(_camera);
            var spec = Spec(AkimboPistolId, AkimboSpec.Default);

            _mock.ShowBodyWeapon(AkimboPistolId, spec);
            _mock.ShowBodyWeapon(AkimboPistolId, spec);

            Assert.That(_mock.LeftHandGun.childCount, Is.EqualTo(1), "showing it again replaces the left gun instead of adding one");
            Assert.That(_mock.LeftHandGun.GetChild(0), Is.SameAs(_mock.LeftHandGunModel));
        }

        [Test]
        public void WithoutALeftPrefabTheRightBodyModelIsMirrored()
        {
            var pistol = Track(PartPrefab("Pistol"));
            UseCatalog(new WeaponExtrasAssets(AkimboPistolId, null, null, null) { PickupModel = pistol });
            _mock.Build(_camera);

            _mock.ShowBodyWeapon(AkimboPistolId, Spec(AkimboPistolId, AkimboSpec.Default));

            Assert.That(_mock.BodyGun.Find("PistolPrefab/Pistol"), Is.Not.Null, "the right hand keeps the weapon's model");
            Assert.That(_mock.LeftHandGunModel.Find("PistolPrefab/Pistol"), Is.Not.Null);
            Assert.That(_mock.LeftHandGunModel.localScale.x, Is.EqualTo(-1f).Within(Tolerance));
            Assert.That(_mock.LeftHandGunModel.localScale.y, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(_mock.LeftHandGunModel.localScale.z, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void WithoutAnyModelTheLeftHandMirrorsThePlaceholderBox()
        {
            UseCatalog(new WeaponExtrasAssets(AkimboPistolId, null, null, null));
            _mock.Build(_camera);

            _mock.ShowBodyWeapon(AkimboPistolId, Spec(AkimboPistolId, AkimboSpec.Default));

            var box = _mock.LeftHandGunModel.Find("GunBox");
            Assert.That(box, Is.Not.Null);
            Assert.That(box.GetComponent<MeshFilter>(), Is.Not.Null);
            Assert.That(box.localScale, Is.EqualTo(PlayerMockPose.GunSize));
            Assert.That(_mock.LeftHandGunModel.localScale.x, Is.EqualTo(-1f).Within(Tolerance));
        }

        [Test]
        public void ALeftPrefabIsUsedAsIsAtItsOffset()
        {
            var right = Track(PartPrefab("RightPistol"));
            var left = Track(PartPrefab("LeftPistol"));
            var offset = new Vector3(0.02f, -0.01f, 0.05f);
            UseCatalog(new WeaponExtrasAssets(AkimboPistolId, null, null, null) { PickupModel = right, LeftHandBodyModel = left, LeftHandBodyOffset = offset });
            _mock.Build(_camera);

            _mock.ShowBodyWeapon(AkimboPistolId, Spec(AkimboPistolId, AkimboSpec.Default));

            Assert.That(_mock.LeftHandGunModel.Find("LeftPistolPrefab/LeftPistol"), Is.Not.Null);
            Assert.That(_mock.LeftHandGunModel.Find("RightPistolPrefab"), Is.Null);
            Assert.That(_mock.LeftHandGunModel.localScale, Is.EqualTo(Vector3.one), "a prefab made for the left hand is not flipped");
            Assert.That(Vector3.Distance(_mock.LeftHandGunModel.localPosition, offset), Is.LessThan(Tolerance));
            Assert.That(_mock.LeftHandGunModel.GetComponentsInChildren<Collider>(true), Is.Empty);
        }

        [Test]
        public void TheLeftGunMirrorsTheRightGunsPose()
        {
            UseCatalog(new WeaponExtrasAssets(AkimboPistolId, null, null, null));
            _mock.Build(_camera);
            _mock.ShowBodyWeapon(AkimboPistolId, Spec(AkimboPistolId, AkimboSpec.Default));

            var pitch = Quaternion.Euler(-20f, 0f, 0f);
            var state = new PlayerActionState(PlayerActionConditions.Grounded, 0f, null, ShoulderSide.Right, ViewMode.ThirdPerson, AkimboPistolId, 0f);
            _mock.Apply(state, Vector3.zero, Vector3.zero, pitch, -1f, 10f);

            var right = _mock.BodyGun.localPosition;
            var leftPosition = _mock.LeftHandGun.localPosition;
            Assert.That(right.x, Is.EqualTo(PlayerMockPose.BodyHipGunPosition.x).Within(Tolerance));
            Assert.That(leftPosition.x, Is.EqualTo(-PlayerMockPose.BodyHipGunPosition.x).Within(Tolerance));
            Assert.That(leftPosition.y, Is.EqualTo(right.y).Within(Tolerance));
            Assert.That(leftPosition.z, Is.EqualTo(right.z).Within(Tolerance));
            Assert.That(Quaternion.Angle(_mock.LeftHandGun.localRotation, pitch), Is.LessThan(0.01f), "a pure pitch mirrors to the same pitch");
        }

        [Test]
        public void LeftHandGunPoseMirrorsPositionAndYaw()
        {
            var right = new MockPartPose(new Vector3(0.25f, 1.15f, 0.3f), Quaternion.Euler(0f, 30f, 0f));

            var left = PlayerMockPose.LeftHandGun(right);

            Assert.That(Vector3.Distance(left.Position, new Vector3(-0.25f, 1.15f, 0.3f)), Is.LessThan(Tolerance));
            Assert.That(Quaternion.Angle(left.Rotation, Quaternion.Euler(0f, -30f, 0f)), Is.LessThan(0.01f));
        }

        [Test]
        public void HidingTheGunsHidesTheLeftGunToo()
        {
            UseCatalog(new WeaponExtrasAssets(AkimboPistolId, null, null, null));
            _mock.Build(_camera);
            _mock.ShowBodyWeapon(AkimboPistolId, Spec(AkimboPistolId, AkimboSpec.Default));

            _mock.ShowGuns(false);
            Assert.That(_mock.LeftHandGun.gameObject.activeSelf, Is.False);

            _mock.ShowGuns(true);
            Assert.That(_mock.LeftHandGun.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void TheLeftGunCastsOnlyAShadowInFirstPerson()
        {
            UseCatalog(new WeaponExtrasAssets(AkimboPistolId, null, null, null));
            _mock.Build(_camera);
            _mock.ShowBodyWeapon(AkimboPistolId, Spec(AkimboPistolId, AkimboSpec.Default));

            var state = new PlayerActionState(PlayerActionConditions.Grounded, 0f, null, ShoulderSide.Right, ViewMode.FirstPerson, AkimboPistolId, 0f);
            _mock.Apply(state, Vector3.zero, Vector3.zero, Quaternion.identity, -1f, 10f);

            Assert.That(
                _mock.LeftHandGunModel.GetComponentInChildren<Renderer>().shadowCastingMode, Is.EqualTo(ShadowCastingMode.ShadowsOnly),
                "the left gun is part of the body and hides like it");
        }

        [Test]
        public void ASpecOfAnotherWeaponIsRejected()
        {
            _mock.Build(_camera);

            Assert.That(
                () => _mock.ShowBodyWeapon(PistolId, Spec(AkimboPistolId, AkimboSpec.Default)),
                Throws.ArgumentException);
            Assert.That(_mock.LeftHandGun.childCount, Is.EqualTo(0));
        }

        [Test]
        public void DismantleClearsTheLeftHand()
        {
            UseCatalog(new WeaponExtrasAssets(AkimboPistolId, null, null, null));
            _mock.Build(_camera);
            _mock.ShowBodyWeapon(AkimboPistolId, Spec(AkimboPistolId, AkimboSpec.Default));

            _mock.Dismantle();

            Assert.That(_mock.LeftHandGun, Is.Null);
            Assert.That(_mock.LeftHandGunModel, Is.Null);
            Assert.That(_player.transform.Find(PlayerMockBody.BodyRootName), Is.Null);
        }

        [Test]
        public void ANonFiniteLeftHandOffsetFailsValidation()
        {
            var catalog = Track(ScriptableObject.CreateInstance<WeaponExtrasCatalog>());
            catalog.SetWeapons(new[]
            {
                new WeaponExtrasAssets(AkimboPistolId, null, null, Track(AudioClip.Create("DryFire", 1, 1, 8000, false)))
                {
                    LeftHandBodyOffset = new Vector3(float.NaN, 0f, 0f),
                },
            });

            Assert.That(
                () => catalog.Validate(),
                Throws.TypeOf<PresentationCatalogException>().With.Message.Contains("left-hand body offset is not finite"));
        }

        private void UseCatalog(params WeaponExtrasAssets[] weapons)
        {
            var catalog = Track(ScriptableObject.CreateInstance<WeaponExtrasCatalog>());
            catalog.SetWeapons(weapons);
            _mock.WeaponModels = catalog;
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

using System.Collections.Generic;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Edit Mode では LateUpdate が走らないので、Refresh で置き直し、StepHands と WeaponController.Tick で時間を進める。
    public sealed class AkimboViewmodelTests
    {
        private const string PistolId = "5b8e2c14-9d3a-4f67-a1c0-7e4d2b9f3a86";
        private const string PlainPistolId = "e7a13d58-2c6b-4f90-8b14-3d9a6e0c5f27";
        private const int Magazine = 8;
        private const int Reserve = 16;
        private const float FireInterval = 0.2f;
        private const float ReloadSeconds = 1.5f;

        private static readonly Vector3 RightOffset = new Vector3(0.12f, -0.1f, 0.25f);

        private readonly List<string> _logs = new List<string>();
        private readonly List<Object> _created = new List<Object>();
        private GameObject _body;
        private WeaponController _controller;
        private ViewmodelPresenter _presenter;
        private ViewmodelCatalog _catalog;
        private GameObject _pistolPrefab;
        private GameObject _plainPrefab;
        private InputAction _attack;
        private InputAction _reload;

        [SetUp]
        public void SetUp()
        {
            ViewmodelPresenter.ResetOnPlay();
            ViewmodelMotion.GlobalMultiplier = 1f;
            _body = new GameObject("Body");
            var eye = new GameObject("Eye").transform;
            eye.SetParent(_body.transform, false);
            eye.localPosition = new Vector3(0f, 0.6f, 0f);
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.SetParent(eye, false);
            var rig = _body.AddComponent<ViewRig>();
            rig.Initialize(eye, camera);
            rig.ApplyBodyVisibility(true);

            _controller = camera.gameObject.AddComponent<WeaponController>();
            _controller.Rules = GameRules.Default.WithReloadMode(ReloadMode.Manual);
            _attack = new InputAction("Attack", InputActionType.Button);
            _reload = new InputAction("Reload", InputActionType.Button);

            _pistolPrefab = Template("PistolModel");
            _plainPrefab = Template("PlainPistolModel");
            _catalog = ScriptableObject.CreateInstance<ViewmodelCatalog>();
            _catalog.name = "TestAkimboViewmodelCatalog";
            _catalog.SetWeapons(new[]
            {
                new ViewmodelEntry(PistolId, _pistolPrefab, RightOffset, new ViewmodelSwayParameters()),
                new ViewmodelEntry(PlainPistolId, _plainPrefab, RightOffset, new ViewmodelSwayParameters()),
            });

            _presenter = _body.AddComponent<ViewmodelPresenter>();
            _presenter.Catalog = _catalog;

            _logs.Clear();
            Application.logMessageReceived += Capture;
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= Capture;
            _attack.Dispose();
            _reload.Dispose();
            Object.DestroyImmediate(_body);
            Object.DestroyImmediate(_pistolPrefab);
            Object.DestroyImmediate(_plainPrefab);
            Object.DestroyImmediate(_catalog);
            foreach (var created in _created)
            {
                Object.DestroyImmediate(created);
            }

            _created.Clear();
            ViewmodelPresenter.ResetOnPlay();
            ViewmodelMotion.GlobalMultiplier = 1f;
        }

        private void Capture(string condition, string stackTrace, LogType type)
        {
            if (condition.StartsWith(ViewmodelPresenter.LogTag, System.StringComparison.Ordinal))
            {
                _logs.Add(condition);
            }
        }

        [Test]
        public void AnAkimboWeaponShowsOneMirroredLeftModelOppositeTheRight()
        {
            HoldAkimboThenPlain();
            _presenter.Refresh();

            var right = _presenter.Instance;
            var left = _presenter.LeftInstance;
            Assert.That(right, Is.Not.Null);
            Assert.That(left, Is.Not.Null);
            Assert.That(left, Is.Not.SameAs(right));
            Assert.That(left.transform.parent, Is.SameAs(_presenter.Motion.Pivot));
            Assert.That(_presenter.Motion.Pivot.childCount, Is.EqualTo(2), "one model per hand");
            Assert.That(right.transform.localPosition, Is.EqualTo(RightOffset));
            Assert.That(left.transform.localPosition, Is.EqualTo(new Vector3(-0.12f, -0.1f, 0.25f)));
            Assert.That(left.transform.localPosition.x, Is.EqualTo(-right.transform.localPosition.x));
            Assert.That(left.name, Is.EqualTo("PistolModel"), "the right model is reused");
            Assert.That(_presenter.IsLeftMirrored, Is.True);
            Assert.That(left.transform.localScale, Is.EqualTo(new Vector3(-0.05f, 0.08f, 0.2f)));
            Assert.That(right.transform.localScale, Is.EqualTo(new Vector3(0.05f, 0.08f, 0.2f)));
            Assert.That(left.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(_presenter.LeftSockets.Muzzle, Is.Not.Null);
            Assert.That(_presenter.LeftSockets.Muzzle.position.x, Is.LessThan(_presenter.Sockets.Muzzle.position.x));
            Assert.That(_logs, Is.EqualTo(new[]
            {
                ViewmodelPresenter.FormatSpawnLog(PistolId, "PistolModel", true),
                ViewmodelPresenter.FormatLeftHandLog(PistolId, "PistolModel", true, true),
            }));
        }

        [Test]
        public void AConfiguredLeftPrefabAndOffsetAreUsedUnmirrored()
        {
            var leftPrefab = Template("LeftPistolModel");
            _created.Add(leftPrefab);
            var leftOffset = new Vector3(-0.2f, -0.15f, 0.3f);
            _catalog.SetWeapons(new[]
            {
                new ViewmodelEntry(PistolId, _pistolPrefab, RightOffset, new ViewmodelSwayParameters(), ViewmodelEntry.DefaultAdsOffset, leftPrefab, leftOffset),
                new ViewmodelEntry(PlainPistolId, _plainPrefab, RightOffset, new ViewmodelSwayParameters()),
            });
            HoldAkimboThenPlain();
            _presenter.Refresh();

            var left = _presenter.LeftInstance;
            Assert.That(left, Is.Not.Null);
            Assert.That(left.name, Is.EqualTo("LeftPistolModel"));
            Assert.That(_presenter.IsLeftMirrored, Is.False);
            Assert.That(left.transform.localPosition, Is.EqualTo(leftOffset));
            Assert.That(left.transform.localScale, Is.EqualTo(new Vector3(0.05f, 0.08f, 0.2f)));
            Assert.That(_logs, Does.Contain(ViewmodelPresenter.FormatLeftHandLog(PistolId, "LeftPistolModel", false, true)));
        }

        [Test]
        public void AnEntryWithoutALeftOffsetMirrorsTheRightOffset()
        {
            var entry = new ViewmodelEntry(PistolId, _pistolPrefab, RightOffset, new ViewmodelSwayParameters());

            Assert.That(entry.LeftPrefab, Is.Null);
            Assert.That(entry.LeftOffset, Is.EqualTo(new Vector3(-0.12f, -0.1f, 0.25f)));
            Assert.That(entry.AkimboReloadLowering, Is.EqualTo(ViewmodelEntry.DefaultAkimboReloadLowering));
            Assert.That(new ViewmodelEntry().LeftOffset, Is.EqualTo(new Vector3(-0.14f, -0.14f, 0.2f)));
            Assert.That(ViewmodelEntry.DefaultLeftOffset, Is.EqualTo(ViewmodelEntry.Mirror(ViewmodelEntry.DefaultOffset)));
        }

        [Test]
        public void OnlyTheHandThatFiredKicks()
        {
            HoldAkimboThenPlain();
            _presenter.Refresh();
            var right = _presenter.Instance.transform;
            var left = _presenter.LeftInstance.transform;
            var kickBack = ViewmodelSwaySettings.Default.KickBackMeters;
            var kickPitch = ViewmodelSwaySettings.Default.KickPitchDegrees;

            Assert.That(_controller.PullTrigger(AkimboHand.Left, true, true), Is.True);
            _presenter.Refresh();

            Assert.That(_presenter.HandKicks(AkimboHand.Left), Is.EqualTo(1f));
            Assert.That(_presenter.HandKicks(AkimboHand.Right), Is.EqualTo(0f));
            Assert.That(left.localPosition.z, Is.EqualTo(0.25f - kickBack).Within(1e-5f));
            Assert.That(Quaternion.Angle(left.localRotation, Quaternion.Euler(-kickPitch, 0f, 0f)), Is.LessThan(0.01f));
            Assert.That(right.localPosition, Is.EqualTo(RightOffset), "the right gun did not fire");
            Assert.That(right.localRotation, Is.EqualTo(Quaternion.identity));

            Assert.That(_controller.PullTrigger(AkimboHand.Right, true, true), Is.True);
            _presenter.Refresh();
            Assert.That(_presenter.HandKicks(AkimboHand.Right), Is.EqualTo(1f));
            Assert.That(right.localPosition.z, Is.EqualTo(0.25f - kickBack).Within(1e-5f));

            _presenter.StepHands(10f);
            _presenter.Refresh();
            Assert.That(_presenter.HandKicks(AkimboHand.Left), Is.EqualTo(0f));
            Assert.That(_presenter.HandKicks(AkimboHand.Right), Is.EqualTo(0f));
            Assert.That(left.localPosition, Is.EqualTo(new Vector3(-0.12f, -0.1f, 0.25f)));
            Assert.That(right.localPosition, Is.EqualTo(RightOffset));
        }

        [Test]
        public void WhileAkimboTheShotDoesNotKickTheSharedPivot()
        {
            HoldAkimboThenPlain();
            _presenter.Refresh();
            Assert.That(_presenter.Motion.KicksOnShot(_body), Is.False, "each hand kicks on its own");

            SwitchToPlain();
            Assert.That(_presenter.Motion.KicksOnShot(_body), Is.True, "a single weapon kicks the pivot as before");
            Assert.That(_presenter.Motion.KicksOnShot(null), Is.False);
        }

        [Test]
        public void TheReloadingHandAloneDrops()
        {
            HoldAkimboThenPlain();
            _presenter.Refresh();
            var right = _presenter.Instance.transform;
            var left = _presenter.LeftInstance.transform;

            Assert.That(_controller.PullTrigger(AkimboHand.Left, true, true), Is.True);
            Assert.That(_controller.StartReload(), Is.True);
            var hands = _controller.Akimbo;
            Assert.That(hands.IsHandReloading(AkimboHand.Left), Is.True);
            Assert.That(hands.IsHandReloading(AkimboHand.Right), Is.False, "the full right hand does not reload");

            _controller.Tick(hands.ReloadSeconds / 2f);
            _presenter.Refresh();

            var depth = ViewmodelEntry.DefaultAkimboReloadLowering * ViewmodelPresenter.LoweredDrop;
            Assert.That(left.localPosition.y, Is.EqualTo(-0.1f - depth).Within(1e-4f));
            Assert.That(right.localPosition.y, Is.EqualTo(-0.1f).Within(1e-6f));

            _controller.Tick(hands.ReloadSeconds);
            _presenter.Refresh();
            Assert.That(hands.IsHandReloading(AkimboHand.Left), Is.False);
            Assert.That(left.localPosition.y, Is.EqualTo(-0.1f).Within(1e-6f), "back up once loaded");
        }

        [Test]
        public void TheReloadLoweringPeaksHalfwayAndIsZeroWhenNotReloading()
        {
            Assert.That(ViewmodelPresenter.AkimboReloadLowering(false, 0.5f, 0.4f), Is.EqualTo(0f));
            Assert.That(ViewmodelPresenter.AkimboReloadLowering(true, 0f, 0.4f), Is.EqualTo(0f).Within(1e-6f));
            Assert.That(ViewmodelPresenter.AkimboReloadLowering(true, 0.5f, 0.4f), Is.EqualTo(0.4f).Within(1e-6f));
            Assert.That(ViewmodelPresenter.AkimboReloadLowering(true, 1f, 0.4f), Is.EqualTo(0f).Within(1e-6f));
            Assert.That(ViewmodelPresenter.AkimboReloadLowering(true, 0.5f, 3f), Is.EqualTo(1f).Within(1e-6f), "the depth is clamped to 0..1");
            Assert.That(ViewmodelPresenter.AkimboReloadLowering(true, float.NaN, 0.4f), Is.EqualTo(0f));
        }

        [Test]
        public void SwitchingToASingleWeaponRemovesTheLeftModel()
        {
            HoldAkimboThenPlain();
            _presenter.Refresh();
            var left = _presenter.LeftInstance;
            Assert.That(left, Is.Not.Null);

            SwitchToPlain();

            Assert.That(left == null, Is.True, "the left model was destroyed");
            Assert.That(_presenter.LeftInstance, Is.Null);
            Assert.That(_presenter.CurrentWeaponId, Is.EqualTo(PlainPistolId));
            Assert.That(_presenter.Motion.Pivot.childCount, Is.EqualTo(1));
            Assert.That(_presenter.LeftSockets.Muzzle, Is.Null);
            Assert.That(_logs, Does.Contain(ViewmodelPresenter.FormatSwapLog(PistolId, PlainPistolId, "PlainPistolModel", true)));

            Assert.That(_controller.SelectPrevious(), Is.True);
            _controller.Tick(1f);
            _presenter.Refresh();
            Assert.That(_presenter.LeftInstance, Is.Not.Null, "switching back brings the left model back");
            Assert.That(_presenter.Motion.Pivot.childCount, Is.EqualTo(2));
        }

        [Test]
        public void HolsteringHidesBothHands()
        {
            HoldAkimboThenPlain();
            _presenter.Refresh();
            var left = _presenter.LeftInstance;

            Assert.That(_controller.Holster.Claim(this), Is.True);
            _presenter.Refresh();

            Assert.That(_presenter.Instance, Is.Null);
            Assert.That(_presenter.LeftInstance, Is.Null);
            Assert.That(left == null, Is.True);
            Assert.That(_presenter.Motion.Pivot.childCount, Is.EqualTo(0));
            Assert.That(_logs, Does.Contain(ViewmodelPresenter.FormatRemoveLog(PistolId, ViewmodelPresenter.ReasonHolstered)));

            Assert.That(_controller.Holster.Release(this), Is.True);
            _presenter.Refresh();
            Assert.That(_presenter.LeftInstance, Is.Not.Null);
        }

        [Test]
        public void AScopeOverlayHidesBothHands()
        {
            var view = SightView.None;
            _presenter.SightSource = () => view;
            HoldAkimboThenPlain();
            _presenter.Refresh();
            var renderer = _presenter.LeftInstance.GetComponent<Renderer>();
            Assert.That(renderer.enabled, Is.True);

            view = SightView.Of(new SightSpec(scopeOverlay: true), ViewMode.FirstPerson, 1f);
            _presenter.Refresh();

            Assert.That(renderer.enabled, Is.False);
            Assert.That(_presenter.Instance.GetComponent<Renderer>().enabled, Is.False);
        }

        [Test]
        public void ShowingAnotherWeaponThroughTheIdSourceHasNoLeftModel()
        {
            HoldAkimboThenPlain();
            _presenter.WeaponIdSource = () => PlainPistolId;
            _presenter.Refresh();

            Assert.That(_presenter.CurrentWeaponId, Is.EqualTo(PlainPistolId));
            Assert.That(_presenter.LeftInstance, Is.Null, "the held hands belong to another weapon");
        }

        [Test]
        public void AReloadLoweringOutsideZeroToOneFailsValidation()
        {
            _catalog.SetWeapons(new[]
            {
                new ViewmodelEntry(PistolId, _pistolPrefab, RightOffset, new ViewmodelSwayParameters(), ViewmodelEntry.DefaultAdsOffset, null, Vector3.zero, 1.5f),
            });

            var error = Assert.Throws<PresentationCatalogException>(() => _catalog.Validate());
            Assert.That(error.Message, Does.Contain("akimbo reload lowering 1.5 is outside 0..1"));

            _catalog.SetWeapons(new[]
            {
                new ViewmodelEntry(PistolId, _pistolPrefab, RightOffset, new ViewmodelSwayParameters(), ViewmodelEntry.DefaultAdsOffset, null, Vector3.zero, 1f),
            });
            Assert.DoesNotThrow(() => _catalog.Validate());
        }

        private void HoldAkimboThenPlain()
        {
            var akimbo = Definition(Pistol(AkimboSpec.Default, PistolId), "AkimboPistol");
            var plain = Definition(Pistol(null, PlainPistolId), "Pistol");
            _controller.Initialize(_controller.transform, _attack, _reload, null, null, new[] { akimbo, plain });
            Assert.That(_controller.IsAkimbo, Is.True);
        }

        private void SwitchToPlain()
        {
            Assert.That(_controller.SelectNext(), Is.True);
            _controller.Tick(1f);
            Assert.That(_controller.IsAkimbo, Is.False);
            _presenter.Refresh();
        }

        private WeaponDefinition Definition(WeaponSpec spec, string name)
        {
            var definition = ScriptableObject.CreateInstance<WeaponDefinition>();
            definition.name = name;
            WeaponAssets.Configure(definition, spec);
            _created.Add(definition);
            return definition;
        }

        private static WeaponSpec Pistol(AkimboSpec akimbo, string id)
        {
            return new WeaponSpec(
                id,
                "weapon.pistol",
                WeaponCategory.Handgun,
                new TriggerSpec(FireMode.Semi, FireInterval),
                new AmmoSpec(Magazine, Reserve, ReloadSeconds),
                impact: new ImpactSpec(30),
                aim: new AimSpec(2f),
                akimbo: akimbo);
        }

        private static GameObject Template(string name)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = name;
            root.transform.localScale = new Vector3(0.05f, 0.08f, 0.2f);
            var muzzle = new GameObject(WeaponSockets.MuzzleName).transform;
            muzzle.SetParent(root.transform, false);
            muzzle.localPosition = new Vector3(0.4f, 0f, 0.5f);
            return root;
        }
    }
}

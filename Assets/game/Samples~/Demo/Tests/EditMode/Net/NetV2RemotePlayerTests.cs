using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.NetV2;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Net.Tests
{
    public sealed class NetV2RemotePlayerTests
    {
        private const string Desert = "skin-desert";
        private const string _mark = "DesertMark";

        private static readonly StanceGeometry _stance = new StanceGeometry(2f, 0f, 0.6f, 1.2f, 0.5f);

        private readonly List<Object> _created = new List<Object>();
        private NetV2RemotePlayer _remote;
        private GameObject _desertSkin;
        private readonly List<string> _asked = new List<string>();

        [SetUp]
        public void SetUp()
        {
            _desertSkin = Track(GameObject.CreatePrimitive(PrimitiveType.Cylinder));
            _desertSkin.name = "DesertSkin";
            _desertSkin.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
            new GameObject(_mark).transform.SetParent(_desertSkin.transform, false);
            _remote = NetV2RemotePlayer.Create(7, _stance, MotorSpec.Default, null);
            Track(_remote.gameObject);
            _asked.Clear();
            _remote.SkinSource = id =>
            {
                _asked.Add(id);
                return id == Desert ? _desertSkin : null;
            };
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

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }

        [Test]
        public void ASkinIdFromTheSnapshotSwapsTheBody()
        {
            var before = _remote.Body;

            _remote.ShowCosmetic(Desert);

            Assert.That(_remote.CosmeticId, Is.EqualTo(Desert));
            Assert.That(before == null, Is.True, "the default body is gone");
            Assert.That(_remote.Body, Is.Not.Null);
            Assert.That(_remote.Body.name, Is.EqualTo("Body"));
            Assert.That(_remote.Body.parent, Is.SameAs(_remote.transform));
            Assert.That(_remote.Body.Find(_mark), Is.Not.Null, "the body is made from the skin");
            Assert.That(_remote.Body.GetComponentsInChildren<Collider>(true), Is.Empty, "the skin's colliders are dropped");
            Assert.That(_remote.Body.localScale, Is.EqualTo(new Vector3(0.8f, 1f, 0.8f)), "the skin keeps its own size");
        }

        [Test]
        public void ConcealingHidesTheBodyButKeepsTheRoot()
        {
            _remote.SetConcealed(true);

            Assert.That(_remote.IsConcealed, Is.True);
            Assert.That(_remote.Body.gameObject.activeSelf, Is.False);
            Assert.That(_remote.gameObject.activeSelf, Is.True, "effects on the root keep running while hidden");

            _remote.SetConcealed(false);

            Assert.That(_remote.IsConcealed, Is.False);
            Assert.That(_remote.Body.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void ABodySwappedWhileConcealedStaysHidden()
        {
            _remote.SetConcealed(true);

            _remote.ShowCosmetic(Desert);

            Assert.That(_remote.Body.gameObject.activeSelf, Is.False);
            _remote.SetConcealed(false);
            Assert.That(_remote.Body.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void TheSameIdIsNotLookedUpAgain()
        {
            _remote.ShowCosmetic(Desert);
            var body = _remote.Body;

            _remote.ShowCosmetic(Desert);

            Assert.That(_remote.Body, Is.SameAs(body));
            Assert.That(_asked, Is.EqualTo(new[] { Desert }));
        }

        [Test]
        public void AnEmptyOrUnknownIdGoesBackToTheDefaultBody()
        {
            _remote.ShowCosmetic(Desert);

            _remote.ShowCosmetic("");

            Assert.That(_remote.CosmeticId, Is.Empty);
            Assert.That(_remote.Body.Find(_mark), Is.Null);
            Assert.That(_asked, Is.EqualTo(new[] { Desert }), "an empty id is not looked up");

            _remote.ShowCosmetic("unknown");

            Assert.That(_remote.CosmeticId, Is.EqualTo("unknown"));
            Assert.That(_remote.Body.Find(_mark), Is.Null, "an id this client does not know keeps the default body");

            _remote.ShowCosmetic(null);
            Assert.That(_remote.CosmeticId, Is.Empty);
        }

        [Test]
        public void TheNewBodyKeepsTheStance()
        {
            _remote.Show(new RemotePose(System.Numerics.Vector3.Zero, 0f, PlayerStance.Crouching, null));

            _remote.ShowCosmetic(Desert);

            // しゃがみの高さ 1.2 / 立ちの高さ 2 だけ、スキンの縦 (1) を縮める
            Assert.That(_remote.Body.localScale.y, Is.EqualTo(0.6f).Within(1e-5f), "the crouch still squashes the new body");
            Assert.That(_remote.Body.localScale.x, Is.EqualTo(0.8f).Within(1e-5f));
        }

        [Test]
        public void AnOpenParachuteHangsTheBodyUprightInsteadOfLeaning()
        {
            // 出てきた最初の Show は寄せずにそのまま置くので、1 回目で傾きの答えが見える
            _remote.Show(new RemotePose(System.Numerics.Vector3.Zero, 0f, PlayerStance.Standing, null, 1f, false, WallRunSide.None, true));

            Assert.That(_remote.IsParachuting, Is.True);
            Assert.That(Quaternion.Angle(_remote.Tilt, Quaternion.identity), Is.EqualTo(0f).Within(1e-3f), "the lean is not shown while parachuting");
        }

        [Test]
        public void TheWeaponLoweringFollowsThePoseAndAHolsteredWeaponIsHidden()
        {
            _remote.Show(new RemotePose(
                System.Numerics.Vector3.Zero, 0f, PlayerStance.Standing, "rifle", 0f, false, WallRunSide.None, false, ParkourMove.None, 0.5f));

            Assert.That(_remote.WeaponLowering, Is.EqualTo(0.5f));
            Assert.That(_remote.IsWeaponHidden, Is.False);

            _remote.Show(new RemotePose(
                System.Numerics.Vector3.Zero, 0f, PlayerStance.Standing, "rifle", 0f, false, WallRunSide.None, false, ParkourMove.None, 1f));

            Assert.That(_remote.WeaponLowering, Is.EqualTo(1f));
            Assert.That(_remote.IsWeaponHidden, Is.True);

            _remote.Show(new RemotePose(System.Numerics.Vector3.Zero, 0f, PlayerStance.Standing, "rifle"));

            Assert.That(_remote.WeaponLowering, Is.Zero, "drawing again raises the weapon");
            Assert.That(_remote.IsWeaponHidden, Is.False);
        }

        [Test]
        public void NoWeaponLightIsMadeUntilTheSnapshotTurnsItOn()
        {
            _remote.Show(LightPose(false, 0f));

            Assert.That(_remote.WeaponLight, Is.Null);
            Assert.That(_remote.IsWeaponLightOn, Is.False);
        }

        [Test]
        public void ALitWeaponLightIsBuiltOnTheBodyFromTheSpec()
        {
            _remote.WeaponLightCatalog = null;
            _remote.WeaponLightSpec = new WeaponLightSpec(40f, 30f, 2f);

            _remote.Show(LightPose(true, 0f));

            var beam = _remote.WeaponLight;
            Assert.That(beam, Is.Not.Null);
            Assert.That(beam.IsOn, Is.True);
            Assert.That(_remote.IsWeaponLightOn, Is.True);
            Assert.That(beam.transform.IsChildOf(_remote.Body), Is.True, "concealing the body hides the light too");
            Assert.That(beam.BuiltLight, Is.Not.Null, "without a catalog the spot is built in code");
            Assert.That(beam.BuiltLight.range, Is.EqualTo(40f));
            Assert.That(beam.BuiltLight.spotAngle, Is.EqualTo(30f));
            Assert.That(beam.Visual.activeSelf, Is.True);
        }

        [Test]
        public void TurningTheLightOffKeepsThePartButHidesTheLight()
        {
            _remote.WeaponLightCatalog = null;
            _remote.Show(LightPose(true, 0f));
            var beam = _remote.WeaponLight;

            _remote.Show(LightPose(false, 0f));

            Assert.That(_remote.WeaponLight, Is.SameAs(beam));
            Assert.That(beam.IsOn, Is.False);
            Assert.That(beam.Visual.activeSelf, Is.False);
            Assert.That(_remote.IsWeaponLightOn, Is.False);

            _remote.Show(LightPose(true, 0f));

            Assert.That(_remote.WeaponLight, Is.SameAs(beam), "turning it on again reuses the same part");
            Assert.That(beam.IsOn, Is.True);
        }

        [Test]
        public void AHolsteredWeaponShowsNoLight()
        {
            _remote.WeaponLightCatalog = null;
            _remote.Show(LightPose(true, 0f));

            _remote.Show(LightPose(true, 1f));

            Assert.That(_remote.IsWeaponLightOn, Is.False);
            Assert.That(_remote.WeaponLight.IsOn, Is.False);
        }

        [Test]
        public void ChangingTheSpecReconfiguresALightAlreadyMade()
        {
            _remote.WeaponLightCatalog = null;
            _remote.Show(LightPose(true, 0f));

            _remote.WeaponLightSpec = new WeaponLightSpec(12f, 20f, 1f);

            Assert.That(_remote.WeaponLight.BuiltLight.range, Is.EqualTo(12f));
            Assert.That(_remote.WeaponLight.IsOn, Is.True);
        }

        [Test]
        public void TheLightMovesToANewBodyWhenTheCosmeticChanges()
        {
            _remote.WeaponLightCatalog = null;
            _remote.Show(LightPose(true, 0f));
            var beam = _remote.WeaponLight;

            _remote.ShowCosmetic(Desert);

            Assert.That(_remote.WeaponLight, Is.SameAs(beam), "the light is not destroyed with the old body");
            Assert.That(beam.transform.IsChildOf(_remote.Body), Is.True);
            Assert.That(beam.IsOn, Is.True);
        }

        [Test]
        public void TheDefaultSpecIsUsedWhenNoneIsGiven()
        {
            Assert.That(_remote.WeaponLightSpec, Is.SameAs(WeaponLightSpec.Default));
        }

        private static RemotePose LightPose(bool on, float lowering)
        {
            return new RemotePose(
                System.Numerics.Vector3.Zero, 0f, PlayerStance.Standing, "rifle", 0f, false, WallRunSide.None, false, ParkourMove.None, lowering, on);
        }

        [Test]
        public void ALeanWithoutAParachuteStillTiltsTheBody()
        {
            _remote.Show(new RemotePose(System.Numerics.Vector3.Zero, 0f, PlayerStance.Standing, null, 1f, false, WallRunSide.None, false));

            Assert.That(_remote.IsParachuting, Is.False);
            Assert.That(Quaternion.Angle(_remote.Tilt, Quaternion.identity), Is.GreaterThan(1f));
        }

        [Test]
        public void AParkourMoveTiltsTheBodyLikeTheOfflineMockBody()
        {
            _remote.Show(new RemotePose(
                System.Numerics.Vector3.Zero, 0f, PlayerStance.Standing, null, 1f, false, WallRunSide.None, false, ParkourMove.WallClimb));

            // 壁登りは前へ 15 度。届いたリーンより先に使う
            Assert.That(Quaternion.Angle(_remote.Tilt, Quaternion.Euler(15f, 0f, 0f)), Is.EqualTo(0f).Within(1e-3f));
            Assert.That(_remote.Drop, Is.EqualTo(0f));
        }

        [Test]
        public void HangingFromALedgeLowersTheBodyAndEndingTheMoveBringsItBack()
        {
            var standingY = _remote.Body.localPosition.y;

            _remote.Show(new RemotePose(
                System.Numerics.Vector3.Zero, 0f, PlayerStance.Standing, null, 0f, false, WallRunSide.None, false, ParkourMove.LedgeHang));

            Assert.That(_remote.Drop, Is.EqualTo(0.35f).Within(1e-5f));
            Assert.That(_remote.Body.localPosition.y, Is.EqualTo(standingY - 0.35f).Within(1e-4f));
            Assert.That(Quaternion.Angle(_remote.Tilt, Quaternion.identity), Is.EqualTo(0f).Within(1e-3f), "hangs straight down");

            // 寄せは前のフレームの長さで進むので、見え直しの置き直しで終わった姿をそのまま見る
            _remote.SetConcealed(true);
            _remote.SetConcealed(false);
            _remote.Show(new RemotePose(System.Numerics.Vector3.Zero, 0f, PlayerStance.Standing, null));

            Assert.That(_remote.Drop, Is.EqualTo(0f));
            Assert.That(_remote.Body.localPosition.y, Is.EqualTo(standingY).Within(1e-4f));
        }

        [Test]
        public void AnOpenParachuteDoesNotHangFromALedge()
        {
            _remote.Show(new RemotePose(
                System.Numerics.Vector3.Zero, 0f, PlayerStance.Standing, null, 0f, true, WallRunSide.None, true, ParkourMove.LedgeHang));

            Assert.That(_remote.Drop, Is.EqualTo(0f));
        }
    }
}

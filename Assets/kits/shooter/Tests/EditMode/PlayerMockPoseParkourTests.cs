using System;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class PlayerMockPoseParkourTests
    {
        private const float Tolerance = 1e-4f;
        private const float FeetY = -1f;

        private GameObject _player;
        private PlayerMockBody _mock;

        [TearDown]
        public void TearDown()
        {
            if (_mock != null)
            {
                _mock.Dismantle();
            }

            if (_player != null)
            {
                Object.DestroyImmediate(_player);
            }
        }

        private static PlayerActionState State(PlayerActionConditions flags)
        {
            return new PlayerActionState(flags, 0f, null, ShoulderSide.Right, ViewMode.ThirdPerson, "Rifle", 0f);
        }

        private static CharacterMotorState Kicking(System.Numerics.Vector3 wallNormal, float lean = 0f)
        {
            return FakeCharacterMotorStateSource.Motor(lean: lean)
                .WithParkour(new CharacterParkourState(ParkourMove.WallKick, 0.1f, wallNormal, System.Numerics.Vector3.Zero));
        }

        private static Vector3 Up(Quaternion tilt)
        {
            return tilt * Vector3.up;
        }

        private static void AssertUp(Quaternion tilt, float x, float y, float z, string message = null)
        {
            var up = Up(tilt);
            Assert.That(up.x, Is.EqualTo(x).Within(Tolerance), message);
            Assert.That(up.y, Is.EqualTo(y).Within(Tolerance), message);
            Assert.That(up.z, Is.EqualTo(z).Within(Tolerance), message);
        }

        private static float Sin(float degrees)
        {
            return Mathf.Sin(degrees * Mathf.Deg2Rad);
        }

        private static float Cos(float degrees)
        {
            return Mathf.Cos(degrees * Mathf.Deg2Rad);
        }

        [Test]
        public void TheDefaultAnglesAreTheDocumentedValues()
        {
            Assert.That(PlayerMockPose.WallKickTiltDegrees, Is.EqualTo(20f));
            Assert.That(PlayerMockPose.LandingRollPitchDegrees, Is.EqualTo(60f));
            Assert.That(PlayerMockPose.WallClimbLeanDegrees, Is.EqualTo(15f));
            Assert.That(PlayerMockPose.LedgeClimbUpLeanDegrees, Is.EqualTo(35f));
            Assert.That(PlayerMockPose.LedgeHangDrop, Is.EqualTo(0.35f));
        }

        // 左の壁 (法線が右 +X) を蹴ると右へ、右の壁を蹴ると左へ、前の壁 (法線が手前 -Z) を蹴ると後ろへ頭が倒れる。
        [TestCase(1f, 0f, 1f, 0f)]
        [TestCase(-1f, 0f, -1f, 0f)]
        [TestCase(0f, -1f, 0f, -1f)]
        [TestCase(0f, 1f, 0f, 1f)]
        public void AWallKickTiltsAwayFromTheWall(float normalX, float normalZ, float awayX, float awayZ)
        {
            Assert.That(PlayerMockPose.TryParkourTilt(ParkourMove.WallKick, new Vector3(normalX, 0f, normalZ), out var tilt), Is.True);

            AssertUp(tilt, awayX * Sin(20f), Cos(20f), awayZ * Sin(20f));
        }

        [Test]
        public void AWallKickIgnoresTheUpwardPartAndTheLengthOfTheNormal()
        {
            Assert.That(PlayerMockPose.TryParkourTilt(ParkourMove.WallKick, new Vector3(3f, 2f, 0f), out var tilt), Is.True);

            AssertUp(tilt, Sin(20f), Cos(20f), 0f);
        }

        [Test]
        public void AWallKickWithoutAKnownWallTiltsBackward()
        {
            Assert.That(PlayerMockPose.TryParkourTilt(ParkourMove.WallKick, out var unknown), Is.True);
            AssertUp(unknown, 0f, Cos(20f), -Sin(20f), "the wall is taken to be in front");

            Assert.That(PlayerMockPose.TryParkourTilt(ParkourMove.WallKick, Vector3.up, out var vertical), Is.True);
            AssertUp(vertical, 0f, Cos(20f), -Sin(20f), "a normal with no horizontal part is unknown too");

            Assert.That(PlayerMockPose.TryParkourTilt(ParkourMove.WallKick, new Vector3(float.NaN, 0f, 1f), out var broken), Is.True);
            AssertUp(broken, 0f, Cos(20f), -Sin(20f), "a broken normal does not tilt to NaN");
        }

        [Test]
        public void ALandingRollPitchesForward()
        {
            Assert.That(PlayerMockPose.TryParkourTilt(ParkourMove.LandingRoll, out var tilt), Is.True);

            AssertUp(tilt, 0f, Cos(60f), Sin(60f));
        }

        [Test]
        public void AWallClimbLeansForward()
        {
            Assert.That(PlayerMockPose.TryParkourTilt(ParkourMove.WallClimb, out var tilt), Is.True);

            AssertUp(tilt, 0f, Cos(15f), Sin(15f));
        }

        [Test]
        public void ClimbingUpALedgeBendsForward()
        {
            Assert.That(PlayerMockPose.TryParkourTilt(ParkourMove.LedgeClimbUp, out var tilt), Is.True);

            AssertUp(tilt, 0f, Cos(35f), Sin(35f));
        }

        [TestCase(ParkourMove.LedgeHang)]
        [TestCase(ParkourMove.LedgeShimmy)]
        public void HangingFromALedgeStaysUpright(ParkourMove move)
        {
            Assert.That(PlayerMockPose.TryParkourTilt(move, Vector3.right, out var tilt), Is.True, "it still wins over the lean");

            Assert.That(tilt, Is.EqualTo(Quaternion.identity));
        }

        [TestCase(ParkourMove.LedgeHang, 0.35f)]
        [TestCase(ParkourMove.LedgeShimmy, 0.35f)]
        [TestCase(ParkourMove.None, 0f)]
        [TestCase(ParkourMove.WallKick, 0f)]
        [TestCase(ParkourMove.LandingRoll, 0f)]
        [TestCase(ParkourMove.WallClimb, 0f)]
        [TestCase(ParkourMove.LedgeClimbUp, 0f)]
        [TestCase((ParkourMove)9, 0f)]
        public void OnlyHangingLowersTheBody(ParkourMove move, float expected)
        {
            Assert.That(PlayerMockPose.ParkourDrop(move), Is.EqualTo(expected));
        }

        [Test]
        public void HangingKeepsTheHeadBelowTheLedge()
        {
            // ぶら下がりの位置では当たり判定のてっぺん (PlayerHeight = 2m) がふちに来る
            var headTop = PlayerMockPose.StandingTop - PlayerMockPose.ParkourDrop(ParkourMove.LedgeHang);

            Assert.That(headTop, Is.EqualTo(1.46f).Within(Tolerance));
            Assert.That(headTop, Is.LessThan(GameBootstrap.PlayerHeight));
        }

        [TestCase(ParkourMove.LedgeHang)]
        [TestCase(ParkourMove.LedgeShimmy)]
        public void HangingRaisesTheGunUpToTheLedge(ParkourMove move)
        {
            Assert.That(PlayerMockPose.TryParkourGun(move, out var gun), Is.True);

            Assert.That(gun.Position, Is.EqualTo(new Vector3(0f, 2.075f, 0.15f)));
            var muzzle = gun.Rotation * Vector3.forward;
            Assert.That(muzzle.y, Is.EqualTo(1f).Within(Tolerance), "the gun points up like raised arms");

            // 体を下げた後の銃の先 (位置 + 長さの半分 - 下げた分) がふち (PlayerHeight) に届く
            var tip = gun.Position.y + (PlayerMockPose.GunSize.z / 2f) - PlayerMockPose.LedgeHangDrop;
            Assert.That(tip, Is.EqualTo(GameBootstrap.PlayerHeight).Within(Tolerance));
        }

        [TestCase(ParkourMove.None)]
        [TestCase(ParkourMove.WallKick)]
        [TestCase(ParkourMove.LandingRoll)]
        [TestCase(ParkourMove.WallClimb)]
        [TestCase(ParkourMove.LedgeClimbUp)]
        [TestCase((ParkourMove)9)]
        public void OtherMovesKeepTheirGun(ParkourMove move)
        {
            Assert.That(PlayerMockPose.TryParkourGun(move, out _), Is.False);
        }

        [Test]
        public void AParkourMoveWinsOverTheLeanSwimAndWallRun()
        {
            var motor = FakeCharacterMotorStateSource.Motor(lean: 1f, parkour: ParkourMove.WallClimb);
            AssertUp(PlayerMockPose.TraversalLean(motor, 15f), 0f, Cos(15f), Sin(15f));

            var swimming = FakeCharacterMotorStateSource.Motor(swimming: true, parkour: ParkourMove.LedgeHang);
            Assert.That(PlayerMockPose.TraversalLean(swimming, 15f), Is.EqualTo(Quaternion.identity));
        }

        [Test]
        public void EndingTheMoveGoesBackToTheLean()
        {
            var leaning = FakeCharacterMotorStateSource.Motor(lean: 1f);

            AssertUp(PlayerMockPose.TraversalLean(leaning, 15f), Sin(15f), Cos(15f), 0f);
        }

        [Test]
        public void TheMotorsWallNormalDecidesTheWallKick()
        {
            var kickingOffTheRight = Kicking(new System.Numerics.Vector3(-1f, 0f, 0f), lean: 1f);

            AssertUp(PlayerMockPose.TraversalLean(kickingOffTheRight, 15f), -Sin(20f), Cos(20f), 0f, "tilts left, away from the wall on the right");
            AssertUp(
                PlayerMockPose.TraversalLean(kickingOffTheRight, 15f, Vector3.right),
                Sin(20f), Cos(20f), 0f, "a normal already turned into the body's frame is used as given");
        }

        [Test]
        public void DownedVaultDodgeAndCoverStillWinOverAParkourMove()
        {
            var motor = FakeCharacterMotorStateSource.Motor(parkour: ParkourMove.LandingRoll);
            foreach (var flags in new[]
                     {
                         PlayerActionConditions.Downed, PlayerActionConditions.Vaulting, PlayerActionConditions.Dodging, PlayerActionConditions.InCover,
                     })
            {
                var state = State(flags);

                Assert.That(
                    PlayerMockPose.BodyLean(state, Vector3.forward, Vector3.back, motor, 15f, Vector3.zero),
                    Is.EqualTo(PlayerMockPose.BodyLean(state, Vector3.forward, Vector3.back)),
                    flags.ToString());
            }
        }

        [TestCase(ParkourMove.WallKick)]
        [TestCase(ParkourMove.LandingRoll)]
        [TestCase(ParkourMove.LedgeHang)]
        public void TheAnimatorRigTakesOnlyTheLeanSoTheParkourParameterDrivesTheClip(ParkourMove move)
        {
            var parameters = new CharacterPoseParameters(
                0f, false, false, CharacterPose.CoverNone, false, false, CharacterPose.NoWeapon, lean: 1f, parkour: CharacterPose.ParkourValue(move));

            AssertUp(PlayerMockPose.TraversalLean(parameters, 15f), Sin(15f), Cos(15f), 0f);
        }

        [Test]
        public void TheRemoteBodyTiltsLikeTheOfflineBodyWithoutAWallNormal()
        {
            AssertUp(
                PlayerMockPose.TraversalLean(1f, false, WallRunSide.None, ParkourMove.WallKick, 15f),
                0f, Cos(20f), -Sin(20f), "the snapshot carries no wall normal");
            AssertUp(PlayerMockPose.TraversalLean(1f, false, WallRunSide.None, ParkourMove.LandingRoll, 15f), 0f, Cos(60f), Sin(60f));
            Assert.That(
                PlayerMockPose.TraversalLean(0.5f, false, WallRunSide.Left, ParkourMove.None, 15f),
                Is.EqualTo(PlayerMockPose.TraversalLean(0.5f, false, WallRunSide.Left, 15f)));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => PlayerMockPose.TraversalLean(1.5f, false, WallRunSide.None, ParkourMove.WallClimb, 15f),
                "a broken lean is still rejected during a move");
        }

        private void BuildMock(Quaternion facing)
        {
            _player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _player.transform.rotation = facing;
            var eye = new GameObject("Eye").transform;
            eye.SetParent(_player.transform, false);
            var camera = new GameObject("Camera").transform;
            camera.SetParent(eye, false);
            camera.gameObject.AddComponent<Camera>();
            _mock = _player.AddComponent<PlayerMockBody>();
            _mock.Build(camera);
        }

        private void Apply(CharacterMotorState motor, PlayerActionConditions flags = PlayerActionConditions.Grounded)
        {
            _mock.Apply(State(flags), Vector3.zero, Vector3.zero, Quaternion.identity, FeetY, 10f, null, motor, 15f);
        }

        [Test]
        public void TheMockBodyTurnsTheWallNormalIntoItsOwnFrame()
        {
            // +X を向いた体から見ると、世界の +Z は左
            BuildMock(Quaternion.Euler(0f, 90f, 0f));

            Apply(Kicking(new System.Numerics.Vector3(0f, 0f, 1f)));

            AssertUp(_mock.BodyRoot.localRotation, -Sin(20f), Cos(20f), 0f);
        }

        [Test]
        public void TheMockBodyHangsLowerWithTheGunUpAndComesBackWhenTheMoveEnds()
        {
            BuildMock(Quaternion.identity);
            var hanging = FakeCharacterMotorStateSource.Motor(lean: 1f, parkour: ParkourMove.LedgeHang);

            Apply(hanging);

            Assert.That(_mock.BodyRoot.localPosition.y, Is.EqualTo(FeetY - 0.35f).Within(Tolerance));
            Assert.That(Quaternion.Angle(_mock.BodyRoot.localRotation, Quaternion.identity), Is.LessThan(0.01f));
            Assert.That((_mock.BodyGun.localPosition - new Vector3(0f, 2.075f, 0.15f)).magnitude, Is.LessThan(Tolerance));

            Apply(FakeCharacterMotorStateSource.Motor());

            Assert.That(_mock.BodyRoot.localPosition.y, Is.EqualTo(FeetY).Within(Tolerance));
            Assert.That((_mock.BodyGun.localPosition - PlayerMockPose.BodyHipGunPosition).magnitude, Is.LessThan(Tolerance));
            Assert.That(Quaternion.Angle(_mock.BodyRoot.localRotation, Quaternion.identity), Is.LessThan(0.01f));
        }

        [Test]
        public void TheMockBodyTiltsForEachMove()
        {
            BuildMock(Quaternion.identity);

            Apply(FakeCharacterMotorStateSource.Motor(parkour: ParkourMove.LandingRoll));
            AssertUp(_mock.BodyRoot.localRotation, 0f, Cos(60f), Sin(60f));
            Assert.That(_mock.BodyRoot.localPosition.y, Is.EqualTo(FeetY).Within(Tolerance), "only hanging lowers the body");

            Apply(FakeCharacterMotorStateSource.Motor(parkour: ParkourMove.WallClimb));
            AssertUp(_mock.BodyRoot.localRotation, 0f, Cos(15f), Sin(15f));

            Apply(FakeCharacterMotorStateSource.Motor(parkour: ParkourMove.LedgeClimbUp));
            AssertUp(_mock.BodyRoot.localRotation, 0f, Cos(35f), Sin(35f));
        }

        [Test]
        public void ADownedBodyDoesNotHang()
        {
            BuildMock(Quaternion.identity);

            Apply(FakeCharacterMotorStateSource.Motor(parkour: ParkourMove.LedgeHang), PlayerActionConditions.Downed);

            Assert.That(_mock.BodyRoot.localPosition.y, Is.EqualTo(FeetY).Within(Tolerance));
        }
    }
}

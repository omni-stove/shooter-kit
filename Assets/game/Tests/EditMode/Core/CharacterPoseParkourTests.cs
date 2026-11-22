using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class CharacterPoseParkourTests
    {
        // 定数配列を直接渡すと CA1861 になるので、欄に置く
        private static readonly int[] _moveNumbers = { 0, 1, 2, 3, 4, 5, 6 };

        private readonly CharacterPose _pose = new CharacterPose(5f, 8f, 2.5f);

        [Test]
        public void EveryMoveHasAValueAndTheValueIsTheMoveNumber()
        {
            var values = new int[]
            {
                CharacterPose.ParkourNone,
                CharacterPose.ParkourWallKick,
                CharacterPose.ParkourLandingRoll,
                CharacterPose.ParkourWallClimb,
                CharacterPose.ParkourLedgeHang,
                CharacterPose.ParkourLedgeShimmy,
                CharacterPose.ParkourLedgeClimbUp,
            };

            Assert.That(values, Is.EqualTo(_moveNumbers));
            for (var move = ParkourMove.None; move <= ParkourMove.LedgeClimbUp; move++)
            {
                Assert.That(CharacterPose.ParkourValue(move), Is.EqualTo(values[(int)move]), move.ToString());
                Assert.That(CharacterPose.IsParkourValue((int)move), Is.True, move.ToString());
            }
        }

        [Test]
        public void WithoutAMoveTheParameterStaysDown()
        {
            var parameters = _pose.Evaluate(Input());

            Assert.That(parameters.Parkour, Is.EqualTo(CharacterPose.ParkourNone));
            Assert.That(new CharacterPoseParameters(0f, false, false, CharacterPose.CoverNone, false, false, CharacterPose.NoWeapon).Parkour,
                Is.EqualTo(CharacterPose.ParkourNone));
        }

        [TestCase(ParkourMove.WallKick, CharacterPose.ParkourWallKick)]
        [TestCase(ParkourMove.LandingRoll, CharacterPose.ParkourLandingRoll)]
        [TestCase(ParkourMove.WallClimb, CharacterPose.ParkourWallClimb)]
        [TestCase(ParkourMove.LedgeHang, CharacterPose.ParkourLedgeHang)]
        [TestCase(ParkourMove.LedgeShimmy, CharacterPose.ParkourLedgeShimmy)]
        [TestCase(ParkourMove.LedgeClimbUp, CharacterPose.ParkourLedgeClimbUp)]
        public void EachMoveSetsItsNumberAndDropsCrouchSlideAndLean(ParkourMove move, int expected)
        {
            var parameters = _pose.Evaluate(Input(parkour: move, crouching: true, sliding: true, lean: 1f, aiming: true));

            Assert.That(parameters.Parkour, Is.EqualTo(expected));
            Assert.That(parameters.Crouch, Is.False);
            Assert.That(parameters.Slide, Is.False);
            Assert.That(parameters.Lean, Is.EqualTo(0f));
            Assert.That(parameters.Aim, Is.True, "aiming is kept like on a wall run");
            Assert.That(CharacterPose.ParkourValue(move), Is.EqualTo(expected));
        }

        [Test]
        public void DeathDownedCoverWallRunAndSwimmingWinOverAMove()
        {
            Assert.That(_pose.Evaluate(Input(parkour: ParkourMove.LedgeHang, dead: true)).Parkour, Is.EqualTo(CharacterPose.ParkourNone));
            Assert.That(_pose.Evaluate(Input(parkour: ParkourMove.LedgeHang, downed: true)).Parkour, Is.EqualTo(CharacterPose.ParkourNone));
            Assert.That(_pose.Evaluate(Input(parkour: ParkourMove.WallKick, cover: CoverHeight.High)).Parkour, Is.EqualTo(CharacterPose.ParkourNone));

            var wallRunning = _pose.Evaluate(Input(grounded: false, parkour: ParkourMove.WallKick, wallRun: WallRunSide.Left));
            Assert.That(wallRunning.Parkour, Is.EqualTo(CharacterPose.ParkourNone));
            Assert.That(wallRunning.WallRun, Is.EqualTo(CharacterPose.WallRunLeft));

            var swimming = _pose.Evaluate(Input(grounded: false, parkour: ParkourMove.LedgeHang, swimming: true));
            Assert.That(swimming.Parkour, Is.EqualTo(CharacterPose.ParkourNone));
            Assert.That(swimming.Swimming, Is.True);
        }

        [Test]
        public void WithMotorTakesTheMoveFromTheMotor()
        {
            var motor = new CharacterMotorState(Vector3.Zero, Vector3.Zero, false, false)
                .WithParkour(new CharacterParkourState(ParkourMove.LedgeShimmy, 0.4f, -Vector3.UnitZ, new Vector3(0f, 2f, 1f)));

            var withMotor = Input(speed: 1f).WithMotor(motor);

            Assert.That(withMotor.Parkour, Is.EqualTo(ParkourMove.LedgeShimmy));
            Assert.That(withMotor.HorizontalSpeed, Is.EqualTo(1f));
            Assert.That(_pose.Evaluate(withMotor).Parkour, Is.EqualTo(CharacterPose.ParkourLedgeShimmy));
            Assert.That(Input().WithMotor(default).Parkour, Is.EqualTo(ParkourMove.None));
        }

        [Test]
        public void RejectsAnUndefinedMove()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Input(parkour: (ParkourMove)7));
            Assert.Throws<ArgumentOutOfRangeException>(() => CharacterPose.ParkourValue((ParkourMove)7));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new CharacterPoseParameters(0f, false, false, CharacterPose.CoverNone, false, false, CharacterPose.NoWeapon, parkour: 7));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new CharacterPoseParameters(0f, false, false, CharacterPose.CoverNone, false, false, CharacterPose.NoWeapon, parkour: -1));
            Assert.That(CharacterPose.IsParkourValue(CharacterPose.ParkourLedgeClimbUp), Is.True);
            Assert.That(CharacterPose.IsParkourValue(7), Is.False);
        }

        [Test]
        public void TheMoveTakesPartInEqualityAndText()
        {
            Assert.That(Input(parkour: ParkourMove.WallKick), Is.Not.EqualTo(Input()));
            Assert.That(Input(parkour: ParkourMove.WallKick), Is.EqualTo(Input(parkour: ParkourMove.WallKick)));
            Assert.That(Input(parkour: ParkourMove.WallKick).GetHashCode(), Is.EqualTo(Input(parkour: ParkourMove.WallKick).GetHashCode()));
            Assert.That(Input(parkour: ParkourMove.LedgeHang).ToString(), Does.EndWith("parkour=LedgeHang"));

            var hanging = new CharacterPoseParameters(
                0f, false, false, CharacterPose.CoverNone, false, false, CharacterPose.NoWeapon, parkour: CharacterPose.ParkourLedgeHang);
            var still = new CharacterPoseParameters(0f, false, false, CharacterPose.CoverNone, false, false, CharacterPose.NoWeapon);
            Assert.That(hanging, Is.Not.EqualTo(still));
            Assert.That(hanging, Is.EqualTo(new CharacterPoseParameters(
                0f, false, false, CharacterPose.CoverNone, false, false, CharacterPose.NoWeapon, parkour: CharacterPose.ParkourLedgeHang)));
            Assert.That(hanging.ToString(), Does.EndWith("Slide=False Parkour=4"));
        }

        private static CharacterPoseInput Input(
            float speed = 0f,
            bool grounded = true,
            bool crouching = false,
            bool aiming = false,
            CoverHeight? cover = null,
            bool downed = false,
            bool dead = false,
            bool sliding = false,
            float lean = 0f,
            bool swimming = false,
            WallRunSide wallRun = WallRunSide.None,
            ParkourMove parkour = ParkourMove.None)
        {
            return new CharacterPoseInput(
                speed, grounded, crouching, false, aiming, cover, downed, dead, WeaponCategory.AssaultRifle, sliding, lean, swimming, wallRun, parkour);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class WallClimbStepTests
    {
        // 2 進で割り切れる tick と速さにして、秒数・高さ・速度の期待値をちょうどの値で書く
        private const float _tick = 0.125f;

        private static readonly Vector3 _wallNormal = new Vector3(0f, 0f, -1f);
        private static readonly FrontWallContact _tallWall = FrontWallContact.WithoutTop(_wallNormal, 0.6f);
        private static readonly Vector3 _running = new Vector3(0f, 0f, 6f);
        private static readonly WallClimbTuning _tuning = new WallClimbTuning(true, 4f, 4f, 0.5f, 10f, 5f);
        private static readonly CharacterParkourState _climbing = new CharacterParkourState(ParkourMove.WallClimb, _tick, _wallNormal, Vector3.Zero);

        [Test]
        public void StartsWhenRunningIntoTheWallFastEnoughAndJumping()
        {
            var result = Step(CharacterParkourState.None, _tallWall, _running, jump: true, grounded: true);

            Assert.That(result.Motion, Is.EqualTo(WallClimbMotion.Climbing));
            Assert.That(result.State, Is.EqualTo(new CharacterParkourState(ParkourMove.WallClimb, 0.125f, _wallNormal, Vector3.Zero)));
            Assert.That(result.Velocity, Is.EqualTo(new Vector3(0f, 4f, 0f)), "the run into the wall turns into climbing straight up");
            Assert.That(result.Displacement, Is.EqualTo(new Vector3(0f, 0.5f, 0f)));
            Assert.That(result.Ledge, Is.EqualTo(Vector3.Zero));
            Assert.That(result.HasEnded, Is.False);
        }

        [Test]
        public void StartsAtExactlyTheMinimumSpeed()
        {
            Assert.That(
                Step(CharacterParkourState.None, _tallWall, new Vector3(0f, 0f, 4f), jump: true, grounded: true).Motion,
                Is.EqualTo(WallClimbMotion.Climbing));
        }

        [Test]
        public void DoesNotStartWhenTooSlow()
        {
            var result = Step(CharacterParkourState.None, _tallWall, new Vector3(0f, 0f, 3.9f), jump: true, grounded: true);

            Assert.That(result.Motion, Is.EqualTo(WallClimbMotion.None));
            Assert.That(result.State, Is.EqualTo(CharacterParkourState.None));
            Assert.That(result.Velocity, Is.EqualTo(Vector3.Zero));
            Assert.That(result.Displacement, Is.EqualTo(Vector3.Zero));
            Assert.That(
                Step(CharacterParkourState.None, _tallWall, new Vector3(6f, 0f, 0f), jump: true, grounded: true).Motion,
                Is.EqualTo(WallClimbMotion.None),
                "fast only along the wall");
            Assert.That(
                Step(CharacterParkourState.None, _tallWall, new Vector3(0f, 0f, -6f), jump: true, grounded: true).Motion,
                Is.EqualTo(WallClimbMotion.None),
                "fast away from the wall");
        }

        [Test]
        public void DoesNotStartWithoutEachCondition()
        {
            var lowWall = new FrontWallContact(_wallNormal, 0.6f, 2f, new Vector3(0f, 2f, 0.6f));

            Assert.That(Step(CharacterParkourState.None, _tallWall, _running, jump: false, grounded: true).Motion, Is.EqualTo(WallClimbMotion.None), "no jump");
            Assert.That(Step(CharacterParkourState.None, _tallWall, _running, jump: true, grounded: false).Motion, Is.EqualTo(WallClimbMotion.None), "in the air");
            Assert.That(
                Step(CharacterParkourState.None, FrontWallContact.None, _running, jump: true, grounded: true).Motion, Is.EqualTo(WallClimbMotion.None), "no wall");
            Assert.That(
                Step(CharacterParkourState.None, lowWall, _running, jump: true, grounded: true).Motion,
                Is.EqualTo(WallClimbMotion.None),
                "a wall low enough to mantle is left to the mantle");
        }

        [Test]
        public void DoesNothingWhenSwitchedOffOrCrouchedOrProne()
        {
            var off = WallClimbStep.Step(
                CharacterParkourState.None, _tallWall, _running, true, true, PlayerStance.Standing, MotorSpec.Default, WallClimbTuning.Default, _tick);
            var crouched = Step(CharacterParkourState.None, _tallWall, _running, jump: true, grounded: true, stance: PlayerStance.Crouching);
            var prone = Step(CharacterParkourState.None, _tallWall, _running, jump: true, grounded: true, stance: PlayerStance.Prone);

            Assert.That(WallClimbTuning.Default.Enabled, Is.False);
            foreach (var result in new[] { off, crouched, prone })
            {
                Assert.That(result.Motion, Is.EqualTo(WallClimbMotion.None));
                Assert.That(result.State, Is.EqualTo(CharacterParkourState.None));
                Assert.That(result.Velocity, Is.EqualTo(Vector3.Zero));
                Assert.That(result.Displacement, Is.EqualTo(Vector3.Zero));
            }
        }

        [Test]
        public void DoesNotTouchAnotherMove()
        {
            var hanging = new CharacterParkourState(ParkourMove.LedgeHang, 0.25f, _wallNormal, new Vector3(0f, 3f, 0.6f));

            var result = Step(hanging, _tallWall, _running, jump: true, grounded: true);

            Assert.That(result.Motion, Is.EqualTo(WallClimbMotion.None));
            Assert.That(result.State, Is.EqualTo(hanging));
        }

        [Test]
        public void KeepsClimbingUpTheWall()
        {
            var result = Step(_climbing, _tallWall, new Vector3(0f, 4f, 0f));

            Assert.That(result.Motion, Is.EqualTo(WallClimbMotion.Climbing));
            Assert.That(result.State, Is.EqualTo(new CharacterParkourState(ParkourMove.WallClimb, 0.25f, _wallNormal, Vector3.Zero)));
            Assert.That(result.Velocity, Is.EqualTo(new Vector3(0f, 4f, 0f)));
            Assert.That(result.Displacement, Is.EqualTo(new Vector3(0f, 0.5f, 0f)));
        }

        [Test]
        public void FallsWhenTheTimeRunsOut()
        {
            var state = CharacterParkourState.None;
            var velocity = _running;
            var climbingTicks = 0;
            var height = 0f;
            var result = Step(state, _tallWall, velocity, jump: true, grounded: true);
            while (result.Motion == WallClimbMotion.Climbing)
            {
                climbingTicks++;
                height += result.Displacement.Y;
                state = result.State;
                velocity = result.Velocity;
                Assert.That(climbingTicks, Is.LessThan(100), "the climb should end");
                result = Step(state, _tallWall, velocity);
            }

            Assert.That(climbingTicks, Is.EqualTo(4), "0.5 seconds of 0.125 second ticks");
            Assert.That(state.Elapsed, Is.EqualTo(0.5f));
            Assert.That(height, Is.EqualTo(2f));
            Assert.That(result.Motion, Is.EqualTo(WallClimbMotion.Fell));
            Assert.That(result.State, Is.EqualTo(CharacterParkourState.None));
            Assert.That(result.Velocity, Is.EqualTo(new Vector3(0f, -2.5f, 0f)), "the climb speed is dropped and the fall starts from zero");
            Assert.That(result.Displacement, Is.EqualTo(new Vector3(0f, -0.15625f, 0f)));
            Assert.That(result.HasEnded, Is.True);
        }

        private static readonly float[] _expectedRises = { 0.5f, 0.5f, 0.25f };

        [Test]
        public void StopsAtTheHighestHeight()
        {
            var lowCap = new WallClimbTuning(true, 4f, 4f, 0.5f, 1.25f, 5f);
            var state = CharacterParkourState.None;
            var height = 0f;
            var result = WallClimbStep.Step(state, _tallWall, _running, true, true, PlayerStance.Standing, MotorSpec.Default, lowCap, _tick);
            var rises = new List<float>();
            while (result.Motion == WallClimbMotion.Climbing)
            {
                rises.Add(result.Displacement.Y);
                height += result.Displacement.Y;
                state = result.State;
                Assert.That(rises.Count, Is.LessThan(100), "the climb should end");
                result = WallClimbStep.Step(state, _tallWall, result.Velocity, false, false, PlayerStance.Standing, MotorSpec.Default, lowCap, _tick);
            }

            Assert.That(rises, Is.EqualTo(_expectedRises), "the last tick rises only what is left");
            Assert.That(height, Is.EqualTo(1.25f));
            Assert.That(state.Elapsed, Is.EqualTo(0.375f), "it ends before the 0.5 second limit");
            Assert.That(WallClimbStep.ClimbedHeight(state, lowCap), Is.EqualTo(1.25f));
            Assert.That(result.Motion, Is.EqualTo(WallClimbMotion.Fell));
            Assert.That(result.State, Is.EqualTo(CharacterParkourState.None));
        }

        [Test]
        public void ReportsReachingTheLedge()
        {
            var ledge = new Vector3(0f, 3.5f, 0.6f);
            var nearTop = new FrontWallContact(_wallNormal, 0.6f, MotorSpec.DefaultMantleMaxHeight, ledge);

            var result = Step(_climbing, nearTop, new Vector3(0f, 4f, 0f));

            Assert.That(result.Motion, Is.EqualTo(WallClimbMotion.ReachedLedge));
            Assert.That(result.Ledge, Is.EqualTo(ledge));
            Assert.That(result.State, Is.EqualTo(CharacterParkourState.None));
            Assert.That(result.Velocity, Is.EqualTo(Vector3.Zero), "it holds at the ledge for the mantle or hang to take over");
            Assert.That(result.Displacement, Is.EqualTo(Vector3.Zero));
            Assert.That(result.HasEnded, Is.True);
            Assert.That(
                Step(_climbing, new FrontWallContact(_wallNormal, 0.6f, 2.25f, ledge), new Vector3(0f, 4f, 0f)).Motion,
                Is.EqualTo(WallClimbMotion.Climbing),
                "a top above the mantle height is still out of reach");
        }

        [Test]
        public void PushesOffBackwardsOnJump()
        {
            var result = Step(_climbing, _tallWall, new Vector3(0f, 4f, 0f), jump: true);
            var up = MathF.Sqrt(2f * MotorSpec.DefaultGravity * MotorSpec.DefaultJumpHeight);

            Assert.That(result.Motion, Is.EqualTo(WallClimbMotion.PushedOff));
            Assert.That(result.State, Is.EqualTo(CharacterParkourState.None));
            Assert.That(result.Velocity.X, Is.EqualTo(0f));
            Assert.That(result.Velocity.Z, Is.EqualTo(-5f), "away from the wall at the push-off speed");
            Assert.That(result.Velocity.Y, Is.EqualTo(up - 2.5f).Within(1e-5f), "up as high as a ground jump, under full gravity");
            Assert.That(result.Displacement.X, Is.EqualTo(0f));
            Assert.That(result.Displacement.Z, Is.EqualTo(-0.625f));
            Assert.That(result.Displacement.Y, Is.EqualTo((up - 1.25f) * 0.125f).Within(1e-5f));
            Assert.That(result.HasEnded, Is.True);
        }

        [Test]
        public void PushesOffEvenWhenTheWallRanOutThisTick()
        {
            var result = Step(_climbing, FrontWallContact.None, new Vector3(0f, 4f, 0f), jump: true);

            Assert.That(result.Motion, Is.EqualTo(WallClimbMotion.PushedOff));
            Assert.That(result.Velocity.Z, Is.EqualTo(-5f));
        }

        [Test]
        public void FallsWhenTheWallRunsOutOrTurnsAway()
        {
            var other = FrontWallContact.WithoutTop(new Vector3(1f, 0f, 0f), 0.6f);

            var gone = Step(_climbing, FrontWallContact.None, new Vector3(0f, 4f, 0f));
            var turned = Step(_climbing, other, new Vector3(0f, 4f, 0f));

            Assert.That(gone.Motion, Is.EqualTo(WallClimbMotion.Fell));
            Assert.That(gone.State, Is.EqualTo(CharacterParkourState.None));
            Assert.That(gone.Velocity, Is.EqualTo(new Vector3(0f, -2.5f, 0f)));
            Assert.That(turned.Motion, Is.EqualTo(WallClimbMotion.Fell), "another wall is not the one being climbed");
        }

        [Test]
        public void FallsWhenSwitchedOffOrCrouchingMidClimb()
        {
            var off = WallClimbStep.Step(
                _climbing, _tallWall, new Vector3(0f, 4f, 0f), false, false, PlayerStance.Standing, MotorSpec.Default, WallClimbTuning.Default, _tick);
            var crouched = Step(_climbing, _tallWall, new Vector3(0f, 4f, 0f), stance: PlayerStance.Crouching);
            var prone = Step(_climbing, _tallWall, new Vector3(0f, 4f, 0f), stance: PlayerStance.Prone);

            foreach (var result in new[] { off, crouched, prone })
            {
                Assert.That(result.Motion, Is.EqualTo(WallClimbMotion.Fell));
                Assert.That(result.State, Is.EqualTo(CharacterParkourState.None));
                Assert.That(result.Velocity, Is.EqualTo(new Vector3(0f, -2.5f, 0f)));
            }
        }

        [Test]
        public void LandingEndsTheClimbWithoutMoving()
        {
            var result = Step(_climbing, _tallWall, new Vector3(0f, 4f, 0f), grounded: true);

            Assert.That(result.Motion, Is.EqualTo(WallClimbMotion.None));
            Assert.That(result.State, Is.EqualTo(CharacterParkourState.None));
            Assert.That(result.Velocity, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void ClimbedHeightComesFromTheSeconds()
        {
            Assert.That(WallClimbStep.ClimbedHeight(_climbing, _tuning), Is.EqualTo(0.5f));
            Assert.That(WallClimbStep.ClimbedHeight(new CharacterParkourState(ParkourMove.WallClimb, 5f, _wallNormal, Vector3.Zero), _tuning), Is.EqualTo(10f));
            Assert.That(WallClimbStep.ClimbedHeight(CharacterParkourState.None, _tuning), Is.Zero);
            Assert.That(WallClimbStep.IntoWallSpeed(new Vector3(3f, 9f, 6f), _wallNormal), Is.EqualTo(6f));
        }

        [Test]
        public void RejectsMissingSettingsAndABadTick()
        {
            Assert.Throws<ArgumentNullException>(() => WallClimbStep.Step(
                _climbing, _tallWall, Vector3.Zero, false, false, PlayerStance.Standing, null, _tuning, _tick));
            Assert.Throws<ArgumentNullException>(() => WallClimbStep.Step(
                _climbing, _tallWall, Vector3.Zero, false, false, PlayerStance.Standing, MotorSpec.Default, null, _tick));
            Assert.Throws<ArgumentOutOfRangeException>(() => WallClimbStep.Step(
                _climbing, _tallWall, Vector3.Zero, false, false, PlayerStance.Standing, MotorSpec.Default, _tuning, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => WallClimbStep.Step(
                _climbing, _tallWall, Vector3.Zero, false, false, PlayerStance.Standing, MotorSpec.Default, _tuning, float.PositiveInfinity));
            Assert.Throws<ArgumentNullException>(() => WallClimbStep.ClimbedHeight(_climbing, null));
            Assert.Throws<ArgumentNullException>(() => WallClimbStep.CanReachLedge(_tallWall, null));
        }

        [Test]
        public void TheLogLinesNameWhatHappened()
        {
            Assert.That(WallClimbLog.Start(6f, float.PositiveInfinity), Is.EqualTo("[WallClimb] start speed=6.00 top=-"));
            Assert.That(WallClimbLog.Start(4.5f, 3.25f), Is.EqualTo("[WallClimb] start speed=4.50 top=3.25"));
            Assert.That(WallClimbLog.End(WallClimbMotion.Fell, 0.5f, 2f), Is.EqualTo("[WallClimb] end reason=Fell seconds=0.50 height=2.00"));
            Assert.That(
                WallClimbLog.End(WallClimbMotion.ReachedLedge, 0.25f, 1f), Does.StartWith(WallClimbLog.EndPrefixFor(WallClimbMotion.ReachedLedge)));
            Assert.That(
                WallClimbLog.End(WallClimbMotion.PushedOff, 0.25f, 1f), Does.Not.StartWith(WallClimbLog.EndPrefixFor(WallClimbMotion.Fell)));
            Assert.Throws<ArgumentOutOfRangeException>(() => WallClimbLog.End(WallClimbMotion.None, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => WallClimbLog.End(WallClimbMotion.Climbing, 0f, 0f));
        }

        private static WallClimbResult Step(
            CharacterParkourState state,
            FrontWallContact wall,
            Vector3 velocity,
            bool jump = false,
            bool grounded = false,
            PlayerStance stance = PlayerStance.Standing)
        {
            return WallClimbStep.Step(state, wall, velocity, jump, grounded, stance, MotorSpec.Default, _tuning, _tick);
        }
    }
}

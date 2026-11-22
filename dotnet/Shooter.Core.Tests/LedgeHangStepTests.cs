using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class LedgeHangStepTests
    {
        // 2 進で割り切れる tick にして、秒数と横の位置の期待値をちょうどの値で書く
        private const float _tick = 0.125f;
        private const float _tolerance = 1e-4f;

        // 手の高さが位置の 1 m 上、半径 0.5 の体。ぶら下がると位置はふちから 1 m 下、壁から 0.5 + 0.1 m 手前になる
        private static readonly StanceGeometry _stance = new StanceGeometry(2f, 0f, 0.6f, 1.2f, 0.5f);
        private static readonly Vector3 _wall = new Vector3(0f, 0f, -1f);
        private static readonly Vector3 _ledgePoint = new Vector3(0f, 3f, 1f);
        private static readonly Vector3 _hang = new Vector3(0f, 2f, 0.4f);
        private static readonly Vector3 _falling = new Vector3(0f, -3f, 1f);
        private static readonly Vector3 _airborne = new Vector3(0.1f, 2.3f, 0.2f);
        private static readonly LedgeContact _ledge = new LedgeContact(_ledgePoint, _wall, true, true);
        private static readonly LedgeHangTuning _tuning = new LedgeHangTuning(true, 0.6f, 2f, 4f, 0.5f);
        private static readonly CharacterParkourState _hanging = new CharacterParkourState(ParkourMove.LedgeHang, _tick, _wall, _ledgePoint);

        [Test]
        public void GrabsALedgeInReachWhileFalling()
        {
            var result = Step(CharacterParkourState.None, _ledge);

            Assert.That(result.Motion, Is.EqualTo(LedgeHangMotion.Grabbed));
            Assert.That(result.State, Is.EqualTo(new CharacterParkourState(ParkourMove.LedgeHang, 0.125f, _wall, _ledgePoint)));
            AssertVector(result.Position, _hang);
            Assert.That(result.Velocity, Is.EqualTo(Vector3.Zero), "grabbing stops the fall");
            Assert.That(result.HoldsBody, Is.True);
        }

        [Test]
        public void GrabsAtTheTopOfAJump()
        {
            Assert.That(Step(CharacterParkourState.None, _ledge, velocity: new Vector3(0f, 0f, 1f)).Motion, Is.EqualTo(LedgeHangMotion.Grabbed));
        }

        [Test]
        public void DoesNotGrabWhenSwitchedOff()
        {
            var result = LedgeHangStep.Step(
                CharacterParkourState.None,
                _ledge,
                _airborne,
                _falling,
                Vector2.Zero,
                false,
                false,
                false,
                _stance,
                LedgeHangTuning.Default,
                _tick,
                _ => true);

            Assert.That(LedgeHangTuning.Default.Enabled, Is.False);
            Assert.That(result.Motion, Is.EqualTo(LedgeHangMotion.None));
            Assert.That(result.State, Is.EqualTo(CharacterParkourState.None));
            Assert.That(result.Position, Is.EqualTo(_airborne), "the body is left to the ordinary move");
            Assert.That(result.Velocity, Is.EqualTo(_falling));
            Assert.That(result.HoldsBody, Is.False);
        }

        [Test]
        public void DoesNotGrabWithoutEachCondition()
        {
            var climbing = new CharacterParkourState(ParkourMove.WallClimb, _tick, _wall, Vector3.Zero);

            Assert.That(Step(CharacterParkourState.None, LedgeContact.None).Motion, Is.EqualTo(LedgeHangMotion.None), "no ledge");
            Assert.That(Step(CharacterParkourState.None, _ledge, grounded: true).Motion, Is.EqualTo(LedgeHangMotion.None), "on the ground");
            Assert.That(
                Step(CharacterParkourState.None, _ledge, velocity: new Vector3(0f, 0.5f, 1f)).Motion, Is.EqualTo(LedgeHangMotion.None), "rising");
            Assert.That(Step(CharacterParkourState.None, _ledge, crouch: true).Motion, Is.EqualTo(LedgeHangMotion.None), "crouch held");
            var other = Step(climbing, _ledge);
            Assert.That(other.Motion, Is.EqualTo(LedgeHangMotion.None), "another parkour move is running");
            Assert.That(other.State, Is.EqualTo(climbing));
        }

        [Test]
        public void HangsStillWithoutInput()
        {
            var result = Step(_hanging, _ledge);

            Assert.That(result.Motion, Is.EqualTo(LedgeHangMotion.Hanging));
            Assert.That(result.State, Is.EqualTo(new CharacterParkourState(ParkourMove.LedgeHang, 0.25f, _wall, _ledgePoint)));
            AssertVector(result.Position, _hang);
            Assert.That(result.Velocity, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void HangingFollowsTheMeasuredLedge()
        {
            var lower = new LedgeContact(new Vector3(0f, 2.95f, 1f), _wall, true, true);

            var result = Step(_hanging, lower);

            Assert.That(result.State.LedgePoint, Is.EqualTo(new Vector3(0f, 2.95f, 1f)));
            AssertVector(result.Position, new Vector3(0f, 1.95f, 0.4f));
        }

        [Test]
        public void ShimmiesRightAlongTheLedgeAtTheTunedSpeed()
        {
            var result = Step(_hanging, _ledge, new Vector2(1f, 0f));

            Assert.That(result.Motion, Is.EqualTo(LedgeHangMotion.Shimmying));
            Assert.That(result.State, Is.EqualTo(new CharacterParkourState(ParkourMove.LedgeShimmy, 0.125f, _wall, new Vector3(0.25f, 3f, 1f))));
            AssertVector(result.Position, new Vector3(0.25f, 2f, 0.4f));
            Assert.That(result.Velocity, Is.EqualTo(new Vector3(2f, 0f, 0f)));
        }

        [Test]
        public void ShimmiesLeftAndKeepsCountingTheShimmy()
        {
            var shimmying = new CharacterParkourState(ParkourMove.LedgeShimmy, _tick, _wall, new Vector3(0.25f, 3f, 1f));
            var here = new LedgeContact(new Vector3(0.25f, 3f, 1f), _wall, true, true);

            var result = Step(shimmying, here, new Vector2(-0.3f, 0f));

            Assert.That(result.Motion, Is.EqualTo(LedgeHangMotion.Shimmying));
            Assert.That(result.State, Is.EqualTo(new CharacterParkourState(ParkourMove.LedgeShimmy, 0.25f, _wall, new Vector3(0f, 3f, 1f))));
            AssertVector(result.Position, _hang);
            Assert.That(result.Velocity, Is.EqualTo(new Vector3(-2f, 0f, 0f)), "any sideways input moves at the tuned speed");
        }

        [Test]
        public void StopsAtTheEndOfTheLedge()
        {
            var shimmying = new CharacterParkourState(ParkourMove.LedgeShimmy, _tick, _wall, new Vector3(0.5f, 3f, 1f));
            var atEnd = new LedgeContact(new Vector3(0.5f, 3f, 1f), _wall, true, false);

            var stopped = Step(shimmying, atEnd, new Vector2(1f, 0f));

            Assert.That(stopped.Motion, Is.EqualTo(LedgeHangMotion.StoppedAtEdge));
            Assert.That(stopped.State, Is.EqualTo(new CharacterParkourState(ParkourMove.LedgeHang, 0.125f, _wall, new Vector3(0.5f, 3f, 1f))));
            AssertVector(stopped.Position, new Vector3(0.5f, 2f, 0.4f));
            Assert.That(stopped.Velocity, Is.EqualTo(Vector3.Zero));

            var back = Step(stopped.State, atEnd, new Vector2(-1f, 0f));

            Assert.That(back.Motion, Is.EqualTo(LedgeHangMotion.Shimmying), "the ledge still runs to the left");
            AssertVector(back.Position, new Vector3(0.25f, 2f, 0.4f));
        }

        [Test]
        public void ClimbsUpOnJump()
        {
            var result = Step(_hanging, _ledge, jump: true);

            Assert.That(result.Motion, Is.EqualTo(LedgeHangMotion.ClimbingUp));
            Assert.That(result.State, Is.EqualTo(new CharacterParkourState(ParkourMove.LedgeClimbUp, 0.125f, _wall, _ledgePoint)));
            AssertVector(result.Position, new Vector3(0f, 2.5f, 0.4f));
            AssertVector(result.Velocity, new Vector3(0f, 4f, 0f));
        }

        [Test]
        public void TheClimbFollowsThePathOverTheLedge()
        {
            var climbing = new CharacterParkourState(ParkourMove.LedgeClimbUp, 0.5f, _wall, _ledgePoint);

            var result = Step(climbing, LedgeContact.None, new Vector2(1f, 0f), jump: true, crouch: true);

            Assert.That(result.Motion, Is.EqualTo(LedgeHangMotion.ClimbingUp), "input and the ledge are not read while climbing");
            Assert.That(result.State, Is.EqualTo(new CharacterParkourState(ParkourMove.LedgeClimbUp, 0.625f, _wall, _ledgePoint)));
            AssertVector(result.Position, new Vector3(0f, 4.15f, 0.75f));
            AssertVector(result.Velocity, new Vector3(0f, 1.2f, 2.8f));
        }

        [Test]
        public void TheClimbEndsStandingOnTheLedge()
        {
            var climbing = new CharacterParkourState(ParkourMove.LedgeClimbUp, 0.8125f, _wall, _ledgePoint);

            var result = Step(climbing, LedgeContact.None);

            Assert.That(result.Motion, Is.EqualTo(LedgeHangMotion.ClimbedUp));
            Assert.That(result.State, Is.EqualTo(CharacterParkourState.None), "climbing up leaves no wait to grab again");
            AssertVector(result.Position, new Vector3(0f, 4f, 1.6f));
            Assert.That(_stance.Standing.BottomY + result.Position.Y, Is.EqualTo(_ledgePoint.Y).Within(_tolerance), "the feet are on the top");
            Assert.That(result.Velocity, Is.EqualTo(Vector3.Zero));
            Assert.That(result.HoldsBody, Is.True);
        }

        [Test]
        public void TheClimbPathRisesThenCrossesOntoTheTop()
        {
            var path = LedgeHangStep.ClimbPath(_wall, _stance, _tuning);

            Assert.That(path.Points.Count, Is.EqualTo(4));
            AssertVector(path.Points[1], new Vector3(0f, 2.15f, 0f));
            AssertVector(path.Points[2], new Vector3(0f, 2.15f, 1.2f));
            AssertVector(path.Points[3], new Vector3(0f, 2f, 1.2f));
            Assert.That(path.Speed, Is.EqualTo(4f));
            Assert.That(path.Duration, Is.EqualTo(0.875f).Within(_tolerance));
        }

        [Test]
        public void DoesNotClimbUnderABlockedPath()
        {
            var result = Step(_hanging, _ledge, jump: true, climbClear: false);

            Assert.That(result.Motion, Is.EqualTo(LedgeHangMotion.Hanging));
            Assert.That(result.State.Move, Is.EqualTo(ParkourMove.LedgeHang));
            AssertVector(result.Position, _hang);
        }

        [Test]
        public void LetsGoOnCrouch()
        {
            var result = Step(_hanging, _ledge, crouch: true, position: _hang);

            Assert.That(result.Motion, Is.EqualTo(LedgeHangMotion.LetGo));
            Assert.That(result.LetGoReason, Is.EqualTo(LedgeLetGoReason.Dropped));
            Assert.That(result.State, Is.EqualTo(new CharacterParkourState(ParkourMove.None, 0f, _wall, _ledgePoint)));
            Assert.That(result.Position, Is.EqualTo(_hang));
            Assert.That(result.Velocity, Is.EqualTo(Vector3.Zero), "the fall starts from rest");
            Assert.That(result.HoldsBody, Is.False);
            Assert.That(LedgeHangStep.IsWaitingToRegrab(result.State), Is.True);
        }

        [Test]
        public void LetsGoWhenTheLedgeIsLostOrTheFeetLand()
        {
            Assert.That(Step(_hanging, LedgeContact.None).LetGoReason, Is.EqualTo(LedgeLetGoReason.LostLedge));
            Assert.That(Step(_hanging, _ledge, grounded: true).LetGoReason, Is.EqualTo(LedgeLetGoReason.Landed));
            Assert.That(Step(_hanging, _ledge).LetGoReason, Is.EqualTo(LedgeLetGoReason.None), "still hanging");
        }

        [Test]
        public void DoesNotGrabAgainUntilTheRegrabSecondsPass()
        {
            var state = Step(_hanging, _ledge, crouch: true).State;

            var expected = new[] { 0.125f, 0.25f, 0.375f };
            foreach (var since in expected)
            {
                var waiting = Step(state, _ledge);
                Assert.That(waiting.Motion, Is.EqualTo(LedgeHangMotion.None), $"{since} s after letting go");
                Assert.That(waiting.State, Is.EqualTo(new CharacterParkourState(ParkourMove.None, since, _wall, _ledgePoint)));
                Assert.That(waiting.Position, Is.EqualTo(_airborne));
                state = waiting.State;
            }

            var regrabbed = Step(state, _ledge);

            Assert.That(regrabbed.Motion, Is.EqualTo(LedgeHangMotion.Grabbed), "0.5 s after letting go");
            Assert.That(regrabbed.State, Is.EqualTo(new CharacterParkourState(ParkourMove.LedgeHang, 0.125f, _wall, _ledgePoint)));
        }

        [Test]
        public void TheWaitEndsEvenWithoutALedge()
        {
            var waiting = new CharacterParkourState(ParkourMove.None, 0.375f, _wall, _ledgePoint);

            var result = Step(waiting, LedgeContact.None);

            Assert.That(result.Motion, Is.EqualTo(LedgeHangMotion.None));
            Assert.That(result.State, Is.EqualTo(CharacterParkourState.None));
            Assert.That(LedgeHangStep.IsWaitingToRegrab(result.State), Is.False);
        }

        [Test]
        public void SwitchingOffWhileHangingDropsTheHang()
        {
            var result = LedgeHangStep.Step(
                _hanging, _ledge, _hang, Vector3.Zero, Vector2.Zero, false, false, false, _stance, LedgeHangTuning.Default, _tick, _ => true);

            Assert.That(result.Motion, Is.EqualTo(LedgeHangMotion.None));
            Assert.That(result.State, Is.EqualTo(CharacterParkourState.None));
        }

        [Test]
        public void TheHangPositionKeepsTheBodyOffTheWall()
        {
            AssertVector(LedgeHangStep.HangPosition(_ledgePoint, _wall, _stance), _hang);
            AssertVector(LedgeHangStep.HangPosition(new Vector3(2f, 5f, 0f), new Vector3(1f, 0f, 0f), _stance), new Vector3(2.6f, 4f, 0f));
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            Assert.Throws<ArgumentNullException>(() => LedgeHangStep.Step(
                CharacterParkourState.None, _ledge, _airborne, _falling, Vector2.Zero, false, false, false, null, _tuning, _tick, _ => true));
            Assert.Throws<ArgumentNullException>(() => LedgeHangStep.Step(
                CharacterParkourState.None, _ledge, _airborne, _falling, Vector2.Zero, false, false, false, _stance, null, _tick, _ => true));
            Assert.Throws<ArgumentNullException>(() => LedgeHangStep.Step(
                CharacterParkourState.None, _ledge, _airborne, _falling, Vector2.Zero, false, false, false, _stance, _tuning, _tick, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => LedgeHangStep.Step(
                CharacterParkourState.None, _ledge, _airborne, _falling, Vector2.Zero, false, false, false, _stance, _tuning, 0f, _ => true));
            Assert.Throws<ArgumentOutOfRangeException>(() => LedgeHangStep.Step(
                CharacterParkourState.None, _ledge, _airborne, _falling, Vector2.Zero, false, false, false, _stance, _tuning, float.PositiveInfinity, _ => true));
        }

        private static LedgeHangResult Step(
            CharacterParkourState parkour,
            LedgeContact ledge,
            Vector2 move = default,
            bool jump = false,
            bool crouch = false,
            bool grounded = false,
            Vector3? velocity = null,
            Vector3? position = null,
            bool climbClear = true)
        {
            return LedgeHangStep.Step(
                parkour,
                ledge,
                position ?? _airborne,
                velocity ?? _falling,
                move,
                jump,
                crouch,
                grounded,
                _stance,
                _tuning,
                _tick,
                _ => climbClear);
        }

        private static void AssertVector(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(_tolerance));
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(_tolerance));
            Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(_tolerance));
        }
    }
}

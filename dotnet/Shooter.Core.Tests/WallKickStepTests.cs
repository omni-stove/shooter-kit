using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class WallKickStepTests
    {
        // 2 進で割り切れる tick にして、秒数の期待値をちょうどの値で書く
        private const float _tick = 0.125f;

        private static readonly Vector3 _rightWall = new Vector3(-1f, 0f, 0f);
        private static readonly Vector3 _leftWall = new Vector3(1f, 0f, 0f);
        private static readonly WallContact _onRight = new WallContact(WallRunSide.Right, _rightWall, 0.6f);
        private static readonly WallContact _onLeft = new WallContact(WallRunSide.Left, _leftWall, 0.6f);
        private static readonly WallKickTuning _tuning = new WallKickTuning(true, 6f, 7f);
        private static readonly CharacterParkourState _kickedRight = new CharacterParkourState(ParkourMove.WallKick, 0f, _rightWall, Vector3.Zero);

        [Test]
        public void KicksAwayFromAWallBesideWhenJumpingInTheAir()
        {
            var result = Step(CharacterParkourState.None, _onRight, new Vector3(2f, -3f, 8f), jump: true);

            Assert.That(result.Outcome, Is.EqualTo(WallKickOutcome.Kicked));
            Assert.That(result.Kicked, Is.True);
            Assert.That(result.State, Is.EqualTo(new CharacterParkourState(ParkourMove.WallKick, 0f, _rightWall, Vector3.Zero)));
            Assert.That(result.State.Move, Is.EqualTo(ParkourMove.WallKick));
            Assert.That(
                result.Velocity,
                Is.EqualTo(new Vector3(-7f, 6f, 8f)),
                "away from the wall and up at the tuned speeds, the fall and the push into the wall are dropped, the speed along the wall is kept");
        }

        [Test]
        public void KicksStraightAwayWhenNotMoving()
        {
            Assert.That(Step(CharacterParkourState.None, _onRight, Vector3.Zero, jump: true).Velocity, Is.EqualTo(new Vector3(-7f, 6f, 0f)));
            Assert.That(Step(CharacterParkourState.None, _onLeft, Vector3.Zero, jump: true).Velocity, Is.EqualTo(new Vector3(7f, 6f, 0f)));
        }

        [Test]
        public void KicksAlongTheNormalOfATurnedWall()
        {
            var turned = Vector3.Normalize(new Vector3(-1f, 0f, -0.5f));
            var result = Step(CharacterParkourState.None, new WallContact(WallRunSide.Right, turned, 0.6f), new Vector3(0f, 0f, 8f), jump: true);

            Assert.That(result.Kicked, Is.True);
            Assert.That(result.State.WallNormal, Is.EqualTo(turned));
            Assert.That(Vector3.Dot(result.Velocity, turned), Is.EqualTo(7f).Within(1e-5f), "away from the wall at the kick speed");
            Assert.That(result.Velocity.Y, Is.EqualTo(6f));
        }

        [Test]
        public void DoesNothingWithoutAWall()
        {
            var result = Step(CharacterParkourState.None, WallContact.None, new Vector3(2f, -3f, 8f), jump: true);

            Assert.That(result.Outcome, Is.EqualTo(WallKickOutcome.None));
            Assert.That(result.State, Is.EqualTo(CharacterParkourState.None));
            Assert.That(result.Velocity, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void DoesNothingWithoutJumping()
        {
            var result = Step(CharacterParkourState.None, _onRight, new Vector3(2f, -3f, 8f));

            Assert.That(result.Outcome, Is.EqualTo(WallKickOutcome.None));
            Assert.That(result.State, Is.EqualTo(CharacterParkourState.None));
            Assert.That(result.Velocity, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void DoesNothingWhenSwitchedOff()
        {
            var result = WallKickStep.Step(
                CharacterParkourState.None, CharacterWallRunState.None, _onRight, new Vector3(2f, -3f, 8f), true, false, WallKickTuning.Default, _tick);

            Assert.That(WallKickTuning.Default.Enabled, Is.False);
            Assert.That(result.Outcome, Is.EqualTo(WallKickOutcome.None));
            Assert.That(result.State, Is.EqualTo(CharacterParkourState.None));
            Assert.That(result.Velocity, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void DoesNothingOnTheGround()
        {
            var result = Step(CharacterParkourState.None, _onRight, new Vector3(2f, 0f, 8f), jump: true, grounded: true);

            Assert.That(result.Outcome, Is.EqualTo(WallKickOutcome.None));
            Assert.That(result.State, Is.EqualTo(CharacterParkourState.None));
            Assert.That(result.Velocity, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void DoesNothingWhileWallRunning()
        {
            var running = new CharacterWallRunState(WallRunSide.Right, _tick, _rightWall);
            var result = WallKickStep.Step(
                CharacterParkourState.None, running, _onRight, new Vector3(0f, -1f, 8f), true, false, _tuning, _tick);

            Assert.That(result.Outcome, Is.EqualTo(WallKickOutcome.None));
            Assert.That(result.State, Is.EqualTo(CharacterParkourState.None));
            Assert.That(result.Velocity, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void KicksAfterTheWallRunHasEnded()
        {
            var result = WallKickStep.Step(
                CharacterParkourState.None, CharacterWallRunState.LeftWall(_leftWall), _onRight, Vector3.Zero, true, false, _tuning, _tick);

            Assert.That(result.Kicked, Is.True);
            Assert.That(result.Velocity, Is.EqualTo(new Vector3(-7f, 6f, 0f)));
        }

        [Test]
        public void DoesNotTouchAnotherParkourMove()
        {
            var hanging = new CharacterParkourState(ParkourMove.LedgeHang, 0.25f, _rightWall, new Vector3(0f, 2f, 0f));
            var result = Step(hanging, _onRight, Vector3.Zero, jump: true);

            Assert.That(result.Outcome, Is.EqualTo(WallKickOutcome.None));
            Assert.That(result.State, Is.EqualTo(hanging));
            Assert.That(result.Velocity, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void DoesNotKickTheSameWallAgainBeforeLanding()
        {
            var first = Step(CharacterParkourState.None, _onRight, new Vector3(0f, -3f, 8f), jump: true);
            var second = Step(first.State, _onRight, new Vector3(-7f, 6f, 8f), jump: true);

            Assert.That(first.Kicked, Is.True);
            Assert.That(second.Outcome, Is.EqualTo(WallKickOutcome.SameWall));
            Assert.That(second.Kicked, Is.False);
            Assert.That(second.Velocity, Is.EqualTo(Vector3.Zero));
            Assert.That(
                second.State,
                Is.EqualTo(new CharacterParkourState(ParkourMove.WallKick, 0.125f, _rightWall, Vector3.Zero)),
                "the kicked wall is kept and the time since the kick goes on");
        }

        [Test]
        public void ASlightlyTurnedPartOfTheKickedWallIsTheSameWall()
        {
            var turned = Vector3.Normalize(new Vector3(-1f, 0f, -0.1f));
            var result = Step(_kickedRight, new WallContact(WallRunSide.Right, turned, 0.6f), Vector3.Zero, jump: true);

            Assert.That(result.Outcome, Is.EqualTo(WallKickOutcome.SameWall));
        }

        [Test]
        public void KicksTheWallAcrossAfterKicking()
        {
            var result = Step(_kickedRight, _onLeft, new Vector3(-7f, 6f, 8f), jump: true);

            Assert.That(result.Outcome, Is.EqualTo(WallKickOutcome.Kicked));
            Assert.That(result.State, Is.EqualTo(new CharacterParkourState(ParkourMove.WallKick, 0f, _leftWall, Vector3.Zero)));
            Assert.That(result.Velocity, Is.EqualTo(new Vector3(7f, 6f, 8f)));
        }

        [Test]
        public void KicksTheSameWallAgainAfterLanding()
        {
            var landed = Step(_kickedRight, WallContact.None, Vector3.Zero, grounded: true);
            var again = Step(landed.State, _onRight, Vector3.Zero, jump: true);

            Assert.That(landed.State, Is.EqualTo(CharacterParkourState.None));
            Assert.That(again.Outcome, Is.EqualTo(WallKickOutcome.Kicked));
            Assert.That(again.Velocity, Is.EqualTo(new Vector3(-7f, 6f, 0f)));
        }

        [Test]
        public void AWallRememberedByAnotherMoveDoesNotBlockTheKick()
        {
            var released = new CharacterParkourState(ParkourMove.None, 0.25f, _rightWall, Vector3.Zero);

            Assert.That(Step(released, _onRight, Vector3.Zero, jump: true).Outcome, Is.EqualTo(WallKickOutcome.Kicked));
        }

        [Test]
        public void CountsTheTimeSinceTheKickInTheAir()
        {
            var once = Step(_kickedRight, WallContact.None, new Vector3(-7f, 6f, 8f));
            var twice = Step(once.State, WallContact.None, new Vector3(-7f, 4f, 8f));

            Assert.That(once.Outcome, Is.EqualTo(WallKickOutcome.None));
            Assert.That(once.State, Is.EqualTo(new CharacterParkourState(ParkourMove.WallKick, 0.125f, _rightWall, Vector3.Zero)));
            Assert.That(twice.State, Is.EqualTo(new CharacterParkourState(ParkourMove.WallKick, 0.25f, _rightWall, Vector3.Zero)));
            Assert.That(twice.Velocity, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void LandingEndsTheKickButKeepsOtherMoves()
        {
            var rolling = new CharacterParkourState(ParkourMove.LandingRoll, 0.125f, Vector3.Zero, Vector3.Zero, 0.5f);

            Assert.That(WallKickStep.Land(_kickedRight), Is.EqualTo(CharacterParkourState.None));
            Assert.That(WallKickStep.Land(rolling), Is.EqualTo(rolling));
            Assert.That(Step(rolling, _onRight, Vector3.Zero, jump: true, grounded: true).State, Is.EqualTo(rolling));
        }

        [Test]
        public void RejectsMissingSettingsAndABadTick()
        {
            Assert.Throws<ArgumentNullException>(() => WallKickStep.Step(
                CharacterParkourState.None, CharacterWallRunState.None, _onRight, Vector3.Zero, true, false, null, _tick));
            Assert.Throws<ArgumentOutOfRangeException>(() => WallKickStep.Step(
                CharacterParkourState.None, CharacterWallRunState.None, _onRight, Vector3.Zero, true, false, _tuning, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => WallKickStep.Step(
                CharacterParkourState.None, CharacterWallRunState.None, _onRight, Vector3.Zero, true, false, _tuning, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => WallKickStep.Step(
                CharacterParkourState.None, CharacterWallRunState.None, _onRight, Vector3.Zero, true, false, _tuning, float.PositiveInfinity));
            Assert.Throws<ArgumentNullException>(() => WallKickStep.KickVelocity(Vector3.Zero, _rightWall, null));
        }

        [Test]
        public void LogLinesStartWithTheTagAndUseInvariantNumbers()
        {
            Assert.That(
                WallKickLog.Kicked(_onRight, new Vector3(-7f, 6f, 8f)),
                Is.EqualTo("[WallKick] kicked side=Right normal=(-1.00, 0.00, 0.00) velocity=(-7.00, 6.00, 8.00)"));
            Assert.That(WallKickLog.SameWall(_onLeft), Is.EqualTo("[WallKick] sameWall side=Left normal=(1.00, 0.00, 0.00)"));
            Assert.Throws<ArgumentException>(() => WallKickLog.Kicked(WallContact.None, Vector3.Zero));
            Assert.Throws<ArgumentException>(() => WallKickLog.SameWall(WallContact.None));
        }

        private static WallKickResult Step(
            CharacterParkourState parkour, WallContact wall, Vector3 velocity, bool jump = false, bool grounded = false)
        {
            return WallKickStep.Step(parkour, CharacterWallRunState.None, wall, velocity, jump, grounded, _tuning, _tick);
        }
    }
}

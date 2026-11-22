using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class LandingRollStepTests
    {
        // 2 進で割り切れる tick にして、秒数と速度の期待値をちょうどの値で書く
        private const float _tick = 0.125f;

        private static readonly LandingRollTuning _tuning = new LandingRollTuning(true, 0.25f, 0.5f, 0.5f, 6f);
        private static readonly Vector3 _falling = new Vector3(0f, -4f, 4f);
        private static readonly Vector3 _hardLanding = new Vector3(0f, -8f, 4f);
        private static readonly Vector3 _rolling = new Vector3(0f, 0f, 4f);

        [Test]
        public void PressingCrouchInTheAirStartsTheWindow()
        {
            var result = InAir(CharacterParkourState.None, crouch: true);

            Assert.That(result.Motion, Is.EqualTo(LandingRollMotion.None));
            Assert.That(result.State, Is.EqualTo(new CharacterParkourState(ParkourMove.None, 0.125f, Vector3.Zero, Vector3.Zero)));
            Assert.That(result.State.FallSpeedScale, Is.EqualTo(1f), "nothing is softened before landing");
            Assert.That(result.Velocity, Is.EqualTo(Vector3.Zero));
            Assert.That(result.Displacement, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void TheWindowKeepsCountingAfterCrouchIsLetGo()
        {
            var armed = InAir(CharacterParkourState.None, crouch: true).State;

            Assert.That(InAir(armed, crouch: false).State.Elapsed, Is.EqualTo(0.25f));
            Assert.That(InAir(armed, crouch: true).State.Elapsed, Is.EqualTo(0.25f), "holding it does not start the window again");
        }

        [Test]
        public void FallingWithoutCrouchDoesNotStartTheWindow()
        {
            Assert.That(InAir(CharacterParkourState.None, crouch: false).State, Is.EqualTo(CharacterParkourState.None));
        }

        [Test]
        public void CrouchHeldWhileLeavingTheGroundDoesNotCountUntilInTheAir()
        {
            var result = LandingRollStep.Step(CharacterParkourState.None, _falling, true, true, false, _tuning, _tick);

            Assert.That(result.State, Is.EqualTo(CharacterParkourState.None));
        }

        [Test]
        public void LandingInsideTheWindowRollsWithTheScaledFallSpeed()
        {
            var armed = InAir(CharacterParkourState.None, crouch: true).State;

            var result = Land(armed, _hardLanding);

            Assert.That(result.Motion, Is.EqualTo(LandingRollMotion.Started), "0.25 seconds after the press is the end of the window");
            Assert.That(result.IsRolling, Is.True);
            Assert.That(result.State, Is.EqualTo(new CharacterParkourState(ParkourMove.LandingRoll, 0.125f, Vector3.Zero, Vector3.Zero, 0.5f)));
            Assert.That(result.State.FallSpeedScale, Is.EqualTo(0.5f));
            Assert.That(result.Velocity, Is.EqualTo(new Vector3(0f, 0f, 4f)), "the fall is dropped and the forward speed is kept");
            Assert.That(result.Displacement, Is.EqualTo(new Vector3(0f, 0f, 0.5f)));
        }

        [Test]
        public void PressingOnTheLandingTickRolls()
        {
            var result = Land(CharacterParkourState.None, _hardLanding, crouch: true);

            Assert.That(result.Motion, Is.EqualTo(LandingRollMotion.Started));
            Assert.That(result.State.FallSpeedScale, Is.EqualTo(0.5f));
        }

        [Test]
        public void TheRollKeepsSidewaysAndForwardSpeedTogether()
        {
            var result = Land(CharacterParkourState.None, new Vector3(3f, -10f, 4f), crouch: true);

            Assert.That(result.Velocity, Is.EqualTo(new Vector3(3f, 0f, 4f)));
            Assert.That(result.Displacement, Is.EqualTo(new Vector3(0.375f, 0f, 0.5f)));
        }

        [Test]
        public void LandingAfterTheWindowDoesNothing()
        {
            var state = InAir(CharacterParkourState.None, crouch: true).State;
            state = InAir(state, crouch: false).State;

            var result = Land(state, _hardLanding);

            Assert.That(state.Elapsed, Is.EqualTo(0.25f));
            Assert.That(result.Motion, Is.EqualTo(LandingRollMotion.None), "0.375 seconds after the press is past 0.25");
            Assert.That(result.State, Is.EqualTo(CharacterParkourState.None));
            Assert.That(result.State.FallSpeedScale, Is.EqualTo(1f));
            Assert.That(result.Velocity, Is.EqualTo(Vector3.Zero));
            Assert.That(result.Displacement, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void LandingWithoutPressingDoesNothing()
        {
            var result = Land(CharacterParkourState.None, _hardLanding);

            Assert.That(result.Motion, Is.EqualTo(LandingRollMotion.None));
            Assert.That(result.State.FallSpeedScale, Is.EqualTo(1f));
        }

        [Test]
        public void DoesNothingWhenSwitchedOff()
        {
            var armed = new CharacterParkourState(ParkourMove.None, 0.125f, Vector3.Zero, Vector3.Zero);

            var result = LandingRollStep.Step(armed, _hardLanding, true, false, true, LandingRollTuning.Default, _tick);

            Assert.That(LandingRollTuning.Default.Enabled, Is.False);
            Assert.That(result.Motion, Is.EqualTo(LandingRollMotion.None));
            Assert.That(result.State, Is.EqualTo(armed));
            Assert.That(result.State.FallSpeedScale, Is.EqualTo(1f));
            Assert.That(result.Velocity, Is.EqualTo(Vector3.Zero));
            Assert.That(result.Displacement, Is.EqualTo(Vector3.Zero));
            Assert.That(
                LandingRollStep.Step(CharacterParkourState.None, _falling, true, false, false, LandingRollTuning.Default, _tick).State,
                Is.EqualTo(CharacterParkourState.None),
                "the window does not start either");
        }

        [Test]
        public void LandingFromLowDoesNothing()
        {
            var armed = InAir(CharacterParkourState.None, crouch: true).State;

            var result = Land(armed, new Vector3(0f, -5.5f, 4f));

            Assert.That(result.Motion, Is.EqualTo(LandingRollMotion.None), "5.5 is slower than the 6 needed");
            Assert.That(result.State, Is.EqualTo(CharacterParkourState.None));
            Assert.That(result.State.FallSpeedScale, Is.EqualTo(1f));
            Assert.That(result.Velocity, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void LandingAtExactlyTheMinimumFallSpeedRolls()
        {
            var armed = InAir(CharacterParkourState.None, crouch: true).State;

            var result = Land(armed, new Vector3(0f, -6f, 4f));

            Assert.That(result.Motion, Is.EqualTo(LandingRollMotion.Started));
            Assert.That(result.State.FallSpeedScale, Is.EqualTo(0.5f));
        }

        [Test]
        public void RollsForTheTunedSecondsAndThenStops()
        {
            var armed = InAir(CharacterParkourState.None, crouch: true).State;
            var result = Land(armed, _hardLanding);
            var rollingTicks = 0;
            var moved = Vector3.Zero;
            while (result.IsRolling)
            {
                rollingTicks++;
                moved += result.Displacement;
                Assert.That(result.State.FallSpeedScale, Is.EqualTo(0.5f), "the scale lasts the whole roll");
                Assert.That(result.Velocity, Is.EqualTo(_rolling));
                Assert.That(rollingTicks, Is.LessThan(100), "the roll should end");
                result = OnGround(result.State, result.Velocity);
            }

            Assert.That(rollingTicks, Is.EqualTo(4), "0.5 seconds of 0.125 second ticks");
            Assert.That(moved, Is.EqualTo(new Vector3(0f, 0f, 2f)));
            Assert.That(result.Motion, Is.EqualTo(LandingRollMotion.Finished));
            Assert.That(result.State, Is.EqualTo(CharacterParkourState.None));
            Assert.That(result.State.FallSpeedScale, Is.EqualTo(1f));
            Assert.That(result.Velocity, Is.EqualTo(Vector3.Zero));
            Assert.That(result.Displacement, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void ContinuesWithTheElapsedSecondsAndScale()
        {
            var rolling = new CharacterParkourState(ParkourMove.LandingRoll, 0.125f, Vector3.Zero, Vector3.Zero, 0.5f);

            var result = OnGround(rolling, new Vector3(1f, 0f, 2f));

            Assert.That(result.Motion, Is.EqualTo(LandingRollMotion.Rolling));
            Assert.That(result.State, Is.EqualTo(new CharacterParkourState(ParkourMove.LandingRoll, 0.25f, Vector3.Zero, Vector3.Zero, 0.5f)));
            Assert.That(result.Velocity, Is.EqualTo(new Vector3(1f, 0f, 2f)));
            Assert.That(result.Displacement, Is.EqualTo(new Vector3(0.125f, 0f, 0.25f)));
        }

        [Test]
        public void RollingOffAnEdgeStops()
        {
            var rolling = new CharacterParkourState(ParkourMove.LandingRoll, 0.125f, Vector3.Zero, Vector3.Zero, 0.5f);

            var result = LandingRollStep.Step(rolling, _rolling, false, true, false, _tuning, _tick);

            Assert.That(result.Motion, Is.EqualTo(LandingRollMotion.Finished));
            Assert.That(result.State, Is.EqualTo(CharacterParkourState.None));
            Assert.That(result.State.FallSpeedScale, Is.EqualTo(1f));
        }

        [Test]
        public void SwitchingOffMidRollStops()
        {
            var rolling = new CharacterParkourState(ParkourMove.LandingRoll, 0.125f, Vector3.Zero, Vector3.Zero, 0.5f);

            var result = LandingRollStep.Step(rolling, _rolling, false, true, true, LandingRollTuning.Default, _tick);

            Assert.That(result.Motion, Is.EqualTo(LandingRollMotion.Finished));
            Assert.That(result.State, Is.EqualTo(CharacterParkourState.None));
        }

        [Test]
        public void LeavesOtherMovesAlone()
        {
            var kick = new CharacterParkourState(ParkourMove.WallKick, 0.125f, Vector3.UnitX, Vector3.Zero);

            var result = Land(kick, _hardLanding, crouch: true);

            Assert.That(result.Motion, Is.EqualTo(LandingRollMotion.None));
            Assert.That(result.State, Is.EqualTo(kick));
        }

        [Test]
        public void KeepsTheWallAndLedgeItRemembers()
        {
            var remembered = new CharacterParkourState(ParkourMove.None, 0f, Vector3.UnitX, new Vector3(1f, 2f, 3f));

            var armed = InAir(remembered, crouch: true).State;
            var landed = Land(InAir(armed, crouch: false).State, _hardLanding).State;

            Assert.That(armed, Is.EqualTo(new CharacterParkourState(ParkourMove.None, 0.125f, Vector3.UnitX, new Vector3(1f, 2f, 3f))));
            Assert.That(landed, Is.EqualTo(remembered));
        }

        [Test]
        public void StandingOnTheGroundClearsAnOpenWindow()
        {
            var armed = new CharacterParkourState(ParkourMove.None, 0.125f, Vector3.Zero, Vector3.Zero);

            Assert.That(OnGround(armed, _rolling).State, Is.EqualTo(CharacterParkourState.None));
            Assert.That(OnGround(CharacterParkourState.None, _rolling, crouch: true).State, Is.EqualTo(CharacterParkourState.None), "crouching on the ground");
        }

        [Test]
        public void RejectsBadArguments()
        {
            Assert.That(
                () => LandingRollStep.Step(CharacterParkourState.None, _falling, false, false, false, null, _tick),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => LandingRollStep.Step(CharacterParkourState.None, _falling, false, false, false, _tuning, 0f),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => LandingRollStep.Step(CharacterParkourState.None, _falling, false, false, false, _tuning, float.NaN),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => LandingRollStep.Step(CharacterParkourState.None, _falling, false, false, false, _tuning, float.PositiveInfinity),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => LandingRollStep.CanRoll(0.125f, _hardLanding, null), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void FallSpeedIsTheDownwardSpeedOnly()
        {
            Assert.That(LandingRollStep.FallSpeed(new Vector3(3f, -7f, 4f)), Is.EqualTo(7f));
            Assert.That(LandingRollStep.FallSpeed(new Vector3(0f, 2f, 0f)), Is.EqualTo(0f));
        }

        [Test]
        public void StartLogShowsTheFallTheScaleTheSpeedAndTheSeconds()
        {
            var landing = new Vector3(3f, -8f, 4f);
            var result = Land(CharacterParkourState.None, landing, crouch: true);

            Assert.That(
                LandingRollLog.Start(landing, result, _tuning),
                Is.EqualTo("[LandingRoll] start fallSpeed=8.00 fallSpeedScale=0.50 speed=5.00 seconds=0.50"));
            Assert.That(LandingRollLog.Start(landing, result, _tuning), Does.StartWith(LandingRollLog.StartPrefix));
        }

        [Test]
        public void StartLogRefusesAResultThatDidNotStart()
        {
            var nothing = LandingRollResult.Resting(CharacterParkourState.None);

            Assert.That(() => LandingRollLog.Start(_hardLanding, nothing, _tuning), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => LandingRollLog.Start(_hardLanding, nothing, null), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void FinishLogShowsTheRolledSeconds()
        {
            Assert.That(LandingRollLog.Finish(0.5f), Is.EqualTo("[LandingRoll] finish seconds=0.50"));
            Assert.That(LandingRollLog.Finish(0.5f), Does.StartWith(LandingRollLog.FinishPrefix));
            Assert.That(() => LandingRollLog.Finish(-0.1f), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => LandingRollLog.Finish(float.NaN), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        private static LandingRollResult InAir(CharacterParkourState state, bool crouch)
        {
            return LandingRollStep.Step(state, _falling, crouch, false, false, _tuning, _tick);
        }

        private static LandingRollResult Land(CharacterParkourState state, Vector3 velocity, bool crouch = false)
        {
            return LandingRollStep.Step(state, velocity, crouch, false, true, _tuning, _tick);
        }

        private static LandingRollResult OnGround(CharacterParkourState state, Vector3 velocity, bool crouch = false)
        {
            return LandingRollStep.Step(state, velocity, crouch, true, true, _tuning, _tick);
        }
    }
}

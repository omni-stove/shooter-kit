using System.Numerics;
using GameKit.World;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // 床の上面は y = 0。重力は 20 なので、6 の高さからは着地まで 0.25 秒より長く落ち、落ちる速さは 6 を超える。
    public sealed class CharacterMotorSimulationLandingRollTests
    {
        // 2 進で割り切れる tick にして、転がる秒数を数え違えずに tick の数で書く
        private const float _tick = 1f / 64f;
        private const int _maxTicks = 200;

        // 窓 0.25 秒・割合 0.5・転がる 0.5 秒・落ちる速さ 6 から
        private static readonly LandingRollTuning _roll = new LandingRollTuning(true, 0.25f, 0.5f, 0.5f, 6f);

        // 着地の 0.25 秒前より手前の高さ。そこからしゃがみを押すと、窓の中で着地する
        private const float _pressBelowY = 1.5f;

        [Test]
        public void PressingCrouchJustBeforeLandingRollsWithTheScaledFallSpeed()
        {
            var simulation = NewSimulation(_roll);

            var landed = FallAndLand(simulation, pressCrouchNearTheGround: true);

            Assert.That(landed.Parkour.Move, Is.EqualTo(ParkourMove.LandingRoll));
            Assert.That(landed.Parkour.Elapsed, Is.EqualTo(_tick));
            Assert.That(landed.Parkour.FallSpeedScale, Is.EqualTo(0.5f));
            Assert.That(-landed.Velocity.Y, Is.GreaterThan(6f), "the landing tick keeps the fall speed for the fall damage");
            Assert.That(simulation.LastLandingRoll.Motion, Is.EqualTo(LandingRollMotion.Started));
        }

        [Test]
        public void TheRolledLandingCountsHalfTheFallSpeedOfAPlainLanding()
        {
            var plain = FallAndLand(NewSimulation(_roll), pressCrouchNearTheGround: false);
            var rolled = FallAndLand(NewSimulation(_roll), pressCrouchNearTheGround: true);

            var plainSpeed = -plain.Velocity.Y * plain.Parkour.FallSpeedScale;
            var rolledSpeed = -rolled.Velocity.Y * rolled.Parkour.FallSpeedScale;

            Assert.That(plain.Parkour, Is.EqualTo(CharacterParkourState.None), "not pressing crouch does not roll");
            Assert.That(plain.Parkour.FallSpeedScale, Is.EqualTo(1f));
            Assert.That(rolledSpeed, Is.EqualTo(plainSpeed * 0.5f).Within(0.5f), "the same drop, half the counted speed");
            Assert.That(rolledSpeed, Is.LessThan(plainSpeed));
        }

        [Test]
        public void TheRollCarriesTheLandingSpeedForwardForTheTunedSecondsWithoutInput()
        {
            var simulation = NewSimulation(_roll);
            var state = FallAndLand(simulation, pressCrouchNearTheGround: true);
            var forward = new Vector3(state.Velocity.X, 0f, state.Velocity.Z);
            Assert.That(forward.Z, Is.GreaterThan(1f), "the runner was moving forward when landing");

            var rollingTicks = 0;
            while (true)
            {
                var before = state;
                state = simulation.Step(state, Input(Vector2.Zero));
                if (state.Parkour.Move != ParkourMove.LandingRoll)
                {
                    break;
                }

                rollingTicks++;
                Assert.That(rollingTicks, Is.LessThan(_maxTicks), "the roll should end");
                Assert.That(simulation.LastLandingRoll.Motion, Is.EqualTo(LandingRollMotion.Rolling));
                Assert.That(state.IsGrounded, Is.True, "tick " + rollingTicks);
                Assert.That(state.Velocity, Is.EqualTo(new Vector3(forward.X, PlayerMotor.GroundedVerticalSpeed, forward.Z)), "tick " + rollingTicks);
                Assert.That(state.Position.Z - before.Position.Z, Is.EqualTo(forward.Z * _tick).Within(1e-3f), "tick " + rollingTicks);
                Assert.That(state.Parkour.FallSpeedScale, Is.EqualTo(0.5f));
                Assert.That(state.Stance, Is.EqualTo(PlayerStance.Standing), "the roll does not read crouch");
            }

            Assert.That(rollingTicks, Is.EqualTo(31), "the landing tick and 31 more make 0.5 seconds of 1/64 second ticks");
            Assert.That(state.Parkour, Is.EqualTo(CharacterParkourState.None));
            Assert.That(state.Parkour.FallSpeedScale, Is.EqualTo(1f));
            Assert.That(simulation.LastLandingRoll.Motion, Is.EqualTo(LandingRollMotion.Finished));
        }

        [Test]
        public void PressingCrouchTooEarlyDoesNotRoll()
        {
            var simulation = NewSimulation(_roll);
            var state = Falling();
            var before = state;
            for (var i = 0; i < _maxTicks && !(!before.IsGrounded && state.IsGrounded); i++)
            {
                before = state;
                state = simulation.Step(state, Input(new Vector2(0f, 1f), crouch: i == 0));
            }

            Assert.That(state.IsGrounded, Is.True);
            Assert.That(state.Parkour.Move, Is.EqualTo(ParkourMove.None), "the 0.25 second window closed long before landing");
            Assert.That(state.Parkour.FallSpeedScale, Is.EqualTo(1f));
        }

        [Test]
        public void TheDefaultTuningDoesNotRoll()
        {
            var landed = FallAndLand(NewSimulation(LandingRollTuning.Default), pressCrouchNearTheGround: true);

            Assert.That(landed.Parkour, Is.EqualTo(CharacterParkourState.None));
            Assert.That(landed.Parkour.FallSpeedScale, Is.EqualTo(1f));
        }

        [Test]
        public void ADownedPlayerDoesNotRollFromTheCrouchItIsForcedInto()
        {
            var simulation = NewSimulation(_roll);
            var state = Falling();
            var before = state;
            for (var i = 0; i < _maxTicks && !(!before.IsGrounded && state.IsGrounded); i++)
            {
                before = state;
                state = simulation.Step(state, Input(Vector2.Zero, downed: true));
            }

            Assert.That(state.IsGrounded, Is.True);
            Assert.That(state.Parkour.Move, Is.EqualTo(ParkourMove.None));
            Assert.That(state.Parkour.FallSpeedScale, Is.EqualTo(1f));
        }

        [Test]
        public void DodgeAndJumpAreIgnoredWhileRolling()
        {
            var simulation = NewSimulation(_roll);
            var state = FallAndLand(simulation, pressCrouchNearTheGround: true);

            var after = simulation.Step(state, Input(new Vector2(1f, 0f), jump: true, dodge: true));

            Assert.That(after.IsDodging, Is.False);
            Assert.That(after.IsGrounded, Is.True, "the jump is not taken");
            Assert.That(after.Parkour.Move, Is.EqualTo(ParkourMove.LandingRoll));
            Assert.That(after.Velocity.X, Is.EqualTo(state.Velocity.X), "the move input does not steer the roll");
        }

        [Test]
        public void TheSameInputsRollTheSameWay()
        {
            var first = NewSimulation(_roll);
            var second = NewSimulation(_roll);
            var a = FallAndLand(first, pressCrouchNearTheGround: true);
            var b = FallAndLand(second, pressCrouchNearTheGround: true);
            Assert.That(b, Is.EqualTo(a));

            for (var i = 0; i < 40; i++)
            {
                a = first.Step(a, Input(Vector2.Zero));
                b = second.Step(b, Input(Vector2.Zero));
                Assert.That(b, Is.EqualTo(a), "tick " + i);
            }
        }

        [Test]
        public void AStateReceivedMidRollKeepsRolling()
        {
            var simulation = NewSimulation(_roll);
            var standing = new CharacterMotorState(new Vector3(0f, 1.1f, 0f), Vector3.Zero, false, false);
            for (var i = 0; i < 60; i++)
            {
                standing = simulation.Step(standing, Input(Vector2.Zero));
            }

            Assert.That(standing.IsGrounded, Is.True);
            var rolling = new CharacterMotorState(standing.Position, new Vector3(0f, -10f, 4f), true, false)
                .WithParkour(new CharacterParkourState(ParkourMove.LandingRoll, 0.25f, Vector3.Zero, Vector3.Zero, 0.5f));

            var after = simulation.Step(rolling, Input(Vector2.Zero));

            Assert.That(after.Parkour, Is.EqualTo(new CharacterParkourState(ParkourMove.LandingRoll, 0.25f + _tick, Vector3.Zero, Vector3.Zero, 0.5f)));
            Assert.That(after.Velocity, Is.EqualTo(new Vector3(0f, PlayerMotor.GroundedVerticalSpeed, 4f)), "the fall is dropped and the forward speed is kept");
            Assert.That(after.Position.Z - rolling.Position.Z, Is.EqualTo(4f * _tick).Within(1e-3f));
        }

        private static CharacterMotorState Falling()
        {
            return new CharacterMotorState(new Vector3(0f, 6f, 0f), new Vector3(0f, 0f, 4f), false, false);
        }

        // 前へ歩きながら落ち、着地した tick の状態を返す
        private static CharacterMotorState FallAndLand(CharacterMotorSimulation simulation, bool pressCrouchNearTheGround)
        {
            var state = Falling();
            for (var i = 0; i < _maxTicks; i++)
            {
                var crouch = pressCrouchNearTheGround && state.Position.Y < _pressBelowY;
                var before = state;
                state = simulation.Step(state, Input(new Vector2(0f, 1f), crouch: crouch));
                if (!before.IsGrounded && state.IsGrounded)
                {
                    return state;
                }
            }

            Assert.Fail("the runner never landed");
            return state;
        }

        private static CharacterMotorInput Input(Vector2 move, bool crouch = false, bool jump = false, bool dodge = false, bool downed = false)
        {
            return new CharacterMotorInput(move, 0f, false, jump, crouch, downed, false, false, dodge, true);
        }

        private static CharacterMotorSimulation NewSimulation(LandingRollTuning landingRoll)
        {
            var colliders = new[]
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, new Vector3(0f, -1f, 0f), new Vector3(50f, 1f, 50f), Quaternion.Identity),
            };
            var body = new CharacterBody(
                new BvhWorldCollision(new WorldMap(colliders)), CapsuleMoverSpec.Default, new StanceGeometry(2f, 0f, 0.6f, 1.2f, 0.5f));
            var simulation = new CharacterMotorSimulation(body, new PlayerMotor(MotorSpec.Default), _tick, -50f);
            simulation.Parkour = new ParkourTuning(WallKickTuning.Default, landingRoll, WallClimbTuning.Default, LedgeHangTuning.Default);
            return simulation;
        }
    }
}

using System.Numerics;
using GameKit.World;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class CharacterMotorSimulationLedgeHangTests
    {
        private const float _tick = 1f / 60f;
        private const float _tolerance = 1e-3f;

        private static readonly Vector3 _wallNormal = new Vector3(0f, 0f, -1f);

        // 上端が高さ 2.9、手前の面が z = 1、幅が x = -1 から 1 までの台の、x = 0 のふち
        private static readonly Vector3 _ledgePoint = new Vector3(0f, 2.9f, 1f);

        // 半径 0.5 + 離す幅 0.1 だけ手前に、手 (立った形の上端、位置の 1 m 上) をふちの高さに合わせた位置
        private static readonly Vector3 _hangPosition = new Vector3(0f, 1.9f, 0.4f);

        [Test]
        public void FallingPastTheLedgeGrabsItAndHangsBelowIt()
        {
            var simulation = NewSimulation(true);

            var after = simulation.Step(Falling(new Vector3(0f, 2.2f, 0.4f)), Input());

            Assert.That(after.Parkour.Move, Is.EqualTo(ParkourMove.LedgeHang));
            AssertVector(after.Parkour.LedgePoint, _ledgePoint);
            AssertVector(after.Parkour.WallNormal, _wallNormal);
            AssertVector(after.Position, _hangPosition);
            Assert.That(after.Velocity, Is.EqualTo(Vector3.Zero));
            Assert.That(after.IsGrounded, Is.False);
            Assert.That(after.IsHangingOnLedge, Is.True);
            Assert.That(simulation.LastLedgeHang.Motion, Is.EqualTo(LedgeHangMotion.Grabbed));
            Assert.That(simulation.LastLedgeHangBefore, Is.EqualTo(CharacterParkourState.None));
        }

        [Test]
        public void HangingStaysAtTheLedgeWithoutFalling()
        {
            var simulation = NewSimulation(true);
            var state = simulation.Step(Falling(new Vector3(0f, 2.2f, 0.4f)), Input());

            for (var i = 0; i < 60; i++)
            {
                state = simulation.Step(state, Input());
                Assert.That(state.Parkour.Move, Is.EqualTo(ParkourMove.LedgeHang), "tick " + i);
            }

            AssertVector(state.Position, _hangPosition);
            Assert.That(state.Parkour.Elapsed, Is.EqualTo(61 * _tick).Within(1e-4f));
            Assert.That(simulation.LastLedgeHang.Motion, Is.EqualTo(LedgeHangMotion.Hanging));
        }

        [Test]
        public void HangingResetsTheAirJumpsAndBlocksAimingAndTheWeapon()
        {
            var simulation = NewSimulation(true);

            var after = simulation.Step(Falling(new Vector3(0f, 2.2f, 0.4f)), Input());

            Assert.That(after.AirJumpsUsed, Is.Zero);
            Assert.That(after.CanAimDownSights, Is.False);
        }

        [Test]
        public void MovingRightShimmiesAlongTheLedgeAndStopsAtItsEnd()
        {
            var simulation = NewSimulation(true);
            var state = simulation.Step(Falling(new Vector3(0f, 2.2f, 0.4f)), Input());

            state = simulation.Step(state, Input(new Vector2(1f, 0f)));
            Assert.That(state.Parkour.Move, Is.EqualTo(ParkourMove.LedgeShimmy));
            Assert.That(simulation.LastLedgeHang.Motion, Is.EqualTo(LedgeHangMotion.Shimmying));
            Assert.That(state.Position.X, Is.EqualTo(LedgeHangTuning.DefaultShimmySpeed * _tick).Within(_tolerance));
            Assert.That(state.Velocity.X, Is.EqualTo(LedgeHangTuning.DefaultShimmySpeed).Within(_tolerance));

            for (var i = 0; i < 60; i++)
            {
                state = simulation.Step(state, Input(new Vector2(1f, 0f)));
            }

            Assert.That(state.Parkour.Move, Is.EqualTo(ParkourMove.LedgeHang), "it stops where the body would pass the end at x = 1");
            Assert.That(simulation.LastLedgeHang.Motion, Is.EqualTo(LedgeHangMotion.StoppedAtEdge));
            Assert.That(state.Position.X, Is.GreaterThan(0.4f).And.LessThan(0.5f));
            Assert.That(state.Position.Y, Is.EqualTo(_hangPosition.Y).Within(_tolerance));
            Assert.That(state.Position.Z, Is.EqualTo(_hangPosition.Z).Within(_tolerance));
        }

        [Test]
        public void MovingLeftShimmiesTheOtherWay()
        {
            var simulation = NewSimulation(true);
            var state = simulation.Step(Falling(new Vector3(0f, 2.2f, 0.4f)), Input());

            for (var i = 0; i < 10; i++)
            {
                state = simulation.Step(state, Input(new Vector2(-1f, 0f)));
            }

            Assert.That(state.Parkour.Move, Is.EqualTo(ParkourMove.LedgeShimmy));
            Assert.That(state.Position.X, Is.EqualTo(-10f * LedgeHangTuning.DefaultShimmySpeed * _tick).Within(_tolerance));
        }

        [Test]
        public void JumpingClimbsOntoTheTopAndStandsThere()
        {
            var simulation = NewSimulation(true);
            var state = simulation.Step(Falling(new Vector3(0f, 2.2f, 0.4f)), Input());

            state = simulation.Step(state, Input(jump: true));
            Assert.That(state.Parkour.Move, Is.EqualTo(ParkourMove.LedgeClimbUp));
            Assert.That(simulation.LastLedgeHang.Motion, Is.EqualTo(LedgeHangMotion.ClimbingUp));
            Assert.That(state.IsHangingOnLedge, Is.True, "climbing up still holds the ledge");

            var climbedUp = false;
            for (var i = 0; i < 120 && !climbedUp; i++)
            {
                state = simulation.Step(state, Input());
                climbedUp = simulation.LastLedgeHang.Motion == LedgeHangMotion.ClimbedUp;
            }

            Assert.That(climbedUp, Is.True);
            Assert.That(state.Parkour, Is.EqualTo(CharacterParkourState.None));
            Assert.That(state.Position.Z, Is.GreaterThan(1f), "the body is over the top, past the face");

            for (var i = 0; i < 30; i++)
            {
                state = simulation.Step(state, Input());
            }

            Assert.That(state.IsGrounded, Is.True);
            Assert.That(state.Position.Y, Is.EqualTo(3.9f + CapsuleMoverSpec.DefaultSkinWidth).Within(0.05f), "standing on the top at 2.9, within the skin");
        }

        [Test]
        public void JumpingUnderACeilingKeepsHanging()
        {
            var simulation = NewSimulation(true, withCeiling: true);
            var state = simulation.Step(Falling(new Vector3(0f, 2.2f, 0.4f)), Input());

            state = simulation.Step(state, Input(jump: true));

            Assert.That(state.Parkour.Move, Is.EqualTo(ParkourMove.LedgeHang));
            AssertVector(state.Position, _hangPosition);
        }

        [Test]
        public void CrouchingLetsGoAndDoesNotRegrabOnTheWayDown()
        {
            var simulation = NewSimulation(true);
            var state = simulation.Step(Falling(new Vector3(0f, 2.2f, 0.4f)), Input());

            state = simulation.Step(state, Input(crouch: true));

            Assert.That(simulation.LastLedgeHang.Motion, Is.EqualTo(LedgeHangMotion.LetGo));
            Assert.That(simulation.LastLedgeHang.LetGoReason, Is.EqualTo(LedgeLetGoReason.Dropped));
            Assert.That(simulation.LastLedgeHangBefore.Move, Is.EqualTo(ParkourMove.LedgeHang));
            Assert.That(state.Parkour.Move, Is.EqualTo(ParkourMove.None));
            Assert.That(LedgeHangStep.IsWaitingToRegrab(state.Parkour), Is.True);
            Assert.That(state.IsHangingOnLedge, Is.False);

            var landed = false;
            for (var i = 0; i < 60; i++)
            {
                state = simulation.Step(state, Input());
                Assert.That(state.IsHangingOnLedge, Is.False, "tick " + i);
                landed |= state.IsGrounded;
            }

            Assert.That(landed, Is.True);
            Assert.That(state.Parkour, Is.EqualTo(CharacterParkourState.None), "the regrab wait is over");
        }

        [Test]
        public void WithLandingRollOnTheRegrabWaitCountsOnceATickAndDoesNotRoll()
        {
            var simulation = NewSimulation(true, landingRoll: true);
            var state = simulation.Step(Falling(new Vector3(0f, 2.2f, 0.4f)), Input());
            state = simulation.Step(state, Input(crouch: true));
            Assert.That(state.Parkour.Elapsed, Is.Zero, "the landing roll does not count the crouch that let go");

            for (var i = 0; i < 5; i++)
            {
                state = simulation.Step(state, Input());
            }

            Assert.That(LedgeHangStep.IsWaitingToRegrab(state.Parkour), Is.True);
            Assert.That(state.Parkour.Elapsed, Is.EqualTo(5 * _tick).Within(1e-4f));

            for (var i = 0; i < 60; i++)
            {
                state = simulation.Step(state, Input());
                Assert.That(state.Parkour.Move, Is.Not.EqualTo(ParkourMove.LandingRoll), "tick " + i);
            }

            Assert.That(state.IsGrounded, Is.True);
            Assert.That(state.Parkour, Is.EqualTo(CharacterParkourState.None), "the regrab wait is over");
        }

        [Test]
        public void HoldingCrouchWhileFallingNeverGrabs()
        {
            var simulation = NewSimulation(true);

            var after = simulation.Step(Falling(new Vector3(0f, 2.2f, 0.4f)), Input(crouch: true));

            Assert.That(after.Parkour, Is.EqualTo(CharacterParkourState.None));
            Assert.That(simulation.LastLedgeHang.Motion, Is.EqualTo(LedgeHangMotion.None));
        }

        [Test]
        public void RisingPastTheLedgeDoesNotGrab()
        {
            var simulation = NewSimulation(true);
            var rising = new CharacterMotorState(new Vector3(0f, 2.2f, 0.4f), new Vector3(0f, 3f, 0f), false, false);

            var after = simulation.Step(rising, Input());

            Assert.That(after.Parkour, Is.EqualTo(CharacterParkourState.None));
        }

        [Test]
        public void WithTheDefaultTuningFallingPastTheLedgeNeverGrabs()
        {
            Assert.That(LedgeHangTuning.DefaultEnabled, Is.False);
            var simulation = NewSimulation(false);
            var state = Falling(new Vector3(0f, 2.2f, 0.4f));

            for (var i = 0; i < 60; i++)
            {
                state = simulation.Step(state, Input());
                Assert.That(state.Parkour, Is.EqualTo(CharacterParkourState.None), "tick " + i);
                Assert.That(simulation.LastLedgeHang.Motion, Is.EqualTo(LedgeHangMotion.None), "tick " + i);
            }

            Assert.That(state.IsGrounded, Is.True, "it fell to the floor");
            for (var i = 0; i < 30; i++)
            {
                state = simulation.Step(state, Input());
            }

            Assert.That(state.Position.Y, Is.EqualTo(1f + CapsuleMoverSpec.DefaultSkinWidth).Within(0.05f), "standing on the floor, within the skin");
        }

        [Test]
        public void ASnapshotHangWithoutTheLedgeFindsItAgain()
        {
            var simulation = NewSimulation(true);
            var fromSnapshot = new CharacterMotorState(_hangPosition, Vector3.Zero, false, false)
                .WithParkour(new CharacterParkourState(ParkourMove.LedgeHang, 0f, Vector3.Zero, Vector3.Zero));

            var after = simulation.Step(fromSnapshot, Input());

            Assert.That(after.Parkour.Move, Is.EqualTo(ParkourMove.LedgeHang));
            AssertVector(after.Parkour.LedgePoint, _ledgePoint);
            AssertVector(after.Parkour.WallNormal, _wallNormal);
            AssertVector(after.Position, _hangPosition);
        }

        [Test]
        public void ASnapshotHangFacingAwayFromTheLedgeLetsGo()
        {
            var simulation = NewSimulation(true);
            var fromSnapshot = new CharacterMotorState(_hangPosition, Vector3.Zero, false, false)
                .WithParkour(new CharacterParkourState(ParkourMove.LedgeShimmy, 0f, Vector3.Zero, Vector3.Zero));

            var after = simulation.Step(fromSnapshot, Input(yaw: System.MathF.PI));

            Assert.That(after.IsHangingOnLedge, Is.False);
            Assert.That(after.Position.Y, Is.LessThan(_hangPosition.Y), "it falls");
        }

        [Test]
        public void ASnapshotClimbWithoutTheLedgeStartsOverInsteadOfClimbingStraightUp()
        {
            var simulation = NewSimulation(true);
            var fromSnapshot = new CharacterMotorState(_hangPosition + new Vector3(0f, 0.3f, 0f), Vector3.Zero, false, false)
                .WithParkour(new CharacterParkourState(ParkourMove.LedgeClimbUp, 0f, Vector3.Zero, Vector3.Zero));

            var after = simulation.Step(fromSnapshot, Input());

            Assert.That(after.Parkour.Move, Is.Not.EqualTo(ParkourMove.LedgeClimbUp));
        }

        [Test]
        public void TheSimulationGivesItsLedgeSensorTheSameBody()
        {
            var simulation = NewSimulation(true);

            var first = simulation.LedgeSensor;
            var second = simulation.LedgeSensor;

            Assert.That(second, Is.SameAs(first));
            Assert.That(simulation.LedgeSensor.HandHeight, Is.EqualTo(1f));
        }

        private static CharacterMotorState Falling(Vector3 position)
        {
            return new CharacterMotorState(position, new Vector3(0f, -1f, 0f), false, false);
        }

        private static CharacterMotorInput Input(Vector2 move = default, bool jump = false, bool crouch = false, float yaw = 0f)
        {
            return new CharacterMotorInput(move, yaw, false, jump, crouch, false, false, false, false, true);
        }

        private static void AssertVector(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(_tolerance), "x");
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(_tolerance), "y");
            Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(_tolerance), "z");
        }

        private static CharacterMotorSimulation NewSimulation(bool ledgeHang, bool withCeiling = false, bool landingRoll = false)
        {
            var colliders = new System.Collections.Generic.List<WorldCollider>
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, new Vector3(0f, -1f, 0f), new Vector3(50f, 1f, 50f), Quaternion.Identity),
                WorldCollider.Box(2, 0, WorldCollider.StaticGroup, new Vector3(0f, 1.45f, 1.5f), new Vector3(1f, 1.45f, 0.5f), Quaternion.Identity),
            };
            if (withCeiling)
            {
                colliders.Add(WorldCollider.Box(3, 0, WorldCollider.StaticGroup, new Vector3(0f, 4.5f, 1.5f), new Vector3(2f, 0.25f, 2f), Quaternion.Identity));
            }

            var body = new CharacterBody(
                new BvhWorldCollision(new WorldMap(colliders)), CapsuleMoverSpec.Default, new StanceGeometry(2f, 0f, 0.6f, 1.2f, 0.5f));
            var simulation = new CharacterMotorSimulation(body, new PlayerMotor(MotorSpec.Default), _tick, -50f);
            if (ledgeHang)
            {
                simulation.Parkour = new ParkourTuning(
                    landingRoll: landingRoll ? new LandingRollTuning(true) : null, ledgeHang: new LedgeHangTuning(true));
            }

            return simulation;
        }
    }
}

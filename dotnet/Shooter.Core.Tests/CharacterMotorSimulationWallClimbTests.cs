using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // 床の上面は y = 0。体は高さ 2・半径 0.5・中心 0 なので、床に立つと本体は y = 1 にある。
    // 正面の壁は手前の面を z = 0.7 にそろえて置き、立った所 (z = 0) から 0.7 m 先に来るようにする。
    public sealed class CharacterMotorSimulationWallClimbTests
    {
        private const float _tick = 1f / 60f;
        private const float _faceZ = 0.7f;

        private static readonly StanceGeometry _stance = new StanceGeometry(2f, 0f, 0.6f, 1.2f, 0.5f);
        private static readonly Vector3 _wallNormal = new Vector3(0f, 0f, -1f);
        private static readonly ParkourTuning _climbOn = new ParkourTuning(wallClimb: new WallClimbTuning(true));

        private readonly List<WorldCollider> _colliders = new List<WorldCollider>();

        [SetUp]
        public void SetUp()
        {
            _colliders.Clear();
            AddBox(new Vector3(0f, -1f, 0f), new Vector3(50f, 1f, 50f));
        }

        [Test]
        public void JumpingIntoAHighWallStartsTheClimb()
        {
            AddBlock(3f, 3f);
            var simulation = NewSimulation(_climbOn);
            var start = RunningAtTheWall(simulation, 6f);

            var next = simulation.Step(start, Input(jump: true));

            Assert.That(next.Parkour.Move, Is.EqualTo(ParkourMove.WallClimb));
            Assert.That(next.Parkour.Elapsed, Is.EqualTo(_tick));
            AssertClose(next.Parkour.WallNormal, _wallNormal);
            Assert.That(next.Velocity, Is.EqualTo(new Vector3(0f, 5f, 0f)), "straight up at the climb speed");
            Assert.That(next.Position.Y - start.Position.Y, Is.EqualTo(5f * _tick).Within(1e-3f));
            Assert.That(next.Position.Z, Is.EqualTo(start.Position.Z).Within(1e-3f), "the run into the wall does not carry on");
            Assert.That(next.IsGrounded, Is.False);
            Assert.That(next.IsVaulting, Is.False);
        }

        [Test]
        public void WithTheDefaultTuningTheSameJumpIsAPlainJump()
        {
            AddBlock(3f, 3f);
            var simulation = NewSimulation(ParkourTuning.Default);
            var start = RunningAtTheWall(simulation, 6f);

            var next = simulation.Step(start, Input(jump: true));

            Assert.That(next.Parkour, Is.EqualTo(CharacterParkourState.None));
            Assert.That(next.Velocity.Y, Is.GreaterThan(6f), "a ground jump of 1.2 m under 20 m/s^2 leaves at about 6.9 m/s");
        }

        [Test]
        public void ReachingTheLedgeMantlesOntoTheTop()
        {
            AddBlock(3f, 3f);
            var simulation = NewSimulation(_climbOn);
            var state = simulation.Step(RunningAtTheWall(simulation, 6f), Input(jump: true));

            var climbingTicks = 1;
            while (state.Parkour.Move == ParkourMove.WallClimb && climbingTicks < 60)
            {
                state = simulation.Step(state, Input());
                if (state.Parkour.Move == ParkourMove.WallClimb)
                {
                    climbingTicks++;
                }
            }

            // 3 m の上端がよじ登れる 2.2 m まで下りるのに 0.8 m、5 m/s で 1/60 秒ずつ上がると 10 tick 目で届く
            Assert.That(climbingTicks, Is.InRange(9, 11));
            Assert.That(state.Parkour, Is.EqualTo(CharacterParkourState.None));
            Assert.That(state.IsVaulting, Is.True, "the mantle rides on the vault state");
            Assert.That(state.Vault.Elapsed, Is.EqualTo(_tick), "the ledge tick already follows the first step of the path");

            var vaultingTicks = 1;
            while (state.IsVaulting && vaultingTicks < 120)
            {
                state = simulation.Step(state, Input());
                vaultingTicks++;
            }

            Assert.That(state.IsVaulting, Is.False);
            Assert.That(state.Velocity, Is.EqualTo(Vector3.Zero), "the mantle ends without a bounce");
            for (var i = 0; i < 5; i++)
            {
                state = simulation.Step(state, Input());
            }

            Assert.That(state.IsGrounded, Is.True);
            Assert.That(state.Position.Y, Is.EqualTo(3f + 1f + CapsuleMoverSpec.DefaultSkinWidth).Within(0.05f), "standing on the 3 m top");
            Assert.That(state.Position.Z, Is.GreaterThan(_faceZ + 0.4f), "the body is past the face");
        }

        [Test]
        public void TheMantleFollowsThePathAtTheVaultSpeed()
        {
            AddBlock(3f, 3f);
            var simulation = NewSimulation(_climbOn);
            var state = ClimbToTheLedge(simulation);

            var path = state.Vault.ToPath(MotorSpec.DefaultVaultSpeed);
            var ticks = 1;
            while (state.IsVaulting && ticks < 200)
            {
                state = simulation.Step(state, Input());
                ticks++;
            }

            Assert.That(ticks, Is.EqualTo((int)MathF.Ceiling(path.Duration / _tick)).Within(1), "the path's length over 6 m/s, not the 3 m/s of a mantle from the ground");
        }

        [Test]
        public void AFreshSimulationReplaysTheMantleTheSameWay()
        {
            AddBlock(3f, 3f);
            var simulation = NewSimulation(_climbOn);
            var state = ClimbToTheLedge(simulation);
            for (var i = 0; i < 5; i++)
            {
                state = simulation.Step(state, Input());
            }

            Assert.That(state.IsVaulting, Is.True);
            var replay = NewSimulation(_climbOn);
            var a = state;
            var b = state;
            for (var i = 0; i < 60; i++)
            {
                a = simulation.Step(a, Input());
                b = replay.Step(b, Input());
                Assert.That(b, Is.EqualTo(a), "tick " + i);
            }
        }

        [Test]
        public void AWallTallerThanTheClimbEndsInAFall()
        {
            AddBlock(8f, 3f);
            var simulation = NewSimulation(_climbOn);
            var start = RunningAtTheWall(simulation, 6f);
            var state = simulation.Step(start, Input(jump: true));

            var climbingTicks = 1;
            var top = state.Position.Y;
            while (state.Parkour.Move == ParkourMove.WallClimb && climbingTicks < 60)
            {
                state = simulation.Step(state, Input());
                top = Math.Max(top, state.Position.Y);
                if (state.Parkour.Move == ParkourMove.WallClimb)
                {
                    climbingTicks++;
                }
            }

            // 2.5 m を 5 m/s で上がると 0.5 秒 (30 tick)
            Assert.That(climbingTicks, Is.InRange(30, 31));
            Assert.That(top - start.Position.Y, Is.EqualTo(2.5f).Within(0.02f), "it rises as high as the tuning allows");
            Assert.That(state.Parkour, Is.EqualTo(CharacterParkourState.None));
            Assert.That(state.Velocity.Y, Is.EqualTo(-20f * _tick).Within(1e-4f), "it falls from rest, not from the climb speed");
            Assert.That(state.IsVaulting, Is.False);

            for (var i = 0; i < 120 && !state.IsGrounded; i++)
            {
                state = simulation.Step(state, Input());
                Assert.That(state.IsVaulting, Is.False, "tick " + i);
            }

            Assert.That(state.IsGrounded, Is.True);
            Assert.That(state.Position.Y, Is.EqualTo(start.Position.Y).Within(0.05f), "back on the floor");
        }

        [Test]
        public void ALedgeTooNarrowToStandOnEndsInAFall()
        {
            AddBlock(3f, 0.3f);
            var simulation = NewSimulation(_climbOn);
            var state = simulation.Step(RunningAtTheWall(simulation, 6f), Input(jump: true));
            Assert.That(state.Parkour.Move, Is.EqualTo(ParkourMove.WallClimb));

            for (var i = 0; i < 60 && state.Parkour.Move == ParkourMove.WallClimb; i++)
            {
                state = simulation.Step(state, Input());
            }

            Assert.That(state.Parkour, Is.EqualTo(CharacterParkourState.None));
            Assert.That(state.IsVaulting, Is.False, "0.3 m is less than the 1 m a standing body needs");
            Assert.That(state.Velocity.X, Is.Zero);
            Assert.That(state.Velocity.Y, Is.EqualTo(-20f * _tick).Within(1e-4f));
            Assert.That(state.Velocity.Z, Is.Zero);
        }

        [Test]
        public void WithMantleAtLedgeOffAStandableLedgeEndsInAFall()
        {
            AddBlock(3f, 3f);
            var noMantle = new ParkourTuning(wallClimb: new WallClimbTuning(true, mantleAtLedge: false));
            var simulation = NewSimulation(noMantle);
            var state = simulation.Step(RunningAtTheWall(simulation, 6f), Input(jump: true));
            Assert.That(state.Parkour.Move, Is.EqualTo(ParkourMove.WallClimb));

            for (var i = 0; i < 60 && state.Parkour.Move == ParkourMove.WallClimb; i++)
            {
                state = simulation.Step(state, Input());
            }

            Assert.That(state.Parkour, Is.EqualTo(CharacterParkourState.None));
            Assert.That(state.IsVaulting, Is.False, "the 3 m deep top could be stood on, but the tuning says not to mantle");
            Assert.That(state.Velocity.X, Is.Zero);
            Assert.That(state.Velocity.Y, Is.EqualTo(-20f * _tick).Within(1e-4f));
            Assert.That(state.Velocity.Z, Is.Zero);

            for (var i = 0; i < 120 && !state.IsGrounded; i++)
            {
                state = simulation.Step(state, Input());
            }

            Assert.That(state.IsGrounded, Is.True);
            Assert.That(state.Position.Y, Is.LessThan(1.2f), "back on the floor, not on the 3 m top");
        }

        [Test]
        public void JumpingWhileClimbingPushesOffTheWall()
        {
            AddBlock(8f, 3f);
            var motor = new PlayerMotor(MotorSpec.Default);
            var jumps = new List<int>();
            motor.Jumped += jumps.Add;
            var simulation = NewSimulation(_climbOn, motor);
            var state = simulation.Step(RunningAtTheWall(simulation, 6f), Input(jump: true));
            for (var i = 0; i < 5; i++)
            {
                state = simulation.Step(state, Input());
            }

            Assert.That(state.Parkour.Move, Is.EqualTo(ParkourMove.WallClimb));
            Assert.That(jumps, Is.Empty, "starting the climb is not a jump");

            state = simulation.Step(state, Input(jump: true));

            Assert.That(state.Parkour, Is.EqualTo(CharacterParkourState.None));
            Assert.That(state.Velocity.X, Is.Zero);
            Assert.That(state.Velocity.Y, Is.EqualTo(MathF.Sqrt(48f) - (20f * _tick)).Within(1e-3f), "as high as a ground jump");
            Assert.That(state.Velocity.Z, Is.EqualTo(-5f).Within(1e-4f), "away from the wall at the push-off speed");
            Assert.That(jumps, Has.Count.EqualTo(1));
            Assert.That(jumps[0], Is.EqualTo(PlayerMotor.GroundJumpNumber), "a push-off counts as the first jump");
        }

        [Test]
        public void ASlowJumpOrALowWallDoesNotStartTheClimb()
        {
            AddBlock(3f, 3f);
            var simulation = NewSimulation(_climbOn);

            var slow = simulation.Step(RunningAtTheWall(simulation, 2f), Input(jump: true));

            Assert.That(slow.Parkour, Is.EqualTo(CharacterParkourState.None), "2 m/s is below the 4 m/s the climb needs");
            Assert.That(slow.Velocity.Y, Is.GreaterThan(6f), "a plain jump");

            _colliders.RemoveAt(_colliders.Count - 1);
            AddBlock(1.8f, 3f);
            var low = NewSimulation(_climbOn);
            var next = low.Step(RunningAtTheWall(low, 6f), Input(jump: true));

            Assert.That(next.Parkour, Is.EqualTo(CharacterParkourState.None), "a 1.8 m wall is left to the mantle from the ground");
        }

        [Test]
        public void ACrouchedJumpDoesNotStartTheClimb()
        {
            AddBlock(3f, 3f);
            var simulation = NewSimulation(_climbOn);
            var start = RunningAtTheWall(simulation, 6f);
            var crouched = new CharacterMotorState(start.Position, start.Velocity, true, true);

            var next = simulation.Step(crouched, new CharacterMotorInput(Vector2.Zero, 0f, false, true, true, false, false, false, false, false));

            Assert.That(next.Parkour, Is.EqualTo(CharacterParkourState.None));
        }

        private static CharacterMotorState ClimbToTheLedge(CharacterMotorSimulation simulation)
        {
            var state = simulation.Step(RunningAtTheWall(simulation, 6f), Input(jump: true));
            for (var i = 0; i < 60 && !state.IsVaulting; i++)
            {
                state = simulation.Step(state, Input());
            }

            Assert.That(state.IsVaulting, Is.True, "the climb should have reached the ledge");
            return state;
        }

        // 床に下ろしてから、壁へ走り込んでいる速さを持たせる
        private static CharacterMotorState RunningAtTheWall(CharacterMotorSimulation simulation, float speed)
        {
            var state = new CharacterMotorState(new Vector3(0f, 1.05f, 0f), Vector3.Zero, false, false);
            for (var i = 0; i < 30; i++)
            {
                state = simulation.Step(state, Input());
            }

            Assert.That(state.IsGrounded, Is.True, "the runner should stand on the floor");
            return new CharacterMotorState(state.Position, new Vector3(0f, 0f, speed), true, false);
        }

        private static CharacterMotorInput Input(bool jump = false)
        {
            return new CharacterMotorInput(Vector2.Zero, 0f, false, jump, false, false, false, false, false, false);
        }

        private static void AssertClose(Vector3 actual, Vector3 expected)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(1e-4f), $"{actual} should be {expected}");
        }

        private CharacterMotorSimulation NewSimulation(ParkourTuning parkour, PlayerMotor motor = null)
        {
            var body = new CharacterBody(new BvhWorldCollision(new WorldMap(_colliders)), CapsuleMoverSpec.Default, _stance);
            return new CharacterMotorSimulation(body, motor ?? new PlayerMotor(MotorSpec.Default), _tick, -50f)
            {
                SpawnPosition = new Vector3(0f, 1.2f, -5f),
                Parkour = parkour,
            };
        }

        // 手前の面を z = 0.7、下端を床にそろえ、奥へ depth だけ続く台を置く
        private void AddBlock(float top, float depth)
        {
            AddBox(new Vector3(0f, top * 0.5f, _faceZ + (depth * 0.5f)), new Vector3(5f, top * 0.5f, depth * 0.5f));
        }

        private void AddBox(Vector3 center, Vector3 halfExtents)
        {
            _colliders.Add(WorldCollider.Box(_colliders.Count + 1, 0, WorldCollider.StaticGroup, center, halfExtents, Quaternion.Identity));
        }
    }
}

using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class CharacterParkourStateTests
    {
        [Test]
        public void TheEmptyStateDoesNothingAndKeepsTheWholeFallSpeed()
        {
            var none = CharacterParkourState.None;

            Assert.That(none.Move, Is.EqualTo(ParkourMove.None));
            Assert.That(none.IsActive, Is.False);
            Assert.That(none.IsHangingOnLedge, Is.False);
            Assert.That(none.Elapsed, Is.Zero);
            Assert.That(none.WallNormal, Is.EqualTo(Vector3.Zero));
            Assert.That(none.LedgePoint, Is.EqualTo(Vector3.Zero));
            Assert.That(none.FallSpeedScale, Is.EqualTo(1f));
            Assert.That(none.IsValid, Is.True);
            Assert.That(default(CharacterParkourState), Is.EqualTo(new CharacterParkourState(ParkourMove.None, 0f, Vector3.Zero, Vector3.Zero, 1f)));
            Assert.That(default(CharacterParkourState).GetHashCode(), Is.EqualTo(new CharacterParkourState(ParkourMove.None, 0f, Vector3.Zero, Vector3.Zero).GetHashCode()));
        }

        [Test]
        public void HoldsWhatItIsGiven()
        {
            var state = new CharacterParkourState(ParkourMove.LandingRoll, 0.25f, Vector3.UnitX, new Vector3(1f, 2f, 3f), 0.4f);

            Assert.That(state.Move, Is.EqualTo(ParkourMove.LandingRoll));
            Assert.That(state.IsActive, Is.True);
            Assert.That(state.Elapsed, Is.EqualTo(0.25f));
            Assert.That(state.WallNormal, Is.EqualTo(Vector3.UnitX));
            Assert.That(state.LedgePoint, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(state.FallSpeedScale, Is.EqualTo(0.4f));
            Assert.That(state.IsValid, Is.True);
        }

        [TestCase(ParkourMove.LedgeHang, true)]
        [TestCase(ParkourMove.LedgeShimmy, true)]
        [TestCase(ParkourMove.LedgeClimbUp, true)]
        [TestCase(ParkourMove.WallKick, false)]
        [TestCase(ParkourMove.LandingRoll, false)]
        [TestCase(ParkourMove.WallClimb, false)]
        public void OnlyTheLedgeMovesHangOnALedge(ParkourMove move, bool hanging)
        {
            Assert.That(new CharacterParkourState(move, 0f, Vector3.Zero, Vector3.Zero).IsHangingOnLedge, Is.EqualTo(hanging));
        }

        [Test]
        public void EqualsSeesEveryPart()
        {
            var plain = new CharacterParkourState(ParkourMove.WallClimb, 0.1f, Vector3.UnitZ, Vector3.UnitY, 1f);

            Assert.That(new CharacterParkourState(ParkourMove.WallClimb, 0.1f, Vector3.UnitZ, Vector3.UnitY, 1f), Is.EqualTo(plain));
            Assert.That(new CharacterParkourState(ParkourMove.WallKick, 0.1f, Vector3.UnitZ, Vector3.UnitY, 1f), Is.Not.EqualTo(plain));
            Assert.That(new CharacterParkourState(ParkourMove.WallClimb, 0.2f, Vector3.UnitZ, Vector3.UnitY, 1f), Is.Not.EqualTo(plain));
            Assert.That(new CharacterParkourState(ParkourMove.WallClimb, 0.1f, Vector3.UnitX, Vector3.UnitY, 1f), Is.Not.EqualTo(plain));
            Assert.That(new CharacterParkourState(ParkourMove.WallClimb, 0.1f, Vector3.UnitZ, Vector3.UnitX, 1f), Is.Not.EqualTo(plain));
            Assert.That(new CharacterParkourState(ParkourMove.WallClimb, 0.1f, Vector3.UnitZ, Vector3.UnitY, 0.5f), Is.Not.EqualTo(plain));
            Assert.That(plain == new CharacterParkourState(ParkourMove.WallClimb, 0.1f, Vector3.UnitZ, Vector3.UnitY), Is.True);
            Assert.That(plain != CharacterParkourState.None, Is.True);
        }

        [Test]
        public void BrokenValuesAreNotValid()
        {
            Assert.That(new CharacterParkourState((ParkourMove)7, 0f, Vector3.Zero, Vector3.Zero).IsValid, Is.False);
            Assert.That(new CharacterParkourState(ParkourMove.WallKick, -0.1f, Vector3.Zero, Vector3.Zero).IsValid, Is.False);
            Assert.That(new CharacterParkourState(ParkourMove.WallKick, float.NaN, Vector3.Zero, Vector3.Zero).IsValid, Is.False);
            Assert.That(new CharacterParkourState(ParkourMove.WallKick, 0f, new Vector3(float.NaN, 0f, 0f), Vector3.Zero).IsValid, Is.False);
            Assert.That(new CharacterParkourState(ParkourMove.LedgeHang, 0f, Vector3.Zero, new Vector3(0f, float.PositiveInfinity, 0f)).IsValid, Is.False);
            Assert.That(new CharacterParkourState(ParkourMove.LandingRoll, 0f, Vector3.Zero, Vector3.Zero, 1.5f).IsValid, Is.False);
            Assert.That(new CharacterParkourState(ParkourMove.LandingRoll, 0f, Vector3.Zero, Vector3.Zero, -0.1f).IsValid, Is.False);
            Assert.That(new CharacterParkourState(ParkourMove.LandingRoll, 0f, Vector3.Zero, Vector3.Zero, float.NaN).IsValid, Is.False);
            Assert.That(new CharacterParkourState(ParkourMove.LandingRoll, 0f, Vector3.Zero, Vector3.Zero, 0f).IsValid, Is.True);
        }

        [Test]
        public void TheMotorStateStartsWithoutAParkourMove()
        {
            var older = new CharacterMotorState(Vector3.Zero, Vector3.Zero, true, false);

            Assert.That(older.Parkour, Is.EqualTo(CharacterParkourState.None));
            Assert.That(older.Parkour.FallSpeedScale, Is.EqualTo(1f));
            Assert.That(older.IsFinite, Is.True);
        }

        [Test]
        public void TheMotorStateCarriesAndComparesTheParkourState()
        {
            var plain = new CharacterMotorState(Vector3.Zero, Vector3.Zero, false, false);
            var rolling = plain.WithParkour(new CharacterParkourState(ParkourMove.LandingRoll, 0.1f, Vector3.Zero, Vector3.Zero, 0.5f));

            Assert.That(rolling.Parkour.Move, Is.EqualTo(ParkourMove.LandingRoll));
            Assert.That(rolling.Parkour.FallSpeedScale, Is.EqualTo(0.5f));
            Assert.That(rolling, Is.Not.EqualTo(plain));
            Assert.That(rolling.WithParkour(CharacterParkourState.None), Is.EqualTo(plain));
            Assert.That(rolling.GetHashCode(), Is.EqualTo(plain.WithParkour(rolling.Parkour).GetHashCode()));
        }

        [Test]
        public void TheOtherWithMethodsKeepTheParkourState()
        {
            var parkour = new CharacterParkourState(ParkourMove.LedgeHang, 0.3f, -Vector3.UnitZ, new Vector3(0f, 2f, 1f));
            var state = new CharacterMotorState(Vector3.Zero, Vector3.Zero, false, false).WithParkour(parkour);

            Assert.That(state.WithKnockback(CharacterKnockbackState.None.Launch(new Vector3(0f, 5f, 0f))).Parkour, Is.EqualTo(parkour));
            Assert.That(state.WithParachute(CharacterParachuteState.Open(Vector2.Zero)).Parkour, Is.EqualTo(parkour));
            Assert.That(state.WithSlide(CharacterSlideState.None).Parkour, Is.EqualTo(parkour));
        }

        [Test]
        public void ABrokenParkourStateMakesTheMotorStateNotFinite()
        {
            var state = new CharacterMotorState(Vector3.Zero, Vector3.Zero, false, false)
                .WithParkour(new CharacterParkourState(ParkourMove.WallKick, 0f, new Vector3(float.NaN, 0f, 0f), Vector3.Zero));

            Assert.That(state.IsFinite, Is.False);
        }

        [Test]
        public void ADifferentParkourMoveIsAlwaysRolledBack()
        {
            var plain = new CharacterMotorState(Vector3.Zero, Vector3.Zero, false, false);
            var kicking = plain.WithParkour(new CharacterParkourState(ParkourMove.WallKick, 0f, Vector3.UnitX, Vector3.Zero));
            var laterKick = plain.WithParkour(new CharacterParkourState(ParkourMove.WallKick, 0.05f, Vector3.UnitX, Vector3.Zero));

            Assert.That(CharacterMotorState.PositionError(kicking, plain), Is.EqualTo(CharacterMotorState.StanceMismatchError));
            Assert.That(CharacterMotorState.PositionError(plain, kicking), Is.EqualTo(CharacterMotorState.StanceMismatchError));
            Assert.That(CharacterMotorState.PositionError(kicking, laterKick), Is.Zero, "only the move decides; its time is the prediction's own");
            Assert.That(CharacterMotorState.PositionError(plain, plain), Is.Zero);
        }

        [Test]
        public void TheSimulationTakesTheParkourTuning()
        {
            var simulation = NewSimulation();
            var tuning = new ParkourTuning(new WallKickTuning(true));

            Assert.That(simulation.Parkour, Is.SameAs(ParkourTuning.Default));
            simulation.Parkour = tuning;
            Assert.That(simulation.Parkour, Is.SameAs(tuning));
            Assert.Throws<ArgumentNullException>(() => simulation.Parkour = null);
        }

        [Test]
        public void FallingAndJumpingWithTheDefaultTuningLeavesNoParkourMove()
        {
            var simulation = NewSimulation();
            var state = new CharacterMotorState(new Vector3(0f, 4f, 0f), new Vector3(0f, -8f, 0f), false, false);

            for (var i = 0; i < 90; i++)
            {
                state = simulation.Step(state, new CharacterMotorInput(new Vector2(0f, 1f), 0f, true, i < 60 && i % 20 == 0, i % 2 == 0, false, false, false, false, false));
                Assert.That(state.Parkour, Is.EqualTo(CharacterParkourState.None), "tick " + i);
            }

            Assert.That(state.IsGrounded, Is.True, "the fall should land on the floor");
        }

        private static CharacterMotorSimulation NewSimulation()
        {
            var colliders = new[]
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, new Vector3(0f, -1f, 0f), new Vector3(50f, 1f, 50f), Quaternion.Identity),
            };
            var body = new CharacterBody(new BvhWorldCollision(new WorldMap(colliders)), CapsuleMoverSpec.Default, new StanceGeometry(2f, 0f, 0.6f, 1.2f, 0.5f));
            return new CharacterMotorSimulation(body, new PlayerMotor(MotorSpec.Default), 1f / 60f, -50f);
        }
    }
}

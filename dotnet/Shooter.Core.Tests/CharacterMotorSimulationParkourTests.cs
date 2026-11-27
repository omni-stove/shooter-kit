using System.Numerics;
using GameKit.World;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class CharacterMotorSimulationParkourTests
    {
        private const float _tick = 1f / 60f;

        private static readonly ParkourTuning _everyMoveOn = new ParkourTuning(
            new WallKickTuning(true),
            new LandingRollTuning(true),
            new WallClimbTuning(true),
            new LedgeHangTuning(true));

        [Test]
        public void LandingWithTheDefaultTuningKeepsTheWholeFallSpeed()
        {
            var simulation = NewSimulation();
            var state = new CharacterMotorState(new Vector3(0f, 6f, 0f), Vector3.Zero, false, false);

            var landed = false;
            for (var i = 0; i < 120 && !landed; i++)
            {
                var before = state;
                state = simulation.Step(state, Walk(Vector2.Zero));
                if (!before.IsGrounded && state.IsGrounded)
                {
                    landed = true;
                    Assert.That(state.Parkour, Is.EqualTo(CharacterParkourState.None));
                    Assert.That(state.Parkour.FallSpeedScale, Is.EqualTo(1f));
                    Assert.That(-state.Velocity.Y, Is.GreaterThan(MotorSpec.DefaultSafeFallSpeed * 0.5f), "the landing tick still holds the fall speed");
                }
            }

            Assert.That(landed, Is.True);
        }

        // 壁もふちも無い床で、しゃがまずに歩いて跳ぶだけなら、どの動きを入れても始まる所が無い。
        [Test]
        public void OnAnOpenFloorTurningEveryMoveOnMovesExactlyLikeTheDefault()
        {
            var plain = NewSimulation();
            var parkour = NewSimulation();
            parkour.Parkour = _everyMoveOn;
            var start = new CharacterMotorState(new Vector3(0f, 3f, 0f), Vector3.Zero, false, false);
            var a = start;
            var b = start;

            for (var i = 0; i < 180; i++)
            {
                var input = Walk(new Vector2(i % 90 < 45 ? 0.3f : -0.6f, 1f), sprint: i > 60, jump: i % 40 == 0);
                a = plain.Step(a, input);
                b = parkour.Step(b, input);
                Assert.That(b, Is.EqualTo(a), "tick " + i);
                Assert.That(b.Parkour, Is.EqualTo(CharacterParkourState.None), "tick " + i);
            }

            Assert.That(a.Position.Z, Is.GreaterThan(5f), "the walk should have gone somewhere");
        }

        [Test]
        public void BeingKnockedBackEndsTheParkourMove()
        {
            var simulation = NewSimulation();
            simulation.Parkour = _everyMoveOn;
            var kicking = new CharacterMotorState(new Vector3(0f, 2f, 0f), Vector3.Zero, false, false)
                .WithParkour(new CharacterParkourState(ParkourMove.WallKick, 0.1f, Vector3.UnitX, Vector3.Zero));
            var launched = CharacterMotorSimulation.Launch(kicking, new Vector3(0f, 4f, 3f));

            var after = simulation.Step(launched, Walk(Vector2.Zero));

            Assert.That(after.IsKnockedBack, Is.True);
            Assert.That(after.Parkour, Is.EqualTo(CharacterParkourState.None));
        }

        [Test]
        public void FallingBelowTheKillPlaneEndsTheParkourMove()
        {
            var simulation = NewSimulation();
            simulation.SpawnPosition = new Vector3(0f, 1f, 0f);
            var hanging = new CharacterMotorState(new Vector3(100f, -49.99f, 0f), new Vector3(0f, -10f, 0f), false, false)
                .WithParkour(new CharacterParkourState(ParkourMove.LedgeHang, 0.2f, -Vector3.UnitZ, new Vector3(100f, -48f, 0f)));

            var after = simulation.Step(hanging, Walk(Vector2.Zero));

            Assert.That(after.Position, Is.EqualTo(simulation.SpawnPosition));
            Assert.That(after.Parkour, Is.EqualTo(CharacterParkourState.None));
        }

        [Test]
        public void SplittingTheSimulationKeepsTheSlideAndVaultParts()
        {
            var simulation = NewSimulation();
            var slide = new SlideTuning(startSpeed: 12f);

            Assert.That(simulation.Slide, Is.SameAs(SlideTuning.Default));
            simulation.Slide = slide;
            Assert.That(simulation.Slide, Is.SameAs(slide));
            Assert.Throws<System.ArgumentNullException>(() => simulation.Slide = null);
            Assert.That(simulation.VaultSensor, Is.Not.Null);
            Assert.That(simulation.LastSlideEnd, Is.EqualTo(SlideEnd.None));
            Assert.That(simulation.LastSlideStart.IsSliding, Is.False);

            var state = new CharacterMotorState(new Vector3(0f, 3f, 0f), Vector3.Zero, false, false);
            for (var i = 0; i < 120; i++)
            {
                state = simulation.Step(state, Walk(Vector2.Zero));
            }

            Assert.That(state.IsGrounded, Is.True);
            simulation.Step(state, Walk(Vector2.Zero, jump: true));
            Assert.That(simulation.LastVaultProbe.CanVault, Is.False, "nothing to vault over on an open floor");
        }

        private static CharacterMotorInput Walk(Vector2 move, bool sprint = false, bool jump = false)
        {
            return new CharacterMotorInput(move, 0f, sprint, jump, false, false, false, false, false, true);
        }

        private static CharacterMotorSimulation NewSimulation()
        {
            var colliders = new[]
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, new Vector3(0f, -1f, 0f), new Vector3(50f, 1f, 50f), Quaternion.Identity),
            };
            var body = new CharacterBody(
                new BvhWorldCollision(new WorldMap(colliders)), CapsuleMoverSpec.Default, new StanceGeometry(2f, 0f, 0.6f, 1.2f, 0.5f));
            return new CharacterMotorSimulation(body, new PlayerMotor(MotorSpec.Default), _tick, -50f);
        }
    }
}

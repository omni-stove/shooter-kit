using System;
using System.Collections.Generic;
using System.Numerics;
using GameKit.World;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class CharacterMotorMomentumTests
    {
        private const float _tick = 1f / 60f;
        private const int _settleTicks = 30;

        // 床の上面は y=0。体の原点はカプセルの中心で、立った足元はそれより 1 m 下
        private static readonly StanceGeometry _stance = new StanceGeometry(2f, 0f, 0.6f, 1.2f, 0.5f);
        private static readonly Vector3 _start = new Vector3(0f, 1.2f, 0f);

        private readonly List<WorldCollider> _colliders = new List<WorldCollider>();

        [SetUp]
        public void SetUp()
        {
            _colliders.Clear();
        }

        [Test]
        public void TheSlideJumpSpeedAddsTheBoostToTheSlideSpeed()
        {
            Assert.That(MomentumStep.SlideJumpSpeed(9f, MomentumTuning.Default), Is.EqualTo(9f));
            Assert.That(MomentumStep.SlideJumpSpeed(9f, new MomentumTuning(slideJumpBoost: 1.5f)), Is.EqualTo(10.5f));
            Assert.That(MomentumStep.SlideJumpSpeed(0f, new MomentumTuning(slideJumpBoost: 2f)), Is.EqualTo(2f));
        }

        [Test]
        public void TheSlideJumpSpeedRejectsBadValues()
        {
            Assert.Throws<ArgumentNullException>(() => MomentumStep.SlideJumpSpeed(1f, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => MomentumStep.SlideJumpSpeed(-1f, MomentumTuning.Default));
            Assert.Throws<ArgumentOutOfRangeException>(() => MomentumStep.SlideJumpSpeed(float.NaN, MomentumTuning.Default));
            Assert.Throws<ArgumentOutOfRangeException>(() => MomentumStep.SlideJumpSpeed(float.PositiveInfinity, MomentumTuning.Default));
        }

        [Test]
        public void TheVaultExitKeepsAShareOfThePathSpeedAcrossTheObstacle()
        {
            // 0.3 m 先の高さ 0.8 m・奥行き 0.4 m の壁を、半径 0.5 m の体で速さ 6 m/s で越える。降りる区間は真下
            var path = VaultProbe.PlanPath(new Vector3(1f, 0f, 1f), 0.9f, 0.8f, 0.4f, 0.5f, 6f);

            var kept = MomentumStep.VaultExitVelocity(path, new MomentumTuning(vaultExitSpeedKeep: 0.5f));

            var diagonal = 3f / MathF.Sqrt(2f);
            Assert.That(kept.X, Is.EqualTo(diagonal).Within(1e-5f));
            Assert.That(kept.Y, Is.EqualTo(0f), "the drop down the far side is not carried over");
            Assert.That(kept.Z, Is.EqualTo(diagonal).Within(1e-5f));
            Assert.That(kept.Length(), Is.EqualTo(3f).Within(1e-5f));
            Assert.That(MomentumStep.VaultExitVelocity(path, new MomentumTuning(vaultExitSpeedKeep: 1f)).Length(), Is.EqualTo(6f).Within(1e-5f));
        }

        [Test]
        public void TheDefaultVaultExitKeepsNoSpeed()
        {
            var path = VaultProbe.PlanPath(Vector3.UnitZ, 0.9f, 0.8f, 0.4f, 0.5f, 6f);

            Assert.That(MomentumStep.VaultExitVelocity(path, MomentumTuning.Default), Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void AStraightUpPathKeepsNoSpeed()
        {
            var path = new VaultPath(new[] { Vector3.Zero, Vector3.UnitY }, 6f);

            Assert.That(MomentumStep.VaultExitVelocity(path, new MomentumTuning(vaultExitSpeedKeep: 1f)), Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void TheVaultExitRejectsNulls()
        {
            var path = VaultProbe.PlanPath(Vector3.UnitZ, 0.9f, 0.8f, 0.4f, 0.5f, 6f);

            Assert.Throws<ArgumentNullException>(() => MomentumStep.VaultExitVelocity(null, MomentumTuning.Default));
            Assert.Throws<ArgumentNullException>(() => MomentumStep.VaultExitVelocity(path, null));
        }

        [Test]
        public void WithTheDefaultTuningASlideJumpKeepsOnlyTheSlideSpeed()
        {
            AddFloor();
            var simulation = NewSimulation(ParkourTuning.Default);
            var sliding = simulation.Step(Sprint(simulation, Settle(simulation)), SlideInput(sprint: true, crouch: true, slide: true));
            Assert.That(sliding.IsSliding, Is.True);

            var state = simulation.Step(sliding, SlideInput(crouch: true, jump: true));

            Assert.That(simulation.LastSlideEnd, Is.EqualTo(SlideEnd.Jumped));
            Assert.That(state.Velocity.Z, Is.EqualTo(sliding.Slide.Speed).Within(1e-4f));
        }

        [Test]
        public void TheSlideJumpBoostAddsSpeedForward()
        {
            AddFloor();
            var simulation = NewSimulation(new ParkourTuning(momentum: new MomentumTuning(slideJumpBoost: 2f)));
            var sliding = simulation.Step(Sprint(simulation, Settle(simulation)), SlideInput(sprint: true, crouch: true, slide: true));

            var state = simulation.Step(sliding, SlideInput(crouch: true, jump: true));

            var launch = MathF.Sqrt(2f * MotorSpec.DefaultGravity * MotorSpec.DefaultJumpHeight);
            Assert.That(simulation.LastSlideEnd, Is.EqualTo(SlideEnd.Jumped));
            Assert.That(simulation.LastSlideEndSpeed, Is.EqualTo(sliding.Slide.Speed), "the slide still ends at the speed it slid");
            Assert.That(state.Velocity.Z, Is.EqualTo(sliding.Slide.Speed + 2f).Within(1e-4f));
            Assert.That(state.Velocity.X, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(state.Velocity.Y, Is.EqualTo(launch - (MotorSpec.DefaultGravity * _tick)).Within(1e-3f), "the boost does not change the jump height");
            Assert.That(state.Position.Z - sliding.Position.Z, Is.EqualTo((sliding.Slide.Speed + 2f) * _tick).Within(1e-3f));
        }

        [Test]
        public void TheBoostedSlideJumpCarriesFurtherThanTheDefault()
        {
            AddFloor();
            var plain = NewSimulation(ParkourTuning.Default);
            var boosted = NewSimulation(new ParkourTuning(momentum: new MomentumTuning(slideJumpBoost: 3f)));

            var plainLanding = SlideJumpAndLand(plain);
            var boostedLanding = SlideJumpAndLand(boosted);

            // 跳んだ tick の速さの差 3 m/s は、宙で前を押し続けると空中の操作 (AirControl) で走る速さへ寄っていくので、距離の差は 3 m/s × 滞空より小さい
            Assert.That(plainLanding.IsGrounded, Is.True);
            Assert.That(boostedLanding.IsGrounded, Is.True);
            Assert.That(boostedLanding.Position.Z - plainLanding.Position.Z, Is.GreaterThan(0.2f));
        }

        [Test]
        public void WithTheDefaultTuningAVaultEndsAtRest()
        {
            AddFloor();
            AddLowWall();
            var simulation = NewSimulation(ParkourTuning.Default);

            var ended = VaultToTheEnd(simulation, Settle(simulation));

            Assert.That(ended.Velocity, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void TheVaultExitSpeedKeepLeavesTheVaultMovingForward()
        {
            AddFloor();
            AddLowWall();
            var simulation = NewSimulation(new ParkourTuning(momentum: new MomentumTuning(vaultExitSpeedKeep: 0.5f)));

            var ended = VaultToTheEnd(simulation, Settle(simulation));

            Assert.That(ended.IsVaulting, Is.False);
            Assert.That(ended.Velocity.X, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(ended.Velocity.Y, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(ended.Velocity.Z, Is.EqualTo(MotorSpec.DefaultVaultSpeed * 0.5f).Within(1e-5f));
        }

        [Test]
        public void AVaultOffADropCarriesForwardWithTheKeptSpeed()
        {
            AddDropBehindLowWall();
            var plain = NewSimulation(ParkourTuning.Default);
            var kept = NewSimulation(new ParkourTuning(momentum: new MomentumTuning(vaultExitSpeedKeep: 0.5f)));

            var plainEnd = VaultToTheEnd(plain, Settle(plain));
            var keptEnd = VaultToTheEnd(kept, Settle(kept));
            Assert.That(plainEnd.Position, Is.EqualTo(keptEnd.Position), "both follow the same path");
            Assert.That(plainEnd.IsGrounded, Is.False, "the far side is a drop");

            var plainLanding = Fall(plain, plainEnd);
            var keptLanding = Fall(kept, keptEnd);

            // 手を離して落ちると、3 m/s は 1 tick ごとに空中の操作 (0.1) で 0.9 倍になる。足し合わせると 3 × 0.9 / 0.1 / 60 = 0.45 m に近づく
            Assert.That(plainLanding.Position.Z - plainEnd.Position.Z, Is.EqualTo(0f).Within(1e-3f), "without momentum it drops straight down");
            Assert.That(keptLanding.Position.Z - keptEnd.Position.Z, Is.InRange(0.35f, 0.46f));
        }

        [Test]
        public void ACancelledVaultStillStopsWithTheKeptSpeedTuned()
        {
            AddFloor();
            AddLowWall();
            var simulation = NewSimulation(new ParkourTuning(momentum: new MomentumTuning(vaultExitSpeedKeep: 1f)));
            var state = simulation.Step(Settle(simulation), VaultInput(jump: true));
            Assert.That(state.IsVaulting, Is.True);

            var downed = new CharacterMotorInput(Vector2.Zero, 0f, false, false, false, true, false, false, false, false);
            state = simulation.Step(state, downed);

            Assert.That(state.IsVaulting, Is.False);
            Assert.That(state.Velocity.X, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(state.Velocity.Z, Is.EqualTo(0f).Within(1e-5f), "a vault cut short drops from where it stopped");
        }

        [Test]
        public void TheSameInputsGiveTheSameStatesWithMomentumTuned()
        {
            AddFloor();
            AddBox(new Vector3(0f, 0.4f, 6f), new Vector3(2f, 0.4f, 0.2f));
            var tuning = new ParkourTuning(momentum: new MomentumTuning(1.5f, 0.6f));
            var first = NewSimulation(tuning);
            var second = NewSimulation(tuning);
            var a = new CharacterMotorState(_start, Vector3.Zero, false, false);
            var b = a;
            for (var i = 0; i < 300; i++)
            {
                var crouch = i % 100 >= 40 && i % 100 < 60;
                var input = new CharacterMotorInput(
                    new Vector2(0f, 1f), 0f, i % 100 < 50, i % 100 == 50 || i % 100 == 80, crouch, false, false, false, false, true,
                    false, false, false, slide: crouch, slideAllowed: true);
                a = first.Step(a, input);
                b = second.Step(b, input);
                Assert.That(b, Is.EqualTo(a), "tick " + i);
                Assert.That(a.IsFinite, Is.True, "tick " + i);
            }
        }

        private static CharacterMotorState SlideJumpAndLand(CharacterMotorSimulation simulation)
        {
            var sliding = simulation.Step(Sprint(simulation, Settle(simulation)), SlideInput(sprint: true, crouch: true, slide: true));
            var state = simulation.Step(sliding, SlideInput(jump: true));
            Assert.That(state.IsGrounded, Is.False);
            for (var i = 0; i < 120 && !state.IsGrounded; i++)
            {
                state = simulation.Step(state, SlideInput());
            }

            return state;
        }

        private static CharacterMotorState VaultToTheEnd(CharacterMotorSimulation simulation, CharacterMotorState state)
        {
            state = simulation.Step(state, VaultInput(jump: true));
            Assert.That(simulation.LastVaultProbe.CanVault, Is.True, simulation.LastVaultProbe.Verdict.ToString());
            Assert.That(state.IsVaulting, Is.True);
            for (var i = 0; i < 120 && state.IsVaulting; i++)
            {
                state = simulation.Step(state, VaultInput());
            }

            Assert.That(state.IsVaulting, Is.False);
            return state;
        }

        private static CharacterMotorState Fall(CharacterMotorSimulation simulation, CharacterMotorState state)
        {
            for (var i = 0; i < 120 && !state.IsGrounded; i++)
            {
                state = simulation.Step(state, VaultInput());
            }

            Assert.That(state.IsGrounded, Is.True, "the body lands on the lower floor");
            return state;
        }

        private static CharacterMotorState Sprint(CharacterMotorSimulation simulation, CharacterMotorState state)
        {
            for (var i = 0; i < 5; i++)
            {
                state = simulation.Step(state, SlideInput(sprint: true));
            }

            return state;
        }

        private static CharacterMotorState Settle(CharacterMotorSimulation simulation)
        {
            var state = new CharacterMotorState(_start, Vector3.Zero, false, false);
            for (var i = 0; i < _settleTicks; i++)
            {
                state = simulation.Step(state, VaultInput());
            }

            Assert.That(state.IsGrounded, Is.True, "the body should stand on the floor before the test");
            return state;
        }

        private static CharacterMotorInput SlideInput(bool sprint = false, bool crouch = false, bool slide = false, bool jump = false)
        {
            return new CharacterMotorInput(
                new Vector2(0f, 1f), 0f, sprint, jump, crouch, false, false, false, false, false, false, false, false,
                slide: slide, slideAllowed: true);
        }

        // 乗り越えは手を離していても同じ軌跡をたどるので、ふつうの移動に戻ってからの速さが入力で変わらないよう手を離しておく
        private static CharacterMotorInput VaultInput(bool jump = false)
        {
            return new CharacterMotorInput(Vector2.Zero, 0f, false, jump, false, false, false, false, false, true);
        }

        private CharacterMotorSimulation NewSimulation(ParkourTuning parkour)
        {
            var world = new BvhWorldCollision(new WorldMap(_colliders));
            var body = new CharacterBody(world, CapsuleMoverSpec.Default, _stance);
            return new CharacterMotorSimulation(body, new PlayerMotor(MotorSpec.Default), _tick, -50f)
            {
                SpawnPosition = _start,
                Parkour = parkour,
            };
        }

        private void AddFloor()
        {
            AddBox(new Vector3(0f, -1f, 0f), new Vector3(50f, 1f, 50f));
        }

        // 体の前 1.0〜1.4 m に、高さ 0.8 m の乗り越えられる壁を置く
        private void AddLowWall()
        {
            AddBox(new Vector3(0f, 0.4f, 1.2f), new Vector3(2f, 0.4f, 0.2f));
        }

        // 上の床は壁の下で切れ、その先は 3 m 下の床になる
        private void AddDropBehindLowWall()
        {
            AddBox(new Vector3(0f, -1f, -24.4f), new Vector3(50f, 1f, 25.6f));
            AddBox(new Vector3(0f, -4f, 0f), new Vector3(50f, 1f, 50f));
            AddLowWall();
        }

        private void AddBox(Vector3 center, Vector3 halfExtents)
        {
            _colliders.Add(WorldCollider.Box(_colliders.Count + 1, 0, WorldCollider.StaticGroup, center, halfExtents, Quaternion.Identity));
        }
    }
}

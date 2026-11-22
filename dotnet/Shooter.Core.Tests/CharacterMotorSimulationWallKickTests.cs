using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // 床の上面は y = 0。体は高さ 2・半径 0.5・中心の高さ 0 で、重力は 20 (MotorSpec.Default)。
    // 右 (+x) の壁の面は x = 0.7 で、体の中心からの 0.7 は壁を探す距離 (0.5 + 0.3) に届く。
    public sealed class CharacterMotorSimulationWallKickTests
    {
        private const float _tick = 1f / 60f;
        private const float _killPlaneY = -50f;
        private const float _gravity = MotorSpec.DefaultGravity;

        private static readonly StanceGeometry _stance = new StanceGeometry(2f, 0f, 0.6f, 1.2f, 0.5f);
        private static readonly Vector3 _rightWallNormal = new Vector3(-1f, 0f, 0f);
        private static readonly Vector3 _leftWallNormal = new Vector3(1f, 0f, 0f);
        private static readonly Vector3 _beside = new Vector3(0f, 3f, 0f);
        private static readonly Vector3 _falling = new Vector3(0f, -1f, 2f);
        private static readonly WallKickTuning _kickOn = new WallKickTuning(true);

        private readonly List<WorldCollider> _colliders = new List<WorldCollider>();
        private readonly List<int> _jumps = new List<int>();

        [SetUp]
        public void SetUp()
        {
            _colliders.Clear();
            _jumps.Clear();
            AddBox(new Vector3(0f, -1f, 0f), new Vector3(50f, 1f, 50f));
            AddBox(new Vector3(0.95f, 4f, 0f), new Vector3(0.25f, 4f, 10f));
        }

        [Test]
        public void AJumpInTheAirBesideAWallKicksAwayFromItAndUp()
        {
            var simulation = NewSimulation(_kickOn);

            var next = simulation.Step(Airborne(_beside, _falling), Input(jump: true));

            // 壁に沿った 2 は残し、落ちる 1 は捨て、壁から 6 (AwaySpeed) と上へ 6 (UpSpeed) で跳ぶ。縦はこの tick の重力だけ減る
            var upEnd = WallKickTuning.DefaultUpSpeed - (_gravity * _tick);
            Assert.That(next.Velocity.X, Is.EqualTo(-WallKickTuning.DefaultAwaySpeed).Within(1e-4f));
            Assert.That(next.Velocity.Y, Is.EqualTo(upEnd).Within(1e-4f));
            Assert.That(next.Velocity.Z, Is.EqualTo(2f).Within(1e-4f));
            Assert.That(next.Position.X, Is.EqualTo(-WallKickTuning.DefaultAwaySpeed * _tick).Within(1e-3f));
            Assert.That(next.Position.Y, Is.EqualTo(3f + ((WallKickTuning.DefaultUpSpeed + upEnd) * 0.5f * _tick)).Within(1e-3f));
            Assert.That(next.Position.Z, Is.EqualTo(2f * _tick).Within(1e-3f));
            Assert.That(next.IsGrounded, Is.False);
            Assert.That(next.Parkour.Move, Is.EqualTo(ParkourMove.WallKick));
            Assert.That(next.Parkour.Elapsed, Is.Zero);
            AssertNormal(next.Parkour.WallNormal, _rightWallNormal);
            Assert.That(next.AirJumpsUsed, Is.Zero, "a wall kick is not an air jump");
            Assert.That(_jumps, Is.EqualTo(new[] { PlayerMotor.GroundJumpNumber }), "the kick goes out through PlayerMotor.Jumped like a ground jump");
        }

        [Test]
        public void WithTheDefaultTuningAJumpBesideAWallDoesNotKick()
        {
            Assert.That(WallKickTuning.Default.Enabled, Is.False);
            var simulation = NewSimulation(WallKickTuning.Default);

            var next = simulation.Step(Airborne(_beside, _falling), Input(jump: true));

            Assert.That(next.Parkour, Is.EqualTo(CharacterParkourState.None));
            Assert.That(next.Velocity.X, Is.Zero);
            Assert.That(next.Velocity.Y, Is.EqualTo(-1f - (_gravity * _tick)).Within(1e-4f), "it keeps falling");
            Assert.That(_jumps, Is.Empty, "no air jumps by default either");
        }

        [Test]
        public void WithoutAJumpOrAwayFromAWallNothingIsKicked()
        {
            var simulation = NewSimulation(_kickOn);

            var noJump = simulation.Step(Airborne(_beside, _falling), Input());
            var noWall = simulation.Step(Airborne(new Vector3(-5f, 3f, 0f), _falling), Input(jump: true));

            Assert.That(noJump.Parkour, Is.EqualTo(CharacterParkourState.None));
            Assert.That(noJump.Velocity.Y, Is.EqualTo(-1f - (_gravity * _tick)).Within(1e-4f));
            Assert.That(noWall.Parkour, Is.EqualTo(CharacterParkourState.None));
            Assert.That(noWall.Velocity.Y, Is.EqualTo(-1f - (_gravity * _tick)).Within(1e-4f));
            Assert.That(_jumps, Is.Empty);
        }

        [Test]
        public void AJumpOnTheGroundBesideAWallIsAGroundJumpNotAKick()
        {
            var simulation = NewSimulation(_kickOn);
            var standing = new CharacterMotorState(new Vector3(0f, 1f, 0f), Vector3.Zero, true, false);

            var next = simulation.Step(standing, Input(jump: true));

            Assert.That(next.Parkour, Is.EqualTo(CharacterParkourState.None));
            Assert.That(next.Velocity.X, Is.Zero);
            Assert.That(next.Velocity.Y, Is.EqualTo(MathF.Sqrt(2f * _gravity * MotorSpec.DefaultJumpHeight) - (_gravity * _tick)).Within(1e-3f));
            Assert.That(_jumps, Is.EqualTo(new[] { PlayerMotor.GroundJumpNumber }));
        }

        [Test]
        public void WhenTheRulesForbidJumpingTheWallIsNotKicked()
        {
            var simulation = NewSimulation(_kickOn);
            var forbidden = new CharacterMotorInput(
                Vector2.Zero, 0f, false, true, false, false, false, false, false, false, false, false, false, jumpAllowed: false);

            var next = simulation.Step(Airborne(_beside, _falling), forbidden);

            Assert.That(next.Parkour, Is.EqualTo(CharacterParkourState.None));
            Assert.That(_jumps, Is.Empty);
        }

        [Test]
        public void TheSameWallIsNotKickedAgainBeforeLanding()
        {
            var simulation = NewSimulation(_kickOn);
            var kicked = new CharacterParkourState(ParkourMove.WallKick, 0.1f, _rightWallNormal, Vector3.Zero);

            var next = simulation.Step(Airborne(_beside, _falling).WithParkour(kicked), Input(jump: true));

            Assert.That(next.Parkour.Move, Is.EqualTo(ParkourMove.WallKick));
            Assert.That(next.Parkour.Elapsed, Is.EqualTo(0.1f + _tick).Within(1e-6f), "the time in the air goes on");
            Assert.That(next.Parkour.WallNormal, Is.EqualTo(_rightWallNormal));
            Assert.That(next.Velocity.X, Is.Zero);
            Assert.That(next.Velocity.Y, Is.EqualTo(-1f - (_gravity * _tick)).Within(1e-4f));
            Assert.That(_jumps, Is.Empty);
        }

        [Test]
        public void AnotherWallCanBeKickedRightAfterAKick()
        {
            var simulation = NewSimulation(_kickOn);
            var kickedLeft = new CharacterParkourState(ParkourMove.WallKick, 0.3f, _leftWallNormal, Vector3.Zero);

            var next = simulation.Step(Airborne(_beside, _falling).WithParkour(kickedLeft), Input(jump: true));

            Assert.That(next.Parkour.Move, Is.EqualTo(ParkourMove.WallKick));
            Assert.That(next.Parkour.Elapsed, Is.Zero);
            AssertNormal(next.Parkour.WallNormal, _rightWallNormal);
            Assert.That(next.Velocity.X, Is.EqualTo(-WallKickTuning.DefaultAwaySpeed).Within(1e-4f));
            Assert.That(_jumps, Has.Count.EqualTo(1));
        }

        [Test]
        public void TheKickFliesUnderGravityAndLandingEndsIt()
        {
            var simulation = NewSimulation(_kickOn);
            var state = simulation.Step(Airborne(_beside, _falling), Input(jump: true));
            var apex = state.Position.Y;

            var ticks = 0;
            while (!state.IsGrounded)
            {
                Assert.That(ticks++, Is.LessThan(240), "the kick never came down");
                state = simulation.Step(state, Input());
                apex = Math.Max(apex, state.Position.Y);
                if (!state.IsGrounded)
                {
                    Assert.That(state.Parkour.Move, Is.EqualTo(ParkourMove.WallKick), "tick " + ticks);
                }
            }

            // 上へ 6 で跳ぶと 6^2 / (2 * 20) = 0.9 m 上がる
            Assert.That(apex, Is.EqualTo(3f + 0.9f).Within(0.06f));
            // 手を離していると空中の操作 (AirControl 0.1) で横の速さが毎 tick 1 割ずつ落ちるので、離れるのは 1 m ほど
            Assert.That(state.Position.X, Is.LessThan(-0.5f), "it flew away from the wall");

            state = simulation.Step(state, Input());

            Assert.That(state.Parkour, Is.EqualTo(CharacterParkourState.None), "standing on the floor ends the kick");
            Assert.That(_jumps, Has.Count.EqualTo(1));
        }

        [Test]
        public void WhileRunningOnTheWallTheJumpIsTheWallRunsJumpOff()
        {
            var simulation = NewSimulation(_kickOn);
            simulation.WallRun = new WallRunTuning(true);
            var s = Airborne(_beside, new Vector3(0f, 1f, 8f));
            var running = new CharacterMotorState(
                s.Position, s.Velocity, s.IsGrounded, s.Stance, s.Cover, s.Dodge, s.Vault, s.Prone, s.Ladder, s.Knockback,
                s.Lean, s.Swim, new CharacterWallRunState(WallRunSide.Right, 0.2f, _rightWallNormal), 0);

            var next = simulation.Step(running, Input(new Vector2(0f, 1f), jump: true));

            Assert.That(next.IsWallRunning, Is.False);
            Assert.That(next.Parkour, Is.EqualTo(CharacterParkourState.None), "the wall run jumped off, not the kick");
            Assert.That(next.Velocity.X, Is.EqualTo(-WallRunTuning.DefaultJumpOffSpeed).Within(1e-3f));
            Assert.That(_jumps, Has.Count.EqualTo(1));
        }

        [Test]
        public void FlyIntegratesOneTickOfGravityLikeTheMotor()
        {
            var end = WallKickStep.Fly(new Vector3(-6f, 6f, 2f), 20f, 0.1f, out var displacement);

            Assert.That(end.X, Is.EqualTo(-6f));
            Assert.That(end.Y, Is.EqualTo(4f).Within(1e-5f));
            Assert.That(end.Z, Is.EqualTo(2f));
            Assert.That(displacement.X, Is.EqualTo(-0.6f).Within(1e-5f));
            Assert.That(displacement.Y, Is.EqualTo(0.5f).Within(1e-5f), "the average of 6 and 4 over 0.1 s");
            Assert.That(displacement.Z, Is.EqualTo(0.2f).Within(1e-5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => WallKickStep.Fly(Vector3.Zero, -1f, 0.1f, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => WallKickStep.Fly(Vector3.Zero, float.PositiveInfinity, 0.1f, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => WallKickStep.Fly(Vector3.Zero, 20f, 0f, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => WallKickStep.Fly(Vector3.Zero, 20f, float.NaN, out _));
        }

        private static CharacterMotorState Airborne(Vector3 position, Vector3 velocity)
        {
            return new CharacterMotorState(position, velocity, false, false);
        }

        private static CharacterMotorInput Input(bool jump = false)
        {
            return Input(Vector2.Zero, jump);
        }

        private static CharacterMotorInput Input(Vector2 move, bool jump = false)
        {
            return new CharacterMotorInput(move, 0f, false, jump, false, false, false, false, false, false);
        }

        private static void AssertNormal(Vector3 actual, Vector3 expected)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(1e-4f), $"normal {actual} should be {expected}");
        }

        private CharacterMotorSimulation NewSimulation(WallKickTuning kick)
        {
            var motor = new PlayerMotor(MotorSpec.Default);
            motor.Jumped += _jumps.Add;
            var body = new CharacterBody(new BvhWorldCollision(new WorldMap(_colliders)), CapsuleMoverSpec.Default, _stance);
            return new CharacterMotorSimulation(body, motor, _tick, _killPlaneY)
            {
                Parkour = new ParkourTuning(kick),
            };
        }

        private void AddBox(Vector3 center, Vector3 halfExtents)
        {
            _colliders.Add(WorldCollider.Box(_colliders.Count + 1, 0, WorldCollider.StaticGroup, center, halfExtents, Quaternion.Identity));
        }
    }
}

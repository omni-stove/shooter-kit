using System.Text.RegularExpressions;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // 床の上面は y = 0。体は高さ 2・半径 0.5・中心 0 で、重力は 20・空中の操作は 0.1・歩く速さは 5。
    // 右 (+x) の壁の面は x = 0.7 で、体の中心からの 0.7 は壁を探す距離 (0.5 + CharacterWallSensor.ReachBeyondRadius) に届く。
    public sealed class PlayerControllerWallKickTests
    {
        private const float _tickSeconds = 0.02f;
        private const float _gravity = 20f;
        private const int _maxTicks = 1000;

        private static readonly ParkourTuning _kickOn = new ParkourTuning(new WallKickTuning(true));
        private static readonly Vector2 _right = new Vector2(1f, 0f);

        private GameObject _floor;
        private GameObject _wall;
        private GameObject _body;
        private PlayerController _player;
        private InputAction _move;
        private InputAction _sprint;
        private InputAction _crouch;
        private InputAction _jump;

        [SetUp]
        public void SetUp()
        {
            _floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _floor.transform.position = new Vector3(0f, -0.5f, 0f);
            _floor.transform.localScale = new Vector3(40f, 1f, 40f);
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.transform.position = new Vector3(0.8f, 4f, 0f);
            _wall.transform.localScale = new Vector3(0.2f, 8f, 30f);

            _body = new GameObject("Runner");
            var controller = _body.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.center = Vector3.zero;
            _player = _body.AddComponent<PlayerController>();
            _body.AddComponent<Damageable>().Initialize("Runner", 100);
            _move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            _sprint = new InputAction("Sprint", InputActionType.Button);
            _crouch = new InputAction("Crouch", InputActionType.Button);
            _jump = new InputAction("Jump", InputActionType.Button);
            var look = _body.AddComponent<FirstPersonLook>();
            _player.Initialize(controller, look, _move, _sprint, _crouch, _jump, new MotorSpec(5f, 8f, 1.2f, _gravity, 0.1f, 2.5f));
            _player.Water = NoWaterVolumes.Instance;
            _player.WallRunTuning = WallRunTuning.Default;
            _player.ParkourTuning = _kickOn;
        }

        [TearDown]
        public void TearDown()
        {
            _move.Dispose();
            _sprint.Dispose();
            _crouch.Dispose();
            _jump.Dispose();
            Object.DestroyImmediate(_body);
            Object.DestroyImmediate(_wall);
            Object.DestroyImmediate(_floor);
        }

        [Test]
        public void AJumpInTheAirBesideAWallKicksAwayFromItAndUp()
        {
            FallBesideTheWall();
            LogAssert.Expect(
                LogType.Log, new Regex(@"^\[WallKick\] kicked side=Right normal=\(-1\.00, -?0\.00, -?0\.00\) velocity=\(-6\.00, 6\.00, -?0\.00\)$"));

            _player.Tick(Vector2.zero, false, false, true, _tickSeconds);

            Assert.That(_player.Parkour.Move, Is.EqualTo(ParkourMove.WallKick));
            Assert.That(_player.Parkour.Elapsed, Is.Zero);
            Assert.That(_player.Parkour.WallNormal.X, Is.EqualTo(-1f).Within(1e-3f), "the wall faces the runner (-x)");
            Assert.That(_player.Motor.Velocity.X, Is.EqualTo(-WallKickTuning.DefaultAwaySpeed).Within(1e-3f));
            Assert.That(_player.Motor.Velocity.Y, Is.EqualTo(WallKickTuning.DefaultUpSpeed - (_gravity * _tickSeconds)).Within(1e-3f));
            Assert.That(_player.Motor.Velocity.Z, Is.Zero.Within(1e-3f));
            Assert.That(_body.transform.position.x, Is.EqualTo(-WallKickTuning.DefaultAwaySpeed * _tickSeconds).Within(0.01f));
            Assert.That(_player.IsGrounded, Is.False);
            Assert.That(_player.AirJumpsUsed, Is.Zero, "a wall kick is not an air jump");
            Assert.That(_player.TryGetMotorState(out var state), Is.True);
            Assert.That(state.Parkour.Move, Is.EqualTo(ParkourMove.WallKick), "the pose reads the kick from the motor state");
        }

        [Test]
        public void TheKickIsOffUnlessTheTuningTurnsItOn()
        {
            Assert.That(WallKickTuning.Default.Enabled, Is.False);
            _player.ParkourTuning = ParkourTuning.Default;
            FallBesideTheWall();

            _player.Tick(Vector2.zero, false, false, true, _tickSeconds);

            Assert.That(_player.Parkour, Is.EqualTo(CharacterParkourState.None));
            Assert.That(_player.Motor.Velocity.X, Is.Zero.Within(1e-3f));
            Assert.That(_player.Motor.Velocity.Y, Is.LessThan(0f), "it keeps falling");
        }

        [Test]
        public void WhenTheJumpRuleIsOffTheWallIsNotKicked()
        {
            _player.JumpRule = Jump.Off;
            FallBesideTheWall();

            _player.Tick(Vector2.zero, false, false, true, _tickSeconds);

            Assert.That(_player.Parkour, Is.EqualTo(CharacterParkourState.None));
            Assert.That(_player.Motor.Velocity.X, Is.Zero.Within(1e-3f));
        }

        [Test]
        public void AJumpOnTheGroundBesideAWallIsAGroundJump()
        {
            _body.transform.position = new Vector3(0f, 1f, 0f);
            Physics.SyncTransforms();
            Settle();

            _player.Tick(Vector2.zero, false, false, true, _tickSeconds);

            Assert.That(_player.Parkour, Is.EqualTo(CharacterParkourState.None));
            Assert.That(_player.Motor.Velocity.X, Is.Zero.Within(1e-3f));
            Assert.That(_player.Motor.Velocity.Y, Is.EqualTo(Mathf.Sqrt(2f * _gravity * 1.2f) - (_gravity * _tickSeconds)).Within(1e-3f));
        }

        [Test]
        public void TheSameWallIsNotKickedAgainBeforeLanding()
        {
            FallBesideTheWall();
            LogAssert.Expect(LogType.Log, new Regex(@"^\[WallKick\] kicked "));
            _player.Tick(Vector2.zero, false, false, true, _tickSeconds);

            // 空中の操作で右へ寄せ直し、壁にもう一度届く所まで戻る (0.5 s のうちに 0.2 m ほど戻れば届く)
            for (var i = 0; i < 25; i++)
            {
                _player.Tick(_right, false, false, false, _tickSeconds);
            }

            Assert.That(_player.IsGrounded, Is.False, "still in the air");
            Assert.That(_body.transform.position.x, Is.GreaterThanOrEqualTo(-0.1f), "back within reach of the wall");
            var elapsed = _player.Parkour.Elapsed;
            var vertical = _player.Motor.Velocity.Y;
            LogAssert.Expect(LogType.Log, new Regex(@"^\[WallKick\] sameWall side=Right normal=\(-1\.00, -?0\.00, -?0\.00\)$"));

            _player.Tick(_right, false, false, true, _tickSeconds);

            Assert.That(_player.Parkour.Move, Is.EqualTo(ParkourMove.WallKick));
            Assert.That(_player.Parkour.Elapsed, Is.EqualTo(elapsed + _tickSeconds).Within(1e-5f), "the time in the air goes on");
            Assert.That(_player.Motor.Velocity.Y, Is.EqualTo(vertical - (_gravity * _tickSeconds)).Within(1e-3f), "no second kick");
        }

        [Test]
        public void LandingEndsTheKick()
        {
            FallBesideTheWall();
            LogAssert.Expect(LogType.Log, new Regex(@"^\[WallKick\] kicked "));
            _player.Tick(Vector2.zero, false, false, true, _tickSeconds);

            var ticks = 0;
            while (!_player.IsGrounded)
            {
                Assert.That(ticks++, Is.LessThan(_maxTicks), "the runner never landed");
                Assert.That(_player.Parkour.Move, Is.EqualTo(ParkourMove.WallKick), "tick " + ticks);
                _player.Tick(Vector2.zero, false, false, false, _tickSeconds);
            }

            _player.Tick(Vector2.zero, false, false, false, _tickSeconds);

            Assert.That(_player.Parkour, Is.EqualTo(CharacterParkourState.None));
        }

        // 置いた直後の CharacterController は接地していないので、1 tick 落としてから跳ぶ。
        private void FallBesideTheWall()
        {
            _body.transform.position = new Vector3(0f, 4f, 0f);
            Physics.SyncTransforms();
            _player.Tick(Vector2.zero, false, false, false, _tickSeconds);
            Assert.That(_player.IsGrounded, Is.False, "in the air beside the wall");
            Assert.That(_player.Parkour, Is.EqualTo(CharacterParkourState.None));
        }

        private void Settle()
        {
            for (var i = 0; i < _maxTicks; i++)
            {
                _player.Tick(Vector2.zero, false, false, false, _tickSeconds);
                if (_player.IsGrounded && _player.Motor.Velocity.Y <= 0f)
                {
                    return;
                }
            }

            Assert.Fail("the runner never landed");
        }
    }
}

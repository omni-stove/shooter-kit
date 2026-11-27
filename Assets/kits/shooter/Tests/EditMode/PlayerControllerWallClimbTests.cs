using System.Collections.Generic;
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
    // 床の上面は y = 0。体は高さ 2・半径 0.5・中心 0 で、重力は 20。
    // 正面 (+z) の壁は手前の面を z = 2 にそろえて置き、z = 0 から走り込んで面に押し付けてから跳ぶ。
    public sealed class PlayerControllerWallClimbTests
    {
        private const float _tickSeconds = 0.02f;
        private const float _faceZ = 2f;

        private static readonly ParkourTuning _climbOn = new ParkourTuning(wallClimb: new WallClimbTuning(true));

        private readonly List<GameObject> _walls = new List<GameObject>();
        private GameObject _floor;
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

            _body = new GameObject("Climber");
            var controller = _body.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.center = Vector3.zero;
            _player = _body.AddComponent<PlayerController>();
            _body.AddComponent<Damageable>().Initialize("Climber", 100);
            _move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            _sprint = new InputAction("Sprint", InputActionType.Button);
            _crouch = new InputAction("Crouch", InputActionType.Button);
            _jump = new InputAction("Jump", InputActionType.Button);
            var look = _body.AddComponent<FirstPersonLook>();
            _player.Initialize(controller, look, _move, _sprint, _crouch, _jump, new MotorSpec(5f, 8f, 1.2f, 20f, 0.1f, 2.5f));
            _player.Water = NoWaterVolumes.Instance;
            _player.WallRunTuning = WallRunTuning.Default;
            _player.ParkourTuning = _climbOn;
        }

        [TearDown]
        public void TearDown()
        {
            _move.Dispose();
            _sprint.Dispose();
            _crouch.Dispose();
            _jump.Dispose();
            foreach (var wall in _walls)
            {
                Object.DestroyImmediate(wall);
            }

            _walls.Clear();
            Object.DestroyImmediate(_body);
            Object.DestroyImmediate(_floor);
        }

        [Test]
        public void JumpingIntoAHighWallClimbsItAndLogsTheStart()
        {
            AddBlock(3f, 3f);
            var before = RunIntoTheWall();

            LogAssert.Expect(LogType.Log, new Regex("^" + Regex.Escape(WallClimbLog.StartPrefix) + @"speed=8\.00 top="));
            JumpAtTheWall();

            Assert.That(_player.IsWallClimbing, Is.True);
            Assert.That(_player.Parkour.Elapsed, Is.EqualTo(_tickSeconds));
            Assert.That(_player.Parkour.WallNormal.Z, Is.EqualTo(-1f).Within(1e-4f));
            Assert.That(_body.transform.position.y - before.y, Is.EqualTo(5f * _tickSeconds).Within(1e-3f), "straight up at the climb speed");
            Assert.That(_body.transform.position.z, Is.EqualTo(before.z).Within(1e-3f));
            Assert.That(_player.TryGetMotorState(out var state), Is.True);
            Assert.That(state.Parkour.Move, Is.EqualTo(ParkourMove.WallClimb));
            Assert.That(state.Velocity.Y, Is.EqualTo(5f));
        }

        [Test]
        public void ReachingTheLedgeMantlesOntoTheTopEvenWithTheMantleRuleOff()
        {
            AddBlock(3f, 3f);
            RunIntoTheWall();
            Assert.That(_player.MantleRule, Is.EqualTo(Mantle.Off));

            LogAssert.Expect(LogType.Log, new Regex("^" + Regex.Escape(WallClimbLog.StartPrefix)));
            LogAssert.Expect(LogType.Log, new Regex("^" + Regex.Escape(WallClimbLog.EndPrefixFor(WallClimbMotion.ReachedLedge))));
            JumpAtTheWall();

            var climbingTicks = 1;
            for (var i = 0; i < 60 && _player.IsWallClimbing; i++)
            {
                Idle();
                if (_player.IsWallClimbing)
                {
                    climbingTicks++;
                }
            }

            // 上端までの 2.9 m ほどがよじ登れる 2.2 m まで下りるのに 0.7〜0.8 m、0.1 m ずつ上がるので 8 tick ほどで届く
            Assert.That(climbingTicks, Is.InRange(6, 9));
            Assert.That(_player.IsMantling, Is.True);
            Assert.That(_player.MantlePath.Speed, Is.EqualTo(MotorSpec.DefaultVaultSpeed), "the same speed as the prediction and the server");
            Assert.That(_player.Parkour, Is.EqualTo(CharacterParkourState.None));

            for (var i = 0; i < 120 && _player.IsMantling; i++)
            {
                Idle();
            }

            Assert.That(_player.IsMantling, Is.False);
            for (var i = 0; i < 5; i++)
            {
                Idle();
            }

            Assert.That(_player.IsGrounded, Is.True);
            Assert.That(_body.transform.position.y, Is.EqualTo(4f).Within(0.15f), "standing on the 3 m top");
            Assert.That(_body.transform.position.z, Is.GreaterThan(_faceZ + 0.4f), "the body is past the face");
        }

        [Test]
        public void WithMantleAtLedgeOffReachingTheLedgeEndsInAFallLikeTheSimulation()
        {
            AddBlock(3f, 3f);
            _player.ParkourTuning = new ParkourTuning(wallClimb: new WallClimbTuning(true, mantleAtLedge: false));
            var start = RunIntoTheWall();

            LogAssert.Expect(LogType.Log, new Regex("^" + Regex.Escape(WallClimbLog.StartPrefix)));
            LogAssert.Expect(LogType.Log, new Regex("^" + Regex.Escape(WallClimbLog.EndPrefixFor(WallClimbMotion.ReachedLedge))));
            JumpAtTheWall();
            for (var i = 0; i < 60 && _player.IsWallClimbing; i++)
            {
                Idle();
            }

            Assert.That(_player.IsWallClimbing, Is.False);
            Assert.That(_player.IsMantling, Is.False, "the 3 m deep top could be stood on, but the tuning says not to mantle");
            Assert.That(_player.TryGetMotorState(out var state), Is.True);
            Assert.That(state.Velocity.X, Is.Zero);
            Assert.That(state.Velocity.Y, Is.EqualTo(-20f * _tickSeconds).Within(1e-4f), "it falls from rest");
            Assert.That(state.Velocity.Z, Is.Zero);

            for (var i = 0; i < 120 && !_player.IsGrounded; i++)
            {
                Idle();
                Assert.That(_player.IsMantling, Is.False, "tick " + i);
            }

            Assert.That(_player.IsGrounded, Is.True);
            Assert.That(_body.transform.position.y, Is.EqualTo(start.y).Within(0.05f), "back on the floor");
        }

        [Test]
        public void AWallTallerThanTheClimbEndsInAFall()
        {
            AddBlock(8f, 3f);
            var start = RunIntoTheWall();

            // 0.02 秒を 25 回足すと 0.5 にわずかに届かないことがあり、そのときは残りの高さだけ上がる 26 tick 目の後に落ちる
            LogAssert.Expect(LogType.Log, new Regex("^" + Regex.Escape(WallClimbLog.EndPrefixFor(WallClimbMotion.Fell)) + @"seconds=0\.5[02] height=2\.50$"));
            JumpAtTheWall();
            var top = _body.transform.position.y;
            for (var i = 0; i < 60 && _player.IsWallClimbing; i++)
            {
                Idle();
                top = Mathf.Max(top, _body.transform.position.y);
            }

            Assert.That(_player.IsWallClimbing, Is.False);
            Assert.That(top - start.y, Is.EqualTo(2.5f).Within(0.05f), "as high as the tuning allows");
            Assert.That(_player.TryGetMotorState(out var state), Is.True);
            Assert.That(state.Velocity.Y, Is.EqualTo(-20f * _tickSeconds).Within(1e-4f), "it falls from rest");

            for (var i = 0; i < 120 && !_player.IsGrounded; i++)
            {
                Idle();
                Assert.That(_player.IsMantling, Is.False, "tick " + i);
            }

            Assert.That(_player.IsGrounded, Is.True);
            Assert.That(_body.transform.position.y, Is.EqualTo(start.y).Within(0.05f), "back on the floor");
        }

        [Test]
        public void JumpingWhileClimbingPushesOffTheWall()
        {
            AddBlock(8f, 3f);
            RunIntoTheWall();
            JumpAtTheWall();
            for (var i = 0; i < 4; i++)
            {
                Idle();
            }

            Assert.That(_player.IsWallClimbing, Is.True);
            LogAssert.Expect(LogType.Log, new Regex("^" + Regex.Escape(WallClimbLog.EndPrefixFor(WallClimbMotion.PushedOff))));
            _player.Tick(Vector2.zero, false, false, true, _tickSeconds);

            Assert.That(_player.IsWallClimbing, Is.False);
            Assert.That(_player.TryGetMotorState(out var state), Is.True);
            Assert.That(state.Velocity.Z, Is.EqualTo(-5f).Within(1e-4f), "away from the wall at the push-off speed");
            Assert.That(state.Velocity.Y, Is.EqualTo(Mathf.Sqrt(48f) - (20f * _tickSeconds)).Within(1e-3f), "as fast up as a ground jump of 1.2 m");
        }

        [Test]
        public void PushingOffUnderAnOverhangDropsTheUpwardSpeedLikeTheSimulation()
        {
            AddBlock(8f, 3f);
            RunIntoTheWall();

            // 立った頭の上端は y = 2。1 tick 登って 2.1 になった後、跳び離れの 1 tick (約 0.14 m) で下面 2.2 の張り出しに当たる
            var overhang = GameObject.CreatePrimitive(PrimitiveType.Cube);
            overhang.transform.position = new Vector3(0f, 2.7f, 0.5f);
            overhang.transform.localScale = new Vector3(10f, 1f, 3f);
            _walls.Add(overhang);
            Physics.SyncTransforms();

            JumpAtTheWall();
            Assert.That(_player.IsWallClimbing, Is.True);
            LogAssert.Expect(LogType.Log, new Regex("^" + Regex.Escape(WallClimbLog.EndPrefixFor(WallClimbMotion.PushedOff))));
            _player.Tick(Vector2.zero, false, false, true, _tickSeconds);

            Assert.That(_player.IsWallClimbing, Is.False);
            Assert.That(_player.TryGetMotorState(out var state), Is.True);
            Assert.That(state.Velocity.Y, Is.EqualTo(0f), "the head hit the overhang");
            Assert.That(state.Velocity.Z, Is.EqualTo(-5f).Within(1e-4f), "still away from the wall");
        }

        [Test]
        public void WithTheDefaultTuningTheSameJumpIsAPlainJump()
        {
            _player.ParkourTuning = ParkourTuning.Default;
            AddBlock(3f, 3f);
            RunIntoTheWall();

            JumpAtTheWall();

            Assert.That(_player.IsWallClimbing, Is.False);
            Assert.That(_player.Parkour, Is.EqualTo(CharacterParkourState.None));
            Assert.That(_player.TryGetMotorState(out var state), Is.True);
            Assert.That(state.Velocity.Y, Is.GreaterThan(5f), "a ground jump");
        }

        [Test]
        public void ALowWallOrACrouchedJumpDoesNotStartTheClimb()
        {
            AddBlock(1.8f, 3f);
            RunIntoTheWall();
            JumpAtTheWall();

            Assert.That(_player.IsWallClimbing, Is.False, "a 1.8 m wall is left to the mantle from the ground");

            Object.DestroyImmediate(_walls[0]);
            _walls.Clear();
            AddBlock(3f, 3f);
            RunIntoTheWall();
            _player.Tick(Vector2.up, false, true, false, _tickSeconds);
            _player.Tick(Vector2.up, false, true, true, _tickSeconds);

            Assert.That(_player.IsWallClimbing, Is.False, "a crouched runner does not climb");
        }

        // 面に押し付けた所で止まるまで走らせる。PlayerMotor は地上の速さを入力どおりに置くので、止まっても走る速さは 8 のまま。
        private Vector3 RunIntoTheWall()
        {
            _body.transform.position = new Vector3(0f, 1f, 0f);
            Physics.SyncTransforms();
            for (var i = 0; i < 10; i++)
            {
                Idle();
            }

            for (var i = 0; i < 40; i++)
            {
                _player.Tick(Vector2.up, true, false, false, _tickSeconds);
            }

            Assert.That(_player.IsGrounded, Is.True);
            Assert.That(_body.transform.position.z, Is.GreaterThan(_faceZ - 0.7f), "pressed against the face");
            return _body.transform.position;
        }

        private void JumpAtTheWall()
        {
            _player.Tick(Vector2.up, true, false, true, _tickSeconds);
        }

        private void Idle()
        {
            _player.Tick(Vector2.zero, false, false, false, _tickSeconds);
        }

        // 手前の面を z = 2、下端を床にそろえ、奥へ depth だけ続く台を置く
        private void AddBlock(float top, float depth)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, top * 0.5f, _faceZ + (depth * 0.5f));
            wall.transform.localScale = new Vector3(10f, top, depth);
            _walls.Add(wall);
            Physics.SyncTransforms();
        }
    }
}

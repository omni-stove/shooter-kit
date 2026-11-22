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
    // 床の上面は y = 0。台は上端が y = 2.9、手前の面が z = 1、幅が x = -1 から 1 まで。
    // 体は高さ 2・半径 0.5・中心 0 なので、手 (立った形の上端) は位置の 1 m 上、ぶら下がる位置は (0, 1.9, 0.4)。
    public sealed class PlayerControllerLedgeHangTests
    {
        private const float _tickSeconds = 0.02f;
        private const float _tolerance = 0.02f;

        private static readonly Vector3 _hangPosition = new Vector3(0f, 1.9f, 0.4f);
        private static readonly Vector3 _fallStart = new Vector3(0f, 2.2f, 0.4f);
        private static readonly ParkourTuning _ledgeOn = new ParkourTuning(ledgeHang: new LedgeHangTuning(true));

        private GameObject _floor;
        private GameObject _ledge;
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

            _ledge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ledge.transform.position = new Vector3(0f, 1.45f, 1.5f);
            _ledge.transform.localScale = new Vector3(2f, 2.9f, 1f);

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
            _player.ParkourTuning = _ledgeOn;
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            _move.Dispose();
            _sprint.Dispose();
            _crouch.Dispose();
            _jump.Dispose();
            Object.DestroyImmediate(_body);
            Object.DestroyImmediate(_ledge);
            Object.DestroyImmediate(_floor);
        }

        [Test]
        public void FallingPastTheLedgeGrabsItLogsAndHangsBelowIt()
        {
            LogAssert.Expect(LogType.Log, new Regex("^" + Regex.Escape(LedgeHangLog.GrabPrefix)));

            DropNextToTheLedge();

            Assert.That(_player.IsHangingOnLedge, Is.True);
            Assert.That(_player.Parkour.Move, Is.EqualTo(ParkourMove.LedgeHang));
            Assert.That(_player.Parkour.LedgePoint.Y, Is.EqualTo(2.9f).Within(_tolerance));
            Assert.That(_player.Parkour.LedgePoint.Z, Is.EqualTo(1f).Within(_tolerance));
            Assert.That(_player.Parkour.WallNormal.Z, Is.EqualTo(-1f).Within(_tolerance));
            Assert.That(_player.LastLedgeHang.Motion, Is.EqualTo(LedgeHangMotion.Grabbed));
            AssertNear(_body.transform.position, _hangPosition);
            Assert.That(_player.IsGrounded, Is.False);
        }

        [Test]
        public void HangingHoldsThePlaceAndBlocksAiming()
        {
            DropNextToTheLedge();

            for (var i = 0; i < 50; i++)
            {
                Tick();
                Assert.That(_player.Parkour.Move, Is.EqualTo(ParkourMove.LedgeHang), "tick " + i);
            }

            AssertNear(_body.transform.position, _hangPosition);
            Assert.That(_player.AirJumpsUsed, Is.Zero);
            Assert.That(_player.TryGetMotorState(out var state), Is.True);
            Assert.That(state.IsHangingOnLedge, Is.True);
            Assert.That(state.CanAimDownSights, Is.False);
        }

        [Test]
        public void MovingRightShimmiesAndStopsAtTheEndWithAnEdgeLine()
        {
            DropNextToTheLedge();
            LogAssert.Expect(LogType.Log, new Regex("^" + Regex.Escape(LedgeHangLog.EdgePrefix)));

            Tick(new Vector2(1f, 0f));
            Assert.That(_player.Parkour.Move, Is.EqualTo(ParkourMove.LedgeShimmy));
            Assert.That(_body.transform.position.x, Is.EqualTo(LedgeHangTuning.DefaultShimmySpeed * _tickSeconds).Within(_tolerance));

            for (var i = 0; i < 50; i++)
            {
                Tick(new Vector2(1f, 0f));
            }

            Assert.That(_player.Parkour.Move, Is.EqualTo(ParkourMove.LedgeHang));
            Assert.That(_player.LastLedgeHang.Motion, Is.EqualTo(LedgeHangMotion.StoppedAtEdge));
            Assert.That(_body.transform.position.x, Is.GreaterThan(0.4f).And.LessThan(0.5f), "the body stops before its side passes x = 1");
            Assert.That(_body.transform.position.y, Is.EqualTo(_hangPosition.y).Within(_tolerance));
        }

        [Test]
        public void JumpingClimbsOntoTheTopWithClimbAndTopLines()
        {
            DropNextToTheLedge();
            LogAssert.Expect(LogType.Log, new Regex("^" + Regex.Escape(LedgeHangLog.ClimbPrefix)));
            LogAssert.Expect(LogType.Log, new Regex("^" + Regex.Escape(LedgeHangLog.TopPrefix)));

            Tick(jump: true);
            Assert.That(_player.Parkour.Move, Is.EqualTo(ParkourMove.LedgeClimbUp));
            Assert.That(_player.IsHangingOnLedge, Is.True);

            var climbedUp = false;
            for (var i = 0; i < 200 && !climbedUp; i++)
            {
                Tick();
                climbedUp = _player.LastLedgeHang.Motion == LedgeHangMotion.ClimbedUp;
            }

            Assert.That(climbedUp, Is.True);
            Assert.That(_player.Parkour, Is.EqualTo(CharacterParkourState.None));

            for (var i = 0; i < 20; i++)
            {
                Tick();
            }

            Assert.That(_player.IsGrounded, Is.True);
            Assert.That(_body.transform.position.y, Is.EqualTo(3.9f).Within(0.1f), "standing on the top at 2.9");
            Assert.That(_body.transform.position.z, Is.GreaterThan(1f));
        }

        [Test]
        public void CrouchingLetsGoWithALetGoLineAndFallsToTheFloor()
        {
            DropNextToTheLedge();
            LogAssert.Expect(LogType.Log, LedgeHangLog.LetGo(LedgeLetGoReason.Dropped, _player.Parkour.LedgePoint));

            Tick(crouch: true);

            Assert.That(_player.IsHangingOnLedge, Is.False);
            Assert.That(_player.LastLedgeHang.LetGoReason, Is.EqualTo(LedgeLetGoReason.Dropped));
            Assert.That(LedgeHangStep.IsWaitingToRegrab(_player.Parkour), Is.True);

            var landed = false;
            for (var i = 0; i < 100 && !landed; i++)
            {
                Tick();
                Assert.That(_player.IsHangingOnLedge, Is.False, "tick " + i);
                landed = _player.IsGrounded;
            }

            Assert.That(landed, Is.True);
        }

        [Test]
        public void WithTheDefaultTuningFallingPastTheLedgeNeverGrabs()
        {
            _player.ParkourTuning = ParkourTuning.Default;
            _body.transform.position = _fallStart;
            Physics.SyncTransforms();

            var landed = false;
            for (var i = 0; i < 100 && !landed; i++)
            {
                Tick();
                Assert.That(_player.IsHangingOnLedge, Is.False, "tick " + i);
                landed = _player.IsGrounded;
            }

            Assert.That(landed, Is.True);
            Assert.That(_player.Parkour, Is.EqualTo(CharacterParkourState.None));
        }

        [Test]
        public void HoldingCrouchWhileFallingNeverGrabs()
        {
            _body.transform.position = _fallStart;
            Physics.SyncTransforms();

            Tick(crouch: true);

            Assert.That(_player.IsHangingOnLedge, Is.False);
            Assert.That(_player.LastLedgeHang.Motion, Is.EqualTo(LedgeHangMotion.None));
        }

        // 置いた直後は接地していないので、最初の tick で落ち始めると同時にふちを掴む。
        private void DropNextToTheLedge()
        {
            _body.transform.position = _fallStart;
            Physics.SyncTransforms();
            Tick();
            Assert.That(_player.IsHangingOnLedge, Is.True, "the first tick should grab the ledge");
        }

        private void Tick(Vector2 move = default, bool jump = false, bool crouch = false)
        {
            _player.Tick(move, false, crouch, jump, _tickSeconds);
            Physics.SyncTransforms();
        }

        private static void AssertNear(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(_tolerance), "x");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(_tolerance), "y");
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(_tolerance), "z");
        }
    }
}

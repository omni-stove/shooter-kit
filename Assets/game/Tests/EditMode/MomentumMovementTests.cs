using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // オフラインの SlideMovement と PlayerController が、予測・サーバーの CharacterMotorSimulation と同じく MomentumTuning を読むことを確かめる。
    public sealed class MomentumMovementTests
    {
        private const float StepSeconds = 0.02f;
        private const int MaxSteps = 200;

        private const float WallNearZ = 0.8f;
        private const float WallDepth = 0.4f;
        private const float WallHeight = 1f;

        private GameObject _body;
        private CharacterController _characterController;
        private PlayerController _player;
        private SlideMovement _slide;
        private InputAction _move;
        private InputAction _sprint;
        private InputAction _crouch;
        private InputAction _jump;
        private GameObject _floor;
        private GameObject _lowerFloor;
        private GameObject _wall;
        private bool _crouchWasHeld;
        private bool _sprintWasHeld;

        [SetUp]
        public void SetUp()
        {
            _body = new GameObject("Body");
            _body.transform.position = new Vector3(0f, 1f, 0f);
            _characterController = _body.AddComponent<CharacterController>();
            _characterController.height = 2f;
            _characterController.radius = 0.5f;
            _characterController.center = Vector3.zero;
            _player = _body.AddComponent<PlayerController>();
            _move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            _sprint = new InputAction("Sprint", InputActionType.Button);
            _crouch = new InputAction("Crouch", InputActionType.Button);
            _jump = new InputAction("Jump", InputActionType.Button);
            var look = _body.AddComponent<FirstPersonLook>();
            _player.Initialize(_characterController, look, _move, _sprint, _crouch, _jump, new MotorSpec(5f, 8f, 1.2f, 20f, 0.1f, 2.5f));
            _player.ParkourTuning = ParkourTuning.Default;
            _crouchWasHeld = false;
            _sprintWasHeld = false;
        }

        [TearDown]
        public void TearDown()
        {
            _move.Dispose();
            _sprint.Dispose();
            _crouch.Dispose();
            _jump.Dispose();
            Object.DestroyImmediate(_body);
            foreach (var placed in new[] { _floor, _lowerFloor, _wall })
            {
                if (placed != null)
                {
                    Object.DestroyImmediate(placed);
                }
            }
        }

        [Test]
        public void WithTheDefaultTuningASlideJumpKeepsOnlyTheSlideSpeed()
        {
            PlaceFloor(40f);
            StartSlide();
            var slideSpeed = _slide.Motion.Speed;

            Press(sprint: true, crouch: true, jump: true);

            Assert.That(_slide.Motion.LastEnd, Is.EqualTo(SlideEnd.Jumped));
            Assert.That(_player.Motor.Velocity.Z, Is.EqualTo(slideSpeed).Within(1e-3f));
            Assert.That(_player.Motor.Velocity.Y, Is.GreaterThan(0f), "jumped");
        }

        [Test]
        public void TheSlideJumpBoostAddsSpeedForward()
        {
            PlaceFloor(40f);
            _player.ParkourTuning = new ParkourTuning(momentum: new MomentumTuning(slideJumpBoost: 2.5f));
            StartSlide();
            var slideSpeed = _slide.Motion.Speed;
            var before = _body.transform.position;

            Press(sprint: true, crouch: true, jump: true);

            Assert.That(_slide.Motion.LastEnd, Is.EqualTo(SlideEnd.Jumped));
            Assert.That(_slide.Motion.Speed, Is.EqualTo(slideSpeed), "the slide itself ends at the speed it slid");
            Assert.That(_player.Motor.Velocity.Z, Is.EqualTo(slideSpeed + 2.5f).Within(1e-3f));
            Assert.That(_player.Motor.Velocity.X, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(_body.transform.position.z - before.z, Is.EqualTo((slideSpeed + 2.5f) * StepSeconds).Within(0.01f));
        }

        [Test]
        public void WithTheDefaultTuningAVaultEndsAtRest()
        {
            PlaceFloor(40f);
            PlaceWall();

            VaultToTheEnd();

            Assert.That(_player.Motor.Velocity.X, Is.EqualTo(0f));
            Assert.That(_player.Motor.Velocity.Y, Is.EqualTo(0f));
            Assert.That(_player.Motor.Velocity.Z, Is.EqualTo(0f));
        }

        [Test]
        public void TheVaultExitSpeedKeepLeavesTheVaultMovingForward()
        {
            PlaceFloor(40f);
            PlaceWall();
            _player.ParkourTuning = new ParkourTuning(momentum: new MomentumTuning(vaultExitSpeedKeep: 0.5f));

            VaultToTheEnd();

            var expected = _player.Motor.Spec.VaultSpeed * 0.5f;
            Assert.That(_player.Motor.Velocity.X, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(_player.Motor.Velocity.Y, Is.EqualTo(0f).Within(1e-3f), "the drop down the far side is not carried over");
            Assert.That(_player.Motor.Velocity.Z, Is.EqualTo(expected).Within(1e-3f));
        }

        [Test]
        public void WithTheDefaultTuningAVaultOffADropFallsStraightDown()
        {
            PlaceDropBehindWall();

            VaultToTheEnd();
            var end = _body.transform.position;
            FallToTheLowerFloor();

            Assert.That(_body.transform.position.z - end.z, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void AVaultOffADropCarriesForwardWithTheKeptSpeed()
        {
            PlaceDropBehindWall();
            _player.ParkourTuning = new ParkourTuning(momentum: new MomentumTuning(vaultExitSpeedKeep: 0.5f));

            VaultToTheEnd();
            var end = _body.transform.position;
            FallToTheLowerFloor();

            // 手を離して落ちると、横の速さは 1 Tick ごとに空中の操作の割合だけ 0 へ寄り、足し合わせた距離は 速さ × (1 - 割合) / 割合 × Tick に近づく
            var airControl = _player.Motor.Spec.AirControl;
            var limit = _player.Motor.Spec.VaultSpeed * 0.5f * (1f - airControl) / airControl * StepSeconds;
            Assert.That(_body.transform.position.z - end.z, Is.InRange(limit * 0.7f, limit + 0.01f));
        }

        [Test]
        public void ACancelledVaultDropsWithoutTheKeptSpeed()
        {
            PlaceFloor(40f);
            PlaceWall();
            _player.ParkourTuning = new ParkourTuning(momentum: new MomentumTuning(vaultExitSpeedKeep: 1f));
            Settle();
            _player.VaultRule = Vault.On;
            _player.Tick(Vector2.zero, false, false, true, StepSeconds);
            Assert.That(_player.IsVaulting, Is.True);

            Assert.That(_player.CancelVault(), Is.True);

            Assert.That(_player.Motor.Velocity.X, Is.EqualTo(0f));
            Assert.That(_player.Motor.Velocity.Y, Is.EqualTo(0f));
            Assert.That(_player.Motor.Velocity.Z, Is.EqualTo(0f));
        }

        private void StartSlide()
        {
            _slide = _body.AddComponent<SlideMovement>();
            _slide.Configure(GameRules.Default.WithActions(GameRules.Default.Actions.WithSlide(Slide.On)));
            Settle();
            for (var i = 0; i < 10; i++)
            {
                Press(sprint: true, crouch: false);
            }

            Press(sprint: true, crouch: true);
            Press(sprint: true, crouch: true);
            Assert.That(_slide.IsSliding, Is.True);
        }

        private void VaultToTheEnd()
        {
            Settle();
            _player.VaultRule = Vault.On;
            _player.Tick(Vector2.zero, false, false, true, StepSeconds);
            Assert.That(_player.IsVaulting, Is.True, _player.LastVaultProbe.Verdict.ToString());
            for (var i = 0; i < MaxSteps && _player.IsVaulting; i++)
            {
                _player.Tick(Vector2.zero, false, false, false, StepSeconds);
            }

            Assert.That(_player.IsVaulting, Is.False, "the vault ends");
        }

        private void FallToTheLowerFloor()
        {
            Assert.That(_player.IsGrounded, Is.False, "the far side is a drop");
            for (var i = 0; i < MaxSteps && !_player.IsGrounded; i++)
            {
                _player.Tick(Vector2.zero, false, false, false, StepSeconds);
            }

            Assert.That(_player.IsGrounded, Is.True, "landed on the lower floor");
            Assert.That(_body.transform.position.y, Is.LessThan(0f));
        }

        private void Settle()
        {
            for (var i = 0; i < 30 && !_player.IsGrounded; i++)
            {
                _player.Tick(Vector2.zero, false, false, false, StepSeconds);
            }

            Assert.That(_player.IsGrounded, Is.True, "landed");
        }

        private void Press(bool sprint, bool crouch, bool jump = false)
        {
            var input = new PlayerMoveInput(
                Vector2.up,
                sprint,
                sprint && !_sprintWasHeld,
                InputDeviceKind.KeyboardMouse,
                crouch,
                crouch && !_crouchWasHeld,
                InputDeviceKind.KeyboardMouse,
                jump,
                false);
            _crouchWasHeld = crouch;
            _sprintWasHeld = sprint;
            _player.ApplyInput(input, StepSeconds);
        }

        // 足元の床の上面を y = 0 にし、奥へ farEdgeZ まで敷く。
        private void PlaceFloor(float farEdgeZ)
        {
            const float nearEdgeZ = -20f;
            _floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _floor.transform.position = new Vector3(0f, -0.5f, (nearEdgeZ + farEdgeZ) / 2f);
            _floor.transform.localScale = new Vector3(40f, 1f, farEdgeZ - nearEdgeZ);
            Physics.SyncTransforms();
        }

        // 体の前 0.8〜1.2 m に、高さ 1 m の乗り越えられる壁を置く。
        private void PlaceWall()
        {
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.transform.position = new Vector3(0f, WallHeight / 2f, WallNearZ + (WallDepth / 2f));
            _wall.transform.localScale = new Vector3(3f, WallHeight, WallDepth);
            Physics.SyncTransforms();
        }

        // 上の床は壁の下で切れ、その先は 3 m 下の床になる。
        private void PlaceDropBehindWall()
        {
            PlaceFloor(WallNearZ + (WallDepth / 2f));
            _lowerFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _lowerFloor.transform.position = new Vector3(0f, -3.5f, 0f);
            _lowerFloor.transform.localScale = new Vector3(40f, 1f, 40f);
            PlaceWall();
        }
    }
}

using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;
using NVector3 = System.Numerics.Vector3;

namespace Shooter.Tests.EditMode
{
    // 床の上面は y = 0。体は高さ 2・半径 0.5・中心 0 で、重力は 20。
    public sealed class PlayerControllerParkourTests
    {
        private const float _tickSeconds = 0.02f;
        private const int _maxTicks = 1000;

        private static readonly ParkourTuning _everyMoveOn = new ParkourTuning(
            new WallKickTuning(true),
            new LandingRollTuning(true),
            new WallClimbTuning(true),
            new LedgeHangTuning(true));

        private readonly List<float> _landings = new List<float>();
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
            _player.Initialize(controller, look, _move, _sprint, _crouch, _jump, new MotorSpec(5f, 8f, 1.2f, 20f, 0.1f, 2.5f));
            _player.Water = NoWaterVolumes.Instance;
            _player.WallRunTuning = WallRunTuning.Default;
            CombatEvents.Landed += RecordLanding;
        }

        [TearDown]
        public void TearDown()
        {
            CombatEvents.Landed -= RecordLanding;
            _landings.Clear();
            _move.Dispose();
            _sprint.Dispose();
            _crouch.Dispose();
            _jump.Dispose();
            Object.DestroyImmediate(_body);
            Object.DestroyImmediate(_floor);
        }

        [Test]
        public void TheLandingSpeedIsScaledByTheParkourFallSpeedScale()
        {
            Assert.That(PlayerController.LandingSpeed(-16f, CharacterParkourState.None), Is.EqualTo(16f), "without a roll the whole fall speed counts");
            Assert.That(
                PlayerController.LandingSpeed(-16f, new CharacterParkourState(ParkourMove.LandingRoll, 0f, NVector3.Zero, NVector3.Zero, 0.5f)),
                Is.EqualTo(8f));
            Assert.That(
                PlayerController.LandingSpeed(-16f, new CharacterParkourState(ParkourMove.LandingRoll, 0f, NVector3.Zero, NVector3.Zero, 0f)),
                Is.Zero);
            Assert.That(PlayerController.LandingSpeed(3f, CharacterParkourState.None), Is.Zero, "rising onto a floor is not a fall");
        }

        [Test]
        public void TheParkourTuningDefaultsToTheGameWideValues()
        {
            Assert.That(_player.ParkourTuning, Is.SameAs(Shooter.Runtime.Traversal.TraversalTuning.Current.Parkour));

            _player.ParkourTuning = _everyMoveOn;
            Assert.That(_player.ParkourTuning, Is.SameAs(_everyMoveOn));
            Assert.Throws<ArgumentNullException>(() => _player.ParkourTuning = null);
        }

        [Test]
        public void TheMotorStateStartsWithoutAParkourMove()
        {
            Assert.That(_player.Parkour, Is.EqualTo(CharacterParkourState.None));
            Assert.That(_player.TryGetMotorState(out var state), Is.True);
            Assert.That(state.Parkour, Is.EqualTo(CharacterParkourState.None));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ADropOnAnOpenFloorLandsWithTheWholeFallSpeed(bool everyMoveOn)
        {
            if (everyMoveOn)
            {
                _player.ParkourTuning = _everyMoveOn;
            }

            DropFrom(5f);

            Assert.That(_landings, Has.Count.EqualTo(1));
            Assert.That(_landings[0], Is.EqualTo(Mathf.Sqrt(2f * 20f * 5f)).Within(0.6f), "one tick of gravity (0.4 m/s) at most off the exact drop");
            Assert.That(_player.Parkour, Is.EqualTo(CharacterParkourState.None), "no wall or ledge to start a move from");
        }

        private void RecordLanding(LandingInfo landing)
        {
            if (landing.Body == _body)
            {
                _landings.Add(landing.FallSpeed);
            }
        }

        // 置いた直後の最初の接地は着地に数えないので、まず床に立たせてから持ち上げる。
        private void DropFrom(float drop)
        {
            _body.transform.position = new Vector3(0f, 1f, 0f);
            Physics.SyncTransforms();
            Settle();
            _landings.Clear();

            _body.transform.position = new Vector3(0f, 1f + drop, 0f);
            Physics.SyncTransforms();
            for (var i = 0; i < _maxTicks; i++)
            {
                _player.Tick(Vector2.zero, false, false, false, _tickSeconds);
                if (i > 0 && _player.IsGrounded)
                {
                    return;
                }
            }

            Assert.Fail("the runner never landed");
        }

        private void Settle()
        {
            for (var i = 0; i < 10; i++)
            {
                _player.Tick(Vector2.zero, false, false, false, _tickSeconds);
            }
        }
    }
}

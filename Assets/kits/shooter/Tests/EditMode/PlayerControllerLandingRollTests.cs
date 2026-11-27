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
    // 5 落とすと着地の速さは約 14.1 で、安全な 12 を超える。受け身で半分の約 7.1 になれば傷まない。
    public sealed class PlayerControllerLandingRollTests
    {
        private const float _tickSeconds = 0.02f;
        private const float _drop = 5f;
        private const int _maxTicks = 1000;

        // 立った高さ (1) の 1 上。落ちる速さで 0.1 秒ほどで着地するので、既定の窓 0.3 秒の中に入る
        private const float _pressBelowY = 2f;

        private static readonly ParkourTuning _rollOn = new ParkourTuning(
            WallKickTuning.Default, new LandingRollTuning(true), WallClimbTuning.Default, LedgeHangTuning.Default);

        private readonly List<float> _landings = new List<float>();
        private GameObject _floor;
        private GameObject _body;
        private PlayerController _player;
        private Damageable _damageable;
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

            _body = new GameObject("Roller");
            var controller = _body.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.center = Vector3.zero;
            _player = _body.AddComponent<PlayerController>();
            _damageable = _body.AddComponent<Damageable>();
            _damageable.Initialize("Roller", 100);
            _move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            _sprint = new InputAction("Sprint", InputActionType.Button);
            _crouch = new InputAction("Crouch", InputActionType.Button);
            _jump = new InputAction("Jump", InputActionType.Button);
            var look = _body.AddComponent<FirstPersonLook>();
            _player.Initialize(controller, look, _move, _sprint, _crouch, _jump, new MotorSpec(5f, 8f, 1.2f, 20f, 0.1f, 2.5f));
            _player.Water = NoWaterVolumes.Instance;
            _player.WallRunTuning = WallRunTuning.Default;
            _player.ParkourTuning = _rollOn;
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
        public void PressingCrouchJustBeforeLandingHalvesTheLandingSpeed()
        {
            var plain = DropAndLand(pressCrouch: false);
            Assert.That(_player.Parkour.Move, Is.EqualTo(ParkourMove.None), "not pressing crouch does not roll");

            var rolled = DropAndLand(pressCrouch: true);

            Assert.That(plain, Is.EqualTo(Mathf.Sqrt(2f * 20f * _drop)).Within(0.6f));
            Assert.That(rolled, Is.EqualTo(plain * LandingRollTuning.DefaultFallSpeedScale).Within(0.4f));
            Assert.That(_player.Parkour.Move, Is.EqualTo(ParkourMove.LandingRoll));
            Assert.That(_player.Parkour.FallSpeedScale, Is.EqualTo(LandingRollTuning.DefaultFallSpeedScale));
            Assert.That(_player.IsLandingRolling, Is.True);
        }

        [Test]
        public void RollingTakesLessFallDamageThanLandingFlat()
        {
            _player.FallDamage = FallDamage.On;

            DropAndLand(pressCrouch: false);
            var afterPlain = _damageable.Health.Current;
            DropAndLand(pressCrouch: true);
            var afterRolled = _damageable.Health.Current;

            var plainDamage = 100 - afterPlain;
            var rolledDamage = afterPlain - afterRolled;
            Assert.That(plainDamage, Is.GreaterThan(0), "14.1 is over the safe 12");
            Assert.That(rolledDamage, Is.Zero, "7.1 is under the safe 12");
            Assert.That(rolledDamage, Is.LessThan(plainDamage));
        }

        [Test]
        public void TheRollGoesForwardForTheTunedSecondsAndIsLogged()
        {
            LogAssert.Expect(LogType.Log, new Regex("^" + Regex.Escape(LandingRollLog.StartPrefix) + "fallSpeed=\\d+\\.\\d\\d fallSpeedScale=0\\.50 "));
            DropAndLand(pressCrouch: true);
            var landedAt = _body.transform.position;
            Assert.That(_player.IsLandingRolling, Is.True);

            LogAssert.Expect(LogType.Log, new Regex("^" + Regex.Escape(LandingRollLog.FinishPrefix) + "seconds=0\\.\\d\\d$"));
            var rollingTicks = 1;
            for (var i = 0; i < _maxTicks && _player.IsLandingRolling; i++)
            {
                // 動かす入力・ダッシュ・しゃがみ・ジャンプを押しても転がる向きは変わらず、跳ばない
                _player.Tick(new Vector2(1f, 0f), true, true, true, _tickSeconds);
                if (_player.IsLandingRolling)
                {
                    rollingTicks++;
                    Assert.That(_player.IsGrounded, Is.True, "tick " + i);
                    Assert.That(_player.IsCrouched, Is.False, "tick " + i);
                }
            }

            Assert.That(_player.Parkour, Is.EqualTo(CharacterParkourState.None));
            Assert.That(rollingTicks * _tickSeconds, Is.EqualTo(LandingRollTuning.DefaultRollSeconds).Within(_tickSeconds * 1.5f));
            Assert.That(_body.transform.position.z, Is.GreaterThan(landedAt.z + 0.01f), "the roll carries the forward speed of the fall");
        }

        [Test]
        public void WithTheDefaultTuningCrouchingBeforeLandingDoesNotSoftenIt()
        {
            _player.ParkourTuning = ParkourTuning.Default;

            var crouched = DropAndLand(pressCrouch: true);

            Assert.That(crouched, Is.EqualTo(Mathf.Sqrt(2f * 20f * _drop)).Within(0.6f));
            Assert.That(_player.Parkour, Is.EqualTo(CharacterParkourState.None));
            Assert.That(_player.IsLandingRolling, Is.False);
        }

        [Test]
        public void PressingCrouchTooEarlyDoesNotRoll()
        {
            var landing = DropAndLand(pressCrouch: false, pressFirstTickOnly: true);

            Assert.That(landing, Is.EqualTo(Mathf.Sqrt(2f * 20f * _drop)).Within(0.6f), "the 0.3 second window closed long before landing");
            Assert.That(_player.Parkour.Move, Is.EqualTo(ParkourMove.None));
        }

        [Test]
        public void TheMotorStateCarriesTheRoll()
        {
            DropAndLand(pressCrouch: true);

            Assert.That(_player.TryGetMotorState(out var state), Is.True);
            Assert.That(state.Parkour, Is.EqualTo(_player.Parkour));
            Assert.That(state.Parkour.Move, Is.EqualTo(ParkourMove.LandingRoll));
        }

        private void RecordLanding(LandingInfo landing)
        {
            if (landing.Body == _body)
            {
                _landings.Add(landing.FallSpeed);
            }
        }

        // 置いた直後の最初の接地は着地に数えないので、まず床に立たせてから持ち上げる。前へ歩く入力のまま落とし、着地を知らせた速さを返す。
        private float DropAndLand(bool pressCrouch, bool pressFirstTickOnly = false)
        {
            _body.transform.position = new Vector3(0f, 1f, 0f);
            Physics.SyncTransforms();
            for (var i = 0; i < 10; i++)
            {
                _player.Tick(Vector2.zero, false, false, false, _tickSeconds);
            }

            _landings.Clear();
            _body.transform.position = new Vector3(0f, 1f + _drop, 0f);
            Physics.SyncTransforms();
            for (var i = 0; i < _maxTicks && _landings.Count == 0; i++)
            {
                var crouch = pressFirstTickOnly ? i == 1 : pressCrouch && _body.transform.position.y < _pressBelowY;
                _player.Tick(new Vector2(0f, 1f), false, crouch, false, _tickSeconds);
            }

            Assert.That(_landings, Has.Count.EqualTo(1), "the roller should land once");
            return _landings[0];
        }
    }
}

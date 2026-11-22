using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 予測・サーバーの CharacterMotorSimulation.LedgeHang と同じく、ふちを CharacterLedgeSensor で測って LedgeHangStep に渡す。
    public sealed partial class PlayerController
    {
        private CharacterLedgeSensor _ledgeSensor;
        private CharacterBody _ledgeSensorBody;

        // 登れるかの判定に渡す。毎 tick ラムダを作らずに済むよう、この tick のぶら下がる位置を持たせて使い回す
        private Func<VaultPath, bool> _isLedgeClimbClear;
        private System.Numerics.Vector3 _ledgeClimbFrom;

        public bool IsHangingOnLedge => _parkour.IsHangingOnLedge;

        public LedgeHangResult LastLedgeHang { get; private set; }

        // 横取り・泳ぎはシミュレーションでは Parkour = None から始まるので、ぶら下がりを黙って離す。
        private void DropLedgeHangSilently()
        {
            if (_parkour.IsHangingOnLedge)
            {
                _parkour = CharacterParkourState.None;
            }
        }

        // CharacterMotorSimulation.TryStepLedgeHang と同じく、立っていない・カバー・倒れているときはしゃがみと同じく掴まず、掴んでいれば離す。
        private void TryOverrideMoveForLedgeHang(
            Vector2 move, bool jumpPressed, bool crouch, float deltaSeconds, ref bool overridden, ref Vector3 displacement)
        {
            LastLedgeHang = default;
            var tuning = ParkourTuning.LedgeHang;
            if (!(deltaSeconds > 0f) || float.IsInfinity(deltaSeconds)
                || (!tuning.Enabled && !_parkour.IsHangingOnLedge && !LedgeHangStep.IsWaitingToRegrab(_parkour)))
            {
                return;
            }

            var before = _parkour;
            var grounded = _controller.isGrounded;
            var velocity = _motor.Velocity;
            var position = transform.position.ToNumerics();
            var yawRadians = _look.transform.eulerAngles.y * Mathf.Deg2Rad;
            var release = crouch || !CanWallRun();
            var jump = jumpPressed && _jumpRule == Jump.On;
            var ledge = MeasureLedge(tuning, before, position, velocity, yawRadians, grounded, release, deltaSeconds);

            var geometry = _stance;
            _ledgeClimbFrom = ledge.IsFound ? LedgeHangStep.HangPosition(ledge.Point, ledge.Normal, geometry) : position;
            _isLedgeClimbClear ??= path => PlaceLedgeSensor().IsClimbClear(_ledgeClimbFrom, path);
            var result = LedgeHangStep.Step(
                before, ledge, position, velocity, move.ToNumerics(), jump, release, grounded, geometry, tuning, deltaSeconds, _isLedgeClimbClear);
            _parkour = result.State;
            LastLedgeHang = result;

            var line = LedgeHangLog.ForStep(before, result);
            if (line != null)
            {
                Debug.Log(line, this);
            }

            if (!result.HoldsBody)
            {
                return;
            }

            displacement = result.Position.ToUnity() - transform.position;
            _motor.SetVelocity(result.Velocity);
            overridden = true;
        }

        // CharacterMotorSimulation.Measure と同じく、掴めない tick はふちを調べない。
        private LedgeContact MeasureLedge(
            LedgeHangTuning tuning,
            CharacterParkourState parkour,
            System.Numerics.Vector3 position,
            System.Numerics.Vector3 velocity,
            float yawRadians,
            bool grounded,
            bool release,
            float deltaSeconds)
        {
            if (!tuning.Enabled || grounded)
            {
                return LedgeContact.None;
            }

            switch (parkour.Move)
            {
                case ParkourMove.LedgeHang:
                case ParkourMove.LedgeShimmy:
                    return release ? LedgeContact.None : PlaceLedgeSensor().Follow(parkour.LedgePoint, parkour.WallNormal);
                case ParkourMove.None:
                case ParkourMove.WallKick:
                    var waiting = LedgeHangStep.IsWaitingToRegrab(parkour) && parkour.Elapsed + deltaSeconds < tuning.RegrabSeconds;
                    return release || waiting || velocity.Y > 0f
                        ? LedgeContact.None
                        : PlaceLedgeSensor().Probe(position, yawRadians, tuning.GrabReach);
                default:
                    return LedgeContact.None;
            }
        }

        // ふちは壁走りと同じ体 (自分のコライダーを除く Unity の当たり) で調べる。体を作り直したときだけ測る部品も作り直す。
        private CharacterLedgeSensor PlaceLedgeSensor()
        {
            var body = PlaceLeanBody();
            if (_ledgeSensor == null || !ReferenceEquals(_ledgeSensorBody, body))
            {
                _ledgeSensor = new CharacterLedgeSensor(body);
                _ledgeSensorBody = body;
            }

            return _ledgeSensor;
        }

        private void ResetLedgeHang()
        {
            LastLedgeHang = default;
            _ledgeSensor = null;
            _ledgeSensorBody = null;
        }
    }
}

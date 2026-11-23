using Shooter.Core;
using UnityEngine;
using NVector3 = System.Numerics.Vector3;

namespace Shooter.Runtime
{
    // 予測・サーバーの CharacterMotorSimulation.WallClimb と同じく、壁を駆け上がり、ふちに届いたら上に立てるときだけよじ登る。
    // よじ登りはオフラインの崖のぼり (PlayerController.Mantle) の道でたどるが、速さはシミュレーションと同じ VaultSpeed にそろえる。
    public sealed partial class PlayerController
    {
        private CharacterFrontWallSensor _frontWallSensor;
        private CharacterBody _frontWallSensorBody;

        // この tick の移動を壁登りが決めたか。_controller.Move の後で SettleWallClimb が天井の当たりを見るため
        private bool _wallClimbMoved;

        public bool IsWallClimbing => _parkour.Move == ParkourMove.WallClimb;

        // 崖のぼりのルール (MantleRule) を見ないのは、予測とサーバーがそのルールを知らずにふちで上に乗るので、オフラインだけ落ちると動きが分かれるため。
        partial void TryOverrideMoveForWallClimb(Vector2 move, bool jumpPressed, float deltaSeconds, ref bool overridden, ref Vector3 displacement)
        {
            if (!(deltaSeconds > 0f) || float.IsInfinity(deltaSeconds))
            {
                return;
            }

            var tuning = ParkourTuning.WallClimb;
            var climbing = IsWallClimbing;
            var allowed = CanWallRun();
            var jump = jumpPressed && _jumpRule == Jump.On;
            var grounded = _controller.isGrounded;

            // CharacterMotorSimulation.TryStepWallClimb と同じく、始められない tick は壁を調べない
            if (!climbing && (!tuning.Enabled || !allowed || !jump || !grounded || _parkour.IsActive))
            {
                return;
            }

            var spec = _motor.Spec;
            var yawRadians = _look.transform.eulerAngles.y * Mathf.Deg2Rad;
            var wall = allowed ? PlaceFrontWallSensor().Probe(transform.position.ToNumerics(), yawRadians, spec, tuning) : FrontWallContact.None;
            var before = _parkour;
            var velocity = _motor.Velocity;
            var result = WallClimbStep.Step(before, wall, velocity, jump && allowed, grounded, _currentStance, spec, tuning, deltaSeconds);
            _parkour = result.State;
            ReportWallClimb(before, result, velocity, wall, tuning);
            switch (result.Motion)
            {
                case WallClimbMotion.None:
                    return;
                case WallClimbMotion.ReachedLedge:
                    MantleFromWallClimb(tuning.MantleAtLedge, deltaSeconds, ref displacement);
                    overridden = true;
                    return;
                case WallClimbMotion.PushedOff:
                    _motor.JumpWith(result.Velocity);
                    break;
                default:
                    _motor.SetVelocity(result.Velocity);
                    break;
            }

            displacement = result.Displacement.ToUnity();
            overridden = true;
            _wallClimbMoved = true;
        }

        // CharacterMotorSimulation.TryStepWallClimb と同じく、頭が天井に当たったら上向きの速さを捨てる
        private void SettleWallClimb(CollisionFlags collisions)
        {
            if (!_wallClimbMoved)
            {
                return;
            }

            _wallClimbMoved = false;
            var velocity = _motor.Velocity;
            if ((collisions & CollisionFlags.Above) != 0 && velocity.Y > 0f)
            {
                _motor.SetVelocity(new NVector3(velocity.X, 0f, velocity.Z));
            }
        }

        private void ReportWallClimb(CharacterParkourState before, WallClimbResult result, NVector3 velocity, FrontWallContact wall, WallClimbTuning tuning)
        {
            if (before.Move != ParkourMove.WallClimb && result.Motion == WallClimbMotion.Climbing)
            {
                Debug.Log(WallClimbLog.Start(WallClimbStep.IntoWallSpeed(velocity, wall.Normal), wall.TopHeight), this);
            }
            else if (result.HasEnded)
            {
                Debug.Log(WallClimbLog.End(result.Motion, before.Elapsed, WallClimbStep.ClimbedHeight(before, tuning)), this);
            }
        }

        // CharacterMotorSimulation.TryStepWallClimb と同じく、上に立てないふちと、ふちで上に乗らない調整では上向きの速さを捨てて 0 から落ち始める
        private void MantleFromWallClimb(bool mantleAtLedge, float deltaSeconds, ref Vector3 displacement)
        {
            if (!mantleAtLedge)
            {
                FallFromWall(deltaSeconds, ref displacement);
                return;
            }

            var probe = MeasureMantle();
            LastMantleProbe = probe;
            if (!probe.CanMantle)
            {
                FallFromWall(deltaSeconds, ref displacement);
                return;
            }

            BeginMantle(new VaultPath(probe.Path.Points, _motor.Spec.VaultSpeed), probe.Direction.ToUnity());
            _mantleKeepsVaultExit = true;
            var overridden = false;
            TryOverrideMoveForMantle(false, deltaSeconds, ref overridden, ref displacement);
        }

        private void FallFromWall(float deltaSeconds, ref Vector3 displacement)
        {
            var verticalEnd = -_motor.Spec.Gravity * deltaSeconds;
            _motor.SetVelocity(new NVector3(0f, verticalEnd, 0f));
            displacement = new Vector3(0f, verticalEnd * 0.5f * deltaSeconds, 0f);
        }

        // 壁はリーンと同じ体 (自分のコライダーを除く Unity の当たり) で調べる。体を作り直したときだけ測る部品も作り直す。
        private CharacterFrontWallSensor PlaceFrontWallSensor()
        {
            var body = PlaceLeanBody();
            if (_frontWallSensor == null || !ReferenceEquals(_frontWallSensorBody, body))
            {
                _frontWallSensor = new CharacterFrontWallSensor(body);
                _frontWallSensorBody = body;
            }

            return _frontWallSensor;
        }
    }
}

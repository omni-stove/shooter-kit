using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 予測・サーバーの CharacterMotorSimulation.TryStepWallKick と同じ決まりで蹴る。蹴らなかった tick も WallKickStep を通し、着地で壁蹴りを終える。
    public sealed partial class PlayerController
    {
        private void TryOverrideMoveForWallKick(Vector2 move, bool jumpPressed, float deltaSeconds, ref bool overridden, ref Vector3 displacement)
        {
            if (!(deltaSeconds > 0f) || float.IsInfinity(deltaSeconds))
            {
                return;
            }

            var tuning = ParkourTuning.WallKick;
            var grounded = _controller.isGrounded;
            var kick = jumpPressed && _jumpRule == Jump.On && CanWallRun();
            var wall = kick && tuning.Enabled && !grounded && !_wallRun.IsRunning
                ? PlaceWallSensor().Probe(transform.position.ToNumerics(), _look.transform.eulerAngles.y * Mathf.Deg2Rad)
                : WallContact.None;
            var result = WallKickStep.Step(_parkour, _wallRun, wall, _motor.Velocity, kick, grounded, tuning, deltaSeconds);
            _parkour = result.State;
            switch (result.Outcome)
            {
                case WallKickOutcome.SameWall:
                    Debug.Log(WallKickLog.SameWall(wall), this);
                    return;
                case WallKickOutcome.Kicked:
                    break;
                default:
                    return;
            }

            var velocity = WallKickStep.Fly(result.Velocity, _motor.Spec.Gravity, deltaSeconds, out var flight);
            _motor.JumpWith(velocity);
            displacement = flight.ToUnity();
            overridden = true;
            Debug.Log(WallKickLog.Kicked(wall, result.Velocity), this);
        }
    }
}

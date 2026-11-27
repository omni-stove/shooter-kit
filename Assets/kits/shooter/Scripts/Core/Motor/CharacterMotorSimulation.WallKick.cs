using System.Numerics;

namespace Shooter.Core
{
    public sealed partial class CharacterMotorSimulation
    {
        // 蹴らなかった tick も WallKickStep を通すのは、着地で壁蹴りを終え、空中の秒数を進める決まりを WallKickStep の 1 か所に置くため。
        // 壁はジャンプを押した空中の tick だけ調べる (使わないゲームと押していない tick に Raycast を払わせない)。
        private bool TryStepWallKick(
            WallKickTuning tuning,
            CharacterMotorState state,
            Vector2 move,
            float yaw,
            bool jump,
            bool allowed,
            ref CharacterParkourState parkour,
            out Vector3 velocity)
        {
            velocity = Vector3.Zero;
            var kick = jump && allowed;
            var wall = kick && tuning.Enabled && !state.IsGrounded && !state.WallRun.IsRunning
                ? _wallSensor.Probe(Body.Position, yaw)
                : WallContact.None;
            var result = WallKickStep.Step(parkour, state.WallRun, wall, _motor.Velocity, kick, state.IsGrounded, tuning, TickSeconds);
            parkour = result.State;
            if (!result.Kicked)
            {
                return false;
            }

            velocity = WallKickStep.Fly(result.Velocity, _motor.Spec.Gravity, TickSeconds, out var displacement);
            _motor.JumpWith(velocity);
            Body.Move(displacement);
            return true;
        }
    }
}

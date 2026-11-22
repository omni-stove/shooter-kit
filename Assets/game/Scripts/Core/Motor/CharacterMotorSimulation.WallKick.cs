using System.Numerics;

namespace Shooter.Core
{
    public sealed partial class CharacterMotorSimulation
    {
        private static bool TryStepWallKick(
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
            return false;
        }
    }
}

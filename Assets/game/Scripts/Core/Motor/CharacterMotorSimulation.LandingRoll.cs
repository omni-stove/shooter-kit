using System.Numerics;

namespace Shooter.Core
{
    public sealed partial class CharacterMotorSimulation
    {
        private static CharacterParkourState StepLandingRoll(
            LandingRollTuning tuning,
            CharacterParkourState parkour,
            CharacterMotorInput input,
            Vector3 velocity,
            bool wasGrounded,
            bool grounded)
        {
            return parkour;
        }
    }
}

using System.Numerics;

namespace Shooter.Core
{
    // 受け身は着地した tick を Finish (SettleParkour) で見て始め、次の tick からふつうの移動の代わりに TryMoveLandingRoll で前へ転がす。
    // 始めた tick に体を動かさないのは、その tick はもうふつうの移動で着地しており、もう一度動かすと二重に進むため。
    // 始めた tick の速度を落ちる速さのまま残すのは、サーバーの ServerMovement.LandingSpeed がその速度に FallSpeedScale を掛けて落下の傷みを決めるため。
    public sealed partial class CharacterMotorSimulation
    {
        // この tick の転がる動きを TryMoveLandingRoll で進めたか。Finish で同じ tick を二度進めないため
        private bool _landingRollMoved;

        public LandingRollResult LastLandingRoll { get; private set; } = LandingRollResult.Resting(CharacterParkourState.None);

        private void BeginLandingRoll()
        {
            _landingRollMoved = false;
            LastLandingRoll = LandingRollResult.Resting(CharacterParkourState.None);
        }

        // 転がっている間は、地面へ吸い付ける縦の速さ (PlayerMotor と回避の Roll と同じ) を足し、段差を転がり降りても浮かせない。
        private bool TryMoveLandingRoll(LandingRollTuning tuning, CharacterMotorState state, ref CharacterParkourState parkour, out Vector3 velocity)
        {
            velocity = Vector3.Zero;
            if (parkour.Move != ParkourMove.LandingRoll)
            {
                return false;
            }

            var result = LandingRollStep.Step(parkour, state.Velocity, false, true, state.IsGrounded, tuning, TickSeconds);
            LastLandingRoll = result;
            parkour = result.State;
            if (!result.IsRolling)
            {
                return false;
            }

            _landingRollMoved = true;
            Body.Move(new Vector3(result.Displacement.X, PlayerMotor.GroundedVerticalSpeed * TickSeconds, result.Displacement.Z));
            velocity = new Vector3(result.Velocity.X, PlayerMotor.GroundedVerticalSpeed, result.Velocity.Z);
            return true;
        }

        // 倒れている間は Restrained がしゃがみを入れるので、倒れて落ちただけで受け身をとらないよう、しゃがみに数えない。
        private CharacterParkourState StepLandingRoll(
            LandingRollTuning tuning,
            CharacterParkourState parkour,
            CharacterMotorInput input,
            Vector3 velocity,
            bool wasGrounded,
            bool grounded)
        {
            if (_landingRollMoved)
            {
                return parkour;
            }

            var crouch = input.Crouch && !input.Downed;
            var result = LandingRollStep.Step(parkour, velocity, crouch, wasGrounded, grounded, tuning, TickSeconds);
            if (result.Motion != LandingRollMotion.None)
            {
                LastLandingRoll = result;
            }

            return result.State;
        }
    }
}

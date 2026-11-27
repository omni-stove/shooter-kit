using Shooter.Core;
using UnityEngine;
using NVector3 = System.Numerics.Vector3;

namespace Shooter.Runtime
{
    // 予測・サーバーの CharacterMotorSimulation.LandingRoll と同じ段 (LandingRollStep) を、着地した tick の FinishParkour で始め、次の tick から移動の横取りで前へ転がす。
    // 始めた tick に体を動かさないのは、その tick はもう CharacterController で着地しており、もう一度動かすと二重に進むため。
    public sealed partial class PlayerController
    {
        // この tick の転がる動きを TryOverrideMoveForLandingRoll で進めたか。FinishParkour で同じ tick を二度進めないため
        private bool _landingRollMoved;

        public bool IsLandingRolling => _parkour.Move == ParkourMove.LandingRoll;

        // CharacterMotorSimulation と同じく、転がる間はダッシュ・ジャンプ・しゃがみを読まない
        private void FilterInputForLandingRoll(ref bool sprint, ref bool crouch, ref bool jumpPressed)
        {
            if (!IsLandingRolling)
            {
                return;
            }

            sprint = false;
            crouch = false;
            jumpPressed = false;
        }

        // 縦は回避の横取りと同じく地面へ吸い付ける速さにし、段差を転がり降りても浮かせない。転がり終えたら PlayerMotor がその速さから引き継ぐ。
        private void TryOverrideMoveForLandingRoll(float deltaSeconds, ref bool overridden, ref Vector3 displacement)
        {
            if (!IsLandingRolling || !(deltaSeconds > 0f) || float.IsInfinity(deltaSeconds))
            {
                return;
            }

            var rolledSeconds = _parkour.Elapsed;
            var result = LandingRollStep.Step(_parkour, _motor.Velocity, false, true, _controller.isGrounded, ParkourTuning.LandingRoll, deltaSeconds);
            _parkour = result.State;
            if (!result.IsRolling)
            {
                Debug.Log(LandingRollLog.Finish(rolledSeconds), this);
                return;
            }

            _landingRollMoved = true;
            var vertical = PlayerMotor.GroundedVerticalSpeed;
            _motor.SetVelocity(new NVector3(result.Velocity.X, vertical, result.Velocity.Z));
            displacement = new Vector3(result.Displacement.X, vertical * deltaSeconds, result.Displacement.Z);
            overridden = true;
        }

        // 着地の速さを読む ReportLanding より先に呼ばれるので、始めた tick の _motor.Velocity はまだ落ちる速さを持っている。
        private void StepLandingRoll(bool footing, bool crouch, float deltaSeconds)
        {
            if (_landingRollMoved)
            {
                _landingRollMoved = false;
                return;
            }

            // CharacterMotorSimulation.StepLandingRoll と同じく、掴み直しの待ちの Elapsed はぶら下がりの段が持つので、押下の秒数として読まない。
            if (!(deltaSeconds > 0f) || float.IsInfinity(deltaSeconds) || LedgeHangStep.IsWaitingToRegrab(_parkour))
            {
                return;
            }

            var tuning = ParkourTuning.LandingRoll;
            var landingVelocity = _motor.Velocity;
            var rolledSeconds = _parkour.Elapsed;
            var result = LandingRollStep.Step(_parkour, landingVelocity, crouch, _lastGrounded == true, footing, tuning, deltaSeconds);
            _parkour = result.State;
            if (result.Motion == LandingRollMotion.Started)
            {
                Debug.Log(LandingRollLog.Start(landingVelocity, result, tuning), this);
            }
            else if (result.Motion == LandingRollMotion.Finished)
            {
                Debug.Log(LandingRollLog.Finish(rolledSeconds), this);
            }
        }
    }
}

using System;
using System.Numerics;

namespace Shooter.Core
{
    public enum LandingRollMotion : byte
    {
        None = 0,
        Started = 1,
        Rolling = 2,
        Finished = 3,
    }

    public readonly struct LandingRollResult
    {
        public LandingRollResult(CharacterParkourState state, LandingRollMotion motion, Vector3 velocity, Vector3 displacement)
        {
            State = state;
            Motion = motion;

            // 転がっていない結果に速度を残すと、ふつうの移動に任せた tick で読む側が古い値を使いかねない
            var moving = motion == LandingRollMotion.Started || motion == LandingRollMotion.Rolling;
            Velocity = moving ? velocity : Vector3.Zero;
            Displacement = moving ? displacement : Vector3.Zero;
        }

        public CharacterParkourState State { get; }

        public LandingRollMotion Motion { get; }

        public Vector3 Velocity { get; }

        public Vector3 Displacement { get; }

        public bool IsRolling => Motion == LandingRollMotion.Started || Motion == LandingRollMotion.Rolling;

        public static LandingRollResult Resting(CharacterParkourState state)
        {
            return new LandingRollResult(state, LandingRollMotion.None, Vector3.Zero, Vector3.Zero);
        }
    }

    // 押した時刻を CharacterParkourState の None の間の Elapsed に持つのは、状態をひとつにまとめた土台どおりに、
    // 予測とサーバーで比べる状態とスナップショットを増やさないため。
    // しゃがみの押下を入力の立ち上がりでなく「押していない (Elapsed が 0) 所から押した」で見るのは、
    // CharacterMotorInput が押しているかしか持たず、前の tick の入力を残さずに決めるため。
    // 着地を呼び手が体を動かした後に渡すのは、WallRunStep と同じく世界を引かない計算にして、決まりを世界を組まずに確かめるため。
    public static class LandingRollStep
    {
        public static LandingRollResult Step(
            CharacterParkourState parkour,
            Vector3 velocity,
            bool crouch,
            bool startedGrounded,
            bool grounded,
            LandingRollTuning tuning,
            float tickSeconds)
        {
            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }

            if (!(tickSeconds > 0f) || float.IsInfinity(tickSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(tickSeconds), tickSeconds, "tickSeconds must be positive and finite");
            }

            if (parkour.Move == ParkourMove.LandingRoll)
            {
                return tuning.Enabled && grounded
                    ? Continue(parkour, velocity, tuning, tickSeconds)
                    : Finish();
            }

            // ほかの動きの最中は、その動きの段が状態を持つので触らない
            if (parkour.IsActive || !tuning.Enabled)
            {
                return LandingRollResult.Resting(parkour);
            }

            if (startedGrounded)
            {
                return LandingRollResult.Resting(Idle(parkour, 0f));
            }

            var sincePressed = SincePressed(parkour.Elapsed, crouch, tickSeconds);
            if (!grounded)
            {
                return LandingRollResult.Resting(Idle(parkour, sincePressed));
            }

            if (!CanRoll(sincePressed, velocity, tuning))
            {
                return LandingRollResult.Resting(Idle(parkour, 0f));
            }

            var state = new CharacterParkourState(ParkourMove.LandingRoll, tickSeconds, Vector3.Zero, Vector3.Zero, tuning.FallSpeedScale);
            return Roll(state, LandingRollMotion.Started, velocity, tickSeconds);
        }

        // 押した tick を 0 でなく tick の長さから数えるのは、Elapsed が 0 の状態を「押していない」に取っておくため
        public static float SincePressed(float elapsed, bool crouch, float tickSeconds)
        {
            if (elapsed > 0f)
            {
                return elapsed + tickSeconds;
            }

            return crouch ? tickSeconds : 0f;
        }

        public static bool CanRoll(float sincePressed, Vector3 velocity, LandingRollTuning tuning)
        {
            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }

            return sincePressed > 0f && sincePressed <= tuning.WindowSeconds && FallSpeed(velocity) >= tuning.MinFallSpeed;
        }

        public static float FallSpeed(Vector3 velocity)
        {
            return Math.Max(0f, -velocity.Y);
        }

        private static LandingRollResult Continue(CharacterParkourState parkour, Vector3 velocity, LandingRollTuning tuning, float tickSeconds)
        {
            if (parkour.Elapsed >= tuning.RollSeconds)
            {
                return Finish();
            }

            var state = new CharacterParkourState(
                ParkourMove.LandingRoll, parkour.Elapsed + tickSeconds, Vector3.Zero, Vector3.Zero, parkour.FallSpeedScale);
            return Roll(state, LandingRollMotion.Rolling, velocity, tickSeconds);
        }

        private static LandingRollResult Finish()
        {
            return new LandingRollResult(CharacterParkourState.None, LandingRollMotion.Finished, Vector3.Zero, Vector3.Zero);
        }

        // 壁の法線とふちの位置を残すのは、それを読む壁蹴り・ぶら下がりの段の覚えを受け身の待ちで消さないため
        private static CharacterParkourState Idle(CharacterParkourState parkour, float sincePressed)
        {
            return sincePressed.Equals(parkour.Elapsed)
                ? parkour
                : new CharacterParkourState(ParkourMove.None, sincePressed, parkour.WallNormal, parkour.LedgePoint, parkour.FallSpeedScale);
        }

        // 縦の速さを 0 にするのは、着地した tick の落ちる速さを転がる間に持ち越して地面へ押し込まないため
        private static LandingRollResult Roll(CharacterParkourState state, LandingRollMotion motion, Vector3 velocity, float tickSeconds)
        {
            var horizontal = new Vector3(velocity.X, 0f, velocity.Z);
            return new LandingRollResult(state, motion, horizontal, horizontal * tickSeconds);
        }
    }
}

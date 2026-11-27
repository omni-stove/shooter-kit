using System;
using System.Numerics;

namespace Shooter.Core
{
    public enum WallClimbMotion : byte
    {
        None = 0,
        Climbing = 1,
        Fell = 2,
        ReachedLedge = 3,
        PushedOff = 4,
    }

    public readonly struct WallClimbResult
    {
        public WallClimbResult(CharacterParkourState state, WallClimbMotion motion, Vector3 velocity, Vector3 displacement, Vector3 ledge)
        {
            State = state;
            Motion = motion;

            // 動かさない結果に速度を残すと、ふつうの移動に任せた tick で読む側が古い値を使いかねない
            Velocity = motion == WallClimbMotion.None ? Vector3.Zero : velocity;
            Displacement = motion == WallClimbMotion.None ? Vector3.Zero : displacement;
            Ledge = motion == WallClimbMotion.ReachedLedge ? ledge : Vector3.Zero;
        }

        public CharacterParkourState State { get; }

        public WallClimbMotion Motion { get; }

        public Vector3 Velocity { get; }

        public Vector3 Displacement { get; }

        public Vector3 Ledge { get; }

        public bool HasEnded => Motion == WallClimbMotion.Fell || Motion == WallClimbMotion.ReachedLedge || Motion == WallClimbMotion.PushedOff;

        public static WallClimbResult Resting(CharacterParkourState state)
        {
            return new WallClimbResult(state, WallClimbMotion.None, Vector3.Zero, Vector3.Zero, Vector3.Zero);
        }
    }

    // 壁を呼び手が CharacterFrontWallSensor で調べて渡すのは、WallRunStep と同じく世界を引かない計算にして、壁の有無ごとの決まりを世界を組まずに確かめるため。
    // 登った高さを状態に持たず秒数から出すのは、上へは一定の速さで進むので秒数から決まり、CharacterParkourState に項目を足さずに済むため。
    public static class WallClimbStep
    {
        // 法線の一致まで求めないのは、組んだ壁の誤差で登っている途中に止まらないため。
        public const float SameWallDot = 0.95f;

        public static WallClimbResult Step(
            CharacterParkourState parkour,
            FrontWallContact wall,
            Vector3 velocity,
            bool jump,
            bool grounded,
            PlayerStance stance,
            MotorSpec spec,
            WallClimbTuning tuning,
            float tickSeconds)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }

            if (!(tickSeconds > 0f) || float.IsInfinity(tickSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(tickSeconds), tickSeconds, "tickSeconds must be positive and finite");
            }

            if (parkour.Move == ParkourMove.WallClimb)
            {
                return Continue(parkour, wall, jump, grounded, stance, spec, tuning, tickSeconds);
            }

            // ほかの動きの最中に始めると、その動きの状態を上書きして途中で消すので、触らずに返す
            if (parkour.Move != ParkourMove.None)
            {
                return WallClimbResult.Resting(parkour);
            }

            return TryStart(parkour, wall, velocity, jump, grounded, stance, spec, tuning, tickSeconds);
        }

        public static float IntoWallSpeed(Vector3 velocity, Vector3 normal)
        {
            return -((velocity.X * normal.X) + (velocity.Z * normal.Z));
        }

        public static float ClimbedHeight(CharacterParkourState parkour, WallClimbTuning tuning)
        {
            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }

            return parkour.Move == ParkourMove.WallClimb ? Math.Min(parkour.Elapsed * tuning.ClimbSpeed, tuning.MaxHeight) : 0f;
        }

        // ふちに届いたとするのを、よじ登れる高さまで上端が下りてきたときにするのは、そこから先をよじ登りやぶら下がりに渡すため。
        public static bool CanReachLedge(FrontWallContact wall, MotorSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            return wall.HasTop && wall.TopHeight <= spec.MantleMaxHeight;
        }

        private static WallClimbResult TryStart(
            CharacterParkourState parkour,
            FrontWallContact wall,
            Vector3 velocity,
            bool jump,
            bool grounded,
            PlayerStance stance,
            MotorSpec spec,
            WallClimbTuning tuning,
            float tickSeconds)
        {
            if (!tuning.Enabled || stance != PlayerStance.Standing || !grounded || !jump || !wall.IsFound)
            {
                return WallClimbResult.Resting(parkour);
            }

            // 低い壁は乗り越え・よじ登りに任せる。駆け上がると、登らなくても手が届く壁でまで足を止めるため
            if (IntoWallSpeed(velocity, wall.Normal) < tuning.MinSpeed || CanReachLedge(wall, spec))
            {
                return WallClimbResult.Resting(parkour);
            }

            return Climb(0f, wall.Normal, tuning, tickSeconds);
        }

        private static WallClimbResult Continue(
            CharacterParkourState parkour,
            FrontWallContact wall,
            bool jump,
            bool grounded,
            PlayerStance stance,
            MotorSpec spec,
            WallClimbTuning tuning,
            float tickSeconds)
        {
            if (grounded)
            {
                return WallClimbResult.Resting(CharacterParkourState.None);
            }

            if (!tuning.Enabled || stance != PlayerStance.Standing)
            {
                return Fall(spec, tickSeconds);
            }

            // 壁がこの tick で切れていても離れられるよう、壁の有無より先に見る
            if (jump)
            {
                return PushOff(parkour.WallNormal, spec, tuning, tickSeconds);
            }

            if (!wall.IsFound || Vector3.Dot(wall.Normal, parkour.WallNormal) < SameWallDot)
            {
                return Fall(spec, tickSeconds);
            }

            if (CanReachLedge(wall, spec))
            {
                return new WallClimbResult(CharacterParkourState.None, WallClimbMotion.ReachedLedge, Vector3.Zero, Vector3.Zero, wall.Ledge);
            }

            if (parkour.Elapsed >= tuning.MaxSeconds || parkour.Elapsed * tuning.ClimbSpeed >= tuning.MaxHeight)
            {
                return Fall(spec, tickSeconds);
            }

            return Climb(parkour.Elapsed, wall.Normal, tuning, tickSeconds);
        }

        // 最後の tick は残りの高さだけ上げ、tick の長さによらず調整値の高さで止める
        private static WallClimbResult Climb(float elapsed, Vector3 normal, WallClimbTuning tuning, float tickSeconds)
        {
            var climbed = elapsed * tuning.ClimbSpeed;
            var up = Math.Min(tuning.ClimbSpeed * tickSeconds, tuning.MaxHeight - climbed);
            var state = new CharacterParkourState(ParkourMove.WallClimb, elapsed + tickSeconds, normal, Vector3.Zero);
            return new WallClimbResult(
                state, WallClimbMotion.Climbing, new Vector3(0f, tuning.ClimbSpeed, 0f), new Vector3(0f, up, 0f), Vector3.Zero);
        }

        // 登っていた上向きの速さを残すと、登り終えてからさらに浮くので、0 から落ち始める
        private static WallClimbResult Fall(MotorSpec spec, float tickSeconds)
        {
            var result = Airborne(CharacterParkourState.None, Vector3.Zero, 0f, spec.Gravity, tickSeconds);
            return new WallClimbResult(result.State, WallClimbMotion.Fell, result.Velocity, result.Displacement, Vector3.Zero);
        }

        // 上への速さを調整値に足さず地上のジャンプの高さから出すのは、WallRunStep の跳び離れと同じく、サーバーへ送る設定を増やさないため。
        private static WallClimbResult PushOff(Vector3 normal, MotorSpec spec, WallClimbTuning tuning, float tickSeconds)
        {
            var up = MathF.Sqrt(2f * spec.Gravity * spec.JumpHeight);
            var result = Airborne(CharacterParkourState.None, normal * tuning.PushOffSpeed, up, spec.Gravity, tickSeconds);
            return new WallClimbResult(result.State, WallClimbMotion.PushedOff, result.Velocity, result.Displacement, Vector3.Zero);
        }

        // PlayerMotor と同じく縦は台形則で動かし、tick の長さによらず同じ高さにする
        private static WallClimbResult Airborne(
            CharacterParkourState state, Vector3 horizontal, float verticalStart, float gravity, float tickSeconds)
        {
            var verticalEnd = verticalStart - (gravity * tickSeconds);
            var displacement = new Vector3(
                horizontal.X * tickSeconds, (verticalStart + verticalEnd) * 0.5f * tickSeconds, horizontal.Z * tickSeconds);
            return new WallClimbResult(
                state, WallClimbMotion.Fell, new Vector3(horizontal.X, verticalEnd, horizontal.Z), displacement, Vector3.Zero);
        }
    }
}

using System;
using System.Numerics;

namespace Shooter.Core
{
    public enum WallKickOutcome : byte
    {
        None = 0,
        Kicked = 1,
        SameWall = 2,
    }

    public readonly struct WallKickResult
    {
        public WallKickResult(CharacterParkourState state, WallKickOutcome outcome, Vector3 velocity)
        {
            State = state;
            Outcome = outcome;

            // 蹴らなかった結果に速度を残すと、ふつうの移動に任せた tick で読む側が古い値を使いかねない
            Velocity = outcome == WallKickOutcome.Kicked ? velocity : Vector3.Zero;
        }

        public CharacterParkourState State { get; }

        public WallKickOutcome Outcome { get; }

        public bool Kicked => Outcome == WallKickOutcome.Kicked;

        public Vector3 Velocity { get; }

        public static WallKickResult Resting(CharacterParkourState state)
        {
            return new WallKickResult(state, WallKickOutcome.None, Vector3.Zero);
        }
    }

    // 壁を呼び手が CharacterWallSensor で調べて渡すのは、WallRunStep と同じく世界を引かない計算にして、壁の有無ごとの決まりを世界を組まずに確かめるため。
    // 位置の移動を返さず速度だけを返すのは、蹴った後は空中のふつうの動き (重力と空中の操作) に任せ、跳んだ高さを地上のジャンプと同じ積分で決めるため。
    public static class WallKickStep
    {
        public static WallKickResult Step(
            CharacterParkourState parkour,
            CharacterWallRunState wallRun,
            WallContact wall,
            Vector3 velocity,
            bool jump,
            bool grounded,
            WallKickTuning tuning,
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

            if (grounded)
            {
                return WallKickResult.Resting(Land(parkour));
            }

            // 壁走りからの跳び出しは WallRunStep が受け持つので、同じ tick のジャンプで二重に跳ばない。
            // 壁蹴り以外の動き (ぶら下がりなど) の最中も、その動きの状態を上書きしない。
            if (!tuning.Enabled || wallRun.IsRunning || (parkour.IsActive && parkour.Move != ParkourMove.WallKick))
            {
                return WallKickResult.Resting(parkour);
            }

            var airborne = parkour.Move == ParkourMove.WallKick
                ? new CharacterParkourState(
                    ParkourMove.WallKick, parkour.Elapsed + tickSeconds, parkour.WallNormal, parkour.LedgePoint, parkour.FallSpeedScale)
                : parkour;
            if (!jump || !wall.IsFound)
            {
                return WallKickResult.Resting(airborne);
            }

            // 同じ壁を蹴り直せると、壁際でジャンプを連打するだけで登り続けられるので、着地までは別の壁だけを蹴れる
            if (parkour.Move == ParkourMove.WallKick && WallRunStep.IsSameWall(parkour.WallNormal, wall.Normal))
            {
                return new WallKickResult(airborne, WallKickOutcome.SameWall, Vector3.Zero);
            }

            return new WallKickResult(
                new CharacterParkourState(ParkourMove.WallKick, 0f, wall.Normal, Vector3.Zero),
                WallKickOutcome.Kicked,
                KickVelocity(velocity, wall.Normal, tuning));
        }

        public static CharacterParkourState Land(CharacterParkourState parkour)
        {
            return parkour.Move == ParkourMove.WallKick ? CharacterParkourState.None : parkour;
        }

        // 壁に沿った勢いを捨てないのは、斜めに走り込んだ向きへそのまま跳び移れるようにするため。
        // 落ちる速さと壁へ向かう速さは捨てるのは、落ちながら蹴っても調整値どおりの高さと離れ方にするため。
        public static Vector3 KickVelocity(Vector3 velocity, Vector3 normal, WallKickTuning tuning)
        {
            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }

            var horizontal = WallRunStep.AlongWall(velocity, normal) + (normal * tuning.AwaySpeed);
            return new Vector3(horizontal.X, tuning.UpSpeed, horizontal.Z);
        }
    }
}

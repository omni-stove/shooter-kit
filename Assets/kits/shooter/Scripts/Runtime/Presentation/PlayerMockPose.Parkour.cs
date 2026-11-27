using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public static partial class PlayerMockPose
    {
        public const float WallKickTiltDegrees = 20f;

        // 経過の秒数はオンラインの体へ届かないので、回り続けるのではなく、前へ丸まった姿で止めて見せる。
        public const float LandingRollPitchDegrees = 60f;

        public const float WallClimbLeanDegrees = 15f;

        public const float LedgeClimbUpLeanDegrees = 35f;

        // ぶら下がる位置は当たり判定の頭のてっぺんをふちに合わせるので、上げた腕の分だけ体を下げないと、頭がふちから出る。
        public const float LedgeHangDrop = 0.35f;

        // 上げた腕の代わりに、銃を上へ向けて先がふちに届く高さへ置く。体を LedgeHangDrop だけ下げる分を足しておく。
        public static readonly Vector3 BodyHangGunPosition = new Vector3(0f, GameBootstrap.PlayerHeight + LedgeHangDrop - (GunSize.z / 2f), 0.15f);
        public static readonly Vector3 BodyHangGunEuler = new Vector3(-90f, 0f, 0f);

        public static bool TryParkourTilt(ParkourMove move, out Quaternion tilt)
        {
            return TryParkourTilt(move, Vector3.zero, out tilt);
        }

        // 傾けるときは true を返し、リーン・水泳・壁走りの傾きより先に使わせる。
        // 知らない番号で投げないのは、PlayerMockBody が読む前に確かめるのはリーンと壁走りだけで、投げると毎フレーム体の動きごと止まるため。
        // 壁の法線はスナップショットに載らないので、分からないとき (0) の壁蹴りは前の壁を蹴ったとみなして後ろへ傾ける。
        public static bool TryParkourTilt(ParkourMove move, Vector3 localWallNormal, out Quaternion tilt)
        {
            switch (move)
            {
                case ParkourMove.WallKick:
                    var away = Horizontal(localWallNormal);
                    tilt = LeanToward(away == Vector3.zero ? Vector3.back : away, WallKickTiltDegrees);
                    return true;
                case ParkourMove.LandingRoll:
                    tilt = Quaternion.Euler(LandingRollPitchDegrees, 0f, 0f);
                    return true;
                case ParkourMove.WallClimb:
                    tilt = Quaternion.Euler(WallClimbLeanDegrees, 0f, 0f);
                    return true;
                case ParkourMove.LedgeHang:
                case ParkourMove.LedgeShimmy:
                    // ぶら下がりは傾けず真っすぐ吊るす。true を返すのは、ふちを掴んでいる間にリーンの傾きを残さないため。
                    tilt = Quaternion.identity;
                    return true;
                case ParkourMove.LedgeClimbUp:
                    tilt = Quaternion.Euler(LedgeClimbUpLeanDegrees, 0f, 0f);
                    return true;
                default:
                    tilt = Quaternion.identity;
                    return false;
            }
        }

        public static bool IsHangingFromLedge(ParkourMove move)
        {
            return move == ParkourMove.LedgeHang || move == ParkourMove.LedgeShimmy;
        }

        public static float ParkourDrop(ParkourMove move)
        {
            return IsHangingFromLedge(move) ? LedgeHangDrop : 0f;
        }

        public static bool TryParkourGun(ParkourMove move, out MockPartPose gun)
        {
            if (!IsHangingFromLedge(move))
            {
                gun = default;
                return false;
            }

            gun = new MockPartPose(BodyHangGunPosition, Quaternion.Euler(BodyHangGunEuler));
            return true;
        }
    }
}

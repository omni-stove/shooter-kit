using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public static partial class PlayerMockPose
    {
        // 傾けるときは true を返し、リーン・水泳・壁走りの傾きより先に使わせる。
        // 今どの動きも傾けないのは、動きの中身がまだ無く、傾ける向き (蹴った壁・掴んだふち) が決まらないため。
        // 知らない番号で投げないのは、PlayerMockBody が読む前に確かめるのはリーンと壁走りだけで、投げると毎フレーム体の動きごと止まるため。
        public static bool TryParkourTilt(ParkourMove move, out Quaternion tilt)
        {
            tilt = Quaternion.identity;
            return false;
        }
    }
}

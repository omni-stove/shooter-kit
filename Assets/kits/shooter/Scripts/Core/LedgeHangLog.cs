using System;
using System.Globalization;
using System.Numerics;

namespace Shooter.Core
{
    public static class LedgeHangLog
    {
        public const string LogTag = "[LedgeHang]";

        public const string GrabPrefix = LogTag + " grab ";

        public const string EdgePrefix = LogTag + " edge ";

        public const string ClimbPrefix = LogTag + " climb ";

        public const string TopPrefix = LogTag + " top ";

        public const string LetGoPrefix = LogTag + " letgo ";

        public static string Grab(Vector3 ledgePoint, Vector3 wallNormal)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}ledge={1} normal={2}", GrabPrefix, Format(ledgePoint), Format(wallNormal));
        }

        public static string Edge(Vector3 ledgePoint)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}ledge={1}", EdgePrefix, Format(ledgePoint));
        }

        public static string Climb(Vector3 ledgePoint)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}ledge={1}", ClimbPrefix, Format(ledgePoint));
        }

        public static string Top(Vector3 position)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}position={1}", TopPrefix, Format(position));
        }

        public static string LetGo(LedgeLetGoReason reason, Vector3 ledgePoint)
        {
            if (reason == LedgeLetGoReason.None)
            {
                throw new ArgumentOutOfRangeException(nameof(reason), reason, "None is not a reason to let go of a ledge");
            }

            return string.Format(CultureInfo.InvariantCulture, "{0}reason={1} ledge={2}", LetGoPrefix, reason, Format(ledgePoint));
        }

        // 掴んでいる間は毎 tick 結果が出るので、行に残すのは動きが変わった tick だけにする。端で止まったのも、横へ動いていた所から止まった tick だけ。
        public static string ForStep(CharacterParkourState before, LedgeHangResult result)
        {
            switch (result.Motion)
            {
                case LedgeHangMotion.Grabbed:
                    return Grab(result.State.LedgePoint, result.State.WallNormal);
                case LedgeHangMotion.StoppedAtEdge:
                    return before.Move == ParkourMove.LedgeShimmy ? Edge(result.State.LedgePoint) : null;
                case LedgeHangMotion.ClimbingUp:
                    return before.Move == ParkourMove.LedgeClimbUp ? null : Climb(result.State.LedgePoint);
                case LedgeHangMotion.ClimbedUp:
                    return Top(result.Position);
                case LedgeHangMotion.LetGo:
                    return LetGo(result.LetGoReason, result.State.LedgePoint);
                default:
                    return null;
            }
        }

        private static string Format(Vector3 v)
        {
            return string.Format(CultureInfo.InvariantCulture, "({0:0.00}, {1:0.00}, {2:0.00})", v.X, v.Y, v.Z);
        }
    }
}

using System;
using System.Globalization;

namespace Shooter.Core
{
    public static class WallClimbLog
    {
        public const string Tag = "[WallClimb]";

        public const string StartPrefix = Tag + " start ";

        public const string EndPrefix = Tag + " end ";

        public static string Start(float intoWallSpeed, float topHeight)
        {
            return string.Format(
                CultureInfo.InvariantCulture, "{0}speed={1:0.00} top={2}", StartPrefix, intoWallSpeed, Height(topHeight));
        }

        public static string End(WallClimbMotion reason, float seconds, float height)
        {
            if (reason == WallClimbMotion.None || reason == WallClimbMotion.Climbing)
            {
                throw new ArgumentOutOfRangeException(nameof(reason), reason, "an end needs Fell, ReachedLedge or PushedOff");
            }

            return string.Format(
                CultureInfo.InvariantCulture, "{0}reason={1} seconds={2:0.00} height={3:0.00}", EndPrefix, reason, seconds, height);
        }

        // 秒数と高さを問わずに探せるよう、秒数の前までを返す。
        public static string EndPrefixFor(WallClimbMotion reason)
        {
            return $"{EndPrefix}reason={reason} ";
        }

        // 上端が調べた高さより上の壁は無限大で渡るので、数の代わりに - と書く
        private static string Height(float height)
        {
            return float.IsInfinity(height) || float.IsNaN(height) ? "-" : height.ToString("0.00", CultureInfo.InvariantCulture);
        }
    }
}

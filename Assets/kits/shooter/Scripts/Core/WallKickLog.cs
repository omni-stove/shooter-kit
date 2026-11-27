using System;
using System.Globalization;
using System.Numerics;

namespace Shooter.Core
{
    public static class WallKickLog
    {
        public const string Tag = "[WallKick]";

        public const string KickedPrefix = Tag + " kicked ";
        public const string SameWallPrefix = Tag + " sameWall ";

        public static string Kicked(WallContact wall, Vector3 velocity)
        {
            RequireWall(wall);
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}side={1} normal={2} velocity={3}",
                KickedPrefix,
                wall.Side,
                Format(wall.Normal),
                Format(velocity));
        }

        public static string SameWall(WallContact wall)
        {
            RequireWall(wall);
            return string.Format(CultureInfo.InvariantCulture, "{0}side={1} normal={2}", SameWallPrefix, wall.Side, Format(wall.Normal));
        }

        private static void RequireWall(WallContact wall)
        {
            if (!wall.IsFound)
            {
                throw new ArgumentException("a wall kick line needs the wall that was kicked", nameof(wall));
            }
        }

        private static string Format(Vector3 v)
        {
            return string.Format(CultureInfo.InvariantCulture, "({0:0.00}, {1:0.00}, {2:0.00})", v.X, v.Y, v.Z);
        }
    }
}

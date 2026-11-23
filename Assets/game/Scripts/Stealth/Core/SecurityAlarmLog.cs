using System;
using System.Globalization;
using System.Numerics;

namespace Shooter.Core
{
    public static class SecurityAlarmLog
    {
        public const string Tag = "[SecurityAlarm]";

        public static string Prefix(string cameraId)
        {
            return $"{Tag} camera={Name(cameraId)}";
        }

        public static string Started(string cameraId, Vector3 at, float seconds, float radius)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} raised at={1} seconds={2:0.0} radius={3:0.0}",
                Prefix(cameraId),
                Point(at),
                seconds,
                radius);
        }

        public static string Extended(string cameraId, Vector3 at, float seconds)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} extended at={1} seconds={2:0.0}",
                Prefix(cameraId),
                Point(at),
                seconds);
        }

        public static string Raised(SecurityAlarmRaise change, string cameraId, Vector3 at, SecurityAlarmSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            return change == SecurityAlarmRaise.Started
                ? Started(cameraId, at, spec.DurationSeconds, spec.Radius)
                : Extended(cameraId, at, spec.DurationSeconds);
        }

        public static string Stopped(string cameraId, Vector3 at)
        {
            return $"{Prefix(cameraId)} stopped at={Point(at)}";
        }

        // 敵の側の行は EnemyStealth.LogTag で始める。敵 1 人の動きを [Stealth] の行だけで追えるように。
        public const string EnemyTag = "[Stealth]";

        public static string ReceivedPrefix(string enemyId)
        {
            return $"{EnemyTag} id={Name(enemyId)} security-alarm ";
        }

        public static string Received(string enemyId, string cameraId, Vector3 at, HearingOutcome outcome, Stealth rule)
        {
            return $"{ReceivedPrefix(enemyId)}camera={Name(cameraId)} at={Point(at)} outcome={outcome} rule={rule}";
        }

        private static string Point(Vector3 value)
        {
            return string.Format(CultureInfo.InvariantCulture, "({0:0.00}, {1:0.00}, {2:0.00})", value.X, value.Y, value.Z);
        }

        private static string Name(string id)
        {
            return string.IsNullOrEmpty(id) ? "-" : id;
        }
    }
}

using System.Globalization;
using Shooter.Core;

namespace Shooter.Editor
{
    // カメラと警報の行は [SecurityCamera]・[SecurityAlarm] で出るが、通しの流れを見張りの行 ([Stealth]) と並べて追えるよう、確かめ終えた順に [Stealth] の行を足す。
    public static class SecurityCameraSmokeLog
    {
        public const string Tag = SecurityAlarmLog.EnemyTag;

        public const string Prefix = Tag + " smoke=security-camera";

        public static string Line(SecurityCameraSmokeCheck check)
        {
            return $"{Prefix} check={check}";
        }

        public static string Spotted(string cameraId, float seconds)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} camera={1} seconds={2:0.00}", Line(SecurityCameraSmokeCheck.Spotted), Name(cameraId), seconds);
        }

        public static string AlarmRaised(string cameraId, float radius)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} camera={1} radius={2:0.0}", Line(SecurityCameraSmokeCheck.AlarmRaised), Name(cameraId), radius);
        }

        public static string Investigated(string enemyId, string ignoredEnemyId, float approached)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} enemy={1} approached={2:0.00} ignored={3}",
                Line(SecurityCameraSmokeCheck.Investigated),
                Name(enemyId),
                approached,
                Name(ignoredEnemyId));
        }

        public static string Destroyed(string cameraId, int shots)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} camera={1} shots={2}", Line(SecurityCameraSmokeCheck.Destroyed), Name(cameraId), shots);
        }

        public static string StayedQuiet(string cameraId, float heldSeconds, float watchedSeconds)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} camera={1} held={2:0.00} watched={3:0.00}",
                Line(SecurityCameraSmokeCheck.StayedQuiet),
                Name(cameraId),
                heldSeconds,
                watchedSeconds);
        }

        private static string Name(string id)
        {
            return string.IsNullOrEmpty(id) ? "-" : id;
        }
    }

    public enum SecurityCameraSmokeCheck
    {
        Spotted,
        AlarmRaised,
        Investigated,
        Destroyed,
        StayedQuiet,
    }
}

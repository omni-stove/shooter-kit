using System;
using System.Globalization;

namespace Shooter.Core
{
    public static class WeaponFreeZoneLog
    {
        public const string Tag = "[WeaponFreeZone]";

        public const string ExitedReason = "exited";

        public const string UnarmedReason = "unarmed";

        public const string ZoneReason = "zone";

        public const string ShotReason = "shot";

        public static string Loaded(string zoneId, WeaponFreeZoneSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} zone={1} reaction={2} hostileAfter={3:0.00}",
                Tag,
                Name(zoneId),
                spec.Reaction,
                spec.HostileAfterSeconds);
        }

        public static string InvalidSetting(string zoneId, string message)
        {
            return $"{Tag} zone={Name(zoneId)} has an invalid setting: {message}";
        }

        public static string Entered(string zoneId, string holderId, bool armed)
        {
            return $"{Tag} zone={Name(zoneId)} holder={Name(holderId)} entered armed={(armed ? "true" : "false")}";
        }

        public static string Exited(string zoneId, string holderId)
        {
            return $"{Tag} zone={Name(zoneId)} holder={Name(holderId)} exited";
        }

        public static string WarningBegan(string zoneId, WeaponFreeZoneReaction reaction, float secondsToHostile)
        {
            return string.Format(
                CultureInfo.InvariantCulture, "{0} zone={1} warning reaction={2} secondsToHostile={3:0.00}", Tag, Name(zoneId), reaction, secondsToHostile);
        }

        public static string WarningCleared(string zoneId, string reason)
        {
            return $"{Tag} zone={Name(zoneId)} warning cleared reason={Name(reason)}";
        }

        public static string Hostile(string zoneId)
        {
            return $"{Tag} zone={Name(zoneId)} hostile";
        }

        public static string AutoHolstered(string zoneId, string holderId, bool started)
        {
            return $"{Tag} zone={Name(zoneId)} holder={Name(holderId)} autoHolster={(started ? "began" : "skipped")}";
        }

        public static string DrawBlocked(string zoneId, string holderId, bool blocked)
        {
            return $"{Tag} zone={Name(zoneId)} holder={Name(holderId)} blockDraw={(blocked ? "on" : "off")}";
        }

        public static string NoHolster(string zoneId, string holderId, WeaponFreeZoneReaction reaction)
        {
            return $"{Tag} zone={Name(zoneId)} holder={Name(holderId)} has no HolsterController; reaction={reaction} cannot holster or block drawing";
        }

        public static string GuardPhase(string guardId, string zoneId, NeutralGuardPhase phase, string reason)
        {
            return $"{Tag} guard={Name(guardId)} zone={Name(zoneId)} phase={phase} reason={Name(reason)}";
        }

        public static string GuardWithoutZone(string guardId, string zoneId)
        {
            return $"{Tag} guard={Name(guardId)} zone={Name(zoneId)} is not in the scene; the guard stays neutral until it is shot";
        }

        private static string Name(string id)
        {
            return string.IsNullOrEmpty(id) ? "-" : id;
        }
    }
}

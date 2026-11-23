using System.Globalization;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    internal static class ServerCombatLog
    {
        public static string Dealt(int? attackerId, int victimId, string weaponId, int amount, HitZone zone, DamageOutcome outcome)
        {
            return ServerCombat.LogTag + " dealt attacker=" + (attackerId.HasValue ? Text(attackerId.Value) : "none")
                + " victim=" + Text(victimId) + " weapon=" + (string.IsNullOrEmpty(weaponId) ? "none" : weaponId)
                + " zone=" + zone + " dmg=" + Text(amount) + " outcome=" + outcome;
        }

        public static string ReloadInterrupted(int playerId, ReloadInterrupt by)
        {
            return ServerCombat.LogTag + " reload-cancelled player=" + Text(playerId) + " by=" + by;
        }

        public static string WeaponActionHolstered(int playerId, WeaponAction action)
        {
            return ServerCombat.LogTag + " ignored " + action + " player=" + Text(playerId) + " reason=" + ShotRejection.Holstered;
        }

        public static string ShieldBlocked(int holderId, int shooterId, string weaponId, int damage)
        {
            return ServerCombat.LogTag + " riot-shield blocked id=" + Text(holderId) + " attacker=" + Text(shooterId)
                + " weapon=" + (string.IsNullOrEmpty(weaponId) ? "none" : weaponId) + " dmg=" + Text(damage);
        }

        // 武器の部分は WeaponJamLog の行をそのまま使い、オフラインのログと同じ書式で突き合わせられるようにする
        public static string Jammed(int playerId, string weaponId, int sustainedShots, float chance)
        {
            return JamPrefix(playerId) + WeaponJamLog.Jammed(weaponId, sustainedShots, chance);
        }

        public static string JamClearStarted(int playerId, string weaponId, float clearSeconds)
        {
            return JamPrefix(playerId) + WeaponJamLog.ClearStarted(weaponId, clearSeconds);
        }

        public static string JamClearCanceled(int playerId, string weaponId, float remainingSeconds)
        {
            return JamPrefix(playerId) + WeaponJamLog.ClearCanceled(weaponId, remainingSeconds);
        }

        public static string JamCleared(int playerId, string weaponId)
        {
            return JamPrefix(playerId) + WeaponJamLog.Cleared(weaponId);
        }

        private static string JamPrefix(int playerId) => ServerCombat.LogTag + " player=" + Text(playerId) + " ";

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}

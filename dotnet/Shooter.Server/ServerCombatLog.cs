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

        public static string AkimboFire(int playerId, string weaponId, AkimboHand hand, int ammoInMagazine, int offhandAmmoInMagazine)
        {
            return ServerCombat.LogTag + " akimbo fire player=" + Text(playerId) + " weapon=" + weaponId + " hand=" + AkimboLog.HandName(hand)
                + " right=" + Text(ammoInMagazine) + " left=" + Text(offhandAmmoInMagazine);
        }

        public static string AkimboReloadStart(int playerId, string weaponId, AkimboHand hand, bool automatic, int ammoInMagazine, int reserveAmmo)
        {
            return ServerCombat.LogTag + " akimbo reload start player=" + Text(playerId) + " weapon=" + weaponId + " hand=" + AkimboLog.HandName(hand)
                + " auto=" + (automatic ? "true" : "false") + " ammo=" + Text(ammoInMagazine) + " reserve=" + Text(reserveAmmo);
        }

        public static string AkimboReloaded(int playerId, string weaponId, AkimboHand hand, int ammoInMagazine, int reserveAmmo)
        {
            return ServerCombat.LogTag + " akimbo reloaded player=" + Text(playerId) + " weapon=" + weaponId + " hand=" + AkimboLog.HandName(hand)
                + " ammo=" + Text(ammoInMagazine) + " reserve=" + Text(reserveAmmo);
        }

        // 二丁持ちでない武器への左の手。weaponId は手に持っている武器で、送られてきた文字列は出さない
        public static string NotAkimbo(int playerId, string weaponId, AkimboHand hand)
        {
            return ServerCombat.LogTag + " rejected shot player=" + Text(playerId) + " weapon=" + (string.IsNullOrEmpty(weaponId) ? "none" : weaponId)
                + " hand=" + AkimboLog.HandName(hand) + " reason=" + ShotRejection.NotAkimbo;
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

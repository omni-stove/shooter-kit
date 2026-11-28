using System;
using System.Globalization;
using GameKit.Core;

namespace Shooter.Core
{
    // MatchLog (GameKit.Core) に置かないのは、ルール・持ち物・武器の行がシューターの型 (GameRules・ServerLoadout・WeaponSpec) を読み、core はそれを知らないため。
    // 行の頭は MatchLog.Tag のままにして、サーバーとクライアントのログを突き合わせる読み手が今までどおり [Match] の行として読めるようにする。
    public static class MatchSetupLog
    {
        public static string RulesChanged(MatchObjective fromObjective, MatchObjective toObjective)
        {
            return MatchLog.Tag + " rules changed objective from=" + fromObjective + " to=" + toObjective;
        }

        public static string Loadout(string name, ServerLoadout loadout)
        {
            if (loadout == null)
            {
                throw new ArgumentNullException(nameof(loadout));
            }

            return MatchLog.Tag + " loadout name=" + (string.IsNullOrWhiteSpace(name) ? "-" : name)
                + " slots=" + loadout.Weapons.Count.ToString(CultureInfo.InvariantCulture)
                + " switch=" + Seconds(loadout.SwitchSeconds);
        }

        public static string LoadoutCatalog(string name, int weaponCount)
        {
            return MatchLog.Tag + " loadout catalog name=" + (string.IsNullOrWhiteSpace(name) ? "-" : name)
                + " weapons=" + weaponCount.ToString(CultureInfo.InvariantCulture);
        }

        public static string Rules(string name, GameRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            return MatchLog.Tag + " rules name=" + (string.IsNullOrWhiteSpace(name) ? "-" : name)
                + " friendlyFire=" + rules.FriendlyFire
                + " respawnMode=" + rules.RespawnMode
                + " waveInterval=" + Seconds(rules.WaveIntervalSeconds)
                + " regenDelay=" + Seconds(rules.RegenDelaySeconds)
                + " regenPerSecond=" + rules.RegenPerSecond.ToString("F2", CultureInfo.InvariantCulture)
                + " deathBody=" + rules.DeathBody
                + " deathCamera=" + rules.DeathCamera
                + " infiniteReserve=" + (rules.InfiniteReserve ? "true" : "false")
                + " reserveMultiplier=" + rules.ReserveAmmoMultiplier.ToString("F2", CultureInfo.InvariantCulture)
                + " destruction=" + rules.Destruction;
        }

        public static string Weapon(int slot, WeaponSpec weapon)
        {
            if (weapon == null)
            {
                throw new ArgumentNullException(nameof(weapon));
            }

            return MatchLog.Tag + " weapon slot=" + slot.ToString(CultureInfo.InvariantCulture)
                + " name=" + weapon.LogName
                + " id=" + weapon.Id
                + " category=" + weapon.Category
                + " mode=" + weapon.FireMode
                + " damage=" + weapon.Damage.ToString(CultureInfo.InvariantCulture)
                + " range=" + weapon.Range.ToString("F1", CultureInfo.InvariantCulture) + "m"
                + " falloff=" + Falloff(weapon.Falloff)
                + " fireInterval=" + Seconds(weapon.FireInterval)
                + " magazine=" + weapon.MagazineSize.ToString(CultureInfo.InvariantCulture)
                + " reserve=" + weapon.ReserveAmmo.ToString(CultureInfo.InvariantCulture)
                + " reload=" + Seconds(weapon.ReloadSeconds);
        }

        // key=value を空白で区切って読めるよう、値に空白を入れない形にする。
        private static string Falloff(DamageFalloff falloff)
        {
            // DamageFalloff.None は float.MaxValue の距離を持つので、そのまま書くと読めない
            if (falloff.MinMultiplier >= 1f)
            {
                return "none";
            }

            return falloff.StartDistance.ToString("F1", CultureInfo.InvariantCulture)
                + "-" + falloff.EndDistance.ToString("F1", CultureInfo.InvariantCulture)
                + "m*" + falloff.MinMultiplier.ToString("F2", CultureInfo.InvariantCulture);
        }

        private static string Seconds(float seconds)
        {
            return seconds.ToString("F2", CultureInfo.InvariantCulture) + "s";
        }
    }
}

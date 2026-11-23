using System.Globalization;
using Shooter.Core;

namespace Shooter.Server
{
    // 頭はオフラインと同じ WeaponLightLog.Prefix にそろえ、同じ grep でオフラインとサーバーの行を拾えるようにする
    internal static class ServerWeaponLightLog
    {
        public static string Started()
        {
            return WeaponLightLog.Prefix + " server ready";
        }

        public static string TurnedOn(int playerId, string weaponId)
        {
            return string.Format(
                CultureInfo.InvariantCulture, "{0} server player={1} weapon={2} on=true", WeaponLightLog.Prefix, playerId, Id(weaponId));
        }

        public static string TurnedOff(int playerId, string weaponId, ServerWeaponLight.OffReason reason)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} server player={1} weapon={2} on=false reason={3}",
                WeaponLightLog.Prefix, playerId, Id(weaponId), reason);
        }

        public static string Refused(int playerId, string weaponId, ServerWeaponLight.Refusal reason)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} server refused player={1} weapon={2} reason={3}",
                WeaponLightLog.Prefix, playerId, Id(weaponId), reason);
        }

        private static string Id(string id)
        {
            return string.IsNullOrEmpty(id) ? "-" : id;
        }
    }
}

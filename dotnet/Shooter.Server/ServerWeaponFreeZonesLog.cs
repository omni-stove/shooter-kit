using System.Globalization;
using Shooter.Core;

namespace Shooter.Server
{
    // タグはオフラインと同じ WeaponFreeZoneLog.Tag にそろえ、同じ grep でオフラインとサーバーの行を拾えるようにする
    internal static class ServerWeaponFreeZonesLog
    {
        public static string Loaded(string map, int zones)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} server map={1} zones={2}", WeaponFreeZoneLog.Tag, Name(map), zones);
        }

        public static string Zone(MapWeaponFreeZone zone, WeaponFreeZoneReaction online)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} server zone={1} reaction={2} online={3}",
                WeaponFreeZoneLog.Tag,
                Name(zone.Id),
                zone.Reaction,
                online);
        }

        public static string Entered(string zoneId, int playerId, bool armed)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} server zone={1} player={2} entered armed={3}",
                WeaponFreeZoneLog.Tag,
                Name(zoneId),
                playerId,
                armed ? "true" : "false");
        }

        public static string Exited(string zoneId, int playerId)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} server zone={1} player={2} exited", WeaponFreeZoneLog.Tag, Name(zoneId), playerId);
        }

        public static string WarningBegan(string zoneId, int playerId)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} server zone={1} player={2} warning", WeaponFreeZoneLog.Tag, Name(zoneId), playerId);
        }

        public static string WarningCleared(string zoneId, int playerId, string reason)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} server zone={1} player={2} warning cleared reason={3}",
                WeaponFreeZoneLog.Tag,
                Name(zoneId),
                playerId,
                Name(reason));
        }

        public static string AutoHolstered(string zoneId, int playerId, ServerHolster.Refusal refusal)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} server zone={1} player={2} autoHolster={3}",
                WeaponFreeZoneLog.Tag,
                Name(zoneId),
                playerId,
                refusal == ServerHolster.Refusal.None ? "began" : "skipped reason=" + refusal);
        }

        private static string Name(string id)
        {
            return string.IsNullOrEmpty(id) ? "-" : id;
        }
    }
}

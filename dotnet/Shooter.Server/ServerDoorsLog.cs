using System.Globalization;
using Shooter.Core;

namespace Shooter.Server
{
    // 行の頭はオフラインと同じタグ (BreachLog.Tag) にし、オフラインと同じ語で検索できるようにする。
    // ドアは .doors.json の並びの番号、人はプレイヤーの番号で書く
    internal static class ServerDoorsLog
    {
        public const string LogTag = BreachLog.Tag;

        public static string Started(BreachSpec spec, string specPath)
        {
            return BreachLog.Loaded(spec) + " from=" + (specPath ?? "default");
        }

        public static string MapLoaded(string map, int doors)
        {
            return LogTag + " server map=" + map + " doors=" + Text(doors);
        }

        public static string TooManyDoors(string map, int doors, int max)
        {
            return LogTag + " server map=" + map + " has " + Text(doors) + " doors but at most " + Text(max)
                + " can be sent; every door stays closed";
        }

        public static string KickBegan(int door, int playerId, float kickSeconds)
        {
            return string.Format(
                CultureInfo.InvariantCulture, "{0} door={1} kick-began by={2} kickSeconds={3:0.00}", LogTag, door, playerId, kickSeconds);
        }

        public static string KickCancelled(int door, int playerId, ServerDoors.CancelReason reason)
        {
            return LogTag + " door=" + Text(door) + " kick-cancelled by=" + Text(playerId) + " reason=" + reason;
        }

        public static string Kicked(int door, int playerId)
        {
            return BreachLog.Kicked(Text(door), Text(playerId));
        }

        public static string PlantBegan(int door, int playerId, BreachSpec spec)
        {
            return BreachLog.PlantBegan(Text(door), Text(playerId), spec.PlantSeconds, spec.FuseSeconds);
        }

        public static string PlantCancelled(int door, int playerId, ServerDoors.CancelReason reason)
        {
            return BreachLog.PlantCancelled(Text(door), Text(playerId), reason.ToString());
        }

        public static string Armed(int door, int playerId, float fuseSeconds)
        {
            return BreachLog.Armed(Text(door), Text(playerId), fuseSeconds);
        }

        public static string ChargeUsed(int door, int playerId, string itemId)
        {
            return LogTag + " door=" + Text(door) + " charge-used by=" + Text(playerId) + " item=" + itemId;
        }

        // 仕掛けた人が抜けた後に爆ぜた爆薬は、破った人を書かない
        public static string Detonated(int door, int? playerId, BreachSpec spec)
        {
            return BreachLog.Detonated(Text(door), playerId.HasValue ? Text(playerId.Value) : null, spec.ChargeDamage, spec.ChargeRadius);
        }

        public static string Staggered(int door, BreachMethod method, BreachSpec spec, int count)
        {
            return BreachLog.Staggered(Text(door), method, BreachSpec.StaggerKindFor(method), spec.StaggerSecondsFor(method), count);
        }

        public static string Refused(int door, int playerId, BreachMethod method, BreachDoorPhase phase, ServerDoors.Refusal reason)
        {
            return BreachLog.Refused(Text(door), Text(playerId), method, phase, reason.ToString());
        }

        public static string UnknownDoor(int door, int playerId, BreachMethod method, int count)
        {
            return LogTag + " door=" + Text(door) + " refused by=" + Text(playerId) + " method=" + method
                + " reason=" + ServerDoors.Refusal.UnknownDoor + " doors=" + Text(count);
        }

        private static string Text(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}

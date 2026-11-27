using System.Globalization;

namespace Shooter.NetV2
{
    public static class ClientVipLog
    {
        public static string MarkerShown(int playerId)
        {
            return MatchClient.LogTag + " vip marker shown player=" + Number(playerId);
        }

        public static string MarkerRemoved(int playerId)
        {
            return MatchClient.LogTag + " vip marker removed player=" + Number(playerId);
        }

        private static string Number(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}

using System.Globalization;

namespace Shooter.NetV2
{
    public static class ClientJuggernautLog
    {
        public static string MarkerShown(int playerId)
        {
            return MatchClient.LogTag + " juggernaut marker shown player=" + Number(playerId);
        }

        public static string MarkerRemoved(int playerId)
        {
            return MatchClient.LogTag + " juggernaut marker removed player=" + Number(playerId);
        }

        private static string Number(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}

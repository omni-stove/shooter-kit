using System;
using System.Globalization;
using Shooter.Core;

namespace Shooter.Server
{
    internal static class JuggernautRulesLog
    {
        public const string Tag = ServerModeTuningLog.JuggernautTag;

        public static string Started(JuggernautSpec spec, float respawnSeconds)
        {
            ArgumentNullException.ThrowIfNull(spec);
            return Tag + " net respawn=" + Text(respawnSeconds)
                + " body maxHealth=" + Text(spec.JuggernautMaxHealth)
                + " speedMultiplier=" + Text(spec.JuggernautSpeedMultiplier);
        }

        public static string Changed(JuggernautChange change)
        {
            return Tag + " change player=" + Text(change.JuggernautId)
                + " from=" + (change.PreviousId.HasValue ? Text(change.PreviousId.Value) : JuggernautLog.Nobody)
                + " reason=" + JuggernautLog.ReasonText(change.Reason);
        }

        public static string Body(int playerId, JuggernautSpec spec)
        {
            ArgumentNullException.ThrowIfNull(spec);
            return Tag + " juggernaut body player=" + Text(playerId)
                + " maxHealth=" + Text(spec.JuggernautMaxHealth)
                + " speedMultiplier=" + Text(spec.JuggernautSpeedMultiplier);
        }

        public static string Restored(int playerId)
        {
            return Tag + " body restored player=" + Text(playerId);
        }

        public static string Score(int leadingScore, int juggernautScore)
        {
            return Tag + " score leading=" + Text(leadingScore) + " juggernaut=" + Text(juggernautScore);
        }

        public static string Winner(int? winnerId, int winnerScore)
        {
            return winnerId.HasValue
                ? Tag + " winner player=" + Text(winnerId.Value) + " score=" + Text(winnerScore)
                : Tag + " winner=" + MatchLog.Draw;
        }

        public static string Restart()
        {
            return Tag + " restart";
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Text(float value) => value.ToString(CultureInfo.InvariantCulture);
    }
}

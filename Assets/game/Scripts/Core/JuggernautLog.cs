using System;
using System.Globalization;

namespace Shooter.Core
{
    public static class JuggernautLog
    {
        public const string LogPrefix = ModeLog.Tag + " juggernaut";

        public const string Nobody = "-";

        public static string Changed(JuggernautChange change)
        {
            return LogPrefix + " change player=" + Number(change.JuggernautId)
                + " from=" + (change.PreviousId.HasValue ? Number(change.PreviousId.Value) : Nobody)
                + " reason=" + ReasonText(change.Reason);
        }

        public static string Score(int playerId, int score)
        {
            return LogPrefix + " score player=" + Number(playerId) + " score=" + Number(score);
        }

        public static string Ended(int? winnerId, int winnerScore)
        {
            return winnerId.HasValue
                ? LogPrefix + " end winner=" + Number(winnerId.Value) + " score=" + Number(winnerScore)
                : LogPrefix + " end winner=" + ModeLog.Draw;
        }

        public static string ReasonText(JuggernautChangeReason reason)
        {
            switch (reason)
            {
                case JuggernautChangeReason.Chosen:
                    return "chosen";
                case JuggernautChangeReason.TakenOver:
                    return "taken-over";
                case JuggernautChangeReason.Repicked:
                    return "repicked";
                case JuggernautChangeReason.Left:
                    return "left";
                default:
                    throw new ArgumentOutOfRangeException(nameof(reason), reason, "unknown juggernaut change reason");
            }
        }

        private static string Number(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}

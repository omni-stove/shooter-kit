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

        // 速さの倍率は bot (EnemyAgent) にしか掛からないので、掛けなかった人は Nobody と書く。
        public static string Body(int playerId, int maxHealth, float? speedMultiplier)
        {
            return LogPrefix + " body player=" + Number(playerId) + " max-health=" + Number(maxHealth) + " speed=" + Multiplier(speedMultiplier);
        }

        public static string Restored(int playerId, int maxHealth, float? speedMultiplier)
        {
            return LogPrefix + " restore player=" + Number(playerId) + " max-health=" + Number(maxHealth) + " speed=" + Multiplier(speedMultiplier);
        }

        public static string BotTarget(int playerId, int targetId, bool asJuggernaut)
        {
            return LogPrefix + " bot-target player=" + Number(playerId) + " target=" + Number(targetId)
                + " role=" + (asJuggernaut ? "juggernaut" : "hunter");
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

        private static string Multiplier(float? value)
        {
            return value.HasValue ? value.Value.ToString("0.###", CultureInfo.InvariantCulture) : Nobody;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Shooter.Core;

namespace Shooter.Server
{
    internal static class VipRulesLog
    {
        public const string Tag = ServerModeTuningLog.VipTag;

        public static string Started(IReadOnlyList<NetMapExtractionPoint> exits)
        {
            ArgumentNullException.ThrowIfNull(exits);
            var text = new StringBuilder(Tag).Append(" net exits=");
            for (var i = 0; i < exits.Count; i++)
            {
                if (i > 0)
                {
                    text.Append(',');
                }

                text.Append(exits[i].Id);
            }

            return text.ToString();
        }

        public static string NoExtractionPoints(string mapName)
        {
            return Tag + " map=" + mapName + " refused: it has no extraction points for the vip to escape to";
        }

        public static string TooManyExtractionPoints(string mapName, int count, int max)
        {
            return Tag + " map=" + mapName + " refused: it has " + Text(count) + " extraction points but at most " + Text(max) + " fit in a snapshot";
        }

        public static string Round(int round, Side attacker, Side defender) => VipLog.RoundLog(round, attacker, defender);

        public static string Live(int round) => Tag + " live round=" + Text(round);

        public static string Chosen(int round, int playerId, int maxHealth)
        {
            return VipLog.ChosenLog(round, playerId) + " maxHealth=" + Text(maxHealth);
        }

        public static string Restored(int playerId) => Tag + " body restored player=" + Text(playerId);

        public static string Escaped(int playerId) => VipLog.EscapedLog(playerId);

        public static string RoundEnded(int round, Side winner, Side defender, VipRoundEnd reason, int alphaWins, int bravoWins)
        {
            return VipLog.RoundEndLog(round, winner, defender, reason, alphaWins, bravoWins);
        }

        public static string Swapped(Side attacker, Side defender) => VipLog.SwapLog(attacker, defender);

        public static string Ended(Side winner) => VipLog.EndedLog(winner);

        public static string Restart() => Tag + " restart";

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}

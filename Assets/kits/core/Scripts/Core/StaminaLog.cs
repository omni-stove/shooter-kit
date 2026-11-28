using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GameKit.Core
{
    public static class StaminaLog
    {
        public const string LogTag = "[Stamina]";

        public static string FormatConfigured(Stamina rule, StaminaSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var line = string.Format(
                CultureInfo.InvariantCulture,
                "{0} rule={1} max={2:0.00} sprintPerSecond={3:0.00} regenPerSecond={4:0.00} regenDelay={5:0.00} recover={6:0.00}",
                LogTag,
                rule,
                spec.Max,
                spec.SprintCostPerSecond,
                spec.RegenPerSecond,
                spec.RegenDelaySeconds,
                spec.RecoverAmount);

            // 辞書の並びに任せず並べ直すのは、同じ調整値ならいつも同じ行になり、ログを比べられるようにするため。
            var uses = new List<string>(spec.UseCosts.Keys);
            uses.Sort(StringComparer.Ordinal);
            var builder = new StringBuilder(line);
            foreach (var use in uses)
            {
                builder.Append(" cost.").Append(use).Append('=').Append(spec.UseCosts[use].ToString("0.00", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        public static string FormatDepleted(StaminaStatus status)
        {
            return LogTag + " depleted " + FormatStatus(status);
        }

        public static string FormatRecovered(StaminaStatus status)
        {
            return LogTag + " recovered " + FormatStatus(status);
        }

        public static string FormatFull(StaminaStatus status)
        {
            return LogTag + " full " + FormatStatus(status);
        }

        public static string FormatRefilled(StaminaStatus status)
        {
            return LogTag + " refilled " + FormatStatus(status);
        }

        public static string FormatSpent(string use, float cost, StaminaStatus status)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} spent use={1} cost={2:0.00} ", LogTag, use, cost) + FormatStatus(status);
        }

        public static string FormatRefused(string use, float cost, StaminaStatus status)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} refused use={1} cost={2:0.00} ", LogTag, use, cost) + FormatStatus(status);
        }

        private static string FormatStatus(StaminaStatus status)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "current={0:0.00} max={1:0.00} depleted={2}",
                status.Current,
                status.Max,
                status.Depleted ? "true" : "false");
        }
    }
}

using System;
using System.Globalization;
using Shooter.Core;

namespace Shooter.Server
{
    // 目的のルールより先に起動のログへ調整値を出すのは、目的のルールが断っても、ルールのファイルの節を読めたかを見分けられるようにするため
    internal static class ServerModeTuningLog
    {
        public const string JuggernautTag = "[Juggernaut]";

        public const string VipTag = "[Vip]";

        // 調整値の節を持たない目的なら null
        public static string For(MatchObjective objective, NetMatchSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            switch (objective)
            {
                case MatchObjective.Juggernaut:
                    return Juggernaut(settings.Juggernaut);
                case MatchObjective.Vip:
                    return Vip(settings.Vip);
                default:
                    return null;
            }
        }

        public static string Juggernaut(JuggernautSpec spec)
        {
            ArgumentNullException.ThrowIfNull(spec);
            return JuggernautTag + " tuning maxHealth=" + Text(spec.JuggernautMaxHealth)
                + " speedMultiplier=" + Text(spec.JuggernautSpeedMultiplier)
                + " scorePerKillAsJuggernaut=" + Text(spec.ScorePerKillAsJuggernaut)
                + " scorePerJuggernautKill=" + Text(spec.ScorePerJuggernautKill)
                + " scoreLimit=" + Text(spec.ScoreLimit)
                + " timeLimit=" + Text(spec.TimeLimitSeconds)
                + " startDelay=" + Text(spec.StartDelaySeconds);
        }

        public static string Vip(VipSpec spec)
        {
            ArgumentNullException.ThrowIfNull(spec);
            return VipTag + " tuning maxHealth=" + Text(spec.VipMaxHealth)
                + " roundSeconds=" + Text(spec.RoundSeconds)
                + " winsToWin=" + Text(spec.WinsToWin)
                + " exitHold=" + Text(spec.ExitHoldSeconds)
                + " swapEveryRounds=" + Text(spec.SwapEveryRounds);
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Text(float value) => value.ToString(CultureInfo.InvariantCulture);
    }
}

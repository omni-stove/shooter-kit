using System;
using System.Globalization;

namespace Shooter.Core
{
    public static class VipLog
    {
        public const string Tag = "[Vip]";

        public const string Nobody = "-";

        public static string RoundLog(int round, Side attacker, Side defender)
        {
            return Tag + " round=" + Number(round) + " attacker=" + Number(attacker.Id) + " defender=" + Number(defender.Id);
        }

        public static string ChosenLog(int round, int playerId)
        {
            return Tag + " chosen round=" + Number(round) + " vip=" + Number(playerId);
        }

        public static string EscapedLog(int playerId) => Tag + " escaped vip=" + Number(playerId);

        public static string RoundEndLog(int round, Side winner, Side defender, VipRoundEnd reason, int alphaWins, int bravoWins)
        {
            if (!Enum.IsDefined(typeof(VipRoundEnd), reason))
            {
                throw new ArgumentOutOfRangeException(nameof(reason), reason, "reason is not a defined VipRoundEnd");
            }

            return Tag + " round_end round=" + Number(round)
                + " winner=" + Number(winner.Id)
                + " role=" + (winner == defender ? "defender" : "attacker")
                + " reason=" + reason
                + " wins=" + Number(Side.Alpha.Id) + ":" + Number(alphaWins) + "," + Number(Side.Bravo.Id) + ":" + Number(bravoWins);
        }

        public static string SwapLog(Side attacker, Side defender)
        {
            return Tag + " swap attacker=" + Number(attacker.Id) + " defender=" + Number(defender.Id);
        }

        public static string EndedLog(Side winner) => Tag + " ended winner=" + Number(winner.Id);

        public static string BodyLog(int playerId, int maxHealth)
        {
            return Tag + " body player=" + Number(playerId) + " max-health=" + Number(maxHealth);
        }

        public static string RestoredLog(int playerId, int maxHealth)
        {
            return Tag + " restore player=" + Number(playerId) + " max-health=" + Number(maxHealth);
        }

        public static string SpawnSwapLog(int points, Side attacker, Side defender)
        {
            return Tag + " spawn_swap points=" + Number(points) + " attacker=" + Number(attacker.Id) + " defender=" + Number(defender.Id);
        }

        public static string BotExitLog(int playerId, int exitIndex)
        {
            return Tag + " bot_exit player=" + Number(playerId) + " exit=" + Number(exitIndex);
        }

        public static string BotEscortLog(int playerId, int vipId)
        {
            return Tag + " bot_escort player=" + Number(playerId) + " vip=" + Number(vipId);
        }

        public static string BotTargetLog(int playerId, int vipId)
        {
            return Tag + " bot_target player=" + Number(playerId) + " vip=" + Number(vipId);
        }

        public static string NoExitLog(string runnerName)
        {
            return Tag + " not_started runner=" + (string.IsNullOrEmpty(runnerName) ? Nobody : runnerName) + " reason=no-exit-point";
        }

        public static string StatusLog(VipStatus status)
        {
            if (status == null)
            {
                throw new ArgumentNullException(nameof(status));
            }

            return Tag + " status vip=" + (status.VipId.HasValue ? Number(status.VipId.Value) : Nobody)
                + " exit=" + status.ExitFraction.ToString("0.##", CultureInfo.InvariantCulture)
                + " defender=" + Number(status.DefenderSide.Id)
                + " local=" + (status.IsLocalVip.HasValue ? (status.IsLocalVip.Value ? "vip" : "other") : Nobody);
        }

        public static string SettingsLog(VipSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            return Tag + " settings vipHealth=" + Number(spec.VipMaxHealth)
                + " winsToWin=" + Number(spec.WinsToWin)
                + " round=" + Seconds(spec.RoundSeconds) + "s"
                + " exitHold=" + Seconds(spec.ExitHoldSeconds) + "s"
                + " swapEvery=" + Number(spec.SwapEveryRounds);
        }

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Seconds(float value) => value.ToString(CultureInfo.InvariantCulture);
    }
}

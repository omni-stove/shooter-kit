using System.Collections.Generic;
using GameKit.Core;

namespace Shooter.Core
{
    public static class VipJson
    {
        public const string ResourcePath = "Tuning/vip";

        public const string VipMaxHealthItem = "vipMaxHealth";
        public const string RoundSecondsItem = "roundSeconds";
        public const string WinsToWinItem = "winsToWin";
        public const string ExitHoldSecondsItem = "exitHoldSeconds";
        public const string SwapEveryRoundsItem = "swapEveryRounds";

        private const string _kind = "vip JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[]
        {
            VipMaxHealthItem,
            RoundSecondsItem,
            WinsToWinItem,
            ExitHoldSecondsItem,
            SwapEveryRoundsItem,
        };

        public static VipSpec Parse(string text, string sourceName)
        {
            return SpecJsonReader.Read(text, sourceName, _kind, ItemNames, fields =>
            {
                var vipMaxHealth = fields.ReadInt(VipMaxHealthItem, VipSpec.DefaultVipMaxHealth);
                var roundSeconds = fields.ReadFloat(RoundSecondsItem, VipSpec.DefaultRoundSeconds);
                var winsToWin = fields.ReadInt(WinsToWinItem, VipSpec.DefaultWinsToWin);
                var exitHoldSeconds = fields.ReadFloat(ExitHoldSecondsItem, VipSpec.DefaultExitHoldSeconds);
                var swapEveryRounds = fields.ReadInt(SwapEveryRoundsItem, VipSpec.DefaultSwapEveryRounds);
                return SpecJsonReader.Build(
                    fields,
                    ItemNames,
                    () => new VipSpec(vipMaxHealth, roundSeconds, winsToWin, exitHoldSeconds, swapEveryRounds));
            });
        }
    }
}

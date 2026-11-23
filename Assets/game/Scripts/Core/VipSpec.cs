using System;
using System.Globalization;

namespace Shooter.Core
{
    // 脱出地点の数と場所はマップ (NetMapExtractionPoint / VipExitPoint) で決まるので持たない。
    public sealed class VipSpec
    {
        public const int DefaultVipMaxHealth = 200;
        public const float DefaultRoundSeconds = 150f;
        public const int DefaultWinsToWin = 4;
        public const float DefaultExitHoldSeconds = 3f;
        public const int DefaultSwapEveryRounds = 1;

        public static VipSpec Default { get; } = new VipSpec();

        public VipSpec(
            int vipMaxHealth = DefaultVipMaxHealth,
            float roundSeconds = DefaultRoundSeconds,
            int winsToWin = DefaultWinsToWin,
            float exitHoldSeconds = DefaultExitHoldSeconds,
            int swapEveryRounds = DefaultSwapEveryRounds)
        {
            VipMaxHealth = SpecGuard.AtLeast(vipMaxHealth, 1, nameof(vipMaxHealth));
            RoundSeconds = SpecGuard.PositiveFinite(roundSeconds, nameof(roundSeconds));
            WinsToWin = SpecGuard.AtLeast(winsToWin, 1, nameof(winsToWin));
            ExitHoldSeconds = SpecGuard.NonNegativeFinite(exitHoldSeconds, nameof(exitHoldSeconds));

            // 留まり切る前にラウンドが終わると、守る側はどうやっても勝てない。
            if (ExitHoldSeconds >= RoundSeconds)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(exitHoldSeconds),
                    exitHoldSeconds,
                    nameof(exitHoldSeconds) + " must be shorter than roundSeconds (" + roundSeconds.ToString(CultureInfo.InvariantCulture) + ")");
            }

            // 0 は入れ替えないことを表し、Search and Destroy の swapEveryRounds と同じ読み方にする。
            SwapEveryRounds = SpecGuard.NonNegative(swapEveryRounds, nameof(swapEveryRounds));
        }

        public int VipMaxHealth { get; }

        public float RoundSeconds { get; }

        public int WinsToWin { get; }

        public float ExitHoldSeconds { get; }

        public int SwapEveryRounds { get; }

        public bool SameAs(VipSpec other)
        {
            return other != null
                && VipMaxHealth == other.VipMaxHealth
                && RoundSeconds == other.RoundSeconds
                && WinsToWin == other.WinsToWin
                && ExitHoldSeconds == other.ExitHoldSeconds
                && SwapEveryRounds == other.SwapEveryRounds;
        }
    }
}

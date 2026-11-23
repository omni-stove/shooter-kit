namespace Shooter.Core
{
    // 動きごとの調整値をひとつずつ引数に並べると、TraversalSpec・NetMotorSettings・サーバーの設定の作り方が動きを足すたびに増えるので、まとめて持つ。
    public sealed class ParkourTuning
    {
        public static readonly ParkourTuning Default = new ParkourTuning();

        public ParkourTuning(
            WallKickTuning wallKick = null,
            LandingRollTuning landingRoll = null,
            WallClimbTuning wallClimb = null,
            LedgeHangTuning ledgeHang = null,
            MomentumTuning momentum = null)
        {
            WallKick = wallKick ?? WallKickTuning.Default;
            LandingRoll = landingRoll ?? LandingRollTuning.Default;
            WallClimb = wallClimb ?? WallClimbTuning.Default;
            LedgeHang = ledgeHang ?? LedgeHangTuning.Default;
            Momentum = momentum ?? MomentumTuning.Default;
        }

        public WallKickTuning WallKick { get; }

        public LandingRollTuning LandingRoll { get; }

        public WallClimbTuning WallClimb { get; }

        public LedgeHangTuning LedgeHang { get; }

        public MomentumTuning Momentum { get; }

        public bool SameAs(ParkourTuning other)
        {
            return other != null
                && WallKick.SameAs(other.WallKick)
                && LandingRoll.SameAs(other.LandingRoll)
                && WallClimb.SameAs(other.WallClimb)
                && LedgeHang.SameAs(other.LedgeHang)
                && Momentum.SameAs(other.Momentum);
        }
    }

    public sealed class WallKickTuning
    {
        // 既定で切なのは、壁走りと同じく、壁際で跳んだだけで蹴り出さないよう使うゲームが入れるものにするため。
        public const bool DefaultEnabled = false;

        public const float DefaultUpSpeed = 6f;
        public const float DefaultAwaySpeed = 6f;

        public static readonly WallKickTuning Default = new WallKickTuning();

        public WallKickTuning(bool enabled = DefaultEnabled, float upSpeed = DefaultUpSpeed, float awaySpeed = DefaultAwaySpeed)
        {
            TuningChecks.RequireNonNegativeFinite(upSpeed, nameof(upSpeed));
            TuningChecks.RequireNonNegativeFinite(awaySpeed, nameof(awaySpeed));
            Enabled = enabled;
            UpSpeed = upSpeed;
            AwaySpeed = awaySpeed;
        }

        public bool Enabled { get; }

        public float UpSpeed { get; }

        public float AwaySpeed { get; }

        public bool SameAs(WallKickTuning other)
        {
            return other != null && Enabled == other.Enabled && UpSpeed.Equals(other.UpSpeed) && AwaySpeed.Equals(other.AwaySpeed);
        }
    }

    public sealed class LandingRollTuning
    {
        // 既定で切なのは、落ちながらしゃがんだだけで転がって、今までの着地と変わらないようにするため。
        public const bool DefaultEnabled = false;

        public const float DefaultWindowSeconds = 0.3f;
        public const float DefaultFallSpeedScale = 0.5f;
        public const float DefaultRollSeconds = 0.6f;
        public const float DefaultMinFallSpeed = 6f;

        public static readonly LandingRollTuning Default = new LandingRollTuning();

        public LandingRollTuning(
            bool enabled = DefaultEnabled,
            float windowSeconds = DefaultWindowSeconds,
            float fallSpeedScale = DefaultFallSpeedScale,
            float rollSeconds = DefaultRollSeconds,
            float minFallSpeed = DefaultMinFallSpeed)
        {
            TuningChecks.RequireNonNegativeFinite(windowSeconds, nameof(windowSeconds));

            // 1 を超えると受け身で落下の傷みが増え、負なら着地の速さが向きを変えるので、割合の範囲に収める。
            TuningChecks.RequireFraction(fallSpeedScale, nameof(fallSpeedScale));
            TuningChecks.RequirePositiveFinite(rollSeconds, nameof(rollSeconds));
            TuningChecks.RequireNonNegativeFinite(minFallSpeed, nameof(minFallSpeed));
            Enabled = enabled;
            WindowSeconds = windowSeconds;
            FallSpeedScale = fallSpeedScale;
            RollSeconds = rollSeconds;
            MinFallSpeed = minFallSpeed;
        }

        public bool Enabled { get; }

        public float WindowSeconds { get; }

        public float FallSpeedScale { get; }

        public float RollSeconds { get; }

        public float MinFallSpeed { get; }

        public bool SameAs(LandingRollTuning other)
        {
            return other != null
                && Enabled == other.Enabled
                && WindowSeconds.Equals(other.WindowSeconds)
                && FallSpeedScale.Equals(other.FallSpeedScale)
                && RollSeconds.Equals(other.RollSeconds)
                && MinFallSpeed.Equals(other.MinFallSpeed);
        }
    }

    public sealed class WallClimbTuning
    {
        // 既定で切なのは、正面の壁へ走り込んで跳んだだけで駆け上がり、今までの乗り越え・よじ登りと変わらないようにするため。
        public const bool DefaultEnabled = false;

        public const float DefaultMinSpeed = 4f;
        public const float DefaultClimbSpeed = 5f;
        public const float DefaultMaxSeconds = 0.6f;
        public const float DefaultMaxHeight = 2.5f;
        public const float DefaultPushOffSpeed = 5f;

        // 既定で入なのは、ふちに届いたら上に乗るのが壁登りを入れた今までの動きで、よじ登りのルール (ActionRules.Mantle) とは別に壁登りだけで切り替えられるようにするため。
        // ルールで切り替えないのは、予測が使う MatchRulesSync に Mantle が無く、クライアントとサーバーで答えが食い違うため。
        public const bool DefaultMantleAtLedge = true;

        public static readonly WallClimbTuning Default = new WallClimbTuning();

        public WallClimbTuning(
            bool enabled = DefaultEnabled,
            float minSpeed = DefaultMinSpeed,
            float climbSpeed = DefaultClimbSpeed,
            float maxSeconds = DefaultMaxSeconds,
            float maxHeight = DefaultMaxHeight,
            float pushOffSpeed = DefaultPushOffSpeed,
            bool mantleAtLedge = DefaultMantleAtLedge)
        {
            TuningChecks.RequireNonNegativeFinite(minSpeed, nameof(minSpeed));
            TuningChecks.RequirePositiveFinite(climbSpeed, nameof(climbSpeed));
            TuningChecks.RequirePositiveFinite(maxSeconds, nameof(maxSeconds));
            TuningChecks.RequirePositiveFinite(maxHeight, nameof(maxHeight));
            TuningChecks.RequireNonNegativeFinite(pushOffSpeed, nameof(pushOffSpeed));
            Enabled = enabled;
            MinSpeed = minSpeed;
            ClimbSpeed = climbSpeed;
            MaxSeconds = maxSeconds;
            MaxHeight = maxHeight;
            PushOffSpeed = pushOffSpeed;
            MantleAtLedge = mantleAtLedge;
        }

        public bool Enabled { get; }

        public float MinSpeed { get; }

        public float ClimbSpeed { get; }

        public float MaxSeconds { get; }

        public float MaxHeight { get; }

        public float PushOffSpeed { get; }

        public bool MantleAtLedge { get; }

        public bool SameAs(WallClimbTuning other)
        {
            return other != null
                && Enabled == other.Enabled
                && MinSpeed.Equals(other.MinSpeed)
                && ClimbSpeed.Equals(other.ClimbSpeed)
                && MaxSeconds.Equals(other.MaxSeconds)
                && MaxHeight.Equals(other.MaxHeight)
                && PushOffSpeed.Equals(other.PushOffSpeed)
                && MantleAtLedge == other.MantleAtLedge;
        }
    }

    public sealed class LedgeHangTuning
    {
        // 既定で切なのは、落ちている間にふちへ近づいただけで掴んで止まり、今までの落ち方と変わらないようにするため。
        public const bool DefaultEnabled = false;

        public const float DefaultGrabReach = 0.6f;
        public const float DefaultShimmySpeed = 1.5f;
        public const float DefaultClimbUpSpeed = 3f;
        public const float DefaultRegrabSeconds = 0.5f;

        public static readonly LedgeHangTuning Default = new LedgeHangTuning();

        public LedgeHangTuning(
            bool enabled = DefaultEnabled,
            float grabReach = DefaultGrabReach,
            float shimmySpeed = DefaultShimmySpeed,
            float climbUpSpeed = DefaultClimbUpSpeed,
            float regrabSeconds = DefaultRegrabSeconds)
        {
            TuningChecks.RequirePositiveFinite(grabReach, nameof(grabReach));
            TuningChecks.RequireNonNegativeFinite(shimmySpeed, nameof(shimmySpeed));
            TuningChecks.RequirePositiveFinite(climbUpSpeed, nameof(climbUpSpeed));
            TuningChecks.RequireNonNegativeFinite(regrabSeconds, nameof(regrabSeconds));
            Enabled = enabled;
            GrabReach = grabReach;
            ShimmySpeed = shimmySpeed;
            ClimbUpSpeed = climbUpSpeed;
            RegrabSeconds = regrabSeconds;
        }

        public bool Enabled { get; }

        public float GrabReach { get; }

        public float ShimmySpeed { get; }

        public float ClimbUpSpeed { get; }

        public float RegrabSeconds { get; }

        public bool SameAs(LedgeHangTuning other)
        {
            return other != null
                && Enabled == other.Enabled
                && GrabReach.Equals(other.GrabReach)
                && ShimmySpeed.Equals(other.ShimmySpeed)
                && ClimbUpSpeed.Equals(other.ClimbUpSpeed)
                && RegrabSeconds.Equals(other.RegrabSeconds);
        }
    }

    // 入り切りを持たないのは、既定の 0 が今の動き (滑りから跳んでも速さは増えず、乗り越えた所から速度 0 で落ちる) そのものだから。
    public sealed class MomentumTuning
    {
        public const float DefaultSlideJumpBoost = 0f;
        public const float DefaultVaultExitSpeedKeep = 0f;

        public static readonly MomentumTuning Default = new MomentumTuning();

        public MomentumTuning(float slideJumpBoost = DefaultSlideJumpBoost, float vaultExitSpeedKeep = DefaultVaultExitSpeedKeep)
        {
            TuningChecks.RequireNonNegativeFinite(slideJumpBoost, nameof(slideJumpBoost));

            // 1 を超えると乗り越えるたびに速くなるので、割合の範囲に収める。
            TuningChecks.RequireFraction(vaultExitSpeedKeep, nameof(vaultExitSpeedKeep));
            SlideJumpBoost = slideJumpBoost;
            VaultExitSpeedKeep = vaultExitSpeedKeep;
        }

        public float SlideJumpBoost { get; }

        public float VaultExitSpeedKeep { get; }

        public bool SameAs(MomentumTuning other)
        {
            return other != null && SlideJumpBoost.Equals(other.SlideJumpBoost) && VaultExitSpeedKeep.Equals(other.VaultExitSpeedKeep);
        }
    }
}

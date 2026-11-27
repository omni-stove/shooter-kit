namespace Shooter.Core
{
    // 値は後ろにだけ足す (前の値の番号を変えない)。
    public enum BarkVerdict
    {
        Allowed = 0,

        KindCooldown = 1,

        SpeakerCooldown = 2,

        GlobalInterval = 3,

        TooManyNearby = 4,
    }
}

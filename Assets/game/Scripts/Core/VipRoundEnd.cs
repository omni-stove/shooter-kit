namespace Shooter.Core
{
    // RoundEndReason に足さないのは、あれが Search and Destroy の写しに 1 バイトで載り、値を足すと受け手の範囲の確かめが変わるため。
    public enum VipRoundEnd
    {
        Escaped,

        VipDown,

        TimeUp,

        AttackersEliminated,

        DefendersEliminated,

        // ModeSession.Finish で外から終えられたときは、決着の理由が無いのでこれにする。
        Finished,
    }
}

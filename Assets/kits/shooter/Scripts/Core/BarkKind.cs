namespace Shooter.Core
{
    // 値を書き換えないのは、BarkCatalog の種類ごとの声と barks.json の項目がこの順と数で並ぶため。末尾にだけ足す。
    public enum BarkKind
    {
        Spotted = 0,

        LostTarget = 1,

        Reloading = 2,

        Grenade = 3,

        TakingFire = 4,

        MovingToCover = 5,

        AllyDown = 6,
    }
}

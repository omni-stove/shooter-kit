namespace Shooter.Core
{
    public enum ShotRejection
    {
        None,

        InvalidOrigin,

        InvalidDirection,

        InvalidTime,

        OriginTooFar,

        OriginBlocked,

        FireRateExceeded,

        NoWeapon,

        Reloading,

        OutOfAmmo,

        UnknownWeapon,

        WeaponMismatch,

        Switching,

        BlindFireBlocked,

        Dodging,

        Vaulting,

        NotAiming,

        Overheated,

        Undercharged,

        SeatCannotFire,

        OutsideSeatArc,

        // 盾の武器は弾を撃たない (オフラインの WeaponController.IsFireBlocked と同じ)。
        ShieldWeapon,

        // 武器をしまっている・しまう途中・構える途中 (オフラインの WeaponController.IsFireBlocked の IsHolstered と同じ)。
        Holstered,

        // 二丁持ちでない武器を左の手で撃とうとした (FireRequestMessage.Hand が Left)。
        NotAkimbo,
    }
}

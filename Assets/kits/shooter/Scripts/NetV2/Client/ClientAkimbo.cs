using System;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    public static class ClientAkimbo
    {
        // 押している間ずっと出る撃ち方は、サーバーが照射を人ごとに 1 本しか持たない (ServerCombat の Beams・Spray) ので、左手の引き金を作らない。
        public static ClientTrigger OffhandTriggerFor(WeaponSpec spec)
        {
            if (spec == null || !spec.IsAkimbo || ClientTrigger.IsContinuousDelivery(spec.Round.Delivery))
            {
                return null;
            }

            return new ClientTrigger(spec);
        }

        // 覗き込みのボタンを左手の引き金に使うので、武器の二丁持ちの部品が断るなら覗き込まない (オフラインの AimDownSights と同じ)。
        public static bool BlocksAds(WeaponSpec spec)
        {
            return spec != null && spec.IsAkimbo && spec.Akimbo.BlocksAds;
        }

        // 左右を 1 度に書くのは、HudModel が 1 発で Ammo の知らせを 2 度出さないように。予備弾は左右で分け合うので 1 つだけ出す。
        public static void ShowAmmo(HudModel model, in WeaponSync weapon, WeaponSpec spec, bool infiniteReserve)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            var akimbo = spec != null && spec.IsAkimbo;
            model.SetAmmo(
                Math.Max(0, weapon.AmmoInMagazine),
                Math.Max(0, weapon.ReserveAmmo),
                weapon.IsReloading,
                infiniteReserve,
                akimbo ? Math.Max(0, weapon.OffhandAmmoInMagazine) : (int?)null,
                akimbo && weapon.IsOffhandReloading);
        }
    }
}

using System;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // 自分の二丁持ちの手ごとのリロードの進みを、一人称の武器モデルを下げるために見積もる。
    // スナップショットはリロードの時刻をアクティブリロードの窓がある手のぶんしか運ばない (ServerAkimbo.Fill) ので、
    // リロード中になったのを見た手元の時刻から、手の秒数 (武器のリロードの秒数 × AkimboSpec.ReloadSecondsMultiplier) で進める。
    public sealed class ClientAkimboReloads
    {
        private string _weaponId;
        private bool _rightReloading;
        private bool _leftReloading;
        private double _rightStartedAt;
        private double _leftStartedAt;

        public AkimboHandsView? Observe(OwnerSnapshot owner, string weaponId, WeaponSpec spec, double nowSeconds)
        {
            if (double.IsNaN(nowSeconds) || double.IsInfinity(nowSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(nowSeconds), nowSeconds, "nowSeconds must be a finite number");
            }

            if (owner == null || spec == null || !spec.IsAkimbo || string.IsNullOrEmpty(weaponId)
                || !string.Equals(ClientFire.CurrentWeaponId(owner), weaponId, StringComparison.Ordinal))
            {
                Forget();
                return null;
            }

            if (!string.Equals(_weaponId, weaponId, StringComparison.Ordinal))
            {
                Forget();
                _weaponId = weaponId;
            }

            var weapon = owner.Weapon;
            var seconds = (double)spec.ReloadSeconds * spec.Akimbo.ReloadSecondsMultiplier;
            var right = Progress(weapon.IsReloading, ref _rightReloading, ref _rightStartedAt, nowSeconds, seconds);
            var left = Progress(weapon.IsOffhandReloading, ref _leftReloading, ref _leftStartedAt, nowSeconds, seconds);
            return new AkimboHandsView(weapon.IsReloading, right, weapon.IsOffhandReloading, left);
        }

        public void Forget()
        {
            _weaponId = null;
            _rightReloading = false;
            _leftReloading = false;
            _rightStartedAt = 0d;
            _leftStartedAt = 0d;
        }

        private static float Progress(bool reloading, ref bool wasReloading, ref double startedAt, double now, double seconds)
        {
            if (!reloading)
            {
                wasReloading = false;
                return 0f;
            }

            if (!wasReloading)
            {
                wasReloading = true;
                startedAt = now;
            }

            if (!(seconds > 0d))
            {
                return 0f;
            }

            return (float)Math.Min(1d, Math.Max(0d, (now - startedAt) / seconds));
        }
    }
}

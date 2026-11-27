using System;
using GameKit.Core;

namespace Shooter.Core
{
    public readonly struct BarkObservation
    {
        public EnemyState State { get; }

        public bool IsReloading { get; }

        public bool WasHit { get; }

        public float? NearestHostileGrenadeDistance { get; }

        public bool NearbyAllyWentDown { get; }

        public bool IsDead { get; }

        // WasHit と NearbyAllyWentDown を「前の観測からの間に起きたか」で持つのは、被弾も仲間が倒れるのも一瞬の出来事で、続いている状態として比べられないため。
        public BarkObservation(
            EnemyState state,
            bool isReloading = false,
            bool wasHit = false,
            float? nearestHostileGrenadeDistance = null,
            bool nearbyAllyWentDown = false,
            bool isDead = false)
        {
            if (!Enum.IsDefined(typeof(EnemyState), state))
            {
                throw new ArgumentOutOfRangeException(nameof(state), state, "state is not an EnemyState");
            }

            if (nearestHostileGrenadeDistance.HasValue)
            {
                SpecGuard.NonNegativeFinite(nearestHostileGrenadeDistance.Value, nameof(nearestHostileGrenadeDistance));
            }

            State = state;
            IsReloading = isReloading;
            WasHit = wasHit;
            NearestHostileGrenadeDistance = nearestHostileGrenadeDistance;
            NearbyAllyWentDown = nearbyAllyWentDown;
            IsDead = isDead;
        }
    }
}

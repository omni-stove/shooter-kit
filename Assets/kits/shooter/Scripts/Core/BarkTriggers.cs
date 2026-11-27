using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public static class BarkTriggers
    {
        private static readonly BarkKind[] _none = Array.Empty<BarkKind>();

        // 優先順を固定するのは、同時に起きた変化のうち先頭だけを喋らせる側が、命に関わる知らせから選べるようにするため。
        public static IReadOnlyList<BarkKind> Pick(BarkObservation previous, BarkObservation current, BarkSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (current.IsDead || current.State == EnemyState.Dead)
            {
                return _none;
            }

            var picked = new List<BarkKind>(4);

            if (GrenadeEntered(previous, current, spec.GrenadeWarningRadius))
            {
                picked.Add(BarkKind.Grenade);
            }

            if (current.NearbyAllyWentDown)
            {
                picked.Add(BarkKind.AllyDown);
            }

            if (IsUnaware(previous.State) && IsEngaging(current.State))
            {
                picked.Add(BarkKind.Spotted);
            }

            if (current.WasHit)
            {
                picked.Add(BarkKind.TakingFire);
            }

            if (IsEngaging(previous.State) && current.State == EnemyState.Search)
            {
                picked.Add(BarkKind.LostTarget);
            }

            if (previous.State != EnemyState.TakeCover && current.State == EnemyState.TakeCover)
            {
                picked.Add(BarkKind.MovingToCover);
            }

            if (!previous.IsReloading && current.IsReloading)
            {
                picked.Add(BarkKind.Reloading);
            }

            return picked.Count == 0 ? _none : picked.ToArray();
        }

        private static bool GrenadeEntered(BarkObservation previous, BarkObservation current, float warningRadius)
        {
            if (!IsWithin(current.NearestHostileGrenadeDistance, warningRadius))
            {
                return false;
            }

            return !IsWithin(previous.NearestHostileGrenadeDistance, warningRadius);
        }

        private static bool IsWithin(float? distance, float radius)
        {
            return distance.HasValue && distance.Value <= radius;
        }

        private static bool IsUnaware(EnemyState state)
        {
            return state == EnemyState.Idle || state == EnemyState.Patrol || state == EnemyState.Search;
        }

        private static bool IsEngaging(EnemyState state)
        {
            return state == EnemyState.Chase || state == EnemyState.Attack;
        }
    }
}

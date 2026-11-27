using System;
using System.Numerics;

namespace Shooter.Core
{
    // Unity 抜きのサーバーがシーンの WeaponFreeZone と同じ範囲と反応で判定するよう、NetMapWeaponFreeZones で書き出したものを読む。
    // 範囲を軸にそろえた箱にするのは、水 (WaterBox) と同じく毎 tick の判定を回転の誤差なしに引くため。
    public sealed class MapWeaponFreeZone
    {
        public MapWeaponFreeZone(string id, Vector3 min, Vector3 max, WeaponFreeZoneSpec spec)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("id must not be blank", nameof(id));
            }

            RequireFinite(min, nameof(min));
            RequireFinite(max, nameof(max));
            if (min.X > max.X || min.Y > max.Y || min.Z > max.Z)
            {
                throw new ArgumentException($"min {min} must not exceed max {max} on any axis", nameof(min));
            }

            Id = id;
            Min = min;
            Max = max;
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        public string Id { get; }

        public Vector3 Min { get; }

        public Vector3 Max { get; }

        public WeaponFreeZoneSpec Spec { get; }

        public WeaponFreeZoneReaction Reaction => Spec.Reaction;

        public bool Contains(Vector3 position)
        {
            return position.X >= Min.X && position.X <= Max.X
                && position.Y >= Min.Y && position.Y <= Max.Y
                && position.Z >= Min.Z && position.Z <= Max.Z;
        }

        private static void RequireFinite(Vector3 value, string name)
        {
            if (!IsFinite(value.X) || !IsFinite(value.Y) || !IsFinite(value.Z))
            {
                throw new ArgumentException($"{name} {value} must be finite", name);
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}

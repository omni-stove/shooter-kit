using System;
using GameKit.Core;

namespace Shooter.Core
{
    public readonly struct WeaponFreeZoneStatus : IEquatable<WeaponFreeZoneStatus>
    {
        public static WeaponFreeZoneStatus Outside => default;

        // 外にいる・警告していない・敵対しない反応のときは残り秒数を 0 に揃える。意味の無い値の違いで変化の知らせが余計に出ないようにするため。
        public WeaponFreeZoneStatus(bool inside, WeaponFreeZoneReaction reaction, bool warning, float secondsToHostile)
        {
            if (!Enum.IsDefined(typeof(WeaponFreeZoneReaction), reaction))
            {
                throw new ArgumentOutOfRangeException(nameof(reaction), reaction, "reaction is not a defined WeaponFreeZoneReaction");
            }

            SpecGuard.NonNegativeFinite(secondsToHostile, nameof(secondsToHostile));
            Inside = inside;
            Reaction = reaction;
            Warning = inside && warning;
            SecondsToHostile = Warning && reaction == WeaponFreeZoneReaction.WarnThenHostile ? secondsToHostile : 0f;
        }

        public bool Inside { get; }

        public WeaponFreeZoneReaction Reaction { get; }

        public bool Warning { get; }

        public float SecondsToHostile { get; }

        public bool Equals(WeaponFreeZoneStatus other)
        {
            return Inside == other.Inside && Reaction == other.Reaction && Warning == other.Warning && SecondsToHostile.Equals(other.SecondsToHostile);
        }

        public override bool Equals(object obj)
        {
            return obj is WeaponFreeZoneStatus other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Inside, Reaction, Warning, SecondsToHostile);
        }

        public static bool operator ==(WeaponFreeZoneStatus left, WeaponFreeZoneStatus right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(WeaponFreeZoneStatus left, WeaponFreeZoneStatus right)
        {
            return !left.Equals(right);
        }
    }
}

using System;

namespace Shooter.Core
{
    public sealed class WeaponFreeZoneSpec
    {
        public const WeaponFreeZoneReaction DefaultReaction = WeaponFreeZoneReaction.WarnThenHostile;

        public const float DefaultHostileAfterSeconds = 5f;

        public static WeaponFreeZoneSpec Default { get; } = new WeaponFreeZoneSpec();

        public WeaponFreeZoneReaction Reaction { get; }

        public float HostileAfterSeconds { get; }

        public bool TurnsHostile => Reaction == WeaponFreeZoneReaction.WarnThenHostile;

        public bool Warns => Reaction == WeaponFreeZoneReaction.WarnOnly || Reaction == WeaponFreeZoneReaction.WarnThenHostile;

        public WeaponFreeZoneSpec(WeaponFreeZoneReaction reaction = DefaultReaction, float hostileAfterSeconds = DefaultHostileAfterSeconds)
        {
            if (!Enum.IsDefined(typeof(WeaponFreeZoneReaction), reaction))
            {
                throw new ArgumentOutOfRangeException(nameof(reaction), reaction, "reaction is not a defined WeaponFreeZoneReaction");
            }

            Reaction = reaction;
            HostileAfterSeconds = SpecGuard.NonNegativeFinite(hostileAfterSeconds, nameof(hostileAfterSeconds));
        }
    }
}

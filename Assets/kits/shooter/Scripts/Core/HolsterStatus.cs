using System;
using GameKit.Core;

namespace Shooter.Core
{
    public readonly struct HolsterStatus : IEquatable<HolsterStatus>
    {
        public HolsterStatus(HolsterPhase phase, float lowering, bool isArmed)
        {
            if (!Enum.IsDefined(typeof(HolsterPhase), phase))
            {
                throw new ArgumentOutOfRangeException(nameof(phase), phase, "phase is not a defined HolsterPhase");
            }

            Phase = phase;
            Lowering = SpecGuard.Fraction(lowering, nameof(lowering));
            IsArmed = isArmed;
        }

        public HolsterPhase Phase { get; }

        public float Lowering { get; }

        public bool IsArmed { get; }

        public bool Equals(HolsterStatus other)
        {
            return Phase == other.Phase && Lowering.Equals(other.Lowering) && IsArmed == other.IsArmed;
        }

        public override bool Equals(object obj)
        {
            return obj is HolsterStatus other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Phase, Lowering, IsArmed);
        }

        public static bool operator ==(HolsterStatus left, HolsterStatus right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(HolsterStatus left, HolsterStatus right)
        {
            return !left.Equals(right);
        }
    }
}

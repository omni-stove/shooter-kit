using System;
using GameKit.Core;

namespace Shooter.Core
{
    public enum WeaponJamPhase
    {
        None,
        Jammed,
        Clearing,
    }

    public readonly struct WeaponJamStatus : IEquatable<WeaponJamStatus>
    {
        public static readonly WeaponJamStatus None = new WeaponJamStatus(WeaponJamPhase.None, 0f, 0f);

        public WeaponJamStatus(WeaponJamPhase phase, float clearRemainingSeconds, float clearSeconds)
        {
            if (!Enum.IsDefined(typeof(WeaponJamPhase), phase))
            {
                throw new ArgumentOutOfRangeException(nameof(phase), phase, "phase is not a defined WeaponJamPhase");
            }

            SpecGuard.NonNegativeFinite(clearSeconds, nameof(clearSeconds));
            SpecGuard.NonNegativeFinite(clearRemainingSeconds, nameof(clearRemainingSeconds));
            if (phase != WeaponJamPhase.Clearing && clearRemainingSeconds != 0f)
            {
                throw new ArgumentException("only a clearing jam has seconds left", nameof(clearRemainingSeconds));
            }

            if (clearRemainingSeconds > clearSeconds)
            {
                throw new ArgumentOutOfRangeException(nameof(clearRemainingSeconds), clearRemainingSeconds, "the seconds left must not exceed the clear seconds");
            }

            Phase = phase;
            ClearRemainingSeconds = clearRemainingSeconds;
            ClearSeconds = clearSeconds;
        }

        public WeaponJamPhase Phase { get; }

        public bool IsJammed => Phase != WeaponJamPhase.None;

        public bool IsClearing => Phase == WeaponJamPhase.Clearing;

        public float ClearRemainingSeconds { get; }

        public float ClearSeconds { get; }

        public float ClearFraction => IsClearing && ClearSeconds > 0f ? 1f - (ClearRemainingSeconds / ClearSeconds) : 0f;

        public static WeaponJamStatus Of(Weapon weapon)
        {
            var jam = weapon?.Jam;
            if (jam == null)
            {
                return None;
            }

            var clearSeconds = jam.Spec.ClearSeconds;
            if (jam.IsClearing)
            {
                return new WeaponJamStatus(WeaponJamPhase.Clearing, Math.Min(jam.ClearRemainingSeconds, clearSeconds), clearSeconds);
            }

            return new WeaponJamStatus(jam.IsJammed ? WeaponJamPhase.Jammed : WeaponJamPhase.None, 0f, clearSeconds);
        }

        public bool Equals(WeaponJamStatus other)
        {
            return Phase == other.Phase && ClearRemainingSeconds.Equals(other.ClearRemainingSeconds) && ClearSeconds.Equals(other.ClearSeconds);
        }

        public override bool Equals(object obj)
        {
            return obj is WeaponJamStatus other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Phase, ClearRemainingSeconds, ClearSeconds);
        }

        public static bool operator ==(WeaponJamStatus left, WeaponJamStatus right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(WeaponJamStatus left, WeaponJamStatus right)
        {
            return !left.Equals(right);
        }
    }
}

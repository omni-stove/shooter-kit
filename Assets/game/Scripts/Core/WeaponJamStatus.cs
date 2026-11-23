using System;

namespace Shooter.Core
{
    public enum WeaponJamPhase
    {
        // 詰まっていない。詰まらない武器・ルールもこれ。
        None,

        // 詰まっていて、解除を始めていない。
        Jammed,

        // 解除している。ClearRemainingSeconds が経つとまた撃てる。
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

        // 解除にかかる全体の秒数 (武器の ammo.jam.clearSeconds)。詰まらない武器は 0。
        public float ClearSeconds { get; }

        // 0 から 1 へ進む解除の割合。解除中でなければ 0。
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

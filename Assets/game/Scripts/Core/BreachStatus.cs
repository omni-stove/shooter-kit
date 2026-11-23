using System;

namespace Shooter.Core
{
    public readonly struct BreachStatus : IEquatable<BreachStatus>
    {
        public BreachStatus(BreachDoorPhase phase, float progress, float secondsRemaining)
        {
            Phase = SpecGuard.Defined(phase, nameof(phase));
            Progress = SpecGuard.Fraction(progress, nameof(progress));
            SecondsRemaining = SpecGuard.NonNegativeFinite(secondsRemaining, nameof(secondsRemaining));
        }

        public BreachDoorPhase Phase { get; }

        // 秒数でなく割合でも持つのは、仕掛ける秒数と爆発までの秒数が違っても UI が同じ輪で描けるようにするため。
        public float Progress { get; }

        public float SecondsRemaining { get; }

        public bool IsPlanting => Phase == BreachDoorPhase.Planting;

        public bool IsArmed => Phase == BreachDoorPhase.Armed;

        public bool IsBreached => Phase == BreachDoorPhase.Opened || Phase == BreachDoorPhase.Destroyed;

        public bool Equals(BreachStatus other)
        {
            return Phase == other.Phase && Progress.Equals(other.Progress) && SecondsRemaining.Equals(other.SecondsRemaining);
        }

        public override bool Equals(object obj)
        {
            return obj is BreachStatus other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Phase, Progress, SecondsRemaining);
        }

        public static bool operator ==(BreachStatus left, BreachStatus right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(BreachStatus left, BreachStatus right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "breach phase={0} progress={1:0.00} remaining={2:0.00}",
                Phase,
                Progress,
                SecondsRemaining);
        }
    }
}

using System;

namespace Shooter.Core
{
    public sealed class JamSpec
    {
        public const float DefaultChancePerShot = 0.01f;

        public const float DefaultChancePerSustainedShot = 0.005f;

        public const float DefaultSustainedResetSeconds = 0.5f;

        public const float DefaultMaxChance = 0.15f;

        public const float DefaultClearSeconds = 1.2f;

        public static readonly JamSpec Default = new JamSpec();

        public JamSpec(
            float chancePerShot = DefaultChancePerShot,
            float chancePerSustainedShot = DefaultChancePerSustainedShot,
            float sustainedResetSeconds = DefaultSustainedResetSeconds,
            float maxChance = DefaultMaxChance,
            float clearSeconds = DefaultClearSeconds)
        {
            ChancePerShot = SpecGuard.Fraction(chancePerShot, nameof(chancePerShot));
            ChancePerSustainedShot = SpecGuard.Fraction(chancePerSustainedShot, nameof(chancePerSustainedShot));
            SustainedResetSeconds = SpecGuard.NonNegativeFinite(sustainedResetSeconds, nameof(sustainedResetSeconds));

            // 上限が 1 発目の確率より低いと、撃ち始めから上限を超えてしまい、上限が何を抑えるのか決まらない。
            if (!(maxChance >= chancePerShot && maxChance <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(maxChance), maxChance, "maxChance must be at least chancePerShot and at most 1");
            }

            MaxChance = maxChance;
            ClearSeconds = SpecGuard.PositiveFinite(clearSeconds, nameof(clearSeconds));
        }

        public float ChancePerShot { get; }

        public float ChancePerSustainedShot { get; }

        public float SustainedResetSeconds { get; }

        public float MaxChance { get; }

        public float ClearSeconds { get; }
    }
}

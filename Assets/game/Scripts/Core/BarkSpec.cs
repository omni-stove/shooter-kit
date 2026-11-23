using System;
using System.Collections.Generic;
using System.Globalization;

namespace Shooter.Core
{
    public sealed class BarkSpec
    {
        public const float DefaultSpottedCooldownSeconds = 6f;

        public const float DefaultLostTargetCooldownSeconds = 8f;

        public const float DefaultReloadingCooldownSeconds = 4f;

        public const float DefaultGrenadeCooldownSeconds = 3f;

        public const float DefaultTakingFireCooldownSeconds = 5f;

        public const float DefaultMovingToCoverCooldownSeconds = 6f;

        public const float DefaultAllyDownCooldownSeconds = 8f;

        public const float DefaultSpeakerCooldownSeconds = 3f;

        public const float DefaultMinIntervalSeconds = 0.75f;

        public const float DefaultCrowdRadius = 12f;

        public const int DefaultMaxSpeakersInCrowd = 2;

        public const float DefaultHearingDistance = 30f;

        public const float DefaultGrenadeWarningRadius = 8f;

        private static readonly BarkKind[] _kinds = (BarkKind[])Enum.GetValues(typeof(BarkKind));

        private readonly float[] _kindCooldownSeconds;

        public static BarkSpec Default { get; } = new BarkSpec();

        public static IReadOnlyList<BarkKind> Kinds { get; } = Array.AsReadOnly(_kinds);

        public float SpeakerCooldownSeconds { get; }

        public float MinIntervalSeconds { get; }

        public float CrowdRadius { get; }

        public int MaxSpeakersInCrowd { get; }

        public float HearingDistance { get; }

        public float GrenadeWarningRadius { get; }

        // 種類の欠けを既定値で埋めるのは、barks.json に一部の種類だけを書いても残りの種類が黙らないようにするため。
        public BarkSpec(
            IReadOnlyDictionary<BarkKind, float> kindCooldownSeconds = null,
            float speakerCooldownSeconds = DefaultSpeakerCooldownSeconds,
            float minIntervalSeconds = DefaultMinIntervalSeconds,
            float crowdRadius = DefaultCrowdRadius,
            int maxSpeakersInCrowd = DefaultMaxSpeakersInCrowd,
            float hearingDistance = DefaultHearingDistance,
            float grenadeWarningRadius = DefaultGrenadeWarningRadius)
        {
            _kindCooldownSeconds = new float[_kinds.Length];
            foreach (var kind in _kinds)
            {
                _kindCooldownSeconds[(int)kind] = DefaultCooldownSecondsFor(kind);
            }

            if (kindCooldownSeconds != null)
            {
                foreach (var pair in kindCooldownSeconds)
                {
                    if (!Enum.IsDefined(typeof(BarkKind), pair.Key))
                    {
                        throw new ArgumentOutOfRangeException(nameof(kindCooldownSeconds), pair.Key, "kindCooldownSeconds has a key that is not a BarkKind");
                    }

                    if (!SpecGuard.IsNonNegativeFinite(pair.Value))
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(kindCooldownSeconds),
                            pair.Value,
                            string.Format(CultureInfo.InvariantCulture, "kindCooldownSeconds of {0} must be a non-negative finite number", pair.Key));
                    }

                    _kindCooldownSeconds[(int)pair.Key] = pair.Value;
                }
            }

            SpeakerCooldownSeconds = SpecGuard.NonNegativeFinite(speakerCooldownSeconds, nameof(speakerCooldownSeconds));
            MinIntervalSeconds = SpecGuard.NonNegativeFinite(minIntervalSeconds, nameof(minIntervalSeconds));
            CrowdRadius = SpecGuard.NonNegativeFinite(crowdRadius, nameof(crowdRadius));

            // 0 を断るのは、上限 0 が「誰も話さない」になり、声を止めたいときは BarkCatalog を空にすれば済むため。
            MaxSpeakersInCrowd = SpecGuard.AtLeast(maxSpeakersInCrowd, 1, nameof(maxSpeakersInCrowd));
            HearingDistance = SpecGuard.PositiveFinite(hearingDistance, nameof(hearingDistance));
            GrenadeWarningRadius = SpecGuard.NonNegativeFinite(grenadeWarningRadius, nameof(grenadeWarningRadius));
        }

        public static float DefaultCooldownSecondsFor(BarkKind kind)
        {
            switch (kind)
            {
                case BarkKind.Spotted:
                    return DefaultSpottedCooldownSeconds;
                case BarkKind.LostTarget:
                    return DefaultLostTargetCooldownSeconds;
                case BarkKind.Reloading:
                    return DefaultReloadingCooldownSeconds;
                case BarkKind.Grenade:
                    return DefaultGrenadeCooldownSeconds;
                case BarkKind.TakingFire:
                    return DefaultTakingFireCooldownSeconds;
                case BarkKind.MovingToCover:
                    return DefaultMovingToCoverCooldownSeconds;
                case BarkKind.AllyDown:
                    return DefaultAllyDownCooldownSeconds;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "kind is not a BarkKind");
            }
        }

        public float CooldownSecondsFor(BarkKind kind)
        {
            if (!Enum.IsDefined(typeof(BarkKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "kind is not a BarkKind");
            }

            return _kindCooldownSeconds[(int)kind];
        }
    }
}

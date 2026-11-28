using System;
using System.Collections.Generic;
using System.Globalization;
using GameKit.Core;

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

        public const float DefaultSpeakerCooldownSeconds = BarkPacing.DefaultSpeakerCooldownSeconds;

        public const float DefaultMinIntervalSeconds = BarkPacing.DefaultMinIntervalSeconds;

        public const float DefaultCrowdRadius = BarkPacing.DefaultCrowdRadius;

        public const int DefaultMaxSpeakersInCrowd = BarkPacing.DefaultMaxSpeakersInCrowd;

        public const float DefaultHearingDistance = BarkPacing.DefaultHearingDistance;

        public const float DefaultGrenadeWarningRadius = 8f;

        public const float DefaultSpeakSeconds = BarkPacing.DefaultSpeakSeconds;

        private static readonly BarkKind[] _kinds = (BarkKind[])Enum.GetValues(typeof(BarkKind));

        public static BarkSpec Default { get; } = new BarkSpec();

        public static IReadOnlyList<BarkKind> Kinds { get; } = Array.AsReadOnly(_kinds);

        // 間隔と混み具合の決まりはキットの BarkSelector に渡すので、種類を barks.json の項目名と同じキー (KeyOf) で持つ。
        public BarkPacing Pacing { get; }

        public float SpeakerCooldownSeconds => Pacing.SpeakerCooldownSeconds;

        public float MinIntervalSeconds => Pacing.MinIntervalSeconds;

        public float CrowdRadius => Pacing.CrowdRadius;

        public int MaxSpeakersInCrowd => Pacing.MaxSpeakersInCrowd;

        public float HearingDistance => Pacing.HearingDistance;

        public float GrenadeWarningRadius { get; }

        public float SpeakSeconds => Pacing.SpeakSeconds;

        // 種類の欠けを既定値で埋めるのは、barks.json に一部の種類だけを書いても残りの種類が黙らないようにするため。
        public BarkSpec(
            IReadOnlyDictionary<BarkKind, float> kindCooldownSeconds = null,
            float speakerCooldownSeconds = DefaultSpeakerCooldownSeconds,
            float minIntervalSeconds = DefaultMinIntervalSeconds,
            float crowdRadius = DefaultCrowdRadius,
            int maxSpeakersInCrowd = DefaultMaxSpeakersInCrowd,
            float hearingDistance = DefaultHearingDistance,
            float grenadeWarningRadius = DefaultGrenadeWarningRadius,
            float speakSeconds = DefaultSpeakSeconds)
        {
            var cooldowns = new Dictionary<string, float>(StringComparer.Ordinal);
            foreach (var kind in _kinds)
            {
                cooldowns[KeyOf(kind)] = DefaultCooldownSecondsFor(kind);
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

                    cooldowns[KeyOf(pair.Key)] = pair.Value;
                }
            }

            Pacing = new BarkPacing(cooldowns, speakerCooldownSeconds, minIntervalSeconds, crowdRadius, maxSpeakersInCrowd, hearingDistance, speakSeconds);
            GrenadeWarningRadius = SpecGuard.NonNegativeFinite(grenadeWarningRadius, nameof(grenadeWarningRadius));
        }

        // キーを列挙の名前から作らないのは、BarkKind の名前を変えても barks.json の項目名とキットに渡すキーが変わらないようにするため。
        public static string KeyOf(BarkKind kind)
        {
            switch (kind)
            {
                case BarkKind.Spotted:
                    return "spotted";
                case BarkKind.LostTarget:
                    return "lostTarget";
                case BarkKind.Reloading:
                    return "reloading";
                case BarkKind.Grenade:
                    return "grenade";
                case BarkKind.TakingFire:
                    return "takingFire";
                case BarkKind.MovingToCover:
                    return "movingToCover";
                case BarkKind.AllyDown:
                    return "allyDown";
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "kind is not a BarkKind");
            }
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

            return Pacing.CooldownSecondsFor(KeyOf(kind));
        }
    }
}

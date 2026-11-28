using System;
using System.Collections.Generic;
using System.Globalization;

namespace GameKit.Core
{
    // 掛け声の間隔と混み具合の決まり。掛け声の種類を列挙で持たないのは、どんな知らせを喋るかをゲームが決めるため。
    public sealed class BarkPacing
    {
        public const float DefaultSpeakerCooldownSeconds = 3f;

        public const float DefaultMinIntervalSeconds = 0.75f;

        public const float DefaultCrowdRadius = 12f;

        public const int DefaultMaxSpeakersInCrowd = 2;

        public const float DefaultHearingDistance = 30f;

        // 全体の最小間隔 × 同時発声の上限より長くするのは、短いと上限に届く前に先の声が終わり、同時発声の上限が効かなくなるため。
        public const float DefaultSpeakSeconds = 2f;

        private static readonly IReadOnlyDictionary<string, float> _noKinds = new Dictionary<string, float>(StringComparer.Ordinal);

        public static BarkPacing Default { get; } = new BarkPacing();

        // 種類のキーごとの間隔 (秒)。ここに無い種類は TryBark で断る。
        public IReadOnlyDictionary<string, float> KindCooldownSeconds { get; }

        public float SpeakerCooldownSeconds { get; }

        public float MinIntervalSeconds { get; }

        public float CrowdRadius { get; }

        public int MaxSpeakersInCrowd { get; }

        public float HearingDistance { get; }

        public float SpeakSeconds { get; }

        public BarkPacing(
            IReadOnlyDictionary<string, float> kindCooldownSeconds = null,
            float speakerCooldownSeconds = DefaultSpeakerCooldownSeconds,
            float minIntervalSeconds = DefaultMinIntervalSeconds,
            float crowdRadius = DefaultCrowdRadius,
            int maxSpeakersInCrowd = DefaultMaxSpeakersInCrowd,
            float hearingDistance = DefaultHearingDistance,
            float speakSeconds = DefaultSpeakSeconds)
        {
            // 渡された辞書をそのまま持たないのは、渡した側が後で書き換えると間隔まで変わってしまうため。
            var kinds = new Dictionary<string, float>(StringComparer.Ordinal);
            if (kindCooldownSeconds != null)
            {
                foreach (var pair in kindCooldownSeconds)
                {
                    if (string.IsNullOrEmpty(pair.Key))
                    {
                        throw new ArgumentException("kindCooldownSeconds must not have an empty kind key", nameof(kindCooldownSeconds));
                    }

                    if (!SpecGuard.IsNonNegativeFinite(pair.Value))
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(kindCooldownSeconds),
                            pair.Value,
                            string.Format(CultureInfo.InvariantCulture, "kindCooldownSeconds of {0} must be a non-negative finite number", pair.Key));
                    }

                    kinds.Add(pair.Key, pair.Value);
                }
            }

            KindCooldownSeconds = kinds.Count == 0 ? _noKinds : kinds;
            SpeakerCooldownSeconds = SpecGuard.NonNegativeFinite(speakerCooldownSeconds, nameof(speakerCooldownSeconds));
            MinIntervalSeconds = SpecGuard.NonNegativeFinite(minIntervalSeconds, nameof(minIntervalSeconds));
            CrowdRadius = SpecGuard.NonNegativeFinite(crowdRadius, nameof(crowdRadius));

            // 0 を断るのは、上限 0 が「誰も話さない」になり、声を止めたいときは声の一覧を空にすれば済むため。
            MaxSpeakersInCrowd = SpecGuard.AtLeast(maxSpeakersInCrowd, 1, nameof(maxSpeakersInCrowd));
            HearingDistance = SpecGuard.PositiveFinite(hearingDistance, nameof(hearingDistance));
            SpeakSeconds = SpecGuard.NonNegativeFinite(speakSeconds, nameof(speakSeconds));
        }

        public bool Knows(string kind)
        {
            return kind != null && KindCooldownSeconds.ContainsKey(kind);
        }

        // 知らない種類を間隔 0 として通さないのは、キーの綴り違いで間隔の無い掛け声になっても気付けなくなるため。
        public float CooldownSecondsFor(string kind)
        {
            if (kind == null)
            {
                throw new ArgumentNullException(nameof(kind));
            }

            if (!KindCooldownSeconds.TryGetValue(kind, out var seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "kind has no cooldown in this pacing");
            }

            return seconds;
        }
    }
}

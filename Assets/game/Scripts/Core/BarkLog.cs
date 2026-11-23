using System;
using System.Globalization;
using System.Text;

namespace Shooter.Core
{
    // 書式を 1 か所に置くのは、smoke とテストがログの行を同じ関数で作って突き合わせるため。
    public static class BarkLog
    {
        public const string Tag = "[Bark]";

        public static string Spoken(string speakerId, EnemyArchetype archetype, BarkKind kind, float hearingDistance)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} speaker={1} archetype={2} kind={3} hearing={4:0.00}",
                Tag,
                Name(speakerId),
                archetype,
                kind,
                hearingDistance);
        }

        public static string Skipped(string speakerId, BarkKind kind, string reason)
        {
            return $"{Tag} speaker={Name(speakerId)} kind={kind} skipped reason={Name(reason)}";
        }

        public static string NoClip(BarkKind kind)
        {
            return $"{Tag} kind={kind} has no voice clip; nothing is played";
        }

        public static string Loaded(BarkSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var cooldowns = new StringBuilder();
            foreach (var kind in BarkSpec.Kinds)
            {
                if (cooldowns.Length > 0)
                {
                    cooldowns.Append(',');
                }

                cooldowns.Append(kind).Append(':').Append(spec.CooldownSecondsFor(kind).ToString("0.00", CultureInfo.InvariantCulture));
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} spec kinds={1} speaker={2:0.00} interval={3:0.00} crowdRadius={4:0.00} crowdMax={5} hearing={6:0.00} grenadeWarning={7:0.00}",
                Tag,
                cooldowns,
                spec.SpeakerCooldownSeconds,
                spec.MinIntervalSeconds,
                spec.CrowdRadius,
                spec.MaxSpeakersInCrowd,
                spec.HearingDistance,
                spec.GrenadeWarningRadius);
        }

        private static string Name(string id)
        {
            return string.IsNullOrEmpty(id) ? "-" : id;
        }
    }
}

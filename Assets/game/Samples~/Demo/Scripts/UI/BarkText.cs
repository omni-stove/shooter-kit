using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public static class BarkText
    {
        public const string LineKey = "bark.line";

        public const string SpeakerKeyPrefix = "bark.speaker.";

        public const string KindKeyPrefix = "bark.";

        public static string SpeakerKey(EnemyArchetype archetype)
        {
            if (!Enum.IsDefined(typeof(EnemyArchetype), archetype))
            {
                throw new ArgumentOutOfRangeException(nameof(archetype), archetype, "archetype is not an EnemyArchetype");
            }

            return SpeakerKeyPrefix + archetype;
        }

        public static string KindKey(BarkKind kind)
        {
            if (!Enum.IsDefined(typeof(BarkKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "kind is not a BarkKind");
            }

            return KindKeyPrefix + kind;
        }

        public static string Line(EnemyArchetype archetype, BarkKind kind)
        {
            return GameText.Format(LineKey, GameText.Get(SpeakerKey(archetype)), GameText.Get(KindKey(kind)));
        }

        public static string Line(BarkCue cue)
        {
            return Line(cue.Archetype, cue.Kind);
        }

        // 字幕の話し手に表示名でなく Id を渡すのは、字幕が話し手ごとに 1 行へまとめるので、同じ兵種の別の兵の掛け声で行を置き換えないため。
        public static SubtitleSpeech Speech(BarkCue cue)
        {
            var line = Line(cue);
            var speaker = cue.SpeakerId.Length > 0 ? cue.SpeakerId : SpeakerKey(cue.Archetype);
            return new SubtitleSpeech(speaker, line);
        }

        // 聞いている人が分からないときに出すのは、音の字幕 (SoundCue) と同じく、伝わらないより出すほうを選ぶため。
        public static bool TrySpeech(BarkCue cue, Vector3? listener, out SubtitleSpeech speech)
        {
            if (listener.HasValue && !cue.IsHeardAt(listener.Value))
            {
                speech = default;
                return false;
            }

            speech = Speech(cue);
            return true;
        }
    }
}

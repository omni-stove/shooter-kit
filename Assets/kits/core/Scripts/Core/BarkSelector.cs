using System;
using System.Collections.Generic;
using System.Numerics;

namespace GameKit.Core
{
    public sealed class BarkSelector
    {
        private readonly Dictionary<string, float> _kindLastAt = new Dictionary<string, float>(StringComparer.Ordinal);

        private readonly Dictionary<string, float> _speakerLastAt = new Dictionary<string, float>(StringComparer.Ordinal);

        private readonly List<Speaking> _speaking = new List<Speaking>();

        private float _lastAt = float.NegativeInfinity;

        public BarkSelector(BarkPacing pacing)
        {
            Pacing = pacing ?? throw new ArgumentNullException(nameof(pacing));
        }

        public BarkPacing Pacing { get; }

        // 通したときだけ記録するのは、止めた掛け声まで間隔を延ばすと、言い続けようとする話し手がいつまでも話せなくなるため。
        public BarkVerdict TryBark(string speakerId, string kind, float time, Vector3 position)
        {
            CheckSpeaker(speakerId);
            if (kind == null)
            {
                throw new ArgumentNullException(nameof(kind));
            }

            if (!Pacing.Knows(kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "kind has no cooldown in this pacing");
            }

            CheckTime(time);
            if (!IsFinite(position))
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, "position must be finite");
            }

            var verdict = Judge(speakerId, kind, time, position);
            if (verdict == BarkVerdict.Allowed)
            {
                Record(speakerId, kind, time, position);
            }

            return verdict;
        }

        public int CountSpeakingNear(Vector3 position, float time)
        {
            CheckTime(time);
            if (!IsFinite(position))
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, "position must be finite");
            }

            var radiusSquared = Pacing.CrowdRadius * Pacing.CrowdRadius;
            var count = 0;
            foreach (var speaking in _speaking)
            {
                if (speaking.EndsAt > time && Vector3.DistanceSquared(speaking.Position, position) <= radiusSquared)
                {
                    count++;
                }
            }

            return count;
        }

        // 倒れた・消えた話し手を外すため。外さないと話し手ごとの記録がマッチの間たまり続ける。
        public void Forget(string speakerId)
        {
            CheckSpeaker(speakerId);
            _speakerLastAt.Remove(speakerId);
            RemoveSpeaking(speakerId);
        }

        public void Clear()
        {
            _kindLastAt.Clear();
            _speakerLastAt.Clear();
            _speaking.Clear();
            _lastAt = float.NegativeInfinity;
        }

        private BarkVerdict Judge(string speakerId, string kind, float time, Vector3 position)
        {
            if (_kindLastAt.TryGetValue(kind, out var kindLastAt) && time - kindLastAt < Pacing.CooldownSecondsFor(kind))
            {
                return BarkVerdict.KindCooldown;
            }

            if (_speakerLastAt.TryGetValue(speakerId, out var speakerLastAt) && time - speakerLastAt < Pacing.SpeakerCooldownSeconds)
            {
                return BarkVerdict.SpeakerCooldown;
            }

            if (time - _lastAt < Pacing.MinIntervalSeconds)
            {
                return BarkVerdict.GlobalInterval;
            }

            if (CountSpeakingNear(position, time) >= Pacing.MaxSpeakersInCrowd)
            {
                return BarkVerdict.TooManyNearby;
            }

            return BarkVerdict.Allowed;
        }

        private void Record(string speakerId, string kind, float time, Vector3 position)
        {
            _kindLastAt[kind] = time;
            _speakerLastAt[speakerId] = time;
            _lastAt = time;

            for (var i = _speaking.Count - 1; i >= 0; i--)
            {
                if (_speaking[i].EndsAt <= time || string.Equals(_speaking[i].SpeakerId, speakerId, StringComparison.Ordinal))
                {
                    _speaking.RemoveAt(i);
                }
            }

            if (Pacing.SpeakSeconds > 0f)
            {
                _speaking.Add(new Speaking(speakerId, position, time + Pacing.SpeakSeconds));
            }
        }

        private void RemoveSpeaking(string speakerId)
        {
            for (var i = _speaking.Count - 1; i >= 0; i--)
            {
                if (string.Equals(_speaking[i].SpeakerId, speakerId, StringComparison.Ordinal))
                {
                    _speaking.RemoveAt(i);
                }
            }
        }

        // 空の Id を断るのは、Id の無い話し手どうしが同じ話し手として間隔を分け合ってしまうため。
        private static void CheckSpeaker(string speakerId)
        {
            if (string.IsNullOrEmpty(speakerId))
            {
                throw new ArgumentException("speakerId must not be null or empty", nameof(speakerId));
            }
        }

        private static void CheckTime(float time)
        {
            if (float.IsNaN(time) || float.IsInfinity(time))
            {
                throw new ArgumentOutOfRangeException(nameof(time), time, "time must be finite");
            }
        }

        private static bool IsFinite(Vector3 v)
        {
            return !float.IsNaN(v.X) && !float.IsInfinity(v.X)
                && !float.IsNaN(v.Y) && !float.IsInfinity(v.Y)
                && !float.IsNaN(v.Z) && !float.IsInfinity(v.Z);
        }

        private readonly struct Speaking
        {
            public Speaking(string speakerId, Vector3 position, float endsAt)
            {
                SpeakerId = speakerId;
                Position = position;
                EndsAt = endsAt;
            }

            public string SpeakerId { get; }

            public Vector3 Position { get; }

            public float EndsAt { get; }
        }
    }
}

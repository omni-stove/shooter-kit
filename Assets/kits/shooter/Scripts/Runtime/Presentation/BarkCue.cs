using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public readonly struct BarkCue
    {
        private readonly string _speakerId;

        public BarkCue(string speakerId, EnemyArchetype archetype, BarkKind kind, Vector3 position, float hearingDistance)
        {
            if (!Enum.IsDefined(typeof(BarkKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "kind is not a BarkKind");
            }

            if (!(hearingDistance > 0f) || float.IsInfinity(hearingDistance))
            {
                throw new ArgumentOutOfRangeException(nameof(hearingDistance), hearingDistance, "hearingDistance must be positive and finite");
            }

            _speakerId = speakerId ?? string.Empty;
            Archetype = archetype;
            Kind = kind;
            Position = position;
            HearingDistance = hearingDistance;
        }

        // default(BarkCue) でも null を返さないよう、空の文字列に揃える。
        public string SpeakerId => _speakerId ?? string.Empty;

        public EnemyArchetype Archetype { get; }

        public BarkKind Kind { get; }

        public Vector3 Position { get; }

        public float HearingDistance { get; }

        public bool IsHeardAt(Vector3 listener)
        {
            return (listener - Position).sqrMagnitude <= HearingDistance * HearingDistance;
        }
    }
}

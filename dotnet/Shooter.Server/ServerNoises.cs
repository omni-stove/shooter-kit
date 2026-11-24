using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // サーバーで鳴った音 (ドアの蹴り・爆薬) を Bot の耳へ渡す。オフラインの SoundStimuli は聞く側へその場で知らせる event だが、
    // Bot は自分の tick の中で知覚を組み直すので、鳴った音を溜めておき、Bot が次に考える tick に 1 回だけ聞かせる
    internal sealed class ServerNoises
    {
        // Bot のモジュールが音を受け取らない組み方でも溜まり続けないよう、1 tick に溜める数に上限を置く
        public const int MaxPending = 64;

        private List<Noise> _pending = new List<Noise>();
        private List<Noise> _current = new List<Noise>();

        public IReadOnlyList<Noise> Current => _current;

        public int PendingCount => _pending.Count;

        // sourceId は鳴らした人。誰が鳴らしたか分からない音 (仕掛けた人が抜けた後の爆薬) は PlayerIds.None にし、誰にでも聞かせる
        public void Emit(int sourceId, SoundStimulus sound)
        {
            if (!(sound.Radius > 0f))
            {
                return;
            }

            if (_pending.Count >= MaxPending)
            {
                _pending.RemoveAt(0);
            }

            _pending.Add(new Noise(sourceId, sound));
        }

        // 前の tick から溜まった音を、この tick に聞く音にする。前に聞かせた音は捨てる
        public void Advance()
        {
            (_current, _pending) = (_pending, _current);
            _pending.Clear();
        }

        // 聞こえる音のうちいちばん大きく聞こえるものを選ぶ。isHostile が false を返す人の音 (味方の蹴り) は聞き流す
        public bool TryHear(Vector3 listener, Func<int, bool> isHostile, out Noise heard)
        {
            ArgumentNullException.ThrowIfNull(isHostile);
            heard = default;
            var loudest = 0f;
            foreach (var noise in _current)
            {
                var loudness = noise.Sound.LoudnessAt(listener);
                if (loudness > loudest && (noise.SourceId == PlayerIds.None || isHostile(noise.SourceId)))
                {
                    heard = noise;
                    loudest = loudness;
                }
            }

            return loudest > 0f;
        }

        public readonly struct Noise
        {
            public Noise(int sourceId, SoundStimulus sound)
            {
                SourceId = sourceId;
                Sound = sound;
            }

            public int SourceId { get; }

            public SoundStimulus Sound { get; }
        }
    }
}

using System;
using GameKit.Core;

namespace Shooter.Core
{
    public sealed class WeaponJamState
    {
        private int _sustainedShots;
        private float _sinceShot;
        private float _clearRemaining;

        public WeaponJamState(JamSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _sinceShot = spec.SustainedResetSeconds;
        }

        public JamSpec Spec { get; }

        public bool IsJammed { get; private set; }

        public bool IsClearing { get; private set; }

        public float ClearRemainingSeconds => IsClearing ? _clearRemaining : 0f;

        // 次の 1 発の前に撃ち続けていた発数。SustainedResetSeconds 撃たずにいれば 0。
        public int SustainedShots => _sinceShot >= Spec.SustainedResetSeconds ? 0 : _sustainedShots;

        public float NextShotChance => Math.Min(Spec.MaxChance, Spec.ChancePerShot + (SustainedShots * Spec.ChancePerSustainedShot));

        // 撃てた 1 発のあとに呼ぶ。roll が確率を下回れば詰まり、true を返す。
        public bool RegisterShot(float roll)
        {
            if (!(roll >= 0f && roll < 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(roll), roll, "roll must be in [0, 1)");
            }

            if (IsJammed)
            {
                throw new InvalidOperationException("cannot fire while jammed");
            }

            var chance = NextShotChance;
            _sustainedShots = SustainedShots + 1;
            _sinceShot = 0f;
            if (!(roll < chance))
            {
                return false;
            }

            IsJammed = true;
            return true;
        }

        public bool TryStartClear()
        {
            if (!IsJammed || IsClearing)
            {
                return false;
            }

            IsClearing = true;
            _clearRemaining = Spec.ClearSeconds;
            return true;
        }

        // 途中まで進めた解除は残さない。持ち替えて戻ったら最初からやり直す。
        public bool CancelClear()
        {
            if (!IsClearing)
            {
                return false;
            }

            IsClearing = false;
            _clearRemaining = 0f;
            return true;
        }

        // 解除し終えてまた撃てるようになった tick なら true。
        public bool Tick(float deltaSeconds)
        {
            SpecGuard.NonNegativeFinite(deltaSeconds, nameof(deltaSeconds));

            // 上限で止めるのは、長く撃たずにいても足し続けた float が際限なく大きくならないようにするため。
            _sinceShot = Math.Min(Spec.SustainedResetSeconds, _sinceShot + deltaSeconds);
            if (!IsClearing)
            {
                return false;
            }

            _clearRemaining -= deltaSeconds;
            if (_clearRemaining > 0f)
            {
                return false;
            }

            _clearRemaining = 0f;
            IsClearing = false;
            IsJammed = false;
            return true;
        }
    }
}

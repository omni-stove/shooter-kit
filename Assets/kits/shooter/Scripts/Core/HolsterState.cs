using System;
using GameKit.Core;

namespace Shooter.Core
{
    // 残りの秒数でなく下げ具合を持つのは、しまう途中で構え直したとき、今の下げ具合から戻す (残り = 構える秒数 × 下げ具合) ため。
    public sealed class HolsterState
    {
        private float _lowering;
        private float _seconds;

        public HolsterPhase Phase { get; private set; } = HolsterPhase.Drawn;

        public float Lowering => _lowering;

        public bool IsMoving => Phase == HolsterPhase.Holstering || Phase == HolsterPhase.Drawing;

        public float RemainingSeconds
        {
            get
            {
                switch (Phase)
                {
                    case HolsterPhase.Holstering:
                        return _seconds * (1f - _lowering);
                    case HolsterPhase.Drawing:
                        return _seconds * _lowering;
                    default:
                        return 0f;
                }
            }
        }

        public HolsterStatus Status(HolsterSpec spec, bool heldByOthers = false)
        {
            return new HolsterStatus(Phase, _lowering, IsArmed(spec, heldByOthers));
        }

        public bool IsArmed(HolsterSpec spec, bool heldByOthers = false)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            return spec.IsArmed(Phase, heldByOthers);
        }

        public bool BeginHolster(float seconds)
        {
            SpecGuard.NonNegativeFinite(seconds, nameof(seconds));
            if (Phase == HolsterPhase.Holstering || Phase == HolsterPhase.Holstered)
            {
                return false;
            }

            Begin(HolsterPhase.Holstering, seconds);
            return true;
        }

        public bool BeginDraw(float seconds)
        {
            SpecGuard.NonNegativeFinite(seconds, nameof(seconds));
            if (Phase == HolsterPhase.Drawing || Phase == HolsterPhase.Drawn)
            {
                return false;
            }

            Begin(HolsterPhase.Drawing, seconds);
            return true;
        }

        public bool Tick(float deltaSeconds)
        {
            SpecGuard.NonNegativeFinite(deltaSeconds, nameof(deltaSeconds));
            if (!IsMoving)
            {
                return false;
            }

            // 残りを足し引きで詰めると丸めで 1 や 0 にわずかに届かず止まることがあるので、残り秒数と比べて揃える。
            if (deltaSeconds >= RemainingSeconds)
            {
                Settle();
                return true;
            }

            var step = deltaSeconds / _seconds;
            _lowering = Phase == HolsterPhase.Holstering ? Math.Min(1f, _lowering + step) : Math.Max(0f, _lowering - step);
            return false;
        }

        public void ResetToDrawn()
        {
            Phase = HolsterPhase.Drawn;
            _lowering = 0f;
            _seconds = 0f;
        }

        private void Begin(HolsterPhase phase, float seconds)
        {
            Phase = phase;
            _seconds = seconds;
            if (RemainingSeconds <= 0f)
            {
                Settle();
            }
        }

        private void Settle()
        {
            if (Phase == HolsterPhase.Holstering)
            {
                Phase = HolsterPhase.Holstered;
                _lowering = 1f;
            }
            else
            {
                Phase = HolsterPhase.Drawn;
                _lowering = 0f;
            }

            _seconds = 0f;
        }
    }
}

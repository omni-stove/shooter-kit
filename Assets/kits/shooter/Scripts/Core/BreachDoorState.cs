using System;
using GameKit.Core;

namespace Shooter.Core
{
    // 破った後に閉じ直す口を持たないのは、板を外した・壊した見た目と当たりを戻す手立てがキットに無く、オンラインでも閉じたドアを配り直さないため。
    public sealed class BreachDoorState
    {
        private float _stepSeconds;
        private float _elapsed;
        private float _fuseSeconds;

        public BreachDoorPhase Phase { get; private set; } = BreachDoorPhase.Closed;

        public bool IsClosed => Phase == BreachDoorPhase.Closed;

        public bool IsTiming => Phase == BreachDoorPhase.Planting || Phase == BreachDoorPhase.Armed;

        public bool IsBreached => Phase == BreachDoorPhase.Opened || Phase == BreachDoorPhase.Destroyed;

        public float SecondsRemaining => IsTiming ? Math.Max(0f, _stepSeconds - _elapsed) : 0f;

        public float Progress
        {
            get
            {
                if (IsBreached)
                {
                    return 1f;
                }

                if (!IsTiming)
                {
                    return 0f;
                }

                return _stepSeconds > 0f ? Math.Min(1f, _elapsed / _stepSeconds) : 1f;
            }
        }

        public BreachStatus Status()
        {
            return new BreachStatus(Phase, Progress, SecondsRemaining);
        }

        public bool Kick()
        {
            if (!IsClosed)
            {
                return false;
            }

            Phase = BreachDoorPhase.Opened;
            ClearTiming();
            return true;
        }

        public bool BeginPlant(float plantSeconds, float fuseSeconds)
        {
            SpecGuard.NonNegativeFinite(plantSeconds, nameof(plantSeconds));
            SpecGuard.NonNegativeFinite(fuseSeconds, nameof(fuseSeconds));
            if (!IsClosed)
            {
                return false;
            }

            _fuseSeconds = fuseSeconds;
            Enter(BreachDoorPhase.Planting, plantSeconds);
            SettleFinishedSteps();
            return true;
        }

        // 仕掛け終わった爆薬は取り外せない。取りやめられるのは仕掛けている途中だけ。
        public bool Cancel()
        {
            if (Phase != BreachDoorPhase.Planting)
            {
                return false;
            }

            Phase = BreachDoorPhase.Closed;
            ClearTiming();
            return true;
        }

        // 段が変わった Tick にだけ真を返す。仕掛け終わりと爆発が 1 回の Tick に重なれば、余った秒数を爆発までに回して Destroyed まで進める。
        public bool Tick(float deltaSeconds)
        {
            SpecGuard.NonNegativeFinite(deltaSeconds, nameof(deltaSeconds));
            if (!IsTiming)
            {
                return false;
            }

            var started = Phase;
            var left = deltaSeconds;
            while (IsTiming)
            {
                var remaining = _stepSeconds - _elapsed;

                // 足し算を重ねると丸めで残りにわずかに届かず止まることがあるので、残りの秒数と比べて段を進める。
                if (left < remaining)
                {
                    _elapsed += left;
                    break;
                }

                left -= remaining;
                Advance();
            }

            return Phase != started;
        }

        private void Advance()
        {
            if (Phase == BreachDoorPhase.Planting)
            {
                Enter(BreachDoorPhase.Armed, _fuseSeconds);
                return;
            }

            Phase = BreachDoorPhase.Destroyed;
            ClearTiming();
        }

        private void SettleFinishedSteps()
        {
            while (IsTiming && _stepSeconds - _elapsed <= 0f)
            {
                Advance();
            }
        }

        private void Enter(BreachDoorPhase phase, float seconds)
        {
            Phase = phase;
            _stepSeconds = seconds;
            _elapsed = 0f;
        }

        private void ClearTiming()
        {
            _stepSeconds = 0f;
            _elapsed = 0f;
            _fuseSeconds = 0f;
        }
    }
}

using System;

namespace Shooter.Core
{
    public sealed class MeleeSwing
    {
        private MeleeSpec _spec;
        private MeleeSpec _swingSpec;
        private float _elapsed;
        private bool _impacted;
        private float _cooldownRemaining;
        private float _sweepProgress;

        public MeleeSwing(MeleeSpec spec)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        // 振っている最中に替えても、その振りは始めたときの値のまま終える (途中で当たりの瞬間が動かないように)。
        public MeleeSpec Spec
        {
            get => _spec;
            set => _spec = value ?? throw new ArgumentNullException(nameof(value));
        }

        public bool IsSwinging => _swingSpec != null;

        public bool IsLunging => _swingSpec != null && !_impacted && _swingSpec.LungeDistance > 0f;

        public float Elapsed => _swingSpec != null ? _elapsed : 0f;

        public float CooldownRemaining => _cooldownRemaining;

        public bool CanStart => _swingSpec == null && _cooldownRemaining <= 0f;

        public MeleeSpec CurrentSpec => _swingSpec ?? _spec;

        // 直前の Tick で軌道 (MeleeSpec.Path) の刃が進んだか。進んだ範囲は SweepFrom から SweepTo (0〜1)。
        // 当たりの瞬間の Tick から刃が通り終える Tick まで続き、振り終えた Tick でも値は残る (呼ぶ側が振り始めに控えた Spec で判定できるように)。
        public bool HasSweep { get; private set; }

        public float SweepFrom { get; private set; }

        public float SweepTo { get; private set; }

        public bool TryStart()
        {
            if (!CanStart)
            {
                return false;
            }

            _swingSpec = _spec;
            _elapsed = 0f;
            _impacted = false;
            _sweepProgress = 0f;
            return true;
        }

        // 当たりの瞬間を越えた Tick だけ true。大きな deltaSeconds で振り終えまで越えても 1 度は true を返す。
        public bool Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            HasSweep = false;
            if (_swingSpec == null)
            {
                _cooldownRemaining = Math.Max(0f, _cooldownRemaining - deltaSeconds);
                return false;
            }

            _elapsed += deltaSeconds;
            var impact = false;
            if (!_impacted && _elapsed >= _swingSpec.ImpactSeconds)
            {
                _impacted = true;
                impact = true;
            }

            var path = _swingSpec.Path;
            if (path != null && _impacted && _sweepProgress < 1f)
            {
                var progress = path.ActiveSeconds > 0f ? Math.Min(1f, (_elapsed - _swingSpec.ImpactSeconds) / path.ActiveSeconds) : 1f;
                HasSweep = true;
                SweepFrom = _sweepProgress;
                SweepTo = progress;
                _sweepProgress = progress;
            }

            if (_elapsed >= _swingSpec.SwingSeconds)
            {
                Finish();
            }

            return impact;
        }

        // 当たりの前に止めても待ち時間は課す。止められては振り直すのを繰り返して、当たりの瞬間だけを早められないように。
        public bool Cancel()
        {
            if (_swingSpec == null)
            {
                return false;
            }

            Finish();
            return true;
        }

        public void Reset()
        {
            HasSweep = false;
            _swingSpec = null;
            _elapsed = 0f;
            _impacted = false;
            _cooldownRemaining = 0f;
        }

        private void Finish()
        {
            _cooldownRemaining = _swingSpec.CooldownSeconds;
            _swingSpec = null;
            _elapsed = 0f;
            _impacted = false;
        }
    }
}

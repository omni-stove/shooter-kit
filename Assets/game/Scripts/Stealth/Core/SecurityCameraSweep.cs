using System;
using System.Numerics;

namespace Shooter.Core
{
    public sealed class SecurityCameraSweep
    {
        private readonly AwarenessMeter _meter;
        private float _direction = 1f;
        private bool _pausing;
        private float _pauseLeft;

        public SecurityCameraSweep(SecurityCameraSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _meter = new AwarenessMeter(spec.Awareness);
        }

        // 1 tick に段階が 2 つ進んでも (止めた時間が明けてすぐ見つけたなど) 知らせは tick の前後の 1 回にまとめる。途中の段階を知らせると、記録や警報が一瞬だけの段階に反応する。
        public event Action<SecurityCameraPhase, SecurityCameraPhase> PhaseChanged;

        public SecurityCameraSpec Spec { get; }

        public SecurityCameraPhase Phase { get; private set; }

        public float YawDegrees { get; private set; }

        public bool IsPausedAtEdge => _pausing && Phase == SecurityCameraPhase.Sweeping;

        public float Level => _meter.Level;

        public AwarenessStage Stage => _meter.Stage;

        public float DisabledSecondsLeft { get; private set; }

        // 見つけ続けている間は最後に見た場所に更新する。警報で呼ぶ見張りを、最初に見つけた場所ではなく今いる場所へ向かわせるため。
        public Vector3 SpottedAt { get; private set; }

        public bool IsActive => Phase != SecurityCameraPhase.Disabled && Phase != SecurityCameraPhase.Destroyed;

        public SecurityCameraPhase Tick(float deltaSeconds, SecurityCameraTarget target)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be non-negative and finite");
            }

            var before = Phase;
            if (Phase == SecurityCameraPhase.Destroyed)
            {
                return Phase;
            }

            var remaining = deltaSeconds;
            if (Phase == SecurityCameraPhase.Disabled)
            {
                if (remaining < DisabledSecondsLeft)
                {
                    DisabledSecondsLeft -= remaining;
                    return Phase;
                }

                // 明けた後の残りの時間も首振りと気づきに使う。tick の刻み方で、明けてから見つけるまでの秒数が変わらないように。
                remaining -= DisabledSecondsLeft;
                DisabledSecondsLeft = 0f;
                Phase = SecurityCameraPhase.Sweeping;
            }

            if (Sees(target))
            {
                Track(remaining, target);
            }
            else
            {
                _meter.Tick(remaining, Spec.ViewDistance, AwarenessSighting.None);
                Phase = SecurityCameraPhase.Sweeping;
                Sweep(remaining);
            }

            Announce(before);
            return Phase;
        }

        public bool Disable()
        {
            return Disable(Spec.DisabledSeconds);
        }

        // すでに止まっていれば長い方を残す。止め直しで残りが短くなると、2 人目が止めたせいで早く動き出してしまう。
        public bool Disable(float seconds)
        {
            SpecGuard.PositiveFinite(seconds, nameof(seconds));
            if (Phase == SecurityCameraPhase.Destroyed)
            {
                return false;
            }

            var before = Phase;
            DisabledSecondsLeft = Math.Max(DisabledSecondsLeft, seconds);
            Phase = SecurityCameraPhase.Disabled;
            Announce(before);
            return true;
        }

        public bool Destroy()
        {
            if (Phase == SecurityCameraPhase.Destroyed)
            {
                return false;
            }

            var before = Phase;
            DisabledSecondsLeft = 0f;
            _pausing = false;
            _pauseLeft = 0f;
            _meter.Reset();
            Phase = SecurityCameraPhase.Destroyed;
            Announce(before);
            return true;
        }

        public static float AngleBetween(float bearingDegrees, float yawDegrees)
        {
            return Math.Abs(SignedDelta(bearingDegrees, yawDegrees));
        }

        // 視野の中かどうかだけでなく、たまる量が 0 より大きいことで見えたとする。真っ暗な所に立つ相手を視野の中だけで見えたとすると、ゲージはたまらないのに首振りが止まって釘付けになる。
        private bool Sees(SecurityCameraTarget target)
        {
            return AwarenessMeter.GainPerSecond(Spec.Awareness, Spec.ViewDistance, Sighting(target)) > 0f;
        }

        private void Track(float deltaSeconds, SecurityCameraTarget target)
        {
            // 首振りの端での待ちは捨てる。追った後に首振りへ戻ったとき、端から離れた所で残りの待ちを消化して止まらないように。
            _pausing = false;
            _pauseLeft = 0f;

            // 追う速さに首振りの速さを使うのは、追う方が速いと首振りの間を縫って抜ける遊びが、見つかりかけた後に消えるため。
            var half = Spec.SweepDegrees / 2f;
            var wanted = Clamp(YawDegrees + SignedDelta(target.BearingDegrees, YawDegrees), -half, half);
            var step = Spec.SweepDegreesPerSecond * deltaSeconds;
            var gap = wanted - YawDegrees;
            YawDegrees = Math.Abs(gap) <= step ? wanted : YawDegrees + (Math.Sign(gap) * step);

            _meter.Tick(deltaSeconds, Spec.ViewDistance, Sighting(target));
            if (_meter.Stage == AwarenessStage.Engaged)
            {
                Phase = SecurityCameraPhase.Spotted;
                SpottedAt = target.Position;
            }
            else
            {
                Phase = SecurityCameraPhase.Tracking;
            }
        }

        private void Sweep(float deltaSeconds)
        {
            var half = Spec.SweepDegrees / 2f;
            if (!(half > 0f))
            {
                return;
            }

            var remaining = deltaSeconds;
            while (remaining > 0f)
            {
                if (_pausing)
                {
                    if (remaining < _pauseLeft)
                    {
                        _pauseLeft -= remaining;
                        return;
                    }

                    remaining -= _pauseLeft;
                    _pauseLeft = 0f;
                    _pausing = false;
                    _direction = -_direction;
                    continue;
                }

                var edge = _direction * half;
                var toEdge = Math.Abs(edge - YawDegrees);
                var seconds = toEdge / Spec.SweepDegreesPerSecond;
                if (remaining < seconds)
                {
                    // 丸めで端を越えないように切り詰める。越えると次の tick で端までの距離が逆向きに測られ、反対の端まで止まらずに進む。
                    YawDegrees = Clamp(YawDegrees + (_direction * Spec.SweepDegreesPerSecond * remaining), -half, half);
                    return;
                }

                YawDegrees = edge;
                remaining -= seconds;
                ReachEdge();
            }
        }

        private void ReachEdge()
        {
            if (Spec.EdgePauseSeconds > 0f)
            {
                _pausing = true;
                _pauseLeft = Spec.EdgePauseSeconds;
            }
            else
            {
                _direction = -_direction;
            }
        }

        private AwarenessSighting Sighting(SecurityCameraTarget target)
        {
            if (!target.HasLineOfSight)
            {
                return AwarenessSighting.None;
            }

            return new AwarenessSighting(
                true,
                target.Distance,
                AngleBetween(target.BearingDegrees, YawDegrees),
                target.Posture,
                target.LightFactor,
                target.Speed,
                target.IsArmed);
        }

        private void Announce(SecurityCameraPhase before)
        {
            if (before != Phase)
            {
                PhaseChanged?.Invoke(before, Phase);
            }
        }

        private static float SignedDelta(float bearingDegrees, float yawDegrees)
        {
            var delta = (bearingDegrees - yawDegrees) % 360f;
            if (delta > 180f)
            {
                delta -= 360f;
            }
            else if (delta < -180f)
            {
                delta += 360f;
            }

            return delta;
        }

        private static float Clamp(float value, float min, float max)
        {
            return value < min ? min : (value > max ? max : value);
        }
    }
}

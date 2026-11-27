using System;
using System.Collections.Generic;
using System.Numerics;
using GameKit.Core;

namespace Shooter.Core
{
    // 鳴っている間の Raise は秒数を足さずに満タンへ戻す。足していくと、カメラが見続けている間に止む時刻が際限なく先へ延びる。
    public sealed class SecurityAlarmState
    {
        private readonly SecurityAlarmSpec _spec;
        private float _secondsLeft;
        private Vector3 _spottedAt;
        private string _cameraId = string.Empty;

        public SecurityAlarmState(SecurityAlarmSpec spec)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        public SecurityAlarmSpec Spec => _spec;

        public bool Ringing => _secondsLeft > 0f;

        public float SecondsLeft => _secondsLeft;

        public Vector3 SpottedAt => Ringing ? _spottedAt : Vector3.Zero;

        public string CameraId => Ringing ? _cameraId : string.Empty;

        public SecurityAlarmStatus Status => Ringing
            ? new SecurityAlarmStatus(true, _secondsLeft, _spottedAt, _cameraId)
            : SecurityAlarmStatus.Silent;

        public SecurityAlarmRaise Raise(Vector3 spottedAt, string cameraId)
        {
            if (!IsFinite(spottedAt))
            {
                throw new ArgumentOutOfRangeException(nameof(spottedAt), spottedAt, "spottedAt must be finite");
            }

            var change = Ringing ? SecurityAlarmRaise.Extended : SecurityAlarmRaise.Started;
            _secondsLeft = _spec.DurationSeconds;
            _spottedAt = spottedAt;
            _cameraId = SpecGuard.ReferenceId(cameraId);
            return change;
        }

        // 止んだ Tick でだけ true を返す。止む行を 1 度だけ出せるようにするため。
        public bool Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must not be negative and must be finite");
            }

            if (!Ringing)
            {
                return false;
            }

            _secondsLeft = Math.Max(0f, _secondsLeft - deltaSeconds);
            if (Ringing)
            {
                return false;
            }

            Clear();
            return true;
        }

        public void Reset()
        {
            _secondsLeft = 0f;
            Clear();
        }

        // 鳴っていなければ誰も選ばない。止んだ後の場所は空なので、そこへ敵を呼ばないため。
        public int SelectRecipients(IReadOnlyList<Vector3> candidates, List<int> selected)
        {
            return Ringing
                ? SelectWithin(_spottedAt, _spec.Radius, candidates, selected)
                : SelectWithin(Vector3.Zero, 0f, candidates, selected);
        }

        // 半径ちょうどの候補は選ぶ。見張りどうしの呼び声 (EnemyStealth.RaiseAlarm) も距離 <= 半径で伝えており、境目の扱いを揃える。
        // 半径 0 は SecurityAlarmSpec で「誰も呼ばない」としているので、同じ場所に立つ候補も選ばない。
        public static int SelectWithin(Vector3 origin, float radius, IReadOnlyList<Vector3> candidates, List<int> selected)
        {
            if (!IsFinite(origin))
            {
                throw new ArgumentOutOfRangeException(nameof(origin), origin, "origin must be finite");
            }

            SpecGuard.NonNegativeFinite(radius, nameof(radius));
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            if (selected == null)
            {
                throw new ArgumentNullException(nameof(selected));
            }

            selected.Clear();
            if (radius <= 0f)
            {
                return 0;
            }

            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (IsFinite(candidate) && Vector3.Distance(origin, candidate) <= radius)
                {
                    selected.Add(i);
                }
            }

            return selected.Count;
        }

        private void Clear()
        {
            _spottedAt = Vector3.Zero;
            _cameraId = string.Empty;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.X) && !float.IsInfinity(value.X)
                && !float.IsNaN(value.Y) && !float.IsInfinity(value.Y)
                && !float.IsNaN(value.Z) && !float.IsInfinity(value.Z);
        }
    }
}

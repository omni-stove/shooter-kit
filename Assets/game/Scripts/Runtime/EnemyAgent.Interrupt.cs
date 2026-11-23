using System;
using System.Globalization;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed partial class EnemyAgent
    {
        private readonly EnemyInterruption _interruption = new EnemyInterruption();

        public bool IsInterrupted => _interruption.IsActive;

        public string InterruptReason => _interruption.IsActive ? _interruption.Reason : string.Empty;

        public float InterruptSecondsRemaining => _interruption.SecondsRemaining;

        public bool Interrupt(float seconds, string reason)
        {
            EnemyInterruption.Validate(seconds, reason);
            if (_brain == null)
            {
                throw new InvalidOperationException($"EnemyAgent '{name}' is not initialized");
            }

            if (_brain.IsDead || !_interruption.Begin(seconds, reason))
            {
                return false;
            }

            Debug.Log(FormatInterruptLog(Id, reason, seconds), this);
            return true;
        }

        // 理由が違えば解かないのは、より長い怯みなど、ほかが止めている分まで消さないため。
        public bool Resume(string reason)
        {
            if (!_interruption.IsActive || !string.Equals(_interruption.Reason, reason, StringComparison.Ordinal))
            {
                return false;
            }

            Debug.Log(FormatResumeLog(Id, reason), this);
            EndInterruption();
            return true;
        }

        public static string FormatInterruptLog(string id, string reason, float seconds)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} id={1} interrupted reason={2} seconds={3:F2}", LogTag, id, reason, seconds);
        }

        public static string FormatResumeLog(string id, string reason)
        {
            return $"{LogTag} id={id} resumed reason={reason}";
        }

        // 怯みの間にたまった撃たれた記録は、解けたら捨てる。残すと怯ませた弾のせいで撃ち返さずに遮蔽へ退いてしまう。
        // 撃たれた方向 (物音) は残し、解けた後に振り向けるようにする。背後からの近接など怯み以外で止めた分は、解けた後に隠れられるよう残す。
        private void EndInterruption()
        {
            var staggered = _interruption.Reason.StartsWith(StaggerReaction.InterruptReasonPrefix, StringComparison.Ordinal);
            _interruption.Clear();
            if (staggered)
            {
                DropPendingHit();
            }
        }

        // 止まっている間は判断を進めない。進めると見失いや捜索の時間だけが経ち、解けたときに状態が飛ぶ。
        private bool HoldWhileInterrupted(float deltaSeconds)
        {
            if (!_interruption.IsActive)
            {
                return false;
            }

            if (_interruption.Tick(deltaSeconds))
            {
                Debug.Log(FormatResumeLog(Id, _interruption.Reason), this);
                EndInterruption();
                return false;
            }

            _sprinting = false;
            _aiming = false;
            _agent.speed = _walkSpeed * _moveSpeedMultiplier;
            MoveTo(null);
            WatchProgress(deltaSeconds);
            return true;
        }
    }
}

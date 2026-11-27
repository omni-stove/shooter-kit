using System;
using GameKit.Core;

namespace Shooter.Core
{
    // 1 つのゾーンと 1 人のプレイヤーの組を見る。警告は入って構えた tick から数え始め、出るかしまえば数えた秒を捨てる。
    public sealed class WeaponFreeZoneState
    {
        private float _warnedSeconds;

        public WeaponFreeZoneState(WeaponFreeZoneSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        public WeaponFreeZoneSpec Spec { get; }

        public bool Inside { get; private set; }

        public bool Armed { get; private set; }

        public bool Warning { get; private set; }

        // 一度敵対したら戻らない。出て入り直したりしまったりして、撃ってくる相手が元に戻ると、ゾーンの意味が薄れるため。
        public bool IsHostile { get; private set; }

        // 自動でしまう反応は、構えたまま入った tick だけ立てる。中で構え直すのは止めないのが「構えられない」との違い。
        public bool ShouldHolster { get; private set; }

        public bool BlocksDraw => Inside && Spec.Reaction == WeaponFreeZoneReaction.BlockDraw;

        public float WarnedSeconds => _warnedSeconds;

        public float SecondsToHostile => Warning && Spec.TurnsHostile ? Math.Max(0f, Spec.HostileAfterSeconds - _warnedSeconds) : 0f;

        // 外にいるときは反応も Outside に揃える。どのゾーンの外かで値が分かれると、外にいることを Outside と比べて確かめられないため。
        public WeaponFreeZoneStatus Status => Inside ? new WeaponFreeZoneStatus(true, Spec.Reaction, Warning, SecondsToHostile) : WeaponFreeZoneStatus.Outside;

        // 戻り値はこの tick で敵対に変わったか。
        public bool Tick(float deltaSeconds, bool inside, bool armed)
        {
            SpecGuard.NonNegativeFinite(deltaSeconds, nameof(deltaSeconds));
            var entered = inside && !Inside;
            Inside = inside;
            Armed = armed;
            ShouldHolster = entered && armed && Spec.Reaction == WeaponFreeZoneReaction.AutoHolster;

            if (!inside || !armed || IsHostile || !Spec.Warns)
            {
                ClearWarning();
                return false;
            }

            if (!Warning)
            {
                Warning = true;
                _warnedSeconds = 0f;
            }
            else
            {
                _warnedSeconds += deltaSeconds;
            }

            if (!Spec.TurnsHostile || _warnedSeconds < Spec.HostileAfterSeconds)
            {
                return false;
            }

            IsHostile = true;
            ClearWarning();
            return true;
        }

        public void Leave()
        {
            Inside = false;
            Armed = false;
            ShouldHolster = false;
            ClearWarning();
        }

        private void ClearWarning()
        {
            Warning = false;
            _warnedSeconds = 0f;
        }
    }
}

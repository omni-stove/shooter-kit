using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // EnemyAgent の目標 (陣営でいちばん近い敵) は替えず、知覚の Target と撃つ相手だけを差し替える。目標を替えると、ラウンドが変わって
    // 攻守が入れ替わった後や Runner を外した後にも、bot が VIP だった人を追い続ける (JuggernautTargetPerception と同じ)。
    // VIP は頭上の目印で場のだれにも居場所が知れているので、見えていなくても居場所を物音として渡し、攻める側の bot に追わせる。
    public sealed class VipTargetPerception : IPerceivedTargetContributor
    {
        private readonly EnemyAgent _agent;
        private readonly VipRunner _runner;
        private Damageable _perceivedTarget;
        private int _loggedTargetId = -1;

        public VipTargetPerception(EnemyAgent agent, VipRunner runner)
        {
            _agent = agent != null ? agent : throw new ArgumentNullException(nameof(agent));
            _runner = runner != null ? runner : throw new ArgumentNullException(nameof(runner));
        }

        public EnemyAgent Agent => _agent;

        public Damageable PerceivedTarget => _perceivedTarget;

        // 守る側と、VIP がいない (選ぶ前・抜けた直後・倒れた後) あいだは null を返し、ふだんの目標 (いちばん近い敵) に任せる。
        public static SideMember SelectTarget(SideMember self, SideMember vip, bool selfIsAttacker)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }

            if (!selfIsAttacker || vip == null || vip == self || vip.Damageable == null || !vip.IsAlive)
            {
                return null;
            }

            return vip;
        }

        public EnemyPerception Contribute(EnemyPerception perception)
        {
            _perceivedTarget = null;
            var self = SideMember.Of(_agent.gameObject);
            if (self == null || !_runner.IsStarted || _runner.IsMatchOver || !_runner.IsRoundLive)
            {
                return perception;
            }

            var vip = _runner.VipMember;
            var target = SelectTarget(self, vip, _runner.IsAttacker(self));
            if (target == null)
            {
                return perception;
            }

            var damageable = target.Damageable;
            _perceivedTarget = damageable;
            LogTarget(self, target);

            var sensed = EnemySenses.Perceive(perception.Self.ToUnity(), _agent.EyePosition, damageable, _agent.SightRange, _agent.transform);
            var heardNoise = perception.HeardNoise;
            var noise = perception.NoisePosition;

            // 撃たれた者の位置などの物音がもうあれば、そちらを残す。撃ってきた相手の方が遠くの VIP より差し迫っている。
            if (!sensed.HasLineOfSight && !heardNoise && sensed.Target.HasValue)
            {
                heardNoise = true;
                noise = sensed.Target;
            }

            return new EnemyPerception(
                perception.Self,
                sensed.Target,
                sensed.HasLineOfSight,
                heardNoise,
                noise,
                perception.WasHit,
                perception.HitSource,
                perception.HealthFraction,
                EnemyPerception.AwarenessFromSight(sensed.HasLineOfSight),
                perception.IsSuppressed,
                perception.SuppressionLevel);
        }

        private void LogTarget(SideMember self, SideMember target)
        {
            var selfId = _runner.IdOf(self);
            var targetId = _runner.IdOf(target);
            if (selfId < 0 || targetId < 0 || targetId == _loggedTargetId)
            {
                return;
            }

            _loggedTargetId = targetId;
            Debug.Log(VipLog.BotTargetLog(selfId, targetId), _agent);
        }
    }
}

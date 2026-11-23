using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // EnemyAgent の目標 (陣営でいちばん近い敵) は替えず、知覚の Target と撃つ相手だけを差し替える。目標を替えると、Juggernaut が終わった後や
    // Runner を外した後にも bot がジャガーノートだった人を追い続ける。
    // ジャガーノートは頭上の目印で場のだれにも居場所が知れているので、見えていなくても居場所を物音として渡し、ジャガーノートでない bot に追わせる。
    public sealed class JuggernautTargetPerception : IPerceivedTargetContributor
    {
        private readonly EnemyAgent _agent;
        private readonly JuggernautRunner _runner;
        private Damageable _perceivedTarget;
        private int _loggedTargetId = -1;

        public JuggernautTargetPerception(EnemyAgent agent, JuggernautRunner runner)
        {
            _agent = agent != null ? agent : throw new ArgumentNullException(nameof(agent));
            _runner = runner != null ? runner : throw new ArgumentNullException(nameof(runner));
        }

        public EnemyAgent Agent => _agent;

        public Damageable PerceivedTarget => _perceivedTarget;

        // ジャガーノートがいない (選ぶ前・倒れてリスポーンを待つ間) あいだは null を返し、ふだんの目標 (いちばん近い敵) に任せる。
        public static SideMember SelectTarget(SideMember self, SideMember juggernaut, IReadOnlyList<SideMember> members)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }

            if (members == null)
            {
                throw new ArgumentNullException(nameof(members));
            }

            if (juggernaut == null || juggernaut.Damageable == null || !juggernaut.IsAlive)
            {
                return null;
            }

            if (juggernaut != self)
            {
                return juggernaut;
            }

            SideMember nearest = null;
            var nearestDistance = float.PositiveInfinity;
            var from = self.GroundPosition;
            foreach (var member in members)
            {
                if (member == null || member == self || member.Damageable == null || !member.IsAlive)
                {
                    continue;
                }

                var distance = (member.GroundPosition - from).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearest = member;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        public EnemyPerception Contribute(EnemyPerception perception)
        {
            _perceivedTarget = null;
            var self = SideMember.Of(_agent.gameObject);
            if (self == null || !_runner.IsStarted || _runner.IsMatchOver || _runner.Juggernaut == null)
            {
                return perception;
            }

            var juggernaut = _runner.JuggernautMember;
            var target = SelectTarget(self, juggernaut, _runner.Members);
            if (target == null)
            {
                return perception;
            }

            var damageable = target.Damageable;
            _perceivedTarget = damageable;
            LogTarget(self, target, juggernaut == self);

            var sensed = EnemySenses.Perceive(perception.Self.ToUnity(), _agent.EyePosition, damageable, _agent.SightRange, _agent.transform);
            var heardNoise = perception.HeardNoise;
            var noise = perception.NoisePosition;

            // 撃たれた者の位置などの物音がもうあれば、そちらを残す。撃ってきた相手の方が遠くのジャガーノートより差し迫っている。
            if (!sensed.HasLineOfSight && juggernaut != self && !heardNoise && sensed.Target.HasValue)
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

        private void LogTarget(SideMember self, SideMember target, bool asJuggernaut)
        {
            var selfId = _runner.IdOf(self);
            var targetId = _runner.IdOf(target);
            if (selfId < 0 || targetId < 0 || targetId == _loggedTargetId)
            {
                return;
            }

            _loggedTargetId = targetId;
            Debug.Log(JuggernautLog.BotTarget(selfId, targetId, asJuggernaut), _agent);
        }
    }
}

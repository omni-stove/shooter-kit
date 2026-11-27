using System.Numerics;

namespace Shooter.Core
{
    public sealed partial class EnemyBrain
    {
        private EnemyDecision Engage(float deltaSeconds, Vector3 self, Vector3 target)
        {
            _lostElapsed = 0f;
            _lastSeen = target;

            // 撃ちながら近づいて Attack に入ったときは構え直さない。入った途端に撃つのをやめると、近づくほど撃たなくなって見える。
            // FireRange の外で見続けていた間も構え時間に数える。FireRange に入ってから数え始めると、構え終わるまで撃たずに走る距離ができる。
            // TakeCover で _watching が立つのは、覗いて構え終えたまま遮蔽をやめた tick だけ。
            if (_watching && (State == EnemyState.Attack || State == EnemyState.Chase || State == EnemyState.TakeCover))
            {
                _reactionElapsed += deltaSeconds;
            }
            else
            {
                _reactionElapsed = 0f;
                _shotsInAttack = 0;
            }

            _watching = true;
            var distance = Vector3.Distance(self, target);
            if (distance > _spec.FireRange)
            {
                _engaging = false;
                SetState(EnemyState.Chase);
                return new EnemyDecision(target, target, false);
            }

            _engaging = true;
            var ready = _reactionElapsed >= _spec.ReactionSeconds;
            if (distance <= _spec.AttackRange)
            {
                SetState(EnemyState.Attack);
                return new EnemyDecision(null, target, ready);
            }

            SetState(EnemyState.Chase);
            return new EnemyDecision(ready ? target : (Vector3?)null, target, ready);
        }

        private EnemyDecision Pursue(float deltaSeconds)
        {
            _engaging = false;
            _watching = false;
            _lostElapsed += deltaSeconds;
            if (_lostElapsed >= _spec.LoseSightSeconds)
            {
                StartSearch(_lastSeen);
                return SearchDecision();
            }

            SetState(EnemyState.Chase);
            return new EnemyDecision(_lastSeen, _lastSeen, false);
        }
    }
}

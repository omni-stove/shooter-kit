using System.Numerics;

namespace Shooter.Core
{
    public sealed partial class EnemyBrain
    {
        private EnemyDecision Engage(float deltaSeconds, Vector3 self, Vector3 target)
        {
            _lostElapsed = 0f;
            _lastSeen = target;

            var distance = Vector3.Distance(self, target);
            if (distance > _spec.FireRange)
            {
                _engaging = false;
                SetState(EnemyState.Chase);
                return new EnemyDecision(target, target, false);
            }

            // 撃ちながら近づいて Attack に入ったときは構え直さない。入った途端に撃つのをやめると、近づくほど撃たなくなって見える。
            if (_engaging && (State == EnemyState.Attack || State == EnemyState.Chase))
            {
                _reactionElapsed += deltaSeconds;
            }
            else
            {
                _reactionElapsed = 0f;
                _shotsInAttack = 0;
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

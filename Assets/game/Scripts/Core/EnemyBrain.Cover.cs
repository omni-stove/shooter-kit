using System.Numerics;

namespace Shooter.Core
{
    public sealed partial class EnemyBrain
    {
        private bool ShouldTakeCover(EnemyPerception p)
        {
            return _cover != null && (p.WasHit || p.HealthFraction < _spec.CoverHealthFraction);
        }

        private static Vector3? ThreatOf(EnemyPerception p)
        {
            if (p.Target.HasValue)
            {
                return p.Target.Value;
            }

            return p.WasHit ? p.HitSource : null;
        }

        private bool TryCover(float deltaSeconds, EnemyPerception p, bool seen, out EnemyDecision decision)
        {
            decision = EnemyDecision.None;
            if (seen)
            {
                _lastSeen = p.Target.Value;
            }

            var threat = ThreatOf(p);
            if (_cover == null || !threat.HasValue || !CoverPoint.IsFinite(threat.Value) || !CoverPoint.IsFinite(p.Self))
            {
                LeaveCover();
                return false;
            }

            _coverThreat = threat.Value;

            // 回り込まれて今の点が脅威の方向を遮らなくなったら、放して選び直す。
            if (_coverIndex >= 0 && !_cover.Protects(_coverIndex, _coverThreat))
            {
                ReleaseCoverPoint();
            }

            if (_coverIndex < 0)
            {
                var next = _cover.Select(p.Self, _coverThreat, this, _unreachableCover);
                if (next < 0 || !_cover.TryClaim(next, this))
                {
                    LeaveCover();
                    return false;
                }

                _coverIndex = next;
                CoverPhase = EnemyCoverPhase.Moving;
            }

            if (State != EnemyState.TakeCover)
            {
                _lostElapsed = 0f;
            }

            decision = CoverStep(deltaSeconds, p.Self, seen);

            // 決定を作った後に移す。StateChanged の購読者が Kill() して点を放しても、ここでは点を読まない。
            SetState(EnemyState.TakeCover);
            return true;
        }

        private EnemyDecision CoverStep(float deltaSeconds, Vector3 self, bool seen)
        {
            var point = _cover.Points[_coverIndex].Position;
            switch (CoverPhase)
            {
                case EnemyCoverPhase.Moving:
                    if (Vector3.Distance(self, point) > _spec.ArrivalDistance)
                    {
                        return new EnemyDecision(point, _coverThreat, false, false);
                    }

                    StartHiding();
                    return HideDecision();
                case EnemyCoverPhase.Hiding:
                    _coverElapsed += deltaSeconds;
                    if (_coverElapsed < _spec.CoverHideSeconds)
                    {
                        return HideDecision();
                    }

                    CoverPhase = EnemyCoverPhase.Peeking;
                    _coverElapsed = 0f;
                    _reactionElapsed = 0f;
                    _shotsInAttack = 0;
                    return PeekDecision(self, seen);
                default:
                    _coverElapsed += deltaSeconds;
                    if (_coverElapsed >= _spec.CoverPeekSeconds)
                    {
                        StartHiding();
                        return HideDecision();
                    }

                    _reactionElapsed = _coverElapsed;
                    return PeekDecision(self, seen);
            }
        }

        private void StartHiding()
        {
            CoverPhase = EnemyCoverPhase.Hiding;
            _coverElapsed = 0f;
        }

        private EnemyDecision HideDecision()
        {
            return new EnemyDecision(null, _coverThreat, false, true);
        }

        private EnemyDecision PeekDecision(Vector3 self, bool seen)
        {
            var inRange = Vector3.Distance(self, _coverThreat) <= _spec.FireRange;
            return new EnemyDecision(null, _coverThreat, seen && inRange && _reactionElapsed >= _spec.ReactionSeconds, false);
        }

        private void LeaveCover()
        {
            if (State == EnemyState.TakeCover)
            {
                _lastSeen = _coverThreat;
                _lostElapsed = 0f;
            }

            ReleaseCoverPoint();
            CoverPhase = EnemyCoverPhase.None;
        }

        // 遮蔽への移動中に撃つときの散らばりを足す口。後続で CoverMoveSpreadDegrees を使うまで、撃ち方を変えないよう 0 を返す。
        private float CoverSpreadDegrees()
        {
            return 0f;
        }
    }
}

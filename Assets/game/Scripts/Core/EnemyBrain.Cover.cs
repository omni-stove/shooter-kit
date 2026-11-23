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

            var freshAim = false;
            if (_coverIndex < 0)
            {
                var keepAim = KeepsAimIntoCover();
                var next = _cover.Select(p.Self, _coverThreat, this, _unreachableCover, CoverSearchDistance(p));
                if (next < 0 || !_cover.TryClaim(next, this))
                {
                    LeaveCover();
                    return false;
                }

                _coverIndex = next;
                CoverPhase = EnemyCoverPhase.Moving;
                if (!keepAim)
                {
                    _reactionElapsed = 0f;
                    _shotsInAttack = 0;
                    freshAim = true;
                }
            }

            if (State != EnemyState.TakeCover)
            {
                _lostElapsed = 0f;
            }

            decision = CoverStep(deltaSeconds, p.Self, seen, freshAim);

            // 決定を作った後に移す。StateChanged の購読者が Kill() して点を放しても、ここでは点を読まない。
            SetState(EnemyState.TakeCover);
            return true;
        }

        // 体力が残っていて撃たれただけなら、近くの点にだけ移る。遠くの点へ走る間は撃ち返せず、撃ち合いを捨てて背を向けたように見えるので。
        private float? CoverSearchDistance(EnemyPerception p)
        {
            if (State == EnemyState.TakeCover || p.HealthFraction < _spec.CoverHealthFraction)
            {
                return null;
            }

            return _spec.CoverOnHitMaxDistance;
        }

        // 撃ち合いの最中や覗いている最中に点へ移り始めたなら、構え直さずにそのまま撃たせる。
        private bool KeepsAimIntoCover()
        {
            return IsAiming || (State == EnemyState.TakeCover && CoverPhase == EnemyCoverPhase.Moving);
        }

        private EnemyDecision CoverStep(float deltaSeconds, Vector3 self, bool seen, bool freshAim)
        {
            var point = _cover.Points[_coverIndex].Position;
            switch (CoverPhase)
            {
                case EnemyCoverPhase.Moving:
                    if (Vector3.Distance(self, point) > _spec.ArrivalDistance)
                    {
                        return MoveDecision(deltaSeconds, self, point, seen, freshAim);
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

        // 見失ったら構え直させる。交戦 (Engage) と同じく、見え直した瞬間には撃たない。
        private EnemyDecision MoveDecision(float deltaSeconds, Vector3 self, Vector3 point, bool seen, bool freshAim)
        {
            if (!seen)
            {
                _reactionElapsed = 0f;
                _shotsInAttack = 0;
            }
            else if (!freshAim)
            {
                _reactionElapsed += deltaSeconds;
            }

            var inRange = Vector3.Distance(self, _coverThreat) <= _spec.FireRange;
            return new EnemyDecision(point, _coverThreat, seen && inRange && _reactionElapsed >= _spec.ReactionSeconds, false);
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

        private float CoverSpreadDegrees()
        {
            return State == EnemyState.TakeCover && CoverPhase == EnemyCoverPhase.Moving ? _spec.CoverMoveSpreadDegrees : 0f;
        }
    }
}

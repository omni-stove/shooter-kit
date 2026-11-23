using System;
using System.Numerics;

namespace Shooter.Core
{
    public sealed partial class EnemyBrain
    {
        private bool ShouldTakeCover(EnemyPerception p)
        {
            return _cover != null && _coverCooldown <= 0f && (p.WasHit || p.HealthFraction < _spec.CoverHealthFraction);
        }

        // 遮蔽中に見えていない相手の今の位置を使うと、壁越しに回り込みを知って点を選び直してしまう。
        private Vector3? CoverThreatOf(EnemyPerception p, bool seen)
        {
            if (seen)
            {
                return p.Target.Value;
            }

            if (p.WasHit && p.HitSource.HasValue)
            {
                return p.HitSource.Value;
            }

            if (!p.Target.HasValue)
            {
                return null;
            }

            return State == EnemyState.TakeCover ? _coverThreat : p.Target.Value;
        }

        private bool TryCover(float deltaSeconds, EnemyPerception p, bool seen, out EnemyDecision decision)
        {
            decision = EnemyDecision.None;
            if (seen)
            {
                _lastSeen = p.Target.Value;
            }

            var threat = CoverThreatOf(p, seen);
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
                _coverPeeks = 0;
            }

            if (!CoverStep(deltaSeconds, p.Self, seen, freshAim, p.IsSuppressed, out decision))
            {
                var aimed = CoverPhase == EnemyCoverPhase.Peeking && seen && _reactionElapsed >= _spec.CoverPeekReactionSeconds;
                LeaveCover();
                if (aimed)
                {
                    // 覗いて構え終えた銃口を出るときに下ろすと、出た途端に撃たなくなるので、交戦の構え時間も済んだことにする。
                    _watching = true;
                    _reactionElapsed = Math.Max(_reactionElapsed, _spec.ReactionSeconds);
                }

                decision = EnemyDecision.None;
                return false;
            }

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

        // false を返したら遮蔽をやめる。覗き終えるたびに隠れ直すだけだと、相手が来ない限り遮蔽から出てこないので。
        // 制圧されている間はやめない。撃ち込まれている所へ身をさらすことになり、制圧で釘付けにする遊びが成り立たない。
        private bool CoverStep(float deltaSeconds, Vector3 self, bool seen, bool freshAim, bool suppressed, out EnemyDecision decision)
        {
            var point = _cover.Points[_coverIndex].Position;
            switch (CoverPhase)
            {
                case EnemyCoverPhase.Moving:
                    if (Vector3.Distance(self, point) > _spec.ArrivalDistance)
                    {
                        decision = MoveDecision(deltaSeconds, self, point, seen, freshAim);
                        return true;
                    }

                    StartHiding();
                    decision = HideDecision();
                    return true;
                case EnemyCoverPhase.Hiding:
                    _coverElapsed += deltaSeconds;
                    if (_coverElapsed < _spec.CoverHideSeconds)
                    {
                        decision = HideDecision();
                        return true;
                    }

                    // 届かない相手を覗いても撃てず、隠れては覗くだけになる。
                    if (!suppressed && !InFireRange(self))
                    {
                        decision = EnemyDecision.None;
                        return false;
                    }

                    CoverPhase = EnemyCoverPhase.Peeking;
                    _coverElapsed = 0f;
                    _reactionElapsed = 0f;
                    _shotsInAttack = 0;
                    if (_coverPeeks < int.MaxValue)
                    {
                        _coverPeeks++;
                    }

                    decision = PeekDecision(self, seen);
                    return true;
                default:
                    _coverElapsed += deltaSeconds;
                    if (_coverElapsed >= _spec.CoverPeekSeconds)
                    {
                        if (!suppressed && _coverPeeks >= _spec.CoverMaxPeeks)
                        {
                            decision = EnemyDecision.None;
                            return false;
                        }

                        StartHiding();
                        decision = HideDecision();
                        return true;
                    }

                    _reactionElapsed = _coverElapsed;
                    decision = PeekDecision(self, seen);
                    return true;
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

            return new EnemyDecision(point, _coverThreat, seen && InFireRange(self) && _reactionElapsed >= _spec.ReactionSeconds, false);
        }

        // 覗くたびに交戦と同じ構え時間を待つと、覗いている間のほとんどを撃たずに過ごすので、短い構え時間を使う。
        private EnemyDecision PeekDecision(Vector3 self, bool seen)
        {
            return new EnemyDecision(null, _coverThreat, seen && InFireRange(self) && _reactionElapsed >= _spec.CoverPeekReactionSeconds, false);
        }

        private bool InFireRange(Vector3 self)
        {
            return Vector3.Distance(self, _coverThreat) <= _spec.FireRange;
        }

        private void LeaveCover()
        {
            if (State == EnemyState.TakeCover)
            {
                _lastSeen = _coverThreat;
                _lostElapsed = 0f;
                _coverCooldown = _spec.CoverCooldownSeconds;
            }

            ReleaseCoverPoint();
            CoverPhase = EnemyCoverPhase.None;
            _coverPeeks = 0;
        }

        private float CoverSpreadDegrees()
        {
            return State == EnemyState.TakeCover && CoverPhase == EnemyCoverPhase.Moving ? _spec.CoverMoveSpreadDegrees : 0f;
        }

        private float AimReactionSeconds()
        {
            return State == EnemyState.TakeCover && CoverPhase == EnemyCoverPhase.Peeking ? _spec.CoverPeekReactionSeconds : _spec.ReactionSeconds;
        }
    }
}

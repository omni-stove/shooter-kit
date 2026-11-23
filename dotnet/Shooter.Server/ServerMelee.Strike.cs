using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    // 当たりの瞬間に、壁越しでない相手のうち振る向きでいちばん近い 1 人を選んで与える。軌道のある振りは刃が通った相手へ順に与える
    internal sealed partial class ServerMelee
    {
        private readonly List<int> _candidateIds = new List<int>();
        private readonly List<Vector3> _candidatePoints = new List<Vector3>();
        private readonly List<int> _order = new List<int>();
        private readonly List<int> _crossed = new List<int>();

        // weaponId が null なら、ふつうの近接として StrikeWeaponId で数える
        private void Strike(int attackerId, MeleeSpec spec, string weaponId)
        {
            if (!_context.Bodies.TryGet(attackerId, out var attacker))
            {
                return;
            }

            var origin = attacker.Center;
            var forward = new Vector3(MathF.Sin(attacker.Yaw), 0f, MathF.Cos(attacker.Yaw));
            var target = FindTarget(attackerId, origin, forward, spec, out var point);
            if (target < 0)
            {
                _log.WriteLine(MeleeLog.Miss(Text(attackerId)));
                return;
            }

            Deal(attackerId, attacker, target, Vector3.Distance(origin, point), spec, weaponId);
        }

        // 軌道 (MeleeSpec.Path) のある振りは、刃がこの Tick に通った相手へ通った順に与え、振りごとの上限 (MeleePath.MaxTargets) で止める。
        // 向きは Tick ごとの体の向きを使う。振りの途中で向きを変えれば刃もついて回る。
        private void Sweep(Swinger swinger, MeleeSpec spec, string weaponId, float progressFrom, float progressTo)
        {
            var attackerId = swinger.PlayerId;
            if (!_context.Bodies.TryGet(attackerId, out var attacker))
            {
                return;
            }

            var origin = attacker.Center;
            var forward = new Vector3(MathF.Sin(attacker.Yaw), 0f, MathF.Cos(attacker.Yaw));
            GatherCandidates(attackerId, origin);
            MeleeJudge.SelectCrossed(origin, forward, spec, _candidatePoints, progressFrom, progressTo, _crossed);
            foreach (var index in _crossed)
            {
                if (swinger.Struck.Count >= spec.Path.MaxTargets)
                {
                    break;
                }

                var target = _candidateIds[index];
                if (swinger.Struck.Add(target))
                {
                    Deal(attackerId, attacker, target, Vector3.Distance(origin, _candidatePoints[index]), spec, weaponId);
                }
            }

            if (progressTo >= 1f && swinger.Struck.Count == 0)
            {
                _log.WriteLine(MeleeLog.Miss(Text(attackerId)));
            }
        }

        private void Deal(int attackerId, ServerBody attacker, int target, float distance, MeleeSpec spec, string weaponId)
        {
            _log.WriteLine(MeleeLog.Hit(Text(attackerId), Text(target), spec.Damage, distance));
            var outcome = _combat.DealDamage(attackerId, target, spec.Damage, weaponId ?? StrikeWeaponId(attacker), HitZone.Body, attacker.Position, DamageKind.Melee);
            if (outcome != DamageOutcome.Ignored)
            {
                _combat.NoteWounded(target, DamageKind.Melee);
            }
        }

        // 並びは Id の小さい順にそろえ、同じ距離の相手を毎回同じに選ぶ (MeleeStrike.TryFind と同じ)。壁越しの相手は外す
        private int FindTarget(int attackerId, Vector3 origin, Vector3 forward, MeleeSpec spec, out Vector3 point)
        {
            GatherCandidates(attackerId, origin);
            var best = MeleeJudge.SelectNearest(origin, forward, spec, _candidatePoints);
            if (best < 0)
            {
                point = default;
                return -1;
            }

            point = _candidatePoints[best];
            return _candidateIds[best];
        }

        private void GatherCandidates(int attackerId, Vector3 origin)
        {
            _order.Clear();
            foreach (var body in _context.Bodies.All)
            {
                if (body.PlayerId != attackerId && body.HasSpawned && _combat.IsHittable(body.PlayerId))
                {
                    _order.Add(body.PlayerId);
                }
            }

            _order.Sort();
            _candidateIds.Clear();
            _candidatePoints.Clear();
            foreach (var playerId in _order)
            {
                _context.Bodies.TryGet(playerId, out var body);
                var closest = ClosestPointOnBody(body, origin);
                if (!IsBlocked(origin, closest))
                {
                    _candidateIds.Add(playerId);
                    _candidatePoints.Add(closest);
                }
            }
        }

        // 立ち姿・しゃがみ・伏せで変わるカプセルの表面のうち、振る者の中心にいちばん近い点。中心が体の中なら中心そのもの
        internal static Vector3 ClosestPointOnBody(ServerBody body, Vector3 origin)
        {
            body.Capsule(out var bottom, out var top);
            var axis = top - bottom;
            var lengthSquared = axis.LengthSquared();
            var t = lengthSquared > 1e-12f ? Math.Clamp(Vector3.Dot(origin - bottom, axis) / lengthSquared, 0f, 1f) : 0f;
            var onAxis = bottom + (axis * t);
            var offset = origin - onAxis;
            var length = offset.Length();
            return length <= body.Radius ? origin : onAxis + (offset * (body.Radius / length));
        }

        private bool IsBlocked(Vector3 from, Vector3 to)
        {
            var offset = to - from;
            var length = offset.Length();
            return length >= 1e-4f && _context.World.Raycast(from, offset / length, length, ServerHitboxes.RaycastLayers, out _);
        }

        private string StrikeWeaponId(ServerBody attacker)
        {
            return _weaponId.Length > 0 ? _weaponId : attacker.HeldWeapon?.Id ?? string.Empty;
        }
    }
}

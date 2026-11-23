using System.Globalization;
using Shooter.Core;

namespace Shooter.Server
{
    // 振り始めてよいかをサーバーの知る状態とルールで決め、拒んだ理由を間引いてログに出す
    internal sealed partial class ServerMelee
    {
        // 拒んだ警告を出す最短の間隔 (秒)。押し続けたり連打したりする入力でログを埋めないため
        private const float _refusalLogInterval = 1f;

        private double _nextRefusalLogTime = double.NegativeInfinity;

        internal bool IsSwinging(int playerId)
        {
            return _swingers.TryGetValue(playerId, out var swinger) && swinger.Swing.IsSwinging;
        }

        internal float CooldownRemaining(int playerId)
        {
            return _swingers.TryGetValue(playerId, out var swinger) ? swinger.Swing.CooldownRemaining : 0f;
        }

        // 持っている銃で殴る振り (と技) を振れるか。ルールの Melee が Off なら振れない
        internal MeleeRefusal Check(int playerId)
        {
            return _combat.Rules.Rules?.Actions.Melee != Melee.On ? MeleeRefusal.Off : CheckWeapon(playerId);
        }

        // 背後から倒す技を出せるか。銃で殴る振りとは別のルール (Takedown) なので、Melee が Off でも出せる (ContextMeleeAction と同じ)
        internal MeleeRefusal CheckTakedown(int playerId)
        {
            return _combat.Rules.Rules?.Actions.Takedown != Shooter.Core.Takedown.On ? MeleeRefusal.Off : CheckWeapon(playerId);
        }

        // 近接武器の振りと投げを振れるか。武器の攻撃なので、ルールの Melee が Off でも振れる (MeleeController.CheckWeaponSwing と同じ)
        internal MeleeRefusal CheckWeapon(int playerId)
        {
            if (!IsStanding(playerId)
                || _movement.IsRiding(playerId)
                || !_context.Bodies.TryGet(playerId, out var body)
                || !body.HasSpawned
                || body.IsClimbing
                || IsPerformingTakedown(playerId))
            {
                return MeleeRefusal.Busy;
            }

            return !_swingers.TryGetValue(playerId, out var swinger) || swinger.Swing.CanStart ? MeleeRefusal.None : MeleeRefusal.Cooldown;
        }

        private void TrySwing(Swinger swinger)
        {
            var refusal = Check(swinger.PlayerId);
            if (refusal != MeleeRefusal.None)
            {
                LogRefused(swinger.PlayerId, refusal);
                return;
            }

            Begin(swinger);
        }

        private void Begin(Swinger swinger)
        {
            swinger.Swing.TryStart();
            swinger.SwingWeaponId = null;
            _log.WriteLine(MeleeLog.Swing(Text(swinger.PlayerId)));
            _combat.InterruptReload(swinger.PlayerId, ReloadInterrupt.Melee);
        }

        private void LogRefused(int playerId, MeleeRefusal refusal)
        {
            if (_now >= _nextRefusalLogTime)
            {
                _nextRefusalLogTime = _now + _refusalLogInterval;
                _log.WriteLine(MeleeLog.Refused(Text(playerId), refusal));
            }
        }

        private bool IsStanding(int playerId)
        {
            var session = _combat.Rules.Session;
            return session != null && session.HasPlayer(playerId) && session.IsAlive(playerId) && !session.IsDowned(playerId);
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}

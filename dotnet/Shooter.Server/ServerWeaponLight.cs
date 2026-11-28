using System;
using System.Collections.Generic;
using System.IO;
using GameKit.Core;
using GameKit.Net;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // ウェポンライトの要求 (WeaponLightRequestMessage) を受け、今の武器に付けた物にライトがあるかを ServerPlayerWeapons と試合のアイテムの表で確かめてから点け、
    // スナップショットの Weapon の部分 (PlayerSnapshot.WeaponLightOn) で全員へ配る。点灯は Core の WeaponLightState で持ち、
    // オフラインの WeaponLightController と同じく持ち替え・しまう (ServerHolster が武装と数えない間)・ライトを外す・死亡で消す。
    // ServerHolster の後に Tick するよう並べる。同じ tick にしまい始めた人のライトを、その tick のスナップショットで消して送るため
    internal sealed class ServerWeaponLight : IServerModule, ISnapshotContributor
    {
        // 断った理由のログを出す最短の間隔 (秒)。連打でログを埋めないため
        private const float _refusalLogInterval = 1f;

        private readonly ServerCombat _combat;
        private readonly ServerHolster _holster;
        private readonly Dictionary<int, WeaponLightState> _lit = new Dictionary<int, WeaponLightState>();
        private readonly List<int> _scratch = new List<int>();
        private ServerContext _context;
        private TextWriter _log;
        private ItemCatalog _items = ItemCatalog.Empty;
        private double _now;
        private double _nextRefusalLogTime = double.NegativeInfinity;

        public ServerWeaponLight(ServerCombat combat, ServerHolster holster)
        {
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
            _holster = holster ?? throw new ArgumentNullException(nameof(holster));
        }

        public enum Refusal
        {
            None,

            // もう同じ向きになっている。要求が重なって届いただけなので、ログに残さない
            Unchanged,

            Dead,

            Downed,

            NoWeapon,

            // 今の武器に付けた物にライトが無い
            NoLight,

            // しまっている・しまう途中など、ServerHolster が武装と数えない
            Holstered,
        }

        public enum OffReason
        {
            Requested,

            Switched,

            Holstered,

            NoLight,

            Died,

            Spawned,

            MatchStarted,

            MapChanged,
        }

        public string Name => "weaponLight";

        public void Start(ServerContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _log = context.Log;
            _items = context.Rules?.Items ?? ItemCatalog.Empty;
            context.Match.PlayerLeft += HandlePlayerLeft;
            context.Match.MapChanged += HandleMapChanged;
            _combat.Rules.PlayerSpawned += HandlePlayerSpawned;
            _combat.Rules.PlayerKilled += HandlePlayerKilled;
            _combat.Rules.MatchStarted += HandleMatchStarted;
            _log.WriteLine(ServerWeaponLightLog.Started());
        }

        public void Tick(long tick, float deltaSeconds)
        {
            _now += deltaSeconds;
            if (_lit.Count == 0)
            {
                return;
            }

            // 消すと一覧から外すので、回している間に書き換えないよう先に写す
            _scratch.Clear();
            _scratch.AddRange(_lit.Keys);
            foreach (var playerId in _scratch)
            {
                Follow(playerId);
            }

            _scratch.Clear();
        }

        public void OnConnected(ConnectionId connection)
        {
        }

        public void OnDisconnected(ConnectionId connection)
        {
        }

        public void OnMessage(ConnectionId connection, IMessage message)
        {
            if (!(message is WeaponLightRequestMessage request) || !_context.Match.TryGetPlayer(connection, out var player))
            {
                return;
            }

            Request(player.PlayerId, request.On);
        }

        public void Stop()
        {
            if (_context == null)
            {
                return;
            }

            _context.Match.PlayerLeft -= HandlePlayerLeft;
            _context.Match.MapChanged -= HandleMapChanged;
            _combat.Rules.PlayerSpawned -= HandlePlayerSpawned;
            _combat.Rules.PlayerKilled -= HandlePlayerKilled;
            _combat.Rules.MatchStarted -= HandleMatchStarted;
            _lit.Clear();
        }

        // 点けていない人は一覧に持たないので、行の既定 (消えている) のまま送る。
        // 武器の無い行に点灯を書くと書き出しで投げるので、行に武器の Id がある時だけ点ける
        public void Contribute(SnapshotMessage snapshot, ServerPlayer receiver)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            if (_lit.Count == 0)
            {
                return;
            }

            var players = snapshot.Players;
            for (var i = 0; i < players.Count; i++)
            {
                ref var row = ref players[i];
                if (_lit.ContainsKey(row.PlayerId) && !string.IsNullOrEmpty(row.WeaponId))
                {
                    row.WeaponLightOn = true;
                }
            }
        }

        public bool IsOn(int playerId)
        {
            return _lit.ContainsKey(playerId);
        }

        public string LitWeaponIdOf(int playerId)
        {
            return _lit.TryGetValue(playerId, out var state) ? state.WeaponId : null;
        }

        // 今の武器に付けた物のライト。付いていなければ null
        public WeaponLightSpec LightOf(int playerId)
        {
            var weapons = _combat.WeaponsOf(playerId);
            var weaponId = weapons.CurrentSpec?.Id;
            return WeaponAttachments.LightOf(_items, weaponId, weapons.AttachmentsOf(weaponId));
        }

        internal Refusal Request(int playerId, bool on)
        {
            var refusal = on ? TurnOn(playerId) : TurnOffByRequest(playerId);
            if (refusal != Refusal.None && refusal != Refusal.Unchanged)
            {
                Refuse(playerId, refusal);
            }

            return refusal;
        }

        private Refusal TurnOn(int playerId)
        {
            if (_lit.ContainsKey(playerId))
            {
                return Refusal.Unchanged;
            }

            var refusal = CheckAlive(playerId);
            if (refusal != Refusal.None)
            {
                return refusal;
            }

            var weaponId = _combat.WeaponsOf(playerId).CurrentSpec?.Id;
            if (string.IsNullOrEmpty(weaponId))
            {
                return Refusal.NoWeapon;
            }

            if (LightOf(playerId) == null)
            {
                return Refusal.NoLight;
            }

            if (!_holster.IsArmed(playerId))
            {
                return Refusal.Holstered;
            }

            var state = new WeaponLightState();
            state.Toggle(weaponId, true, true);
            _lit.Add(playerId, state);
            _log.WriteLine(ServerWeaponLightLog.TurnedOn(playerId, weaponId));
            return Refusal.None;
        }

        // 消す要求は断らない。倒れた・しまった人が消すのも、消えている見た目に合わせるだけなので
        private Refusal TurnOffByRequest(int playerId)
        {
            return TurnOff(playerId, OffReason.Requested) ? Refusal.None : Refusal.Unchanged;
        }

        // ダウンは体力 0 で倒れた後の状態で、生きていない扱いにもなり得るので、先に見ないと Downed が出ない (ServerHolster と同じ順)
        private Refusal CheckAlive(int playerId)
        {
            var session = _combat.Rules.Session;
            if (session != null && session.HasPlayer(playerId) && session.IsDowned(playerId))
            {
                return Refusal.Downed;
            }

            if (session == null
                || !session.HasPlayer(playerId)
                || !session.IsAlive(playerId)
                || !_context.Bodies.TryGet(playerId, out var body)
                || !body.HasSpawned)
            {
                return Refusal.Dead;
            }

            return Refusal.None;
        }

        // ダウンしている間は点けたままにする。ServerHolster もダウンの間はしまう状態を戻さないため
        private void Follow(int playerId)
        {
            var session = _combat.Rules.Session;
            if (session == null || !session.HasPlayer(playerId) || (!session.IsAlive(playerId) && !session.IsDowned(playerId)))
            {
                TurnOff(playerId, OffReason.Died);
                return;
            }

            var state = _lit[playerId];
            var litWeaponId = state.WeaponId;
            var weaponId = _combat.WeaponsOf(playerId).CurrentSpec?.Id;
            var cause = state.Follow(weaponId, LightOf(playerId) != null, _holster.IsArmed(playerId));
            if (!cause.HasValue)
            {
                return;
            }

            _lit.Remove(playerId);
            _log.WriteLine(ServerWeaponLightLog.TurnedOff(playerId, litWeaponId, ReasonOf(cause.Value)));
        }

        private static OffReason ReasonOf(WeaponLightOffCause cause)
        {
            switch (cause)
            {
                case WeaponLightOffCause.Switched:
                    return OffReason.Switched;
                case WeaponLightOffCause.Holstered:
                    return OffReason.Holstered;
                case WeaponLightOffCause.NoLight:
                    return OffReason.NoLight;
                case WeaponLightOffCause.Died:
                    return OffReason.Died;
                default:
                    throw new ArgumentOutOfRangeException(nameof(cause), cause, "the server does not turn the light off for this cause");
            }
        }

        private bool TurnOff(int playerId, OffReason reason)
        {
            if (!_lit.TryGetValue(playerId, out var state))
            {
                return false;
            }

            var litWeaponId = state.WeaponId;
            _lit.Remove(playerId);
            _log.WriteLine(ServerWeaponLightLog.TurnedOff(playerId, litWeaponId, reason));
            return true;
        }

        private void TurnOffAll(OffReason reason)
        {
            _scratch.Clear();
            _scratch.AddRange(_lit.Keys);
            foreach (var playerId in _scratch)
            {
                TurnOff(playerId, reason);
            }

            _scratch.Clear();
        }

        private void Refuse(int playerId, Refusal reason)
        {
            if (_now < _nextRefusalLogTime)
            {
                return;
            }

            _nextRefusalLogTime = _now + _refusalLogInterval;
            _log.WriteLine(ServerWeaponLightLog.Refused(playerId, _combat.WeaponsOf(playerId).CurrentSpec?.Id, reason));
        }

        private void HandlePlayerLeft(ServerPlayer player)
        {
            _lit.Remove(player.PlayerId);
        }

        private void HandlePlayerSpawned(int playerId)
        {
            TurnOff(playerId, OffReason.Spawned);
        }

        private void HandlePlayerKilled(KillFeedEntry entry)
        {
            TurnOff(entry.VictimId, OffReason.Died);
        }

        private void HandleMatchStarted()
        {
            TurnOffAll(OffReason.MatchStarted);
        }

        private void HandleMapChanged(ServerMapData map)
        {
            TurnOffAll(OffReason.MapChanged);
        }
    }
}

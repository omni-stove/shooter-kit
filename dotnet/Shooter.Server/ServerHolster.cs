using System;
using System.Collections.Generic;
using System.IO;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // しまう・構えるの要求 (HolsterRequestMessage) を受け、Core の HolsterState を持っている武器の秒数で進めて、スナップショットの
    // Weapon の部分で全員へ配る。しまっている・しまう途中・構える途中の手の武器は ServerCombat が撃たせず込めさせない。
    // 秒数はオフラインと同じく、しまうのが WeaponSpec.HolsterSeconds (0 なら構える秒数)、構えるのが Loadout.DrawSecondsOf。
    // 武器を出してはいけない場所 (ServerWeaponFreeZones) は、自動でしまう反応で入った人をしまわせ、構えられない反応の中では構えるのを断り、
    // 中にいるかと警告をスナップショットの持ち主の部分で本人へ配る。別のモジュールにしないのは、しまう状態と同じ tick の順で判定するため
    internal sealed class ServerHolster : IServerModule, ISnapshotContributor
    {
        // 断った理由のログを出す最短の間隔 (秒)。連打でログを埋めないため
        private const float _refusalLogInterval = 1f;

        private readonly ServerCombat _combat;
        private readonly ServerMovement _movement;
        private readonly ServerWeaponFreeZones _zones;
        private readonly Dictionary<int, HolsterState> _holders = new Dictionary<int, HolsterState>();
        private readonly List<int> _scratch = new List<int>();
        private ServerContext _context;
        private TextWriter _log;
        private double _now;
        private double _nextRefusalLogTime = double.NegativeInfinity;

        // holster はどの状態を武装と数えるか (server-data/tuning/holster.json)。null ならオフラインの既定と同じ値にする
        public ServerHolster(ServerCombat combat, ServerMovement movement, HolsterSpec holster = null)
        {
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
            _movement = movement ?? throw new ArgumentNullException(nameof(movement));
            _zones = new ServerWeaponFreeZones(holster);
        }

        public enum Refusal
        {
            None,

            // 同じ向きへもう進んでいる・止まっている。要求が重なって届いただけなので、ログに残さない
            Unchanged,

            Dead,

            Downed,

            Riding,

            NoWeapon,

            Switching,

            // 構えられない反応 (WeaponFreeZoneReaction.BlockDraw) のゾーンの中にいる
            WeaponFreeZone,
        }

        public enum ResetReason
        {
            Died,

            Spawned,

            NoWeapon,

            Switched,

            MatchStarted,

            MapChanged,
        }

        public string Name => "holster";

        internal ServerWeaponFreeZones Zones => _zones;

        public void Start(ServerContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _log = context.Log;
            _zones.Load(context.Map.Name, context.Map.WeaponFreeZones, _log);
            _combat.IsHolstered = IsLowered;
            context.Match.PlayerLeft += HandlePlayerLeft;
            context.Match.MapChanged += HandleMapChanged;
            _combat.Rules.PlayerSpawned += HandlePlayerSpawned;
            _combat.Rules.PlayerKilled += HandlePlayerKilled;
            _combat.Rules.MatchStarted += HandleMatchStarted;
            _log.WriteLine(ServerHolsterLog.Started());
        }

        public void Tick(long tick, float deltaSeconds)
        {
            _now += deltaSeconds;
            TickHolders(deltaSeconds);

            // 構え終えた・しまい終えた後の状態で武装を数え、しまわせたら同じ tick のうちにスナップショットへ載せる
            TickZones(deltaSeconds);
        }

        private void TickHolders(float deltaSeconds)
        {
            if (_holders.Count == 0)
            {
                return;
            }

            // 戻すと一覧から外すので、回している間に書き換えないよう先に写す
            _scratch.Clear();
            _scratch.AddRange(_holders.Keys);
            foreach (var playerId in _scratch)
            {
                var state = _holders[playerId];
                var reset = ResetReasonOf(playerId);
                if (reset.HasValue)
                {
                    Reset(playerId, reset.Value);
                    continue;
                }

                if (state.Tick(deltaSeconds))
                {
                    _log.WriteLine(ServerHolsterLog.Settled(playerId, state.Phase));
                }

                if (state.Phase == HolsterPhase.Drawn)
                {
                    _holders.Remove(playerId);
                }
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
            if (!(message is HolsterRequestMessage request) || !_context.Match.TryGetPlayer(connection, out var player))
            {
                return;
            }

            Request(player.PlayerId, request.Action);
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
            _combat.IsHolstered = null;
            _holders.Clear();
            _zones.Clear();
        }

        // 構えている人は一覧に持たないので、行の既定 (Drawn・0) のまま送る。
        // ゾーンの状態は受け手ごとに書き直す。スナップショットは受け手の間で使い回すので、前の受け手の警告を残さないため
        public void Contribute(SnapshotMessage snapshot, ServerPlayer receiver)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            snapshot.Owner.WeaponFreeZone = receiver != null ? _zones.StatusOf(receiver.PlayerId) : WeaponFreeZoneStatus.Outside;
            if (_holders.Count == 0)
            {
                return;
            }

            var players = snapshot.Players;
            for (var i = 0; i < players.Count; i++)
            {
                ref var row = ref players[i];
                if (_holders.TryGetValue(row.PlayerId, out var state))
                {
                    row.HolsterPhase = state.Phase;
                    row.HolsterLowering = state.Lowering;
                }
            }
        }

        public HolsterPhase PhaseOf(int playerId)
        {
            return _holders.TryGetValue(playerId, out var state) ? state.Phase : HolsterPhase.Drawn;
        }

        public float LoweringOf(int playerId)
        {
            return _holders.TryGetValue(playerId, out var state) ? state.Lowering : 0f;
        }

        // オフラインの WeaponController.IsFireBlocked と同じく、構え終えるまでは撃たせない
        public bool IsLowered(int playerId)
        {
            return _holders.TryGetValue(playerId, out var state) && state.Phase != HolsterPhase.Drawn;
        }

        internal Refusal Request(int playerId, HolsterAction action)
        {
            var refusal = action == HolsterAction.Holster ? BeginHolster(playerId) : BeginDraw(playerId);
            if (refusal != Refusal.None && refusal != Refusal.Unchanged)
            {
                Refuse(playerId, action, refusal);
            }

            return refusal;
        }

        private Refusal BeginHolster(int playerId)
        {
            var refusal = CheckAlive(playerId);
            if (refusal != Refusal.None)
            {
                return refusal;
            }

            // オフラインと同じく、乗り物などで入力を手放している間はしまわない
            if (_movement.IsRiding(playerId))
            {
                return Refusal.Riding;
            }

            var weapons = _combat.WeaponsOf(playerId);
            if (weapons.CurrentSpec == null)
            {
                return Refusal.NoWeapon;
            }

            // 持ち替えは構え終えるまで武器を下げているので、その上からしまうと下げ具合が二重になる
            if (weapons.IsSwitching)
            {
                return Refusal.Switching;
            }

            var state = GetOrAdd(playerId);
            var seconds = weapons.CurrentHolsterSeconds;
            if (!state.BeginHolster(seconds))
            {
                return Refusal.Unchanged;
            }

            weapons.CancelReload();
            _log.WriteLine(ServerHolsterLog.Began(playerId, HolsterPhase.Holstering, seconds, weapons.CurrentSpec.Id));
            if (state.Phase == HolsterPhase.Holstered)
            {
                _log.WriteLine(ServerHolsterLog.Settled(playerId, state.Phase));
            }

            return Refusal.None;
        }

        // 乗っている・ダウンしている間も構え直せる。座席から手の武器で撃つ席や、ダウン中に撃てるルールで、しまったまま撃てなくならないように
        private Refusal BeginDraw(int playerId)
        {
            if (!_holders.TryGetValue(playerId, out var state))
            {
                return Refusal.Unchanged;
            }

            var refusal = CheckAlive(playerId);
            if (refusal != Refusal.None && refusal != Refusal.Downed)
            {
                return refusal;
            }

            // 射撃のボタンで構え直す要求もここに来るので、撃つ要求はしまったままとして ServerCombat が Holstered で断る
            if (_zones.DrawBlockedBy(playerId) != null)
            {
                return Refusal.WeaponFreeZone;
            }

            var weapons = _combat.WeaponsOf(playerId);
            var seconds = weapons.CurrentDrawSeconds;
            if (!state.BeginDraw(seconds))
            {
                return Refusal.Unchanged;
            }

            _log.WriteLine(ServerHolsterLog.Began(playerId, HolsterPhase.Drawing, seconds, weapons.CurrentSpec?.Id));
            if (state.Phase == HolsterPhase.Drawn)
            {
                _log.WriteLine(ServerHolsterLog.Settled(playerId, state.Phase));
                _holders.Remove(playerId);
            }

            return Refusal.None;
        }

        // ダウンは体力 0 で倒れた後の状態で、生きていない扱いにもなり得るので、先に見ないと Downed が出ない (ServerEmotes と同じ順)
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

        private ResetReason? ResetReasonOf(int playerId)
        {
            var session = _combat.Rules.Session;
            if (session == null || !session.HasPlayer(playerId) || (!session.IsAlive(playerId) && !session.IsDowned(playerId)))
            {
                return ResetReason.Died;
            }

            var weapons = _combat.WeaponsOf(playerId);
            if (weapons.CurrentSpec == null)
            {
                return ResetReason.NoWeapon;
            }

            // オフラインと同じく、持ち替えたら Loadout の構える時間で上げるので、しまう状態は構えた状態へ戻す。
            // 構えられないゾーンの中では戻さない。オフラインの HolsterController も持ち替えで構え直すのを BlockDraw で断るため
            if (!weapons.IsSwitching || _zones.DrawBlockedBy(playerId) != null)
            {
                return null;
            }

            return ResetReason.Switched;
        }

        private void TickZones(float deltaSeconds)
        {
            if (_zones.Count == 0)
            {
                return;
            }

            foreach (var player in _context.Match.Players)
            {
                // オフラインのゾーンもプレイヤーのタグの付いた体だけを見るので、Bot はしまわせず止めもしない
                if (player.IsBot)
                {
                    continue;
                }

                var playerId = player.PlayerId;
                System.Numerics.Vector3? center = null;
                var armed = false;
                if (IsInWorld(playerId, out var body))
                {
                    // 足元ではなく体の中ほどで見るのは、床に置いたゾーンの底と足の高さが誤差で入れ違って出入りを繰り返さないため
                    center = body.Center;
                    armed = IsArmed(playerId, body);
                }

                var holsterIn = _zones.Update(playerId, center, armed, deltaSeconds);
                if (holsterIn != null)
                {
                    _log.WriteLine(ServerWeaponFreeZonesLog.AutoHolstered(holsterIn, playerId, BeginHolster(playerId)));
                }
            }
        }

        // ダウンしている間もゾーンの中にいると数える。オフラインのゾーンも死ぬまでは中にいると数えるため
        private bool IsInWorld(int playerId, out ServerBody body)
        {
            body = null;
            var session = _combat.Rules.Session;
            return session != null
                && session.HasPlayer(playerId)
                && (session.IsAlive(playerId) || session.IsDowned(playerId))
                && _context.Bodies.TryGet(playerId, out body)
                && body.HasSpawned;
        }

        // オフラインの HolsterController.Status.IsArmed と同じく、はしごや乗り物がしまわせている間は holster.json の armedWhileHeldByOthers で数える
        private bool IsArmed(int playerId, ServerBody body)
        {
            if (_combat.WeaponsOf(playerId).CurrentSpec == null)
            {
                return false;
            }

            var heldByOthers = body.IsClimbing || _movement.IsRiding(playerId);
            return _zones.Holster.IsArmed(PhaseOf(playerId), heldByOthers);
        }

        private void Reset(int playerId, ResetReason reason)
        {
            if (!_holders.Remove(playerId))
            {
                return;
            }

            _log.WriteLine(ServerHolsterLog.Reset(playerId, reason));
        }

        private void ResetAll(ResetReason reason)
        {
            _scratch.Clear();
            _scratch.AddRange(_holders.Keys);
            foreach (var playerId in _scratch)
            {
                Reset(playerId, reason);
            }

            _scratch.Clear();
        }

        private HolsterState GetOrAdd(int playerId)
        {
            if (!_holders.TryGetValue(playerId, out var state))
            {
                state = new HolsterState();
                _holders.Add(playerId, state);
            }

            return state;
        }

        private void Refuse(int playerId, HolsterAction action, Refusal reason)
        {
            if (_now < _nextRefusalLogTime)
            {
                return;
            }

            _nextRefusalLogTime = _now + _refusalLogInterval;
            _log.WriteLine(ServerHolsterLog.Refused(playerId, action, reason));
        }

        private void HandlePlayerLeft(ServerPlayer player)
        {
            _holders.Remove(player.PlayerId);
            _zones.Forget(player.PlayerId);
        }

        private void HandlePlayerSpawned(int playerId)
        {
            Reset(playerId, ResetReason.Spawned);
        }

        private void HandlePlayerKilled(KillFeedEntry entry)
        {
            Reset(entry.VictimId, ResetReason.Died);
        }

        private void HandleMatchStarted()
        {
            ResetAll(ResetReason.MatchStarted);
        }

        private void HandleMapChanged(ServerMapData map)
        {
            ResetAll(ResetReason.MapChanged);
            _zones.Load(map.Name, map.WeaponFreeZones, _log);
        }
    }
}

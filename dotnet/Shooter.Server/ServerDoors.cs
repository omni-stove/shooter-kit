using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using GameKit.Net;
using GameKit.World;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // 蹴る・仕掛ける要求 (BreachRequestMessage) を受け、距離・秒数・品・ドアの状態を確かめてから Core の BreachDoorState を進め、
    // 変わるたびに全ドアの状態 (DoorStateMessage) を全員へ配る。破ったら板の当たりの組を外し、向こう側の箱の中の人と Bot を怯ませ、
    // 爆薬なら戦闘 (ServerCombat.Explode) で爆発させる。
    // 建物 (ServerStructures) と同じく、同じマップで続ける試合をまたいで破れたままにする。マップを替えると板の当たりを戻し、どのドアも閉じた状態から始める
    internal sealed class ServerDoors : IServerModule
    {
        public const string BreachFileName = "breach.json";

        // オフラインの BreachDoor.StaggerSource と同じ語にし、よろけのログを同じ語で検索できるようにする
        public const string StaggerSource = "breach";

        // 断った理由のログを出す最短の間隔 (秒)。押し続けてログを埋めないため (ServerInventories と同じ)
        private const float _refusalLogInterval = 1f;

        private readonly ServerCombat _combat;
        private readonly ServerPlayerStatus _status;
        private readonly ServerInventories _inventories;
        private readonly ServerNoises _noises;
        private readonly string _specPath;
        private readonly List<Door> _doors = new List<Door>();

        // 人ごとに、蹴っている・仕掛けているドアの番号。1 人が 2 枚を同時に進めないようにするため
        private readonly Dictionary<int, int> _working = new Dictionary<int, int>();
        private readonly Dictionary<int, double> _nextRefusalLogTimes = new Dictionary<int, double>();
        private readonly List<int> _targets = new List<int>();
        private readonly DoorStateMessage _state = new DoorStateMessage();
        private ServerContext _context;
        private TextWriter _log = TextWriter.Null;
        private BvhWorldCollision _collision;
        private string _mapName = string.Empty;
        private double _now;
        private bool _dirty;

        // 怯ませる (status)・品を使う (inventories)・Bot に音を聞かせる (noises) は、無ければその部分だけをしない。品を求める spec で在庫が無ければ、爆薬は仕掛けさせない
        public ServerDoors(
            ServerCombat combat,
            ServerPlayerStatus status = null,
            ServerInventories inventories = null,
            BreachSpec spec = null,
            string specPath = null,
            ServerNoises noises = null)
        {
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
            _status = status;
            _inventories = inventories;
            _noises = noises;
            Spec = spec ?? BreachSpec.Default;
            _specPath = specPath;
        }

        public enum Refusal
        {
            None,

            // 同じ人が同じことをもう進めている。要求が重なって届いただけなので、ログに残さない
            Unchanged,

            UnknownDoor,

            CannotAct,

            Staggered,

            OutOfReach,

            DoorBusy,

            ActorBusy,

            Breached,

            Charging,

            NoChargeItem,
        }

        public enum CancelReason
        {
            Requested,

            CannotAct,

            Staggered,

            OutOfReach,

            Left,

            NoChargeItem,

            MatchStarted,
        }

        public string Name => "doors";

        public BreachSpec Spec { get; }

        public int Count => _doors.Count;

        public static bool TryLoadSpec(IReadOnlyList<string> searchRoots, out BreachSpec spec, out string path, out string error)
        {
            ArgumentNullException.ThrowIfNull(searchRoots);
            if (!ServerCombatTuning.TryRead(searchRoots, BreachFileName, "breach", BreachJson.Parse, out spec, out path, out error))
            {
                return false;
            }

            spec ??= BreachSpec.Default;
            return true;
        }

        public void Start(ServerContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _log = context.Log;
            _context.Match.MapChanged += HandleMapChanged;
            _context.Match.PlayerLeft += HandlePlayerLeft;
            _combat.Rules.MatchStarted += HandleMatchStarted;
            _log.WriteLine(ServerDoorsLog.Started(Spec, _specPath));
            UseMap(context.Map);
        }

        public void Tick(long tick, float deltaSeconds)
        {
            _now += deltaSeconds;
            for (var i = 0; i < _doors.Count; i++)
            {
                var door = _doors[i];
                if (door.Kicker.HasValue)
                {
                    TickKick(i, door, deltaSeconds);
                }
                else if (door.State.Phase == BreachDoorPhase.Planting)
                {
                    TickPlant(i, door, deltaSeconds);
                }
                else if (door.State.Phase == BreachDoorPhase.Armed)
                {
                    Advance(i, door, deltaSeconds);
                }
            }

            Flush();
        }

        // 何も破れていないマップは、クライアントが最初からそう見ているので送らない (ServerStructures.SendState と同じ)
        public void OnConnected(ConnectionId connection)
        {
            if (_context == null || !AnyChanged())
            {
                return;
            }

            Fill();
            _context.Messenger.Send(connection, TransportChannel.Reliable, _state);
        }

        public void OnDisconnected(ConnectionId connection)
        {
        }

        public void OnMessage(ConnectionId connection, IMessage message)
        {
            if (!(message is BreachRequestMessage request) || !_context.Match.TryGetPlayer(connection, out var player))
            {
                return;
            }

            Request(player.PlayerId, request.DoorIndex, request.Method, request.Cancel);
            Flush();
        }

        public void Stop()
        {
            if (_context == null)
            {
                return;
            }

            _context.Match.MapChanged -= HandleMapChanged;
            _context.Match.PlayerLeft -= HandlePlayerLeft;
            _combat.Rules.MatchStarted -= HandleMatchStarted;
            _doors.Clear();
            _working.Clear();
            _nextRefusalLogTimes.Clear();
            _dirty = false;
        }

        public BreachDoorPhase PhaseOf(int door)
        {
            return IsDoor(door) ? _doors[door].State.Phase : BreachDoorPhase.Closed;
        }

        public BreachStatus StatusOf(int door)
        {
            return IsDoor(door) ? _doors[door].State.Status() : new BreachDoorState().Status();
        }

        public bool IsKicking(int door)
        {
            return IsDoor(door) && _doors[door].Kicker.HasValue;
        }

        // 符号をオフラインの BreachDoor.FarSignFrom とそろえるのは、クライアントが同じ向きで板を開き爆薬を貼るため
        public float FarSignOf(int door)
        {
            return IsDoor(door) ? _doors[door].FarSign : 1f;
        }

        internal Refusal Request(int playerId, int doorIndex, BreachMethod method, bool cancel)
        {
            if (!IsDoor(doorIndex))
            {
                if (ShouldLogRefusal(playerId))
                {
                    _log.WriteLine(ServerDoorsLog.UnknownDoor(doorIndex, playerId, method, _doors.Count));
                }

                return Refusal.UnknownDoor;
            }

            var door = _doors[doorIndex];
            if (cancel)
            {
                return CancelBy(playerId, doorIndex, door, method);
            }

            var refusal = Check(playerId, door, method, out var body);
            if (refusal != Refusal.None)
            {
                if (refusal != Refusal.Unchanged && ShouldLogRefusal(playerId))
                {
                    _log.WriteLine(ServerDoorsLog.Refused(doorIndex, playerId, method, door.State.Phase, refusal));
                }

                return refusal;
            }

            if (method == BreachMethod.Kick)
            {
                BeginKick(doorIndex, door, playerId);
            }
            else
            {
                BeginCharge(doorIndex, door, playerId, body);
            }

            return Refusal.None;
        }

        // ドアの状態を先に見るのは、破れたドアの前でいくら押しても、人の側の理由ではなくドアが破れていることを返すため
        private Refusal Check(int playerId, Door door, BreachMethod method, out ServerBody body)
        {
            body = null;
            if (door.State.IsBreached)
            {
                return Refusal.Breached;
            }

            if (door.State.IsTiming)
            {
                return method == BreachMethod.Charge && door.State.Phase == BreachDoorPhase.Planting && door.Planter == playerId
                    ? Refusal.Unchanged
                    : Refusal.Charging;
            }

            if (door.Kicker.HasValue)
            {
                return method == BreachMethod.Kick && door.Kicker.Value == playerId ? Refusal.Unchanged : Refusal.DoorBusy;
            }

            if (_working.ContainsKey(playerId))
            {
                return Refusal.ActorBusy;
            }

            var actor = CheckActor(playerId, door, out body);
            if (actor.HasValue)
            {
                return actor.Value switch
                {
                    CancelReason.Staggered => Refusal.Staggered,
                    CancelReason.OutOfReach => Refusal.OutOfReach,
                    _ => Refusal.CannotAct,
                };
            }

            if (method == BreachMethod.Charge && Spec.NeedsChargeItem && ChargeCount(playerId) <= 0)
            {
                return Refusal.NoChargeItem;
            }

            return Refusal.None;
        }

        private CancelReason? CheckActor(int playerId, Door door, out ServerBody body)
        {
            body = null;
            if (!_context.Match.TryGetPlayer(playerId, out _))
            {
                return CancelReason.Left;
            }

            if (!_combat.Rules.CanFire(playerId) || !_context.Bodies.TryGet(playerId, out body) || !body.HasSpawned)
            {
                return CancelReason.CannotAct;
            }

            if (IsStaggered(playerId))
            {
                return CancelReason.Staggered;
            }

            return Vector3.Distance(body.Center, ClosestOnPanel(door.Map, body.Center)) > Spec.Reach ? CancelReason.OutOfReach : (CancelReason?)null;
        }

        private bool IsStaggered(int playerId)
        {
            return _status != null && _status.TryGetMeters(playerId, out var meters) && meters.StaggerKind != StaggerKind.None;
        }

        private Refusal CancelBy(int playerId, int index, Door door, BreachMethod method)
        {
            if (method == BreachMethod.Kick && door.Kicker == playerId)
            {
                CancelKick(index, door, CancelReason.Requested);
                return Refusal.None;
            }

            if (method == BreachMethod.Charge && door.State.Phase == BreachDoorPhase.Planting && door.Planter == playerId)
            {
                CancelPlant(index, door, CancelReason.Requested);
                return Refusal.None;
            }

            // 取りやめる前に段が進んだ (仕掛け終えた・蹴り終えた) だけのことが多いので、ログに残さない
            return Refusal.Unchanged;
        }

        private void BeginKick(int index, Door door, int playerId)
        {
            door.Kicker = playerId;
            door.KickRemaining = Spec.KickSeconds;
            _working[playerId] = index;
            _log.WriteLine(ServerDoorsLog.KickBegan(index, playerId, Spec.KickSeconds));
            if (!(door.KickRemaining > 0f))
            {
                Kick(index, door);
            }
        }

        private void BeginCharge(int index, Door door, int playerId, ServerBody body)
        {
            door.Planter = playerId;
            door.FarSign = FarSign(door.Map, body.Position);
            _working[playerId] = index;
            _log.WriteLine(ServerDoorsLog.PlantBegan(index, playerId, Spec));
            door.State.BeginPlant(Spec.PlantSeconds, Spec.FuseSeconds);
            _dirty = true;

            // 仕掛ける秒数が 0 なら、始めたその場で仕掛け終える。品はさっき数えたので取れる
            if (door.State.Phase != BreachDoorPhase.Planting)
            {
                UseCharge(index, playerId);
                Settled(index, door, BreachDoorPhase.Planting);
            }
        }

        private void TickKick(int index, Door door, float deltaSeconds)
        {
            var reason = CheckActor(door.Kicker.Value, door, out _);
            if (reason.HasValue)
            {
                CancelKick(index, door, reason.Value);
                return;
            }

            door.KickRemaining -= deltaSeconds;
            if (door.KickRemaining <= 0f)
            {
                Kick(index, door);
            }
        }

        private void TickPlant(int index, Door door, float deltaSeconds)
        {
            var planter = door.Planter.Value;
            var reason = CheckActor(planter, door, out _);
            if (reason.HasValue)
            {
                CancelPlant(index, door, reason.Value);
                return;
            }

            // 品は仕掛け終える tick に使う。途中で取りやめた人から取らないため。BreachDoorState.Tick と同じく残りの秒数と比べる
            if (deltaSeconds >= door.State.SecondsRemaining && !UseCharge(index, planter))
            {
                CancelPlant(index, door, CancelReason.NoChargeItem);
                return;
            }

            Advance(index, door, deltaSeconds);
        }

        private void Advance(int index, Door door, float deltaSeconds)
        {
            var before = door.State.Phase;
            if (door.State.Tick(deltaSeconds))
            {
                Settled(index, door, before);
            }
        }

        private void Settled(int index, Door door, BreachDoorPhase before)
        {
            _dirty = true;
            if (before == BreachDoorPhase.Planting && door.State.Phase != BreachDoorPhase.Planting)
            {
                // 仕掛け終えた爆薬は取り外せないので、仕掛けた人は離れても撃たれてもよく、ほかのドアにも手を付けられる
                _working.Remove(door.Planter.Value);
                _log.WriteLine(ServerDoorsLog.Armed(index, door.Planter.Value, Spec.FuseSeconds));
            }

            if (door.State.Phase == BreachDoorPhase.Destroyed)
            {
                Detonate(index, door);
            }
        }

        private void Kick(int index, Door door)
        {
            var kicker = door.Kicker.Value;
            door.Kicker = null;
            door.KickRemaining = 0f;
            _working.Remove(kicker);
            door.State.Kick();
            if (_context.Bodies.TryGet(kicker, out var body))
            {
                door.FarSign = FarSign(door.Map, body.Position);
            }

            ClearPanel(door);
            _dirty = true;
            _log.WriteLine(ServerDoorsLog.Kicked(index, kicker));
            Finish(index, door, BreachMethod.Kick, kicker, door.Map.Position);
        }

        private void Detonate(int index, Door door)
        {
            int? breacher = door.Planter.HasValue && _context.Match.TryGetPlayer(door.Planter.Value, out _) ? door.Planter : null;
            door.Planter = null;

            // 板の当たりが残ると、向こう側の相手への線が板で遮られ爆発が届かないので、先に外す (オフラインの BreachDoor.Detonate と同じ)
            ClearPanel(door);
            var center = door.Map.Position + (door.Map.Facing * (-door.FarSign * door.Map.PanelSize.Z * 0.5f));
            if (Spec.ChargeRadius > 0f)
            {
                var explosion = new ExplosionSpec(new ExplosionDamage(Spec.ChargeDamage, Spec.ChargeRadius));
                _combat.Explode(center, explosion, breacher ?? PlayerIds.None, Spec.ChargeItemId);
            }

            _log.WriteLine(ServerDoorsLog.Detonated(index, breacher, Spec));
            Finish(index, door, BreachMethod.Charge, breacher, center);
        }

        // 音はオフラインの BreachDoor.Finish と同じく、蹴りは板の中心、爆薬は貼った所から鳴らす
        private void Finish(int index, Door door, BreachMethod method, int? breacher, Vector3 soundPoint)
        {
            var kind = BreachSpec.StaggerKindFor(method);
            var seconds = Spec.StaggerSecondsFor(method);

            // 怯ませた知らせで一覧が変わっても回し続けられるよう、写してから回す
            _targets.Clear();
            foreach (var body in _context.Bodies.All)
            {
                if (body.PlayerId != breacher && body.HasSpawned && _combat.IsHittable(body.PlayerId) && IsInRoom(door, body))
                {
                    _targets.Add(body.PlayerId);
                }
            }

            var staggered = 0;
            if (_status != null)
            {
                foreach (var playerId in _targets)
                {
                    if (_status.Impose(playerId, kind, seconds, StaggerSource))
                    {
                        staggered++;
                    }
                }
            }

            _log.WriteLine(ServerDoorsLog.Staggered(index, method, Spec, staggered));
            Sound(index, method, breacher, soundPoint);
        }

        private void Sound(int index, BreachMethod method, int? breacher, Vector3 point)
        {
            var radius = Spec.SoundRadiusFor(method);
            if (_noises == null || !(radius > 0f))
            {
                return;
            }

            var kind = method == BreachMethod.Kick ? SoundCueKind.Clatter : SoundCueKind.Explosion;
            _noises.Emit(breacher ?? PlayerIds.None, new SoundStimulus(kind, point, radius, false));
            _log.WriteLine(ServerDoorsLog.Sounded(index, method, kind, radius));
        }

        // オフラインの BreachDoor.RoomBox と同じ箱 (板の足元から、向こう側へ奥行き・左右へ幅・上へ高さ) に、体のカプセルが掛かるか。
        // 体は立っているので、水平は芯の位置を半径だけ広げた箱で、上下はカプセルの下端から上端までで測る
        private bool IsInRoom(Door door, ServerBody body)
        {
            var map = door.Map;
            var halfWidth = Spec.RoomWidth * 0.5f;
            var halfHeight = Spec.RoomHeight * 0.5f;
            var halfDepth = Spec.RoomDepth * 0.5f;
            var foot = map.Position - new Vector3(0f, map.PanelSize.Y * 0.5f, 0f);
            var center = foot + new Vector3(0f, halfHeight, 0f) + (map.Facing * (door.FarSign * halfDepth));
            body.Capsule(out var bottom, out var top);
            var radius = body.Radius;
            var offset = bottom - center;
            if (MathF.Abs(Vector3.Dot(offset, Right(map))) > halfWidth + radius || MathF.Abs(Vector3.Dot(offset, map.Facing)) > halfDepth + radius)
            {
                return false;
            }

            return bottom.Y - radius <= center.Y + halfHeight && top.Y + radius >= center.Y - halfHeight;
        }

        private static Vector3 ClosestOnPanel(MapDoor door, Vector3 point)
        {
            var offset = point - door.Position;
            var right = Right(door);
            var half = door.PanelSize * 0.5f;
            var x = Math.Clamp(Vector3.Dot(offset, right), -half.X, half.X);
            var y = Math.Clamp(offset.Y, -half.Y, half.Y);
            var z = Math.Clamp(Vector3.Dot(offset, door.Facing), -half.Z, half.Z);
            return door.Position + (right * x) + new Vector3(0f, y, 0f) + (door.Facing * z);
        }

        private static Vector3 Right(MapDoor door)
        {
            return new Vector3(door.Facing.Z, 0f, -door.Facing.X);
        }

        private static float FarSign(MapDoor door, Vector3 breacher)
        {
            return Vector3.Dot(breacher - door.Position, door.Facing) > 0f ? -1f : 1f;
        }

        private void CancelKick(int index, Door door, CancelReason reason)
        {
            var kicker = door.Kicker.Value;
            door.Kicker = null;
            door.KickRemaining = 0f;
            _working.Remove(kicker);
            _log.WriteLine(ServerDoorsLog.KickCancelled(index, kicker, reason));
        }

        private void CancelPlant(int index, Door door, CancelReason reason)
        {
            var planter = door.Planter.Value;
            if (!door.State.Cancel())
            {
                return;
            }

            door.Planter = null;
            _working.Remove(planter);
            _dirty = true;
            _log.WriteLine(ServerDoorsLog.PlantCancelled(index, planter, reason));
        }

        private bool UseCharge(int index, int playerId)
        {
            if (!Spec.NeedsChargeItem)
            {
                return true;
            }

            var inventory = _inventories?.InventoryOf(playerId);
            if (inventory == null || inventory.Remove(Spec.ChargeItemId, 1) == 0)
            {
                return false;
            }

            _log.WriteLine(ServerDoorsLog.ChargeUsed(index, playerId, Spec.ChargeItemId));
            return true;
        }

        private int ChargeCount(int playerId)
        {
            return _inventories?.InventoryOf(playerId)?.CountOf(Spec.ChargeItemId) ?? 0;
        }

        private void ClearPanel(Door door)
        {
            _collision.SetGroupEnabled(door.Map.ColliderGroup, false);
        }

        private bool IsDoor(int index)
        {
            return index >= 0 && index < _doors.Count;
        }

        private bool AnyChanged()
        {
            foreach (var door in _doors)
            {
                if (!door.State.IsClosed)
                {
                    return true;
                }
            }

            return false;
        }

        private void Fill()
        {
            _state.Clear();
            foreach (var door in _doors)
            {
                _state.Add(door.State.Phase, door.FarSign > 0f);
            }
        }

        private void Flush()
        {
            if (!_dirty)
            {
                return;
            }

            _dirty = false;
            Fill();
            _combat.SendToAll(_state);
        }

        private bool ShouldLogRefusal(int playerId)
        {
            if (_nextRefusalLogTimes.TryGetValue(playerId, out var next) && _now < next)
            {
                return false;
            }

            _nextRefusalLogTimes[playerId] = _now + _refusalLogInterval;
            return true;
        }

        private void UseMap(ServerMapData map)
        {
            _doors.Clear();
            _working.Clear();
            _dirty = false;
            _collision = map.Collision;
            _mapName = map.Name;
            var doors = map.Doors.Doors;

            // マップの当たりは読んだマップごとに 1 つで、前にこのマップで遊んだ試合で外した板が残っているので、閉じたドアから始める
            foreach (var door in doors)
            {
                _collision.SetGroupEnabled(door.ColliderGroup, true);
            }

            if (doors.Count > DoorStateMessage.MaxDoors)
            {
                _log.WriteLine(ServerDoorsLog.TooManyDoors(_mapName, doors.Count, DoorStateMessage.MaxDoors));
                return;
            }

            foreach (var door in doors)
            {
                _doors.Add(new Door(door));
            }

            _log.WriteLine(ServerDoorsLog.MapLoaded(_mapName, _doors.Count));
        }

        private void HandleMapChanged(ServerMapData map)
        {
            UseMap(map);
        }

        private void HandlePlayerLeft(ServerPlayer player)
        {
            _nextRefusalLogTimes.Remove(player.PlayerId);
            if (!_working.TryGetValue(player.PlayerId, out var index))
            {
                return;
            }

            var door = _doors[index];
            if (door.Kicker == player.PlayerId)
            {
                CancelKick(index, door, CancelReason.Left);
            }
            else
            {
                CancelPlant(index, door, CancelReason.Left);
            }

            Flush();
        }

        // 仕掛け終えた爆薬は取り外せないので、試合が始め直しても爆ぜる。取りやめるのは蹴っている・仕掛けている途中だけ
        private void HandleMatchStarted()
        {
            for (var i = 0; i < _doors.Count; i++)
            {
                var door = _doors[i];
                if (door.Kicker.HasValue)
                {
                    CancelKick(i, door, CancelReason.MatchStarted);
                }
                else if (door.State.Phase == BreachDoorPhase.Planting)
                {
                    CancelPlant(i, door, CancelReason.MatchStarted);
                }
            }

            Flush();
        }

        private sealed class Door
        {
            public Door(MapDoor map)
            {
                Map = map;
            }

            public MapDoor Map { get; }

            public BreachDoorState State { get; } = new BreachDoorState();

            public int? Kicker { get; set; }

            public float KickRemaining { get; set; }

            // 仕掛けている人。仕掛け終えた後も、爆ぜたときに破った人として数えるため爆ぜるまで持つ
            public int? Planter { get; set; }

            public float FarSign { get; set; } = 1f;
        }
    }
}

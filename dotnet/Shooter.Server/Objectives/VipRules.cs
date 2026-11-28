using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using GameKit.Core;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // GameRules.Objective が Vip のとき、攻守のラウンド・VIP の選び方・脱出・勝ち負けをオフラインと同じ Core の Vip に任せ、
    // ここは試合の出来事と脱出地点に居るかを渡すだけにする。脱出地点はマップの NetMapExtractionPoint を使う。
    // VIP の体力の上限は JuggernautRules と同じく Session に書き、VIP が替わったときとラウンドの終わりに戻す
    internal sealed class VipRules : IMatchObjectiveRules
    {
        // VIP は 1 人なので場の物も 1 つ。持ち主を Id にしないのは、替わるたびに別の物として消して出し直させないため
        internal const int VipItemId = 0;

        // 脱出地点は VIP の物の後ろに、地点の Id の順に振る
        internal const int FirstExitItemId = VipItemId + 1;

        private readonly IMatchObjectiveHost _host;
        private readonly Vip _game;
        private readonly Dictionary<int, HeldGoal> _botGoals = new Dictionary<int, HeldGoal>();
        private NetMapExtractionPoint[] _exits;
        private bool _started;

        // 体を VIP のものにした人。替わったとき・ラウンドの終わり・試合の頭・目的を止めるときに戻す
        private int? _bodyHolder;

        // ラウンドの頭の出直しは次の Tick で行う。キルの知らせ (Session の Killed の中) でラウンドが終わったとき、
        // Session の出来事の途中で全員を出直させないため
        private bool _respawnPending;

        public VipRules(NetMatchSettings settings, IMatchObjectiveHost host)
            : this(settings, host, null)
        {
        }

        // pickIndex (0 から count 未満の添字を返す) を渡せるのは、テストで VIP を決まった人にするため。null なら Vip が乱数で選ぶ
        internal VipRules(NetMatchSettings settings, IMatchObjectiveHost host, Func<int, int> pickIndex)
        {
            ArgumentNullException.ThrowIfNull(settings);
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _game = new Vip(settings.Vip, pickIndex);
        }

        public MatchObjective Objective => MatchObjective.Vip;

        public Vip Game => _game;

        public VipSpec Spec => _game.Spec;

        // UseMap の前は空。地点の Id の順に並ぶ
        public IReadOnlyList<NetMapExtractionPoint> Exits => _exits ?? Array.Empty<NetMapExtractionPoint>();

        public bool PlaysInRounds => true;

        // 試合の点数はラウンドの勝ち数にする
        public bool KeepsMatchScore => true;

        // VipSpec に用意の時間が無く、ラウンドはすぐに撃ち合いから始まる
        public bool HoldsFire => false;

        public float? RemainingSeconds
        {
            get
            {
                switch (_game.Phase)
                {
                    case RoundPhase.Ended:
                        return 0f;
                    case RoundPhase.Waiting:
                        return _game.Spec.RoundSeconds;
                    default:
                        return Math.Max(0f, _game.RoundRemaining);
                }
            }
        }

        // 試合全体を時間とキル数で切らず、ラウンドの勝ち数で区切るので、Session の上限は届かない値にする
        public TeamDeathmatchSpec CreateSessionSpec(GameRules rules, NetMatchSettings settings)
        {
            ArgumentNullException.ThrowIfNull(rules);
            ArgumentNullException.ThrowIfNull(settings);
            return new TeamDeathmatchSpec(int.MaxValue, float.MaxValue, settings.RespawnSeconds, rules, respawnsByRound: true);
        }

        public void CheckMap(ServerMapData map, NetMapPoints points)
        {
            ArgumentNullException.ThrowIfNull(map);
            ArgumentNullException.ThrowIfNull(points);
            _ = BuildExits(map, points, "cannot change to map " + map.Name + " for the " + Objective + " match: ");
        }

        public void UseMap(ServerMapData map, NetMapPoints points)
        {
            ArgumentNullException.ThrowIfNull(map);
            ArgumentNullException.ThrowIfNull(points);
            _exits = BuildExits(map, points, "cannot start the " + Objective + " match on map " + map.Name + ": ");
            _botGoals.Clear();
        }

        public void Start()
        {
            if (_exits == null)
            {
                throw new InvalidOperationException("the " + Objective + " rules have no map; call UseMap before Start");
            }

            if (_started)
            {
                return;
            }

            _started = true;
            _game.Rounds.PreparationStarted += HandlePreparationStarted;
            _game.RoundStarted += HandleRoundStarted;
            _game.VipChosen += HandleVipChosen;
            _game.VipEscaped += HandleVipEscaped;
            _game.RoundEnded += HandleRoundEnded;
            _game.SidesSwapped += HandleSidesSwapped;
            _game.Ended += HandleEnded;
            _host.Log.WriteLine(VipLog.SettingsLog(_game.Spec));
            _host.Log.WriteLine(VipRulesLog.Started(_exits));
        }

        public void Stop()
        {
            if (!_started)
            {
                return;
            }

            _started = false;
            _game.Rounds.PreparationStarted -= HandlePreparationStarted;
            _game.RoundStarted -= HandleRoundStarted;
            _game.VipChosen -= HandleVipChosen;
            _game.VipEscaped -= HandleVipEscaped;
            _game.RoundEnded -= HandleRoundEnded;
            _game.SidesSwapped -= HandleSidesSwapped;
            _game.Ended -= HandleEnded;
            _botGoals.Clear();
            _respawnPending = false;

            // ルールを替えて次の目的に移っても体は同じ人のまま残るので、VIP の体力の上限を持ち越させない
            RestoreBody();
        }

        public bool Tick(bool matchPlaying, float deltaSeconds)
        {
            if (!_started || !matchPlaying)
            {
                return false;
            }

            RespawnIfPending();
            if (_game.Phase == RoundPhase.Waiting)
            {
                _game.Start();
                RespawnIfPending();
            }

            if (_game.Phase == RoundPhase.Live && _game.Mode.State == GameModeState.Playing)
            {
                UpdateExits();
            }

            if (_game.Phase != RoundPhase.Ended)
            {
                _game.Tick(deltaSeconds);
                RespawnIfPending();
            }

            return _game.Phase == RoundPhase.Ended;
        }

        // 試合の合間にチームを移った人とマップが替わったときにも呼ばれるので、同じ陣営に居る人は入れ直さない
        public void PlayerJoined(int playerId)
        {
            var session = _host.Session;
            if (session == null || !session.HasPlayer(playerId))
            {
                return;
            }

            var side = Side.FromTeam(session.GetTeam(playerId));
            if (_game.HasPlayer(playerId))
            {
                if (_game.Mode.GetSide(playerId) == side)
                {
                    return;
                }

                // VIP がチームを移ると Vip が選び直すので、前の体は VipChosen で戻る
                _game.RemovePlayer(playerId);
            }

            _game.AddPlayer(playerId, side);
        }

        public void PlayerLeft(int playerId)
        {
            _botGoals.Remove(playerId);

            // Session は抜けた人の上限を戻し、体は抜けると捨てられるので、戻さずに忘れる
            if (_bodyHolder == playerId)
            {
                _bodyHolder = null;
            }

            if (_game.HasPlayer(playerId))
            {
                _game.RemovePlayer(playerId);
            }
        }

        // 倒した人が試合の顔ぶれにいなければ (抜けた後の弾など)、倒れたことだけを数える
        public void PlayerKilled(int killerId, int victimId)
        {
            if (!_game.HasPlayer(victimId) || _game.Phase != RoundPhase.Live)
            {
                return;
            }

            if (_game.HasPlayer(killerId))
            {
                _game.ReportKill(killerId, victimId);
            }
            else
            {
                _game.ReportDeath(victimId);
            }
        }

        // Session は Restart で体力の上限を戻した後にここを呼ぶ。Vip.Reset は VIP を外したと知らせないので、体はここで戻す
        public void MatchRestarted()
        {
            RestoreBody();
            _game.Reset();
            _botGoals.Clear();
            _respawnPending = false;
            _host.SetMatchScores(0, 0);
            _host.Log.WriteLine(VipRulesLog.Restart());
        }

        public void MatchEnded()
        {
        }

        // 試合が終わった後もラウンドが Live のまま残りうるので、試合の終わりを Ended として送る
        public RoundPhase? CurrentRoundPhase()
        {
            var session = _host.Session;
            return session != null && session.State == GameModeState.Ended ? RoundPhase.Ended : _game.Phase;
        }

        public Team? DecideWinner(Team? killWinner)
        {
            if (_game.Phase == RoundPhase.Ended)
            {
                return Side.ToTeam(_game.Winner);
            }

            return DominationObjective.Leader(_game.GetRoundWins(Side.Alpha), _game.GetRoundWins(Side.Bravo));
        }

        // VIP の Bot はいちばん近い脱出地点へ、ほかの Bot は攻守のどちらも VIP へ向かう (守る側はついて行き、攻める側は追う)。
        // 毎 tick VIP の今の場所を返すと Bot の路を毎回作り直すので、VIP が替わるか、持っている先から Bot までより遠くへ動くまでは同じ先を返す
        public Vector3? BotGoal(int playerId)
        {
            var vipId = _game.VipId;
            var bodies = _host.Bodies;
            if (!_started
                || _exits == null
                || _game.Phase != RoundPhase.Live
                || _game.Mode.State != GameModeState.Playing
                || !vipId.HasValue
                || !_game.HasPlayer(playerId)
                || !IsStanding(vipId.Value)
                || bodies == null
                || !bodies.TryGet(vipId.Value, out var vip)
                || !bodies.TryGet(playerId, out var self))
            {
                _botGoals.Remove(playerId);
                return null;
            }

            if (playerId == vipId.Value)
            {
                _botGoals.Remove(playerId);
                return _exits[NearestExit(vip.Position)].Center;
            }

            if (_botGoals.TryGetValue(playerId, out var held)
                && held.VipId == vipId.Value
                && Vector3.Distance(held.Position, vip.Position) <= Vector3.Distance(held.Position, self.Position))
            {
                return held.Position;
            }

            var goal = vip.Position;
            _botGoals[playerId] = new HeldGoal(vipId.Value, goal);
            return goal;
        }

        // AlphaPoints・BravoPoints にラウンドの勝ち数を、PlantFraction に VIP の脱出の進み (0〜1) を載せる。
        // Search and Destroy と同じ欄を使うのは、どちらも「1 人が場所に留まる長さ」を 1 つの割合で送るため
        public void FillMatch(ref MatchSnapshot snapshot)
        {
            snapshot.Objective = Objective;
            snapshot.AlphaPoints = _game.GetRoundWins(Side.Alpha);
            snapshot.BravoPoints = _game.GetRoundWins(Side.Bravo);
            snapshot.HasBomb = false;
            snapshot.Bomb = BombState.Carried;
            snapshot.HasActivePoint = false;
            snapshot.ActivePointIndex = 0;
            snapshot.ObjectiveRemainingSeconds = 0;
            snapshot.RoundNumber = _game.RoundNumber;
            snapshot.RoundsToWin = _game.WinsToWin;
            snapshot.HasLastRound = _game.LastRoundEnd.HasValue;
            snapshot.LastRoundEnd = _game.LastRoundEnd.HasValue ? EndReason(_game.LastRoundEnd.Value) : default;
            snapshot.HasLastRoundWinner = _game.LastRoundEnd.HasValue && _game.LastRoundWinner.HasValue;
            snapshot.LastRoundWinner = snapshot.HasLastRoundWinner ? _game.LastRoundWinner.Value.ToTeam() : Team.Alpha;
            snapshot.PlantFraction = _game.VipId.HasValue ? Fraction(_game.ExitFraction) : 0f;
            snapshot.HasDefuser = false;
            snapshot.DefuserId = 0;
            snapshot.DefuseFraction = 0f;
        }

        public void FillControlPoints(SnapshotList<ControlPointSnapshot> points)
        {
            ArgumentNullException.ThrowIfNull(points);
            points.Clear();
        }

        // VIP の持ち主が VIP。立っている間は本人が運び (Carried)、倒れている間は体の場所に落ちる (Dropped)。
        // 物のチームは守る側にして、クライアントが攻守を知れるようにする。脱出地点はいつも開いている
        public void FillObjectiveItems(SnapshotList<ObjectiveItemSnapshot> items)
        {
            ArgumentNullException.ThrowIfNull(items);
            items.Clear();
            var defender = _game.Defender.ToTeam();
            var vipId = _game.VipId;
            if (vipId.HasValue)
            {
                var id = vipId.Value;
                var standing = IsStanding(id);
                ref var item = ref items.Add();
                item.ItemId = VipItemId;
                item.Present = ObjectiveItemFields.All;
                item.Kind = ObjectiveItemKind.Vip;
                item.Team = defender;
                item.HasOwner = true;
                item.OwnerId = id;
                item.Position = _host.Bodies != null && _host.Bodies.TryGet(id, out var body) ? body.Position : Vector3.Zero;
                item.State = standing ? ObjectiveItemState.Carried : ObjectiveItemState.Dropped;
                item.HasCarrier = standing;
                item.CarrierId = standing ? id : 0;
                item.Opened = false;
                item.Radius = 0f;
            }

            if (_exits == null)
            {
                return;
            }

            for (var i = 0; i < _exits.Length && items.Count < items.MaxCount; i++)
            {
                var exit = _exits[i];
                ref var item = ref items.Add();
                item.ItemId = FirstExitItemId + i;
                item.Present = ObjectiveItemFields.All;
                item.Kind = ObjectiveItemKind.ExtractionPoint;
                item.Team = defender;
                item.HasOwner = false;
                item.OwnerId = 0;
                item.Position = exit.Center;
                item.State = ObjectiveItemState.Home;
                item.HasCarrier = false;
                item.CarrierId = 0;
                item.Opened = true;
                item.Radius = exit.Radius;
            }
        }

        public void FillPlayer(int playerId, ref PlayerSnapshot snapshot)
        {
        }

        // RoundEndReason は Search and Destroy の写しに 1 バイトで載るので値を足さず (VipRoundEnd の注記)、ExecutionRules と同じく近い意味の値に読み替える。
        // 脱出は守る側が目的を果たした解除 (Defused)、VIP が倒れたのは攻める側が目的を果たした起爆 (Detonated)、外から終えたのは勝者の無い BothEliminated
        internal static RoundEndReason EndReason(VipRoundEnd reason)
        {
            switch (reason)
            {
                case VipRoundEnd.Escaped:
                    return RoundEndReason.Defused;
                case VipRoundEnd.VipDown:
                    return RoundEndReason.Detonated;
                case VipRoundEnd.TimeUp:
                    return RoundEndReason.TimeUp;
                case VipRoundEnd.AttackersEliminated:
                    return RoundEndReason.AttackersEliminated;
                case VipRoundEnd.DefendersEliminated:
                    return RoundEndReason.DefendersEliminated;
                case VipRoundEnd.Finished:
                    return RoundEndReason.BothEliminated;
                default:
                    throw new ArgumentOutOfRangeException(nameof(reason), reason, "reason is not a defined VipRoundEnd");
            }
        }

        // 浮動小数の誤差で 1 をわずかに超えても書けるよう、0〜1 に収める
        private static float Fraction(float value)
        {
            return value > 0f ? Math.Min(1f, value) : 0f;
        }

        // 地点の Id の順に並べるのは、スナップショットの物の Id がマップの書き出しの順に左右されないようにするため (ExtractionRules と同じ)。
        // VIP の物と同じ並びで送るので、VIP の 1 つと合わせて並びの上限に収まらないマップも断る
        private NetMapExtractionPoint[] BuildExits(ServerMapData map, NetMapPoints points, string failure)
        {
            var count = points.ExtractionPoints.Count;
            if (count == 0)
            {
                _host.Log.WriteLine(VipRulesLog.NoExtractionPoints(map.Name));
                throw new InvalidOperationException(
                    failure + "it has no extraction points for the vip to escape to; place ExtractionZones in the scene and export the map points with make export-server-rules");
            }

            var max = SnapshotMessage.MaxObjectiveItems - FirstExitItemId;
            if (count > max)
            {
                _host.Log.WriteLine(VipRulesLog.TooManyExtractionPoints(map.Name, count, max));
                throw new InvalidOperationException(
                    failure + "it has " + count.ToString(CultureInfo.InvariantCulture) + " extraction points but at most "
                    + max.ToString(CultureInfo.InvariantCulture) + " fit in a snapshot");
            }

            var sorted = new NetMapExtractionPoint[count];
            for (var i = 0; i < count; i++)
            {
                sorted[i] = points.ExtractionPoints[i];
            }

            Array.Sort(sorted, (a, b) => string.CompareOrdinal(a.Id, b.Id));
            return sorted;
        }

        // 倒れている人とダウン中の人は脱出地点に居ても数えない
        private void UpdateExits()
        {
            var session = _host.Session;
            var bodies = _host.Bodies;
            if (session == null || bodies == null)
            {
                return;
            }

            foreach (var body in bodies.All)
            {
                var playerId = body.PlayerId;
                if (!_game.HasPlayer(playerId))
                {
                    continue;
                }

                _game.SetAtExit(playerId, IsStanding(playerId) && InAnyExit(body.Position));
            }
        }

        private bool InAnyExit(Vector3 position)
        {
            foreach (var exit in _exits)
            {
                if (exit.Contains(position))
                {
                    return true;
                }
            }

            return false;
        }

        // 高さを見ないのは、NetMapExtractionPoint.Contains と同じ測り方にするため
        private int NearestExit(Vector3 from)
        {
            var nearest = 0;
            var best = float.MaxValue;
            for (var i = 0; i < _exits.Length; i++)
            {
                var dx = _exits[i].Center.X - from.X;
                var dz = _exits[i].Center.Z - from.Z;
                var distance = (dx * dx) + (dz * dz);
                if (distance < best)
                {
                    best = distance;
                    nearest = i;
                }
            }

            return nearest;
        }

        // Session の無いとき (目的だけを回すとき) は、Vip が倒れていると数えているかで決める
        private bool IsStanding(int playerId)
        {
            var session = _host.Session;
            if (session != null && session.HasPlayer(playerId))
            {
                return session.IsAlive(playerId) && !session.IsDowned(playerId);
            }

            return _game.HasPlayer(playerId) && !_game.Mode.IsDown(playerId);
        }

        // 体力と位置は Session が持つので、全員を出直させる。倒れている人の蘇生の受け付けも捨てる
        private void RespawnIfPending()
        {
            if (!_respawnPending)
            {
                return;
            }

            _respawnPending = false;
            _host.ClearDowned();
            _host.Session?.RespawnAll();
        }

        // Vip が VIP を選んだ後に呼ばれる (Vip は RoundSession の PreparationStarted を先に受ける)
        private void HandlePreparationStarted(int round)
        {
            _host.Log.WriteLine(VipRulesLog.Round(round, _game.Attacker, _game.Defender));
            _botGoals.Clear();
            _respawnPending = true;
        }

        private void HandleRoundStarted(int round)
        {
            _host.Log.WriteLine(VipRulesLog.Live(round));
        }

        private void HandleVipChosen(int playerId)
        {
            if (_bodyHolder.HasValue && _bodyHolder.Value != playerId)
            {
                RestoreBody();
            }

            GiveBody(playerId);
        }

        // 立っている人は新しい上限まで満たす。倒れている人には Heal が効かず、ラウンドの頭の出直しで満タンになる
        private void GiveBody(int playerId)
        {
            var maxHealth = _game.Spec.VipMaxHealth;
            var session = _host.Session;
            if (session != null && session.HasPlayer(playerId))
            {
                session.SetMaxHealth(playerId, maxHealth);
                session.Heal(playerId, maxHealth);
            }

            _bodyHolder = playerId;
            _host.Log.WriteLine(VipRulesLog.Chosen(_game.RoundNumber, playerId, maxHealth));
        }

        private void RestoreBody()
        {
            if (!_bodyHolder.HasValue)
            {
                return;
            }

            var playerId = _bodyHolder.Value;
            _bodyHolder = null;
            var session = _host.Session;
            if (session != null && session.HasPlayer(playerId))
            {
                session.SetMaxHealth(playerId, session.MaxHealth);
            }

            _host.Log.WriteLine(VipRulesLog.Restored(playerId));
        }

        private void HandleVipEscaped(int playerId)
        {
            _host.Log.WriteLine(VipRulesLog.Escaped(playerId));
        }

        // RoundEnded は入れ替えの前に来るので、守る側はこのラウンドの守る側
        private void HandleRoundEnded(Side winner, VipRoundEnd reason)
        {
            RestoreBody();
            var alpha = _game.GetRoundWins(Side.Alpha);
            var bravo = _game.GetRoundWins(Side.Bravo);
            _host.Log.WriteLine(VipRulesLog.RoundEnded(_game.RoundNumber, winner, _game.Defender, reason, alpha, bravo));
            _host.SetMatchScores(alpha, bravo);
        }

        private void HandleSidesSwapped()
        {
            _host.Log.WriteLine(VipRulesLog.Swapped(_game.Attacker, _game.Defender));
        }

        private void HandleEnded(Side winner)
        {
            _host.Log.WriteLine(VipRulesLog.Ended(winner));
        }

        private readonly struct HeldGoal
        {
            public HeldGoal(int vipId, Vector3 position)
            {
                VipId = vipId;
                Position = position;
            }

            public int VipId { get; }

            public Vector3 Position { get; }
        }
    }
}

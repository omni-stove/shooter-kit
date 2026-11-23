using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // GameRules.Objective が Juggernaut のとき、ジャガーノートの選び方・引き継ぎ・点・勝ち負けをオフラインと同じ Core の Juggernaut に任せ、
    // ここは試合の出来事を渡すだけにする。Session (TeamDeathmatchSession) の 2 チームで数えないのは、Juggernaut が 1 人 1 陣営 (FreeForAll) で
    // 点と勝者を決めるため。Session は体力・出直し・出現だけを受け持ち、キル数でも時間でも先に終わらないよう上限を届かない値にする。
    // ジャガーノートの体 (体力の上限・移動の速さ) は InfectedRules と同じく Session と体に書き、替わったら前の人の体を戻す
    internal sealed class JuggernautRules : IMatchObjectiveRules
    {
        // ジャガーノートは 1 人なので、場の物も 1 つ。持ち主を Id にしないのは、替わるたびに別の物として消して出し直させないため
        internal const int ItemId = 0;

        private readonly IMatchObjectiveHost _host;
        private readonly Juggernaut _juggernaut;
        private readonly float _respawnSeconds;
        private readonly Dictionary<int, LeftRecord> _left = new Dictionary<int, LeftRecord>();
        private readonly Dictionary<int, HeldGoal> _botGoals = new Dictionary<int, HeldGoal>();

        // 体をジャガーノートのものにした人。替わったとき・試合の頭・目的を止めるときに戻す
        private int? _bodyHolder;
        private bool _started;
        private bool _ended;
        private int? _winnerId;
        private int _sentLeadingScore;
        private int _sentJuggernautScore;

        public JuggernautRules(NetMatchSettings settings, IMatchObjectiveHost host)
            : this(settings, host, null)
        {
        }

        // pickIndex (0 から count 未満の添字を返す) を渡せるのは、テストでジャガーノートを決まった人にするため。null なら Juggernaut が乱数で選ぶ
        internal JuggernautRules(NetMatchSettings settings, IMatchObjectiveHost host, Func<int, int> pickIndex)
        {
            ArgumentNullException.ThrowIfNull(settings);
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _respawnSeconds = settings.RespawnSeconds;
            _juggernaut = new Juggernaut(settings.Juggernaut, _respawnSeconds, pickIndex);
        }

        public MatchObjective Objective => MatchObjective.Juggernaut;

        public Juggernaut Juggernaut => _juggernaut;

        public JuggernautSpec Spec => _juggernaut.Spec;

        // 決着していない・勝った人が終わる前に抜けた・引き分けなら null
        public int? WinnerId => _ended ? _winnerId : _juggernaut.WinnerId;

        public bool PlaysInRounds => false;

        // チームのキル数はジャガーノートの点ではないので、試合の点数を目的が書く
        public bool KeepsMatchScore => true;

        public bool HoldsFire => false;

        public float? RemainingSeconds => Math.Max(0f, _juggernaut.Session.RemainingSeconds);

        // Session は 2 チームでしか数えないので、同じチームの相手も撃てるようにし、味方による蘇生を外す (FreeForAllRules と同じ)。
        // 出直しの秒を Juggernaut と同じ値にするのは、Session と Juggernaut で倒れている間の長さをそろえるため
        public TeamDeathmatchSpec CreateSessionSpec(GameRules rules, NetMatchSettings settings)
        {
            ArgumentNullException.ThrowIfNull(rules);
            ArgumentNullException.ThrowIfNull(settings);
            var sessionRules = rules
                .WithFriendlyFire(FriendlyFire.On)
                .WithRespawnMode(RespawnMode.Individual)
                .WithActions(rules.Actions.WithDowned(Downed.Off));
            return new TeamDeathmatchSpec(int.MaxValue, float.MaxValue, _respawnSeconds, sessionRules);
        }

        public void CheckMap(ServerMapData map, NetMapPoints points)
        {
        }

        public void UseMap(ServerMapData map, NetMapPoints points)
        {
        }

        public void Start()
        {
            if (_started)
            {
                return;
            }

            _started = true;
            _juggernaut.Changed += HandleChanged;
            _host.Log.WriteLine(JuggernautRulesLog.Started(_juggernaut.Spec, _respawnSeconds));
        }

        public void Stop()
        {
            if (!_started)
            {
                return;
            }

            _started = false;
            _juggernaut.Changed -= HandleChanged;
            _botGoals.Clear();

            // ルールを替えて次の目的に移っても体は同じ人のまま残るので、ジャガーノートの体力と速さを持ち越させない
            RestoreBody();
        }

        public bool Tick(bool matchPlaying, float deltaSeconds)
        {
            if (!matchPlaying)
            {
                return false;
            }

            var mode = _juggernaut.Session;
            if (mode.State == GameModeState.Waiting)
            {
                mode.Start();
            }

            if (mode.State == GameModeState.Playing)
            {
                mode.Tick(deltaSeconds);
                KeepSpeed();
                SendScores();
            }

            if (mode.State != GameModeState.Ended)
            {
                return false;
            }

            RecordEnd();
            return true;
        }

        // 試合の合間にチームを移った人とマップが替わったときにも呼ばれるので、もう居る人は数え直さない
        public void PlayerJoined(int playerId)
        {
            var session = _host.Session;
            var mode = _juggernaut.Session;
            if (session == null || !session.HasPlayer(playerId) || mode.HasPlayer(playerId))
            {
                return;
            }

            // 陣営の Id をプレイヤーの Id にするのは、抜けて同じ Id で戻った人に陣営に残った点を返すため
            var side = new Side(playerId);
            if (_left.TryGetValue(playerId, out var record))
            {
                _left.Remove(playerId);
                mode.AddPlayer(playerId, side, record.Kills, record.Deaths);
                return;
            }

            mode.AddPlayer(playerId, side);
        }

        public void PlayerLeft(int playerId)
        {
            _botGoals.Remove(playerId);
            var mode = _juggernaut.Session;
            if (!mode.HasPlayer(playerId))
            {
                return;
            }

            // Session は抜けた人の上限を戻し、体は抜けると捨てられるので、戻さずに忘れる
            if (_bodyHolder == playerId)
            {
                _bodyHolder = null;
            }

            _left[playerId] = new LeftRecord(mode.GetKills(playerId), mode.GetDeaths(playerId));
            _juggernaut.RemovePlayer(playerId);
            SendScores();
        }

        // キルを先に Juggernaut の Session へ渡すのは、点を倒した時点のジャガーノートで数えてから替えるため (Core の Juggernaut の順)。
        // 自滅と、試合に居ない人に倒されたジャガーノートは引き継がず選び直す
        public void PlayerKilled(int killerId, int victimId)
        {
            var mode = _juggernaut.Session;
            if (!mode.HasPlayer(victimId))
            {
                return;
            }

            var byOther = killerId != victimId && mode.HasPlayer(killerId);
            if (byOther)
            {
                mode.ReportKill(killerId, victimId);
            }
            else
            {
                mode.ReportDeath(victimId);
            }

            _juggernaut.ReportFall(victimId, byOther ? killerId : (int?)null);
            SendScores();
        }

        // Session は Restart で体力の上限を戻した後にここを呼ぶ。Juggernaut.Reset はジャガーノートを替わったと知らせずに外すので、体はここで戻す
        public void MatchRestarted()
        {
            RestoreBody();
            _juggernaut.Reset();
            _botGoals.Clear();
            _left.Clear();
            _ended = false;
            _winnerId = null;
            _sentLeadingScore = 0;
            _sentJuggernautScore = 0;
            _host.SetMatchScores(0, 0);
            _host.Log.WriteLine(JuggernautRulesLog.Restart());
        }

        // Session が先に終わったときも、Juggernaut をそのときの先頭で終えて、残る点と勝者をそろえる
        public void MatchEnded()
        {
            var mode = _juggernaut.Session;
            if (mode.State == GameModeState.Playing)
            {
                mode.Finish(mode.LeadingSide);
            }

            RecordEnd();
        }

        public RoundPhase? CurrentRoundPhase() => null;

        // 勝つのはチームではなく 1 人なので、チームの勝者は出さない。勝ったかは IsWinningPlayer で人で決める
        public Team? DecideWinner(Team? killWinner) => null;

        public bool IsWinningPlayer(int playerId, Team playerTeam, Team? winner)
        {
            return WinnerId == playerId;
        }

        // ジャガーノートを含めて全員が互いの敵。Session が試合のチームで味方を数えないよう、チームを見ない
        public bool AreHostile(int a, Team aTeam, int b, Team bTeam)
        {
            return a != b;
        }

        // ジャガーノートでない Bot をジャガーノートへ向かわせる。毎 tick 今の場所を返すと Bot の路を毎回作り直すので、
        // ジャガーノートが替わるか、持っている先から Bot までより遠くへ動くまでは同じ先を返す
        public Vector3? BotGoal(int playerId)
        {
            var juggernautId = _juggernaut.JuggernautId;
            var bodies = _host.Bodies;
            if (!_started
                || _juggernaut.Session.State != GameModeState.Playing
                || !juggernautId.HasValue
                || juggernautId.Value == playerId
                || !IsStanding(juggernautId.Value)
                || bodies == null
                || !bodies.TryGet(juggernautId.Value, out var target)
                || !bodies.TryGet(playerId, out var self))
            {
                _botGoals.Remove(playerId);
                return null;
            }

            if (_botGoals.TryGetValue(playerId, out var held)
                && held.JuggernautId == juggernautId.Value
                && Vector3.Distance(held.Position, target.Position) <= Vector3.Distance(held.Position, self.Position))
            {
                return held.Position;
            }

            var goal = target.Position;
            _botGoals[playerId] = new HeldGoal(juggernautId.Value, goal);
            return goal;
        }

        // AlphaPoints に点の上限を、AlphaScore・BravoScore にいちばん高い点とジャガーノートの点を、ObjectiveRemainingSeconds に選ぶまでの秒を載せる。
        // 選ぶ前の残りは 1 秒以上で送り、0 を「選ぶ時が過ぎた」の印にする (InfectedRules の感染が始まるまでの秒と同じ)
        public void FillMatch(ref MatchSnapshot snapshot)
        {
            snapshot.Objective = Objective;
            snapshot.AlphaPoints = _juggernaut.Spec.ScoreLimit;
            snapshot.BravoPoints = 0;
            snapshot.AlphaScore = LeadingScore();
            snapshot.BravoScore = JuggernautScore();
            var untilChosen = _juggernaut.SecondsUntilChosen;
            snapshot.ObjectiveRemainingSeconds = _juggernaut.HasJuggernaut || !(untilChosen > 0f)
                ? 0
                : Math.Max(1, HudFormat.DisplayedSeconds(untilChosen));
        }

        public void FillControlPoints(SnapshotList<ControlPointSnapshot> points)
        {
            ArgumentNullException.ThrowIfNull(points);
            points.Clear();
        }

        // 持ち主がジャガーノート。立っている間は本人が運び (Carried)、倒れている間は体の場所に落ちる (Dropped)
        public void FillObjectiveItems(SnapshotList<ObjectiveItemSnapshot> items)
        {
            ArgumentNullException.ThrowIfNull(items);
            items.Clear();
            var juggernautId = _juggernaut.JuggernautId;
            if (!juggernautId.HasValue)
            {
                return;
            }

            var id = juggernautId.Value;
            var standing = IsStanding(id);
            var session = _host.Session;
            ref var item = ref items.Add();
            item.ItemId = ItemId;
            item.Present = ObjectiveItemFields.All;
            item.Kind = ObjectiveItemKind.Juggernaut;
            item.Team = session != null && session.HasPlayer(id) ? session.GetTeam(id) : Team.Alpha;
            item.HasOwner = true;
            item.OwnerId = id;
            item.Position = _host.Bodies != null && _host.Bodies.TryGet(id, out var body) ? body.Position : Vector3.Zero;
            item.State = standing ? ObjectiveItemState.Carried : ObjectiveItemState.Dropped;
            item.HasCarrier = standing;
            item.CarrierId = standing ? id : 0;
            item.Opened = false;
            item.Radius = 0f;
        }

        // Session は同じチームの相手に倒されたときを味方撃ちとして数えるので、キル数と倒れた数は Juggernaut の数で書き直す
        public void FillPlayer(int playerId, ref PlayerSnapshot snapshot)
        {
            var mode = _juggernaut.Session;
            if (!mode.HasPlayer(playerId))
            {
                return;
            }

            snapshot.Kills = mode.GetKills(playerId);
            snapshot.Deaths = mode.GetDeaths(playerId);
        }

        // 目的に入っていない人 (入る前・抜けた後) は 0 点
        public int GetScore(int playerId)
        {
            return _juggernaut.Session.HasPlayer(playerId) ? _juggernaut.GetScore(playerId) : 0;
        }

        private void HandleChanged(JuggernautChange change)
        {
            _host.Log.WriteLine(JuggernautRulesLog.Changed(change));
            if (_bodyHolder.HasValue && _bodyHolder.Value != change.JuggernautId)
            {
                RestoreBody();
            }

            GiveBody(change.JuggernautId);
        }

        // 立っている人は新しい上限まで満たし、満タンのジャガーノートとして動き出させる。倒れている人には Heal が効かず、出直すときに満タンになる
        private void GiveBody(int playerId)
        {
            var spec = _juggernaut.Spec;
            var session = _host.Session;
            if (session != null && session.HasPlayer(playerId))
            {
                session.SetMaxHealth(playerId, spec.JuggernautMaxHealth);
                session.Heal(playerId, spec.JuggernautMaxHealth);
            }

            var bodies = _host.Bodies;
            if (bodies != null && bodies.TryGet(playerId, out var body))
            {
                body.MoveSpeedMultiplier = spec.JuggernautSpeedMultiplier;
            }

            _bodyHolder = playerId;
            _host.Log.WriteLine(JuggernautRulesLog.Body(playerId, spec));
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

            var bodies = _host.Bodies;
            if (bodies != null && bodies.TryGet(playerId, out var body))
            {
                body.MoveSpeedMultiplier = 1f;
            }

            _host.Log.WriteLine(JuggernautRulesLog.Restored(playerId));
        }

        // 選んだ時に体がまだ無かった人 (入った直後など) にも、体ができた tick で速さを書く
        private void KeepSpeed()
        {
            var bodies = _host.Bodies;
            if (!_bodyHolder.HasValue || bodies == null || !bodies.TryGet(_bodyHolder.Value, out var body))
            {
                return;
            }

            var speed = _juggernaut.Spec.JuggernautSpeedMultiplier;
            if (body.MoveSpeedMultiplier != speed)
            {
                body.MoveSpeedMultiplier = speed;
            }
        }

        // Session の無いとき (目的だけを回すとき) は、Juggernaut が倒れていると数えているかで決める
        private bool IsStanding(int playerId)
        {
            var session = _host.Session;
            if (session != null && session.HasPlayer(playerId))
            {
                return session.IsAlive(playerId);
            }

            var mode = _juggernaut.Session;
            return mode.HasPlayer(playerId) && !mode.IsDown(playerId);
        }

        // 終わった後は決めた勝者を使い続ける。Juggernaut.WinnerId は今いる人から探すので、終わった後に抜けた人で勝者が変わらないように
        private void RecordEnd()
        {
            if (_ended)
            {
                return;
            }

            _ended = true;
            _winnerId = _juggernaut.WinnerId;
            SendScores();
            _host.Log.WriteLine(JuggernautRulesLog.Winner(_winnerId, _winnerId.HasValue ? _juggernaut.GetScore(_winnerId.Value) : 0));
        }

        private int LeadingScore()
        {
            var best = 0;
            foreach (var playerId in _juggernaut.Session.GetPlayerIds())
            {
                best = Math.Max(best, _juggernaut.GetScore(playerId));
            }

            return best;
        }

        private int JuggernautScore()
        {
            var juggernautId = _juggernaut.JuggernautId;
            return juggernautId.HasValue ? GetScore(juggernautId.Value) : 0;
        }

        private void SendScores()
        {
            var leading = LeadingScore();
            var juggernaut = JuggernautScore();
            if (leading == _sentLeadingScore && juggernaut == _sentJuggernautScore)
            {
                return;
            }

            _sentLeadingScore = leading;
            _sentJuggernautScore = juggernaut;
            _host.SetMatchScores(leading, juggernaut);
            _host.Log.WriteLine(JuggernautRulesLog.Score(leading, juggernaut));
        }

        private readonly struct LeftRecord
        {
            public LeftRecord(int kills, int deaths)
            {
                Kills = kills;
                Deaths = deaths;
            }

            public int Kills { get; }

            public int Deaths { get; }
        }

        private readonly struct HeldGoal
        {
            public HeldGoal(int juggernautId, Vector3 position)
            {
                JuggernautId = juggernautId;
                Position = position;
            }

            public int JuggernautId { get; }

            public Vector3 Position { get; }
        }
    }
}

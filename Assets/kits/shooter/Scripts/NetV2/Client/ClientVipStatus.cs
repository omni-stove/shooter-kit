using System;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // Detail に専用の型を作らず Core の VipStatus を置くのは、オフラインの Vip.Status と同じ型にして、UI が VipStatus に登録した書式役をそのまま使えるようにするため。
    // サーバーは VIP を場の物 (ObjectiveItemKind.Vip) で送る。持ち主が VIP で、チームが守る側、運んでいる (Carried) 間は立っていて、落ちている (Dropped) なら倒れた。
    // 脱出地点は場の物 (ObjectiveItemKind.ExtractionPoint) で、チームは守る側にする。VIP が選ばれる前も守る側を出せるよう、VIP の物が無ければ脱出地点から読む。
    // 脱出の進み (0〜1) は Search and Destroy の設置と同じ ObjectiveProgress の部分の PlantFraction に載せ、各チームの取ったラウンドの数は AlphaPoints・BravoPoints に載せる
    public sealed class ClientVipStatus
    {
        // オフラインの Vip は TwoTeams の先の陣営から攻めるので、守る側がまだ届いていない間はその後ろの陣営にする
        private static readonly Side _firstDefender = Side.Bravo;

        private readonly int _localPlayerId;
        private Side _defender = _firstDefender;
        private Shown _shown;

        public ClientVipStatus(int localPlayerId)
        {
            _localPlayerId = localPlayerId;
        }

        // Vip の試合のスナップショットが届いていなければ null
        public ModeStatus Status { get; private set; }

        public VipStatus Vip => Status?.Detail as VipStatus;

        // まだ選ばれていないか、Vip の試合でなければ null。倒れている間も VIP のまま
        public int? VipId => Status == null ? null : _shown.VipId;

        // Vip の試合でなければ Waiting
        public RoundPhase Phase => Status == null ? RoundPhase.Waiting : _shown.Phase;

        public bool IsStandingVip(int playerId)
        {
            return Status != null && _shown.VipId == playerId && !_shown.VipFallen;
        }

        // Status を作り直したら (null に戻したときも) true。届いた値が前と同じなら、UI が持っている Status を差し替えない
        public bool Apply(SnapshotMessage snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            ref var match = ref snapshot.Match;
            if (match.Objective != MatchObjective.Vip)
            {
                return Clear();
            }

            ReadItems(snapshot, out var vipId, out var fallen, out var defenderTeam);
            if (defenderTeam.HasValue)
            {
                _defender = Side.FromTeam(defenderTeam.Value);
            }

            Team? ownTeam = null;
            var own = snapshot.Players.IndexOf(_localPlayerId);
            if (own >= 0)
            {
                ownTeam = snapshot.Players[own].Team;
            }

            var phase = match.HasRound ? match.Round : RoundPhase.Waiting;
            var ended = match.State == GameModeState.Ended || phase == RoundPhase.Ended;

            // 倒れた VIP や、ラウンドの外の進みは HUD に出さない (オフラインも倒れた・終わった時点で進みを 0 に戻す)
            var exitFraction = vipId.HasValue && !fallen && phase == RoundPhase.Live && !ended ? Fraction(match.PlantFraction) : 0f;
            var shown = new Shown(
                Math.Max(0, match.RemainingSeconds),
                phase,
                vipId,
                fallen,
                exitFraction,
                _defender,
                Math.Max(0, match.AlphaPoints),
                Math.Max(0, match.BravoPoints),
                ownTeam,
                ended,
                ended && match.HasWinner ? match.Winner : (Team?)null);
            if (Status != null && shown.Equals(_shown))
            {
                return false;
            }

            _shown = shown;
            bool? localVip = null;
            if (ownTeam.HasValue)
            {
                localVip = vipId == _localPlayerId;
            }

            var detail = new VipStatus(vipId, exitFraction, _defender, localVip);

            // ModeStatus は出した後も UI が持つので、得点の並びは使い回さずに毎回作る
            var sideScores = new[]
            {
                new SideScore(Side.Alpha, shown.AlphaWins),
                new SideScore(Side.Bravo, shown.BravoWins),
            };
            Status = new ModeStatus(
                shown.RemainingSeconds,
                sideScores,
                ownTeam.HasValue ? Side.FromTeam(ownTeam.Value) : (Side?)null,
                null,
                ended,
                shown.Winner.HasValue ? Side.FromTeam(shown.Winner.Value) : (Side?)null,
                detail);
            return true;
        }

        // 試合を離れたときや、Vip でない試合に移ったときに呼ぶ。消したら true
        public bool Clear()
        {
            _shown = default;
            _defender = _firstDefender;
            if (Status == null)
            {
                return false;
            }

            Status = null;
            return true;
        }

        // VIP の物が 2 つ届いたら (壊れた行) 先の 1 つを使う。守る側は VIP の物のチームを脱出地点のチームより先に見る
        private static void ReadItems(SnapshotMessage snapshot, out int? vipId, out bool fallen, out Team? defender)
        {
            vipId = null;
            fallen = false;
            defender = null;
            Team? exitTeam = null;
            var items = snapshot.ObjectiveItems;
            for (var i = 0; i < items.Count; i++)
            {
                ref var item = ref items[i];
                if (item.Kind == ObjectiveItemKind.Vip && item.HasOwner && !vipId.HasValue)
                {
                    vipId = item.OwnerId;
                    fallen = item.State != ObjectiveItemState.Carried;
                    defender = item.Team;
                }
                else if (item.Kind == ObjectiveItemKind.ExtractionPoint && !exitTeam.HasValue)
                {
                    exitTeam = item.Team;
                }
            }

            if (!defender.HasValue)
            {
                defender = exitTeam;
            }
        }

        // 読み手は範囲の外を断るが、作った写しにも 0〜1 の外を入れないよう VipStatus の断りの手前で収める
        private static float Fraction(float value)
        {
            if (!(value > 0f))
            {
                return 0f;
            }

            return Math.Min(1f, value);
        }

        // 進みは VIP が脱出地点にいる間ずっと毎 tick 変わるので、HUD に出る百分率が同じ間は同じとみなして ModeStatus を出し直さない
        private static int PercentOf(float fraction) => (int)Math.Floor(fraction * 100f);

        private readonly struct Shown : IEquatable<Shown>
        {
            public Shown(
                int remainingSeconds,
                RoundPhase phase,
                int? vipId,
                bool vipFallen,
                float exitFraction,
                Side defender,
                int alphaWins,
                int bravoWins,
                Team? ownTeam,
                bool ended,
                Team? winner)
            {
                RemainingSeconds = remainingSeconds;
                Phase = phase;
                VipId = vipId;
                VipFallen = vipFallen;
                ExitFraction = exitFraction;
                Defender = defender;
                AlphaWins = alphaWins;
                BravoWins = bravoWins;
                OwnTeam = ownTeam;
                Ended = ended;
                Winner = winner;
            }

            public int RemainingSeconds { get; }

            public RoundPhase Phase { get; }

            public int? VipId { get; }

            public bool VipFallen { get; }

            public float ExitFraction { get; }

            public Side Defender { get; }

            public int AlphaWins { get; }

            public int BravoWins { get; }

            public Team? OwnTeam { get; }

            public bool Ended { get; }

            public Team? Winner { get; }

            public bool Equals(Shown other)
            {
                return RemainingSeconds == other.RemainingSeconds
                    && Phase == other.Phase
                    && VipId == other.VipId
                    && VipFallen == other.VipFallen
                    && PercentOf(ExitFraction) == PercentOf(other.ExitFraction)
                    && Defender == other.Defender
                    && AlphaWins == other.AlphaWins
                    && BravoWins == other.BravoWins
                    && OwnTeam == other.OwnTeam
                    && Ended == other.Ended
                    && Winner == other.Winner;
            }

            public override bool Equals(object obj) => obj is Shown other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(RemainingSeconds, Phase, VipId, VipFallen, Defender, AlphaWins, BravoWins, Ended);
        }
    }
}

using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // Detail に専用の型を作らず Core の JuggernautStatus を置くのは、オフラインと同じ型にして、UI が JuggernautStatus に登録した書式役をそのまま使えるようにするため。
    // サーバーはジャガーノートを場の物 (ObjectiveItemKind.Juggernaut) で送る。持ち主がジャガーノートで、運んでいる (Carried) 間は立っていて、落ちている (Dropped) なら倒れた。
    // 選ばれるまでの秒は ObjectiveRemainingSeconds に載せ、選ばれた後は 0 を送る。
    // 陣営は FFA と同じくプレイヤー Id と同じ番号にする
    public sealed class ClientJuggernautStatus
    {
        private readonly int _localPlayerId;
        private readonly List<SideScore> _read = new List<SideScore>();
        private readonly HashSet<int> _seen = new HashSet<int>();
        private SideScore[] _scores = Array.Empty<SideScore>();
        private Shown _shown;

        public ClientJuggernautStatus(int localPlayerId)
        {
            _localPlayerId = localPlayerId;
        }

        // Juggernaut の試合のスナップショットが届いていなければ null
        public ModeStatus Status { get; private set; }

        public JuggernautStatus Juggernaut => Status?.Detail as JuggernautStatus;

        // まだ選ばれていないか、Juggernaut の試合でなければ null
        public int? JuggernautId => Status == null ? null : _shown.JuggernautId;

        public bool IsStandingJuggernaut(int playerId)
        {
            return Status != null && _shown.JuggernautId == playerId && !_shown.JuggernautFallen;
        }

        // Status を作り直したら (null に戻したときも) true。届いた値が前と同じなら、UI が持っている Status を差し替えない
        public bool Apply(SnapshotMessage snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            ref var match = ref snapshot.Match;
            if (match.Objective != MatchObjective.Juggernaut)
            {
                return Clear();
            }

            ReadJuggernaut(snapshot, out var juggernautId, out var fallen);
            var hasLocal = ReadScores(snapshot);
            var shown = new Shown(
                Math.Max(0, match.RemainingSeconds),
                juggernautId.HasValue ? 0 : Math.Max(0, match.ObjectiveRemainingSeconds),
                juggernautId,
                fallen,
                hasLocal,
                hasLocal ? RespawnSeconds(snapshot) : null,
                match.State == GameModeState.Ended);
            if (Status != null && shown.Equals(_shown) && SameScores())
            {
                return false;
            }

            _shown = shown;

            // ModeStatus は出した後も UI が持つので、得点の並びは使い回さずに作り直す
            _scores = _read.ToArray();
            bool? localJuggernaut = null;
            if (hasLocal)
            {
                localJuggernaut = juggernautId == _localPlayerId;
            }

            var detail = new JuggernautStatus(juggernautId, shown.SecondsUntilChosen, localJuggernaut);
            Status = new ModeStatus(
                shown.RemainingSeconds,
                _scores,
                hasLocal ? new Side(_localPlayerId) : (Side?)null,
                shown.RespawnSeconds,
                shown.Ended,
                shown.Ended ? SoleLeader(_scores) : null,
                detail);
            return true;
        }

        // 試合を離れたときや、Juggernaut でない試合に移ったときに呼ぶ。消したら true
        public bool Clear()
        {
            _shown = default;
            _scores = Array.Empty<SideScore>();
            if (Status == null)
            {
                return false;
            }

            Status = null;
            return true;
        }

        // 2 つ届いたら (壊れた行) 先の 1 つを使う
        private static void ReadJuggernaut(SnapshotMessage snapshot, out int? juggernautId, out bool fallen)
        {
            juggernautId = null;
            fallen = false;
            var items = snapshot.ObjectiveItems;
            for (var i = 0; i < items.Count; i++)
            {
                ref var item = ref items[i];
                if (item.Kind != ObjectiveItemKind.Juggernaut || !item.HasOwner)
                {
                    continue;
                }

                juggernautId = item.OwnerId;
                fallen = item.State != ObjectiveItemState.Carried;
                return;
            }
        }

        // 点はキルごとの重みがジャガーノートかどうかで変わり、キル数からは引けない。
        // PlayerFields に部分の空きが無く、Juggernaut の試合では Gun Game の段を使わないので、サーバーは各人の点を行の GunGameTier に載せる
        private bool ReadScores(SnapshotMessage snapshot)
        {
            _read.Clear();
            _seen.Clear();
            var hasLocal = false;
            var players = snapshot.Players;
            for (var i = 0; i < players.Count; i++)
            {
                ref var row = ref players[i];
                if (row.PlayerId < 0 || !_seen.Add(row.PlayerId))
                {
                    continue;
                }

                _read.Add(new SideScore(new Side(row.PlayerId), Math.Max(0, row.GunGameTier)));
                hasLocal |= row.PlayerId == _localPlayerId;
            }

            // オフラインの ModeSession.Sides と同じく陣営の番号の順に並べる
            _read.Sort((a, b) => a.Side.CompareTo(b.Side));
            return hasLocal;
        }

        private bool SameScores()
        {
            if (_read.Count != _scores.Length)
            {
                return false;
            }

            for (var i = 0; i < _scores.Length; i++)
            {
                if (_read[i].Side != _scores[i].Side || _read[i].Score != _scores[i].Score)
                {
                    return false;
                }
            }

            return true;
        }

        private static float? RespawnSeconds(SnapshotMessage snapshot)
        {
            if (!snapshot.HasOwner || !snapshot.Owner.RespawnSeconds.HasValue)
            {
                return null;
            }

            return Math.Max(0, snapshot.Owner.RespawnSeconds.Value);
        }

        // サーバーのチームの勝者の欄では 1 人を表せないので、点から決める。
        // 点の上限に届いて終わったときは届いた 1 人だけが最高点になり、時間切れで並んだらオフラインの TimeLimitWin と同じく引き分けにする
        private static Side? SoleLeader(SideScore[] scores)
        {
            Side? leader = null;
            var best = double.NegativeInfinity;
            var tied = false;
            foreach (var entry in scores)
            {
                if (entry.Score > best)
                {
                    best = entry.Score;
                    leader = entry.Side;
                    tied = false;
                }
                else if (entry.Score == best)
                {
                    tied = true;
                }
            }

            return tied ? null : leader;
        }

        private readonly struct Shown : IEquatable<Shown>
        {
            public Shown(
                int remainingSeconds,
                int secondsUntilChosen,
                int? juggernautId,
                bool juggernautFallen,
                bool hasLocal,
                float? respawnSeconds,
                bool ended)
            {
                RemainingSeconds = remainingSeconds;
                SecondsUntilChosen = secondsUntilChosen;
                JuggernautId = juggernautId;
                JuggernautFallen = juggernautFallen;
                HasLocal = hasLocal;
                RespawnSeconds = respawnSeconds;
                Ended = ended;
            }

            public int RemainingSeconds { get; }

            public int SecondsUntilChosen { get; }

            public int? JuggernautId { get; }

            public bool JuggernautFallen { get; }

            public bool HasLocal { get; }

            public float? RespawnSeconds { get; }

            public bool Ended { get; }

            public bool Equals(Shown other)
            {
                return RemainingSeconds == other.RemainingSeconds
                    && SecondsUntilChosen == other.SecondsUntilChosen
                    && JuggernautId == other.JuggernautId
                    && JuggernautFallen == other.JuggernautFallen
                    && HasLocal == other.HasLocal
                    && RespawnSeconds == other.RespawnSeconds
                    && Ended == other.Ended;
            }

            public override bool Equals(object obj) => obj is Shown other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(
                RemainingSeconds, SecondsUntilChosen, JuggernautId, JuggernautFallen, HasLocal, RespawnSeconds, Ended);
        }
    }
}

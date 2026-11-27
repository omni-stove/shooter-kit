using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    // 1 人ずつの陣営 (FreeForAll) に載せるのは、ModeSession の得点・得点上限・時間切れ・リスポーンをそのまま使うため。
    public sealed class Juggernaut
    {
        // 小刻みな Tick を足し合わせると選ぶ時刻にわずかに届かないことがあるので、この差までは届いたものとして扱う。
        private const double _snap = 1e-6;

        private const long _multiplier = 48271;
        private const long _modulus = 2147483647;

        public event Action<JuggernautChange> Changed;

        private readonly Func<int, int> _pickIndex;
        private long _randomState;
        private double _untilChosen;
        private bool _delayPassed;
        private int? _juggernautId;

        public JuggernautSpec Spec { get; }

        // 参加者の追加とキルの報告をラップせず ModeSession をそのまま見せるのは、シーンの Runner とサーバーが ModeSession に対して行う形に合わせるため。
        public ModeSession Session { get; }

        public int? JuggernautId => _juggernautId;

        public bool HasJuggernaut => _juggernautId.HasValue;

        public float SecondsUntilChosen => _delayPassed ? 0f : (float)Math.Max(0d, _untilChosen);

        public int? WinnerId
        {
            get
            {
                if (Session.State != GameModeState.Ended || !Session.Winner.HasValue)
                {
                    return null;
                }

                var winner = Session.Winner.Value;
                foreach (var playerId in Session.GetPlayerIds())
                {
                    if (Session.GetSide(playerId) == winner)
                    {
                        return playerId;
                    }
                }

                return null;
            }
        }

        // リスポーンの秒を JuggernautSpec に持たないのは、FreeForAll と同じく呼ぶ側の試合の設定 (オフラインの Runner・サーバーの NetMatchSettings) の値を使うため。
        // pickIndex (0 から count 未満の添字を返す) を渡せるのは、ジャガーノートの選び方をテストで決まったものにするため。
        public Juggernaut(JuggernautSpec spec, float respawnSeconds, Func<int, int> pickIndex = null)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _randomState = ((Environment.TickCount & int.MaxValue) % (_modulus - 1)) + 1;
            if (pickIndex == null)
            {
                pickIndex = NextIndex;
            }

            _pickIndex = pickIndex;
            _untilChosen = spec.StartDelaySeconds;

            Session = new ModeSession(
                new FreeForAll(),
                spec.TimeLimitSeconds,
                new IScoreRule[] { new Rules(this) },
                new IndividualRespawn(respawnSeconds),
                new IWinCondition[] { new ScoreLimitWin(spec.ScoreLimit), new TimeLimitWin() });
        }

        public bool IsJuggernaut(int playerId)
        {
            RequirePlayer(playerId, nameof(playerId));
            return _juggernautId == playerId;
        }

        public int GetScore(int playerId)
        {
            RequirePlayer(playerId, nameof(playerId));
            return HudFormat.DisplayedScore(Session.GetScore(Session.GetSide(playerId)));
        }

        // キルの報告と分けるのは、点を ModeSession へ報告した時点のジャガーノートで数えてから替えるため (Infected.Infect と同じ順)。
        public bool ReportFall(int playerId, int? killerId)
        {
            RequirePlayer(playerId, nameof(playerId));
            if (killerId.HasValue)
            {
                RequirePlayer(killerId.Value, nameof(killerId));
            }

            if (Session.State != GameModeState.Playing || _juggernautId != playerId)
            {
                return false;
            }

            if (killerId.HasValue && killerId.Value != playerId)
            {
                Change(killerId.Value, playerId, JuggernautChangeReason.TakenOver);
                return true;
            }

            var next = Pick(playerId);
            if (!next.HasValue)
            {
                return false;
            }

            Change(next.Value, playerId, JuggernautChangeReason.Repicked);
            return true;
        }

        // Session.RemovePlayer だけでも次の Tick で選び直すが、それまでジャガーノートのいない間を作らないためにここで選ぶ。
        public void RemovePlayer(int playerId)
        {
            RequirePlayer(playerId, nameof(playerId));
            Session.RemovePlayer(playerId);
            if (_juggernautId == playerId)
            {
                FillVacancy();
            }
        }

        public void Reset()
        {
            _delayPassed = false;
            _untilChosen = Spec.StartDelaySeconds;
            _juggernautId = null;
            Session.Reset();
        }

        private void Advance(float seconds)
        {
            if (!_delayPassed)
            {
                if (!(seconds > 0f))
                {
                    return;
                }

                _untilChosen -= seconds;
                if (_untilChosen > _snap)
                {
                    return;
                }

                _delayPassed = true;
                _untilChosen = 0d;
            }

            if (!_juggernautId.HasValue || !Session.HasPlayer(_juggernautId.Value))
            {
                FillVacancy();
            }
        }

        private void FillVacancy()
        {
            var previous = _juggernautId;
            _juggernautId = null;
            if (!_delayPassed || Session.State != GameModeState.Playing)
            {
                return;
            }

            var next = Pick(null);
            if (!next.HasValue)
            {
                return;
            }

            Change(next.Value, previous, previous.HasValue ? JuggernautChangeReason.Left : JuggernautChangeReason.Chosen);
        }

        // 倒れている人を後にするのは、選んだ人がリスポーンを待つ間ジャガーノートが場にいなくなるため。
        private int? Pick(int? excludedId)
        {
            var standing = new List<int>();
            var everyone = new List<int>();
            foreach (var playerId in Session.GetPlayerIds())
            {
                if (playerId == excludedId)
                {
                    continue;
                }

                everyone.Add(playerId);
                if (!Session.IsDown(playerId))
                {
                    standing.Add(playerId);
                }
            }

            var pool = standing.Count > 0 ? standing : everyone;
            if (pool.Count == 0)
            {
                return null;
            }

            var index = _pickIndex(pool.Count);
            if (index < 0 || index >= pool.Count)
            {
                throw new InvalidOperationException($"pickIndex returned {index} for {pool.Count} player(s); it must be from 0 to {pool.Count - 1}");
            }

            return pool[index];
        }

        private void Change(int juggernautId, int? previousId, JuggernautChangeReason reason)
        {
            _juggernautId = juggernautId;
            Changed?.Invoke(new JuggernautChange(juggernautId, previousId, reason));
        }

        // System.Random は実装 (Mono と .NET) で列が変わりうるので使わず、MapVote と同じ Lehmer の乗算合同法 (MINSTD) で選ぶ。
        private int NextIndex(int count)
        {
            _randomState = _randomState * _multiplier % _modulus;
            return (int)(_randomState % count);
        }

        private void RequirePlayer(int playerId, string parameterName)
        {
            if (!Session.HasPlayer(playerId))
            {
                throw new ArgumentOutOfRangeException(parameterName, playerId, "no player with this id");
            }
        }

        private sealed class Rules : IScoreRule
        {
            private readonly Juggernaut _owner;

            public Rules(Juggernaut owner)
            {
                _owner = owner;
            }

            public double PointsForKill(ModeSession session, int killerId, int victimId)
            {
                var juggernautId = _owner._juggernautId;
                if (!juggernautId.HasValue)
                {
                    return 0d;
                }

                if (killerId == juggernautId.Value)
                {
                    return _owner.Spec.ScorePerKillAsJuggernaut;
                }

                return victimId == juggernautId.Value ? _owner.Spec.ScorePerJuggernautKill : 0d;
            }

            public double PointsForAssist(ModeSession session, int assisterId, int victimId) => 0d;

            public double PointsPerSecond(ModeSession session, Side side) => 0d;

            public void Advance(ModeSession session, float seconds) => _owner.Advance(seconds);
        }
    }
}

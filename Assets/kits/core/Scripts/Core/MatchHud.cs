using System;

namespace GameKit.Core
{
    // どの項目も文字列にせず数や型の付いた値で持つのは、どう書くか (体力・残り時間の書式を含む) を HUD に任せるため。
    // 武器や弾のようなジャンルの項目をここに並べないのは、ジャンルに依らない HUD (体力・時間・通知・スコア・結果) をジャンルの型に縛らないため。ジャンルの HUD (Shooter.Core.HudModel など) はこれを持ち、自分の項目だけを足す。
    public sealed class MatchHud
    {
        private Health _health;
        private Match _match;
        private IHudNoticeSource _notice;

        public event Action<MatchHudField> Changed;

        public int? CurrentHealth { get; private set; }

        public int? MaxHealth { get; private set; }

        public bool IsHealthLow => CurrentHealth.HasValue && HudFormat.IsLowHealth(CurrentHealth.Value, MaxHealth.Value);

        public float? HealthFraction => CurrentHealth.HasValue ? Math.Min(1f, (float)CurrentHealth.Value / MaxHealth.Value) : (float?)null;

        public int? RemainingSeconds { get; private set; }

        public bool HasTime => RemainingSeconds.HasValue;

        public HudNotice Notice { get; private set; }

        public Team? FirstScoreTeam { get; private set; }

        public int? FirstScore { get; private set; }

        public Team? SecondScoreTeam => FirstScoreTeam.HasValue ? HudFormat.Opponent(FirstScoreTeam.Value) : (Team?)null;

        public int? SecondScore { get; private set; }

        public bool HasResult { get; private set; }

        public Team? Winner { get; private set; }

        public MatchOutcome? SoloResult { get; private set; }

        public string RestartKey { get; private set; } = string.Empty;

        public void BindHealth(Health health)
        {
            if (_health != null)
            {
                _health.Changed -= OnHealthChanged;
            }

            _health = health;
            if (_health != null)
            {
                _health.Changed += OnHealthChanged;
            }

            OnHealthChanged();
        }

        public void BindMatch(Match match)
        {
            if (_match != null)
            {
                _match.RemainingSecondsChanged -= SetTime;
                _match.StateChanged -= OnMatchStateChanged;
            }

            _match = match;
            if (_match != null)
            {
                _match.RemainingSecondsChanged += SetTime;
                _match.StateChanged += OnMatchStateChanged;
                if (_match.HasTimeLimit)
                {
                    SetTime(_match.RemainingSeconds);
                }
                else
                {
                    ClearTime();
                }
            }
            else
            {
                ClearTime();
            }

            ApplySoloResult(_match?.Result, RestartKey);
        }

        public void SetRestartKey(string key)
        {
            ApplySoloResult(SoloResult, key ?? string.Empty);
        }

        public void BindNotice(IHudNoticeSource notice)
        {
            // With no source bound before or after, the notice was set directly (a respawn countdown) and is not the source's to clear.
            if (notice == null && _notice == null)
            {
                return;
            }

            if (_notice != null)
            {
                _notice.Changed -= SetNotice;
            }

            _notice = notice;
            if (_notice != null)
            {
                _notice.Changed += SetNotice;
            }

            SetNotice(_notice != null ? _notice.Notice : HudNotice.None);
        }

        public void UnbindAll()
        {
            if (_health != null)
            {
                _health.Changed -= OnHealthChanged;
                _health = null;
            }

            if (_match != null)
            {
                _match.RemainingSecondsChanged -= SetTime;
                _match.StateChanged -= OnMatchStateChanged;
                _match = null;
            }

            if (_notice != null)
            {
                _notice.Changed -= SetNotice;
                _notice = null;
            }
        }

        public void SetHealth(int current, int max)
        {
            if (current < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(current), current, "current must not be negative");
            }

            if (max < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(max), max, "max must be at least 1");
            }

            ApplyHealth(current, max);
        }

        public void ClearHealth()
        {
            ApplyHealth(null, null);
        }

        public void SetTime(float remainingSeconds)
        {
            ApplyTime(HudFormat.DisplayedSeconds(remainingSeconds));
        }

        public void ClearTime()
        {
            ApplyTime(null);
        }

        public void SetNotice(HudNotice notice)
        {
            if (notice == Notice)
            {
                return;
            }

            Notice = notice;
            Changed?.Invoke(MatchHudField.Notice);
        }

        public void ClearNotice()
        {
            SetNotice(HudNotice.None);
        }

        public void SetRespawnNotice(RespawnMode mode, int seconds)
        {
            SetNotice(HudNotice.ForRespawn(mode, seconds));
        }

        public void SetScores(Team ownTeam, int alphaScore, int bravoScore)
        {
            var opponent = HudFormat.Opponent(ownTeam);
            if (alphaScore < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(alphaScore), alphaScore, "score must not be negative");
            }

            if (bravoScore < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bravoScore), bravoScore, "score must not be negative");
            }

            ApplyScores(ownTeam, ownTeam == Team.Alpha ? alphaScore : bravoScore, opponent == Team.Alpha ? alphaScore : bravoScore);
        }

        public void ClearScores()
        {
            ApplyScores(null, null, null);
        }

        public void SetResult(Team? winner)
        {
            ApplyResult(true, winner);
        }

        public void ClearResult()
        {
            ApplyResult(false, null);
        }

        private void OnHealthChanged()
        {
            if (_health != null)
            {
                SetHealth(_health.Current, _health.Max);
            }
            else
            {
                ClearHealth();
            }
        }

        private void ApplyHealth(int? current, int? max)
        {
            if (current == CurrentHealth && max == MaxHealth)
            {
                return;
            }

            CurrentHealth = current;
            MaxHealth = max;
            Changed?.Invoke(MatchHudField.Health);
        }

        private void ApplyTime(int? seconds)
        {
            if (seconds == RemainingSeconds)
            {
                return;
            }

            RemainingSeconds = seconds;
            Changed?.Invoke(MatchHudField.Time);
        }

        private void OnMatchStateChanged(GameModeState state)
        {
            ApplySoloResult(_match?.Result, RestartKey);
        }

        private void ApplySoloResult(MatchOutcome? result, string restartKey)
        {
            if (result == SoloResult && restartKey == RestartKey)
            {
                return;
            }

            SoloResult = result;
            RestartKey = restartKey;
            Changed?.Invoke(MatchHudField.Result);
        }

        private void ApplyScores(Team? firstTeam, int? first, int? second)
        {
            if (firstTeam == FirstScoreTeam && first == FirstScore && second == SecondScore)
            {
                return;
            }

            FirstScoreTeam = firstTeam;
            FirstScore = first;
            SecondScore = second;
            Changed?.Invoke(MatchHudField.Score);
        }

        private void ApplyResult(bool hasResult, Team? winner)
        {
            if (hasResult == HasResult && winner == Winner)
            {
                return;
            }

            HasResult = hasResult;
            Winner = winner;
            Changed?.Invoke(MatchHudField.Result);
        }
    }
}

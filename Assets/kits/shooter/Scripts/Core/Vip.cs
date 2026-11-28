using System;
using System.Collections.Generic;
using GameKit.Core;

namespace Shooter.Core
{
    // 倒れたことは ModeSession と同じく Tick の頭でまとめて調べる (同じ間の相打ちを報告の順によらず同時に扱うため)。
    public sealed class Vip
    {
        // 体力と位置は Core の外にあるので、受けた側がここで戻す。
        public event Action<int> RoundStarted;

        public event Action<int> VipChosen;

        public event Action<int> VipEscaped;

        public event Action<Side, VipRoundEnd> RoundEnded;

        public event Action SidesSwapped;

        public event Action<Side> Ended;

        private const long _multiplier = 48271;
        private const long _modulus = 2147483647;

        private readonly ModeSession _mode;
        private readonly RoundSession _rounds;
        private readonly Func<int, int> _pickIndex;

        // 脱出地点にいる参加者。VIP が替わっても続きを数えられるよう、VIP に限らず受け取っておく。
        private readonly HashSet<int> _atExit = new HashSet<int>();

        private long _randomState;

        // イベントのハンドラで Reset されたら、その後の処理を続けないために数える。
        private int _generation;
        private VipRoundEnd? _decidedReason;
        private Side? _decidedWinner;

        public VipSpec Spec { get; }

        public ModeSession Mode => _mode;

        public RoundSession Rounds => _rounds;

        public RoundPhase Phase => _rounds.Phase;

        public int RoundNumber => _rounds.RoundNumber;

        public int WinsToWin => Spec.WinsToWin;

        public Side Attacker => _rounds.Attacker;

        public Side Defender => _rounds.Defender;

        public Side? Winner => _rounds.Winner;

        public int? VipId { get; private set; }

        public float ExitProgress { get; private set; }

        public float RoundRemaining { get; private set; }

        public VipRoundEnd? LastRoundEnd { get; private set; }

        public Side? LastRoundWinner { get; private set; }

        // 留まる秒が 0 なら入った Tick で脱出するので、進みは途中の値を持たない。
        public float ExitFraction => Spec.ExitHoldSeconds > 0f ? Math.Min(1f, ExitProgress / Spec.ExitHoldSeconds) : 0f;

        // pickIndex (0 から count 未満の添字を返す) を渡せるのは、VIP の選び方をテストで決まったものにするため。候補は id の小さい順に並ぶ。
        public Vip(VipSpec spec, Func<int, int> pickIndex = null)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _randomState = ((Environment.TickCount & int.MaxValue) % (_modulus - 1)) + 1;
            _pickIndex = pickIndex ?? new Func<int, int>(NextIndex);

            // ラウンドの時間は脱出の進みと同じ間で比べる必要があり、ModeSession の時間切れでは比べられないので、このクラスで数える。
            _mode = new ModeSession(
                new TwoTeams(),
                float.PositiveInfinity,
                Array.Empty<IScoreRule>(),
                new NoRespawn(),
                new IWinCondition[] { new EscortWin(this) });

            // 用意の時間は VipSpec に無いので 0 にし、Start でそのままラウンドを始める。
            _rounds = new RoundSession(_mode, spec.WinsToWin, 0f, spec.SwapEveryRounds);
            _rounds.PreparationStarted += OnPreparationStarted;
            _rounds.RoundStarted += round => RoundStarted?.Invoke(round);
            _rounds.RoundEnded += OnRoundEnded;
            _rounds.SidesSwapped += () => SidesSwapped?.Invoke();
            _rounds.Ended += winner => Ended?.Invoke(winner);
            ResetRoundState();
        }

        public void Start() => _rounds.Start();

        public void Reset()
        {
            _generation++;
            _rounds.Reset();
            ResetRoundState();
            LastRoundEnd = null;
            LastRoundWinner = null;
        }

        public void AddPlayer(int playerId, Side side)
        {
            _mode.AddPlayer(playerId, side);
            if (!VipId.HasValue && side == Defender && CanChoose())
            {
                ChooseVip();
            }
        }

        public void RemovePlayer(int playerId)
        {
            // 倒れた VIP が次の Tick の前に抜けても、選び直して倒されたことを無かったことにしない。
            var vipWasDown = VipId == playerId && _mode.HasPlayer(playerId) && _mode.IsDown(playerId);
            _mode.RemovePlayer(playerId);
            _atExit.Remove(playerId);
            if (VipId == playerId)
            {
                VipId = null;
                ExitProgress = 0f;
                if (vipWasDown && Phase == RoundPhase.Live && _mode.State == GameModeState.Playing && !_decidedReason.HasValue)
                {
                    Decide(Attacker, VipRoundEnd.VipDown);
                }
                else if (CanChoose())
                {
                    ChooseVip();
                }
            }
        }

        public int GetRoundWins(Side side) => _rounds.GetRoundWins(side);

        public bool HasPlayer(int playerId) => _mode.HasPlayer(playerId);

        public bool IsVip(int playerId) => VipId == playerId;

        public bool IsDefender(int playerId) => _mode.GetSide(playerId) == Defender;

        public void ReportKill(int killerId, int victimId) => _rounds.ReportKill(killerId, victimId);

        public void ReportDeath(int playerId) => _rounds.ReportDeath(playerId);

        public void SetAtExit(int playerId, bool atExit)
        {
            _mode.GetSide(playerId);
            if (atExit)
            {
                _atExit.Add(playerId);
            }
            else
            {
                _atExit.Remove(playerId);
            }
        }

        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsPositiveInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be finite and not negative");
            }

            if (Phase == RoundPhase.Live && _mode.State == GameModeState.Playing && !_decidedReason.HasValue)
            {
                var generation = _generation;
                KeepVipOnDefender();
                if (generation != _generation)
                {
                    return;
                }

                CheckStanding();
            }

            if (Phase == RoundPhase.Live && _mode.State == GameModeState.Playing && !_decidedReason.HasValue)
            {
                var generation = _generation;
                AdvanceObjective(deltaSeconds);
                if (generation != _generation)
                {
                    return;
                }
            }

            _rounds.Tick(deltaSeconds);
        }

        public ModeStatus Status(int? localPlayerId)
        {
            Side? ownSide = null;
            if (localPlayerId.HasValue && _mode.HasPlayer(localPlayerId.Value))
            {
                ownSide = _mode.GetSide(localPlayerId.Value);
            }

            var scores = new[]
            {
                new SideScore(Side.Alpha, GetRoundWins(Side.Alpha)),
                new SideScore(Side.Bravo, GetRoundWins(Side.Bravo)),
            };
            return new ModeStatus(
                RoundRemaining,
                scores,
                ownSide,
                null,
                Phase == RoundPhase.Ended,
                Winner,
                VipStatus.Of(this, localPlayerId));
        }

        // 始まる前は選ばないのは、Start のラウンドの始まりで選び直して VipChosen を 2 度出さないため。
        private bool CanChoose()
        {
            return !_decidedReason.HasValue
                && (Phase == RoundPhase.Preparing || (Phase == RoundPhase.Live && _mode.State == GameModeState.Playing));
        }

        private void OnPreparationStarted(int round)
        {
            var generation = _generation;
            ResetRoundState();
            if (generation != _generation)
            {
                return;
            }

            ChooseVip();
        }

        private void OnRoundEnded(Side? winner)
        {
            var reason = _decidedReason ?? VipRoundEnd.Finished;
            LastRoundEnd = reason;
            LastRoundWinner = winner;

            // 決着はいつも攻守のどちらかの勝ちなので、外から引き分けで終えられたときだけ知らせない。
            if (winner.HasValue)
            {
                RoundEnded?.Invoke(winner.Value, reason);
            }
        }

        private void ResetRoundState()
        {
            _atExit.Clear();
            _decidedReason = null;
            _decidedWinner = null;
            VipId = null;
            ExitProgress = 0f;
            RoundRemaining = Spec.RoundSeconds;
        }

        // ラウンドの途中は立っている人から選ぶ (倒れている人を VIP にすると、選んだ途端に攻める側の勝ちになるため)。
        private void ChooseVip()
        {
            var candidates = new List<int>();
            foreach (var playerId in _mode.GetPlayerIds())
            {
                if (_mode.GetSide(playerId) == Defender && !(Phase == RoundPhase.Live && _mode.IsDown(playerId)))
                {
                    candidates.Add(playerId);
                }
            }

            ExitProgress = 0f;
            if (candidates.Count == 0)
            {
                VipId = null;
                return;
            }

            var index = _pickIndex(candidates.Count);
            if (index < 0 || index >= candidates.Count)
            {
                throw new InvalidOperationException($"pickIndex returned {index} for {candidates.Count} defender(s); it must be from 0 to {candidates.Count - 1}");
            }

            VipId = candidates[index];
            VipChosen?.Invoke(VipId.Value);
        }

        // 抜けたのが Mode の RemovePlayer からでも、守る側に入ったのが Mode の AddPlayer からでも、ここで VIP を選び直す。
        private void KeepVipOnDefender()
        {
            if (VipId.HasValue)
            {
                var vip = VipId.Value;
                if (_mode.HasPlayer(vip) && _mode.GetSide(vip) == Defender)
                {
                    return;
                }

                _atExit.Remove(vip);
                VipId = null;
                ChooseVip();
                return;
            }

            if (HasStandingDefender())
            {
                ChooseVip();
            }
        }

        private bool HasStandingDefender()
        {
            foreach (var playerId in _mode.GetPlayerIds())
            {
                if (_mode.GetSide(playerId) == Defender && !_mode.IsDown(playerId))
                {
                    return true;
                }
            }

            return false;
        }

        private void AdvanceObjective(float deltaSeconds)
        {
            if (VipAtExit())
            {
                var needed = Spec.ExitHoldSeconds - ExitProgress;
                if (needed <= deltaSeconds && needed <= RoundRemaining)
                {
                    RoundRemaining -= needed;
                    ExitProgress = Spec.ExitHoldSeconds;
                    Decide(Defender, VipRoundEnd.Escaped);
                    VipEscaped?.Invoke(VipId.Value);
                    return;
                }

                ExitProgress += Math.Min(deltaSeconds, RoundRemaining);
            }
            else
            {
                ExitProgress = 0f;
            }

            RoundRemaining -= deltaSeconds;
            if (RoundRemaining <= 0f)
            {
                RoundRemaining = 0f;
                ExitProgress = 0f;
                Decide(Attacker, VipRoundEnd.TimeUp);
            }
        }

        private bool VipAtExit()
        {
            return VipId.HasValue && _atExit.Contains(VipId.Value) && !_mode.IsDown(VipId.Value);
        }

        private void Decide(Side winner, VipRoundEnd reason)
        {
            _decidedWinner = winner;
            _decidedReason = reason;
        }

        private ModeOutcome CheckStanding()
        {
            if (_decidedReason.HasValue)
            {
                return ModeOutcome.Decided(_decidedWinner);
            }

            // VIP が倒れたことを攻める側の全滅より先に見るのは、相打ちでも VIP を倒した側の勝ちにするため。
            if (VipId.HasValue && _mode.IsDown(VipId.Value))
            {
                Decide(Attacker, VipRoundEnd.VipDown);
            }
            else if (_mode.IsSideEliminated(Defender))
            {
                Decide(Attacker, VipRoundEnd.DefendersEliminated);
            }
            else if (_mode.IsSideEliminated(Attacker))
            {
                Decide(Defender, VipRoundEnd.AttackersEliminated);
            }
            else
            {
                return ModeOutcome.Undecided;
            }

            return ModeOutcome.Decided(_decidedWinner);
        }

        // System.Random は実装 (Mono と .NET) で列が変わりうるので使わず、Infected と同じ Lehmer の乗算合同法 (MINSTD) で選ぶ。
        private int NextIndex(int count)
        {
            _randomState = _randomState * _multiplier % _modulus;
            return (int)(_randomState % count);
        }

        // 時間切れは Vip が数えるので、ここでは扱わない。
        private sealed class EscortWin : IWinCondition
        {
            private readonly Vip _owner;

            public EscortWin(Vip owner)
            {
                _owner = owner;
            }

            public double? ScoreLimit => null;

            public ModeOutcome CheckStanding(ModeSession session) => _owner.CheckStanding();

            public ModeOutcome CheckTimeUp(ModeSession session) => ModeOutcome.Undecided;
        }
    }
}

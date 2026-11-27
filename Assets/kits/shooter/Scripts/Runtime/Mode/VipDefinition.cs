using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "Vip", menuName = "Shooter/Modes/Vip")]
    public sealed class VipDefinition : ScriptableObject
    {
        [SerializeField, Min(1), Tooltip("VIP の最大体力。ラウンドが変わると元の上限に戻す")]
        private int _vipMaxHealth = VipSpec.DefaultVipMaxHealth;

        [SerializeField, Min(1f), Tooltip("1 ラウンドの制限時間 (秒)。切れたら攻める側の勝ち")]
        private float _roundSeconds = VipSpec.DefaultRoundSeconds;

        [SerializeField, Min(1), Tooltip("試合に勝つのに要るラウンドの勝ち数")]
        private int _winsToWin = VipSpec.DefaultWinsToWin;

        [SerializeField, Min(0f), Tooltip("VIP が脱出地点に留まり続けると脱出になる秒数。ラウンドの秒より短くする")]
        private float _exitHoldSeconds = VipSpec.DefaultExitHoldSeconds;

        [SerializeField, Min(0), Tooltip("何ラウンドごとに攻守を入れ替えるか。0 なら入れ替えない")]
        private int _swapEveryRounds = VipSpec.DefaultSwapEveryRounds;

        public int VipMaxHealth
        {
            get => _vipMaxHealth;
            set => _vipMaxHealth = value;
        }

        public float RoundSeconds
        {
            get => _roundSeconds;
            set => _roundSeconds = value;
        }

        public int WinsToWin
        {
            get => _winsToWin;
            set => _winsToWin = value;
        }

        public float ExitHoldSeconds
        {
            get => _exitHoldSeconds;
            set => _exitHoldSeconds = value;
        }

        public int SwapEveryRounds
        {
            get => _swapEveryRounds;
            set => _swapEveryRounds = value;
        }

        public VipSpec ToSpec()
        {
            try
            {
                return new VipSpec(_vipMaxHealth, _roundSeconds, _winsToWin, _exitHoldSeconds, _swapEveryRounds);
            }
            catch (ArgumentException e)
            {
                throw new InvalidOperationException($"vip definition '{SourceName}' is invalid: {e.Message}", e);
            }
        }

        public void CopyFrom(VipSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _vipMaxHealth = spec.VipMaxHealth;
            _roundSeconds = spec.RoundSeconds;
            _winsToWin = spec.WinsToWin;
            _exitHoldSeconds = spec.ExitHoldSeconds;
            _swapEveryRounds = spec.SwapEveryRounds;
        }

        private string SourceName => string.IsNullOrEmpty(name) ? "(unnamed vip definition)" : name;
    }
}

using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "Juggernaut", menuName = "Shooter/Modes/Juggernaut")]
    public sealed class JuggernautDefinition : ScriptableObject
    {
        [SerializeField, Min(1), Tooltip("ジャガーノートの最大体力")]
        private int _juggernautMaxHealth = JuggernautRunner.DefaultJuggernautMaxHealth;

        [SerializeField, Min(0.01f), Tooltip("ジャガーノート (bot) の移動の速さの倍率")]
        private float _juggernautSpeedMultiplier = JuggernautRunner.DefaultJuggernautSpeedMultiplier;

        [SerializeField, Min(0), Tooltip("ジャガーノートが 1 人倒したときに入る点")]
        private int _scorePerKillAsJuggernaut = JuggernautRunner.DefaultScorePerKillAsJuggernaut;

        [SerializeField, Min(0), Tooltip("ジャガーノートを倒したときに入る点")]
        private int _scorePerJuggernautKill = JuggernautRunner.DefaultScorePerJuggernautKill;

        [SerializeField, Min(1), Tooltip("だれかがこの点に届いたら試合が終わる")]
        private int _scoreLimit = JuggernautRunner.DefaultScoreLimit;

        [SerializeField, Min(1f), Tooltip("制限時間 (秒)。切れたときにいちばん点の高い人の勝ち")]
        private float _timeLimitSeconds = JuggernautRunner.DefaultTimeLimitSeconds;

        [SerializeField, Min(0f), Tooltip("最初のジャガーノートを選ぶまでの秒数")]
        private float _startDelaySeconds = JuggernautRunner.DefaultStartDelaySeconds;

        [SerializeField, Min(0f), Tooltip("倒れてから出現点に戻るまでの秒数")]
        private float _respawnSeconds = JuggernautRunner.DefaultRespawnSeconds;

        public int JuggernautMaxHealth
        {
            get => _juggernautMaxHealth;
            set => _juggernautMaxHealth = value;
        }

        public float JuggernautSpeedMultiplier
        {
            get => _juggernautSpeedMultiplier;
            set => _juggernautSpeedMultiplier = value;
        }

        public int ScorePerKillAsJuggernaut
        {
            get => _scorePerKillAsJuggernaut;
            set => _scorePerKillAsJuggernaut = value;
        }

        public int ScorePerJuggernautKill
        {
            get => _scorePerJuggernautKill;
            set => _scorePerJuggernautKill = value;
        }

        public int ScoreLimit
        {
            get => _scoreLimit;
            set => _scoreLimit = value;
        }

        public float TimeLimitSeconds
        {
            get => _timeLimitSeconds;
            set => _timeLimitSeconds = value;
        }

        public float StartDelaySeconds
        {
            get => _startDelaySeconds;
            set => _startDelaySeconds = value;
        }

        // JuggernautSpec に持たないのは、リスポーンの秒は FreeForAll と同じく試合の設定の値で、サーバーは NetMatchSettings から読むため。
        public float RespawnSeconds
        {
            get => _respawnSeconds;
            set => _respawnSeconds = value;
        }

        public JuggernautSpec ToSpec()
        {
            try
            {
                return new JuggernautSpec(
                    _juggernautMaxHealth,
                    _juggernautSpeedMultiplier,
                    _scorePerKillAsJuggernaut,
                    _scorePerJuggernautKill,
                    _scoreLimit,
                    _timeLimitSeconds,
                    _startDelaySeconds);
            }
            catch (ArgumentException e)
            {
                throw new InvalidOperationException($"juggernaut definition '{SourceName}' is invalid: {e.Message}", e);
            }
        }

        public void CopyFrom(JuggernautSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _juggernautMaxHealth = spec.JuggernautMaxHealth;
            _juggernautSpeedMultiplier = spec.JuggernautSpeedMultiplier;
            _scorePerKillAsJuggernaut = spec.ScorePerKillAsJuggernaut;
            _scorePerJuggernautKill = spec.ScorePerJuggernautKill;
            _scoreLimit = spec.ScoreLimit;
            _timeLimitSeconds = spec.TimeLimitSeconds;
            _startDelaySeconds = spec.StartDelaySeconds;
        }

        private string SourceName => string.IsNullOrEmpty(name) ? "(unnamed juggernaut definition)" : name;
    }
}

using System;
using System.Globalization;

namespace Shooter.Core
{
    // ジャガーノートの武器を持たないのは、ジャガーノートになっても持っている武器のままにするため (武器を配り替えない)。
    public sealed class JuggernautSpec
    {
        public const int DefaultJuggernautMaxHealth = 300;
        public const float DefaultJuggernautSpeedMultiplier = 1.1f;
        public const int DefaultScorePerKillAsJuggernaut = 1;
        public const int DefaultScorePerJuggernautKill = 3;
        public const int DefaultScoreLimit = 15;
        public const float DefaultTimeLimitSeconds = 600f;
        public const float DefaultStartDelaySeconds = 5f;

        public static JuggernautSpec Default { get; } = new JuggernautSpec();

        public JuggernautSpec(
            int juggernautMaxHealth = DefaultJuggernautMaxHealth,
            float juggernautSpeedMultiplier = DefaultJuggernautSpeedMultiplier,
            int scorePerKillAsJuggernaut = DefaultScorePerKillAsJuggernaut,
            int scorePerJuggernautKill = DefaultScorePerJuggernautKill,
            int scoreLimit = DefaultScoreLimit,
            float timeLimitSeconds = DefaultTimeLimitSeconds,
            float startDelaySeconds = DefaultStartDelaySeconds)
        {
            JuggernautMaxHealth = SpecGuard.AtLeast(juggernautMaxHealth, 1, nameof(juggernautMaxHealth));
            JuggernautSpeedMultiplier = SpecGuard.PositiveFinite(juggernautSpeedMultiplier, nameof(juggernautSpeedMultiplier));
            ScorePerKillAsJuggernaut = SpecGuard.NonNegative(scorePerKillAsJuggernaut, nameof(scorePerKillAsJuggernaut));
            ScorePerJuggernautKill = SpecGuard.NonNegative(scorePerJuggernautKill, nameof(scorePerJuggernautKill));

            // 点の入らない組は、時間切れまでだれも点を取れず、いちばん点の高い人を決められない。
            if (ScorePerKillAsJuggernaut == 0 && ScorePerJuggernautKill == 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(scorePerJuggernautKill),
                    scorePerJuggernautKill,
                    nameof(scorePerJuggernautKill) + " must be positive when scorePerKillAsJuggernaut is 0");
            }

            ScoreLimit = SpecGuard.AtLeast(scoreLimit, 1, nameof(scoreLimit));
            TimeLimitSeconds = SpecGuard.PositiveFinite(timeLimitSeconds, nameof(timeLimitSeconds));
            StartDelaySeconds = SpecGuard.NonNegativeFinite(startDelaySeconds, nameof(startDelaySeconds));

            // 最初のジャガーノートを選ぶ前に終わる試合は、だれも点を取れずに終わる。
            if (StartDelaySeconds >= TimeLimitSeconds)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(startDelaySeconds),
                    startDelaySeconds,
                    nameof(startDelaySeconds) + " must be shorter than timeLimitSeconds (" + timeLimitSeconds.ToString(CultureInfo.InvariantCulture) + ")");
            }
        }

        public int JuggernautMaxHealth { get; }

        public float JuggernautSpeedMultiplier { get; }

        public int ScorePerKillAsJuggernaut { get; }

        public int ScorePerJuggernautKill { get; }

        public int ScoreLimit { get; }

        public float TimeLimitSeconds { get; }

        public float StartDelaySeconds { get; }

        public bool SameAs(JuggernautSpec other)
        {
            return other != null
                && JuggernautMaxHealth == other.JuggernautMaxHealth
                && JuggernautSpeedMultiplier == other.JuggernautSpeedMultiplier
                && ScorePerKillAsJuggernaut == other.ScorePerKillAsJuggernaut
                && ScorePerJuggernautKill == other.ScorePerJuggernautKill
                && ScoreLimit == other.ScoreLimit
                && TimeLimitSeconds == other.TimeLimitSeconds
                && StartDelaySeconds == other.StartDelaySeconds;
        }
    }
}

using System.Collections.Generic;
using GameKit.Core;

namespace Shooter.Core
{
    public static class JuggernautJson
    {
        public const string ResourcePath = "Tuning/juggernaut";

        public const string JuggernautMaxHealthItem = "juggernautMaxHealth";
        public const string JuggernautSpeedMultiplierItem = "juggernautSpeedMultiplier";
        public const string ScorePerKillAsJuggernautItem = "scorePerKillAsJuggernaut";
        public const string ScorePerJuggernautKillItem = "scorePerJuggernautKill";
        public const string ScoreLimitItem = "scoreLimit";
        public const string TimeLimitSecondsItem = "timeLimitSeconds";
        public const string StartDelaySecondsItem = "startDelaySeconds";

        private const string _kind = "juggernaut JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[]
        {
            JuggernautMaxHealthItem,
            JuggernautSpeedMultiplierItem,
            ScorePerKillAsJuggernautItem,
            ScorePerJuggernautKillItem,
            ScoreLimitItem,
            TimeLimitSecondsItem,
            StartDelaySecondsItem,
        };

        public static JuggernautSpec Parse(string text, string sourceName)
        {
            return SpecJsonReader.Read(text, sourceName, _kind, ItemNames, fields =>
            {
                var juggernautMaxHealth = fields.ReadInt(JuggernautMaxHealthItem, JuggernautSpec.DefaultJuggernautMaxHealth);
                var juggernautSpeedMultiplier = fields.ReadFloat(JuggernautSpeedMultiplierItem, JuggernautSpec.DefaultJuggernautSpeedMultiplier);
                var scorePerKillAsJuggernaut = fields.ReadInt(ScorePerKillAsJuggernautItem, JuggernautSpec.DefaultScorePerKillAsJuggernaut);
                var scorePerJuggernautKill = fields.ReadInt(ScorePerJuggernautKillItem, JuggernautSpec.DefaultScorePerJuggernautKill);
                var scoreLimit = fields.ReadInt(ScoreLimitItem, JuggernautSpec.DefaultScoreLimit);
                var timeLimitSeconds = fields.ReadFloat(TimeLimitSecondsItem, JuggernautSpec.DefaultTimeLimitSeconds);
                var startDelaySeconds = fields.ReadFloat(StartDelaySecondsItem, JuggernautSpec.DefaultStartDelaySeconds);
                return SpecJsonReader.Build(
                    fields,
                    ItemNames,
                    () => new JuggernautSpec(
                        juggernautMaxHealth,
                        juggernautSpeedMultiplier,
                        scorePerKillAsJuggernaut,
                        scorePerJuggernautKill,
                        scoreLimit,
                        timeLimitSeconds,
                        startDelaySeconds));
            });
        }
    }
}

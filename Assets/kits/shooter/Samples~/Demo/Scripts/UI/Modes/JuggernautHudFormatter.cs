using System;
using System.Collections.Generic;
using GameKit.Core;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class JuggernautHudFormatter : IModeHudFormatter
    {
        public static LocalizedText Waiting => new LocalizedText("mode.juggernaut.waiting");

        public static LocalizedText YouAreJuggernaut => new LocalizedText("mode.juggernaut.youAreJuggernaut");

        public static LocalizedText HuntTheJuggernaut => new LocalizedText("mode.juggernaut.hunt");

        public static LocalizedText ChosenIn(int seconds)
        {
            return new LocalizedText("mode.juggernaut.chosenIn", seconds);
        }

        public static LocalizedText Current(LocalizedText name)
        {
            return new LocalizedText("mode.juggernaut.current", name);
        }

        public static LocalizedText Score(int score)
        {
            return new LocalizedText("mode.juggernaut.yourScore", score);
        }

        // JuggernautStatus は参加者の番号だけを持つので、キルフィードやスコアボードと同じ名前になるよう GameSignals.Scoreboard から引く。
        public static LocalizedText PlayerName(int playerId, IReadOnlyList<ScoreboardEntry> entries)
        {
            if (entries != null)
            {
                for (var i = 0; i < entries.Count; i++)
                {
                    if (entries[i].PlayerId != playerId)
                    {
                        continue;
                    }

                    var name = ScoreboardText.Name(entries[i].Name);
                    if (!name.IsEmpty)
                    {
                        return name;
                    }

                    break;
                }
            }

            return ScoreboardText.PlayerName(playerId);
        }

        public static IReadOnlyList<LocalizedText> HudLines(JuggernautStatus status, IReadOnlyList<ScoreboardEntry> entries)
        {
            if (status == null)
            {
                throw new ArgumentNullException(nameof(status));
            }

            var lines = new List<LocalizedText>();
            if (!status.JuggernautId.HasValue)
            {
                // 選ぶ時刻を過ぎても参加者がいなければ秒は 0 のままなので、0 秒の残りを出さずに待っている旨を出す。
                lines.Add(status.SecondsUntilChosen > 0f ? ChosenIn(HudFormat.DisplayedSeconds(status.SecondsUntilChosen)) : Waiting);
                return lines;
            }

            if (status.IsLocalPlayerJuggernaut == true)
            {
                lines.Add(YouAreJuggernaut);
                return lines;
            }

            lines.Add(Current(PlayerName(status.JuggernautId.Value, entries)));
            if (status.IsLocalPlayerJuggernaut == false)
            {
                lines.Add(HuntTheJuggernaut);
            }

            return lines;
        }

        public static int? OwnScore(ModeStatus status)
        {
            if (status == null)
            {
                throw new ArgumentNullException(nameof(status));
            }

            if (!status.OwnSide.HasValue)
            {
                return null;
            }

            foreach (var score in status.SideScores)
            {
                if (score.Side == status.OwnSide.Value)
                {
                    return HudFormat.DisplayedScore(score.Score);
                }
            }

            return null;
        }

        public LocalizedText Time(ModeStatus status) => ModeStatusFormat.Remaining(status);

        public IReadOnlyList<ModeHudScoreLine> Scores(ModeStatus status) => ModeStatusFormat.Scores(status);

        public void AppendLines(ModeStatus status, List<LocalizedText> lines)
        {
            ModeStatusFormat.AppendRespawn(status, lines);
            if (!(status.Detail is JuggernautStatus detail))
            {
                return;
            }

            lines.AddRange(HudLines(detail, GameSignals.Scoreboard.Value));
            var own = OwnScore(status);
            if (own.HasValue)
            {
                lines.Add(Score(own.Value));
            }
        }

        public LocalizedText Result(ModeStatus status) => ModeStatusFormat.Result(status);

        // Edit Mode (テストやシーン生成) では Play の初期化が走らないので、エディターの読み込みでも登録する。
        // SubsystemRegistration は BeforeSceneLoad より先に走るので、ゲームが BeforeSceneLoad で登録した書式役がこれを置き換えられる。
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            ModeHudFormatters.Register<JuggernautStatus>(new JuggernautHudFormatter());
        }
    }
}

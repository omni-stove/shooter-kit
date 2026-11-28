using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GameKit.Core
{
    // サーバーとクライアントで同じ書式にし、2 つのログを突き合わせてスコアが合うか確かめられるようにする。
    public static class MatchLog
    {
        public const string Tag = "[Match]";

        public const string NoTimeLimit = "none";

        public const string Draw = "draw";

        public static string Score(int alphaScore, int bravoScore)
        {
            return Tag + " score A=" + alphaScore.ToString(CultureInfo.InvariantCulture)
                + " B=" + bravoScore.ToString(CultureInfo.InvariantCulture);
        }

        public static string Ended(Team? winner)
        {
            return Tag + " ended winner=" + (winner.HasValue ? TeamName(winner.Value) : Draw);
        }

        public static string Started(int playerCount)
        {
            return Tag + " started players=" + playerCount.ToString(CultureInfo.InvariantCulture);
        }

        public static string Joined(int playerId, Team team)
        {
            return Tag + " joined player=" + playerId.ToString(CultureInfo.InvariantCulture) + " team=" + TeamName(team);
        }

        public static string Left(int playerId)
        {
            return Tag + " left player=" + playerId.ToString(CultureInfo.InvariantCulture);
        }

        public static string TeamBalanced(int playerId, Team from, Team to)
        {
            return Tag + " balanced player=" + playerId.ToString(CultureInfo.InvariantCulture) + " from=" + TeamName(from) + " to=" + TeamName(to);
        }

        public static string Held(int playerId, float seconds)
        {
            return Tag + " holding player=" + playerId.ToString(CultureInfo.InvariantCulture)
                + " for=" + seconds.ToString("0.##", CultureInfo.InvariantCulture) + "s";
        }

        public static string HoldExpired(int playerId)
        {
            return Tag + " hold expired player=" + playerId.ToString(CultureInfo.InvariantCulture);
        }

        public static string Rejoined(int playerId, HeldPlayer held)
        {
            return Tag + " rejoined player=" + playerId.ToString(CultureInfo.InvariantCulture)
                + " was=" + held.PlayerId.ToString(CultureInfo.InvariantCulture)
                + " team=" + TeamName(held.Team)
                + " kills=" + held.Kills.ToString(CultureInfo.InvariantCulture)
                + " deaths=" + held.Deaths.ToString(CultureInfo.InvariantCulture);
        }

        public static string Synced(int alphaScore, int bravoScore, int remainingSeconds, GameModeState state)
        {
            return Tag + " synced A=" + alphaScore.ToString(CultureInfo.InvariantCulture)
                + " B=" + bravoScore.ToString(CultureInfo.InvariantCulture)
                + " remaining=" + remainingSeconds.ToString(CultureInfo.InvariantCulture) + "s"
                + " state=" + state;
        }

        public static string Kill(int killerId, int victimId)
        {
            return Tag + " kill killer=" + killerId.ToString(CultureInfo.InvariantCulture)
                + " victim=" + victimId.ToString(CultureInfo.InvariantCulture);
        }

        public static string Kill(int killerId, int victimId, IReadOnlyList<int> assisterIds)
        {
            var text = Kill(killerId, victimId);
            if (assisterIds == null || assisterIds.Count == 0)
            {
                return text;
            }

            var builder = new StringBuilder(text).Append(" assists=");
            for (var i = 0; i < assisterIds.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                builder.Append(assisterIds[i].ToString(CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        // 点数の入る Kill と区別できるよう、味方に倒されたことは別の語で書く。
        public static string TeamKill(int killerId, int victimId)
        {
            return Tag + " teamkill killer=" + killerId.ToString(CultureInfo.InvariantCulture)
                + " victim=" + victimId.ToString(CultureInfo.InvariantCulture);
        }

        public static string Spawn(int playerId, SpawnPoint point)
        {
            if (point == null)
            {
                throw new ArgumentNullException(nameof(point));
            }

            return SpawnAt(playerId, point.Position);
        }

        // 倒されて戻るときだけ、ルールの RespawnAnchor のどれを使ったかを足す (オフラインの ModeLog.Spawn と同じ from=)。
        public static string Spawn(int playerId, System.Numerics.Vector3 position, RespawnAnchor anchor)
        {
            if (!Enum.IsDefined(typeof(RespawnAnchor), anchor))
            {
                throw new ArgumentOutOfRangeException(nameof(anchor), anchor, "anchor is not a defined RespawnAnchor");
            }

            return SpawnAt(playerId, position) + " from=" + anchor;
        }

        private static string SpawnAt(int playerId, System.Numerics.Vector3 p)
        {
            return Tag + " spawn player=" + playerId.ToString(CultureInfo.InvariantCulture)
                + " at=(" + Clean(p.X).ToString("F2", CultureInfo.InvariantCulture)
                + ", " + Clean(p.Y).ToString("F2", CultureInfo.InvariantCulture)
                + ", " + Clean(p.Z).ToString("F2", CultureInfo.InvariantCulture) + ")";
        }

        // 向きの計算で出る -0 や極小の負の値が "-0.00" と出ないよう、桁を丸めてから 0 を足して符号を消す。
        private static float Clean(float v)
        {
            return MathF.Round(v * 100f) / 100f + 0f;
        }

        public static string SoloState(Match match)
        {
            if (match == null)
            {
                throw new ArgumentNullException(nameof(match));
            }

            return Tag + " state=" + SoloStateName(match)
                + " kills=" + match.Kills.ToString(CultureInfo.InvariantCulture)
                + " remaining=" + (match.HasTimeLimit ? match.RemainingSeconds.ToString("F1", CultureInfo.InvariantCulture) : NoTimeLimit);
        }

        public static string Restart()
        {
            return Tag + " restart";
        }

        public static string RoundPhaseChanged(RoundPhase? phase)
        {
            return Tag + " round phase=" + (phase.HasValue ? phase.Value.ToString() : "none");
        }

        public static string Elimination(int winsToWin, float preparationSeconds, float roundTimeLimitSeconds)
        {
            return Tag + " elimination winsToWin=" + winsToWin.ToString(CultureInfo.InvariantCulture)
                + " preparation=" + preparationSeconds.ToString(CultureInfo.InvariantCulture)
                + "s roundTimeLimit=" + roundTimeLimitSeconds.ToString(CultureInfo.InvariantCulture) + "s";
        }

        public static string RoundPreparing(int round)
        {
            return Tag + " round " + round.ToString(CultureInfo.InvariantCulture) + " preparing";
        }

        public static string RoundLive(int round)
        {
            return Tag + " round " + round.ToString(CultureInfo.InvariantCulture) + " live";
        }

        public static string RoundEnded(int round, Team? winner, int alphaWins, int bravoWins)
        {
            return Tag + " round " + round.ToString(CultureInfo.InvariantCulture)
                + " ended winner=" + (winner.HasValue ? winner.Value.ToString() : "draw")
                + " wins=" + alphaWins.ToString(CultureInfo.InvariantCulture) + "-" + bravoWins.ToString(CultureInfo.InvariantCulture);
        }

        public static string MapChanged(string fromMap, string toMap)
        {
            if (string.IsNullOrEmpty(fromMap))
            {
                throw new ArgumentException("fromMap must not be empty", nameof(fromMap));
            }

            if (string.IsNullOrEmpty(toMap))
            {
                throw new ArgumentException("toMap must not be empty", nameof(toMap));
            }

            return Tag + " map changed from=" + fromMap + " to=" + toMap;
        }

        public static string NextMatch(float delaySeconds)
        {
            if (!(delaySeconds >= 0f) || float.IsInfinity(delaySeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(delaySeconds), delaySeconds, "delaySeconds must not be negative and must be finite");
            }

            return Tag + " next match in=" + delaySeconds.ToString("F1", CultureInfo.InvariantCulture) + "s";
        }

        private static string SoloStateName(Match match)
        {
            switch (match.State)
            {
                case GameModeState.Waiting:
                    return "Waiting";
                case GameModeState.Playing:
                    return "Playing";
                case GameModeState.Ended:
                    return match.Result == MatchOutcome.Won ? "Won" : "Lost";
                default:
                    throw new ArgumentOutOfRangeException(nameof(match), match.State, "match has an unknown state");
            }
        }

        // internal にしないのは、GameKit.Core は内部をキットのアセンブリに見せないので、同じ書式でチームを書くキットのログ (PingLog・ServerEventLog など) から呼べなくなるため
        public static string TeamName(Team team)
        {
            switch (team)
            {
                case Team.Alpha:
                    return "Alpha";
                case Team.Bravo:
                    return "Bravo";
                default:
                    throw new ArgumentOutOfRangeException(nameof(team), team, "team is not a valid Team");
            }
        }
    }
}

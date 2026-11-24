using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class VipHudFormatter : IModeHudFormatter
    {
        public static LocalizedText Waiting => new LocalizedText("mode.vip.waiting");

        public static LocalizedText YouAreVip => new LocalizedText("mode.vip.youAreVip");

        public static LocalizedText Escort => new LocalizedText("mode.vip.escort");

        public static LocalizedText Eliminate => new LocalizedText("mode.vip.eliminate");

        public static LocalizedText Escaping(int percent)
        {
            return new LocalizedText("mode.vip.escaping", percent);
        }

        public static LocalizedText Rounds(int own, int opponent)
        {
            return new LocalizedText("mode.vip.rounds", own, opponent);
        }

        // 切り捨てるのは、100% を出すのを脱出し終えたときだけにするため。
        public static int ExitPercent(float exitFraction)
        {
            if (!(exitFraction > 0f))
            {
                return 0;
            }

            if (exitFraction >= 1f)
            {
                return 100;
            }

            return Math.Min(99, (int)Math.Floor(exitFraction * 100f));
        }

        // 守る側か攻める側かは VipStatus でなく ModeStatus.OwnSide で決まるので、両方を受け取る。
        public static IReadOnlyList<LocalizedText> HudLines(VipStatus detail, Side? ownSide)
        {
            if (detail == null)
            {
                throw new ArgumentNullException(nameof(detail));
            }

            var lines = new List<LocalizedText>();
            if (detail.IsLocalVip == true)
            {
                lines.Add(YouAreVip);
            }
            else if (ownSide.HasValue)
            {
                lines.Add(ownSide.Value == detail.DefenderSide ? Escort : Eliminate);
            }

            if (!detail.VipId.HasValue)
            {
                lines.Add(Waiting);
            }
            else if (detail.ExitFraction > 0f)
            {
                lines.Add(Escaping(ExitPercent(detail.ExitFraction)));
            }

            return lines;
        }

        // 陣営が分からない (bot だけの) ときは自分と相手を分けられないので null を返し、得点の行だけに任せる。
        public static LocalizedText? RoundLine(ModeStatus status)
        {
            if (status == null)
            {
                throw new ArgumentNullException(nameof(status));
            }

            if (!status.OwnSide.HasValue)
            {
                return null;
            }

            var own = 0;
            var opponent = 0;
            var found = false;
            foreach (var score in status.SideScores)
            {
                if (score.Side == status.OwnSide.Value)
                {
                    own = HudFormat.DisplayedScore(score.Score);
                    found = true;
                }
                else
                {
                    opponent = Math.Max(opponent, HudFormat.DisplayedScore(score.Score));
                }
            }

            return found ? Rounds(own, opponent) : (LocalizedText?)null;
        }

        public LocalizedText Time(ModeStatus status) => ModeStatusFormat.Remaining(status);

        public IReadOnlyList<ModeHudScoreLine> Scores(ModeStatus status) => ModeStatusFormat.Scores(status);

        public void AppendLines(ModeStatus status, List<LocalizedText> lines)
        {
            ModeStatusFormat.AppendRespawn(status, lines);
            if (!(status.Detail is VipStatus detail))
            {
                return;
            }

            lines.AddRange(HudLines(detail, status.OwnSide));
            var rounds = RoundLine(status);
            if (rounds.HasValue)
            {
                lines.Add(rounds.Value);
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
            ModeHudFormatters.Register<VipStatus>(new VipHudFormatter());
        }
    }
}

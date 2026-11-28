using System;
using System.Collections.Generic;

namespace GameKit.Core
{
    public static class StaminaJson
    {
        public const string MaxItem = "max";
        public const string SprintCostPerSecondItem = "sprintCostPerSecond";
        public const string RegenPerSecondItem = "regenPerSecond";
        public const string RegenDelaySecondsItem = "regenDelaySeconds";
        public const string RecoverAmountItem = "recoverAmount";
        public const string CostItemSuffix = "Cost";

        private const string _kind = "stamina JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[]
        {
            MaxItem,
            SprintCostPerSecondItem,
            RegenPerSecondItem,
            RegenDelaySecondsItem,
            RecoverAmountItem,
        };

        // 動きの消費を入れ子の表にせず「動きのキー + Cost」の項目 (slide なら slideCost) に置くのは、
        // 動きが列挙だった頃のファイル (slideCost・dodgeCost を直に並べた形) をそのまま読めるようにするため。
        public static string CostItemOf(string use)
        {
            if (string.IsNullOrEmpty(use))
            {
                throw new ArgumentException("use must not be empty", nameof(use));
            }

            return use + CostItemSuffix;
        }

        public static StaminaSpec Parse(string text, string sourceName)
        {
            return Parse(text, sourceName, null);
        }

        // 読む動きは defaultUseCosts のキーで決める。ゲームが持たない動きの項目は、綴り違いと見分けられないので知らない項目として断る。
        public static StaminaSpec Parse(string text, string sourceName, IReadOnlyDictionary<string, float> defaultUseCosts)
        {
            var source = ServerDataJson.SourceOf(sourceName, _kind);
            var uses = new List<string>();
            if (defaultUseCosts != null)
            {
                uses.AddRange(defaultUseCosts.Keys);
                uses.Sort(StringComparer.Ordinal);
            }

            var known = new List<string>(ItemNames);
            foreach (var use in uses)
            {
                known.Add(CostItemOf(use));
            }

            try
            {
                var fields = new ServerDataJson.Fields(source, _kind, ServerDataJson.ParseDocument(text, source, _kind), string.Empty, known);
                try
                {
                    var max = fields.ReadFloat(MaxItem, StaminaSpec.DefaultMax);
                    var costs = new Dictionary<string, float>(StringComparer.Ordinal);
                    foreach (var use in uses)
                    {
                        var item = CostItemOf(use);
                        var cost = fields.ReadFloat(item, defaultUseCosts[use]);

                        // StaminaSpec は消費の誤りをまとめて useCosts として断り、どの項目かを言えないので、ここで項目名を付けて断る。
                        if (max > 0f && !float.IsInfinity(max) && (!(cost >= 0f) || cost > max))
                        {
                            throw fields.Rejected(item, item + " must be between 0 and max");
                        }

                        costs.Add(use, cost);
                    }

                    return new StaminaSpec(
                        max,
                        fields.ReadFloat(SprintCostPerSecondItem, StaminaSpec.DefaultSprintCostPerSecond),
                        fields.ReadFloat(RegenPerSecondItem, StaminaSpec.DefaultRegenPerSecond),
                        fields.ReadFloat(RegenDelaySecondsItem, StaminaSpec.DefaultRegenDelaySeconds),
                        fields.ReadFloat(RecoverAmountItem, StaminaSpec.DefaultRecoverAmount),
                        costs);
                }
                catch (ArgumentException e)
                {
                    throw fields.Rejected(Known(e.ParamName) ? e.ParamName : null, e.Message, e);
                }
            }
            catch (ServerDataJsonException e)
            {
                // KitTuning とサーバーは TuningJsonException だけを「調整値のファイルが読めない」と扱う。
                var inner = e.InnerException is ArgumentException rejected ? (Exception)rejected : e;
                throw new TuningJsonException(e.SourceName, e.Item, e.Message, inner);
            }
        }

        // StaminaSpec は引数名を ParamName に入れて投げ、引数名は項目名と同じなので、そのまま項目名として返せる。
        private static bool Known(string name)
        {
            foreach (var item in ItemNames)
            {
                if (string.Equals(item, name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

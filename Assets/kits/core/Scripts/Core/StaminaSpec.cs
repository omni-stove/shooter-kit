using System;
using System.Collections.Generic;

namespace GameKit.Core
{
    public sealed class StaminaSpec
    {
        public const float DefaultMax = 100f;
        public const float DefaultSprintCostPerSecond = 20f;
        public const float DefaultRegenPerSecond = 25f;
        public const float DefaultRegenDelaySeconds = 1f;
        public const float DefaultRecoverAmount = 30f;

        private static readonly IReadOnlyDictionary<string, float> _noUseCosts = new Dictionary<string, float>(StringComparer.Ordinal);

        public static StaminaSpec Default { get; } = new StaminaSpec();

        public float Max { get; }

        public float SprintCostPerSecond { get; }

        // 一回ごとに払う動き (滑る・回避など) の消費を動きのキーで引く。動きの種類を列挙で持たないのは、どの動きがあるかをゲームが決めるため。
        public IReadOnlyDictionary<string, float> UseCosts { get; }

        public float RegenPerSecond { get; }

        // 使うたびに数え直す。走りやめた直後や回避の直後に、すぐ戻り始めないようにするため。
        public float RegenDelaySeconds { get; }

        // 尽きたら 0 より上に戻っただけでは使わせず、ここまで戻ってから使わせる。0 を超えた途端に走り出しては尽きるのを毎 tick 繰り返さないため。
        public float RecoverAmount { get; }

        public StaminaSpec(
            float max = DefaultMax,
            float sprintCostPerSecond = DefaultSprintCostPerSecond,
            float regenPerSecond = DefaultRegenPerSecond,
            float regenDelaySeconds = DefaultRegenDelaySeconds,
            float recoverAmount = DefaultRecoverAmount,
            IReadOnlyDictionary<string, float> useCosts = null)
        {
            RequirePositive(max, nameof(max));
            RequireNonNegative(sprintCostPerSecond, nameof(sprintCostPerSecond));
            RequirePositive(regenPerSecond, nameof(regenPerSecond));
            RequireNonNegative(regenDelaySeconds, nameof(regenDelaySeconds));

            if (!(recoverAmount > 0f) || recoverAmount > max)
            {
                throw new ArgumentOutOfRangeException(nameof(recoverAmount), recoverAmount, "recoverAmount must be greater than 0 and at most max");
            }

            // 渡された辞書をそのまま持たないのは、渡した側が後で書き換えると調整値まで変わってしまうため。
            var costs = new Dictionary<string, float>(StringComparer.Ordinal);
            if (useCosts != null)
            {
                foreach (var pair in useCosts)
                {
                    if (string.IsNullOrEmpty(pair.Key))
                    {
                        throw new ArgumentException("useCosts must not have an empty use key", nameof(useCosts));
                    }

                    // max を超える一回の消費は満タンでも払えず、その動きが二度と使えなくなるので弾く。
                    if (!(pair.Value >= 0f) || pair.Value > max)
                    {
                        throw new ArgumentOutOfRangeException(nameof(useCosts), pair.Value, "the cost of " + pair.Key + " must be between 0 and max");
                    }

                    costs.Add(pair.Key, pair.Value);
                }
            }

            Max = max;
            SprintCostPerSecond = sprintCostPerSecond;
            UseCosts = costs.Count == 0 ? _noUseCosts : costs;
            RegenPerSecond = regenPerSecond;
            RegenDelaySeconds = regenDelaySeconds;
            RecoverAmount = recoverAmount;
        }

        public bool Knows(string use)
        {
            return use != null && UseCosts.ContainsKey(use);
        }

        // 知らない動きを 0 として払わせず断るのは、キーの綴り違いで消費の無い動きになっても気付けなくなるため。
        public float CostOf(string use)
        {
            if (use == null)
            {
                throw new ArgumentNullException(nameof(use));
            }

            if (!UseCosts.TryGetValue(use, out var cost))
            {
                throw new ArgumentOutOfRangeException(nameof(use), use, "use has no stamina cost in this spec");
            }

            return cost;
        }

        private static void RequirePositive(float value, string name)
        {
            if (!(value > 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be positive and finite");
            }
        }

        private static void RequireNonNegative(float value, string name)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be non-negative and finite");
            }
        }
    }
}

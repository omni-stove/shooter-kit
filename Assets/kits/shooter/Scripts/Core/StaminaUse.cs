using System;
using System.Collections.Generic;
using GameKit.Core;

namespace Shooter.Core
{
    // シューターの一回ごとに払う動きのキーと既定の消費。キットの StaminaSpec は動きの種類を持たないので、ここで名前と既定値を決める。
    public static class StaminaUse
    {
        public const string Slide = "slide";
        public const string Dodge = "dodge";

        public const float DefaultSlideCost = 20f;
        public const float DefaultDodgeCost = 25f;

        public static IReadOnlyDictionary<string, float> DefaultCosts { get; } = new Dictionary<string, float>(StringComparer.Ordinal)
        {
            [Slide] = DefaultSlideCost,
            [Dodge] = DefaultDodgeCost,
        };

        public static StaminaSpec DefaultSpec { get; } = Spec();

        public static StaminaSpec Spec(
            float max = StaminaSpec.DefaultMax,
            float sprintCostPerSecond = StaminaSpec.DefaultSprintCostPerSecond,
            float slideCost = DefaultSlideCost,
            float dodgeCost = DefaultDodgeCost,
            float regenPerSecond = StaminaSpec.DefaultRegenPerSecond,
            float regenDelaySeconds = StaminaSpec.DefaultRegenDelaySeconds,
            float recoverAmount = StaminaSpec.DefaultRecoverAmount)
        {
            var costs = new Dictionary<string, float>(StringComparer.Ordinal)
            {
                [Slide] = slideCost,
                [Dodge] = dodgeCost,
            };
            return new StaminaSpec(max, sprintCostPerSecond, regenPerSecond, regenDelaySeconds, recoverAmount, costs);
        }

        public static StaminaSpec Parse(string text, string sourceName)
        {
            return StaminaJson.Parse(text, sourceName, DefaultCosts);
        }
    }
}

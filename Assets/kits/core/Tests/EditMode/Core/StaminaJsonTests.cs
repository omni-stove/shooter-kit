using System.Collections.Generic;
using NUnit.Framework;

namespace GameKit.Core.Tests
{
    // Unity の EditMode テストと dotnet/GameKit.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class StaminaJsonTests
    {
        private static readonly IReadOnlyDictionary<string, float> _uses = new Dictionary<string, float> { ["climb"] = 12f, ["roll"] = 7f };

        [Test]
        public void TheCommonItemsAndEachUsesCostItemAreRead()
        {
            const string text = "{\"max\": 50, \"sprintCostPerSecond\": 10, \"climbCost\": 5, \"rollCost\": 15,"
                + " \"regenPerSecond\": 20, \"regenDelaySeconds\": 0.5, \"recoverAmount\": 25}";

            var spec = StaminaJson.Parse(text, "stamina.json", _uses);

            Assert.That(spec.Max, Is.EqualTo(50f));
            Assert.That(spec.SprintCostPerSecond, Is.EqualTo(10f));
            Assert.That(spec.CostOf("climb"), Is.EqualTo(5f));
            Assert.That(spec.CostOf("roll"), Is.EqualTo(15f));
            Assert.That(spec.RegenPerSecond, Is.EqualTo(20f));
            Assert.That(spec.RegenDelaySeconds, Is.EqualTo(0.5f));
            Assert.That(spec.RecoverAmount, Is.EqualTo(25f));
        }

        [Test]
        public void MissingItemsKeepTheDefaultsAndTheGamesUseCosts()
        {
            var spec = StaminaJson.Parse("{\"max\": 80}", "stamina.json", _uses);

            Assert.That(spec.Max, Is.EqualTo(80f));
            Assert.That(spec.SprintCostPerSecond, Is.EqualTo(StaminaSpec.DefaultSprintCostPerSecond));
            Assert.That(spec.CostOf("climb"), Is.EqualTo(12f));
            Assert.That(spec.CostOf("roll"), Is.EqualTo(7f));
            Assert.That(spec.RegenPerSecond, Is.EqualTo(StaminaSpec.DefaultRegenPerSecond));
            Assert.That(spec.RegenDelaySeconds, Is.EqualTo(StaminaSpec.DefaultRegenDelaySeconds));
            Assert.That(spec.RecoverAmount, Is.EqualTo(StaminaSpec.DefaultRecoverAmount));
        }

        [Test]
        public void WithoutUsesOnlyTheCommonItemsAreKnown()
        {
            var spec = StaminaJson.Parse("{\"max\": 40, \"recoverAmount\": 10}", "stamina.json");

            Assert.That(spec.Max, Is.EqualTo(40f));
            Assert.That(spec.UseCosts, Is.Empty);

            var e = Assert.Throws<TuningJsonException>(() => StaminaJson.Parse("{\"slideCost\": 5}", "stamina.json"));
            Assert.That(e.Item, Is.EqualTo("slideCost"), e.Message);
        }

        [Test]
        public void TheCostItemIsTheUseKeyFollowedByCost()
        {
            Assert.That(StaminaJson.CostItemOf("slide"), Is.EqualTo("slideCost"));
            Assert.Throws<System.ArgumentException>(() => StaminaJson.CostItemOf(string.Empty));
            Assert.Throws<System.ArgumentException>(() => StaminaJson.CostItemOf(null));
        }

        [TestCase("{\"max\": 0}", "max")]
        [TestCase("{\"sprintCostPerSecond\": -1}", "sprintCostPerSecond")]
        [TestCase("{\"max\": 10, \"climbCost\": 11, \"rollCost\": 5, \"recoverAmount\": 5}", "climbCost")]
        [TestCase("{\"max\": 10, \"climbCost\": 5, \"rollCost\": -1, \"recoverAmount\": 5}", "rollCost")]
        [TestCase("{\"regenPerSecond\": 0}", "regenPerSecond")]
        [TestCase("{\"regenDelaySeconds\": -1}", "regenDelaySeconds")]
        [TestCase("{\"recoverAmount\": 101}", "recoverAmount")]
        [TestCase("{\"swimCost\": 10}", "swimCost")]
        public void ABadItemIsRefusedWithItsName(string text, string item)
        {
            var e = Assert.Throws<TuningJsonException>(() => StaminaJson.Parse(text, "stamina.json", _uses));

            Assert.That(e.Item, Is.EqualTo(item), e.Message);
        }

        [Test]
        public void TextThatIsNotJsonIsRefused()
        {
            Assert.Throws<TuningJsonException>(() => StaminaJson.Parse("{", "stamina.json", _uses));
        }
    }
}

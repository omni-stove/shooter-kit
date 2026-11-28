using GameKit.Core;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // シューターの stamina.json は動きが列挙だった頃と同じ形 (slideCost・dodgeCost) のまま読めることを確かめる。
    public sealed class StaminaJsonTests
    {
        [Test]
        public void AllItemsAreRead()
        {
            const string text = "{\"max\": 50, \"sprintCostPerSecond\": 10, \"slideCost\": 5, \"dodgeCost\": 15,"
                + " \"regenPerSecond\": 20, \"regenDelaySeconds\": 0.5, \"recoverAmount\": 25}";

            var spec = StaminaUse.Parse(text, "stamina.json");

            Assert.That(spec.Max, Is.EqualTo(50f));
            Assert.That(spec.SprintCostPerSecond, Is.EqualTo(10f));
            Assert.That(spec.CostOf(StaminaUse.Slide), Is.EqualTo(5f));
            Assert.That(spec.CostOf(StaminaUse.Dodge), Is.EqualTo(15f));
            Assert.That(spec.RegenPerSecond, Is.EqualTo(20f));
            Assert.That(spec.RegenDelaySeconds, Is.EqualTo(0.5f));
            Assert.That(spec.RecoverAmount, Is.EqualTo(25f));
        }

        [Test]
        public void MissingItemsKeepTheDefaults()
        {
            var spec = StaminaUse.Parse("{\"max\": 80}", "stamina.json");

            Assert.That(spec.Max, Is.EqualTo(80f));
            Assert.That(spec.SprintCostPerSecond, Is.EqualTo(StaminaSpec.DefaultSprintCostPerSecond));
            Assert.That(spec.CostOf(StaminaUse.Slide), Is.EqualTo(StaminaUse.DefaultSlideCost));
            Assert.That(spec.CostOf(StaminaUse.Dodge), Is.EqualTo(StaminaUse.DefaultDodgeCost));
            Assert.That(spec.RegenPerSecond, Is.EqualTo(StaminaSpec.DefaultRegenPerSecond));
            Assert.That(spec.RegenDelaySeconds, Is.EqualTo(StaminaSpec.DefaultRegenDelaySeconds));
            Assert.That(spec.RecoverAmount, Is.EqualTo(StaminaSpec.DefaultRecoverAmount));
        }

        [TestCase("{\"max\": 0}", "max")]
        [TestCase("{\"sprintCostPerSecond\": -1}", "sprintCostPerSecond")]
        [TestCase("{\"max\": 10, \"slideCost\": 11, \"dodgeCost\": 5, \"recoverAmount\": 5}", "slideCost")]
        [TestCase("{\"max\": 10, \"slideCost\": 5, \"dodgeCost\": 11, \"recoverAmount\": 5}", "dodgeCost")]
        [TestCase("{\"regenPerSecond\": 0}", "regenPerSecond")]
        [TestCase("{\"regenDelaySeconds\": -1}", "regenDelaySeconds")]
        [TestCase("{\"recoverAmount\": 101}", "recoverAmount")]
        [TestCase("{\"maximum\": 10}", "maximum")]
        public void ABadItemIsRefusedWithItsName(string text, string item)
        {
            var e = Assert.Throws<TuningJsonException>(() => StaminaUse.Parse(text, "stamina.json"));

            Assert.That(e.Item, Is.EqualTo(item), e.Message);
        }

        [Test]
        public void TextThatIsNotJsonIsRefused()
        {
            Assert.Throws<TuningJsonException>(() => StaminaUse.Parse("{", "stamina.json"));
        }
    }
}

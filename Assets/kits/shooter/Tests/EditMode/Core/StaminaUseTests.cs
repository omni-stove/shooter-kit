using System;
using GameKit.Core;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class StaminaUseTests
    {
        [Test]
        public void TheDefaultSpecCostsASlideAndADodge()
        {
            var spec = StaminaUse.DefaultSpec;

            Assert.That(spec.Max, Is.EqualTo(StaminaSpec.DefaultMax));
            Assert.That(spec.CostOf(StaminaUse.Slide), Is.EqualTo(20f));
            Assert.That(spec.CostOf(StaminaUse.Dodge), Is.EqualTo(25f));
            Assert.That(spec.UseCosts.Count, Is.EqualTo(2));
            Assert.That(StaminaUse.DefaultCosts[StaminaUse.Slide], Is.EqualTo(StaminaUse.DefaultSlideCost));
            Assert.That(StaminaUse.DefaultCosts[StaminaUse.Dodge], Is.EqualTo(StaminaUse.DefaultDodgeCost));
        }

        [Test]
        public void TheKeysAreTheJsonItemPrefixes()
        {
            Assert.That(StaminaUse.Slide, Is.EqualTo("slide"));
            Assert.That(StaminaUse.Dodge, Is.EqualTo("dodge"));
            Assert.That(StaminaJson.CostItemOf(StaminaUse.Slide), Is.EqualTo("slideCost"));
            Assert.That(StaminaJson.CostItemOf(StaminaUse.Dodge), Is.EqualTo("dodgeCost"));
        }

        [Test]
        public void SpecTakesTheSlideAndDodgeCosts()
        {
            var spec = StaminaUse.Spec(max: 10f, slideCost: 3f, dodgeCost: 4f, recoverAmount: 5f);

            Assert.That(spec.Max, Is.EqualTo(10f));
            Assert.That(spec.CostOf(StaminaUse.Slide), Is.EqualTo(3f));
            Assert.That(spec.CostOf(StaminaUse.Dodge), Is.EqualTo(4f));
            Assert.That(spec.RecoverAmount, Is.EqualTo(5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => StaminaUse.Spec(slideCost: 101f));
            Assert.Throws<ArgumentOutOfRangeException>(() => StaminaUse.Spec(dodgeCost: float.NaN));
        }

        [Test]
        public void TheConfiguredLogLineListsBothCosts()
        {
            Assert.That(
                StaminaLog.FormatConfigured(Stamina.On, StaminaUse.DefaultSpec),
                Is.EqualTo("[Stamina] rule=On max=100.00 sprintPerSecond=20.00 regenPerSecond=25.00 regenDelay=1.00 recover=30.00 cost.dodge=25.00 cost.slide=20.00"));
        }
    }
}

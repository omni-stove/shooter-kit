using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class EnemySpecDefaultTests
    {
        [Test]
        public void DefaultHoldsTheDefaultValues()
        {
            var spec = EnemySpec.Default;

            Assert.That(spec.SightRange, Is.EqualTo(EnemySpec.DefaultSightRange));
            Assert.That(spec.AttackRange, Is.EqualTo(EnemySpec.DefaultAttackRange));
            Assert.That(spec.LoseSightSeconds, Is.EqualTo(EnemySpec.DefaultLoseSightSeconds));
            Assert.That(spec.SearchSeconds, Is.EqualTo(EnemySpec.DefaultSearchSeconds));
            Assert.That(spec.ReactionSeconds, Is.EqualTo(EnemySpec.DefaultReactionSeconds));
            Assert.That(spec.OpeningSpreadDegrees, Is.EqualTo(EnemySpec.DefaultOpeningSpreadDegrees));
            Assert.That(spec.SettledSpreadDegrees, Is.EqualTo(EnemySpec.DefaultSettledSpreadDegrees));
            Assert.That(spec.OpeningShots, Is.EqualTo(EnemySpec.DefaultOpeningShots));
            Assert.That(spec.CoverHideSeconds, Is.EqualTo(EnemySpec.DefaultCoverHideSeconds));
            Assert.That(spec.CoverPeekSeconds, Is.EqualTo(EnemySpec.DefaultCoverPeekSeconds));
            Assert.That(spec.CoverHealthFraction, Is.EqualTo(EnemySpec.DefaultCoverHealthFraction));
            Assert.That(spec.StaggerResistance, Is.EqualTo(EnemySpec.DefaultStaggerResistance));
            Assert.That(spec.Archetype, Is.EqualTo(EnemyArchetype.Rifleman));
            Assert.That(spec.Shield, Is.SameAs(ShieldSpec.Default));
            Assert.That(spec.Sniper, Is.SameAs(SniperSpec.Default));
            Assert.That(spec.Charger, Is.SameAs(ChargerSpec.Default));
            Assert.That(spec.Bomber, Is.SameAs(BomberSpec.Default));
            Assert.That(spec.CoverOnHitMaxDistance, Is.EqualTo(EnemySpec.DefaultCoverOnHitMaxDistance));
            Assert.That(spec.CoverMoveSpreadDegrees, Is.EqualTo(EnemySpec.DefaultCoverMoveSpreadDegrees));
            Assert.That(spec.CoverMaxPeeks, Is.EqualTo(EnemySpec.DefaultCoverMaxPeeks));
            Assert.That(spec.CoverPeekReactionSeconds, Is.EqualTo(EnemySpec.DefaultCoverPeekReactionSeconds));
            Assert.That(spec.CoverCooldownSeconds, Is.EqualTo(EnemySpec.DefaultCoverCooldownSeconds));
        }

        [Test]
        public void TheCoverDefaultsAreTheTunedValues()
        {
            Assert.That(EnemySpec.DefaultCoverOnHitMaxDistance, Is.EqualTo(5f));
            Assert.That(EnemySpec.DefaultCoverMoveSpreadDegrees, Is.EqualTo(4f));
            Assert.That(EnemySpec.DefaultCoverMaxPeeks, Is.EqualTo(3));
            Assert.That(EnemySpec.DefaultCoverPeekReactionSeconds, Is.EqualTo(0.3f));
            Assert.That(EnemySpec.DefaultCoverCooldownSeconds, Is.EqualTo(5f));
        }

        [Test]
        public void ASpecLeftWithoutCoverValuesTakesTheCoverDefaults()
        {
            var spec = new EnemySpec(20f, 10f, 1f, 2f, 0.5f);

            Assert.That(spec.CoverOnHitMaxDistance, Is.EqualTo(EnemySpec.DefaultCoverOnHitMaxDistance));
            Assert.That(spec.CoverMoveSpreadDegrees, Is.EqualTo(EnemySpec.DefaultCoverMoveSpreadDegrees));
            Assert.That(spec.CoverMaxPeeks, Is.EqualTo(EnemySpec.DefaultCoverMaxPeeks));
            Assert.That(spec.CoverPeekReactionSeconds, Is.EqualTo(EnemySpec.DefaultCoverPeekReactionSeconds));
            Assert.That(spec.CoverCooldownSeconds, Is.EqualTo(EnemySpec.DefaultCoverCooldownSeconds));
        }

        [Test]
        public void TheGivenCoverValuesAreKept()
        {
            var spec = new EnemySpec(
                20f,
                10f,
                1f,
                2f,
                0.5f,
                coverOnHitMaxDistance: 8f,
                coverMoveSpreadDegrees: 2.5f,
                coverMaxPeeks: 1,
                coverPeekReactionSeconds: 0f,
                coverCooldownSeconds: 0f);

            Assert.That(spec.CoverOnHitMaxDistance, Is.EqualTo(8f));
            Assert.That(spec.CoverMoveSpreadDegrees, Is.EqualTo(2.5f));
            Assert.That(spec.CoverMaxPeeks, Is.EqualTo(1));
            Assert.That(spec.CoverPeekReactionSeconds, Is.EqualTo(0f));
            Assert.That(spec.CoverCooldownSeconds, Is.EqualTo(0f));
        }

        [TestCase(-1f, EnemySpec.DefaultCoverMoveSpreadDegrees, EnemySpec.DefaultCoverMaxPeeks, EnemySpec.DefaultCoverPeekReactionSeconds, EnemySpec.DefaultCoverCooldownSeconds, "coverOnHitMaxDistance")]
        [TestCase(float.PositiveInfinity, EnemySpec.DefaultCoverMoveSpreadDegrees, EnemySpec.DefaultCoverMaxPeeks, EnemySpec.DefaultCoverPeekReactionSeconds, EnemySpec.DefaultCoverCooldownSeconds, "coverOnHitMaxDistance")]
        [TestCase(float.NaN, EnemySpec.DefaultCoverMoveSpreadDegrees, EnemySpec.DefaultCoverMaxPeeks, EnemySpec.DefaultCoverPeekReactionSeconds, EnemySpec.DefaultCoverCooldownSeconds, "coverOnHitMaxDistance")]
        [TestCase(EnemySpec.DefaultCoverOnHitMaxDistance, -1f, EnemySpec.DefaultCoverMaxPeeks, EnemySpec.DefaultCoverPeekReactionSeconds, EnemySpec.DefaultCoverCooldownSeconds, "coverMoveSpreadDegrees")]
        [TestCase(EnemySpec.DefaultCoverOnHitMaxDistance, EnemySpec.MaxSpreadDegrees + 1f, EnemySpec.DefaultCoverMaxPeeks, EnemySpec.DefaultCoverPeekReactionSeconds, EnemySpec.DefaultCoverCooldownSeconds, "coverMoveSpreadDegrees")]
        [TestCase(EnemySpec.DefaultCoverOnHitMaxDistance, EnemySpec.DefaultCoverMoveSpreadDegrees, 0, EnemySpec.DefaultCoverPeekReactionSeconds, EnemySpec.DefaultCoverCooldownSeconds, "coverMaxPeeks")]
        [TestCase(EnemySpec.DefaultCoverOnHitMaxDistance, EnemySpec.DefaultCoverMoveSpreadDegrees, EnemySpec.DefaultCoverMaxPeeks, -0.1f, EnemySpec.DefaultCoverCooldownSeconds, "coverPeekReactionSeconds")]
        [TestCase(EnemySpec.DefaultCoverOnHitMaxDistance, EnemySpec.DefaultCoverMoveSpreadDegrees, EnemySpec.DefaultCoverMaxPeeks, float.PositiveInfinity, EnemySpec.DefaultCoverCooldownSeconds, "coverPeekReactionSeconds")]
        [TestCase(EnemySpec.DefaultCoverOnHitMaxDistance, EnemySpec.DefaultCoverMoveSpreadDegrees, EnemySpec.DefaultCoverMaxPeeks, EnemySpec.DefaultCoverPeekReactionSeconds, -1f, "coverCooldownSeconds")]
        [TestCase(EnemySpec.DefaultCoverOnHitMaxDistance, EnemySpec.DefaultCoverMoveSpreadDegrees, EnemySpec.DefaultCoverMaxPeeks, EnemySpec.DefaultCoverPeekReactionSeconds, float.NaN, "coverCooldownSeconds")]
        public void ABadCoverValueIsRejectedByName(float onHitMaxDistance, float moveSpread, int maxPeeks, float peekReaction, float cooldown, string name)
        {
            var e = Assert.Throws<System.ArgumentOutOfRangeException>(() => new EnemySpec(
                20f,
                10f,
                1f,
                2f,
                0.5f,
                coverOnHitMaxDistance: onHitMaxDistance,
                coverMoveSpreadDegrees: moveSpread,
                coverMaxPeeks: maxPeeks,
                coverPeekReactionSeconds: peekReaction,
                coverCooldownSeconds: cooldown));

            Assert.That(e.ParamName, Is.EqualTo(name));
        }
    }
}

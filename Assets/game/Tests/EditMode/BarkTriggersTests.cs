using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Tests.EditMode
{
    public sealed class BarkTriggersTests
    {
        private static BarkObservation Calm(EnemyState state = EnemyState.Patrol)
        {
            return new BarkObservation(state);
        }

        private static IReadOnlyList<BarkKind> Pick(BarkObservation previous, BarkObservation current)
        {
            return BarkTriggers.Pick(previous, current, BarkSpec.Default);
        }

        [TestCase(EnemyState.Idle, EnemyState.Chase)]
        [TestCase(EnemyState.Idle, EnemyState.Attack)]
        [TestCase(EnemyState.Patrol, EnemyState.Chase)]
        [TestCase(EnemyState.Patrol, EnemyState.Attack)]
        [TestCase(EnemyState.Search, EnemyState.Chase)]
        [TestCase(EnemyState.Search, EnemyState.Attack)]
        public void Pick_UnawareToEngaging_ReturnsSpotted(EnemyState from, EnemyState to)
        {
            var picked = Pick(Calm(from), Calm(to));

            CollectionAssert.AreEqual(new[] { BarkKind.Spotted }, picked);
        }

        [Test]
        public void Pick_PatrolToAttack_ReturnsSpotted()
        {
            var picked = Pick(Calm(EnemyState.Patrol), Calm(EnemyState.Attack));

            CollectionAssert.AreEqual(new[] { BarkKind.Spotted }, picked);
        }

        [TestCase(EnemyState.Chase, EnemyState.Attack)]
        [TestCase(EnemyState.Attack, EnemyState.Chase)]
        [TestCase(EnemyState.TakeCover, EnemyState.Attack)]
        public void Pick_AlreadyEngagedToEngaging_ReturnsEmpty(EnemyState from, EnemyState to)
        {
            var picked = Pick(Calm(from), Calm(to));

            CollectionAssert.IsEmpty(picked);
        }

        [TestCase(EnemyState.Chase)]
        [TestCase(EnemyState.Attack)]
        public void Pick_EngagingToSearch_ReturnsLostTarget(EnemyState from)
        {
            var picked = Pick(Calm(from), Calm(EnemyState.Search));

            CollectionAssert.AreEqual(new[] { BarkKind.LostTarget }, picked);
        }

        [Test]
        public void Pick_PatrolToSearch_ReturnsEmpty()
        {
            var picked = Pick(Calm(EnemyState.Patrol), Calm(EnemyState.Search));

            CollectionAssert.IsEmpty(picked);
        }

        [TestCase(EnemyState.Attack)]
        [TestCase(EnemyState.Chase)]
        [TestCase(EnemyState.Patrol)]
        public void Pick_EnteringTakeCover_ReturnsMovingToCover(EnemyState from)
        {
            var picked = Pick(Calm(from), Calm(EnemyState.TakeCover));

            CollectionAssert.AreEqual(new[] { BarkKind.MovingToCover }, picked);
        }

        [Test]
        public void Pick_StayingInTakeCover_ReturnsEmpty()
        {
            var picked = Pick(Calm(EnemyState.TakeCover), Calm(EnemyState.TakeCover));

            CollectionAssert.IsEmpty(picked);
        }

        [Test]
        public void Pick_ReloadStarted_ReturnsReloading()
        {
            var previous = new BarkObservation(EnemyState.Attack, isReloading: false);
            var current = new BarkObservation(EnemyState.Attack, isReloading: true);

            CollectionAssert.AreEqual(new[] { BarkKind.Reloading }, Pick(previous, current));
        }

        [Test]
        public void Pick_ReloadContinuing_ReturnsEmpty()
        {
            var previous = new BarkObservation(EnemyState.Attack, isReloading: true);
            var current = new BarkObservation(EnemyState.Attack, isReloading: true);

            CollectionAssert.IsEmpty(Pick(previous, current));
        }

        [Test]
        public void Pick_ReloadFinished_ReturnsEmpty()
        {
            var previous = new BarkObservation(EnemyState.Attack, isReloading: true);
            var current = new BarkObservation(EnemyState.Attack, isReloading: false);

            CollectionAssert.IsEmpty(Pick(previous, current));
        }

        [Test]
        public void Pick_WasHit_ReturnsTakingFire()
        {
            var current = new BarkObservation(EnemyState.Attack, wasHit: true);

            CollectionAssert.AreEqual(new[] { BarkKind.TakingFire }, Pick(Calm(EnemyState.Attack), current));
        }

        [Test]
        public void Pick_NearbyAllyWentDown_ReturnsAllyDown()
        {
            var current = new BarkObservation(EnemyState.Attack, nearbyAllyWentDown: true);

            CollectionAssert.AreEqual(new[] { BarkKind.AllyDown }, Pick(Calm(EnemyState.Attack), current));
        }

        [TestCase(null, 7.9f)]
        [TestCase(null, 8f)]
        [TestCase(8.1f, 8f)]
        [TestCase(20f, 0f)]
        public void Pick_GrenadeEnteredWarningRadius_ReturnsGrenade(float? previousDistance, float currentDistance)
        {
            var previous = new BarkObservation(EnemyState.Attack, nearestHostileGrenadeDistance: previousDistance);
            var current = new BarkObservation(EnemyState.Attack, nearestHostileGrenadeDistance: currentDistance);

            CollectionAssert.AreEqual(new[] { BarkKind.Grenade }, Pick(previous, current));
        }

        [TestCase(null, null)]
        [TestCase(null, 8.1f)]
        [TestCase(12f, 9f)]
        [TestCase(6f, 3f)]
        [TestCase(3f, null)]
        public void Pick_GrenadeNotEnteringWarningRadius_ReturnsEmpty(float? previousDistance, float? currentDistance)
        {
            var previous = new BarkObservation(EnemyState.Attack, nearestHostileGrenadeDistance: previousDistance);
            var current = new BarkObservation(EnemyState.Attack, nearestHostileGrenadeDistance: currentDistance);

            CollectionAssert.IsEmpty(Pick(previous, current));
        }

        [Test]
        public void Pick_GrenadeWarningRadius_IsReadFromSpec()
        {
            var spec = new BarkSpec(grenadeWarningRadius: 2f);
            var previous = new BarkObservation(EnemyState.Attack);
            var outside = new BarkObservation(EnemyState.Attack, nearestHostileGrenadeDistance: 5f);
            var inside = new BarkObservation(EnemyState.Attack, nearestHostileGrenadeDistance: 1.5f);

            CollectionAssert.IsEmpty(BarkTriggers.Pick(previous, outside, spec));
            CollectionAssert.AreEqual(new[] { BarkKind.Grenade }, BarkTriggers.Pick(outside, inside, spec));
        }

        [Test]
        public void Pick_GrenadeAndReloadTogether_PutsGrenadeFirst()
        {
            var previous = new BarkObservation(EnemyState.Attack);
            var current = new BarkObservation(EnemyState.Attack, isReloading: true, nearestHostileGrenadeDistance: 4f);

            CollectionAssert.AreEqual(new[] { BarkKind.Grenade, BarkKind.Reloading }, Pick(previous, current));
        }

        [Test]
        public void Pick_SpottedWithHitAndReload_OrdersByPriority()
        {
            var previous = new BarkObservation(EnemyState.Patrol);
            var current = new BarkObservation(EnemyState.Attack, isReloading: true, wasHit: true);

            CollectionAssert.AreEqual(
                new[] { BarkKind.Spotted, BarkKind.TakingFire, BarkKind.Reloading },
                Pick(previous, current));
        }

        [Test]
        public void Pick_LostTargetWithAllyDown_PutsAllyDownFirst()
        {
            var previous = new BarkObservation(EnemyState.Chase);
            var current = new BarkObservation(EnemyState.Search, nearbyAllyWentDown: true);

            CollectionAssert.AreEqual(new[] { BarkKind.AllyDown, BarkKind.LostTarget }, Pick(previous, current));
        }

        [Test]
        public void Pick_TakeCoverUnderFireWhileReloading_OrdersByPriority()
        {
            var previous = new BarkObservation(EnemyState.Attack);
            var current = new BarkObservation(EnemyState.TakeCover, isReloading: true, wasHit: true);

            CollectionAssert.AreEqual(
                new[] { BarkKind.TakingFire, BarkKind.MovingToCover, BarkKind.Reloading },
                Pick(previous, current));
        }

        [Test]
        public void Pick_EverythingAtOnce_OrdersByPriority()
        {
            var previous = new BarkObservation(EnemyState.Idle);
            var current = new BarkObservation(
                EnemyState.Attack,
                isReloading: true,
                wasHit: true,
                nearestHostileGrenadeDistance: 1f,
                nearbyAllyWentDown: true);

            CollectionAssert.AreEqual(
                new[] { BarkKind.Grenade, BarkKind.AllyDown, BarkKind.Spotted, BarkKind.TakingFire, BarkKind.Reloading },
                Pick(previous, current));
        }

        [TestCase(EnemyState.Idle)]
        [TestCase(EnemyState.Patrol)]
        [TestCase(EnemyState.Chase)]
        [TestCase(EnemyState.Attack)]
        [TestCase(EnemyState.Search)]
        [TestCase(EnemyState.TakeCover)]
        public void Pick_NoChange_ReturnsEmpty(EnemyState state)
        {
            var observation = new BarkObservation(state, isReloading: true, nearestHostileGrenadeDistance: 2f);

            CollectionAssert.IsEmpty(Pick(observation, observation));
        }

        [Test]
        public void Pick_DeadFlag_ReturnsEmptyEvenWithChanges()
        {
            var previous = new BarkObservation(EnemyState.Patrol);
            var current = new BarkObservation(
                EnemyState.Attack,
                isReloading: true,
                wasHit: true,
                nearestHostileGrenadeDistance: 1f,
                nearbyAllyWentDown: true,
                isDead: true);

            CollectionAssert.IsEmpty(Pick(previous, current));
        }

        [Test]
        public void Pick_DeadState_ReturnsEmptyEvenWithChanges()
        {
            var previous = new BarkObservation(EnemyState.Attack);
            var current = new BarkObservation(EnemyState.Dead, wasHit: true, nearbyAllyWentDown: true);

            CollectionAssert.IsEmpty(Pick(previous, current));
        }

        [Test]
        public void Pick_NullSpec_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => BarkTriggers.Pick(Calm(), Calm(), null));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void Observation_InvalidGrenadeDistance_Throws(float distance)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new BarkObservation(EnemyState.Attack, nearestHostileGrenadeDistance: distance));
        }

        [Test]
        public void Observation_UndefinedState_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BarkObservation((EnemyState)99));
        }

        [Test]
        public void Observation_Defaults_AreCalm()
        {
            var observation = new BarkObservation(EnemyState.Patrol);

            Assert.AreEqual(EnemyState.Patrol, observation.State);
            Assert.IsFalse(observation.IsReloading);
            Assert.IsFalse(observation.WasHit);
            Assert.IsNull(observation.NearestHostileGrenadeDistance);
            Assert.IsFalse(observation.NearbyAllyWentDown);
            Assert.IsFalse(observation.IsDead);
        }
    }
}

using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class EnemyBrainCoverOnHitTests
    {
        // プレイヤーは +Z の 10m 先。点はどれも +Z を向き、プレイヤーを遮る。
        private static readonly Vector3 _player = new Vector3(0f, 0f, 10f);
        private static readonly Vector3 _facing = new Vector3(0f, 0f, 1f);

        // 視認 20m、攻撃 10m (FireRange も 10m)、反応 0.5 秒、撃たれただけなら 5m まで、移る途中は 4° 散らす (どちらも既定値)。
        private static EnemySpec Spec(float coverOnHitMaxDistance = EnemySpec.DefaultCoverOnHitMaxDistance, float coverMoveSpreadDegrees = EnemySpec.DefaultCoverMoveSpreadDegrees) =>
            new EnemySpec(
                20f,
                10f,
                1f,
                2f,
                0.5f,
                6f,
                2f,
                3,
                coverHideSeconds: 1f,
                coverPeekSeconds: 1f,
                coverOnHitMaxDistance: coverOnHitMaxDistance,
                coverMoveSpreadDegrees: coverMoveSpreadDegrees);

        private static CoverPoint PointAt(float x) => new CoverPoint(new Vector3(x, 0f, 0f), _facing, CoverHeight.Low);

        private static EnemyPerception Watches(Vector3 self, Vector3 target, bool lineOfSight) =>
            new EnemyPerception(self, target, lineOfSight, false, null);

        private static EnemyPerception Shot(Vector3 self, Vector3 target, bool lineOfSight, float healthFraction) =>
            new EnemyPerception(self, target, lineOfSight, false, null, true, target, healthFraction);

        private static EnemyBrain Brain(EnemySpec spec, params CoverPoint[] points)
        {
            var brain = new EnemyBrain(spec);
            brain.SetPatrolRoute(new[] { new Vector3(0f, 0f, -10f) });
            brain.UseCover(new CoverPointSelector(points));
            return brain;
        }

        // 反応時間を済ませ、1 発撃った後の交戦中にする。
        private static EnemyBrain Engaged(EnemySpec spec, params CoverPoint[] points)
        {
            var brain = Brain(spec, points);
            brain.Tick(0.1f, Watches(Vector3.Zero, _player, true));
            var ready = brain.Tick(0.5f, Watches(Vector3.Zero, _player, true));
            Assert.That(brain.State, Is.EqualTo(EnemyState.Attack));
            Assert.That(ready.Fire, Is.True);
            brain.RecordShot();
            return brain;
        }

        [Test]
        public void FullHealthHitWithOnlyFarCoverKeepsFiring()
        {
            var brain = Engaged(Spec(), PointAt(6f));

            var decision = brain.Tick(0.1f, Shot(Vector3.Zero, _player, true, 1f));

            Assert.That(brain.State, Is.EqualTo(EnemyState.Attack), "6m 先の点には移らない");
            Assert.That(brain.CoverPhase, Is.EqualTo(EnemyCoverPhase.None));
            Assert.That(brain.CoverPointIndex, Is.EqualTo(-1));
            Assert.That(decision.Fire, Is.True, "構え直さずに撃ち続ける");
            Assert.That(decision.LookAt, Is.EqualTo(_player));
            Assert.That(brain.ShotsInAttack, Is.EqualTo(1));
        }

        [Test]
        public void FullHealthHitWhilePatrollingWithOnlyFarCoverFightsBack()
        {
            var brain = Brain(Spec(), PointAt(6f));

            brain.Tick(0.1f, Shot(Vector3.Zero, _player, true, 1f));
            Assert.That(brain.State, Is.EqualTo(EnemyState.Attack));

            Assert.That(brain.Tick(0.5f, Shot(Vector3.Zero, _player, true, 1f)).Fire, Is.True);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Attack));
        }

        [TestCase(4f)]
        [TestCase(5f)]
        public void FullHealthHitMovesToNearCover(float distance)
        {
            var brain = Engaged(Spec(), PointAt(distance));

            var decision = brain.Tick(0.1f, Shot(Vector3.Zero, _player, true, 1f));

            Assert.That(brain.State, Is.EqualTo(EnemyState.TakeCover));
            Assert.That(brain.CoverPhase, Is.EqualTo(EnemyCoverPhase.Moving));
            Assert.That(brain.CoverPointIndex, Is.Zero);
            Assert.That(decision.MoveTo, Is.EqualTo(PointAt(distance).Position));
        }

        [Test]
        public void HitBelowTheHealthThresholdMovesToFarCover()
        {
            var brain = Engaged(Spec(), PointAt(12f));

            var decision = brain.Tick(0.1f, Shot(Vector3.Zero, _player, true, 0.4f));

            Assert.That(brain.State, Is.EqualTo(EnemyState.TakeCover));
            Assert.That(brain.CoverPhase, Is.EqualTo(EnemyCoverPhase.Moving));
            Assert.That(brain.CoverPointIndex, Is.Zero);
            Assert.That(decision.MoveTo, Is.EqualTo(PointAt(12f).Position));
        }

        [Test]
        public void HitAtExactlyTheHealthThresholdStaysNear()
        {
            var brain = Engaged(Spec(), PointAt(12f));

            brain.Tick(0.1f, Shot(Vector3.Zero, _player, true, 0.5f));

            Assert.That(brain.State, Is.EqualTo(EnemyState.Attack), "CoverHealthFraction ちょうどはまだ撃たれただけ");
        }

        [Test]
        public void WoundedWithoutBeingHitMovesToFarCover()
        {
            var brain = Brain(Spec(), PointAt(12f));

            brain.Tick(0.1f, new EnemyPerception(Vector3.Zero, _player, false, false, null, false, null, 0.4f));

            Assert.That(brain.State, Is.EqualTo(EnemyState.TakeCover));
            Assert.That(brain.CoverPointIndex, Is.Zero);
        }

        [Test]
        public void OnHitDistanceComesFromTheSpec()
        {
            var brain = Engaged(Spec(coverOnHitMaxDistance: 13f), PointAt(12f));

            brain.Tick(0.1f, Shot(Vector3.Zero, _player, true, 1f));

            Assert.That(brain.State, Is.EqualTo(EnemyState.TakeCover));
            Assert.That(brain.CoverPointIndex, Is.Zero);
        }

        [Test]
        public void PicksTheNearPointWhenBothAreAvailable()
        {
            var brain = Engaged(Spec(), PointAt(12f), PointAt(4f));

            brain.Tick(0.1f, Shot(Vector3.Zero, _player, true, 1f));

            Assert.That(brain.CoverPointIndex, Is.EqualTo(1));
        }

        [Test]
        public void FiresWhileMovingToCoverWithWiderSpread()
        {
            var brain = Engaged(Spec(), PointAt(4f));
            var before = brain.NextShotSpreadDegrees;
            Assert.That(before, Is.EqualTo(brain.Spec.SpreadDegreesForShot(1)));

            var decision = brain.Tick(0.1f, Shot(Vector3.Zero, _player, true, 1f));

            Assert.That(brain.CoverPhase, Is.EqualTo(EnemyCoverPhase.Moving));
            Assert.That(decision.MoveTo, Is.EqualTo(PointAt(4f).Position));
            Assert.That(decision.LookAt, Is.EqualTo(_player));
            Assert.That(decision.Fire, Is.True, "交戦の構え時間を引き継ぐ");
            Assert.That(decision.Crouch, Is.False);
            Assert.That(brain.ShotsInAttack, Is.EqualTo(1), "撃った数も引き継ぐ");
            Assert.That(brain.NextShotSpreadDegrees, Is.EqualTo(before + 4f).Within(1e-5f));

            var halfway = new Vector3(2f, 0f, 1f);
            Assert.That(brain.Tick(0.1f, Watches(halfway, _player, true)).Fire, Is.True, "途中も撃つ");
            Assert.That(brain.NextShotSpreadDegrees, Is.EqualTo(before + 4f).Within(1e-5f));

            brain.Tick(0.1f, Watches(PointAt(4f).Position, _player, true));
            Assert.That(brain.CoverPhase, Is.EqualTo(EnemyCoverPhase.Hiding));
            Assert.That(brain.NextShotSpreadDegrees, Is.EqualTo(before).Within(1e-5f), "着いたら足さない");
        }

        [Test]
        public void MoveSpreadComesFromTheSpec()
        {
            var brain = Engaged(Spec(coverMoveSpreadDegrees: 7f), PointAt(4f));
            var before = brain.NextShotSpreadDegrees;

            brain.Tick(0.1f, Shot(Vector3.Zero, _player, true, 1f));

            Assert.That(brain.NextShotSpreadDegrees, Is.EqualTo(before + 7f).Within(1e-5f));
        }

        [Test]
        public void DoesNotFireWhileMovingWithoutSeeingTheTarget()
        {
            var brain = Engaged(Spec(), PointAt(4f));

            var decision = brain.Tick(0.1f, Shot(Vector3.Zero, _player, false, 1f));
            Assert.That(brain.CoverPhase, Is.EqualTo(EnemyCoverPhase.Moving));
            Assert.That(decision.Fire, Is.False);

            var halfway = new Vector3(2f, 0f, 1f);
            Assert.That(brain.Tick(0.1f, Watches(halfway, _player, true)).Fire, Is.False, "見え直した瞬間には撃たない");
            Assert.That(brain.Tick(0.5f, Watches(halfway, _player, true)).Fire, Is.True, "ReactionSeconds 構え直したら撃つ");
        }

        [Test]
        public void DoesNotFireWhileMovingAtATargetBeyondFireRange()
        {
            var brain = Engaged(Spec(), PointAt(4f));
            brain.Tick(0.1f, Shot(Vector3.Zero, _player, true, 1f));

            var far = new Vector3(0f, 0f, 15f);
            var decision = brain.Tick(0.1f, Watches(new Vector3(1f, 0f, 0f), far, true));

            Assert.That(brain.CoverPhase, Is.EqualTo(EnemyCoverPhase.Moving));
            Assert.That(decision.LookAt, Is.EqualTo(far));
            Assert.That(decision.Fire, Is.False);
        }

        [Test]
        public void HitWhilePatrollingAimsBeforeFiringOnTheWay()
        {
            var brain = Brain(Spec(), PointAt(4f));

            var first = brain.Tick(1f, Shot(Vector3.Zero, _player, true, 1f));
            Assert.That(brain.CoverPhase, Is.EqualTo(EnemyCoverPhase.Moving));
            Assert.That(first.Fire, Is.False, "撃たれた瞬間には撃ち返さない");
            Assert.That(brain.ShotsInAttack, Is.Zero);

            var halfway = new Vector3(1f, 0f, 1f);
            Assert.That(brain.Tick(0.25f, Watches(halfway, _player, true)).Fire, Is.False, "0.25 秒後");
            Assert.That(brain.Tick(0.25f, Watches(halfway, _player, true)).Fire, Is.True, "ReactionSeconds 後");
        }
    }
}

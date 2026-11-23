using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class EnemyBrainEngageRangeTests
    {
        private static readonly Vector3 _beyond = new Vector3(0f, 0f, 19f);
        private static readonly Vector3 _inside = new Vector3(0f, 0f, 17f);

        // 視認 20m、攻撃 10m、撃ち始める距離 18m、反応 0.5 秒、向きの許容 15 度。
        private static EnemySpec Spec() => new EnemySpec(
            20f, 10f, 1f, 2f, 0.5f, staggerResistance: 0f, fireRange: 18f, fireToleranceDegrees: 15f);

        private static EnemyPerception Sees(Vector3 target) => new EnemyPerception(Vector3.Zero, target, true, false, null);

        private static EnemyPerception Nothing() => new EnemyPerception(Vector3.Zero, null, false, false, null);

        [Test]
        public void WatchingBeyondTheFireRangeLetsItFireOnTheFirstTickInside()
        {
            var brain = new EnemyBrain(Spec());

            foreach (var delta in new[] { 0f, 0.3f, 0.3f })
            {
                var chasing = brain.Tick(delta, Sees(_beyond));
                Assert.That(brain.State, Is.EqualTo(EnemyState.Chase));
                Assert.That(brain.IsAiming, Is.False, "it runs at the target instead of planting its feet");
                Assert.That(chasing.Fire, Is.False);
                Assert.That(chasing.MoveTo, Is.EqualTo(_beyond));
            }

            var firing = brain.Tick(0.02f, Sees(_inside));

            Assert.That(brain.State, Is.EqualTo(EnemyState.Chase));
            Assert.That(firing.Fire, Is.True);
            Assert.That(firing.MoveTo, Is.EqualTo(_inside), "it keeps closing in while it fires");
            Assert.That(firing.LookAt, Is.EqualTo(_inside));
            Assert.That(brain.AimRemainingSeconds, Is.Zero);
        }

        [Test]
        public void WatchingBeyondTheFireRangeForLessThanTheReactionStillAimsTheRest()
        {
            var brain = new EnemyBrain(Spec());
            brain.Tick(0f, Sees(_beyond));
            brain.Tick(0.3f, Sees(_beyond));

            var aiming = brain.Tick(0.1f, Sees(_inside));

            Assert.That(aiming.Fire, Is.False);
            Assert.That(aiming.MoveTo, Is.Null);
            Assert.That(brain.AimRemainingSeconds, Is.EqualTo(0.1f).Within(1e-5f));
        }

        [Test]
        public void LosingSightBeyondTheFireRangeStartsTheAimOver()
        {
            var brain = new EnemyBrain(Spec());
            brain.Tick(0f, Sees(_beyond));
            brain.Tick(0.6f, Sees(_beyond));

            brain.Tick(0.1f, Nothing());
            Assert.That(brain.State, Is.EqualTo(EnemyState.Chase), "it still heads for where it last saw the target");

            brain.Tick(0.1f, Sees(_beyond));
            var aiming = brain.Tick(0.1f, Sees(_inside));

            Assert.That(aiming.Fire, Is.False);
            Assert.That(aiming.MoveTo, Is.Null, "it plants its feet to aim again");
            Assert.That(brain.AimRemainingSeconds, Is.EqualTo(0.4f).Within(1e-5f));
        }
    }
}

using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Server.Tests
{
    // 軌道 (MeleeSpec.Path) のある振りを確かめる。刃は当たりの瞬間 0.2 秒から 0.2 秒かけて通り、通った相手へ通った順に与える。
    // 1 は東を向くので、右 (+ の角度) は南 (-Z)。2 は約 11 度右、4 は約 35 度左にいる (TheNearestTargetInTheFanIsHit と同じ並び)
    public sealed partial class ServerMeleeTests
    {
        private static MeleeSpec PathSpec(MeleePathStyle style, float from, float to, float width, int maxTargets)
        {
            return new MeleeSpec(
                damage: 30, reach: 2f, arcDegrees: 90f, swingSeconds: 0.6f, cooldownSeconds: 0.4f, impactSeconds: 0.2f, lungeDistance: 0f,
                path: new MeleePath(style, from, to, width, activeSeconds: 0.2f, maxTargets: maxTargets));
        }

        [Test]
        public void AHorizontalSwingHitsEveryoneItsBladeCrossesOnce()
        {
            StartWithFourAndPlaceTheEnemies(PathSpec(MeleePathStyle.Horizontal, 60f, -60f, 30f, maxTargets: 2));

            Swing(1);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 30), _log.ToString());
            Assert.That(_rules.Session.GetHealth(4), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 30), _log.ToString());
        }

        [Test]
        public void TheBladeHitsInTheOrderItCrossesNotTheNearestFirst()
        {
            StartWithFourAndPlaceTheEnemies(PathSpec(MeleePathStyle.Horizontal, 60f, -60f, 30f, maxTargets: 1));

            Swing(1);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 30), "the blade sweeps right to left and meets 2 first");
            Assert.That(_rules.Session.GetHealth(4), Is.EqualTo(NetMatchSettings.DefaultMaxHealth), "4 is nearer but the swing has hit its one target");
        }

        [Test]
        public void AVerticalSwingMissesWhoIsOutsideItsWidth()
        {
            StartWithFourAndPlaceTheEnemies(PathSpec(MeleePathStyle.Vertical, 60f, -30f, 30f, maxTargets: 3));

            Swing(1);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 30), _log.ToString());
            Assert.That(_rules.Session.GetHealth(4), Is.EqualTo(NetMatchSettings.DefaultMaxHealth), "35 degrees to the side is outside a 30 degree wide blade");
        }

        [Test]
        public void ASweepThatCrossesNobodyIsAMiss()
        {
            Start(MeleeRules(), PathSpec(MeleePathStyle.Horizontal, 60f, -60f, 30f, maxTargets: 2));
            Join(1);
            Join(2);
            Step(0.2f);
            Place(2, 5f, 0f);

            Swing(1);

            Assert.That(_log.ToString(), Does.Contain("[Melee] miss id=1"));
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
        }

        private void StartWithFourAndPlaceTheEnemies(MeleeSpec spec)
        {
            Start(MeleeRules(), spec);
            Join(1);
            Join(2);
            Join(3);
            Join(4);
            Step(0.2f);
            Place(2, -3.5f, -0.3f);
            Place(4, -4f, 0.7f);
            Place(3, -5f, 6f);
        }
    }
}

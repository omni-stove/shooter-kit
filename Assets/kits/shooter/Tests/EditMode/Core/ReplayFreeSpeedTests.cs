using GameKit.Core;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class ReplayFreeSpeedTests
    {
        // ReplaySpec は core のキットにあって DeathCamFraming を見られないので、既定の速さが倒れたときのカメラと揃っているかをこちらで見る。
        [Test]
        public void ReplayFreeSpeedDefaultMatchesTheDeathCamera()
        {
            Assert.That(ReplaySpec.DefaultFreeSpeed, Is.EqualTo(6f));
            Assert.That(ReplaySpec.DefaultFreeSpeed, Is.EqualTo(DeathCamFraming.DefaultFreeSpeed));
        }
    }
}

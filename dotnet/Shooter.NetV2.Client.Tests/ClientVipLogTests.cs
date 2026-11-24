using NUnit.Framework;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientVipLogTests
    {
        [Test]
        public void AShownMarkerNamesThePlayer()
        {
            Assert.That(ClientVipLog.MarkerShown(7), Is.EqualTo("[Net] vip marker shown player=7"));
        }

        [Test]
        public void ARemovedMarkerNamesThePlayer()
        {
            Assert.That(ClientVipLog.MarkerRemoved(7), Is.EqualTo("[Net] vip marker removed player=7"));
        }
    }
}

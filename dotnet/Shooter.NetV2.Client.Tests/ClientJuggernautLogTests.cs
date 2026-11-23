using NUnit.Framework;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientJuggernautLogTests
    {
        [Test]
        public void AShownMarkerNamesThePlayer()
        {
            Assert.That(ClientJuggernautLog.MarkerShown(7), Is.EqualTo("[Net] juggernaut marker shown player=7"));
        }

        [Test]
        public void ARemovedMarkerNamesThePlayer()
        {
            Assert.That(ClientJuggernautLog.MarkerRemoved(7), Is.EqualTo("[Net] juggernaut marker removed player=7"));
        }
    }
}

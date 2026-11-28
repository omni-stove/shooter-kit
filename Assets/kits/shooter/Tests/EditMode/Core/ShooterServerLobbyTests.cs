using GameKit.Core;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ShooterServerLobbyTests
    {
        [Test]
        public void TheKitDefaultKeepsTheExistingBucketAndName()
        {
            Assert.That(ShooterServerLobby.Default.BucketPrefix, Is.EqualTo("shooter-server:"));
            Assert.That(ShooterServerLobby.Default.DisplayName, Is.EqualTo("shooter-server"));
            Assert.That(ServerLobby.BucketId(ShooterServerLobby.Default, "0.3.0"), Is.EqualTo("shooter-server:0.3.0"));
        }

        [Test]
        public void LeftOutItemsUseTheKitValuesNotTheCoreOnes()
        {
            var empty = ShooterServerLobby.Parse("{}", "server-lobby.json");
            var renamed = ShooterServerLobby.Parse("{\"displayName\": \"arena-host\"}", "server-lobby.json");

            Assert.That(empty.BucketPrefix, Is.EqualTo("shooter-server:"));
            Assert.That(empty.DisplayName, Is.EqualTo("shooter-server"));
            Assert.That(renamed.BucketPrefix, Is.EqualTo("shooter-server:"));
            Assert.That(renamed.DisplayName, Is.EqualTo("arena-host"));
        }

        [Test]
        public void TheKitValuesAreNotTheCoreDefaults()
        {
            Assert.That(ShooterServerLobby.Default.BucketPrefix, Is.Not.EqualTo(ServerLobbySpec.DefaultBucketPrefix));
        }
    }
}

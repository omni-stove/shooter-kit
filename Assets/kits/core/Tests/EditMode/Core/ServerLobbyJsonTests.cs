using System;
using NUnit.Framework;

namespace GameKit.Core.Tests
{
    // Unity の EditMode テストと dotnet/GameKit.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ServerLobbyJsonTests
    {
        private static readonly string[] _expectedItemNames = { "bucketPrefix", "displayName" };

        [Test]
        public void ReadsEveryItem()
        {
            var spec = ServerLobbyJson.Parse("{\"bucketPrefix\": \"shooter-server:\", \"displayName\": \"shooter-server\"}", "server-lobby.json");

            Assert.That(spec.BucketPrefix, Is.EqualTo("shooter-server:"));
            Assert.That(spec.DisplayName, Is.EqualTo("shooter-server"));
        }

        [Test]
        public void TheCoreDefaultsNameNoKit()
        {
            var empty = ServerLobbyJson.Parse("{}", "server-lobby.json");

            Assert.That(empty.BucketPrefix, Is.EqualTo("game-server:"));
            Assert.That(empty.DisplayName, Is.EqualTo("game-server"));
            Assert.That(ServerLobbySpec.Default.BucketPrefix, Is.EqualTo(ServerLobbySpec.DefaultBucketPrefix));
            Assert.That(ServerLobbySpec.Default.DisplayName, Is.EqualTo(ServerLobbySpec.DefaultDisplayName));
        }

        [Test]
        public void LeftOutItemsUseTheGivenDefaults()
        {
            var kit = new ServerLobbySpec("arena-server:", "arena-server");

            var onlyName = ServerLobbyJson.Parse("{\"displayName\": \"arena-host\"}", "server-lobby.json", kit);

            Assert.That(onlyName.BucketPrefix, Is.EqualTo("arena-server:"));
            Assert.That(onlyName.DisplayName, Is.EqualTo("arena-host"));
            Assert.Throws<ArgumentNullException>(() => ServerLobbyJson.Parse("{}", "server-lobby.json", null));
        }

        [Test]
        public void TheItemNamesAreTheConstructorArgumentsAndThePathIsUnderTuning()
        {
            Assert.That(ServerLobbyJson.ItemNames, Is.EqualTo(_expectedItemNames));
            Assert.That(ServerLobbyJson.ResourcePath, Is.EqualTo("Tuning/server-lobby"));
            Assert.That(ServerLobbyJson.FileName, Is.EqualTo("server-lobby.json"));
        }

        [TestCase("{\"bucketPrefix\": \"\"}", "bucketPrefix")]
        [TestCase("{\"bucketPrefix\": \"Shooter Server:\"}", "bucketPrefix")]
        [TestCase("{\"bucketPrefix\": \"a-prefix-that-is-longer-than-32-chars:\"}", "bucketPrefix")]
        [TestCase("{\"displayName\": \" \"}", "displayName")]
        [TestCase("{\"displayName\": \" host\"}", "displayName")]
        [TestCase("{\"displayName\": \"a-display-name-longer-than-32-chars\"}", "displayName")]
        [TestCase("{\"bucketPrefix\": 3}", "bucketPrefix")]
        public void ABadItemIsRejectedByName(string text, string item)
        {
            var e = Assert.Throws<TuningJsonException>(() => ServerLobbyJson.Parse(text, "server-lobby.json"));

            Assert.That(e.SourceName, Is.EqualTo("server-lobby.json"));
            Assert.That(e.Item, Is.EqualTo(item));
            Assert.That(e.Message, Does.StartWith("server lobby JSON 'server-lobby.json' item '" + item + "' "));
        }

        [Test]
        public void AnUnknownItemIsRejectedWithTheKnownOnes()
        {
            var e = Assert.Throws<TuningJsonException>(() => ServerLobbyJson.Parse("{\"prefix\": \"x:\"}", "server-lobby.json"));

            Assert.That(e.Item, Is.EqualTo("prefix"));
            Assert.That(e.Message, Does.StartWith("server lobby JSON 'server-lobby.json': unknown item 'prefix' "));
            Assert.That(e.Message, Does.EndWith("known items here are bucketPrefix, displayName"));
        }

        [Test]
        public void BrokenJsonIsRejected()
        {
            var broken = Assert.Throws<TuningJsonException>(() => ServerLobbyJson.Parse("{\"bucketPrefix\": ", "server-lobby.json"));

            Assert.That(broken.Item, Is.Null);
            Assert.Throws<ArgumentNullException>(() => ServerLobbyJson.Parse(null, "server-lobby.json"));
        }

        [Test]
        public void ThePrefixRuleAllowsTheKitPrefixes()
        {
            Assert.That(ServerLobbySpec.IsBucketPrefix("shooter-server:"), Is.True);
            Assert.That(ServerLobbySpec.IsBucketPrefix("game_server.v2:"), Is.True);
            Assert.That(ServerLobbySpec.IsBucketPrefix("Shooter:"), Is.False);
            Assert.That(ServerLobbySpec.IsBucketPrefix(null), Is.False);
        }
    }
}

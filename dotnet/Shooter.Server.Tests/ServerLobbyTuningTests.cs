using System.IO;
using GameKit.Core;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Server.Tests
{
    public sealed class ServerLobbyTuningTests
    {
        private StringWriter _output;

        [SetUp]
        public void SetUp()
        {
            _output = new StringWriter();
        }

        [TearDown]
        public void TearDown()
        {
            _output.Dispose();
        }

        [Test]
        public void TheShippedFileKeepsTheExistingBucketAndName()
        {
            var root = RepositoryRoot();

            Assert.That(ServerModules.TryLoadServerLobby(Runtime(root), out var lobby, out var error), Is.True, error);

            Assert.That(lobby.BucketPrefix, Is.EqualTo("shooter-server:"));
            Assert.That(lobby.DisplayName, Is.EqualTo("shooter-server"));
            Assert.That(ServerLobby.BucketId(lobby, "0.3.0"), Is.EqualTo("shooter-server:0.3.0"));
            Assert.That(_output.ToString(), Does.Contain("[Lobby] server lobby tuning ").And.Contain("server-lobby.json bucket prefix=shooter-server:"));
        }

        [Test]
        public void TheServerAndTheClientShipTheSameFile()
        {
            var root = RepositoryRoot();

            var server = File.ReadAllText(Path.Combine(root, "server-data", "tuning", "server-lobby.json"));
            var client = File.ReadAllText(Path.Combine(root, "Assets", "kits", "shooter", "Resources", "Tuning", "server-lobby.json"));

            Assert.That(server, Is.EqualTo(client));
        }

        [Test]
        public void WithoutAFileTheShooterValuesAreUsed()
        {
            using var temp = new TempDirectory();

            Assert.That(ServerModules.TryLoadServerLobby(Runtime(temp.Path), out var lobby, out var error), Is.True, error);

            Assert.That(lobby, Is.SameAs(ShooterServerLobby.Default));
            Assert.That(_output.ToString(), Does.Contain("[Lobby] server lobby tuning default bucket prefix=shooter-server:"));
        }

        [Test]
        public void AGameReplacesThePrefixInItsData()
        {
            using var temp = new TempDirectory();
            Write(temp.Path, "{\"bucketPrefix\": \"arena-server:\"}");

            Assert.That(ServerModules.TryLoadServerLobby(Runtime(temp.Path), out var lobby, out var error), Is.True, error);

            Assert.That(ServerLobby.BucketId(lobby, "1.0"), Is.EqualTo("arena-server:1.0"));
            Assert.That(lobby.DisplayName, Is.EqualTo("shooter-server"), "a left-out item keeps the kit value");
        }

        [Test]
        public void ABrokenFileStopsTheServer()
        {
            using var temp = new TempDirectory();
            Write(temp.Path, "{\"bucketPrefix\": \"Bad Prefix\"}");

            Assert.That(ServerModules.TryLoadServerLobby(Runtime(temp.Path), out var lobby, out var error), Is.False);

            Assert.That(lobby, Is.Null);
            Assert.That(error, Does.StartWith("[Lobby] cannot read server lobby tuning: ").And.Contain("item 'bucketPrefix' is invalid"));
        }

        private static void Write(string root, string text)
        {
            var directory = Path.Combine(root, "server-data", "tuning");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "server-lobby.json"), text);
        }

        private ServerRuntime Runtime(string root)
        {
            var roots = new[] { root };
            return new ServerRuntime(_output, TextWriter.Null, _ => null, new ImmediateStopSignal(), roots, roots, () => null);
        }

        private static string RepositoryRoot()
        {
            for (var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); directory != null; directory = directory.Parent)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, ServerMapData.DataDirectoryName)))
                {
                    return directory.FullName;
                }
            }

            Assert.Fail("server-data is not above " + TestContext.CurrentContext.TestDirectory);
            return null;
        }
    }
}

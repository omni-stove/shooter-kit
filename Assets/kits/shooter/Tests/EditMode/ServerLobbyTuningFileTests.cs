using GameKit.Core;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Shooter.Tests.EditMode
{
    public sealed class ServerLobbyTuningFileTests
    {
        private GameContent _content;
        private TextAsset _asset;

        [TearDown]
        public void TearDown()
        {
            GameContent.Use(null);
            if (_asset != null)
            {
                Object.DestroyImmediate(_asset);
            }

            if (_content != null)
            {
                Object.DestroyImmediate(_content);
            }

            KitTuning.Reload();
        }

        [Test]
        public void TheBundledFileKeepsTheExistingBucket()
        {
            var asset = Resources.Load<TextAsset>(ServerLobbyJson.ResourcePath);
            Assert.That(asset, Is.Not.Null, "the kit ships Resources/Tuning/server-lobby.json");

            var spec = ServerLobbyJson.Parse(asset.text, "Resources/Tuning/server-lobby.json");

            Assert.That(spec.BucketPrefix, Is.EqualTo("shooter-server:"));
            Assert.That(spec.DisplayName, Is.EqualTo("shooter-server"));
        }

        [Test]
        public void KitTuningReadsTheBundledFile()
        {
            KitTuning.Reload();

            Assert.That(KitTuning.ServerLobbyResourcePath, Is.EqualTo("Tuning/server-lobby"));
            Assert.That(ServerLobby.BucketId(KitTuning.ServerLobby, "0.3.0"), Is.EqualTo("shooter-server:0.3.0"));
            Assert.That(KitTuning.ServerLobby.DisplayName, Is.EqualTo("shooter-server"));
        }

        [Test]
        public void TheGameContentFileIsReadBeforeResources()
        {
            _content = ScriptableObject.CreateInstance<GameContent>();
            _content.name = "Arena";
            _asset = new TextAsset("{\"bucketPrefix\": \"arena-server:\"}") { name = "server-lobby" };
            _content.ServerLobbyTuning = _asset;
            GameContent.Use(_content);
            KitTuning.Reload();

            LogAssert.Expect(LogType.Log, "[Tuning] read GameContent 'Arena' (server-lobby)");
            var spec = KitTuning.ServerLobby;

            Assert.That(spec.BucketPrefix, Is.EqualTo("arena-server:"));
            Assert.That(spec.DisplayName, Is.EqualTo("shooter-server"), "a left-out item keeps the kit value");
        }

        [Test]
        public void ABrokenFileFallsBackToTheKitValues()
        {
            _content = ScriptableObject.CreateInstance<GameContent>();
            _content.name = "Broken";
            _asset = new TextAsset("{\"bucketPrefix\": \"Bad Prefix\"}") { name = "server-lobby" };
            _content.ServerLobbyTuning = _asset;
            GameContent.Use(_content);
            KitTuning.Reload();

            LogAssert.Expect(LogType.Log, "[Tuning] read GameContent 'Broken' (server-lobby)");
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"(?s)^\[Tuning\] server lobby JSON .* item 'bucketPrefix' is invalid: .*; using the default values instead$"));
            var spec = KitTuning.ServerLobby;

            Assert.That(spec, Is.SameAs(ShooterServerLobby.Default));
        }
    }
}

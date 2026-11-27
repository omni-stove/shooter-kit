using System;
using GameKit.Net;
using NUnit.Framework;

namespace GameKit.Core.Tests
{
    public sealed class UdpTransportOptionsTests
    {
        [Test]
        public void DefaultsNameNoGenre()
        {
            var options = new UdpTransportOptions();

            Assert.That(options.ConnectionKey, Is.EqualTo("game-kit-match"));
            Assert.That(options.ProtocolMismatchReason, Is.EqualTo("game-kit:rejected:protocol-mismatch"));
        }

        [Test]
        public void GameSetsItsOwnProtocolMismatchReason()
        {
            var options = new UdpTransportOptions { ProtocolMismatchReason = "racing:rejected:protocol-mismatch" };

            Assert.That(options.ProtocolMismatchReason, Is.EqualTo("racing:rejected:protocol-mismatch"));
        }

        [TestCase(null)]
        [TestCase("")]
        public void EmptyProtocolMismatchReasonIsRefused(string reason)
        {
            var options = new UdpTransportOptions();

            Assert.Throws<ArgumentException>(() => options.ProtocolMismatchReason = reason);
            Assert.That(options.ProtocolMismatchReason, Is.EqualTo(UdpTransportOptions.DefaultProtocolMismatchReason));
        }
    }
}

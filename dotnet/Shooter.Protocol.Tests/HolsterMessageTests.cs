using System;
using GameKit.Net;
using NUnit.Framework;

namespace Shooter.Protocol.Tests
{
    public sealed class HolsterMessageTests
    {
        private readonly MessageTable _server = MatchMessages.CreateServerTable();

        [Test]
        public void TheHolsterRequestHasItsIdAndVersion()
        {
            Assert.That(MessageIds.HolsterRequest, Is.EqualTo(41));
            Assert.That(new HolsterRequestMessage().Id, Is.EqualTo(MessageIds.HolsterRequest));
            Assert.That(HolsterRequestMessage.CurrentVersion, Is.EqualTo(1));
            Assert.That((int)HolsterAction.Holster, Is.EqualTo(0));
            Assert.That((int)HolsterAction.Draw, Is.EqualTo(1));
        }

        [TestCase(HolsterAction.Holster)]
        [TestCase(HolsterAction.Draw)]
        public void ARequestRoundTripsWithItsAction(HolsterAction action)
        {
            var writer = new MessageWriter();
            writer.WriteMessage(new HolsterRequestMessage { Action = action });

            var got = (HolsterRequestMessage)_server.Read(writer.AsSegment());

            // Id (2)・版 (1)・向き (1)
            Assert.That(writer.Length, Is.EqualTo(3 + 1));
            Assert.That(got.Action, Is.EqualTo(action));
        }

        [Test]
        public void AReadRequestOverwritesTheReusedMessage()
        {
            var holster = new MessageWriter();
            holster.WriteMessage(new HolsterRequestMessage { Action = HolsterAction.Holster });
            var draw = new MessageWriter();
            draw.WriteMessage(new HolsterRequestMessage { Action = HolsterAction.Draw });

            var first = _server.Read(holster.AsSegment());
            var got = (HolsterRequestMessage)_server.Read(draw.AsSegment());

            Assert.That(got, Is.SameAs(first));
            Assert.That(got.Action, Is.EqualTo(HolsterAction.Draw));
        }

        [Test]
        public void AnUnknownActionIsRejected()
        {
            var writer = new MessageWriter();
            writer.WriteUInt16(MessageIds.HolsterRequest);
            writer.WriteByte(HolsterRequestMessage.CurrentVersion);
            writer.WriteByte((byte)Enum.GetValues<HolsterAction>().Length);

            var e = Assert.Throws<MessageReadException>(() => _server.Read(writer.AsSegment()));

            Assert.That(e.Message, Does.Contain("holster action"));
        }

        [Test]
        public void AnUnknownActionIsNotWritten()
        {
            Assert.Throws<InvalidOperationException>(
                () => new MessageWriter().WriteMessage(new HolsterRequestMessage { Action = (HolsterAction)2 }));
        }

        [Test]
        public void ARequestWithoutItsActionIsRejected()
        {
            var writer = new MessageWriter();
            writer.WriteUInt16(MessageIds.HolsterRequest);
            writer.WriteByte(HolsterRequestMessage.CurrentVersion);

            Assert.Throws<MessageReadException>(() => _server.Read(writer.AsSegment()));
        }

        [Test]
        public void TheRequestIsReadOnlyByTheServer()
        {
            var writer = new MessageWriter();
            writer.WriteMessage(new HolsterRequestMessage { Action = HolsterAction.Holster });

            var client = Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(writer.AsSegment()));

            Assert.That(client.Message, Does.Contain("message id " + MessageIds.HolsterRequest + " is not accepted here"));
            Assert.That(MessageTable.IsReserved(MessageIds.HolsterRequest), Is.False);
        }
    }
}

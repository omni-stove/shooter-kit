using GameKit.Net;
using NUnit.Framework;

namespace Shooter.Protocol.Tests
{
    public sealed class WeaponLightMessageTests
    {
        private readonly MessageTable _server = MatchMessages.CreateServerTable();

        [Test]
        public void TheWeaponLightRequestHasItsIdAndVersion()
        {
            Assert.That(MessageIds.WeaponLightRequest, Is.EqualTo(43));
            Assert.That(new WeaponLightRequestMessage().Id, Is.EqualTo(MessageIds.WeaponLightRequest));
            Assert.That(WeaponLightRequestMessage.CurrentVersion, Is.EqualTo(1));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ARequestRoundTripsWithItsWantedState(bool on)
        {
            var writer = new MessageWriter();
            writer.WriteMessage(new WeaponLightRequestMessage { On = on });

            var got = (WeaponLightRequestMessage)_server.Read(writer.AsSegment());

            // Id (2)・版 (1)・点けるか (1)
            Assert.That(writer.Length, Is.EqualTo(3 + 1));
            Assert.That(got.On, Is.EqualTo(on));
        }

        [Test]
        public void AReadRequestOverwritesTheReusedMessage()
        {
            var on = new MessageWriter();
            on.WriteMessage(new WeaponLightRequestMessage { On = true });
            var off = new MessageWriter();
            off.WriteMessage(new WeaponLightRequestMessage { On = false });

            var first = _server.Read(on.AsSegment());
            var got = (WeaponLightRequestMessage)_server.Read(off.AsSegment());

            Assert.That(got, Is.SameAs(first));
            Assert.That(got.On, Is.False);
        }

        [Test]
        public void AFlagThatIsNeitherOnNorOffIsRejected()
        {
            var writer = new MessageWriter();
            writer.WriteUInt16(MessageIds.WeaponLightRequest);
            writer.WriteByte(WeaponLightRequestMessage.CurrentVersion);
            writer.WriteByte(2);

            Assert.Throws<MessageReadException>(() => _server.Read(writer.AsSegment()));
        }

        [Test]
        public void ARequestWithoutItsFlagIsRejected()
        {
            var writer = new MessageWriter();
            writer.WriteUInt16(MessageIds.WeaponLightRequest);
            writer.WriteByte(WeaponLightRequestMessage.CurrentVersion);

            Assert.Throws<MessageReadException>(() => _server.Read(writer.AsSegment()));
        }

        [Test]
        public void TheRequestIsReadOnlyByTheServer()
        {
            var writer = new MessageWriter();
            writer.WriteMessage(new WeaponLightRequestMessage { On = true });

            var client = Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(writer.AsSegment()));

            Assert.That(client.Message, Does.Contain("message id " + MessageIds.WeaponLightRequest + " is not accepted here"));
            Assert.That(MessageTable.IsReserved(MessageIds.WeaponLightRequest), Is.False);
        }
    }
}

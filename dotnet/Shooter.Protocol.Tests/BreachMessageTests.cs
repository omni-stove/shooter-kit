using System;
using GameKit.Net;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Protocol.Tests
{
    public sealed class BreachMessageTests
    {
        private readonly MessageTable _server = MatchMessages.CreateServerTable();
        private readonly MessageTable _client = MatchMessages.CreateClientTable();

        [Test]
        public void TheMessagesHaveTheirIdsAndVersions()
        {
            Assert.That(MessageIds.BreachRequest, Is.EqualTo(42));
            Assert.That(MessageIds.DoorState, Is.EqualTo(53));
            Assert.That(new BreachRequestMessage().Id, Is.EqualTo(MessageIds.BreachRequest));
            Assert.That(new DoorStateMessage().Id, Is.EqualTo(MessageIds.DoorState));
            Assert.That(BreachRequestMessage.CurrentVersion, Is.EqualTo(1));
            Assert.That(DoorStateMessage.CurrentVersion, Is.EqualTo(1));
            Assert.That(MessageTable.IsReserved(MessageIds.BreachRequest), Is.False);
            Assert.That(MessageTable.IsReserved(MessageIds.DoorState), Is.False);
        }

        [TestCase(0, BreachMethod.Kick, false)]
        [TestCase(3, BreachMethod.Charge, false)]
        [TestCase(65535, BreachMethod.Charge, true)]
        public void ARequestRoundTrips(int door, BreachMethod method, bool cancel)
        {
            var writer = new MessageWriter();
            writer.WriteMessage(new BreachRequestMessage { DoorIndex = door, Method = method, Cancel = cancel });

            var got = (BreachRequestMessage)_server.Read(writer.AsSegment());

            // Id (2)・版 (1)・ドア (2)・方法 (1)・取りやめ (1)
            Assert.That(writer.Length, Is.EqualTo(3 + 4));
            Assert.That(got.DoorIndex, Is.EqualTo(door));
            Assert.That(got.Method, Is.EqualTo(method));
            Assert.That(got.Cancel, Is.EqualTo(cancel));
        }

        [Test]
        public void AReadRequestOverwritesTheReusedMessage()
        {
            var begin = new MessageWriter();
            begin.WriteMessage(new BreachRequestMessage { DoorIndex = 1, Method = BreachMethod.Charge });
            var cancel = new MessageWriter();
            cancel.WriteMessage(new BreachRequestMessage { DoorIndex = 2, Method = BreachMethod.Kick, Cancel = true });

            var first = _server.Read(begin.AsSegment());
            var got = (BreachRequestMessage)_server.Read(cancel.AsSegment());

            Assert.That(got, Is.SameAs(first));
            Assert.That(got.DoorIndex, Is.EqualTo(2));
            Assert.That(got.Method, Is.EqualTo(BreachMethod.Kick));
            Assert.That(got.Cancel, Is.True);
        }

        [TestCase(-1)]
        [TestCase(65536)]
        public void ARequestWithADoorOutOfRangeIsNotWritten(int door)
        {
            Assert.Throws<InvalidOperationException>(
                () => new MessageWriter().WriteMessage(new BreachRequestMessage { DoorIndex = door, Method = BreachMethod.Kick }));
        }

        [Test]
        public void ARequestWithAnUnknownMethodIsNotWritten()
        {
            Assert.Throws<InvalidOperationException>(
                () => new MessageWriter().WriteMessage(new BreachRequestMessage { DoorIndex = 0, Method = (BreachMethod)2 }));
        }

        [Test]
        public void ARequestWithAnUnknownMethodIsRejected()
        {
            var writer = new MessageWriter();
            writer.WriteUInt16(MessageIds.BreachRequest);
            writer.WriteByte(BreachRequestMessage.CurrentVersion);
            writer.WriteUInt16(0);
            writer.WriteByte((byte)Enum.GetValues<BreachMethod>().Length);
            writer.WriteBool(false);

            var e = Assert.Throws<MessageReadException>(() => _server.Read(writer.AsSegment()));

            Assert.That(e.Message, Does.Contain("breach method"));
        }

        [Test]
        public void ARequestCutShortIsRejected()
        {
            var writer = new MessageWriter();
            writer.WriteUInt16(MessageIds.BreachRequest);
            writer.WriteByte(BreachRequestMessage.CurrentVersion);
            writer.WriteUInt16(0);
            writer.WriteByte((byte)BreachMethod.Kick);

            Assert.Throws<MessageReadException>(() => _server.Read(writer.AsSegment()));
        }

        [Test]
        public void TheRequestIsReadOnlyByTheServer()
        {
            var writer = new MessageWriter();
            writer.WriteMessage(new BreachRequestMessage { DoorIndex = 0, Method = BreachMethod.Kick });

            var client = Assert.Throws<MessageReadException>(() => _client.Read(writer.AsSegment()));

            Assert.That(client.Message, Does.Contain("message id " + MessageIds.BreachRequest + " is not accepted here"));
        }

        [Test]
        public void TheDoorStateRoundTripsEveryDoorInOrder()
        {
            var state = new DoorStateMessage();
            state.Add(BreachDoorPhase.Closed, true);
            state.Add(BreachDoorPhase.Planting, false);
            state.Add(BreachDoorPhase.Armed, true);
            state.Add(BreachDoorPhase.Opened, false);
            state.Add(BreachDoorPhase.Destroyed, true);
            var writer = new MessageWriter();
            writer.WriteMessage(state);

            var got = (DoorStateMessage)_client.Read(writer.AsSegment());

            // Id (2)・版 (1)・数 (2)・ドアごとに段 (1) と向こう側 (1)
            Assert.That(writer.Length, Is.EqualTo(3 + 2 + (5 * 2)));
            Assert.That(got.DoorCount, Is.EqualTo(5));
            Assert.That(got[0].Phase, Is.EqualTo(BreachDoorPhase.Closed));
            Assert.That(got[0].FarSideFront, Is.True);
            Assert.That(got[1].Phase, Is.EqualTo(BreachDoorPhase.Planting));
            Assert.That(got[1].FarSideFront, Is.False);
            Assert.That(got[2].Phase, Is.EqualTo(BreachDoorPhase.Armed));
            Assert.That(got[3].Phase, Is.EqualTo(BreachDoorPhase.Opened));
            Assert.That(got[3].FarSideFront, Is.False);
            Assert.That(got[4].Phase, Is.EqualTo(BreachDoorPhase.Destroyed));
            Assert.That(got[4].FarSideFront, Is.True);
        }

        [Test]
        public void AMapWithoutDoorsSendsAnEmptyState()
        {
            var writer = new MessageWriter();
            writer.WriteMessage(new DoorStateMessage());

            var got = (DoorStateMessage)_client.Read(writer.AsSegment());

            Assert.That(writer.Length, Is.EqualTo(3 + 2));
            Assert.That(got.DoorCount, Is.Zero);
        }

        [Test]
        public void AReadStateReplacesTheDoorsOfTheReusedMessage()
        {
            var three = new DoorStateMessage();
            three.Add(BreachDoorPhase.Opened, true);
            three.Add(BreachDoorPhase.Opened, true);
            three.Add(BreachDoorPhase.Opened, true);
            var one = new DoorStateMessage();
            one.Add(BreachDoorPhase.Destroyed, false);
            var first = new MessageWriter();
            first.WriteMessage(three);
            var second = new MessageWriter();
            second.WriteMessage(one);

            _client.Read(first.AsSegment());
            var got = (DoorStateMessage)_client.Read(second.AsSegment());

            Assert.That(got.DoorCount, Is.EqualTo(1));
            Assert.That(got[0].Phase, Is.EqualTo(BreachDoorPhase.Destroyed));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = got[1]);
        }

        [Test]
        public void ClearingTheStateEmptiesIt()
        {
            var state = new DoorStateMessage();
            state.Add(BreachDoorPhase.Opened, true);

            state.Clear();

            Assert.That(state.DoorCount, Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = state[0]);
        }

        [Test]
        public void AnUnknownPhaseIsNotAdded()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DoorStateMessage().Add((BreachDoorPhase)5, false));
        }

        [Test]
        public void MoreDoorsThanTheLimitAreNotAdded()
        {
            var state = new DoorStateMessage();
            for (var i = 0; i < DoorStateMessage.MaxDoors; i++)
            {
                state.Add(BreachDoorPhase.Closed, false);
            }

            Assert.Throws<InvalidOperationException>(() => state.Add(BreachDoorPhase.Closed, false));
            Assert.That(state.DoorCount, Is.EqualTo(DoorStateMessage.MaxDoors));
        }

        [Test]
        public void AStateWithAnUnknownPhaseIsRejected()
        {
            var writer = new MessageWriter();
            writer.WriteUInt16(MessageIds.DoorState);
            writer.WriteByte(DoorStateMessage.CurrentVersion);
            writer.WriteUInt16(1);
            writer.WriteByte((byte)Enum.GetValues<BreachDoorPhase>().Length);
            writer.WriteBool(false);

            var e = Assert.Throws<MessageReadException>(() => _client.Read(writer.AsSegment()));

            Assert.That(e.Message, Does.Contain("door phase"));
        }

        [Test]
        public void AStateCountingMoreDoorsThanTheLimitIsRejected()
        {
            var writer = new MessageWriter();
            writer.WriteUInt16(MessageIds.DoorState);
            writer.WriteByte(DoorStateMessage.CurrentVersion);
            writer.WriteUInt16(DoorStateMessage.MaxDoors + 1);

            var e = Assert.Throws<MessageReadException>(() => _client.Read(writer.AsSegment()));

            Assert.That(e.Message, Does.Contain("door count"));
        }

        [Test]
        public void AStateMissingADoorIsRejected()
        {
            var writer = new MessageWriter();
            writer.WriteUInt16(MessageIds.DoorState);
            writer.WriteByte(DoorStateMessage.CurrentVersion);
            writer.WriteUInt16(2);
            writer.WriteByte((byte)BreachDoorPhase.Opened);
            writer.WriteBool(true);

            Assert.Throws<MessageReadException>(() => _client.Read(writer.AsSegment()));
        }

        [Test]
        public void TheStateIsReadOnlyByTheClient()
        {
            var writer = new MessageWriter();
            writer.WriteMessage(new DoorStateMessage());

            var server = Assert.Throws<MessageReadException>(() => _server.Read(writer.AsSegment()));

            Assert.That(server.Message, Does.Contain("message id " + MessageIds.DoorState + " is not accepted here"));
        }
    }
}

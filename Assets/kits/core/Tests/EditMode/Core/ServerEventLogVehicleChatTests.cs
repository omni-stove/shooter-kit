using System;
using NUnit.Framework;

namespace GameKit.Core.Tests
{
    // Unity の EditMode テストと dotnet/GameKit.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    // Chat・Vehicle の enum を使わず文字列だけで呼ぶ。Telemetry がそれらの asmdef なしで使えることをここで確かめるため
    public sealed class ServerEventLogVehicleChatTests
    {
        private const string _matchId = "m";
        private const string _puid = "0002aaaabbbbccccddddeeeeffff0000";
        private static readonly DateTime _time = new DateTime(2026, 11, 23, 9, 0, 0, 0, DateTimeKind.Utc);

        [Test]
        public void VehicleEnteredWritesTheRoleName()
        {
            var line = ServerEventLog.VehicleEntered(_time, _matchId, 1, _puid, 2, "jeep", 0, "Driver");

            Assert.That(line, Does.Contain("\"type\":\"vehicle_entered\""));
            Assert.That(line, Does.Contain("\"role\":\"Driver\""));
            Assert.That(line, Is.EqualTo(
                "{\"type\":\"vehicle_entered\",\"time\":\"2026-11-23T09:00:00.000Z\",\"matchId\":\"m\",\"playerId\":\"1\",\"productUserId\":\""
                + _puid + "\",\"vehicle\":\"2\",\"vehicleId\":\"jeep\",\"seat\":\"0\",\"role\":\"Driver\"}"));
        }

        [Test]
        public void VehicleLeftWritesTheReasonName()
        {
            var line = ServerEventLog.VehicleLeft(_time, _matchId, 1, null, 2, "jeep", 1, "Wrecked");

            Assert.That(line, Is.EqualTo(
                "{\"type\":\"vehicle_left\",\"time\":\"2026-11-23T09:00:00.000Z\",\"matchId\":\"m\",\"playerId\":\"1\","
                + "\"vehicle\":\"2\",\"vehicleId\":\"jeep\",\"seat\":\"1\",\"reason\":\"Wrecked\"}"));
        }

        [Test]
        public void ChatWritesTheChannelNameAndLength()
        {
            var line = ServerEventLog.Chat(_time, _matchId, 1, _puid, "Team", 5);

            Assert.That(line, Does.Contain("\"type\":\"chat\""));
            Assert.That(line, Does.Contain("\"channel\":\"Team\""));
            Assert.That(line, Does.Contain("\"length\":\"5\""));
        }

        [Test]
        public void ChatRefusedWritesTheReasonName()
        {
            var line = ServerEventLog.ChatRefused(_time, _matchId, 1, null, "TooSoon");

            Assert.That(line, Is.EqualTo(
                "{\"type\":\"chat_refused\",\"time\":\"2026-11-23T09:00:00.000Z\",\"matchId\":\"m\",\"playerId\":\"1\",\"reason\":\"TooSoon\"}"));
        }

        [Test]
        public void EmptyNamesAreRefused()
        {
            Assert.Throws<ArgumentException>(() => ServerEventLog.VehicleEntered(_time, _matchId, 1, null, 2, "jeep", 0, ""));
            Assert.Throws<ArgumentException>(() => ServerEventLog.VehicleEntered(_time, _matchId, 1, null, 2, "jeep", 0, null));
            Assert.Throws<ArgumentException>(() => ServerEventLog.VehicleLeft(_time, _matchId, 1, null, 2, "jeep", 0, ""));
            Assert.Throws<ArgumentException>(() => ServerEventLog.Chat(_time, _matchId, 1, null, "", 5));
            Assert.Throws<ArgumentException>(() => ServerEventLog.Chat(_time, _matchId, 1, null, null, 5));
            Assert.Throws<ArgumentException>(() => ServerEventLog.ChatRefused(_time, _matchId, 1, null, ""));
            Assert.Throws<ArgumentException>(() => ServerEventLog.ChatRefused(_time, _matchId, 1, null, null));
        }

        [Test]
        public void EveryLineReadsBackWithFlatJsonObject()
        {
            var lines = new[]
            {
                ServerEventLog.VehicleEntered(_time, _matchId, 1, null, 2, "jeep", 0, "Gunner"),
                ServerEventLog.VehicleLeft(_time, _matchId, 1, null, 2, "jeep", 0, "Request"),
                ServerEventLog.Chat(_time, _matchId, 1, null, "All", 1),
                ServerEventLog.ChatRefused(_time, _matchId, 1, null, "Empty"),
            };

            foreach (var line in lines)
            {
                Assert.That(FlatJsonObject.TryParse(line, out _, out var error), Is.True, line + ": " + error);
            }
        }
    }
}

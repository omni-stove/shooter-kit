using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace GameKit.Core.Tests
{
    // Unity の EditMode テストと dotnet/GameKit.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ServerLobbyTests
    {
        private const string _ownerId = "0123456789abcdef0123456789abcdef";

        private static LobbyInfo Lobby(string endpoint)
        {
            var attributes = endpoint == null
                ? Array.Empty<LobbyAttribute>()
                : new[] { LobbyAttribute.OfText(ServerLobby.ServerKey, endpoint) };
            return new LobbyInfo("lobby-1", _ownerId, LobbySettings.DefaultMaxPlayers, new[] { _ownerId }, attributes);
        }

        private static Dictionary<string, LobbyAttribute> ByKey(IReadOnlyList<LobbyAttribute> attributes)
        {
            var byKey = new Dictionary<string, LobbyAttribute>();
            foreach (var attribute in attributes)
            {
                byKey.Add(attribute.Key, attribute);
            }

            return byKey;
        }

        [Test]
        public void AttributesCarryTheAddressModeVersionPlayersAndVoice()
        {
            var attributes = ByKey(ServerLobby.Attributes("203.0.113.10:7777", "0.3.0", 2));

            Assert.That(attributes.Count, Is.EqualTo(5));
            Assert.That(attributes["server"].Text, Is.EqualTo("203.0.113.10:7777"));
            Assert.That(attributes["mode"].Text, Is.EqualTo("tdm"));
            Assert.That(attributes["version"].Text, Is.EqualTo("0.3.0"));
            Assert.That(attributes["players"].IsText, Is.False);
            Assert.That(attributes["players"].Number, Is.EqualTo(2));
            Assert.That(attributes["voice_enabled"].IsText, Is.False);
            Assert.That(attributes["voice_enabled"].Number, Is.EqualTo(1));
        }

        [Test]
        public void ALobbyMadeFromTheAttributesHasVoice()
        {
            var lobby = new LobbyInfo(
                "lobby-1", _ownerId, ServerLobby.MaxMembers(8), new[] { _ownerId }, ServerLobby.Attributes("203.0.113.10:7777", "0.3.0", 0));

            Assert.That(lobby.VoiceEnabled, Is.True);
        }

        [TestCase(1, 2)]
        [TestCase(8, 9)]
        [TestCase(63, 64)]
        [TestCase(64, 64)]
        public void MaxMembersAddsTheServerWithinTheEosLimit(int maxPlayers, int expected)
        {
            Assert.That(ServerLobby.MaxMembers(maxPlayers), Is.EqualTo(expected));
        }

        [Test]
        public void PlayerSlotsLeaveOutTheServerOnlyInAVoiceLobby()
        {
            var voice = new LobbyInfo(
                "lobby-1", _ownerId, 9, new[] { _ownerId }, new[] { LobbyAttribute.OfNumber(ServerLobby.VoiceEnabledKey, 1) });

            Assert.That(ServerLobby.PlayerSlots(voice), Is.EqualTo(8));
            Assert.That(ServerLobby.PlayerSlots(Lobby("203.0.113.10:7777")), Is.EqualTo(LobbySettings.DefaultMaxPlayers));
        }

        [Test]
        public void VersionMatchesTheBucket()
        {
            var attributes = ByKey(ServerLobby.Attributes("203.0.113.10:7777", " ", 0));

            Assert.That(attributes["version"].Text, Is.EqualTo("0"));
            Assert.That(ServerLobby.BucketId(ServerLobbySpec.Default, " "), Is.EqualTo("game-server:0"));
            Assert.That(ServerLobby.BucketId(ServerLobbySpec.Default, "0.3.0"), Is.EqualTo("game-server:0.3.0"));
        }

        [Test]
        public void TheBucketStartsWithTheConfiguredPrefix()
        {
            var lobby = new ServerLobbySpec("shooter-server:", "shooter-server");

            Assert.That(ServerLobby.BucketId(lobby, "0.3.0"), Is.EqualTo("shooter-server:0.3.0"));
            Assert.That(ServerLobby.BucketId(lobby, " "), Is.EqualTo("shooter-server:0"));
            Assert.That(ServerLobby.BucketId(new ServerLobbySpec("arena:"), "1.2"), Is.EqualTo("arena:1.2"));
            Assert.Throws<ArgumentNullException>(() => ServerLobby.BucketId(null, "0.3.0"));
        }

        [Test]
        public void AUdpQuickMatchConnectsToTheServerAddress()
        {
            var net = ServerLobby.ConnectOptions(Lobby("203.0.113.10:7790"));

            Assert.That(net.Mode, Is.EqualTo(NetLaunchMode.Client));
            Assert.That(net.Transport, Is.EqualTo(NetTransportKind.Udp));
            Assert.That(net.Address, Is.EqualTo("203.0.113.10"));
            Assert.That(net.Port, Is.EqualTo(7790));
            Assert.That(net.EosServerId, Is.Null);
        }

        [TestCase(null)]
        [TestCase("203.0.113.10")]
        [TestCase("0.0.0.0:7777")]
        [TestCase("server.example:7777")]
        public void AUdpQuickMatchRejectsAServerAddressThatIsNotIPv4AndPort(string endpoint)
        {
            Assert.Throws<ArgumentException>(() => ServerLobby.ConnectOptions(Lobby(endpoint)));
            Assert.Throws<ArgumentNullException>(() => ServerLobby.ConnectOptions(null));
        }

        [Test]
        public void LobbyInfoReadsThePrivateJoinCodeSkillAndVoiceAttributes()
        {
            var lobby = new LobbyInfo(
                "lobby-1",
                _ownerId,
                LobbySettings.DefaultMaxPlayers,
                new[] { _ownerId },
                new[]
                {
                    LobbyAttribute.OfNumber(ServerLobby.PrivateKey, 1),
                    LobbyAttribute.OfText(ServerLobby.JoinCodeKey, "AB12cd"),
                    LobbyAttribute.OfNumber(ServerLobby.SkillAverageKey, 1450),
                    LobbyAttribute.OfNumber(ServerLobby.VoiceEnabledKey, 1),
                });

            Assert.That(lobby.IsPrivate, Is.True);
            Assert.That(lobby.JoinCode, Is.EqualTo("AB12cd"));
            Assert.That(lobby.SkillAverage, Is.EqualTo(1450));
            Assert.That(lobby.VoiceEnabled, Is.True);
        }

        [Test]
        public void LobbyInfoTreatsMissingAttributesAsPublicWithoutCodeSkillOrVoice()
        {
            var lobby = Lobby("203.0.113.10:7777");

            Assert.That(lobby.IsPrivate, Is.False);
            Assert.That(lobby.JoinCode, Is.Null);
            Assert.That(lobby.SkillAverage, Is.Null);
            Assert.That(lobby.VoiceEnabled, Is.False);
        }

        [Test]
        public void LobbyInfoIgnoresAttributesOfTheWrongKindOrRange()
        {
            var lobby = new LobbyInfo(
                "lobby-1",
                _ownerId,
                LobbySettings.DefaultMaxPlayers,
                new[] { _ownerId },
                new[]
                {
                    LobbyAttribute.OfText(ServerLobby.PrivateKey, "1"),
                    LobbyAttribute.OfNumber(ServerLobby.JoinCodeKey, 1234),
                    LobbyAttribute.OfNumber(ServerLobby.SkillAverageKey, (long)int.MaxValue + 1),
                    LobbyAttribute.OfNumber(ServerLobby.VoiceEnabledKey, 0),
                });

            Assert.That(lobby.IsPrivate, Is.False);
            Assert.That(lobby.JoinCode, Is.Null);
            Assert.That(lobby.SkillAverage, Is.Null);
            Assert.That(lobby.VoiceEnabled, Is.False);
        }

        [Test]
        public void APublicServerLobbyDoesNotPublishThePrivateCodeOrSkillAttributes()
        {
            foreach (var match in new[] { LobbySettings.Default, new LobbySettings(joinCode: "ABC234") })
            {
                var attributes = ByKey(ServerLobby.Attributes("203.0.113.10:7777", "0.3.0", 2, match));

                Assert.That(attributes.Count, Is.EqualTo(5));
                Assert.That(attributes.ContainsKey(ServerLobby.PrivateKey), Is.False);
                Assert.That(attributes.ContainsKey(ServerLobby.JoinCodeKey), Is.False);
                Assert.That(attributes.ContainsKey(ServerLobby.SkillAverageKey), Is.False);
            }

            Assert.That(ByKey(ServerLobby.Attributes("203.0.113.10:7777", "0.3.0", 2)).ContainsKey(ServerLobby.PrivateKey), Is.False);
        }

        [Test]
        public void APrivateServerLobbyPublishesThePrivateFlagAndTheNormalizedCode()
        {
            var match = new LobbySettings(privateMatch: true, joinCode: "room_42");
            var attributes = ByKey(ServerLobby.Attributes("203.0.113.10:7777", "0.3.0", 2, match));

            Assert.That(attributes.Count, Is.EqualTo(7));
            Assert.That(attributes["server"].Text, Is.EqualTo("203.0.113.10:7777"));
            Assert.That(attributes["players"].Number, Is.EqualTo(2));
            Assert.That(attributes["private"].IsText, Is.False);
            Assert.That(attributes["private"].Number, Is.EqualTo(1));
            Assert.That(attributes["join_code"].IsText, Is.True);
            Assert.That(attributes["join_code"].Text, Is.EqualTo("ROOM_42"));
        }

        [Test]
        public void ALobbyMadeFromPrivateAttributesReadsBackAsPrivateWithTheCode()
        {
            var match = new LobbySettings(privateMatch: true, joinCode: "abc234");
            var lobby = new LobbyInfo(
                "lobby-1", _ownerId, ServerLobby.MaxMembers(8), new[] { _ownerId }, ServerLobby.Attributes("203.0.113.10:7777", "0.3.0", 0, match));

            Assert.That(lobby.IsPrivate, Is.True);
            Assert.That(lobby.JoinCode, Is.EqualTo("ABC234"));
            Assert.That(JoinCode.Matches(lobby.JoinCode, "abc 234"), Is.True);
        }

        [Test]
        public void APrivateServerLobbyWithoutACodeIsStillMarkedPrivate()
        {
            var attributes = ByKey(ServerLobby.Attributes("203.0.113.10:7777", "0.3.0", 0, new LobbySettings(privateMatch: true)));

            Assert.That(attributes.Count, Is.EqualTo(6));
            Assert.That(attributes["private"].Number, Is.EqualTo(1));
            Assert.That(attributes.ContainsKey(ServerLobby.JoinCodeKey), Is.False);
        }

        [Test]
        public void AServerLobbyWithASkillAveragePublishesItAsANumber()
        {
            var attributes = ByKey(ServerLobby.Attributes("203.0.113.10:7777", "0.3.0", 2, LobbySettings.Default, -25));

            Assert.That(attributes.Count, Is.EqualTo(6));
            Assert.That(attributes[ServerLobby.SkillAverageKey].IsText, Is.False);
            Assert.That(attributes[ServerLobby.SkillAverageKey].Number, Is.EqualTo(-25));
            Assert.That(
                ByKey(ServerLobby.Attributes("203.0.113.10:7777", "0.3.0", 2, LobbySettings.Default, null)).ContainsKey(ServerLobby.SkillAverageKey),
                Is.False);

            var lobby = new LobbyInfo(
                "lobby-1", _ownerId, ServerLobby.MaxMembers(8), new[] { _ownerId },
                ServerLobby.Attributes("203.0.113.10:7777", "0.3.0", 2, LobbySettings.Default, 1450));
            Assert.That(lobby.SkillAverage, Is.EqualTo(1450));
        }

        [Test]
        public void SkillAverageCountsOnlyThePlayersWhoSentAValue()
        {
            Assert.That(ServerLobby.SkillAverage(new int?[] { 1000, null, 1400 }), Is.EqualTo(1200));
            Assert.That(ServerLobby.SkillAverage(new int?[] { 1500 }), Is.EqualTo(1500));
            Assert.That(ServerLobby.SkillAverage(new int?[] { null, null }), Is.Null);
            Assert.That(ServerLobby.SkillAverage(System.Array.Empty<int?>()), Is.Null);
        }

        [Test]
        public void SkillAverageRoundsHalvesAwayFromZero()
        {
            Assert.That(ServerLobby.SkillAverage(new int?[] { 1000, 1001 }), Is.EqualTo(1001));
            Assert.That(ServerLobby.SkillAverage(new int?[] { 1000, 1000, 1001 }), Is.EqualTo(1000));
            Assert.That(ServerLobby.SkillAverage(new int?[] { -1, -2 }), Is.EqualTo(-2));
        }

        [Test]
        public void SkillAverageDoesNotOverflowAtTheIntLimits()
        {
            Assert.That(ServerLobby.SkillAverage(new int?[] { int.MaxValue, int.MaxValue, int.MaxValue }), Is.EqualTo(int.MaxValue));
            Assert.That(ServerLobby.SkillAverage(new int?[] { int.MinValue, int.MinValue }), Is.EqualTo(int.MinValue));
            Assert.Throws<ArgumentNullException>(() => ServerLobby.SkillAverage(null));
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            Assert.Throws<ArgumentException>(() => ServerLobby.Attributes(" ", "0.3.0", 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ServerLobby.Attributes("203.0.113.10:7777", "0.3.0", -1));
            Assert.Throws<ArgumentNullException>(() => ServerLobby.Attributes("203.0.113.10:7777", "0.3.0", 0, null));
            Assert.Throws<ArgumentException>(() => ServerLobby.Attributes(" ", "0.3.0", 0, LobbySettings.Default));
            Assert.Throws<ArgumentOutOfRangeException>(() => ServerLobby.PlayersAttribute(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => ServerLobby.MaxMembers(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ServerLobby.MaxMembers(65));
            Assert.Throws<ArgumentNullException>(() => ServerLobby.PlayerSlots(null));
        }

        [Test]
        public void TheModeGoesIntoTheAttributesAndReadsBack()
        {
            var attributes = ServerLobby.Attributes("203.0.113.10:7777", "0.3.0", 2, LobbySettings.Default, null, null, null, "gun-game");
            var lobby = new LobbyInfo("lobby-1", _ownerId, ServerLobby.MaxMembers(8), new[] { _ownerId }, attributes);

            Assert.That(ByKey(attributes)[ServerLobby.ModeKey].Text, Is.EqualTo("gun-game"));
            Assert.That(lobby.Mode, Is.EqualTo("gun-game"));
            Assert.That(
                ByKey(ServerLobby.Attributes("203.0.113.10:7777", "0.3.0", 2, LobbySettings.Default, null, null, null, null))[ServerLobby.ModeKey].Text,
                Is.EqualTo("tdm"),
                "without a mode the lobby carries the mode it always did");
            Assert.That(ServerLobby.ModeAttribute("horde").Key, Is.EqualTo(ServerLobby.ModeKey));
            Assert.That(ServerLobby.ModeAttribute("horde").Text, Is.EqualTo("horde"));
        }

        [TestCase("")]
        [TestCase("-tdm")]
        [TestCase("tdm-")]
        [TestCase("Team Deathmatch")]
        [TestCase("TDM")]
        [TestCase("gun_game")]
        [TestCase("abcdefghijklmnopqrstuvwxyz0123456")]
        public void RejectsModeKeysOutsideTheRule(string mode)
        {
            Assert.That(ServerLobby.IsModeName(mode), Is.False);
            Assert.Throws<ArgumentException>(() => ServerLobby.ModeAttribute(mode));
            Assert.Throws<ArgumentException>(
                () => ServerLobby.Attributes("203.0.113.10:7777", "0.3.0", 0, LobbySettings.Default, null, null, null, mode));
        }

        [Test]
        public void AcceptsAModeKeyAtTheLengthLimit()
        {
            Assert.That(ServerLobby.IsModeName(new string('a', ServerLobby.MaxModeLength)), Is.True);
            Assert.That(ServerLobby.IsModeName(null), Is.False);
        }
    }
}

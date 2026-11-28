using System;
using GameKit.Core;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class NetMatchMaxPlayersTests
    {
        private const string _handgunId = "b4e2a9d7-3c16-4a58-8f2e-7d0b9c6a1e45";

        private static NetMatchFile File(NetMatchSettings settings)
        {
            var handgun = new WeaponSpec(
                _handgunId, "weapon.handgun", WeaponCategory.Handgun, FireMode.Semi, 12, 48, 0.2f, 1.5f, 34, 60f,
                DamageFalloff.None);
            return new NetMatchFile(
                "TeamDeathmatch",
                GameRules.Default,
                settings,
                "DefaultLoadout",
                new LoadoutRule(new[] { new[] { WeaponCategory.Handgun } }),
                new[] { handgun },
                0.4f,
                null,
                null);
        }

        private static string Text(string match)
        {
            return "{\"match\": " + match + ", \"loadout\": {\"slots\": [[\"Handgun\"]], \"weapons\": [\"" + _handgunId + "\"]},"
                + " \"weapons\": [{\"id\": \"" + _handgunId + "\", \"category\": \"Handgun\", \"fireMode\": \"Semi\", \"magazineSize\": 12,"
                + " \"fireInterval\": 0.2, \"reloadSeconds\": 1.5, \"damage\": 34, \"range\": 60}]}";
        }

        [Test]
        public void DefaultIsEightPlayers()
        {
            Assert.That(NetMatchSettings.Default.MaxPlayers, Is.EqualTo(8));
            Assert.That(new NetMatchSettings().MaxPlayers, Is.EqualTo(NetMatchSettings.DefaultMaxPlayers));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(64)]
        public void AcceptsOneUpToTheProtocolLimit(int maxPlayers)
        {
            Assert.That(new NetMatchSettings(maxPlayers: maxPlayers).MaxPlayers, Is.EqualTo(maxPlayers));
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(65)]
        public void RejectsCountsOutsideOneTo64(int maxPlayers)
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => _ = new NetMatchSettings(maxPlayers: maxPlayers));
            Assert.That(e.ParamName, Is.EqualTo("maxPlayers"));
        }

        [TestCase(2)]
        [TestCase(1)]
        [TestCase(64)]
        public void WrittenFileReadsBackTheMaxPlayers(int maxPlayers)
        {
            var text = NetMatchFile.Write(File(new NetMatchSettings(maxPlayers: maxPlayers)));

            Assert.That(text, Does.Contain("\"" + NetMatchFile.MaxPlayersItem + "\": " + maxPlayers));
            Assert.That(NetMatchFile.Parse(text, "TeamDeathmatch.json").Settings.MaxPlayers, Is.EqualTo(maxPlayers));
        }

        [Test]
        public void FileWithoutMaxPlayersReadsTheDefault()
        {
            var text = Text("{\"scoreLimit\": 5}");

            Assert.That(NetMatchFile.Parse(text, "TeamDeathmatch.json").Settings.MaxPlayers, Is.EqualTo(8));
        }

        [TestCase(0)]
        [TestCase(65)]
        public void FileWithMaxPlayersOutOfRangeNamesTheItem(int maxPlayers)
        {
            var text = Text("{\"maxPlayers\": " + maxPlayers + "}");

            var e = Assert.Throws<ServerDataJsonException>(() => NetMatchFile.Parse(text, "bad.json"));

            Assert.That(e.Item, Is.EqualTo("match.maxPlayers"));
        }

        [Test]
        public void EveryMatchSizeFitsALobby()
        {
            Assert.That(LobbyLaunchOptions.MinMaxPlayers, Is.EqualTo(NetMatchSettings.MinMaxPlayers));
            Assert.That(NetMatchSettings.MaxMaxPlayers, Is.LessThanOrEqualTo(LobbyLaunchOptions.MaxMaxPlayers));
            Assert.That(LobbyLaunchOptions.DefaultMaxPlayers, Is.EqualTo(NetMatchSettings.DefaultMaxPlayers));
        }

        [Test]
        public void KeepsTheLobbyValuesInItsLobbySettings()
        {
            var settings = new NetMatchSettings(maxPlayers: 12, privateMatch: true, joinCode: "ABC234");

            Assert.That(settings.Lobby.MaxPlayers, Is.EqualTo(12));
            Assert.That(settings.Lobby.PrivateMatch, Is.True);
            Assert.That(settings.Lobby.JoinCode, Is.EqualTo("ABC234"));
            Assert.That(settings.MaxPlayers, Is.EqualTo(12));
            Assert.That(settings.PrivateMatch, Is.True);
            Assert.That(settings.JoinCode, Is.EqualTo("ABC234"));
        }

        [Test]
        public void WithPrivacyChangesTheLobbySettingsAndKeepsTheRest()
        {
            var settings = new NetMatchSettings(scoreLimit: 7, maxPlayers: 12).WithPrivacy(true, "ABC234");

            Assert.That(settings.Lobby.MaxPlayers, Is.EqualTo(12));
            Assert.That(settings.Lobby.PrivateMatch, Is.True);
            Assert.That(settings.Lobby.JoinCode, Is.EqualTo("ABC234"));
            Assert.That(settings.ScoreLimit, Is.EqualTo(7));
        }

        [Test]
        public void TheDefaultLobbySettingsMatchTheGameKitDefault()
        {
            var lobby = NetMatchSettings.Default.Lobby;

            Assert.That(lobby.MaxPlayers, Is.EqualTo(LobbySettings.Default.MaxPlayers));
            Assert.That(lobby.PrivateMatch, Is.EqualTo(LobbySettings.Default.PrivateMatch));
            Assert.That(lobby.JoinCode, Is.EqualTo(LobbySettings.Default.JoinCode));
            Assert.That(NetMatchSettings.DefaultMaxPlayers, Is.EqualTo(LobbySettings.DefaultMaxPlayers));
            Assert.That(NetMatchSettings.MinMaxPlayers, Is.EqualTo(LobbySettings.MinMaxPlayers));
            Assert.That(NetMatchSettings.MaxMaxPlayers, Is.EqualTo(LobbySettings.MaxMaxPlayers));
        }

        [Test]
        public void TheRegionRulesOfTheSettingsAreTheServerRegionRules()
        {
            Assert.That(ControlSettings.AutoRegion, Is.EqualTo(ServerRegion.Auto));
            Assert.That(ControlSettings.MaxRegionLength, Is.EqualTo(ServerRegion.MaxLength));
            Assert.That(ControlSettings.IsValidRegion("north-america"), Is.True);
            Assert.That(ControlSettings.IsValidRegion("North America"), Is.False);
        }
    }
}

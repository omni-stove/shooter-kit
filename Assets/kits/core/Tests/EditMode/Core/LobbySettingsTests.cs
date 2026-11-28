using System;
using NUnit.Framework;

namespace GameKit.Core.Tests
{
    // Unity の EditMode テストと dotnet/GameKit.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class LobbySettingsTests
    {
        private static readonly Type[] _movedWithTheLobby =
        {
            typeof(LobbySettings),
            typeof(ServerRegion),
            typeof(ServerLobby),
            typeof(ConnectionApproval),
            typeof(ConnectionVerdict),
            typeof(ConnectionPayload),
            typeof(DisconnectNotice),
            typeof(MenuReturnReason),
            typeof(MenuReturnKind),
            typeof(BuildVersion),
            typeof(LobbyInfo),
            typeof(LobbyLaunchOptions),
            typeof(LobbySelector),
            typeof(ServerBrowserStatus),
            typeof(ServerBrowserStage),
            typeof(ServerJoinOutcome),
            typeof(ServerListing),
            typeof(ServerListQuery),
            typeof(ServerListSort),
            typeof(NetLaunchOptions),
            typeof(NetLaunchMode),
            typeof(NetTransportKind),
        };

        [Test]
        public void TheLobbyAndConnectionTypesLiveInTheGameKitCore()
        {
            foreach (var type in _movedWithTheLobby)
            {
                Assert.That(type.Assembly.GetName().Name, Is.EqualTo("GameKit.Core"), type.Name);
                Assert.That(type.Namespace, Is.EqualTo("GameKit.Core"), type.Name);
                Assert.That(type.IsPublic, Is.True, type.Name);
            }
        }

        [Test]
        public void DefaultIsAPublicMatchOfEightPlayers()
        {
            var settings = LobbySettings.Default;

            Assert.That(settings.MaxPlayers, Is.EqualTo(8));
            Assert.That(settings.PrivateMatch, Is.False);
            Assert.That(settings.JoinCode, Is.Null);
            Assert.That(LobbySettings.MinMaxPlayers, Is.EqualTo(1));
            Assert.That(LobbySettings.MaxMaxPlayers, Is.EqualTo(64));
            Assert.That(LobbySettings.Default, Is.SameAs(settings));
        }

        [Test]
        public void KeepsTheGivenValuesAndTreatsAnEmptyCodeAsNoCode()
        {
            var settings = new LobbySettings(12, true, "ABC234");
            var empty = new LobbySettings(privateMatch: true, joinCode: string.Empty);

            Assert.That(settings.MaxPlayers, Is.EqualTo(12));
            Assert.That(settings.PrivateMatch, Is.True);
            Assert.That(settings.JoinCode, Is.EqualTo("ABC234"));
            Assert.That(empty.JoinCode, Is.Null);
        }

        [Test]
        public void WithChangesOnlyTheGivenValues()
        {
            var settings = new LobbySettings(12, true, "ABC234");

            var resized = settings.WithMaxPlayers(4);
            var opened = settings.WithPrivacy(false, null);

            Assert.That(resized.MaxPlayers, Is.EqualTo(4));
            Assert.That(resized.PrivateMatch, Is.True);
            Assert.That(resized.JoinCode, Is.EqualTo("ABC234"));
            Assert.That(opened.MaxPlayers, Is.EqualTo(12));
            Assert.That(opened.PrivateMatch, Is.False);
            Assert.That(opened.JoinCode, Is.Null);
        }

        [TestCase(0)]
        [TestCase(65)]
        public void RejectsAPlayerCountOutsideTheRangeWithTheFileItemName(int maxPlayers)
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => new LobbySettings(maxPlayers));

            Assert.That(e.ParamName, Is.EqualTo("maxPlayers"));
        }

        [TestCase("has space")]
        [TestCase("0123456789abcdefg")]
        public void RejectsAJoinCodeThatCannotBeSentWithTheFileItemName(string joinCode)
        {
            var e = Assert.Throws<ArgumentException>(() => new LobbySettings(privateMatch: true, joinCode: joinCode));

            Assert.That(e.ParamName, Is.EqualTo("joinCode"));
        }

        [TestCase("north-america", true)]
        [TestCase("eu1", true)]
        [TestCase("auto", true)]
        [TestCase("", false)]
        [TestCase(null, false)]
        [TestCase("North-America", false)]
        [TestCase("-asia", false)]
        [TestCase("asia-", false)]
        [TestCase("asia east", false)]
        [TestCase("abcdefghijabcdefghijabcdefghijabc", false)]
        public void ServerRegionTakesLowerCaseLettersDigitsAndInnerHyphens(string region, bool valid)
        {
            Assert.That(ServerRegion.IsValid(region), Is.EqualTo(valid));
        }

        [Test]
        public void ServerRegionKeepsTheNamesTheSavedSettingsUse()
        {
            Assert.That(ServerRegion.Auto, Is.EqualTo("auto"));
            Assert.That(ServerRegion.MaxLength, Is.EqualTo(32));
        }

        [Test]
        public void MaxMembersAndPrivacyComeFromTheLobbySettings()
        {
            Assert.That(ServerLobby.MaxMembers(LobbySettings.DefaultMaxPlayers), Is.EqualTo(9));
            Assert.That(ServerLobby.MaxMembers(LobbySettings.MaxMaxPlayers), Is.EqualTo(LobbyLaunchOptions.MaxMaxPlayers));
            Assert.Throws<ArgumentOutOfRangeException>(() => ServerLobby.MaxMembers(LobbySettings.MinMaxPlayers - 1));
            Assert.That(
                ConnectionApproval.Judge("0.2.0", ConnectionPayload.Encode("0.2.0", "0123456789abcdef0123456789abcdef"), false, new LobbySettings(privateMatch: true, joinCode: "ABC234")).Approved,
                Is.False);
        }
    }
}

using System;
using System.Collections.Generic;
using GameKit.Core;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ServerLobbyModesTests
    {
        [TestCase(MatchObjective.None, "tdm")]
        [TestCase(MatchObjective.Domination, "domination")]
        [TestCase(MatchObjective.Elimination, "elimination")]
        [TestCase(MatchObjective.CaptureTheFlag, "capture-the-flag")]
        [TestCase(MatchObjective.Hardpoint, "hardpoint")]
        [TestCase(MatchObjective.SearchAndDestroy, "search-and-destroy")]
        [TestCase(MatchObjective.FreeForAll, "free-for-all")]
        [TestCase(MatchObjective.KillConfirmed, "kill-confirmed")]
        [TestCase(MatchObjective.GunGame, "gun-game")]
        [TestCase(MatchObjective.Payload, "payload")]
        [TestCase(MatchObjective.BattleRoyale, "battle-royale")]
        [TestCase(MatchObjective.Extraction, "extraction")]
        [TestCase(MatchObjective.Horde, "horde")]
        [TestCase(MatchObjective.Infected, "infected")]
        [TestCase(MatchObjective.Guardian, "guardian")]
        [TestCase(MatchObjective.Execution, "execution")]
        [TestCase(MatchObjective.Juggernaut, "juggernaut")]
        [TestCase(MatchObjective.Vip, "vip")]
        public void ModeNameGivesEachObjectiveItsLobbyKey(MatchObjective objective, string expected)
        {
            Assert.That(ServerLobbyModes.ModeName(objective), Is.EqualTo(expected));
            Assert.That(ServerLobby.IsModeName(expected), Is.True);
        }

        [Test]
        public void EveryObjectiveHasItsOwnModeName()
        {
            var names = new HashSet<string>();
            // Unity の .NET には Enum.GetValues<TEnum>() が無いので、dotnet でだけ出る CA2263 を止める
#pragma warning disable CA2263
            foreach (MatchObjective objective in Enum.GetValues(typeof(MatchObjective)))
#pragma warning restore CA2263
            {
                Assert.That(names.Add(ServerLobbyModes.ModeName(objective)), Is.True, objective.ToString());
            }

            Assert.That(names.Count, Is.EqualTo(18));

            Assert.That(ServerLobbyModes.ModeName(MatchObjective.None), Is.EqualTo(ServerLobbyModes.TeamDeathmatchMode));
            Assert.Throws<ArgumentOutOfRangeException>(() => ServerLobbyModes.ModeName((MatchObjective)999));
        }

        [Test]
        public void TheTeamDeathmatchKeyIsTheKeyALobbyWithoutAModeCarries()
        {
            Assert.That(ServerLobbyModes.TeamDeathmatchMode, Is.EqualTo(ServerLobby.DefaultMode));
            Assert.That(ServerLobbyModes.TeamDeathmatchMode, Is.EqualTo("tdm"));
        }
    }
}

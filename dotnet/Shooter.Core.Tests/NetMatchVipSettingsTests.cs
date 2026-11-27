using System.IO;
using GameKit.Core;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class NetMatchVipSettingsTests
    {
        private const string _handgunId = "b4e2a9d7-3c16-4a58-8f2e-7d0b9c6a1e45";

        private static NetMatchFile File(NetMatchSettings settings, MatchObjective objective = MatchObjective.None)
        {
            var handgun = new WeaponSpec(
                _handgunId, "weapon.handgun", WeaponCategory.Handgun, FireMode.Semi, 12, 48, 0.2f, 1.5f, 34, 60f,
                DamageFalloff.None);
            return new NetMatchFile(
                objective.ToString(),
                GameRules.Default.WithObjective(objective),
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
            return "{\"rules\": {\"objective\": \"Vip\"}, \"match\": " + match
                + ", \"loadout\": {\"slots\": [[\"Handgun\"]], \"weapons\": [\"" + _handgunId + "\"]},"
                + " \"weapons\": [{\"id\": \"" + _handgunId + "\", \"category\": \"Handgun\", \"fireMode\": \"Semi\", \"magazineSize\": 12,"
                + " \"fireInterval\": 0.2, \"reloadSeconds\": 1.5, \"damage\": 34, \"range\": 60}]}";
        }

        [Test]
        public void TheDefaultIsTheCoreDefault()
        {
            Assert.That(NetMatchSettings.Default.Vip, Is.SameAs(VipSpec.Default));
            Assert.That(new NetMatchSettings(vip: null).Vip, Is.SameAs(VipSpec.Default));
        }

        [Test]
        public void WrittenValuesReadBack()
        {
            var original = new NetMatchSettings(vip: new VipSpec(250, 180f, 3, 4.5f, 0));
            var text = NetMatchFile.Write(File(original, MatchObjective.Vip));

            var read = NetMatchFile.Parse(text, "Vip.json").Settings.Vip;

            Assert.That(read.VipMaxHealth, Is.EqualTo(250));
            Assert.That(read.RoundSeconds, Is.EqualTo(180f));
            Assert.That(read.WinsToWin, Is.EqualTo(3));
            Assert.That(read.ExitHoldSeconds, Is.EqualTo(4.5f));
            Assert.That(read.SwapEveryRounds, Is.Zero, "0 keeps the sides for the whole match");
            Assert.That(read.SameAs(original.Vip), Is.True);
            Assert.That(NetMatchFile.Write(NetMatchFile.Parse(text, "Vip.json")), Is.EqualTo(text));
        }

        [Test]
        public void AVipFileListsTheSectionEvenAtTheDefaults()
        {
            var text = NetMatchFile.Write(File(NetMatchSettings.Default, MatchObjective.Vip));

            Assert.That(text, Does.Contain(
                "\"vip\": {\n      \"vipMaxHealth\": 200,\n      \"roundSeconds\": 150,\n      \"winsToWin\": 4,\n"
                + "      \"exitHoldSeconds\": 3,\n      \"swapEveryRounds\": 1\n    }\n  },"));
            Assert.That(text, Does.Not.Contain("\"" + NetMatchFile.JuggernautItem + "\""), "the other asymmetric mode keeps its defaults out");
        }

        // 節を足す前に書き出したルールのファイルを、書き出し直さずにそのまま使えるようにする
        [Test]
        public void AnotherObjectiveLeavesTheSectionOutAtTheDefaults()
        {
            var text = NetMatchFile.Write(File(NetMatchSettings.Default));

            Assert.That(text, Does.Not.Contain("\"vip\""));
            Assert.That(NetMatchFile.Parse(text, "TeamDeathmatch.json").Settings.Vip, Is.SameAs(VipSpec.Default));
        }

        // プレイリストは最初の項目のルールの match.* を使い続けるので、Vip でないルールにも変えた値は書く
        [Test]
        public void AnotherObjectiveWritesTheSectionWhenItWasChanged()
        {
            var text = NetMatchFile.Write(File(new NetMatchSettings(vip: new VipSpec(winsToWin: 6))));

            Assert.That(text, Does.Contain("\"vip\": {"));
            Assert.That(NetMatchFile.Parse(text, "TeamDeathmatch.json").Settings.Vip.WinsToWin, Is.EqualTo(6));
            Assert.That(NetMatchFile.Write(NetMatchFile.Parse(text, "TeamDeathmatch.json")), Is.EqualTo(text));
        }

        [Test]
        public void BothAsymmetricSectionsAreWrittenInOrderAfterTheOthers()
        {
            var settings = new NetMatchSettings(juggernaut: new JuggernautSpec(scoreLimit: 20), vip: new VipSpec(vipMaxHealth: 300));
            var text = NetMatchFile.Write(File(settings));

            Assert.That(text.IndexOf("\"juggernaut\": {", System.StringComparison.Ordinal), Is.GreaterThan(text.IndexOf("\"economy\": {", System.StringComparison.Ordinal)));
            Assert.That(text.IndexOf("\"vip\": {", System.StringComparison.Ordinal), Is.GreaterThan(text.IndexOf("\"juggernaut\": {", System.StringComparison.Ordinal)));
            var read = NetMatchFile.Parse(text, "TeamDeathmatch.json").Settings;
            Assert.That(read.Juggernaut.ScoreLimit, Is.EqualTo(20));
            Assert.That(read.Vip.VipMaxHealth, Is.EqualTo(300));
        }

        [Test]
        public void MissingItemsReadAsTheirDefaults()
        {
            var read = NetMatchFile.Parse(Text("{\"vip\": {\"vipMaxHealth\": 120}}"), "Vip.json").Settings.Vip;

            Assert.That(read.VipMaxHealth, Is.EqualTo(120));
            Assert.That(read.RoundSeconds, Is.EqualTo(VipSpec.DefaultRoundSeconds));
            Assert.That(read.WinsToWin, Is.EqualTo(VipSpec.DefaultWinsToWin));
            Assert.That(read.ExitHoldSeconds, Is.EqualTo(VipSpec.DefaultExitHoldSeconds));
            Assert.That(read.SwapEveryRounds, Is.EqualTo(VipSpec.DefaultSwapEveryRounds));
            Assert.That(NetMatchFile.Parse(Text("{}"), "Vip.json").Settings.Vip, Is.SameAs(VipSpec.Default));
        }

        [TestCase("{\"vip\": {\"vipMaxHealth\": 0}}", "match.vip.vipMaxHealth")]
        [TestCase("{\"vip\": {\"roundSeconds\": 0}}", "match.vip.roundSeconds")]
        [TestCase("{\"vip\": {\"winsToWin\": 0}}", "match.vip.winsToWin")]
        [TestCase("{\"vip\": {\"exitHoldSeconds\": -1}}", "match.vip.exitHoldSeconds")]
        [TestCase("{\"vip\": {\"exitHoldSeconds\": 150}}", "match.vip.exitHoldSeconds")]
        [TestCase("{\"vip\": {\"swapEveryRounds\": -1}}", "match.vip.swapEveryRounds")]
        [TestCase("{\"vip\": {\"vipCount\": 1}}", "match.vip.vipCount")]
        [TestCase("{\"vip\": 1}", "match.vip")]
        public void ABadValueNamesItsItem(string match, string item)
        {
            var e = Assert.Throws<ServerDataJsonException>(() => NetMatchFile.Parse(Text(match), "Vip.json"));

            Assert.That(e.Item, Is.EqualTo(item));
            Assert.That(e.Message, Does.Contain("Vip.json"));
        }

        [Test]
        public void WithPrivacyKeepsTheVipSettings()
        {
            var original = new NetMatchSettings(vip: new VipSpec(vipMaxHealth: 150));

            Assert.That(original.WithPrivacy(true, "ABC234").Vip, Is.SameAs(original.Vip));
        }

        [Test]
        public void TheCommittedVipRulesReadTheirObjectiveAndTuning()
        {
            var path = Path.Combine(NetMatchFileTests.ServerDataRoot(), "rules", "Vip.json");

            var file = NetMatchFile.Parse(System.IO.File.ReadAllText(path), path);

            Assert.That(file.Name, Is.EqualTo("Vip"));
            Assert.That(file.Rules.Objective, Is.EqualTo(MatchObjective.Vip));
            Assert.That(file.Rules.RespawnMode, Is.EqualTo(RespawnMode.None));
            Assert.That(file.Settings.Vip.SameAs(VipSpec.Default), Is.True);
            Assert.That(file.Settings.Juggernaut, Is.SameAs(JuggernautSpec.Default));
        }

        [Test]
        public void TheOtherCommittedRulesLeaveTheSectionOut()
        {
            foreach (var path in Directory.GetFiles(Path.Combine(NetMatchFileTests.ServerDataRoot(), "rules"), "*" + NetMatchFile.Extension))
            {
                var file = NetMatchFile.Parse(System.IO.File.ReadAllText(path), path);
                if (file.Rules.Objective == MatchObjective.Vip)
                {
                    continue;
                }

                Assert.That(System.IO.File.ReadAllText(path), Does.Not.Contain("\"" + NetMatchFile.VipItem + "\""), path);
                Assert.That(file.Settings.Vip, Is.SameAs(VipSpec.Default), path);
            }
        }
    }
}

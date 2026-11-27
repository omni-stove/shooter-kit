using System.IO;
using GameKit.Core;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class NetMatchJuggernautSettingsTests
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
            return "{\"rules\": {\"objective\": \"Juggernaut\"}, \"match\": " + match
                + ", \"loadout\": {\"slots\": [[\"Handgun\"]], \"weapons\": [\"" + _handgunId + "\"]},"
                + " \"weapons\": [{\"id\": \"" + _handgunId + "\", \"category\": \"Handgun\", \"fireMode\": \"Semi\", \"magazineSize\": 12,"
                + " \"fireInterval\": 0.2, \"reloadSeconds\": 1.5, \"damage\": 34, \"range\": 60}]}";
        }

        [Test]
        public void TheDefaultIsTheCoreDefault()
        {
            Assert.That(NetMatchSettings.Default.Juggernaut, Is.SameAs(JuggernautSpec.Default));
            Assert.That(new NetMatchSettings(juggernaut: null).Juggernaut, Is.SameAs(JuggernautSpec.Default));
        }

        [Test]
        public void WrittenValuesReadBack()
        {
            var original = new NetMatchSettings(juggernaut: new JuggernautSpec(400, 1.25f, 2, 5, 30, 900f, 12f));
            var text = NetMatchFile.Write(File(original, MatchObjective.Juggernaut));

            var read = NetMatchFile.Parse(text, "Juggernaut.json").Settings.Juggernaut;

            Assert.That(read.JuggernautMaxHealth, Is.EqualTo(400));
            Assert.That(read.JuggernautSpeedMultiplier, Is.EqualTo(1.25f));
            Assert.That(read.ScorePerKillAsJuggernaut, Is.EqualTo(2));
            Assert.That(read.ScorePerJuggernautKill, Is.EqualTo(5));
            Assert.That(read.ScoreLimit, Is.EqualTo(30));
            Assert.That(read.TimeLimitSeconds, Is.EqualTo(900f));
            Assert.That(read.StartDelaySeconds, Is.EqualTo(12f));
            Assert.That(read.SameAs(original.Juggernaut), Is.True);
            Assert.That(NetMatchFile.Write(NetMatchFile.Parse(text, "Juggernaut.json")), Is.EqualTo(text));
        }

        [Test]
        public void AJuggernautFileListsTheSectionEvenAtTheDefaults()
        {
            var text = NetMatchFile.Write(File(NetMatchSettings.Default, MatchObjective.Juggernaut));

            Assert.That(text, Does.Contain(
                "\"juggernaut\": {\n      \"juggernautMaxHealth\": 300,\n      \"juggernautSpeedMultiplier\": 1.1,\n"
                + "      \"scorePerKillAsJuggernaut\": 1,\n      \"scorePerJuggernautKill\": 3,\n      \"scoreLimit\": 15,\n"
                + "      \"timeLimitSeconds\": 600,\n      \"startDelaySeconds\": 5\n    }\n  },"));
            Assert.That(text, Does.Not.Contain("\"" + NetMatchFile.VipItem + "\""), "the other asymmetric mode keeps its defaults out");
        }

        // 節を足す前に書き出したルールのファイルを、書き出し直さずにそのまま使えるようにする
        [Test]
        public void AnotherObjectiveLeavesTheSectionOutAtTheDefaults()
        {
            var text = NetMatchFile.Write(File(NetMatchSettings.Default));

            Assert.That(text, Does.Not.Contain("\"juggernaut\""));
            Assert.That(NetMatchFile.Parse(text, "TeamDeathmatch.json").Settings.Juggernaut, Is.SameAs(JuggernautSpec.Default));
        }

        // プレイリストは最初の項目のルールの match.* を使い続けるので、Juggernaut でないルールにも変えた値は書く
        [Test]
        public void AnotherObjectiveWritesTheSectionWhenItWasChanged()
        {
            var text = NetMatchFile.Write(File(new NetMatchSettings(juggernaut: new JuggernautSpec(scoreLimit: 25))));

            Assert.That(text, Does.Contain("\"juggernaut\": {"));
            Assert.That(NetMatchFile.Parse(text, "TeamDeathmatch.json").Settings.Juggernaut.ScoreLimit, Is.EqualTo(25));
            Assert.That(NetMatchFile.Write(NetMatchFile.Parse(text, "TeamDeathmatch.json")), Is.EqualTo(text));
        }

        [Test]
        public void MissingItemsReadAsTheirDefaults()
        {
            var read = NetMatchFile.Parse(Text("{\"juggernaut\": {\"juggernautMaxHealth\": 500}}"), "Juggernaut.json").Settings.Juggernaut;

            Assert.That(read.JuggernautMaxHealth, Is.EqualTo(500));
            Assert.That(read.JuggernautSpeedMultiplier, Is.EqualTo(JuggernautSpec.DefaultJuggernautSpeedMultiplier));
            Assert.That(read.ScorePerKillAsJuggernaut, Is.EqualTo(JuggernautSpec.DefaultScorePerKillAsJuggernaut));
            Assert.That(read.ScorePerJuggernautKill, Is.EqualTo(JuggernautSpec.DefaultScorePerJuggernautKill));
            Assert.That(read.ScoreLimit, Is.EqualTo(JuggernautSpec.DefaultScoreLimit));
            Assert.That(read.TimeLimitSeconds, Is.EqualTo(JuggernautSpec.DefaultTimeLimitSeconds));
            Assert.That(read.StartDelaySeconds, Is.EqualTo(JuggernautSpec.DefaultStartDelaySeconds));
            Assert.That(NetMatchFile.Parse(Text("{}"), "Juggernaut.json").Settings.Juggernaut, Is.SameAs(JuggernautSpec.Default));
        }

        [TestCase("{\"juggernaut\": {\"juggernautMaxHealth\": 0}}", "match.juggernaut.juggernautMaxHealth")]
        [TestCase("{\"juggernaut\": {\"juggernautSpeedMultiplier\": 0}}", "match.juggernaut.juggernautSpeedMultiplier")]
        [TestCase("{\"juggernaut\": {\"scorePerKillAsJuggernaut\": -1}}", "match.juggernaut.scorePerKillAsJuggernaut")]
        [TestCase("{\"juggernaut\": {\"scorePerJuggernautKill\": -1}}", "match.juggernaut.scorePerJuggernautKill")]
        [TestCase("{\"juggernaut\": {\"scorePerKillAsJuggernaut\": 0, \"scorePerJuggernautKill\": 0}}", "match.juggernaut.scorePerJuggernautKill")]
        [TestCase("{\"juggernaut\": {\"scoreLimit\": 0}}", "match.juggernaut.scoreLimit")]
        [TestCase("{\"juggernaut\": {\"timeLimitSeconds\": 0}}", "match.juggernaut.timeLimitSeconds")]
        [TestCase("{\"juggernaut\": {\"startDelaySeconds\": -1}}", "match.juggernaut.startDelaySeconds")]
        [TestCase("{\"juggernaut\": {\"startDelaySeconds\": 600}}", "match.juggernaut.startDelaySeconds")]
        [TestCase("{\"juggernaut\": {\"juggernautCount\": 1}}", "match.juggernaut.juggernautCount")]
        [TestCase("{\"juggernaut\": 1}", "match.juggernaut")]
        public void ABadValueNamesItsItem(string match, string item)
        {
            var e = Assert.Throws<ServerDataJsonException>(() => NetMatchFile.Parse(Text(match), "Juggernaut.json"));

            Assert.That(e.Item, Is.EqualTo(item));
            Assert.That(e.Message, Does.Contain("Juggernaut.json"));
        }

        [Test]
        public void WithPrivacyKeepsTheJuggernautSettings()
        {
            var original = new NetMatchSettings(juggernaut: new JuggernautSpec(juggernautMaxHealth: 250));

            Assert.That(original.WithPrivacy(true, "ABC234").Juggernaut, Is.SameAs(original.Juggernaut));
        }

        [Test]
        public void TheCommittedJuggernautRulesReadTheirObjectiveAndTuning()
        {
            var path = Path.Combine(NetMatchFileTests.ServerDataRoot(), "rules", "Juggernaut.json");

            var file = NetMatchFile.Parse(System.IO.File.ReadAllText(path), path);

            Assert.That(file.Name, Is.EqualTo("Juggernaut"));
            Assert.That(file.Rules.Objective, Is.EqualTo(MatchObjective.Juggernaut));
            Assert.That(file.Settings.Juggernaut.SameAs(JuggernautSpec.Default), Is.True);
            Assert.That(file.Settings.Vip, Is.SameAs(VipSpec.Default));
        }

        [Test]
        public void TheOtherCommittedRulesLeaveTheSectionOut()
        {
            foreach (var path in Directory.GetFiles(Path.Combine(NetMatchFileTests.ServerDataRoot(), "rules"), "*" + NetMatchFile.Extension))
            {
                var file = NetMatchFile.Parse(System.IO.File.ReadAllText(path), path);
                if (file.Rules.Objective == MatchObjective.Juggernaut)
                {
                    continue;
                }

                Assert.That(System.IO.File.ReadAllText(path), Does.Not.Contain("\"" + NetMatchFile.JuggernautItem + "\""), path);
                Assert.That(file.Settings.Juggernaut, Is.SameAs(JuggernautSpec.Default), path);
            }
        }
    }
}

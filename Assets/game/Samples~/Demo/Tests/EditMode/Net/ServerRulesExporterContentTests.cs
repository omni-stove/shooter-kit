using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Net.Tests
{
    public sealed class ServerRulesExporterContentTests
    {
        private readonly List<Object> _created = new List<Object>();
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            // プロジェクトの Resources/ShooterKit に置かれた入口の欄に左右されないよう、空の入口に替えておく。
            GameContent.Use(Create<GameContent>("Empty"));
            _directory = Path.Combine(Path.GetTempPath(), "ServerRulesExporterContentTests-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            GameContent.Use(null);
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        [Test]
        public void ThePingOfGameContentIsWrittenInsteadOfResources()
        {
            const string json = "{ \"lifetimeSeconds\": 12 }";
            var content = Create<GameContent>("Game");
            content.PingTuning = Text("GamePing", json);
            GameContent.Use(content);

            var output = ServerRulesExporter.ExportTuningFile(
                _directory, ServerRulesExporter.PingFileName, c => c.PingTuning, KitTuning.PingResourcePath, TuningJson.ParsePing);

            Assert.That(output, Is.EqualTo(Path.Combine(_directory, "ping.json")));
            Assert.That(File.ReadAllText(output), Is.EqualTo(json));
            Assert.That(TuningJson.ParsePing(File.ReadAllText(output), output).LifetimeSeconds, Is.EqualTo(12f), "Resources/Tuning/ping.json has 6");
        }

        [Test]
        public void WithoutGameContentThePingOfResourcesIsWritten()
        {
            var output = ServerRulesExporter.ExportTuningFile(
                _directory, ServerRulesExporter.PingFileName, c => c.PingTuning, KitTuning.PingResourcePath, TuningJson.ParsePing);

            var resources = Resources.Load<TextAsset>(KitTuning.PingResourcePath);
            Assert.That(File.ReadAllText(output), Is.EqualTo(resources.text));
            Assert.That(TuningJson.ParsePing(File.ReadAllText(output), output).LifetimeSeconds, Is.EqualTo(6f));
        }

        [Test]
        public void TheKnockbackAndBotsOfGameContentAreWrittenWithTheOtherTuning()
        {
            const string knockback = "{ \"explosionSpeed\": 11 }";
            const string bots = "{ \"sightRange\": 30 }";
            var content = Create<GameContent>("Game");
            content.KnockbackTuning = Text("GameKnockback", knockback);
            content.BotsTuning = Text("GameBots", bots);
            GameContent.Use(content);

            var written = ServerRulesExporter.ExportTuning(_directory);

            Assert.That(written, Is.EqualTo(new[]
            {
                Path.Combine(_directory, "ping.json"),
                Path.Combine(_directory, "grenade.json"),
                Path.Combine(_directory, "inventory.json"),
                Path.Combine(_directory, "knockback.json"),
                Path.Combine(_directory, "bots.json"),
                Path.Combine(_directory, "assist.json"),
                Path.Combine(_directory, "loot.json"),
                Path.Combine(_directory, "supply-drops.json"),
                Path.Combine(_directory, "redeploy.json"),
                Path.Combine(_directory, "melee.json"),
                Path.Combine(_directory, "streaks.json"),
                Path.Combine(_directory, "extraction.json"),
                Path.Combine(_directory, "stash.json"),
                Path.Combine(_directory, "stagger.json"),
                Path.Combine(_directory, "suppression.json"),
                Path.Combine(_directory, "emotes.json"),
                Path.Combine(_directory, "holster.json"),
            }));
            Assert.That(File.ReadAllText(Path.Combine(_directory, "knockback.json")), Is.EqualTo(knockback));
            Assert.That(File.ReadAllText(Path.Combine(_directory, "bots.json")), Is.EqualTo(bots));
            Assert.That(TuningJson.ParseKnockback(knockback, "knockback.json").Explosion.Speed, Is.EqualTo(11f));
            Assert.That(TuningJson.ParseBots(bots, "bots.json").Brain.SightRange, Is.EqualTo(30f));
        }

        [Test]
        public void WithoutGameContentTheKnockbackOfResourcesIsWritten()
        {
            ServerRulesExporter.ExportTuning(_directory);

            var resources = Resources.Load<TextAsset>(KitTuning.KnockbackResourcePath);
            Assert.That(File.ReadAllText(Path.Combine(_directory, "knockback.json")), Is.EqualTo(resources.text));
        }

        [Test]
        public void TheAssistOfGameContentIsWrittenAndWithoutItTheResourcesOne()
        {
            ServerRulesExporter.ExportTuning(_directory);
            var resources = Resources.Load<TextAsset>(KitTuning.AssistResourcePath);
            Assert.That(File.ReadAllText(Path.Combine(_directory, "assist.json")), Is.EqualTo(resources.text));

            const string assist = "{ \"windowSeconds\": 4 }";
            var content = Create<GameContent>("Game");
            content.AssistTuning = Text("GameAssist", assist);
            GameContent.Use(content);

            ServerRulesExporter.ExportTuning(_directory);

            Assert.That(File.ReadAllText(Path.Combine(_directory, "assist.json")), Is.EqualTo(assist));
            Assert.That(AssistJson.Parse(assist, "assist.json").WindowSeconds, Is.EqualTo(4f));
        }

        [Test]
        public void WithoutGameContentTheBotsOfResourcesIsWritten()
        {
            ServerRulesExporter.ExportTuning(_directory);

            var resources = Resources.Load<TextAsset>(KitTuning.BotsResourcePath);
            Assert.That(File.ReadAllText(Path.Combine(_directory, "bots.json")), Is.EqualTo(resources.text));
        }

        [TestCase("melee.json", MeleeActionInstaller.TuningResourcePath)]
        [TestCase("streaks.json", StreakJson.ResourcePath)]
        [TestCase("extraction.json", ExtractionJson.ResourcePath)]
        [TestCase("stash.json", StashJson.ResourcePath)]
        [TestCase("stagger.json", StaggerActionInstaller.TuningResourcePath)]
        [TestCase("suppression.json", KitTuning.SuppressionResourcePath)]
        [TestCase("holster.json", KitTuning.HolsterResourcePath)]
        public void WithoutGameContentTheServerReadTuningOfResourcesIsWritten(string fileName, string resourcePath)
        {
            ServerRulesExporter.ExportTuning(_directory);

            var resources = Resources.Load<TextAsset>(resourcePath);
            Assert.That(resources, Is.Not.Null, resourcePath);
            Assert.That(File.ReadAllText(Path.Combine(_directory, fileName)), Is.EqualTo(resources.text));
        }

        // エモートはゲームが用意するのでキットの Resources に無く、入口の欄が無ければ空の一覧 (既定) のまま渡す
        [Test]
        public void TheEmotesOfGameContentAreWrittenForTheServer()
        {
            ServerRulesExporter.ExportTuning(_directory);
            var output = Path.Combine(_directory, "emotes.json");
            var expected = Resources.Load<TextAsset>(EmoteJson.ResourcePath);
            Assert.That(File.ReadAllText(output), Is.EqualTo(expected != null ? expected.text : ServerRulesExporter.AllDefaultsJson));

            const string emotes = "{ \"emotes\": [ { \"id\": \"wave\", \"durationSeconds\": 2 } ] }";
            var content = Create<GameContent>("Game");
            content.EmotesTuning = Text("GameEmotes", emotes);
            GameContent.Use(content);

            ServerRulesExporter.ExportTuning(_directory);

            Assert.That(File.ReadAllText(output), Is.EqualTo(emotes));
            Assert.That(EmoteJson.Parse(emotes, "emotes.json")[0].DurationSeconds, Is.EqualTo(2f));
        }

        [Test]
        public void TheHolsterOfGameContentIsWrittenForTheServer()
        {
            const string holster = "{ \"armedPhases\": [\"Drawn\"] }";
            var content = Create<GameContent>("Game");
            content.HolsterTuning = Text("GameHolster", holster);
            GameContent.Use(content);

            ServerRulesExporter.ExportTuning(_directory);

            Assert.That(File.ReadAllText(Path.Combine(_directory, "holster.json")), Is.EqualTo(holster));
            Assert.That(HolsterJson.Parse(holster, "holster.json").IsArmedPhase(HolsterPhase.Drawing), Is.False);
        }

        [Test]
        public void AZoneWithoutAColliderIsWrittenAsTheBoxItBuildsWhenPlayed()
        {
            var zone = Zone("Shop", new Vector3(10f, 0f, 5f));
            zone.Configure(new WeaponFreeZoneSpec(WeaponFreeZoneReaction.BlockDraw));

            var zones = ServerRulesExporter.ToWeaponFreeZones("Test.unity", new[] { zone });

            Assert.That(zones.Zones, Has.Count.EqualTo(1));
            var written = zones.Zones[0];
            Assert.That(written.Id, Is.EqualTo("Shop"));
            Assert.That(written.Reaction, Is.EqualTo(WeaponFreeZoneReaction.BlockDraw));
            AssertNear(written.Min, 6f, 0f, 1f);
            AssertNear(written.Max, 14f, 3f, 9f);
        }

        [Test]
        public void AZoneWithABoxColliderIsWrittenAsThatBoxInTheWorld()
        {
            var zone = Zone("Gate", Vector3.zero);
            zone.transform.localScale = new Vector3(2f, 1f, 1f);
            var box = zone.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1f, 0f);
            box.size = new Vector3(2f, 2f, 2f);

            var written = ServerRulesExporter.ToWeaponFreeZones("Test.unity", new[] { zone }).Zones[0];

            Assert.That(written.Reaction, Is.EqualTo(WeaponFreeZoneSpec.DefaultReaction));
            Assert.That(written.Spec.HostileAfterSeconds, Is.EqualTo(WeaponFreeZoneSpec.DefaultHostileAfterSeconds));
            AssertNear(written.Min, -2f, 0f, -1f);
            AssertNear(written.Max, 2f, 2f, 1f);
        }

        [Test]
        public void ADisabledZoneIsLeftOutAndTheOthersKeepTheirOrder()
        {
            var first = Zone("First", Vector3.zero);
            var disabled = Zone("Off", new Vector3(20f, 0f, 0f));
            disabled.enabled = false;
            var last = Zone("Last", new Vector3(-20f, 0f, 0f));

            var zones = ServerRulesExporter.ToWeaponFreeZones("Test.unity", new[] { first, disabled, last });

            Assert.That(zones.Zones, Has.Count.EqualTo(2));
            Assert.That(zones.Zones[0].Id, Is.EqualTo("First"));
            Assert.That(zones.Zones[1].Id, Is.EqualTo("Last"));
        }

        [Test]
        public void TwoZonesWithTheSameIdAreRefused()
        {
            var a = Zone("Shop", Vector3.zero);
            var b = Zone("Shop", new Vector3(20f, 0f, 0f));

            var e = Assert.Throws<InvalidOperationException>(() => ServerRulesExporter.ToWeaponFreeZones("Test.unity", new[] { a, b }));

            Assert.That(e.Message, Does.Contain("share the zone id 'Shop'").And.Contain("Test.unity"));
        }

        [Test]
        public void ARotatedZoneIsWrittenAsTheBoxAroundItWithAWarning()
        {
            var zone = Zone("Turned", Vector3.zero);
            zone.Size = new Vector3(2f, 2f, 2f);
            zone.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            LogAssert.Expect(LogType.Warning, new Regex("WeaponFreeZone 'Turned' in Test.unity is rotated"));

            var written = ServerRulesExporter.ToWeaponFreeZones("Test.unity", new[] { zone }).Zones[0];

            var reach = Mathf.Sqrt(2f);
            AssertNear(written.Min, -reach, 0f, -reach);
            AssertNear(written.Max, reach, 2f, reach);
        }

        [Test]
        public void WrittenZonesReadBackForTheServer()
        {
            var zone = Zone("Shop", new Vector3(10f, 0f, 5f));
            zone.Configure(new WeaponFreeZoneSpec(WeaponFreeZoneReaction.AutoHolster));

            var text = NetMapWeaponFreeZones.Write(ServerRulesExporter.ToWeaponFreeZones("Test.unity", new[] { zone }));
            var read = NetMapWeaponFreeZones.Parse(text, "Test" + NetMapWeaponFreeZones.Extension);

            Assert.That(read.Zones[0].Id, Is.EqualTo("Shop"));
            Assert.That(read.Zones[0].Reaction, Is.EqualTo(WeaponFreeZoneReaction.AutoHolster));
            Assert.That(read.Zones[0].Contains(new System.Numerics.Vector3(10f, 1f, 5f)), Is.True);
            Assert.That(ServerRulesExporter.ToWeaponFreeZones("Test.unity", Array.Empty<WeaponFreeZone>()).Zones, Is.Empty);
        }

        [Test]
        public void TuningInNeitherGameContentNorResourcesIsWrittenAsTheDefaults()
        {
            const string missingPath = "Tuning/ServerRulesExporterContentTests-missing";
            Assert.That(Resources.Load<TextAsset>(missingPath), Is.Null);

            var output = ServerRulesExporter.ExportTuningFile(
                _directory, ServerRulesExporter.BotsFileName, c => c.BotsTuning, missingPath, TuningJson.ParseBots);

            Assert.That(File.ReadAllText(output), Is.EqualTo("{}\n"));
            var spec = TuningJson.ParseBots(File.ReadAllText(output), output);
            Assert.That(spec.Brain.SightRange, Is.EqualTo(NetBotSpec.DefaultSightRange));
            Assert.That(spec.StuckProgress, Is.EqualTo(NetBotSpec.DefaultStuckProgress));
        }

        [Test]
        public void BrokenGameContentTuningThrowsAndKeepsTheFileWrittenBefore()
        {
            Directory.CreateDirectory(_directory);
            var output = Path.Combine(_directory, "ping.json");
            File.WriteAllText(output, "{ \"maxMarkers\": 4 }");
            var content = Create<GameContent>("Broken");
            content.PingTuning = Text("BrokenPing", "{ \"maxMarkers\": 0 }");
            GameContent.Use(content);

            var e = Assert.Throws<TuningJsonException>(() => ServerRulesExporter.ExportTuningFile(
                _directory, ServerRulesExporter.PingFileName, c => c.PingTuning, KitTuning.PingResourcePath, TuningJson.ParsePing));

            Assert.That(e.Message, Does.Contain("GameContent 'Broken' (BrokenPing)").And.Contain("maxMarkers"));
            Assert.That(File.ReadAllText(output), Is.EqualTo("{ \"maxMarkers\": 4 }"));
        }

        [Test]
        public void TheRulesOfGameContentComeFirstAndThenTheListedRules()
        {
            var game = Create<GameRulesDefinition>("Game");
            var teamDeathmatch = Create<GameRulesDefinition>("TeamDeathmatch");
            var domination = Create<GameRulesDefinition>("Domination");
            var content = Create<GameContent>("Game");
            content.Rules = game;
            GameContent.Use(content);
            var match = Create<NetMatchDefinition>("NetMatch");
            match.SetRules(new[] { teamDeathmatch, null, game, domination, teamDeathmatch });

            var rules = ServerRulesExporter.RulesToExport(match);

            Assert.That(rules, Is.EqualTo(new[] { game, teamDeathmatch, domination }));
        }

        [Test]
        public void WithoutGameContentRulesOnlyTheListedRulesAreExported()
        {
            var teamDeathmatch = Create<GameRulesDefinition>("TeamDeathmatch");
            var domination = Create<GameRulesDefinition>("Domination");
            var match = Create<NetMatchDefinition>("NetMatch");
            match.SetRules(new[] { teamDeathmatch, domination });

            Assert.That(ServerRulesExporter.RulesToExport(match), Is.EqualTo(new[] { teamDeathmatch, domination }));
        }

        [Test]
        public void ANewNetMatchListsNoRules()
        {
            var match = Create<NetMatchDefinition>("NetMatch");

            Assert.That(match.Rules, Is.Empty);
            Assert.That(ServerRulesExporter.RulesToExport(match), Is.Empty);
        }

        [Test]
        public void TheLoadoutAndCatalogOfGameContentAreChosenBeforeTheNetMatch()
        {
            var match = Create<NetMatchDefinition>("NetMatch");
            match.Loadout = Create<LoadoutDefinition>("MatchLoadout");
            match.Catalog = Create<WeaponCatalogAsset>("MatchCatalog");
            var content = Create<GameContent>("Game");
            content.Loadout = Create<LoadoutDefinition>("GameLoadout");
            content.WeaponCatalog = Create<WeaponCatalogAsset>("GameCatalog");
            GameContent.Use(content);

            Assert.That(NetMatchDefinition.PickLoadout(match), Is.SameAs(content.Loadout));
            Assert.That(NetMatchDefinition.PickCatalog(match), Is.SameAs(content.WeaponCatalog));
            Assert.That(NetMatchDefinition.PickLoadout(null), Is.SameAs(content.Loadout), "the client has no net match");
            Assert.That(NetMatchDefinition.PickCatalog(null), Is.SameAs(content.WeaponCatalog), "the client has no net match");
        }

        [Test]
        public void WithoutGameContentTheLoadoutAndCatalogOfTheNetMatchAreChosen()
        {
            var match = Create<NetMatchDefinition>("NetMatch");
            match.Loadout = Create<LoadoutDefinition>("MatchLoadout");
            match.Catalog = Create<WeaponCatalogAsset>("MatchCatalog");

            Assert.That(NetMatchDefinition.PickLoadout(match), Is.SameAs(match.Loadout));
            Assert.That(NetMatchDefinition.PickCatalog(match), Is.SameAs(match.Catalog));
            Assert.That(NetMatchDefinition.PickLoadout(null), Is.Null);
            Assert.That(NetMatchDefinition.PickCatalog(null), Is.Null);
        }

        [Test]
        public void WithoutAnyLoadoutTheNetMatchCannotCreateOne()
        {
            var match = Create<NetMatchDefinition>("NetMatch");

            Assert.That(match.TryCreateLoadout(out var loadout, out var error), Is.False);
            Assert.That(loadout, Is.Null);
            Assert.That(error, Is.EqualTo("neither GameContent nor net match 'NetMatch' has a loadout"));
        }

        private T Create<T>(string assetName)
            where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            asset.name = assetName;
            _created.Add(asset);
            return asset;
        }

        private TextAsset Text(string assetName, string text)
        {
            var asset = new TextAsset(text) { name = assetName };
            _created.Add(asset);
            return asset;
        }

        // Edit Mode では Awake が走らないので、Collider も印もまだ無いシーンのゾーンと同じになる
        private WeaponFreeZone Zone(string zoneId, Vector3 position)
        {
            var go = new GameObject(zoneId);
            _created.Add(go);
            go.transform.position = position;
            var zone = go.AddComponent<WeaponFreeZone>();
            zone.ZoneId = zoneId;
            return zone;
        }

        private static void AssertNear(System.Numerics.Vector3 actual, float x, float y, float z)
        {
            Assert.That(actual.X, Is.EqualTo(x).Within(1e-4f), "x");
            Assert.That(actual.Y, Is.EqualTo(y).Within(1e-4f), "y");
            Assert.That(actual.Z, Is.EqualTo(z).Within(1e-4f), "z");
        }
    }
}

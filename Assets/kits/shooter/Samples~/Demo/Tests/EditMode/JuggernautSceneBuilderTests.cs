using System.Collections.Generic;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    // シーンを組んで NavMesh を焼く処理は開いているシーンを置き換える (NewScene Single) ので、
    // -executeMethod Shooter.Editor.JuggernautSceneBuilder.Build で確かめる。
    public sealed class JuggernautSceneBuilderTests
    {
        [Test]
        public void TheJuggernautSceneIsWrittenUnderItsRulesName()
        {
            var builder = new JuggernautSceneBuilder();

            Assert.That(builder.ScenePath, Is.EqualTo(DemoSample.Root + "/Scenes/Juggernaut.unity"));
            Assert.That(builder.RulesPath, Is.EqualTo(GameRulesAssetBuilder.Folder + "/Juggernaut.asset"));
            Assert.That(builder.NavMeshDataPath, Is.EqualTo(DemoSample.Root + "/Scenes/Juggernaut/NavMesh-Floor.asset"));
            Assert.That(builder.LogTag, Is.EqualTo("[JuggernautSceneBuilder]"));
            Assert.That(JuggernautSceneBuilder.DefinitionPath, Is.EqualTo(DemoSample.Root + "/Modes/Juggernaut.asset"));
            Assert.That(JuggernautSceneBuilder.ModeName, Is.EqualTo(GameRulesAssetBuilder.JuggernautName));
            Assert.That(GameRulesAssetBuilder.ModeNames, Has.Member(JuggernautSceneBuilder.ModeName));
            Assert.That(builder.RulesPath, Is.EqualTo(GameRulesAssetBuilder.ModeRulesPath(JuggernautSceneBuilder.ModeName)));
            Assert.That(JuggernautSceneBuilder.RulesPathValue, Is.EqualTo(GameRulesAssetBuilder.JuggernautPath));
        }

        [Test]
        public void EachPlayerIsOnTheirOwnSideLikeFreeForAll()
        {
            var sides = FreeForAllSceneBuilder.Layout(JuggernautSceneBuilder.BotCount);

            Assert.That(JuggernautSceneBuilder.BotCount, Is.EqualTo(5));
            Assert.That(ModeSceneBuilder.ValidateLayout(sides, new Side(0)), Is.SameAs(sides));
            Assert.That(sides, Has.Count.EqualTo(JuggernautSceneBuilder.BotCount + 1));

            var seen = new HashSet<int>();
            for (var i = 0; i < sides.Count; i++)
            {
                Assert.That(seen.Add(sides[i].Side.Id), Is.True, $"side #{i} is shared");
                Assert.That(sides[i].BotCount, Is.EqualTo(i == 0 ? 0 : 1), "the player stands alone on side 0 and every bot has a side of its own");
                Assert.That(sides[i].Spawns, Has.Count.EqualTo(FreeForAllSceneBuilder.SpawnPool.Count));
                Assert.That(sides[i].Spawns[0].Feet, Is.EqualTo(FreeForAllSceneBuilder.SpawnPool[i]), "each side starts at a different spawn");
            }
        }

        [Test]
        public void TheRulesAreFreeForAllWithTheJuggernautObjective()
        {
            var rules = GameRulesAssetBuilder.JuggernautRules();
            var free = GameRulesAssetBuilder.FreeForAllRules();

            Assert.That(rules.Objective, Is.EqualTo(MatchObjective.Juggernaut));
            Assert.That(rules.RespawnMode, Is.EqualTo(RespawnMode.Individual));
            Assert.That(rules.Actions.Downed, Is.EqualTo(Downed.Off));
            Assert.That(rules.FriendlyFire, Is.EqualTo(free.FriendlyFire));
            Assert.That(rules.DeathCamera, Is.EqualTo(DeathCamera.Killer));
            Assert.That(rules.SpawnProtectionSeconds, Is.EqualTo(GameRulesAssetBuilder.TeamDeathmatchSpawnProtectionSeconds));
            Assert.That(GameRulesAssetBuilder.ModeRules(JuggernautSceneBuilder.ModeName).Objective, Is.EqualTo(MatchObjective.Juggernaut));
        }

        [Test]
        public void ANewDefinitionStartsFromTheDefaultTuning()
        {
            Assert.That(JuggernautRunner.DefaultRespawnSeconds, Is.EqualTo(FreeForAllMode.DefaultRespawnSeconds));
            Assert.That(JuggernautSpec.Default.JuggernautMaxHealth, Is.EqualTo(300));
            Assert.That(JuggernautSpec.Default.StartDelaySeconds, Is.LessThan(JuggernautSpec.Default.TimeLimitSeconds));
        }
    }
}

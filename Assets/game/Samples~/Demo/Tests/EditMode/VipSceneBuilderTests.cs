using System;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    // シーンを組んで NavMesh を焼く処理は開いているシーンを置き換える (NewScene Single) ので、
    // -executeMethod Shooter.Editor.VipSceneBuilder.Build で確かめる。
    public sealed class VipSceneBuilderTests
    {
        [Test]
        public void TheVipSceneIsWrittenUnderItsRulesName()
        {
            var builder = new VipSceneBuilder();

            Assert.That(builder.ScenePath, Is.EqualTo(DemoSample.Root + "/Scenes/Vip.unity"));
            Assert.That(builder.RulesPath, Is.EqualTo(GameRulesAssetBuilder.Folder + "/Vip.asset"));
            Assert.That(builder.NavMeshDataPath, Is.EqualTo(DemoSample.Root + "/Scenes/Vip/NavMesh-Floor.asset"));
            Assert.That(builder.LogTag, Is.EqualTo("[VipSceneBuilder]"));
            Assert.That(VipSceneBuilder.DefinitionPath, Is.EqualTo(DemoSample.Root + "/Modes/Vip.asset"));
            Assert.That(VipSceneBuilder.ModeName, Is.EqualTo(GameRulesAssetBuilder.VipName));
            Assert.That(GameRulesAssetBuilder.ModeNames, Has.Member(VipSceneBuilder.ModeName));
            Assert.That(builder.RulesPath, Is.EqualTo(GameRulesAssetBuilder.ModeRulesPath(VipSceneBuilder.ModeName)));
            Assert.That(VipSceneBuilder.RulesPathValue, Is.EqualTo(GameRulesAssetBuilder.VipPath));
        }

        [Test]
        public void TwoTeamsOfThreeStartOnOppositeEnds()
        {
            var sides = VipSceneBuilder.Layout();

            Assert.That(ModeSceneBuilder.ValidateLayout(sides, Side.Alpha), Is.SameAs(sides));
            Assert.That(sides, Has.Count.EqualTo(2));
            Assert.That(sides[0].Side, Is.EqualTo(Side.Alpha));
            Assert.That(sides[0].BotCount, Is.EqualTo(2), "the player and two bots");
            Assert.That(sides[1].Side, Is.EqualTo(Side.Bravo));
            Assert.That(sides[1].BotCount, Is.EqualTo(3));
            Assert.That(sides[0].Spawns[0].Feet.z, Is.EqualTo(0f));
            Assert.That(sides[1].Spawns[0].Feet.z, Is.EqualTo(40f));
        }

        [Test]
        public void TheFirstAttackerAndDefenderMatchTheMode()
        {
            var vip = new Vip(VipSpec.Default);

            Assert.That(VipSceneBuilder.FirstAttacker, Is.EqualTo(vip.Attacker));
            Assert.That(VipSceneBuilder.FirstDefender, Is.EqualTo(vip.Defender));
            Assert.That(VipSceneBuilder.FirstDefender, Is.EqualTo(Side.Bravo), "the bots-only side defends first, so a bot is the first VIP");
        }

        [Test]
        public void TheExitsAreOnTheFlanksFarFromTheDefenders()
        {
            var sides = VipSceneBuilder.Layout();

            Assert.That(VipSceneBuilder.ExitCenters, Is.EqualTo(new[] { new Vector3(-16f, 0f, 8f), new Vector3(16f, 0f, 8f) }));
            Assert.That(VipSceneBuilder.ExitRadius, Is.EqualTo(VipExitPoint.DefaultRadius));
            Assert.That(VipSceneBuilder.MinExitDistanceFromDefenders, Is.EqualTo(25f));
            Assert.That(VipSceneBuilder.ValidateExits(VipSceneBuilder.ExitCenters, sides, VipSceneBuilder.FirstDefender), Is.SameAs(VipSceneBuilder.ExitCenters));

            foreach (var exit in VipSceneBuilder.ExitCenters)
            {
                var fromDefenders = VipSceneBuilder.NearestSpawnDistance(exit, sides[1]);
                var fromAttackers = VipSceneBuilder.NearestSpawnDistance(exit, sides[0]);

                Assert.That(fromDefenders, Is.EqualTo(Mathf.Sqrt((4f * 4f) + (32f * 32f))).Within(1e-4f));
                Assert.That(fromDefenders, Is.GreaterThan(fromAttackers));
                Assert.That(fromAttackers, Is.GreaterThan(VipSceneBuilder.ExitRadius), "an attacker must not spawn inside an exit");
            }
        }

        [Test]
        public void RefusesAnExitNearTheDefenders()
        {
            var sides = VipSceneBuilder.Layout();
            var near = new[] { new Vector3(0f, 0f, 30f) };

            Assert.That(VipSceneBuilder.NearestSpawnDistance(near[0], sides[1]), Is.EqualTo(10f).Within(1e-4f));
            Assert.Throws<InvalidOperationException>(() => VipSceneBuilder.ValidateExits(near, sides, VipSceneBuilder.FirstDefender));
            Assert.Throws<InvalidOperationException>(() => VipSceneBuilder.ValidateExits(Array.Empty<Vector3>(), sides, VipSceneBuilder.FirstDefender));
            Assert.Throws<InvalidOperationException>(() => VipSceneBuilder.ValidateExits(null, sides, VipSceneBuilder.FirstDefender));
            Assert.Throws<InvalidOperationException>(() => VipSceneBuilder.ValidateExits(VipSceneBuilder.ExitCenters, sides, new Side(5)));
            Assert.Throws<ArgumentNullException>(() => VipSceneBuilder.ValidateExits(VipSceneBuilder.ExitCenters, null, VipSceneBuilder.FirstDefender));
        }

        [Test]
        public void NamesTheExitsFromOne()
        {
            Assert.That(VipSceneBuilder.ExitObjectName(0), Is.EqualTo("VipExit-1"));
            Assert.That(VipSceneBuilder.ExitObjectName(1), Is.EqualTo("VipExit-2"));
            Assert.That(VipSceneBuilder.ExitsRootName, Is.EqualTo("VipExits"));
            Assert.Throws<ArgumentOutOfRangeException>(() => VipSceneBuilder.ExitObjectName(-1));
        }

        [Test]
        public void TheRulesKeepTheDeadOutUntilTheNextRound()
        {
            var rules = GameRulesAssetBuilder.VipRules();

            Assert.That(rules.Objective, Is.EqualTo(MatchObjective.Vip));
            Assert.That(rules.RespawnMode, Is.EqualTo(RespawnMode.None));
            Assert.That(rules.SpawnProtectionSeconds, Is.Zero);
            Assert.That(rules.Actions.Downed, Is.EqualTo(Downed.Off));
            Assert.That(rules.FriendlyFire, Is.EqualTo(FriendlyFire.Off));
            Assert.That(GameRulesAssetBuilder.ModeRules(VipSceneBuilder.ModeName).Objective, Is.EqualTo(MatchObjective.Vip));
        }
    }
}

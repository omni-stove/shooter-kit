using System;
using System.Collections.Generic;
using System.Linq;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Shooter.Tests.EditMode
{
    public sealed class HolsterFeatureSceneBuilderTests
    {
        private const float Epsilon = 1e-4f;

        private Scene _scene;
        private readonly List<UnityEngine.Object> _assets = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            _scene = TestScenes.NewAdditive();
        }

        [TearDown]
        public void TearDown()
        {
            if (_scene.IsValid())
            {
                EditorSceneManager.CloseScene(_scene, true);
            }

            foreach (var asset in _assets)
            {
                if (asset != null)
                {
                    UnityEngine.Object.DestroyImmediate(asset);
                }
            }

            _assets.Clear();
            GameSignals.WeaponFreeZone.Clear();
        }

        [Test]
        public void ThereIsOneZoneForEveryReaction()
        {
            Assert.That(HolsterFeatureSceneBuilder.Reactions, Is.EquivalentTo(Enum.GetValues(typeof(WeaponFreeZoneReaction))));
            Assert.That(HolsterFeatureSceneBuilder.Reactions.Select(HolsterFeatureSceneBuilder.ZoneId).Distinct().Count(), Is.EqualTo(4));
            Assert.That(HolsterFeatureSceneBuilder.ZoneId(WeaponFreeZoneReaction.WarnThenHostile), Is.EqualTo("zone-warn-then-hostile"));
            Assert.That(HolsterFeatureSceneBuilder.ZoneName(WeaponFreeZoneReaction.BlockDraw), Is.EqualTo("WeaponFreeZone_BlockDraw"));
            Assert.Throws<ArgumentOutOfRangeException>(() => HolsterFeatureSceneBuilder.ZoneId((WeaponFreeZoneReaction)9));
            Assert.Throws<ArgumentOutOfRangeException>(() => HolsterFeatureSceneBuilder.ZoneFloorCenter((WeaponFreeZoneReaction)9));
        }

        [Test]
        public void TheZonesAndTheGuardPostDoNotOverlap()
        {
            var areas = HolsterFeatureSceneBuilder.Reactions
                .Select(r => (Name: r.ToString(), Bounds: HolsterFeatureSceneBuilder.ZoneBounds(r)))
                .Append((Name: HolsterFeatureSceneBuilder.GuardPostName, Bounds: HolsterFeatureSceneBuilder.GuardPostBounds))
                .ToArray();

            for (var i = 0; i < areas.Length; i++)
            {
                for (var j = i + 1; j < areas.Length; j++)
                {
                    Assert.That(areas[i].Bounds.Intersects(areas[j].Bounds), Is.False, $"{areas[i].Name} overlaps {areas[j].Name}");
                }
            }
        }

        [Test]
        public void TheZonesLieInATwoByTwoGridThreeMetresApart()
        {
            Assert.That(HolsterFeatureSceneBuilder.ZoneFloorCenter(WeaponFreeZoneReaction.WarnOnly), Is.EqualTo(new Vector3(3f, 0f, 3f)));
            Assert.That(HolsterFeatureSceneBuilder.ZoneFloorCenter(WeaponFreeZoneReaction.WarnThenHostile), Is.EqualTo(new Vector3(12f, 0f, 3f)));
            Assert.That(HolsterFeatureSceneBuilder.ZoneFloorCenter(WeaponFreeZoneReaction.AutoHolster), Is.EqualTo(new Vector3(3f, 0f, 12f)));
            Assert.That(HolsterFeatureSceneBuilder.ZoneFloorCenter(WeaponFreeZoneReaction.BlockDraw), Is.EqualTo(new Vector3(12f, 0f, 12f)));
            Assert.That(HolsterFeatureSceneBuilder.ZoneBounds(WeaponFreeZoneReaction.WarnOnly).size, Is.EqualTo(new Vector3(6f, 3f, 6f)));
        }

        // RuntimeNavMeshArea は範囲の中の Collider を全部焼くので、ゾーンのトリガーが入ると箱の上に NavMesh ができる。
        [Test]
        public void TheGuardPostStandsOutsideItsZoneWithinSight()
        {
            var zone = HolsterFeatureSceneBuilder.ZoneBounds(HolsterFeatureSceneBuilder.GuardedReaction);
            var post = HolsterFeatureSceneBuilder.GuardPostCenter;
            var bake = new Bounds(post, new Vector3(HolsterFeatureSceneBuilder.GuardPostWidth, HolsterFeatureSceneBuilder.GuardPostBakeHeight, HolsterFeatureSceneBuilder.GuardPostWidth));

            Assert.That(HolsterFeatureSceneBuilder.GuardedReaction, Is.EqualTo(WeaponFreeZoneReaction.WarnThenHostile));
            Assert.That(post, Is.EqualTo(new Vector3(17.5f, 0f, 3f)));
            Assert.That(bake.Intersects(zone), Is.False);

            var farCorner = new Vector3(zone.min.x, 0f, zone.min.z);
            Assert.That(Vector3.Distance(post, farCorner), Is.LessThan(HolsterFeatureSceneBuilder.GuardSightRange));
        }

        [Test]
        public void TheFootprintCoversTheZonesAndThePost()
        {
            var footprint = HolsterFeatureSceneBuilder.Footprint;

            Assert.That(footprint.min, Is.EqualTo(Vector3.zero));
            Assert.That(footprint.max, Is.EqualTo(new Vector3(19f, 3f, 15f)));
            Assert.That(HolsterFeatureSceneBuilder.Fixture().Footprint, Is.EqualTo(footprint));
            Assert.That(HolsterFeatureSceneBuilder.Fixture().Name, Is.EqualTo(HolsterFeatureSceneBuilder.FixtureName));
        }

        [Test]
        public void PlacingPutsFourZonesWithTheirReactionsAndANeutralGuardOnTheWarnThenHostileZone()
        {
            var root = Populate();

            var zones = root.GetComponentsInChildren<WeaponFreeZone>(true);
            Assert.That(zones.Select(z => z.Spec.Reaction), Is.EquivalentTo(HolsterFeatureSceneBuilder.Reactions));
            foreach (var zone in zones)
            {
                var reaction = zone.Spec.Reaction;
                Assert.That(zone.name, Is.EqualTo(HolsterFeatureSceneBuilder.ZoneName(reaction)));
                Assert.That(zone.ZoneId, Is.EqualTo(HolsterFeatureSceneBuilder.ZoneId(reaction)));
                Assert.That(zone.transform.localPosition, Is.EqualTo(HolsterFeatureSceneBuilder.ZoneFloorCenter(reaction)));

                var trigger = zone.GetComponent<BoxCollider>();
                Assert.That(trigger, Is.Not.Null, zone.name);
                Assert.That(trigger.isTrigger, Is.True, zone.name);
                Assert.That(trigger.size, Is.EqualTo(HolsterFeatureSceneBuilder.ZoneSize), zone.name);
                Assert.That(zone.transform.Find(WeaponFreeZone.VisualName), Is.Not.Null, zone.name + " has a floor marker");
            }

            var guards = root.GetComponentsInChildren<NeutralGuard>(true);
            Assert.That(guards, Has.Length.EqualTo(1));
            var guard = guards[0];
            Assert.That(guard.name, Is.EqualTo(HolsterFeatureSceneBuilder.GuardName));
            Assert.That(guard.Agent.Id, Is.EqualTo(HolsterFeatureSceneBuilder.GuardId));
            Assert.That(guard.Zone, Is.Not.Null);
            Assert.That(guard.Zone.Spec.Reaction, Is.EqualTo(WeaponFreeZoneReaction.WarnThenHostile));
            Assert.That(guard.Phase, Is.EqualTo(NeutralGuardPhase.Neutral));
            Assert.That(guard.gameObject.activeSelf, Is.False, "the guard waits for its NavMesh");
            Assert.That(guard.Agent.WeaponDefinition, Is.Not.Null);
            Assert.That(guard.Agent.CreateSpec().SightRange, Is.EqualTo(HolsterFeatureSceneBuilder.GuardSightRange));

            var area = root.transform.Find(HolsterFeatureSceneBuilder.GuardPostName).GetComponent<RuntimeNavMeshArea>();
            Assert.That(area, Is.Not.Null);
            Assert.That(area.ActivateAfterBuild, Is.EqualTo(new[] { guard.gameObject }));
            foreach (var zone in zones)
            {
                var trigger = new Bounds(zone.transform.position + (Vector3.up * (HolsterFeatureSceneBuilder.ZoneHeight / 2f)), HolsterFeatureSceneBuilder.ZoneSize);
                Assert.That(area.WorldBounds.Intersects(trigger), Is.False, zone.name + " is inside the NavMesh bake");
            }
        }

        [Test]
        public void PlacingWithoutABootstrapWeaponLeavesNothingBehind()
        {
            Assert.Throws<InvalidOperationException>(() => HolsterFeatureSceneBuilder.Fixture().Place(_scene, GameRulesAssetBuilder.PlaygroundRules()));
            Assert.That(_scene.rootCount, Is.EqualTo(0));
        }

        [Test]
        public void CreateZoneRefusesMissingPieces()
        {
            var spec = new WeaponFreeZoneSpec(WeaponFreeZoneReaction.WarnOnly);
            Assert.Throws<ArgumentException>(() => HolsterFeatureSceneBuilder.CreateZone(null, " ", "id", spec, Vector3.zero, Vector3.one));
            Assert.Throws<ArgumentException>(() => HolsterFeatureSceneBuilder.CreateZone(null, "Zone", "", spec, Vector3.zero, Vector3.one));
            Assert.Throws<ArgumentNullException>(() => HolsterFeatureSceneBuilder.CreateZone(null, "Zone", "id", null, Vector3.zero, Vector3.one));
            Assert.Throws<ArgumentOutOfRangeException>(() => HolsterFeatureSceneBuilder.CreateZone(null, "Zone", "id", spec, Vector3.zero, new Vector3(1f, 0f, 1f)));
        }

        [Test]
        public void CreateZoneLeavesAnActiveConfiguredTrigger()
        {
            var parent = new GameObject("Parent");
            SceneManager.MoveGameObjectToScene(parent, _scene);
            var spec = new WeaponFreeZoneSpec(WeaponFreeZoneReaction.AutoHolster, 2f);

            LogAssert.Expect(LogType.Log, WeaponFreeZoneLog.Loaded("zone-a", spec));
            var zone = HolsterFeatureSceneBuilder.CreateZone(parent.transform, "ZoneA", "zone-a", spec, new Vector3(1f, 0f, 2f), new Vector3(4f, 3f, 4f));

            Assert.That(zone.gameObject.activeSelf, Is.True);
            Assert.That(zone.transform.parent, Is.SameAs(parent.transform));
            Assert.That(zone.transform.localPosition, Is.EqualTo(new Vector3(1f, 0f, 2f)));
            Assert.That(zone.Spec.Reaction, Is.EqualTo(WeaponFreeZoneReaction.AutoHolster));
            Assert.That(zone.Spec.HostileAfterSeconds, Is.EqualTo(2f));
            var trigger = zone.GetComponent<BoxCollider>();
            Assert.That(trigger.isTrigger, Is.True);
            Assert.That(trigger.size, Is.EqualTo(new Vector3(4f, 3f, 4f)));
            Assert.That(trigger.center, Is.EqualTo(new Vector3(0f, 1.5f, 0f)));
        }

        [Test]
        public void TheSmokeFindsTheStepOnce()
        {
            Assert.That(TpsRangeSmokeRun.ExtraSteps().OfType<HolsterSmokeStep>().Count(), Is.EqualTo(1));
            Assert.That(new HolsterSmokeStep().Name, Is.EqualTo("holster"));
        }

        // 射撃場には武器を出してはいけない場所の置く物が無いので、smoke は開けた場所に一時的に置く。開けた場所からはみ出さず、2 つが重ならない。
        [Test]
        public void TheSmokeZonesFitInTheOpenAreaApart()
        {
            var warn = new Bounds(HolsterSmokeStep.WarnZoneCenter + (Vector3.up * 1.5f), HolsterSmokeStep.ZoneSize);
            var auto = new Bounds(HolsterSmokeStep.AutoHolsterZoneCenter + (Vector3.up * 1.5f), HolsterSmokeStep.ZoneSize);
            var half = TpsRangeSceneBuilder.OpenAreaHalfExtent;
            var open = new Bounds(TpsRangeSceneBuilder.OpenAreaCenter, new Vector3(half * 2f, 10f, half * 2f));

            Assert.That(warn.Intersects(auto), Is.False);
            foreach (var zone in new[] { warn, auto })
            {
                Assert.That(open.Contains(zone.min) && open.Contains(zone.max), Is.True, zone.ToString());
            }
        }

        [Test]
        public void TheSmokeMatchesTheHolsterAndZoneLinesByTheirHeads()
        {
            Assert.That(HolsterSmokeStep.BeganPrefix("Player", HolsterPhase.Holstering), Is.EqualTo("[Holster] holder=Player began=Holstering "));
            Assert.That(HolsterLog.Began("Player", HolsterPhase.Holstering, 0.4f), Does.StartWith(HolsterSmokeStep.BeganPrefix("Player", HolsterPhase.Holstering)));
            Assert.That(HolsterSmokeStep.WarningPrefix("smoke-warn-only", WeaponFreeZoneReaction.WarnOnly), Is.EqualTo("[WeaponFreeZone] zone=smoke-warn-only warning reaction=WarnOnly "));
        }

        private GameObject Populate()
        {
            var station = new FeatureStation(HolsterFeatureSceneBuilder.Fixture(), HolsterFeatureSceneBuilder.StationOffset);
            var feature = new FeatureScene("Test", GameRulesAssetBuilder.PlaygroundRules(), new[] { station }, 0f, 15f, false);
            FeatureSceneBuilder.PopulateScene(_scene, NewWeapons(), NewRules(feature.Rules), feature);

            foreach (var root in _scene.GetRootGameObjects())
            {
                if (root.name == HolsterFeatureSceneBuilder.FixtureName)
                {
                    Assert.That(root.transform.position, Is.EqualTo(HolsterFeatureSceneBuilder.StationOffset));
                    return root;
                }
            }

            Assert.Fail($"'{HolsterFeatureSceneBuilder.FixtureName}' is not in the scene");
            return null;
        }

        private WeaponDefinition[] NewWeapons()
        {
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            weapon.name = "Rifle";
            _assets.Add(weapon);
            WeaponAssetBuilder.Configure(weapon, DemoTestWeapons.CarbineSpec());
            return new[] { weapon };
        }

        private GameRulesDefinition NewRules(GameRules rules)
        {
            var definition = ScriptableObject.CreateInstance<GameRulesDefinition>();
            definition.name = "Rules";
            _assets.Add(definition);
            GameRulesAssetBuilder.Configure(definition, rules);
            return definition;
        }
    }
}

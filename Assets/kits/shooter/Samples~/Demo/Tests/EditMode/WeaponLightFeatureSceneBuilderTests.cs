using System;
using System.Collections.Generic;
using System.Linq;
using GameKit.Core;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Tests.EditMode
{
    public sealed class WeaponLightFeatureSceneBuilderTests
    {
        private const float Epsilon = 1e-4f;

        private const string LightItemId = "0d6c3f1e-2b4a-4c8d-9e7f-5a1b2c3d4e5f";
        private const string MuzzleItemId = "7a8b9c0d-1e2f-4a3b-8c4d-5e6f7a8b9c0d";

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
        }

        [Test]
        public void TheFootprintCoversTheRoomAndTheLightPickup()
        {
            var footprint = WeaponLightFeatureSceneBuilder.Footprint;

            Assert.That(Vector3.Distance(footprint.min, new Vector3(-2f, 0f, 0f)), Is.LessThan(Epsilon), footprint.min.ToString());
            Assert.That(Vector3.Distance(footprint.max, new Vector3(8f, 3.2f, 12f)), Is.LessThan(Epsilon), footprint.max.ToString());
            Assert.That(WeaponLightFeatureSceneBuilder.Fixture().Footprint, Is.EqualTo(footprint));
            Assert.That(WeaponLightFeatureSceneBuilder.Fixture().Name, Is.EqualTo("DarkRoom"));
            var loose = footprint;
            loose.Expand(Epsilon);
            foreach (var box in WeaponLightFeatureSceneBuilder.Boxes())
            {
                var bounds = new Bounds(box.Center, box.Size);
                Assert.That(loose.Contains(bounds.min) && loose.Contains(bounds.max), Is.True, box.Name);
            }
        }

        // 屋根と四方の壁で日の光を遮り、手前の壁に体の通れる入口だけを開ける。
        [Test]
        public void TheRoomIsWalledAndRoofedWithOneDoorInTheFrontWall()
        {
            var boxes = WeaponLightFeatureSceneBuilder.Boxes().ToDictionary(b => b.Name, b => new Bounds(b.Center, b.Size));

            Assert.That(boxes.Keys, Is.EquivalentTo(new[]
            {
                "DarkRoom_FrontLeft", "DarkRoom_FrontRight", "DarkRoom_Back", "DarkRoom_Left", "DarkRoom_Right", "DarkRoom_Roof",
            }));

            var roof = boxes[WeaponLightFeatureSceneBuilder.RoofName];
            Assert.That(roof.min.x, Is.EqualTo(0f).Within(Epsilon));
            Assert.That(roof.max.x, Is.EqualTo(WeaponLightFeatureSceneBuilder.RoomWidth).Within(Epsilon));
            Assert.That(roof.min.z, Is.EqualTo(0f).Within(Epsilon));
            Assert.That(roof.max.z, Is.EqualTo(WeaponLightFeatureSceneBuilder.RoomDepth).Within(Epsilon));
            Assert.That(roof.min.y, Is.EqualTo(WeaponLightFeatureSceneBuilder.WallHeight).Within(Epsilon), "the roof sits on the walls");

            var left = boxes[WeaponLightFeatureSceneBuilder.FrontLeftWallName];
            var right = boxes[WeaponLightFeatureSceneBuilder.FrontRightWallName];
            Assert.That(right.min.x - left.max.x, Is.EqualTo(WeaponLightFeatureSceneBuilder.DoorWidth).Within(Epsilon));
            Assert.That(WeaponLightFeatureSceneBuilder.DoorWidth, Is.GreaterThan(2f * WeaponLightFeatureSceneBuilder.EnemyRadius));
            Assert.That(WeaponLightFeatureSceneBuilder.DoorCenter.x, Is.EqualTo((left.max.x + right.min.x) / 2f).Within(Epsilon));

            foreach (var pair in boxes.Where(p => p.Key != WeaponLightFeatureSceneBuilder.RoofName))
            {
                Assert.That(pair.Value.min.y, Is.EqualTo(0f).Within(Epsilon), pair.Key);
                Assert.That(pair.Value.max.y, Is.EqualTo(WeaponLightFeatureSceneBuilder.WallHeight).Within(Epsilon), pair.Key);
            }
        }

        [Test]
        public void TheEnemyStandsInsideFacingTheDoorWithinSight()
        {
            var interior = WeaponLightFeatureSceneBuilder.InteriorBounds;
            var feet = WeaponLightFeatureSceneBuilder.EnemyFeet;

            Assert.That(feet, Is.EqualTo(new Vector3(4f, 0f, 10f)));
            Assert.That(interior.Contains(feet + (Vector3.up * 0.5f)), Is.True);
            Assert.That(feet.x - WeaponLightFeatureSceneBuilder.EnemyRadius, Is.GreaterThan(interior.min.x));
            Assert.That(feet.z + WeaponLightFeatureSceneBuilder.EnemyRadius, Is.LessThan(interior.max.z));

            var toDoor = WeaponLightFeatureSceneBuilder.DoorCenter - feet;
            Assert.That(Vector3.Dot(WeaponLightFeatureSceneBuilder.EnemyFacing * Vector3.forward, toDoor.normalized), Is.EqualTo(1f).Within(Epsilon));
            Assert.That(toDoor.magnitude, Is.LessThan(WeaponLightFeatureSceneBuilder.SightRange), "the enemy sees the whole room up to the door");
            Assert.That(WeaponLightFeatureSceneBuilder.Spec().SightRange, Is.EqualTo(WeaponLightFeatureSceneBuilder.SightRange));
            Assert.That(WeaponLightFeatureSceneBuilder.LitGainFactor, Is.EqualTo(1.5f));
            Assert.That(WeaponLightFeatureSceneBuilder.LitGainFactor, Is.GreaterThan(AwarenessSpec.DefaultLitGainFactor));
        }

        // RuntimeNavMeshArea は範囲の中の Collider を全部焼くので、拾い物は焼く範囲の外で、入口から見えない壁の陰に置く。
        [Test]
        public void TheLightPickupIsOutsideTheRoomAndTheBakeBehindTheFrontWall()
        {
            var pickup = WeaponLightFeatureSceneBuilder.LightPickupBounds;
            Assert.That(pickup.Intersects(WeaponLightFeatureSceneBuilder.NavMeshBounds), Is.False);
            foreach (var box in WeaponLightFeatureSceneBuilder.Boxes())
            {
                Assert.That(pickup.Intersects(new Bounds(box.Center, box.Size)), Is.False, box.Name);
            }

            var doorLeft = (WeaponLightFeatureSceneBuilder.RoomWidth - WeaponLightFeatureSceneBuilder.DoorWidth) / 2f;
            Assert.That(pickup.max.x, Is.LessThan(0f), "the pickup is outside the left wall");
            Assert.That(pickup.max.x, Is.LessThan(doorLeft));
        }

        [Test]
        public void LightItemIdPicksTheFirstAttachmentWithALight()
        {
            Assert.That(WeaponLightFeatureSceneBuilder.LightItemId(Items(withLight: true)), Is.EqualTo(LightItemId));
            Assert.That(WeaponLightFeatureSceneBuilder.LightItemId(Items(withLight: false)), Is.Null);
            Assert.Throws<ArgumentNullException>(() => WeaponLightFeatureSceneBuilder.LightItemId(null));
        }

        [Test]
        public void TheKitInventoryHasALightToPickUp()
        {
            Assert.That(WeaponLightFeatureSceneBuilder.LightItemId(KitTuning.Items), Is.EqualTo("f3a7c2e9-58b1-4d06-9e4a-2c7b8d1f6a35"));
        }

        [Test]
        public void PlacingPutsTheRoomAStealthEnemyWithTheLitFactorAndTheLightPickup()
        {
            var root = Populate(Items(withLight: true));

            foreach (var box in WeaponLightFeatureSceneBuilder.Boxes())
            {
                var placed = root.transform.Find(box.Name);
                Assert.That(placed, Is.Not.Null, box.Name);
                Assert.That(placed.GetComponent<Collider>(), Is.Not.Null, box.Name);
                Assert.That(Vector3.Distance(placed.position, box.Center + WeaponLightFeatureSceneBuilder.StationOffset), Is.LessThan(Epsilon), box.Name);
            }

            var agents = root.GetComponentsInChildren<EnemyAgent>(true);
            Assert.That(agents, Has.Length.EqualTo(1));
            var enemy = agents[0];
            Assert.That(enemy.name, Is.EqualTo(WeaponLightFeatureSceneBuilder.EnemyName));
            Assert.That(enemy.Id, Is.EqualTo(WeaponLightFeatureSceneBuilder.EnemyId));
            Assert.That(enemy.WeaponDefinition, Is.Not.Null);
            Assert.That(enemy.CreateSpec().SightRange, Is.EqualTo(WeaponLightFeatureSceneBuilder.SightRange));
            Assert.That(enemy.gameObject.activeSelf, Is.False, "the enemy waits for its NavMesh");

            var stealth = enemy.GetComponent<EnemyStealth>();
            Assert.That(stealth, Is.Not.Null, "the enemy is a stealth enemy");
            Assert.That(stealth.CreateSpec().LitGainFactor, Is.EqualTo(1.5f));
            Assert.That(stealth.CreateSpec().ArmedGainFactor, Is.EqualTo(AwarenessSpec.DefaultArmedGainFactor), "only the lit factor is raised");

            var area = root.transform.Find(WeaponLightFeatureSceneBuilder.NavMeshName).GetComponent<RuntimeNavMeshArea>();
            Assert.That(area, Is.Not.Null);
            Assert.That(area.ActivateAfterBuild, Is.EqualTo(new[] { enemy.gameObject }));
            Assert.That(area.WorldBounds.Contains(enemy.transform.position), Is.True);

            var pickup = root.transform.Find(WeaponLightFeatureSceneBuilder.LightPickupName).GetComponent<PickupSpawnPoint>();
            Assert.That(pickup, Is.Not.Null);
            Assert.That(pickup.Contents.Kind, Is.EqualTo(PickupKinds.Item));
            Assert.That(pickup.Contents.ItemId, Is.EqualTo(LightItemId));
            Assert.That(pickup.Contents.Amount, Is.EqualTo(1));
            Assert.That(pickup.CollectMode, Is.EqualTo(PickupCollectMode.Look));
            Assert.That(
                Vector3.Distance(pickup.transform.position, WeaponLightFeatureSceneBuilder.LightPickupPosition + WeaponLightFeatureSceneBuilder.StationOffset),
                Is.LessThan(Epsilon));
        }

        [Test]
        public void PlacingWithoutABootstrapWeaponOrALightLeavesNothingBehind()
        {
            var rules = GameRulesAssetBuilder.PlaygroundRules();
            Assert.Throws<InvalidOperationException>(() => WeaponLightFeatureSceneBuilder.Place(_scene, rules, Items(withLight: true)));
            Assert.That(_scene.rootCount, Is.EqualTo(0));

            var bootstrap = new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
            SceneManager.MoveGameObjectToScene(bootstrap.gameObject, _scene);
            bootstrap.WeaponDefinitions = NewWeapons();
            var e = Assert.Throws<InvalidOperationException>(() => WeaponLightFeatureSceneBuilder.Place(_scene, rules, Items(withLight: false)));
            Assert.That(e.Message, Does.Contain("light"));
            Assert.That(_scene.rootCount, Is.EqualTo(1), "only the bootstrap is left");
        }

        [Test]
        public void TheDarkRoomDoesNotOverlapTheOtherPlaygroundStationsOrTheSpawn()
        {
            var stations = FeatureSceneBuilder.PlaygroundStations();
            var room = stations.Single(s => s.Fixture.Name == WeaponLightFeatureSceneBuilder.FixtureName);

            Assert.That(room.Offset, Is.EqualTo(WeaponLightFeatureSceneBuilder.StationOffset));
            foreach (var other in stations.Where(s => s.Fixture.Name != WeaponLightFeatureSceneBuilder.FixtureName))
            {
                Assert.That(room.Footprint.Intersects(other.Footprint), Is.False, other.Fixture.Name);
            }

            Assert.That(room.Footprint.Contains(new Vector3(0f, 0.5f, 0f)), Is.False, "the room is away from the spawn");
        }

        [Test]
        public void TheSmokeFindsTheStepOnceAfterTheHolster()
        {
            var steps = TpsRangeSmokeRun.ExtraSteps();
            Assert.That(steps.OfType<WeaponLightSmokeStep>().Count(), Is.EqualTo(1));

            var step = new WeaponLightSmokeStep();
            Assert.That(step.Name, Is.EqualTo("weaponLight"));
            Assert.That(step.TimeoutSeconds, Is.GreaterThan(0f));
            Assert.That(step.Order, Is.GreaterThan(new HolsterSmokeStep().Order));
        }

        [Test]
        public void TheSmokeMatchesTheLightLines()
        {
            Assert.That(WeaponLightSmokeStep.OnLine("Player", "rifle"), Is.EqualTo("[Combat] weaponLight id=Player weapon=rifle on=true"));
            Assert.That(WeaponLightSmokeStep.SwitchedOffLine("Player", "rifle"), Is.EqualTo("[Combat] weaponLight id=Player weapon=rifle on=false cause=Switched"));
            Assert.That(WeaponLightSmokeStep.NoLightLine("Player", "pistol"), Is.EqualTo("[Combat] weaponLight id=Player weapon=pistol not-lit reason=no-light"));
        }

        private GameObject Populate(ItemCatalog items)
        {
            var bootstrap = new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
            SceneManager.MoveGameObjectToScene(bootstrap.gameObject, _scene);
            bootstrap.WeaponDefinitions = NewWeapons();

            var root = WeaponLightFeatureSceneBuilder.Place(_scene, GameRulesAssetBuilder.PlaygroundRules(), items);
            Assert.That(root.name, Is.EqualTo(WeaponLightFeatureSceneBuilder.FixtureName));
            Assert.That(root.scene, Is.EqualTo(_scene));

            // FeatureSceneBuilder.PlaceStation と同じく、置いた後に根ごと置き場へ移す。
            root.transform.position += WeaponLightFeatureSceneBuilder.StationOffset;
            return root;
        }

        private static ItemCatalog Items(bool withLight)
        {
            var specs = new List<ItemSpec>
            {
                ItemSpecs.Create(MuzzleItemId, "item.muzzle", attachment: new AttachmentSpec("muzzle")),
            };
            if (withLight)
            {
                specs.Add(ItemSpecs.Create(LightItemId, "item.weaponLight", attachment: new AttachmentSpec("light", light: new WeaponLightSpec(range: 25f))));
            }

            return new ItemCatalog(specs, InventoryRule.Default);
        }

        private WeaponDefinition[] NewWeapons()
        {
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            weapon.name = "Rifle";
            _assets.Add(weapon);
            WeaponAssetBuilder.Configure(weapon, DemoTestWeapons.CarbineSpec());
            return new[] { weapon };
        }
    }
}

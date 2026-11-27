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
    public sealed class BreachFeatureSceneBuilderTests
    {
        private const float Epsilon = 1e-4f;

        private static readonly Vector3 StationOffset = new Vector3(10f, 0f, 20f);

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
        public void TheFootprintCoversTheRoomTheApproachesAndTheCharge()
        {
            var footprint = BreachFeatureSceneBuilder.Footprint;

            Assert.That(Vector3.Distance(footprint.min, new Vector3(-2.3f, 0f, -2.5f)), Is.LessThan(Epsilon));
            Assert.That(Vector3.Distance(footprint.max, new Vector3(4.8f, BreachFeatureSceneBuilder.WallHeight, 7.5f)), Is.LessThan(Epsilon));

            // 壁の縁は足場の縁と同じ所にあるので、丸めの分だけ広げて比べる。
            var loose = footprint;
            loose.Expand(Epsilon * 10f);
            foreach (var wall in BreachFeatureSceneBuilder.Walls())
            {
                var bounds = new Bounds(wall.Center, wall.Size);
                Assert.That(loose.Contains(bounds.min) && loose.Contains(bounds.max), Is.True, wall.Name);
            }

            Assert.That(footprint.Contains(BreachFeatureSceneBuilder.ChargePickupPosition), Is.True);
            Assert.That(BreachFeatureSceneBuilder.Fixture().Footprint, Is.EqualTo(footprint));
            Assert.That(BreachFeatureSceneBuilder.Fixture().Name, Is.EqualTo(BreachFeatureSceneBuilder.FixtureName));
        }

        [Test]
        public void TheWallsLeaveADoorwayThatAPlayerFitsThroughOnEachEnd()
        {
            var walls = BreachFeatureSceneBuilder.Walls().Select(w => new Bounds(w.Center, w.Size)).ToArray();
            foreach (var feet in new[] { BreachFeatureSceneBuilder.KickDoorFeet, BreachFeatureSceneBuilder.ChargeDoorFeet })
            {
                var doorway = new Bounds(feet + (Vector3.up * 1f), new Vector3(BreachFeatureSceneBuilder.DoorwayWidth - 0.01f, 1.9f, 0.01f));
                Assert.That(walls.Any(w => w.Intersects(doorway)), Is.False, "nothing but the door fills the doorway at " + feet);
            }

            Assert.That(BreachFeatureSceneBuilder.DoorwayWidth, Is.GreaterThan(1f), "a player capsule (1 m across) fits through");
            Assert.That(BreachFeatureSceneBuilder.DoorwayHeight, Is.GreaterThan(2f), "a standing player fits under the lintel");
            Assert.That(BreachFeatureSceneBuilder.PanelSize, Is.EqualTo(new Vector3(BreachFeatureSceneBuilder.DoorwayWidth, BreachFeatureSceneBuilder.DoorwayHeight, BreachFeatureSceneBuilder.PanelThickness)));
        }

        [Test]
        public void TheChargeIsPlacedOutsideTheRoomAwayFromWhereTheDoorsAreUsed()
        {
            var pickup = BreachFeatureSceneBuilder.ChargePickupPosition;
            var reach = KitTuning.Breach.Reach;

            Assert.That(pickup.x - BreachFeatureSceneBuilder.ChargePickupRadius, Is.GreaterThan((BreachFeatureSceneBuilder.RoomWidth / 2f) + BreachFeatureSceneBuilder.WallThickness));
            Assert.That(Vector3.Distance(pickup, BreachFeatureSceneBuilder.KickDoorFeet), Is.GreaterThan(reach + BreachFeatureSceneBuilder.ChargePickupRadius));
            Assert.That(Vector3.Distance(pickup, BreachFeatureSceneBuilder.ChargeDoorFeet), Is.GreaterThan(reach + BreachFeatureSceneBuilder.ChargePickupRadius));
        }

        [Test]
        public void TheChargeDoorTuningNamesTheInventoryChargeAndKeepsTheRestOfTheGameTuning()
        {
            var spec = BreachJson.Parse(BreachFeatureSceneBuilder.ChargeTuningJson(), "charge.json", KitTuning.Breach);

            Assert.That(spec.NeedsChargeItem, Is.True);
            Assert.That(spec.ChargeItemId, Is.EqualTo(BreachFeatureSceneBuilder.ChargeItemId));
            Assert.That(KitTuning.Items.TryFind(BreachFeatureSceneBuilder.ChargeItemId, out var item), Is.True, "Tuning/inventory.json lists the charge");
            Assert.That(item.NameKey, Is.EqualTo("item.breachCharge"));
            Assert.That(spec.PlantSeconds, Is.EqualTo(KitTuning.Breach.PlantSeconds));
            Assert.That(spec.ChargeRadius, Is.EqualTo(KitTuning.Breach.ChargeRadius));
            Assert.That(KitTuning.Breach.NeedsChargeItem, Is.False, "only the breach room's charge door asks for the item");
        }

        [Test]
        public void ChargeSpecRefusesATuningWithoutAnItemOrWithAnItemThatIsNotInTheInventory()
        {
            Assert.Throws<ArgumentNullException>(() => BreachFeatureSceneBuilder.ChargeSpec(null));
            Assert.Throws<InvalidOperationException>(() => BreachFeatureSceneBuilder.ChargeSpec(Text("{}")));
            Assert.Throws<InvalidOperationException>(() => BreachFeatureSceneBuilder.ChargeSpec(Text("{\"chargeItemId\": \"00000000-0000-4000-8000-000000000000\"}")));
            Assert.Throws<TuningJsonException>(() => BreachFeatureSceneBuilder.ChargeSpec(Text("{\"chargeItemId\": ")));
            Assert.That(BreachFeatureSceneBuilder.ChargeSpec(Text(BreachFeatureSceneBuilder.ChargeTuningJson())).ChargeItemId, Is.EqualTo(BreachFeatureSceneBuilder.ChargeItemId));
        }

        [Test]
        public void TheTargetAndTheEnemyStandInsideTheBreachBoxOfBothDoors()
        {
            var spec = KitTuning.Breach;
            var kick = Door(BreachFeatureSceneBuilder.KickDoorFeet, BreachFeatureSceneBuilder.KickDoorRotation);
            var charge = Door(BreachFeatureSceneBuilder.ChargeDoorFeet, BreachFeatureSceneBuilder.ChargeDoorRotation);
            var inside = new[]
            {
                BreachFeatureSceneBuilder.TargetFeet + Vector3.up,
                BreachFeatureSceneBuilder.EnemyFeet + (Vector3.up * (BreachFeatureSceneBuilder.EnemyHeight / 2f)),
            };

            foreach (var door in new[] { kick, charge })
            {
                // 部屋の外 (ドアの前) から破るので、奥は部屋の中になる。
                var outside = door.position + (door.forward * 1f);
                var farSign = Vector3.Dot(outside - door.position, door.forward) > 0f ? -1f : 1f;
                BreachDoor.RoomBox(door, farSign, spec, out var center, out var half);
                foreach (var point in inside)
                {
                    var local = Quaternion.Inverse(door.rotation) * (point - center);
                    Assert.That(Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z, Is.True, $"{point} is in the box of the door at {door.position}");
                }
            }
        }

        [Test]
        public void PlacingBuildsTheRoomWithClosedDoorsAndArmsTheEnemy()
        {
            var root = Populate();

            var doors = root.GetComponentsInChildren<BreachDoor>(true);
            Assert.That(doors, Has.Length.EqualTo(2));
            var kick = doors.Single(d => d.Id == BreachFeatureSceneBuilder.KickDoorId);
            var charge = doors.Single(d => d.Id == BreachFeatureSceneBuilder.ChargeDoorId);
            Assert.That(kick.name, Is.EqualTo(BreachFeatureSceneBuilder.KickDoorName));
            Assert.That(charge.name, Is.EqualTo(BreachFeatureSceneBuilder.ChargeDoorName));
            Assert.That(kick.Tuning, Is.Null, "the kick door reads the game tuning");
            Assert.That(charge.Tuning, Is.Not.Null);
            Assert.That(BreachFeatureSceneBuilder.ChargeSpec(charge.Tuning).ChargeItemId, Is.EqualTo(BreachFeatureSceneBuilder.ChargeItemId));
            foreach (var door in doors)
            {
                Assert.That(door.IsBound, Is.True, door.name + " is bound so its panel is saved with the scene");
                Assert.That(door.Phase, Is.EqualTo(BreachDoorPhase.Closed));
                Assert.That(door.Visuals.Panel, Is.Not.Null, door.name);
                Assert.That(door.Visuals.PanelSize, Is.EqualTo(BreachFeatureSceneBuilder.PanelSize), door.name);
                Assert.That(door.Visuals.PanelColliders(), Has.Length.EqualTo(1), door.name);
            }

            Assert.That(Vector3.Distance(kick.transform.position, StationOffset + BreachFeatureSceneBuilder.KickDoorFeet), Is.LessThan(Epsilon));
            Assert.That(Vector3.Distance(charge.transform.position, StationOffset + BreachFeatureSceneBuilder.ChargeDoorFeet), Is.LessThan(Epsilon));

            var target = root.GetComponentsInChildren<TargetDummy>(true).Single();
            Assert.That(target.Damageable.Id, Is.EqualTo(BreachFeatureSceneBuilder.TargetId));
            Assert.That(target.Damageable.MaxHealth, Is.EqualTo(BreachFeatureSceneBuilder.TargetMaxHealth));
            Assert.That(target.GetComponent<StaggerReaction>(), Is.Not.Null);
            Assert.That(Vector3.Distance(target.transform.position, StationOffset + BreachFeatureSceneBuilder.TargetFeet), Is.LessThan(Epsilon));

            var enemy = root.GetComponentsInChildren<EnemyAgent>(true).Single();
            Assert.That(enemy.name, Is.EqualTo(BreachFeatureSceneBuilder.EnemyName));
            Assert.That(enemy.Id, Is.EqualTo(BreachFeatureSceneBuilder.EnemyId));
            Assert.That(enemy.WeaponDefinition, Is.Not.Null);
            Assert.That(enemy.GetComponent<StaggerReaction>(), Is.Not.Null);
            Assert.That(enemy.CreateSpec().SightRange, Is.EqualTo(BreachFeatureSceneBuilder.EnemySightRange));
            Assert.That(enemy.gameObject.activeSelf, Is.False, "the enemy waits for its NavMesh");

            var area = root.GetComponentInChildren<RuntimeNavMeshArea>(true);
            Assert.That(area, Is.Not.Null);
            Assert.That(area.ActivateAfterBuild, Is.EqualTo(new[] { enemy.gameObject }));
            Assert.That(area.WorldBounds.Contains(enemy.transform.position), Is.True);

            var point = root.GetComponentInChildren<PickupSpawnPoint>(true);
            Assert.That(point, Is.Not.Null);
            Assert.That(point.name, Is.EqualTo(BreachFeatureSceneBuilder.ChargePickupName));
            Assert.That(point.CollectMode, Is.EqualTo(PickupCollectMode.Touch));
            Assert.That(point.RespawnSeconds, Is.EqualTo(BreachFeatureSceneBuilder.ChargePickupRespawnSeconds));
            var contents = point.Contents.ToSpec();
            Assert.That(contents.Kind, Is.EqualTo(PickupKind.Item));
            Assert.That(contents.ItemId, Is.EqualTo(BreachFeatureSceneBuilder.ChargeItemId));
            Assert.That(contents.Amount, Is.EqualTo(1));
        }

        [Test]
        public void TheClosedDoorsBlockTheirDoorwaysAndAKickStaggersTheTargetInside()
        {
            var root = Populate();
            Physics.SyncTransforms();
            var kick = root.GetComponentsInChildren<BreachDoor>(true).Single(d => d.Id == BreachFeatureSceneBuilder.KickDoorId);
            var origin = kick.transform.position + (kick.transform.forward * 1f) + (Vector3.up * 1f);

            Assert.That(Physics.Raycast(origin, -kick.transform.forward, out var hit, 2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), Is.True);
            Assert.That(hit.collider.GetComponentInParent<BreachDoor>(), Is.SameAs(kick), "the doorway is closed by the door, not a wall");

            var reaction = root.GetComponentInChildren<TargetDummy>(true).GetComponent<StaggerReaction>();
            Assert.That(reaction.Bind(), Is.True);
            var breacher = new GameObject("Breacher");
            SceneManager.MoveGameObjectToScene(breacher, _scene);
            breacher.transform.position = kick.transform.position + (kick.transform.forward * 1f);
            breacher.AddComponent<Damageable>().Initialize("breacher", 100);

            Assert.That(kick.Kick(breacher), Is.True);

            Assert.That(kick.IsPassable, Is.True);
            Assert.That(reaction.Current, Is.EqualTo(BreachSpec.StaggerKindFor(BreachMethod.Kick)));
        }

        [Test]
        public void PlacingWithoutABootstrapWeaponLeavesNothingBehind()
        {
            Assert.Throws<InvalidOperationException>(() => BreachFeatureSceneBuilder.Fixture().Place(_scene, GameRulesAssetBuilder.PlaygroundRules()));
            Assert.That(_scene.rootCount, Is.EqualTo(0));
        }

        [Test]
        public void CreateDoorRefusesMissingPieces()
        {
            var size = BreachFeatureSceneBuilder.PanelSize;
            Assert.Throws<ArgumentException>(() => BreachFeatureSceneBuilder.CreateDoor(null, " ", "id", Vector3.zero, Quaternion.identity, size));
            Assert.Throws<ArgumentException>(() => BreachFeatureSceneBuilder.CreateDoor(null, "Door", "", Vector3.zero, Quaternion.identity, size));
            Assert.Throws<ArgumentOutOfRangeException>(() => BreachFeatureSceneBuilder.CreateDoor(null, "Door", "id", Vector3.zero, Quaternion.identity, new Vector3(1f, 0f, 0.1f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => BreachFeatureSceneBuilder.CreateDoor(null, "Door", "id", Vector3.zero, Quaternion.identity, new Vector3(1f, float.PositiveInfinity, 0.1f)));
        }

        [Test]
        public void CreateDoorLeavesAnActiveDoorWithItsPanelAtTheGivenPlace()
        {
            var parent = new GameObject("Parent");
            SceneManager.MoveGameObjectToScene(parent, _scene);
            var tuning = Text(BreachFeatureSceneBuilder.ChargeTuningJson());
            var rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);

            var door = BreachFeatureSceneBuilder.CreateDoor(parent.transform, "DoorA", "door-a", new Vector3(1f, 0f, 2f), rotation, new Vector3(2f, 3f, 0.1f), tuning);

            Assert.That(door.gameObject.activeSelf, Is.True);
            Assert.That(door.transform.parent, Is.SameAs(parent.transform));
            Assert.That(Vector3.Distance(door.transform.position, new Vector3(1f, 0f, 2f)), Is.LessThan(Epsilon));
            Assert.That(Quaternion.Angle(door.transform.rotation, rotation), Is.LessThan(0.01f));
            Assert.That(door.Id, Is.EqualTo("door-a"));
            Assert.That(door.Tuning, Is.SameAs(tuning));
            Assert.That(door.Spec.ChargeItemId, Is.EqualTo(BreachFeatureSceneBuilder.ChargeItemId));
            Assert.That(door.Visuals, Is.SameAs(door.GetComponent<BreachDoorVisuals>()));
            Assert.That(door.Visuals.PanelSize, Is.EqualTo(new Vector3(2f, 3f, 0.1f)));
            Assert.That(((IBreachDoorSource)door).Panel, Is.SameAs(door.Visuals.Panel));
            Assert.That(Vector3.Distance(((IBreachDoorSource)door).PanelFacing, Vector3.left), Is.LessThan(Epsilon));
        }

        [Test]
        public void TheSmokeFindsTheStepOnce()
        {
            Assert.That(TpsRangeSmokeRun.ExtraSteps().OfType<BreachSmokeStep>().Count(), Is.EqualTo(1));
            Assert.That(new BreachSmokeStep().Name, Is.EqualTo("breach"));
        }

        // 射撃場には突入の部屋が無いので、smoke は開けた場所に一時的にドアと的を置く。開けた場所からはみ出さず、2 組が重ならない。
        [Test]
        public void TheSmokeDoorsTargetsAndStandPointsFitInTheOpenArea()
        {
            var half = TpsRangeSceneBuilder.OpenAreaHalfExtent;
            var open = new Bounds(TpsRangeSceneBuilder.OpenAreaCenter, new Vector3(half * 2f, 10f, half * 2f));
            var spec = KitTuning.Breach;

            foreach (var feet in new[] { BreachSmokeStep.KickDoorFeet, BreachSmokeStep.ChargeDoorFeet })
            {
                foreach (var point in new[] { feet, BreachSmokeStep.TargetFeet(feet), BreachSmokeStep.InFrontOf(feet, BreachSmokeStep.RetreatDistance) })
                {
                    Assert.That(open.Contains(point), Is.True, point.ToString());
                }

                Assert.That(BreachSmokeStep.StandOff, Is.LessThan(spec.Reach));
                Assert.That(BreachSmokeStep.TargetDepth, Is.LessThan(spec.RoomDepth), "the target is in the breach box");
                Assert.That(BreachSmokeStep.TargetDepth, Is.GreaterThan(spec.ChargeRadius + 0.5f), "the blast does not reach the target");
                Assert.That(BreachSmokeStep.RetreatDistance, Is.GreaterThan(spec.ChargeRadius + 0.5f), "the blast does not reach the planter");
            }

            var gap = Mathf.Abs(BreachSmokeStep.ChargeDoorFeet.x - BreachSmokeStep.KickDoorFeet.x);
            Assert.That(gap, Is.GreaterThan(spec.RoomWidth), "the kick does not stagger the charge door's target");
        }

        [Test]
        public void TheSmokeMatchesTheStaggerLineByItsHead()
        {
            Assert.That(BreachSmokeStep.ImposedPrefix("t", BreachMethod.Kick), Is.EqualTo("[Stagger] id=t imposed kind=" + BreachSpec.StaggerKindFor(BreachMethod.Kick) + " "));
            Assert.That(
                StaggerReaction.FormatImposeLog("t", BreachSpec.StaggerKindFor(BreachMethod.Charge), 3f, BreachDoor.StaggerSource),
                Does.StartWith(BreachSmokeStep.ImposedPrefix("t", BreachMethod.Charge)));
        }

        private GameObject Populate()
        {
            var station = new FeatureStation(BreachFeatureSceneBuilder.Fixture(), StationOffset);
            var feature = new FeatureScene("Test", GameRulesAssetBuilder.PlaygroundRules(), new[] { station }, 0f, 15f, false);
            FeatureSceneBuilder.PopulateScene(_scene, NewWeapons(), NewRules(feature.Rules), feature);

            foreach (var root in _scene.GetRootGameObjects())
            {
                if (root.name == BreachFeatureSceneBuilder.FixtureName)
                {
                    Assert.That(Vector3.Distance(root.transform.position, StationOffset), Is.LessThan(Epsilon));
                    return root;
                }
            }

            Assert.Fail($"'{BreachFeatureSceneBuilder.FixtureName}' is not in the scene");
            return null;
        }

        private Transform Door(Vector3 feet, Quaternion rotation)
        {
            var door = new GameObject("Door");
            SceneManager.MoveGameObjectToScene(door, _scene);
            door.transform.SetPositionAndRotation(feet, rotation);
            return door.transform;
        }

        private TextAsset Text(string json)
        {
            var asset = new TextAsset(json) { name = "breach-room-test" };
            _assets.Add(asset);
            return asset;
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

using System;
using System.Collections.Generic;
using GameKit.Core;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Editor
{
    // Playground の右奥 (武器を出してはいけない場所の +Z) の空いた床に、明かりの無い部屋を置き、奥にステルスの敵を 1 人立たせる。
    // 部屋は壁と屋根で囲んで日の光を遮り、手前 (-Z) の壁の真ん中だけを入口に開ける。入口の脇の外にウェポンライトを拾える所を置く。
    // ライトを点けたときだけ早く気づかれるのを試せるよう、この敵だけ点灯の係数 (LitGainFactor) を上げる。
    public static class WeaponLightFeatureSceneBuilder
    {
        public const string FixtureName = "DarkRoom";

        public const string FrontLeftWallName = "DarkRoom_FrontLeft";
        public const string FrontRightWallName = "DarkRoom_FrontRight";
        public const string BackWallName = "DarkRoom_Back";
        public const string LeftWallName = "DarkRoom_Left";
        public const string RightWallName = "DarkRoom_Right";
        public const string RoofName = "DarkRoom_Roof";
        public const string NavMeshName = "DarkRoom_NavMesh";
        public const string LightPickupName = "DarkRoom_LightPickup";

        public const string EnemyName = "DarkRoomEnemy";
        public const string EnemyId = "dark-room-enemy";

        public const float RoomWidth = 8f;
        public const float RoomDepth = 12f;
        public const float WallHeight = 3f;
        public const float WallThickness = 0.4f;
        public const float RoofThickness = 0.2f;

        // 立ち 2 m・半径 0.5 m の体が通れる幅。
        public const float DoorWidth = 2f;

        // 高さは床の上下を含めて焼けるように取る。
        public const float NavMeshBakeHeight = 8f;

        public const float EnemyHeight = 2f;
        public const float EnemyRadius = 0.5f;
        public const float EnemySpeed = 2.5f;
        public const int EnemyMaxHealth = 100;

        // 奥の壁の手前に立たせ、入口までが視認距離に収まり、入口の外はわずかしか見えない距離にする。
        public const float EnemyBackGap = 2f;
        public const float SightRange = 12f;
        public const float AttackRange = 10f;

        // キットの既定 (AwarenessSpec.DefaultLitGainFactor = 1) のままでは点けても消しても同じ速さでたまるので、この敵だけ上げる。
        public const float LitGainFactor = 1.5f;

        // 入口から見えないよう、手前の壁の左の外 (-X) に置く。
        public static readonly Vector3 LightPickupPosition = new Vector3(-1.5f, 0f, 1f);
        public static readonly Vector3 LightPickupSize = Vector3.one;
        public const float LightPickupRadius = 1.5f;
        public const float LightPickupRespawnSeconds = 5f;

        // 武器を出してはいけない場所 (x = 22..41, z = 6..21) の奥 (+Z) に StationGap より離し、ほかの置く物に重ならない、置く物の根の位置。
        public static readonly Vector3 StationOffset = new Vector3(22f, 0f, 26f);

        // 入口の方 (-Z) を向かせる。
        public static readonly Quaternion EnemyFacing = Quaternion.LookRotation(Vector3.back, Vector3.up);

        public static Vector3 EnemyFeet => new Vector3(RoomWidth / 2f, 0f, RoomDepth - EnemyBackGap);

        public static Vector3 DoorCenter => new Vector3(RoomWidth / 2f, 0f, WallThickness / 2f);

        public static Bounds InteriorBounds => new Bounds(
            new Vector3(RoomWidth / 2f, WallHeight / 2f, RoomDepth / 2f),
            new Vector3(RoomWidth - (2f * WallThickness), WallHeight, RoomDepth - (2f * WallThickness)));

        public static Bounds LightPickupBounds => new Bounds(LightPickupPosition + (Vector3.up * (LightPickupSize.y / 2f)), LightPickupSize);

        public static Bounds NavMeshBounds => new Bounds(
            new Vector3(RoomWidth / 2f, 0f, RoomDepth / 2f), new Vector3(RoomWidth, NavMeshBakeHeight, RoomDepth));

        public static Bounds Footprint
        {
            get
            {
                var bounds = new Bounds(
                    new Vector3(RoomWidth / 2f, (WallHeight + RoofThickness) / 2f, RoomDepth / 2f),
                    new Vector3(RoomWidth, WallHeight + RoofThickness, RoomDepth));
                bounds.Encapsulate(LightPickupBounds);
                return bounds;
            }
        }

        public static ITpsRangeFixture Fixture()
        {
            return new DarkRoomFixture();
        }

        // 置く物の根から見た部屋の箱。手前の壁は入口の分だけ左右に分ける。
        public static IReadOnlyList<LevelBox> Boxes()
        {
            var t = WallThickness;
            var doorLeft = (RoomWidth - DoorWidth) / 2f;
            var doorRight = (RoomWidth + DoorWidth) / 2f;
            return new[]
            {
                Corners(FrontLeftWallName, new Vector3(0f, 0f, 0f), new Vector3(doorLeft, WallHeight, t)),
                Corners(FrontRightWallName, new Vector3(doorRight, 0f, 0f), new Vector3(RoomWidth, WallHeight, t)),
                Corners(BackWallName, new Vector3(0f, 0f, RoomDepth - t), new Vector3(RoomWidth, WallHeight, RoomDepth)),
                Corners(LeftWallName, new Vector3(0f, 0f, t), new Vector3(t, WallHeight, RoomDepth - t)),
                Corners(RightWallName, new Vector3(RoomWidth - t, 0f, t), new Vector3(RoomWidth, WallHeight, RoomDepth - t)),
                Corners(RoofName, new Vector3(0f, WallHeight, 0f), new Vector3(RoomWidth, WallHeight + RoofThickness, RoomDepth)),
            };
        }

        public static EnemySpec Spec() => new EnemySpec(
            SightRange,
            AttackRange,
            EnemySpec.DefaultLoseSightSeconds,
            EnemySpec.DefaultSearchSeconds,
            EnemySpec.DefaultReactionSeconds,
            EnemySpec.DefaultOpeningSpreadDegrees,
            EnemySpec.DefaultSettledSpreadDegrees,
            EnemySpec.DefaultOpeningShots,
            fireRange: SightRange,
            fireToleranceDegrees: EnemySpec.DefaultFireToleranceDegrees,
            rangeSpreadDegrees: EnemySpec.DefaultRangeSpreadDegrees);

        // 持ち物の表で最初に見つかるライトの付けられる物。名前でなく中身 (Attachment.IsLight) で選ぶ。
        public static string LightItemId(ItemCatalog items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            foreach (var item in items.Items)
            {
                if (item.IsAttachment() && item.Attachment().IsLight)
                {
                    return item.Id;
                }
            }

            return null;
        }

        // 持ち物の表を受け取るのは、テストがキットの Resources の表に頼らずライトの有る・無い表を渡せるようにするため。
        public static GameObject Place(Scene scene, GameRules rules, ItemCatalog items)
        {
            var weapon = FindBootstrapWeapon(scene);
            var lightItemId = LightItemId(items);
            if (lightItemId == null)
            {
                throw new InvalidOperationException(
                    $"{FeatureSceneBuilder.LogTag} {nameof(WeaponLightFeatureSceneBuilder)} needs an attachment with a light in the inventory items (Resources/Tuning/inventory.json)");
            }

            var root = new GameObject(FixtureName);
            SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                foreach (var box in Boxes())
                {
                    var placed = SceneBuildHelpers.CreateStaticBox(scene, root.transform, box.Name, box.Center, box.Size);
                    SceneBuildHelpers.SetSurfaceMaterial(placed, SurfaceImpactAssetStep.ConcreteId);
                }

                var enemy = SceneBuildHelpers.CreateEnemy(
                    scene,
                    root.transform,
                    EnemyName,
                    EnemyId,
                    EnemyFeet,
                    EnemyFacing,
                    Array.Empty<Vector3>(),
                    new EnemyBody(EnemyHeight, EnemyRadius, EnemySpeed, EnemyMaxHealth),
                    weapon);
                enemy.ApplySpec(Spec());
                var stealth = enemy.GetComponent<EnemyStealth>();
                if (stealth == null)
                {
                    throw new InvalidOperationException(
                        $"{FeatureSceneBuilder.LogTag} '{EnemyName}' has no {nameof(EnemyStealth)}; {nameof(EnemyStealthBuildStep)} is not registered with the enemy steps");
                }

                stealth.ApplySpec(stealth.CreateSpec().WithLitGainFactor(LitGainFactor));
                enemy.gameObject.SetActive(false);

                var navMesh = new GameObject(NavMeshName);
                navMesh.transform.SetParent(root.transform, false);
                navMesh.transform.localPosition = NavMeshBounds.center;
                var area = navMesh.AddComponent<RuntimeNavMeshArea>();
                area.Size = NavMeshBounds.size;
                area.AgentTypeId = SceneBuildHelpers.HumanoidAgentTypeId;
                area.ActivateAfterBuild = new[] { enemy.gameObject };

                var pickupObject = new GameObject(LightPickupName);
                pickupObject.transform.SetParent(root.transform, false);
                pickupObject.transform.localPosition = LightPickupPosition;
                var pickup = pickupObject.AddComponent<PickupSpawnPoint>();
                pickup.Contents = PickupContents.ForItem(lightItemId, 1);
                pickup.CollectMode = PickupCollectMode.Look;
                pickup.Radius = LightPickupRadius;
                pickup.RespawnSeconds = LightPickupRespawnSeconds;

                EditorSceneManager.MarkSceneDirty(scene);
                return root;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
        }

        private static LevelBox Corners(string name, Vector3 min, Vector3 max)
        {
            return new LevelBox(name, (min + max) / 2f, max - min);
        }

        // 置く物には武器が渡されないので、StealthRangeFixture と同じく先に置かれた GameBootstrap から引く。
        private static WeaponDefinition FindBootstrapWeapon(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var bootstrap = root.GetComponent<GameBootstrap>();
                if (bootstrap != null && bootstrap.WeaponDefinitions != null)
                {
                    foreach (var weapon in bootstrap.WeaponDefinitions)
                    {
                        if (weapon != null)
                        {
                            return weapon;
                        }
                    }
                }
            }

            throw new InvalidOperationException($"{FeatureSceneBuilder.LogTag} {nameof(WeaponLightFeatureSceneBuilder)} needs a GameBootstrap with a weapon placed before it");
        }
    }

    // public にすると TPS の射撃場の生成 (ExtensionPoints) が拾って射撃場にも置くので internal にする (FeatureSceneBuilder の置く物と同じ)。
    internal sealed class DarkRoomFixture : ITpsRangeFixture
    {
        public int Order => 0;

        public string Name => WeaponLightFeatureSceneBuilder.FixtureName;

        public Bounds Footprint => WeaponLightFeatureSceneBuilder.Footprint;

        public GameObject Place(Scene scene, GameRules rules)
        {
            return WeaponLightFeatureSceneBuilder.Place(scene, rules, KitTuning.Items);
        }
    }
}

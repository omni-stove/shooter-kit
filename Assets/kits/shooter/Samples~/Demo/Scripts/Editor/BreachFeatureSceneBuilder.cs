using System;
using System.IO;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Editor
{
    // Playground の武器を出してはいけない場所の並び (HolsterFeatureSceneBuilder) の +X に、ドア 2 枚の小部屋を置く。
    // 南 (-Z) のドアは蹴り開け、北 (+Z) のドアは爆薬で破る。爆薬のドアだけ品を求める JSON を持たせ、その品を南のドアの横に拾える所に置く。
    // 部屋の中には棒立ちの的と CPU を 1 体ずつ置き、どちらのドアの突入の箱 (BreachSpec.RoomWidth・RoomDepth) にも入る位置にする。
    public static class BreachFeatureSceneBuilder
    {
        public const string FixtureName = "BreachRoom";
        public const string WallsName = "BreachRoomWalls";
        public const string NavAreaName = "BreachRoomNav";

        public const string KickDoorName = "BreachDoor_Kick";
        public const string KickDoorId = "breach-room-kick";
        public const string ChargeDoorName = "BreachDoor_Charge";
        public const string ChargeDoorId = "breach-room-charge";

        public const string TargetId = "breach-room-target";
        public const string EnemyName = "BreachRoomEnemy";
        public const string EnemyId = "breach-room-enemy";
        public const string ChargePickupName = "BreachChargePickup";

        // キットの Tuning/inventory.json の突入用爆薬。ゲームが持ち物を差し替えたら、作られた JSON の Id を書き換える。
        public const string ChargeItemId = "5e3b9c1d-7a24-4f86-b0c3-9d1e2a4f6b78";

        // 中の広さは既定の突入の箱 (Tuning/breach の roomWidth 4・roomDepth 5) と同じにし、どちらのドアから破っても部屋の中の全員が箱に入るようにする。
        public const float RoomWidth = 4f;
        public const float RoomDepth = 5f;
        public const float WallHeight = 3f;
        public const float WallThickness = 0.3f;

        // プレイヤーのカプセル (直径 1 m) がくぐれる幅と高さ。
        public const float DoorwayWidth = 1.2f;
        public const float DoorwayHeight = 2.2f;
        public const float PanelThickness = 0.08f;

        // ドアの前で蹴る・仕掛ける人が立つ奥行き。Tuning/breach の reach (1.5 m) より広く取る。
        public const float ApproachDepth = 2.5f;

        public const float TargetOffsetX = -1f;
        public const float EnemyOffsetX = 1f;
        public const int TargetMaxHealth = 1000;

        public const float EnemyHeight = 2f;
        public const float EnemyRadius = 0.5f;
        public const float EnemySpeed = 3f;

        // 爆薬に巻き込まれても倒れず、何度でも試せる体力。
        public const int EnemyMaxHealth = 400;

        // 開いたドアの先 (前に立つ人) は見え、Playground のほかの置く物までは届かない距離。
        public const float EnemySightRange = 9f;
        public const float EnemyAttackRange = 8f;

        public const float NavBakeHeight = 4f;

        // 南のドアの横 (+X) の外。蹴る・仕掛ける人の立つ所と重ならない。
        public static readonly Vector3 ChargePickupPosition = new Vector3((RoomWidth / 2f) + WallThickness + 1.5f, 0f, -1.5f);

        public const float ChargePickupRadius = 1f;

        // 拾った後にまた試せるよう、もう一度出す。
        public const float ChargePickupRespawnSeconds = 10f;

        public static readonly Vector3 PanelSize = new Vector3(DoorwayWidth, DoorwayHeight, PanelThickness);

        public static Vector3 KickDoorFeet => Vector3.zero;

        public static Vector3 ChargeDoorFeet => new Vector3(0f, 0f, RoomDepth);

        // ドアの前 (+Z) を部屋の外へ向け、外に立つ人から見て奥 (部屋の中) を突入の箱にする。
        public static Quaternion KickDoorRotation => Quaternion.LookRotation(Vector3.back, Vector3.up);

        public static Quaternion ChargeDoorRotation => Quaternion.identity;

        public static Vector3 TargetFeet => new Vector3(TargetOffsetX, 0f, RoomDepth / 2f);

        public static Vector3 EnemyFeet => new Vector3(EnemyOffsetX, 0f, RoomDepth / 2f);

        // 蹴り開ける南のドアの方を向かせる。
        public static Quaternion InsideFacing => Quaternion.LookRotation(Vector3.back, Vector3.up);

        public static string ChargeTuningPath => ShooterKitRoot.OutputPath("Tuning/" + FeatureSceneBuilder.PlaygroundName + "BreachCharge.json");

        public static Bounds Footprint
        {
            get
            {
                var min = new Vector3(-(RoomWidth / 2f) - WallThickness, 0f, -ApproachDepth);
                var max = new Vector3(ChargePickupPosition.x + ChargePickupRadius, WallHeight, RoomDepth + ApproachDepth);
                var bounds = new Bounds();
                bounds.SetMinMax(min, max);
                return bounds;
            }
        }

        public static ITpsRangeFixture Fixture()
        {
            return new BreachRoomFixture();
        }

        // after の +X に StationGap を空け、手前 (-Z) の端をそろえて置く。
        public static FeatureStation StationAfter(Bounds after)
        {
            var footprint = Footprint;
            return new FeatureStation(
                Fixture(),
                new Vector3(after.max.x + FeatureSceneBuilder.StationGap - footprint.min.x, 0f, after.min.z - footprint.min.z));
        }

        // 壁 (東西の 2 枚と、南北それぞれ出入口の左右と上の 3 枚)。部屋の根から見た中心と大きさ。
        public static LevelBox[] Walls()
        {
            var side = (RoomWidth - DoorwayWidth) / 2f;
            var sideX = (DoorwayWidth / 2f) + (side / 2f);
            var lintel = WallHeight - DoorwayHeight;
            var walls = new[]
            {
                new LevelBox("Wall_West", new Vector3(-(RoomWidth / 2f) - (WallThickness / 2f), WallHeight / 2f, RoomDepth / 2f), new Vector3(WallThickness, WallHeight, RoomDepth + WallThickness)),
                new LevelBox("Wall_East", new Vector3((RoomWidth / 2f) + (WallThickness / 2f), WallHeight / 2f, RoomDepth / 2f), new Vector3(WallThickness, WallHeight, RoomDepth + WallThickness)),
                new LevelBox("Wall_South_West", new Vector3(-sideX, WallHeight / 2f, 0f), new Vector3(side, WallHeight, WallThickness)),
                new LevelBox("Wall_South_East", new Vector3(sideX, WallHeight / 2f, 0f), new Vector3(side, WallHeight, WallThickness)),
                new LevelBox("Wall_South_Lintel", new Vector3(0f, DoorwayHeight + (lintel / 2f), 0f), new Vector3(DoorwayWidth, lintel, WallThickness)),
                new LevelBox("Wall_North_West", new Vector3(-sideX, WallHeight / 2f, RoomDepth), new Vector3(side, WallHeight, WallThickness)),
                new LevelBox("Wall_North_East", new Vector3(sideX, WallHeight / 2f, RoomDepth), new Vector3(side, WallHeight, WallThickness)),
                new LevelBox("Wall_North_Lintel", new Vector3(0f, DoorwayHeight + (lintel / 2f), RoomDepth), new Vector3(DoorwayWidth, lintel, WallThickness)),
            };
            return walls;
        }

        public static EnemySpec RoomEnemySpec() => new EnemySpec(
            EnemySightRange,
            EnemyAttackRange,
            EnemySpec.DefaultLoseSightSeconds,
            EnemySpec.DefaultSearchSeconds,
            EnemySpec.DefaultReactionSeconds,
            EnemySpec.DefaultOpeningSpreadDegrees,
            EnemySpec.DefaultSettledSpreadDegrees,
            EnemySpec.DefaultOpeningShots,
            fireRange: EnemySightRange,
            fireToleranceDegrees: EnemySpec.DefaultFireToleranceDegrees,
            rangeSpreadDegrees: EnemySpec.DefaultRangeSpreadDegrees);

        // シーンの生成と smoke (Play 中に一時的に置く) の両方で使う。Play 中は SetActive で Awake が走って板ができるので、
        // Id・調整値・板の大きさを渡し終えるまで GameObject を止めておく。Edit Mode では Awake が走らないので、ここで板を作ってシーンに残す
        // (オンラインのマップの書き出しが、板の当たりをドアの組にする)。parent が null なら開いているシーンの根に置く。
        public static BreachDoor CreateDoor(
            Transform parent, string name, string doorId, Vector3 feet, Quaternion rotation, Vector3 panelSize, TextAsset tuning = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("name must not be empty", nameof(name));
            }

            if (string.IsNullOrWhiteSpace(doorId))
            {
                throw new ArgumentException("doorId must not be empty", nameof(doorId));
            }

            if (!(panelSize.x > 0f) || !(panelSize.y > 0f) || !(panelSize.z > 0f)
                || float.IsInfinity(panelSize.x) || float.IsInfinity(panelSize.y) || float.IsInfinity(panelSize.z))
            {
                throw new ArgumentOutOfRangeException(nameof(panelSize), panelSize, "a door panel needs a positive, finite width, height and thickness");
            }

            var gameObject = new GameObject(name);
            gameObject.SetActive(false);
            try
            {
                gameObject.transform.SetParent(parent, false);
                gameObject.transform.SetPositionAndRotation(feet, rotation);
                var visuals = gameObject.AddComponent<BreachDoorVisuals>();
                visuals.PanelSize = panelSize;
                var door = gameObject.AddComponent<BreachDoor>();
                door.Id = doorId;
                door.Tuning = tuning;
                door.Visuals = visuals;
                if (!Application.isPlaying)
                {
                    door.Bind();
                }

                gameObject.SetActive(true);
                return door;
            }
            catch
            {
                DestroySafely(gameObject);
                throw;
            }
        }

        // 棒立ちの的。標的は怯みの取り付け (StaggerActionInstaller) の対象 (EnemyAgent) でないので、ドアの突入で怯むよう怯みの部品をここで付ける。
        public static TargetDummy CreateTarget(Transform parent, string targetId, Vector3 feet, Quaternion rotation, int maxHealth)
        {
            var dummy = TargetDummy.Create(targetId, feet, rotation, parent, maxHealth);
            dummy.gameObject.AddComponent<StaggerReaction>();
            return dummy;
        }

        // 爆薬のドアだけが品を求めるよう、ドアごとの JSON に品の Id だけを書く。残りの値はゲーム全体の Tuning/breach を引き継ぐ。
        public static string ChargeTuningJson()
        {
            return "{\n  \"" + BreachJson.ChargeItemIdItem + "\": \"" + ChargeItemId + "\"\n}\n";
        }

        public static TextAsset LoadOrBuildChargeTuning(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path, ".json");
            var existing = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (existing != null)
            {
                // 読めない JSON をシーンに配線すると、遊ぶときに黙ってゲーム全体の値へ戻り、品が無くても仕掛けられてしまうので、作るときに止める。
                ChargeSpec(existing);
                return existing;
            }

            var json = ChargeTuningJson();
            BreachJson.Parse(json, path, KitTuning.Breach);
            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            File.WriteAllText(FullPath(path), json);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var built = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (built == null)
            {
                throw new IOException($"Failed to import the breach charge JSON '{path}'");
            }

            Debug.Log($"{FeatureSceneBuilder.LogTag} saved '{path}'");
            return built;
        }

        // 拾える所に置く品はドアの JSON の品にそろえる。品を求めない JSON や、持ち物の表に無い品なら、拾って仕掛けられないので止める。
        public static BreachSpec ChargeSpec(TextAsset tuning)
        {
            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }

            var spec = BreachJson.Parse(tuning.text, tuning.name, KitTuning.Breach);
            if (!spec.NeedsChargeItem)
            {
                throw new InvalidOperationException($"{FeatureSceneBuilder.LogTag} '{tuning.name}' must name the charge item the breach room hands out ({BreachJson.ChargeItemIdItem})");
            }

            if (!KitTuning.Items.TryFind(spec.ChargeItemId, out _))
            {
                throw new InvalidOperationException($"{FeatureSceneBuilder.LogTag} the charge item '{spec.ChargeItemId}' of '{tuning.name}' is not in the inventory tuning");
            }

            return spec;
        }

        internal static GameObject Place(Scene scene, TextAsset chargeTuning)
        {
            var spec = ChargeSpec(chargeTuning);
            var weapon = FindBootstrapWeapon(scene);
            var root = new GameObject(FixtureName);
            SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                var walls = new GameObject(WallsName);
                walls.transform.SetParent(root.transform, false);
                foreach (var wall in Walls())
                {
                    var box = SceneBuildHelpers.CreateStaticBox(scene, walls.transform, wall.Name, wall.Center, wall.Size);
                    SceneBuildHelpers.SetSurfaceMaterial(box, SurfaceImpactAssetStep.ConcreteId);
                }

                CreateDoor(root.transform, KickDoorName, KickDoorId, KickDoorFeet, KickDoorRotation, PanelSize);
                CreateDoor(root.transform, ChargeDoorName, ChargeDoorId, ChargeDoorFeet, ChargeDoorRotation, PanelSize, chargeTuning);

                CreateTarget(root.transform, TargetId, TargetFeet, InsideFacing, TargetMaxHealth);

                var nav = new GameObject(NavAreaName);
                nav.transform.SetParent(root.transform, false);
                nav.transform.localPosition = new Vector3(0f, 0f, RoomDepth / 2f);

                var enemy = SceneBuildHelpers.CreateEnemy(
                    scene,
                    nav.transform,
                    EnemyName,
                    EnemyId,
                    EnemyFeet,
                    InsideFacing,
                    Array.Empty<Vector3>(),
                    new EnemyBody(EnemyHeight, EnemyRadius, EnemySpeed, EnemyMaxHealth),
                    weapon);
                enemy.ApplySpec(RoomEnemySpec());
                enemy.gameObject.AddComponent<StaggerReaction>();
                enemy.gameObject.SetActive(false);

                var area = nav.AddComponent<RuntimeNavMeshArea>();
                area.Size = new Vector3(RoomWidth, NavBakeHeight, RoomDepth - WallThickness);
                area.AgentTypeId = SceneBuildHelpers.HumanoidAgentTypeId;
                area.ActivateAfterBuild = new[] { enemy.gameObject };

                var pickup = new GameObject(ChargePickupName);
                pickup.transform.SetParent(root.transform, false);
                pickup.transform.localPosition = ChargePickupPosition;
                var point = pickup.AddComponent<PickupSpawnPoint>();
                point.Contents = PickupContents.ForItem(spec.ChargeItemId, 1);
                point.CollectMode = PickupCollectMode.Touch;
                point.Radius = ChargePickupRadius;
                point.RespawnSeconds = ChargePickupRespawnSeconds;

                EditorSceneManager.MarkSceneDirty(scene);
                return root;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
        }

        // 置く物には武器が渡されないので、HolsterFeatureSceneBuilder と同じく先に置かれた GameBootstrap から引く。
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

            throw new InvalidOperationException($"{FeatureSceneBuilder.LogTag} {nameof(BreachFeatureSceneBuilder)} needs a GameBootstrap with a weapon placed before it");
        }

        private static string FullPath(string assetPath)
        {
            var project = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(project ?? string.Empty, assetPath);
        }

        private static void DestroySafely(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }

    // public にすると TPS の射撃場の生成 (ExtensionPoints) が拾って射撃場にも置くので internal にする (FeatureSceneBuilder の置く物と同じ)。
    internal sealed class BreachRoomFixture : ITpsRangeFixture
    {
        public int Order => 0;

        public string Name => BreachFeatureSceneBuilder.FixtureName;

        public Bounds Footprint => BreachFeatureSceneBuilder.Footprint;

        public GameObject Place(Scene scene, GameRules rules)
        {
            var tuning = BreachFeatureSceneBuilder.LoadOrBuildChargeTuning(BreachFeatureSceneBuilder.ChargeTuningPath);
            return BreachFeatureSceneBuilder.Place(scene, tuning);
        }
    }
}

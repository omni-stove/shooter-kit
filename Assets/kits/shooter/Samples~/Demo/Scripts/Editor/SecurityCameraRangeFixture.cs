using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Editor
{
    // ステルスの囲い (StealthRangeFixture) の北、床の北の端に沿う細長い場所 (x = -38..-1.5, z = 30..37.5) に置く。
    // 警報の半径 (stealth.json の alarm.radius、既定 30 m) の外に見張りを 1 人置くには、見つかる場所から 30 m より離れた所が要るので、床の幅いっぱいに延ばす。
    // 北の壁に首を振るカメラを 2 台付け、あいだを仕切りで分ける。片方の前に立つ間、もう片方から見えないように。
    // 射撃場ではカメラと見張りを無効のまま置き、smoke の手順 (SecurityCameraSmokeStep) が自分の番に有効にする。ほかの手順の間に見つかって警報が鳴らないように。
    // Playground では手で試せるよう、カメラは始めから動かし、見張りは NavMesh を焼いてから起こす。
    public sealed class SecurityCameraRangeFixture : ITpsRangeFixture
    {
        public const string RootName = "SecurityCameraYard";
        public const string BackWallName = "SecurityCameraYard_BackWall";
        public const string DividerName = "SecurityCameraYard_Divider";
        public const string NavFloorName = "NavFloor";
        public const string NearSlabName = "NearSlab";
        public const string FarSlabName = "FarSlab";
        public const string AlarmName = "SecurityAlarm";

        public const string EastCameraName = "SecurityCamera_East";
        public const string EastCameraId = "yard-camera-east";
        public const string WestCameraName = "SecurityCamera_West";
        public const string WestCameraId = "yard-camera-west";

        public const string NearGuardName = "YardGuard_Near";
        public const string NearGuardId = "yard-guard-near";
        public const string FarGuardName = "YardGuard_Far";
        public const string FarGuardId = "yard-guard-far";

        public const int FixtureOrder = 580;

        public const float WallHeight = 3.5f;
        public const float WallThickness = 0.4f;

        // 背の高い人の頭より上で、真下に立てば長押しで止められる (SecurityCameraSpec.DefaultDisableRadius) 高さ。
        public const float CameraHeight = 2.2f;

        // 首の箱 (SecurityCamera.BodySize) の後ろが北の壁に埋まらない位置。
        public const float CameraZ = 36.8f;

        // 見つかる前にゲージが近くの速さ (AwarenessSpec の fullGainDistance) に近い量でたまる距離。
        public const float StandDistance = 3.5f;

        // カメラの横に立つときの離れ方。首を端まで振っても視野 (首振りの半分 + 視野の半分) の外で、止める距離の内側に収まる。
        public const float BesideOffset = 1.2f;

        // 横に立つときに壁から離す分。カメラの目 (SecurityCamera.EyeLocalPosition) とほぼ同じ前後の位置に立たせる。
        public const float BesideForward = 0.35f;

        public const float NavFloorThickness = 0.1f;

        // 床の Plane (y=0) と面を重ねると、敵の足がどちらの面に乗るかが定まらないので、わずかに上げる。
        public const float NavFloorTop = 0.02f;

        public static readonly Vector3 Center = new Vector3(-19.75f, 0f, 33.75f);
        public static readonly Vector3 Size = new Vector3(36.5f, WallHeight, 7.5f);

        public static readonly Vector3 BackWallCenter = new Vector3(-19.75f, WallHeight / 2f, 37.3f);
        public static readonly Vector3 BackWallSize = new Vector3(36.5f, WallHeight, WallThickness);

        public static readonly Vector3 DividerCenter = new Vector3(-10f, WallHeight / 2f, 33.55f);
        public static readonly Vector3 DividerSize = new Vector3(WallThickness, WallHeight, 7.1f);

        public static readonly Vector3 EastCameraMount = new Vector3(-4f, CameraHeight, CameraZ);
        public static readonly Vector3 WestCameraMount = new Vector3(-16f, CameraHeight, CameraZ);

        public static readonly Quaternion CameraFacing = Quaternion.LookRotation(Vector3.back, Vector3.up);

        public static readonly Vector3 AlarmPosition = new Vector3(-10f, WallHeight - 0.3f, CameraZ);

        // 近い見張りは仕切りの東で仕切りを向かせ、東のカメラの前に立つプレイヤーを背にさせる。警報で振り向いて調べに来るように。
        public static readonly Vector3 NearGuardGround = new Vector3(-8.8f, 0f, 31f);

        // 遠い見張りは床の西の端で西の外壁を向かせ、東のカメラの前に立つ所から警報の半径より離す。
        public static readonly Vector3 FarGuardGround = new Vector3(-36.8f, 0f, 34.5f);

        public static readonly Quaternion GuardFacing = Quaternion.LookRotation(Vector3.left, Vector3.up);

        public static readonly Vector3 NearSlabCenter = new Vector3(-5.65f, 0f, 33.55f);
        public static readonly Vector2 NearSlabSize = new Vector2(8.1f, 6.9f);

        public static readonly Vector3 FarSlabCenter = new Vector3(-36.75f, 0f, 34.5f);
        public static readonly Vector2 FarSlabSize = new Vector2(2.3f, 4f);

        public static readonly EnemyBody GuardBody = new EnemyBody(2f, 0.5f, 3.5f, Damageable.DefaultMaxHealth);

        private readonly bool _startsAwake;

        // ExtensionPoints は引数なしで作るので、射撃場ではカメラと見張りを眠らせて置く。
        public SecurityCameraRangeFixture()
            : this(false)
        {
        }

        public SecurityCameraRangeFixture(bool startsAwake)
        {
            _startsAwake = startsAwake;
        }

        public bool StartsAwake => _startsAwake;

        public int Order => FixtureOrder;

        public string Name => RootName;

        public Bounds Footprint => new Bounds(Center + new Vector3(0f, Size.y / 2f, 0f), Size);

        public static Vector3 EastStand => Ground(EastCameraMount) + (CameraFacing * Vector3.forward * StandDistance);

        public static Vector3 WestStand => Ground(WestCameraMount) + (CameraFacing * Vector3.forward * StandDistance);

        public static Vector3 WestBeside => Ground(WestCameraMount)
            + (CameraFacing * Vector3.right * BesideOffset)
            + (CameraFacing * Vector3.forward * BesideForward);

        public GameObject Place(Scene scene, GameRules rules)
        {
            var root = new GameObject(RootName);
            try
            {
                SceneManager.MoveGameObjectToScene(root, scene);
                root.transform.position = Center;

                SceneBuildHelpers.CreateStaticBox(scene, root.transform, BackWallName, BackWallCenter, BackWallSize);
                SceneBuildHelpers.CreateStaticBox(scene, root.transform, DividerName, DividerCenter, DividerSize);

                var cameras = new[]
                {
                    CreateCamera(root.transform, EastCameraName, EastCameraId, EastCameraMount),
                    CreateCamera(root.transform, WestCameraName, WestCameraId, WestCameraMount),
                };
                foreach (var camera in cameras)
                {
                    camera.SetActive(_startsAwake);
                }

                var alarm = new GameObject(AlarmName);
                alarm.transform.SetParent(root.transform, false);
                alarm.transform.position = AlarmPosition;
                alarm.AddComponent<SecurityAlarm>();

                var navFloor = new GameObject(NavFloorName);
                navFloor.transform.SetParent(root.transform, false);
                navFloor.transform.position = new Vector3(Center.x, 0f, Center.z);
                CreateSlab(navFloor.transform, NearSlabName, NearSlabCenter, NearSlabSize);
                CreateSlab(navFloor.transform, FarSlabName, FarSlabCenter, FarSlabSize);

                var guards = new List<GameObject>();
                var weapon = MeleeEnemyRangeFixture.FindEnemyWeapon(scene);
                if (weapon != null)
                {
                    guards.Add(CreateGuard(scene, root.transform, NearGuardName, NearGuardId, NearGuardGround, weapon));
                    guards.Add(CreateGuard(scene, root.transform, FarGuardName, FarGuardId, FarGuardGround, weapon));
                }
                else
                {
                    Debug.LogWarning($"[{nameof(SecurityCameraRangeFixture)}] the range has no weapon for the guards; placing the cameras and the alarm only");
                }

                root.AddComponent<NavMeshBakeActivator>().Configure(
                    navFloor.transform, _startsAwake ? guards : new List<GameObject>(), SceneBuildHelpers.HumanoidAgentTypeId);

                EditorSceneManager.MarkSceneDirty(scene);
                return root;
            }
            catch
            {
                Object.DestroyImmediate(root);
                throw;
            }
        }

        private static Vector3 Ground(Vector3 point)
        {
            return new Vector3(point.x, 0f, point.z);
        }

        // 首の形はカメラが Awake で作る (SecurityCameraCatalog のプレハブ、無ければ箱と筒) ので、ここでは置き場所と Id だけを決める。
        private static GameObject CreateCamera(Transform parent, string name, string id, Vector3 mount)
        {
            var camera = new GameObject(name);
            camera.transform.SetParent(parent, false);
            camera.transform.SetPositionAndRotation(mount, CameraFacing);
            var component = camera.AddComponent<SecurityCamera>();
            var serialized = new SerializedObject(component);
            serialized.FindProperty("_id").stringValue = id;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return camera;
        }

        private static void CreateSlab(Transform parent, string name, Vector3 center, Vector2 size)
        {
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = name;
            slab.transform.SetParent(parent, false);
            slab.transform.position = new Vector3(center.x, NavFloorTop - (NavFloorThickness / 2f), center.z);
            slab.transform.localScale = new Vector3(size.x, NavFloorThickness, size.y);
        }

        // 射撃場は手順の場所が近いので、キットの既定の視界のままだとほかの手順に立つプレイヤーまで見えてしまう。囲いの見張りと同じ視界にする。
        private static GameObject CreateGuard(Scene scene, Transform parent, string name, string id, Vector3 ground, WeaponDefinition weapon)
        {
            var guard = SceneBuildHelpers.CreateEnemy(
                scene,
                parent,
                name,
                id,
                ground + new Vector3(0f, NavFloorTop, 0f),
                GuardFacing,
                new List<Vector3>(),
                GuardBody,
                weapon);
            guard.ApplySpec(StealthRangeFixture.Spec());

            // 焼く前に有効だと NavMeshAgent が NavMesh に乗れずエラーになるので、Playground でも焼いてから起こす。
            guard.gameObject.SetActive(false);
            return guard.gameObject;
        }
    }
}

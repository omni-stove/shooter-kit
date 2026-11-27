using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Editor
{
    // Playground の右奥 (小屋とグラップルの壁より +X) の空いた床に、反応ごとの武器を出してはいけない場所を 2 x 2 に並べる。
    // 警告してしばらくで敵対するゾーンの +X の外に、そのゾーンを見張る中立の警備を 1 人立たせる。
    public static class HolsterFeatureSceneBuilder
    {
        public const string FixtureName = "WeaponFreeZones";
        public const string GuardPostName = "GuardPost";
        public const string GuardName = "ZoneGuard";
        public const string GuardId = "zone-guard";

        public const string ZoneNamePrefix = "WeaponFreeZone_";

        public const float ZoneHeight = 3f;
        public const float ZoneWidth = 6f;
        public const float ZoneGap = 3f;

        // 警備の足元だけに NavMesh を焼く広さ。ゾーンのトリガーを焼く範囲に入れないよう、ゾーンの縁から GuardPostGap 離す。
        public const float GuardPostWidth = 3f;
        public const float GuardPostGap = 1f;

        // 高さは床の上下を含めて焼けるように取る。
        public const float GuardPostBakeHeight = 4f;

        public const float GuardHeight = 2f;
        public const float GuardRadius = 0.5f;
        public const float GuardSpeed = 3f;
        public const int GuardMaxHealth = 100;

        // 見張るゾーンの奥の縁まで届き、出現位置 (原点) やほかの置く物の場所までは届かない距離。
        public const float GuardSightRange = 14f;
        public const float GuardAttackRange = 12f;

        // 小屋 (x = 12..16) とグラップルの壁 (x = 21..33, z = -8..-1) に重ならない、並びの -X・-Z の角。
        public static readonly Vector3 StationOffset = new Vector3(22f, 0f, 6f);

        public static readonly Vector3 ZoneSize = new Vector3(ZoneWidth, ZoneHeight, ZoneWidth);

        public static readonly IReadOnlyList<WeaponFreeZoneReaction> Reactions = new[]
        {
            WeaponFreeZoneReaction.WarnOnly,
            WeaponFreeZoneReaction.WarnThenHostile,
            WeaponFreeZoneReaction.AutoHolster,
            WeaponFreeZoneReaction.BlockDraw,
        };

        public const WeaponFreeZoneReaction GuardedReaction = WeaponFreeZoneReaction.WarnThenHostile;

        public static ITpsRangeFixture Fixture()
        {
            return new HolsterZonesFixture();
        }

        public static string ZoneName(WeaponFreeZoneReaction reaction)
        {
            return ZoneNamePrefix + reaction;
        }

        public static string ZoneId(WeaponFreeZoneReaction reaction)
        {
            switch (reaction)
            {
                case WeaponFreeZoneReaction.WarnOnly:
                    return "zone-warn-only";
                case WeaponFreeZoneReaction.WarnThenHostile:
                    return "zone-warn-then-hostile";
                case WeaponFreeZoneReaction.AutoHolster:
                    return "zone-auto-holster";
                case WeaponFreeZoneReaction.BlockDraw:
                    return "zone-block-draw";
                default:
                    throw new ArgumentOutOfRangeException(nameof(reaction), reaction, "unknown weapon-free zone reaction");
            }
        }

        // 置く物の根から見たゾーンの床の中心。Reactions の並びの順に、+X へ 2 つ、+Z へ 2 列。
        public static Vector3 ZoneFloorCenter(WeaponFreeZoneReaction reaction)
        {
            var index = IndexOf(reaction);
            var column = index % 2;
            var row = index / 2;
            return new Vector3(
                (column * (ZoneWidth + ZoneGap)) + (ZoneWidth / 2f),
                0f,
                (row * (ZoneWidth + ZoneGap)) + (ZoneWidth / 2f));
        }

        public static Bounds ZoneBounds(WeaponFreeZoneReaction reaction)
        {
            return new Bounds(ZoneFloorCenter(reaction) + (Vector3.up * (ZoneHeight / 2f)), ZoneSize);
        }

        public static Vector3 GuardPostCenter
        {
            get
            {
                var zone = ZoneFloorCenter(GuardedReaction);
                return zone + new Vector3((ZoneWidth / 2f) + GuardPostGap + (GuardPostWidth / 2f), 0f, 0f);
            }
        }

        public static Bounds GuardPostBounds => new Bounds(
            GuardPostCenter + (Vector3.up * (GuardHeight / 2f)), new Vector3(GuardPostWidth, GuardHeight, GuardPostWidth));

        // 見張るゾーンの方 (-X) を向かせる。
        public static Quaternion GuardFacing => Quaternion.LookRotation(Vector3.left, Vector3.up);

        public static Bounds Footprint
        {
            get
            {
                var bounds = ZoneBounds(Reactions[0]);
                foreach (var reaction in Reactions)
                {
                    bounds.Encapsulate(ZoneBounds(reaction));
                }

                bounds.Encapsulate(GuardPostBounds);
                return bounds;
            }
        }

        public static EnemySpec GuardSpec() => new EnemySpec(
            GuardSightRange,
            GuardAttackRange,
            EnemySpec.DefaultLoseSightSeconds,
            EnemySpec.DefaultSearchSeconds,
            EnemySpec.DefaultReactionSeconds,
            EnemySpec.DefaultOpeningSpreadDegrees,
            EnemySpec.DefaultSettledSpreadDegrees,
            EnemySpec.DefaultOpeningShots,
            fireRange: GuardSightRange,
            fireToleranceDegrees: EnemySpec.DefaultFireToleranceDegrees,
            rangeSpreadDegrees: EnemySpec.DefaultRangeSpreadDegrees);

        // シーンの生成と smoke (Play 中に一時的に置く) の両方で使う。Play 中は AddComponent で Awake が走るので、
        // 大きさと反応を渡し終えるまで GameObject を止めておく。parent が null なら開いているシーンの根に置く。
        public static WeaponFreeZone CreateZone(
            Transform parent, string name, string zoneId, WeaponFreeZoneSpec spec, Vector3 floorCenter, Vector3 size)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("name must not be empty", nameof(name));
            }

            if (string.IsNullOrWhiteSpace(zoneId))
            {
                throw new ArgumentException("zoneId must not be empty", nameof(zoneId));
            }

            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (!(size.x > 0f) || !(size.y > 0f) || !(size.z > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(size), size, "a weapon-free zone needs a positive size");
            }

            var gameObject = new GameObject(name);
            gameObject.SetActive(false);
            try
            {
                gameObject.transform.SetParent(parent, false);
                gameObject.transform.localPosition = floorCenter;
                var zone = gameObject.AddComponent<WeaponFreeZone>();
                zone.ZoneId = zoneId;
                zone.Size = size;
                zone.Configure(spec);
                zone.Build();
                gameObject.SetActive(true);
                return zone;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                throw;
            }
        }

        internal static GameObject Place(Scene scene, GameRules rules)
        {
            var weapon = FindBootstrapWeapon(scene);
            var root = new GameObject(FixtureName);
            SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                WeaponFreeZone guarded = null;
                foreach (var reaction in Reactions)
                {
                    var zone = CreateZone(
                        root.transform, ZoneName(reaction), ZoneId(reaction), new WeaponFreeZoneSpec(reaction), ZoneFloorCenter(reaction), ZoneSize);
                    if (reaction == GuardedReaction)
                    {
                        guarded = zone;
                    }
                }

                var post = new GameObject(GuardPostName);
                post.transform.SetParent(root.transform, false);
                post.transform.localPosition = GuardPostCenter;

                var guard = SceneBuildHelpers.CreateEnemy(
                    scene,
                    post.transform,
                    GuardName,
                    GuardId,
                    GuardPostCenter,
                    GuardFacing,
                    Array.Empty<Vector3>(),
                    new EnemyBody(GuardHeight, GuardRadius, GuardSpeed, GuardMaxHealth),
                    weapon);
                guard.ApplySpec(GuardSpec());
                var neutral = guard.gameObject.AddComponent<NeutralGuard>();
                neutral.Zone = guarded;
                guard.gameObject.SetActive(false);

                var area = post.AddComponent<RuntimeNavMeshArea>();
                area.Size = new Vector3(GuardPostWidth, GuardPostBakeHeight, GuardPostWidth);
                area.AgentTypeId = SceneBuildHelpers.HumanoidAgentTypeId;
                area.ActivateAfterBuild = new[] { guard.gameObject };

                EditorSceneManager.MarkSceneDirty(scene);
                return root;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
        }

        private static int IndexOf(WeaponFreeZoneReaction reaction)
        {
            for (var i = 0; i < Reactions.Count; i++)
            {
                if (Reactions[i] == reaction)
                {
                    return i;
                }
            }

            throw new ArgumentOutOfRangeException(nameof(reaction), reaction, "unknown weapon-free zone reaction");
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

            throw new InvalidOperationException($"{FeatureSceneBuilder.LogTag} {nameof(HolsterFeatureSceneBuilder)} needs a GameBootstrap with a weapon placed before it");
        }
    }

    // public にすると TPS の射撃場の生成 (ExtensionPoints) が拾って射撃場にも置くので internal にする (FeatureSceneBuilder の置く物と同じ)。
    internal sealed class HolsterZonesFixture : ITpsRangeFixture
    {
        public int Order => 0;

        public string Name => HolsterFeatureSceneBuilder.FixtureName;

        public Bounds Footprint => HolsterFeatureSceneBuilder.Footprint;

        public GameObject Place(Scene scene, GameRules rules)
        {
            return HolsterFeatureSceneBuilder.Place(scene, rules);
        }
    }
}

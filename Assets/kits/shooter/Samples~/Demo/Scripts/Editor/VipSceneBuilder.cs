using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Editor
{
    // 出現点は陣営 0 を手前 (z = 0)、陣営 1 を奥 (z = 40) に置く。VipRunner は攻守を入れ替えるたびに出現点の陣営も入れ替えるので、
    // 守る側はいつも奥から出て、手前の脱出地点まで攻める側の前を通って運ぶことになる。
    public sealed class VipSceneBuilder : ModeSceneBuilder
    {
        public const string ModeName = GameRulesAssetBuilder.VipName;

        public const string MenuPath = SceneBuildSteps.MenuPath;

        public const string ExitsRootName = "VipExits";

        // プレイヤーを合わせて 3 対 3 にする。
        public const int AlphaBots = 2;
        public const int BravoBots = 3;

        public const float ExitRadius = VipExitPoint.DefaultRadius;

        // 守る側の出現点から脱出地点までの水平の距離の下限 (m)。近いと、出てすぐ脱出して撃ち合いにならない。
        public const float MinExitDistanceFromDefenders = 25f;

        // 手前の両脇に置き、手前の出現点 (x = 0, ±6, ±12) と既定の遮蔽物 (x = ±10, z = 13 の低い壁) に掛からないようにする。
        public static readonly IReadOnlyList<Vector3> ExitCenters = new[]
        {
            new Vector3(-16f, 0f, 8f),
            new Vector3(16f, 0f, 8f),
        };

        public VipSceneBuilder()
            : base(ScenePathValue, RulesPathValue)
        {
        }

        public static string ScenePathValue => BuildSceneList.VipScene;

        public static string RulesPathValue => GameRulesAssetBuilder.VipPath;

        public static string DefinitionPath => ModeDefinitionAssets.PathOf(ModeName);

        // Vip の最初の攻守と同じ (一致はテストで確かめる)。
        public static Side FirstAttacker => Side.Alpha;

        public static Side FirstDefender => Side.Bravo;

        protected override IReadOnlyList<ModeSideLayout> Sides => Layout();

        public static IReadOnlyList<ModeSideLayout> Layout() => TwoSides(AlphaBots, BravoBots);

        public static void Build()
        {
            GameRulesAssetBuilder.LoadOrBuildModeRules(ModeName);
            new VipSceneBuilder().BuildSceneFromMenu();
        }

        public static string ExitObjectName(int index)
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "index must not be negative");
            }

            return "VipExit-" + (index + 1);
        }

        // 新しく作るときは調整値の JSON (vip.json) の値から始め、コードの既定値を写さない。
        public static VipDefinition LoadOrCreateDefinition(string path)
        {
            return ModeDefinitionAssets.LoadOrCreate<VipDefinition>(path, d => d.CopyFrom(KitTuning.Vip));
        }

        public static float NearestSpawnDistance(Vector3 point, ModeSideLayout side)
        {
            var nearest = float.PositiveInfinity;
            foreach (var spawn in side.Spawns)
            {
                var offset = spawn.Feet - point;
                offset.y = 0f;
                nearest = Mathf.Min(nearest, offset.magnitude);
            }

            return nearest;
        }

        public static IReadOnlyList<Vector3> ValidateExits(IReadOnlyList<Vector3> exits, IReadOnlyList<ModeSideLayout> sides, Side defender)
        {
            if (exits == null || exits.Count == 0)
            {
                throw new InvalidOperationException("a VIP scene needs at least one exit point");
            }

            if (sides == null)
            {
                throw new ArgumentNullException(nameof(sides));
            }

            ModeSideLayout? defenders = null;
            foreach (var side in sides)
            {
                if (side.Side == defender)
                {
                    defenders = side;
                }
            }

            if (!defenders.HasValue)
            {
                throw new InvalidOperationException($"the defending side {defender.Id} is not laid out");
            }

            foreach (var exit in exits)
            {
                var distance = NearestSpawnDistance(exit, defenders.Value);
                if (distance < MinExitDistanceFromDefenders)
                {
                    throw new InvalidOperationException(
                        $"exit point {exit} is {distance.ToString("0.##", CultureInfo.InvariantCulture)}m from a spawn point of the defending side {defender.Id}; keep it at least {MinExitDistanceFromDefenders}m away");
                }
            }

            return exits;
        }

        protected override ModeRunnerBase AddRunner(GameObject host, ModeSceneContext context)
        {
            // NewScene(Single) は参照されていないアセットをアンロードするので、シーンを作った後のここで読む。
            var definition = LoadOrCreateDefinition(DefinitionPath);

            var runner = host.AddComponent<VipRunner>();
            runner.Definition = definition;
            return runner;
        }

        protected override void AddModeObjects(ModeSceneContext context)
        {
            if (!(context.Runner is VipRunner runner))
            {
                throw new InvalidOperationException($"{GetType().Name}.AddRunner must add a VipRunner");
            }

            var sides = Sides;
            var centers = ValidateExits(ExitCenters, sides, FirstDefender);
            SceneBuildHelpers.RequireOnNavMesh(centers, NavMeshCheckRadius, "VIP exit point");

            // 攻守を入れ替えても守る側は最初に守る側の出現点から出るので、VIP が出る場所はそこだけを見ればよい。
            foreach (var side in sides)
            {
                if (side.Side != FirstDefender)
                {
                    continue;
                }

                foreach (var spawn in side.Spawns)
                {
                    SceneBuildHelpers.RequireReachable(spawn.Feet, centers, NavMeshCheckRadius, "VIP exit point");
                }
            }

            var root = new GameObject(ExitsRootName);
            SceneManager.MoveGameObjectToScene(root, context.Scene);

            var exits = new VipExitPoint[centers.Count];
            for (var i = 0; i < exits.Length; i++)
            {
                var go = new GameObject(ExitObjectName(i));
                go.transform.SetParent(root.transform, false);
                go.transform.position = centers[i];

                var exit = go.AddComponent<VipExitPoint>();
                exit.Radius = ExitRadius;
                exits[i] = exit;
            }

            runner.SetExitPoints(exits);
        }
    }
}

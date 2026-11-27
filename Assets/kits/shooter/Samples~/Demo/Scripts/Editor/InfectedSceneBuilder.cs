using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEngine;

namespace Shooter.Editor
{
    // bot はみな生存者の陣営に置く。感染者の陣営の出現点は、感染した人が戻る先として置く。
    public sealed class InfectedSceneBuilder : ModeSceneBuilder
    {
        public const string ModeName = GameRulesAssetBuilder.InfectedName;

        public const string MenuPath = SceneBuildSteps.MenuPath;

        // プレイヤーを合わせて 5 人の生存者から始める。
        public const int SurvivorBots = 4;

        // ボットが持つ carbine と分け、感染者は pistol に持ち替える。
        public const string InfectedWeaponId = WeaponAssetBuilder.PistolId;

        public InfectedSceneBuilder()
            : base(ScenePathValue, RulesPathValue)
        {
        }

        public static string ScenePathValue => BuildSceneList.InfectedScene;

        public static string RulesPathValue => GameRulesAssetBuilder.InfectedPath;

        public static string DefinitionPath => ModeDefinitionAssets.PathOf(ModeName);

        protected override IReadOnlyList<ModeSideLayout> Sides => TwoSides(SurvivorBots, 0);

        public static void Build()
        {
            GameRulesAssetBuilder.LoadOrBuildModeRules(ModeName);
            new InfectedSceneBuilder().BuildSceneFromMenu();
        }

        public static InfectedDefinition LoadOrCreateDefinition(string path, WeaponCatalogAsset catalog)
        {
            var definition = ModeDefinitionAssets.LoadOrCreate<InfectedDefinition>(path, d =>
            {
                d.CopyFrom(InfectedSpec.Default);
                d.InfectedWeapon = WeaponAssetBuilder.FindInCatalog(catalog, ModeName, InfectedWeaponId);
            });

            // 既にあるアセットは手で直した値を残すが、武器のアセットが消されて参照が切れたときだけ既定の武器を入れ直す。
            if (definition.InfectedWeapon == null)
            {
                definition.InfectedWeapon = WeaponAssetBuilder.FindInCatalog(catalog, ModeName, InfectedWeaponId);
                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssetIfDirty(definition);
            }

            return definition;
        }

        protected override ModeRunnerBase AddRunner(GameObject host, ModeSceneContext context)
        {
            // NewScene(Single) は参照されていないアセットをアンロードするので、シーンを作った後のここで読む。
            var definition = LoadOrCreateDefinition(DefinitionPath, context.Catalog);

            var runner = host.AddComponent<InfectedRunner>();
            runner.Definition = definition;
            runner.WeaponCatalog = context.Catalog;
            return runner;
        }
    }
}

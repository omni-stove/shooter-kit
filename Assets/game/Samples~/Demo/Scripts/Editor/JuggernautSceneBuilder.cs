using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.Editor
{
    // 並びは FreeForAllSceneBuilder と同じ (1 人ずつ別の陣営で、中央を挟んで往復する) にする。ジャガーノートは試合が始まってから選ばれるので、置き方では決めない。
    public sealed class JuggernautSceneBuilder : ModeSceneBuilder
    {
        public const string ModeName = GameRulesAssetBuilder.JuggernautName;

        public const string MenuPath = SceneBuildSteps.MenuPath;

        public const int BotCount = FreeForAllSceneBuilder.BotCount;

        public JuggernautSceneBuilder()
            : base(ScenePathValue, RulesPathValue)
        {
        }

        // BuildSceneList にはつなぎ込みで足すので、それまでは同じ組み方 (出力先ルートの Scenes/<モード名>) でここに持つ。
        public static string ScenePathValue => ShooterKitRoot.OutputScene(ModeName);

        public static string RulesPathValue => GameRulesAssetBuilder.JuggernautPath;

        public static string DefinitionPath => ModeDefinitionAssets.PathOf(ModeName);

        protected override IReadOnlyList<ModeSideLayout> Sides => FreeForAllSceneBuilder.Layout(BotCount);

        public static void Build()
        {
            GameRulesAssetBuilder.LoadOrBuildModeRules(ModeName);
            new JuggernautSceneBuilder().BuildSceneFromMenu();
        }

        public static JuggernautDefinition LoadOrCreateDefinition(string path)
        {
            return ModeDefinitionAssets.LoadOrCreate<JuggernautDefinition>(path, d =>
            {
                d.CopyFrom(JuggernautSpec.Default);
                d.RespawnSeconds = JuggernautRunner.DefaultRespawnSeconds;
            });
        }

        protected override IReadOnlyList<Vector3> BotPatrolRoute(IReadOnlyList<ModeSideLayout> sides, int sideIndex, int spawnIndex)
        {
            var own = sides[sideIndex].Spawns[spawnIndex].Feet;
            return new[] { own, FreeForAllSceneBuilder.Opposite(own) };
        }

        protected override ModeRunnerBase AddRunner(GameObject host, ModeSceneContext context)
        {
            // NewScene(Single) は参照されていないアセットをアンロードするので、シーンを作った後のここで読む。
            var definition = LoadOrCreateDefinition(DefinitionPath);

            var runner = host.AddComponent<JuggernautRunner>();
            runner.Definition = definition;
            return runner;
        }
    }
}

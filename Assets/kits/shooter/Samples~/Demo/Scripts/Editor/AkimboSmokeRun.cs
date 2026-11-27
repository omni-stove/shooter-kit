using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Shooter.Editor
{
    // batchmode には入力も照準も無いので、拾うのは Pickup.TryCollect、撃つのは WeaponController.PullTrigger(手)、込めるのは WeaponController.PressReload を直に呼ぶ。
    // 武器をロードアウトに直に足さず Playground に置いた拾える物から拾うのは、シーンの組み立て (FeatureSceneBuilder) から二丁持ちになるまでを通すため。
    // Play Mode に入るので、batchmode で呼ぶときは -quit を付けない。
    [InitializeOnLoad]
    public static class AkimboSmokeRun
    {
        public const string LogTag = "[AkimboSmokeRun]";
        public const string MenuPath = "Shooter/Smoke/Run Akimbo Smoke";

        public const float WarmUpSeconds = 1f;

        public const float EnterTimeoutSeconds = 120f;

        public const float StepSeconds = 0.1f;

        public const int SuccessExitCode = 0;
        public const int FailureExitCode = 1;

        private const string ActiveKey = "Shooter.AkimboSmokeRun.Active";
        private const string RequestedAtKey = "Shooter.AkimboSmokeRun.RequestedAt";

        private static readonly List<string> _errors = new List<string>();
        private static readonly List<string> _combatLines = new List<string>();
        private static double _playStartedAt = -1d;
        private static bool _finished;

        static AkimboSmokeRun()
        {
            if (SessionState.GetBool(ActiveKey, false))
            {
                Attach();
            }
        }

        public static string ScenePath => BuildSceneList.PlaygroundScene;

        // 右・左の順に 1 発ずつ撃ち、リロードのボタンで両手を込める。込め始めの行は右・左の順に出る (WeaponController.Started)。
        public static IReadOnlyList<string> ExpectedLines(int rightAmmoAfterShot, int leftAmmoAfterShot)
        {
            return new[]
            {
                AkimboLog.Fire(AkimboHand.Right, rightAmmoAfterShot),
                AkimboLog.Fire(AkimboHand.Left, leftAmmoAfterShot),
                AkimboLog.Reload(AkimboHand.Right),
                AkimboLog.Reload(AkimboHand.Left),
            };
        }

        // ほかの [Combat] の行 (拾った・持ち替えた) が間に入るので、並びが同じかだけを見る。
        public static string CheckLines(IReadOnlyList<string> seen, IReadOnlyList<string> expected)
        {
            if (seen == null)
            {
                throw new ArgumentNullException(nameof(seen));
            }

            if (expected == null)
            {
                throw new ArgumentNullException(nameof(expected));
            }

            var next = 0;
            foreach (var line in seen)
            {
                if (next < expected.Count && string.Equals(line, expected[next], StringComparison.Ordinal))
                {
                    next++;
                }
            }

            return next == expected.Count
                ? null
                : $"never saw '{expected[next]}' after {next} of the {expected.Count} expected line(s); saw [{string.Join(" | ", seen)}]";
        }

        [MenuItem(MenuPath)]
        public static void Run()
        {
            try
            {
                // メニューから呼ばれたとき、開いているシーンの未保存の変更を黙って捨てない。
                if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    Finish(FailureExitCode, "cancelled: the open scene was not saved");
                    return;
                }

                // 拾える拳銃は JSON の akimbo 部品を読んだ武器のアセットを指すので、先にアセットを作り直してから Playground を組む。
                WeaponAssetBuilder.Build();
                GameRulesAssetBuilder.Build();
                FeatureSceneBuilder.BuildAt(
                    FeatureSceneBuilder.Playground(), ScenePath, GameRulesAssetBuilder.FeatureRulesPath(FeatureSceneBuilder.PlaygroundName));
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

                _errors.Clear();
                _combatLines.Clear();
                _playStartedAt = -1d;
                _finished = false;
                SessionState.SetBool(ActiveKey, true);
                SessionState.SetFloat(RequestedAtKey, (float)EditorApplication.timeSinceStartup);
                Attach();

                Debug.Log($"{LogTag} entering play mode in '{ScenePath}'");
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Finish(FailureExitCode, $"could not start: {e.Message}");
            }
        }

        private static void Attach()
        {
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
        }

        private static void Detach()
        {
            Application.logMessageReceived -= OnLog;
            EditorApplication.update -= OnUpdate;
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (_finished || !EditorApplication.isPlaying)
            {
                return;
            }

            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                // ほかの SmokeRun と同じく、Editor の起動時インデックスが Play 中に投げる例外はゲームのコードではないので外す。
                if (stackTrace != null && stackTrace.Contains("UnityEditor.Search.SearchInit.IndexationOnStartup"))
                {
                    return;
                }

                _errors.Add($"{type}: {condition}");
                return;
            }

            if (condition != null && condition.StartsWith(AkimboLog.LogTag + " ", StringComparison.Ordinal))
            {
                _combatLines.Add(condition);
            }
        }

        private static void OnUpdate()
        {
            if (_finished)
            {
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            if (!EditorApplication.isPlaying)
            {
                var requestedAt = SessionState.GetFloat(RequestedAtKey, (float)now);
                if (now - requestedAt > EnterTimeoutSeconds)
                {
                    Finish(FailureExitCode, $"did not enter play mode within {EnterTimeoutSeconds}s");
                }

                return;
            }

            if (_playStartedAt < 0d)
            {
                _playStartedAt = now;
                return;
            }

            if (now - _playStartedAt < WarmUpSeconds)
            {
                return;
            }

            var problems = new List<string>();
            try
            {
                Exercise(problems);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                problems.Add($"{e.GetType().Name}: {e.Message}");
            }

            problems.AddRange(_errors);
            if (problems.Count == 0)
            {
                Finish(SuccessExitCode, "passed");
            }
            else
            {
                Finish(FailureExitCode, "failed:\n  " + string.Join("\n  ", problems));
            }
        }

        private static void Exercise(List<string> problems)
        {
            var bootstrap = UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
            if (bootstrap == null || bootstrap.World == null)
            {
                problems.Add("the scene has no GameBootstrap that built the player");
                return;
            }

            var controller = bootstrap.World.Weapon;
            if (controller == null || !controller.IsInitialized)
            {
                problems.Add("the player's WeaponController is not initialized");
                return;
            }

            var player = bootstrap.World.PlayerDamageable;
            if (player == null)
            {
                problems.Add("the player has no Damageable to pick the pistol up with");
                return;
            }

            var pistolRoot = GameObject.Find(BootstrapSceneBuilder.AkimboPistolName);
            var spawnPoint = pistolRoot != null ? pistolRoot.GetComponentInChildren<PickupSpawnPoint>(true) : null;
            if (spawnPoint == null || spawnPoint.Contents == null || spawnPoint.Contents.Weapon == null)
            {
                problems.Add($"'{ScenePath}' has no '{BootstrapSceneBuilder.AkimboPistolName}' weapon pickup; rerun {FeatureSceneBuilder.MenuPath}");
                return;
            }

            var definition = spawnPoint.Contents.Weapon;
            var spec = definition.ToSpec();
            if (!string.Equals(spec.Id, BootstrapSceneBuilder.AkimboPistolId, StringComparison.Ordinal) || !spec.IsAkimbo)
            {
                problems.Add($"the pickup holds {spec.LogName} ({spec.Id}, akimbo={spec.IsAkimbo}), not the akimbo {BootstrapSceneBuilder.AkimboPistolId}; rerun {WeaponAssetBuilder.MenuPath}");
                return;
            }

            // 左手のモデルは右手のプレハブを反転して出すので、左手のプレハブを空のままにしてあるかを見る。
            var viewmodels = ViewmodelCatalog.Load();
            if (viewmodels == null || !viewmodels.TryGetWeapon(spec.Id, out var viewmodel) || viewmodel.Prefab == null)
            {
                problems.Add($"the viewmodel catalog has no model for {spec.LogName}; rerun {PresentationAssetBuilder.MenuPath}");
            }
            else
            {
                Expect(problems, viewmodel.LeftPrefab == null, $"the viewmodel of {spec.LogName} leaves the left-hand prefab empty so the right one is mirrored");
            }

            // 自動の Update と入れ違いにならないよう、確かめる間は止める (時間は Tick で進める)。
            controller.enabled = false;

            var pickup = spawnPoint.Current;
            if (pickup == null || !pickup.IsAvailable)
            {
                problems.Add($"the '{BootstrapSceneBuilder.AkimboPistolName}' pickup has not spawned");
                return;
            }

            if (!pickup.TryCollect(player, out var outcome))
            {
                problems.Add($"the player could not pick up {spec.LogName} ({outcome.Rejection})");
                return;
            }

            if (controller.Weapon == null || !string.Equals(controller.Weapon.Spec.Id, spec.Id, StringComparison.Ordinal))
            {
                Expect(problems, controller.Select(spec.Id), $"the player holds {spec.LogName}");
            }

            // 持ち替えの秒数は武器ごとの抜く秒数で変わるので、SwitchSeconds の 1 回で済ませずに終わるまで進める。
            for (var waited = 0f; controller.IsSwitching && waited <= EnterTimeoutSeconds; waited += StepSeconds)
            {
                controller.Tick(StepSeconds);
            }

            var hands = controller.Akimbo;
            if (hands == null)
            {
                problems.Add($"the player does not hold {spec.LogName} in both hands (holds {controller.Weapon?.Spec.Id ?? "nothing"})");
                return;
            }

            var rightBefore = hands.AmmoInMagazine(AkimboHand.Right);
            var leftBefore = hands.AmmoInMagazine(AkimboHand.Left);
            if (rightBefore < 1 || leftBefore < 1)
            {
                problems.Add($"both hands need a round to fire (right={rightBefore}, left={leftBefore})");
                return;
            }

            Expect(problems, controller.PullTrigger(AkimboHand.Right, true, true), "the right hand fires");
            Expect(
                problems,
                hands.AmmoInMagazine(AkimboHand.Right) == rightBefore - 1 && hands.AmmoInMagazine(AkimboHand.Left) == leftBefore,
                $"only the right magazine spends a round (right {rightBefore}->{hands.AmmoInMagazine(AkimboHand.Right)}, left {leftBefore}->{hands.AmmoInMagazine(AkimboHand.Left)})");

            Expect(problems, controller.PullTrigger(AkimboHand.Left, true, true), "the left hand fires");
            Expect(
                problems,
                hands.AmmoInMagazine(AkimboHand.Left) == leftBefore - 1,
                $"the left magazine spends a round (left {leftBefore}->{hands.AmmoInMagazine(AkimboHand.Left)})");

            controller.PullTrigger(AkimboHand.Right, false, false);
            controller.PullTrigger(AkimboHand.Left, false, false);
            controller.Tick(spec.FireInterval);

            Expect(problems, controller.PressReload(), "the reload key starts reloading");
            Expect(
                problems,
                hands.IsHandReloading(AkimboHand.Right) && hands.IsHandReloading(AkimboHand.Left),
                "the reload key reloads both hands that are not full");

            var reloadWaited = 0f;
            while (hands.IsReloading && reloadWaited <= hands.ReloadSeconds + 1f)
            {
                controller.Tick(StepSeconds);
                reloadWaited += StepSeconds;
            }

            Expect(
                problems,
                hands.AmmoInMagazine(AkimboHand.Right) == hands.MagazineSize && hands.AmmoInMagazine(AkimboHand.Left) == hands.MagazineSize,
                $"both magazines are full after {Format(reloadWaited)}s (right={hands.AmmoInMagazine(AkimboHand.Right)}, left={hands.AmmoInMagazine(AkimboHand.Left)}, size={hands.MagazineSize})");

            var linesProblem = CheckLines(_combatLines, ExpectedLines(rightBefore - 1, leftBefore - 1));
            if (linesProblem != null)
            {
                problems.Add(linesProblem);
            }

            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "{0} weapon={1} right={2} left={3} reload-seconds={4:0.00} waited={5:0.00} reserve={6}",
                LogTag,
                spec.Id,
                rightBefore,
                leftBefore,
                hands.ReloadSeconds,
                reloadWaited,
                hands.ReserveAmmo));
        }

        private static void Expect(List<string> problems, bool condition, string what)
        {
            if (!condition)
            {
                problems.Add(what);
            }
        }

        private static string Format(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static void Finish(int exitCode, string summary)
        {
            _finished = true;
            Detach();
            SessionState.EraseBool(ActiveKey);
            SessionState.EraseFloat(RequestedAtKey);

            var line = $"{LogTag} {summary} (exit code {exitCode})";
            if (exitCode == SuccessExitCode)
            {
                Debug.Log(line);
            }
            else
            {
                Debug.LogError(line);
            }

            if (SmokeSuite.TakeOver(LogTag, exitCode))
            {
                return;
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
            else if (EditorApplication.isPlaying)
            {
                EditorApplication.ExitPlaymode();
            }
        }
    }
}

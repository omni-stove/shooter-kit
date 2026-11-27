using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Shooter.Editor
{
    // batchmode には入力が無いので、Attack と Reload の代わりに WeaponController.PullTrigger・WeaponController.PressReload を呼ぶ。
    // 乱数のままでは詰まるまでの発数が決まらないので、詰まる武器にだけ JamRolls で固定の値の列を渡す。
    // Play Mode に入るので、batchmode で呼ぶときは -quit を付けない。
    [InitializeOnLoad]
    public static class JamSmokeRun
    {
        public const string LogTag = "[JamSmokeRun]";
        public const string MenuPath = "Shooter/Smoke/Run Jam Smoke";

        public const string WeaponJson = "Shotgun.json";

        public const float WarmUpSeconds = 1f;

        public const float EnterTimeoutSeconds = 120f;

        public const float ClearStepSeconds = 0.1f;

        public const int SuccessExitCode = 0;
        public const int FailureExitCode = 1;

        private const string ActiveKey = "Shooter.JamSmokeRun.Active";
        private const string RequestedAtKey = "Shooter.JamSmokeRun.RequestedAt";

        private static readonly List<string> _errors = new List<string>();
        private static readonly List<string> _jamLines = new List<string>();
        private static int _jammedEvents;
        private static int _clearStartedEvents;
        private static int _clearedEvents;
        private static double _playStartedAt = -1d;
        private static bool _finished;

        static JamSmokeRun()
        {
            if (SessionState.GetBool(ActiveKey, false))
            {
                Attach();
            }
        }

        // 0 で終わらせないのは、列を使い切ると最後の値を返し続けるため。0 で終わると解除の後の 1 発がまた詰まる。
        public static IReadOnlyList<float> Rolls { get; } = Array.AsReadOnly(new[] { 0.99f, 0.99f, 0f, 0.99f });

        public static int JamShot => IndexOfJam(Rolls) + 1;

        public static int IndexOfJam(IReadOnlyList<float> rolls)
        {
            if (rolls == null)
            {
                throw new ArgumentNullException(nameof(rolls));
            }

            for (var i = 0; i < rolls.Count; i++)
            {
                if (rolls[i] == 0f)
                {
                    return i;
                }
            }

            throw new ArgumentException("the rolls must hold a 0 to jam on", nameof(rolls));
        }

        public static IReadOnlyList<string> ExpectedLines(string weaponId, int sustainedShots, float chance, float clearSeconds)
        {
            return new[]
            {
                WeaponJamLog.Jammed(weaponId, sustainedShots, chance),
                WeaponJamLog.ClearStarted(weaponId, clearSeconds),
                WeaponJamLog.Cleared(weaponId),
            };
        }

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

            var jamLines = new List<string>();
            foreach (var line in seen)
            {
                if (line != null && line.StartsWith(WeaponJamLog.Tag + " ", StringComparison.Ordinal))
                {
                    jamLines.Add(line);
                }
            }

            if (jamLines.Count != expected.Count)
            {
                return $"expected {expected.Count} {WeaponJamLog.Tag} line(s) but saw {jamLines.Count}: [{string.Join(" | ", jamLines)}]";
            }

            for (var i = 0; i < expected.Count; i++)
            {
                if (!string.Equals(jamLines[i], expected[i], StringComparison.Ordinal))
                {
                    return $"{WeaponJamLog.Tag} line {i + 1} was '{jamLines[i]}', expected '{expected[i]}'";
                }
            }

            return null;
        }

        [MenuItem(MenuPath)]
        public static void Run()
        {
            try
            {
                WeaponAssetBuilder.Build();
                GameRulesAssetBuilder.Build();
                BootstrapSceneBuilder.Build();
                EditorSceneManager.OpenScene(BootstrapSceneBuilder.DefaultScenePath, OpenSceneMode.Single);

                _errors.Clear();
                _jamLines.Clear();
                _jammedEvents = 0;
                _clearStartedEvents = 0;
                _clearedEvents = 0;
                _playStartedAt = -1d;
                _finished = false;
                SessionState.SetBool(ActiveKey, true);
                SessionState.SetFloat(RequestedAtKey, (float)EditorApplication.timeSinceStartup);
                Attach();

                Debug.Log($"{LogTag} entering play mode in '{BootstrapSceneBuilder.DefaultScenePath}'");
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
            CombatEvents.WeaponJammed -= OnJammed;
            CombatEvents.WeaponJamClearStarted -= OnClearStarted;
            CombatEvents.WeaponJamCleared -= OnCleared;
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

            if (condition != null && condition.StartsWith(WeaponJamLog.Tag + " ", StringComparison.Ordinal))
            {
                _jamLines.Add(condition);
            }
        }

        private static void OnJammed(WeaponJamInfo info) => _jammedEvents++;

        private static void OnClearStarted(WeaponJamInfo info) => _clearStartedEvents++;

        private static void OnCleared(WeaponJamInfo info) => _clearedEvents++;

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

            // 武器はロードアウトを作ったときのルール (LoadedRules) で詰まるかが決まるので、Rules でなくそちらを見る。
            var jam = controller.LoadedRules?.Actions.Jam ?? WeaponJam.Off;
            if (jam != WeaponJam.On)
            {
                problems.Add($"the range rules have jams {jam}; rerun {GameRulesAssetBuilder.MenuPath}");
                return;
            }

            var definition = LoadSample(WeaponJson);
            var spec = definition.ToSpec();
            if (spec.Ammo.Jam == null)
            {
                problems.Add($"{WeaponJson} ({spec.Id}) has no ammo.jam; give it one and rerun {WeaponAssetBuilder.MenuPath}");
                return;
            }

            // 自動の Update と入れ違いにならないよう、確かめる間は止める (時間は Tick で進める)。
            controller.enabled = false;
            var roll = new SequenceRoll(Rolls);
            controller.JamRolls = s => string.Equals(s.Id, spec.Id, StringComparison.Ordinal) ? roll : null;

            Expect(problems, controller.AddWeapon(definition), $"the player picks up {spec.LogName}");
            Expect(problems, controller.Select(spec.Id), $"the player holds {spec.LogName}");
            // 持ち替えの秒数は武器ごとの抜く秒数で変わるので、SwitchSeconds の 1 回で済ませずに終わるまで進める。
            for (var waitedToDraw = 0f; controller.IsSwitching && waitedToDraw <= EnterTimeoutSeconds; waitedToDraw += ClearStepSeconds)
            {
                controller.Tick(ClearStepSeconds);
            }

            var weapon = controller.Weapon;
            if (weapon == null || !string.Equals(weapon.Spec.Id, spec.Id, StringComparison.Ordinal) || weapon.Jam == null)
            {
                problems.Add($"the player does not hold a jamming {spec.LogName} (holds {weapon?.Spec.Id ?? "nothing"})");
                return;
            }

            CombatEvents.WeaponJammed += OnJammed;
            CombatEvents.WeaponJamClearStarted += OnClearStarted;
            CombatEvents.WeaponJamCleared += OnCleared;

            var sustained = 0;
            var chance = 0f;
            for (var shot = 1; shot <= JamShot; shot++)
            {
                sustained = weapon.Jam.SustainedShots;
                chance = weapon.Jam.NextShotChance;
                Expect(problems, controller.PullTrigger(true, true), $"shot {shot} fires");
                if (shot < JamShot)
                {
                    Expect(problems, !weapon.IsJammed, $"shot {shot} does not jam");
                    controller.Tick(spec.FireInterval);
                }
            }

            Expect(problems, weapon.IsJammed, $"shot {JamShot} jams");
            controller.Tick(spec.FireInterval);
            Expect(problems, !controller.PullTrigger(true, true), "the jammed weapon does not fire");

            Expect(problems, controller.PressReload(), "the reload key starts clearing the jam");
            var clearSeconds = weapon.JamClearRemainingSeconds;
            Expect(problems, weapon.IsClearingJam, "the weapon is clearing the jam");

            var waited = 0f;
            while (weapon.IsJammed && waited <= spec.Ammo.Jam.ClearSeconds + 1f)
            {
                controller.Tick(ClearStepSeconds);
                waited += ClearStepSeconds;
            }

            Expect(problems, !weapon.IsJammed, $"the jam clears within {Format(spec.Ammo.Jam.ClearSeconds)}s (waited {Format(waited)}s)");
            Expect(problems, controller.PullTrigger(true, true), "the cleared weapon fires again");
            Expect(problems, !weapon.IsJammed, "the shot after clearing does not jam");

            var linesProblem = CheckLines(_jamLines, ExpectedLines(spec.Id, sustained, chance, clearSeconds));
            if (linesProblem != null)
            {
                problems.Add(linesProblem);
            }

            Expect(
                problems,
                _jammedEvents == 1 && _clearStartedEvents == 1 && _clearedEvents == 1,
                $"CombatEvents raised jammed/clear-started/cleared once each (got {_jammedEvents}/{_clearStartedEvents}/{_clearedEvents})");

            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "{0} weapon={1} jam-shot={2} rolls-drawn={3} clear-seconds={4:0.00} waited={5:0.00} jam-lines={6} events={7}/{8}/{9}",
                LogTag,
                spec.Id,
                JamShot,
                roll.Calls,
                clearSeconds,
                waited,
                _jamLines.Count,
                _jammedEvents,
                _clearStartedEvents,
                _clearedEvents));
        }

        // 名前でなく Id で射撃場の一覧から引き、アセットの名前を変えても同じ武器を拾えるようにする。
        private static WeaponDefinition LoadSample(string jsonFile)
        {
            var path = ShooterKitRoot.PhysicalPath(WeaponAssetBuilder.JsonFolder + "/" + jsonFile);
            var id = WeaponJsonImporter.ParseSpec(File.ReadAllText(path), jsonFile).Id;
            if (!WeaponAssetBuilder.LoadRangeWeapons().TryFind(id, out var weapon))
            {
                throw new InvalidOperationException($"{WeaponAssetBuilder.RangeWeaponsPath} does not list {jsonFile} ({id}); run {WeaponAssetBuilder.MenuPath}");
            }

            return weapon;
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

        public sealed class SequenceRoll : IJamRoll
        {
            private readonly float[] _values;

            public SequenceRoll(IReadOnlyList<float> values)
            {
                if (values == null || values.Count == 0)
                {
                    throw new ArgumentException("a fixed roll needs at least one value", nameof(values));
                }

                _values = new float[values.Count];
                for (var i = 0; i < values.Count; i++)
                {
                    if (!(values[i] >= 0f && values[i] < 1f))
                    {
                        throw new ArgumentOutOfRangeException(nameof(values), values[i], "every roll must be in [0, 1)");
                    }

                    _values[i] = values[i];
                }
            }

            public int Calls { get; private set; }

            public float Draw()
            {
                var value = _values[Math.Min(Calls, _values.Length - 1)];
                Calls++;
                return value;
            }
        }
    }
}

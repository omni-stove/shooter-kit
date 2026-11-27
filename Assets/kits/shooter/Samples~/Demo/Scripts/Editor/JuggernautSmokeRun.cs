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
    // batchmode には入力が無いので、プレイヤーは出現点に立ったままで、撃ち合うのは bot だけになる。
    // 最初のジャガーノートも、だれに替わるかも乱数と撃ち合いで決まるので、だれでも通る形 (選ばれた・1 回以上替わった・試合が終わった) で確かめる。
    // Play Mode に入るときにスクリプトが読み直されても続きを走らせられるよう、状態は SessionState に置く。
    // Play Mode に入るので、batchmode で呼ぶときは -quit を付けない。
    [InitializeOnLoad]
    public static class JuggernautSmokeRun
    {
        public const string LogTag = "[JuggernautSmoke]";

        public const string SecondsArgument = "-juggernautSmokeSeconds";

        // 体力を上げたジャガーノートを bot がそろって追って倒すまでを見ても、時間切れまで長く待たない長さにする。
        public const float DefaultSeconds = 60f;

        // 待ちが長いとスイート全体が延びるので、最初のジャガーノートを選ぶまでを縮める。
        public const float StartDelaySeconds = 2f;

        // 選ばれてから bot が寄って撃ち倒す分。
        public const float MinimumSeconds = StartDelaySeconds + 10f;

        // GameBootstrap の Awake と JuggernautRunner の Start を済ませてから見始める。
        public const float WarmUpSeconds = 1f;

        public const float GraceSeconds = 30f;

        public const float EnterTimeoutSeconds = 120f;

        public const int SuccessExitCode = 0;
        public const int FailureExitCode = 1;

        private const string ActiveKey = "Shooter.JuggernautSmokeRun.Active";
        private const string SecondsKey = "Shooter.JuggernautSmokeRun.Seconds";
        private const string RequestedAtKey = "Shooter.JuggernautSmokeRun.RequestedAt";

        private static readonly string _changePrefix = JuggernautLog.LogPrefix + " change player=";
        private static readonly string _chosenSuffix = " reason=" + JuggernautLog.ReasonText(JuggernautChangeReason.Chosen);
        private static readonly string _bodyPrefix = JuggernautLog.LogPrefix + " body player=";
        private static readonly string _endedPrefix = JuggernautLog.LogPrefix + " end winner=";

        private static readonly List<string> _errors = new List<string>();
        private static Step _step;
        private static string _chosenLine;
        private static string _lastChangeLine;
        private static int _changes;
        private static int _bodies;
        private static string _endedLine;
        private static string _winner;
        private static double _playStartedAt = -1d;
        private static bool _finished;

        public enum LineKind
        {
            Other,
            Chosen,
            Change,
            Body,
            Ended,
        }

        private enum Step
        {
            CheckStart,
            WaitChosen,
            WaitChange,
            WaitEnd,
            Done,
        }

        static JuggernautSmokeRun()
        {
            // Play Mode に入るときにスクリプトが読み直された場合は、ここから続きを見張る。
            if (SessionState.GetBool(ActiveKey, false))
            {
                Attach();
            }
        }

        public static void Run()
        {
            try
            {
                var seconds = ParseSeconds(Environment.GetCommandLineArgs());

                WeaponAssetBuilder.Build();
                GameRulesAssetBuilder.Build();
                JuggernautSceneBuilder.Build();
                var scene = EditorSceneManager.OpenScene(JuggernautSceneBuilder.ScenePathValue, OpenSceneMode.Single);

                var runner = UnityEngine.Object.FindAnyObjectByType<JuggernautRunner>();
                if (runner == null)
                {
                    throw new InvalidOperationException(
                        $"'{JuggernautSceneBuilder.ScenePathValue}' has no JuggernautRunner; rerun {JuggernautSceneBuilder.MenuPath}");
                }

                // 調整値のアセットを書き換えると保存しなくてもメモリ上で後の実行に残るので、複製を縮めて配線する。
                runner.Definition = Shorten(runner.Definition, seconds);
                EditorSceneManager.MarkSceneDirty(scene);

                ResetObservations();
                SessionState.SetBool(ActiveKey, true);
                SessionState.SetFloat(SecondsKey, seconds);
                SessionState.SetFloat(RequestedAtKey, (float)EditorApplication.timeSinceStartup);
                Attach();

                Debug.Log(
                    $"{LogTag} entering play mode in '{JuggernautSceneBuilder.ScenePathValue}' with a {seconds.ToString("0.##", CultureInfo.InvariantCulture)}s match");
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Finish(FailureExitCode, $"could not start: {e.Message}");
            }
        }

        public static float ParseSeconds(IReadOnlyList<string> args)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            for (var i = 0; i < args.Count; i++)
            {
                if (!string.Equals(args[i], SecondsArgument, StringComparison.Ordinal))
                {
                    continue;
                }

                if (i + 1 >= args.Count
                    || !float.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
                    || !(seconds >= MinimumSeconds)
                    || float.IsInfinity(seconds))
                {
                    throw new ArgumentException(
                        $"{SecondsArgument} needs a finite number of seconds of at least {MinimumSeconds.ToString("0.##", CultureInfo.InvariantCulture)}",
                        nameof(args));
                }

                return seconds;
            }

            return DefaultSeconds;
        }

        public static JuggernautDefinition Shorten(JuggernautDefinition source, float seconds)
        {
            JuggernautDefinition copy;
            if (source != null)
            {
                copy = UnityEngine.Object.Instantiate(source);
            }
            else
            {
                copy = ScriptableObject.CreateInstance<JuggernautDefinition>();
                copy.CopyFrom(JuggernautSpec.Default);
            }

            copy.name = (source != null ? source.name : nameof(JuggernautDefinition)) + " (smoke)";
            copy.hideFlags = HideFlags.DontSave;
            copy.StartDelaySeconds = StartDelaySeconds;
            copy.TimeLimitSeconds = seconds;
            try
            {
                copy.ToSpec();
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(copy);
                throw;
            }

            return copy;
        }

        public static LineKind Classify(string line)
        {
            if (line == null)
            {
                return LineKind.Other;
            }

            if (line.StartsWith(_changePrefix, StringComparison.Ordinal))
            {
                return line.EndsWith(_chosenSuffix, StringComparison.Ordinal) ? LineKind.Chosen : LineKind.Change;
            }

            if (line.StartsWith(_bodyPrefix, StringComparison.Ordinal))
            {
                return LineKind.Body;
            }

            return line.StartsWith(_endedPrefix, StringComparison.Ordinal) ? LineKind.Ended : LineKind.Other;
        }

        // 時間切れで点が並んだときの引き分けも試合の終わりなので、勝った人がいなくても通す。
        public static string WinnerOf(string endedLine)
        {
            if (endedLine == null || !endedLine.StartsWith(_endedPrefix, StringComparison.Ordinal))
            {
                return null;
            }

            var rest = endedLine.Substring(_endedPrefix.Length);
            var space = rest.IndexOf(' ');
            var winner = space < 0 ? rest : rest.Substring(0, space);
            return winner.Length > 0 ? winner : null;
        }

        public static bool Passed(bool sawChosen, int changes, int bodies, string endedLine, int errors)
        {
            return sawChosen && changes >= 1 && bodies >= 1 && WinnerOf(endedLine) != null && errors == 0;
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

        private static void ResetObservations()
        {
            _errors.Clear();
            _step = Step.CheckStart;
            _chosenLine = null;
            _lastChangeLine = null;
            _changes = 0;
            _bodies = 0;
            _endedLine = null;
            _winner = null;
            _playStartedAt = -1d;
            _finished = false;
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            // Play Mode に入る前 (アセットとシーンの生成) のログは判定に使わない。
            if (_finished || !EditorApplication.isPlaying)
            {
                return;
            }

            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                // CampaignSmokeRun と同じく、Editor の起動時インデックスが Play 中に投げる例外はゲームのコードではないので外す。
                if (stackTrace != null && stackTrace.Contains("UnityEditor.Search.SearchInit.IndexationOnStartup"))
                {
                    return;
                }

                _errors.Add($"{type}: {condition}");
                return;
            }

            switch (Classify(condition))
            {
                case LineKind.Chosen:
                    _chosenLine = condition;
                    break;
                case LineKind.Change:
                    _lastChangeLine = condition;
                    _changes++;
                    break;
                case LineKind.Body:
                    _bodies++;
                    break;
                case LineKind.Ended:
                    _endedLine = condition;
                    break;
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

            var seconds = SessionState.GetFloat(SecondsKey, DefaultSeconds);
            string problem;
            try
            {
                problem = Advance();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                problem = $"{e.GetType().Name} at step {_step}: {e.Message}";
            }

            var timeout = WarmUpSeconds + seconds + GraceSeconds;
            if (problem == null && _step != Step.Done && now - _playStartedAt > timeout)
            {
                problem = $"step {_step} did not finish within {timeout.ToString("0.##", CultureInfo.InvariantCulture)}s of play";
            }

            if (problem == null && _step != Step.Done && _errors.Count == 0)
            {
                return;
            }

            Report(problem);
        }

        private static string Advance()
        {
            var runner = UnityEngine.Object.FindAnyObjectByType<JuggernautRunner>();
            if (runner == null)
            {
                return "the scene has no JuggernautRunner";
            }

            switch (_step)
            {
                case Step.CheckStart:
                    return CheckStart(runner);

                case Step.WaitChosen:
                    return WaitChosen(runner);

                case Step.WaitChange:
                    return WaitChange(runner);

                case Step.WaitEnd:
                    return WaitEnd(runner);

                default:
                    return null;
            }
        }

        private static string CheckStart(JuggernautRunner runner)
        {
            if (!runner.IsStarted || runner.Juggernaut == null)
            {
                return "the JuggernautRunner did not start a match; see the errors above";
            }

            var expected = JuggernautSceneBuilder.BotCount + 1;
            if (runner.Members.Count != expected)
            {
                return $"the match has {runner.Members.Count} member(s), expected {expected} (the player and {JuggernautSceneBuilder.BotCount} bots); rerun {JuggernautSceneBuilder.MenuPath}";
            }

            _step = Step.WaitChosen;
            return null;
        }

        private static string WaitChosen(JuggernautRunner runner)
        {
            var juggernaut = runner.Juggernaut;
            if (juggernaut == null)
            {
                return "the JuggernautRunner stopped the match before it ended";
            }

            if (!juggernaut.HasJuggernaut || _chosenLine == null || _bodies == 0)
            {
                return runner.IsMatchOver ? $"the match ended before the first juggernaut was chosen ({_endedLine ?? "-"})" : null;
            }

            if (runner.JuggernautMember == null)
            {
                return $"the chosen juggernaut is not in the scene ({_chosenLine})";
            }

            _step = Step.WaitChange;
            return null;
        }

        private static string WaitChange(JuggernautRunner runner)
        {
            if (runner.Juggernaut == null)
            {
                return "the JuggernautRunner stopped the match before it ended";
            }

            if (_changes == 0)
            {
                return runner.IsMatchOver ? $"the match ended before the juggernaut changed hands ({_endedLine ?? "-"})" : null;
            }

            _step = Step.WaitEnd;
            return null;
        }

        private static string WaitEnd(JuggernautRunner runner)
        {
            if (!runner.IsMatchOver || _endedLine == null)
            {
                return null;
            }

            _winner = WinnerOf(_endedLine);
            if (_winner == null)
            {
                return $"the match ended with an unreadable result ({_endedLine})";
            }

            _step = Step.Done;
            return null;
        }

        private static void Report(string problem)
        {
            var problems = new List<string>();
            if (problem != null)
            {
                problems.Add(problem);
            }

            problems.AddRange(_errors);
            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "{0} step={1} chosen={2} changes={3} last-change={4} bodies={5} ended={6} errors={7}",
                LogTag,
                _step,
                _chosenLine ?? "-",
                _changes,
                _lastChangeLine ?? "-",
                _bodies,
                _endedLine ?? "-",
                _errors.Count));

            var passed = problems.Count == 0
                && _step == Step.Done
                && Passed(_chosenLine != null, _changes, _bodies, _endedLine, _errors.Count);
            if (passed)
            {
                Finish(SuccessExitCode, string.Format(CultureInfo.InvariantCulture, "passed (changes={0} winner={1})", _changes, _winner));
            }
            else
            {
                if (problems.Count == 0)
                {
                    problems.Add("not every step was confirmed");
                }

                Finish(FailureExitCode, "failed:\n  " + string.Join("\n  ", problems));
            }
        }

        private static void Finish(int exitCode, string summary)
        {
            _finished = true;
            Detach();
            SessionState.EraseBool(ActiveKey);
            SessionState.EraseFloat(SecondsKey);
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

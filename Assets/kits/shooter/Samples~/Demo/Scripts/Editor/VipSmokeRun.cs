using System;
using System.Collections.Generic;
using System.Globalization;
using GameKit.Core;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Shooter.Editor
{
    // batchmode には入力が無いので、プレイヤーは出現点に立ったままで、撃ち合うのは bot だけになる。
    // VIP もラウンドの勝ち方も乱数と撃ち合いで決まるので、だれが勝っても通る形 (ラウンドが進んだ・攻守が入れ替わった・試合が終わった) で確かめる。
    // 最初のラウンドは守る側がみな bot なので、VIP の bot が脱出地点へ向かい・守る側がついて行き・攻める側が追うことも、そこで必ず見られる。
    // Play Mode に入るときにスクリプトが読み直されても続きを走らせられるよう、状態は SessionState に置く。
    // Play Mode に入るので、batchmode で呼ぶときは -quit を付けない。
    [InitializeOnLoad]
    public static class VipSmokeRun
    {
        public const string LogTag = "[VipSmoke]";

        public const string SecondsArgument = "-vipSmokeRoundSeconds";

        // 奥の出現点から手前の脱出地点まで歩いて留まり切るか、撃ち合いで決まるまでを見ても、時間切れまで長く待たない長さにする。
        public const float DefaultRoundSeconds = 30f;

        // 奥の出現点から脱出地点まで (30m 余りを bot の速さ 3.5m/s で) 歩いて、既定の秒 (VipSpec.DefaultExitHoldSeconds) 留まれる長さ。
        public const float MinimumRoundSeconds = 15f;

        // 2 勝で決まれば、どちらが勝っても 2 ラウンド目があり、その前に攻守が 1 回入れ替わる。
        public const int WinsToWin = 2;

        public const int SwapEveryRounds = 1;

        public const int MaxRounds = (WinsToWin * 2) - 1;

        // GameBootstrap の Awake と VipRunner の Start を済ませてから見始める。
        public const float WarmUpSeconds = 1f;

        public const float GraceSeconds = 30f;

        public const float EnterTimeoutSeconds = 120f;

        public const int SuccessExitCode = 0;
        public const int FailureExitCode = 1;

        private const string ActiveKey = "Shooter.VipSmokeRun.Active";
        private const string SecondsKey = "Shooter.VipSmokeRun.RoundSeconds";
        private const string RequestedAtKey = "Shooter.VipSmokeRun.RequestedAt";

        private static readonly string _roundPrefix = VipLog.Tag + " round=";
        private static readonly string _attackerKey = " attacker=";
        private static readonly string _defenderKey = " defender=";
        private static readonly string _chosenPrefix = VipLog.Tag + " chosen round=";
        private static readonly string _roundEndPrefix = VipLog.Tag + " round_end round=";
        private static readonly string _swapPrefix = VipLog.Tag + " swap attacker=";
        private static readonly string _botExitPrefix = VipLog.Tag + " bot_exit player=";
        private static readonly string _botEscortPrefix = VipLog.Tag + " bot_escort player=";
        private static readonly string _botTargetPrefix = VipLog.Tag + " bot_target player=";
        private static readonly string _endedPrefix = VipLog.Tag + " ended winner=";

        private static readonly Observations _seen = new Observations();
        private static readonly List<string> _errors = new List<string>();
        private static Step _step;
        private static double _playStartedAt = -1d;
        private static bool _finished;

        public enum LineKind
        {
            Other,
            Round,
            Chosen,
            RoundEnd,
            Swap,
            BotExit,
            BotEscort,
            BotTarget,
            Ended,
        }

        private enum Step
        {
            CheckStart,
            WaitSwap,
            WaitEnd,
            Done,
        }

        static VipSmokeRun()
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
                var seconds = ParseRoundSeconds(Environment.GetCommandLineArgs());

                WeaponAssetBuilder.Build();
                GameRulesAssetBuilder.Build();
                VipSceneBuilder.Build();
                var scene = EditorSceneManager.OpenScene(VipSceneBuilder.ScenePathValue, OpenSceneMode.Single);

                var runner = UnityEngine.Object.FindAnyObjectByType<VipRunner>();
                if (runner == null)
                {
                    throw new InvalidOperationException(
                        $"'{VipSceneBuilder.ScenePathValue}' has no VipRunner; rerun {VipSceneBuilder.MenuPath}");
                }

                // 調整値のアセットを書き換えると保存しなくてもメモリ上で後の実行に残るので、複製を縮めて配線する。
                runner.Definition = Shorten(runner.Definition, seconds);
                EditorSceneManager.MarkSceneDirty(scene);

                ResetObservations();
                SessionState.SetBool(ActiveKey, true);
                SessionState.SetFloat(SecondsKey, seconds);
                SessionState.SetFloat(RequestedAtKey, (float)EditorApplication.timeSinceStartup);
                Attach();

                Debug.Log(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} entering play mode in '{1}' with {2}s rounds and {3} win(s) to win",
                    LogTag,
                    VipSceneBuilder.ScenePathValue,
                    seconds.ToString("0.##", CultureInfo.InvariantCulture),
                    WinsToWin));
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Finish(FailureExitCode, $"could not start: {e.Message}");
            }
        }

        public static float ParseRoundSeconds(IReadOnlyList<string> args)
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
                    || !(seconds >= MinimumRoundSeconds)
                    || float.IsInfinity(seconds))
                {
                    throw new ArgumentException(
                        $"{SecondsArgument} needs a finite number of seconds of at least {MinimumRoundSeconds.ToString("0.##", CultureInfo.InvariantCulture)}",
                        nameof(args));
                }

                return seconds;
            }

            return DefaultRoundSeconds;
        }

        // ラウンドの秒・勝ち数・入れ替えの間だけを変え、VIP の体力と留まる秒はゲームの値のまま確かめる。
        public static VipDefinition Shorten(VipDefinition source, float roundSeconds)
        {
            VipDefinition copy;
            if (source != null)
            {
                copy = UnityEngine.Object.Instantiate(source);
            }
            else
            {
                copy = ScriptableObject.CreateInstance<VipDefinition>();
                copy.CopyFrom(KitTuning.Vip);
            }

            copy.name = (source != null ? source.name : nameof(VipDefinition)) + " (smoke)";
            copy.hideFlags = HideFlags.DontSave;
            copy.RoundSeconds = roundSeconds;
            copy.WinsToWin = WinsToWin;
            copy.SwapEveryRounds = SwapEveryRounds;
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

        // 1 ラウンドが時間切れまで続いても、決着までのラウンドを全部見られる長さ。
        public static float TimeoutSeconds(float roundSeconds)
        {
            return WarmUpSeconds + (MaxRounds * roundSeconds) + GraceSeconds;
        }

        public static LineKind Classify(string line)
        {
            if (line == null)
            {
                return LineKind.Other;
            }

            if (line.StartsWith(_roundPrefix, StringComparison.Ordinal))
            {
                return LineKind.Round;
            }

            if (line.StartsWith(_chosenPrefix, StringComparison.Ordinal))
            {
                return LineKind.Chosen;
            }

            if (line.StartsWith(_roundEndPrefix, StringComparison.Ordinal))
            {
                return LineKind.RoundEnd;
            }

            if (line.StartsWith(_swapPrefix, StringComparison.Ordinal))
            {
                return LineKind.Swap;
            }

            if (line.StartsWith(_botExitPrefix, StringComparison.Ordinal))
            {
                return LineKind.BotExit;
            }

            if (line.StartsWith(_botEscortPrefix, StringComparison.Ordinal))
            {
                return LineKind.BotEscort;
            }

            if (line.StartsWith(_botTargetPrefix, StringComparison.Ordinal))
            {
                return LineKind.BotTarget;
            }

            return line.StartsWith(_endedPrefix, StringComparison.Ordinal) ? LineKind.Ended : LineKind.Other;
        }

        public static bool TryReadRound(string roundLine, out int round, out Side attacker, out Side defender)
        {
            round = 0;
            attacker = default;
            defender = default;
            if (Classify(roundLine) != LineKind.Round)
            {
                return false;
            }

            var rest = roundLine.Substring(_roundPrefix.Length);
            var attackerAt = rest.IndexOf(_attackerKey, StringComparison.Ordinal);
            var defenderAt = rest.IndexOf(_defenderKey, StringComparison.Ordinal);
            if (attackerAt < 0 || defenderAt < attackerAt)
            {
                return false;
            }

            var roundText = rest.Substring(0, attackerAt);
            var attackerText = rest.Substring(attackerAt + _attackerKey.Length, defenderAt - attackerAt - _attackerKey.Length);
            var defenderText = rest.Substring(defenderAt + _defenderKey.Length);
            if (!TryReadNumber(roundText, out round)
                || !TryReadSide(attackerText, out attacker)
                || !TryReadSide(defenderText, out defender)
                || attacker == defender)
            {
                round = 0;
                attacker = default;
                defender = default;
                return false;
            }

            return true;
        }

        // 勝つのはいつも攻守のどちらかなので、Alpha か Bravo でない勝ち手は読めないものとして扱う。
        public static Side? WinnerOf(string endedLine)
        {
            if (Classify(endedLine) != LineKind.Ended)
            {
                return null;
            }

            return TryReadSide(endedLine.Substring(_endedPrefix.Length), out var winner) ? winner : (Side?)null;
        }

        private static bool TryReadNumber(string text, out int value)
        {
            return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value > 0;
        }

        private static bool TryReadSide(string text, out Side side)
        {
            side = default;
            if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            {
                return false;
            }

            if (id == Side.Alpha.Id)
            {
                side = Side.Alpha;
                return true;
            }

            if (id == Side.Bravo.Id)
            {
                side = Side.Bravo;
                return true;
            }

            return false;
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
            _seen.Clear();
            _errors.Clear();
            _step = Step.CheckStart;
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

            _seen.Observe(condition);
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

            var seconds = SessionState.GetFloat(SecondsKey, DefaultRoundSeconds);
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

            var timeout = TimeoutSeconds(seconds);
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
            var runner = UnityEngine.Object.FindAnyObjectByType<VipRunner>();
            if (runner == null)
            {
                return "the scene has no VipRunner";
            }

            switch (_step)
            {
                case Step.CheckStart:
                    return CheckStart(runner);

                case Step.WaitSwap:
                    return WaitSwap(runner);

                case Step.WaitEnd:
                    return WaitEnd(runner);

                default:
                    return null;
            }
        }

        private static string CheckStart(VipRunner runner)
        {
            if (!runner.IsStarted || runner.Vip == null)
            {
                return "the VipRunner did not start a match; see the errors above";
            }

            var expected = VipSceneBuilder.AlphaBots + VipSceneBuilder.BravoBots + 1;
            if (runner.Members.Count != expected)
            {
                return $"the match has {runner.Members.Count} member(s), expected {expected} (the player and {expected - 1} bots); rerun {VipSceneBuilder.MenuPath}";
            }

            if (runner.ExitPoints.Count != VipSceneBuilder.ExitCenters.Count)
            {
                return $"the match has {runner.ExitPoints.Count} exit point(s), expected {VipSceneBuilder.ExitCenters.Count}; rerun {VipSceneBuilder.MenuPath}";
            }

            if (runner.Vip.WinsToWin != WinsToWin)
            {
                return $"the match needs {runner.Vip.WinsToWin} win(s), expected the shortened {WinsToWin}";
            }

            _step = Step.WaitSwap;
            return null;
        }

        private static string WaitSwap(VipRunner runner)
        {
            if (!_seen.SidesChanged)
            {
                if (runner.IsMatchOver)
                {
                    return $"the match ended before attack and defence changed sides ({_seen.EndedLine ?? "-"})";
                }

                return runner.Vip == null ? "the VipRunner stopped the match before it ended" : null;
            }

            _step = Step.WaitEnd;
            return null;
        }

        private static string WaitEnd(VipRunner runner)
        {
            if (!runner.IsMatchOver || _seen.EndedLine == null)
            {
                return null;
            }

            if (!_seen.Winner.HasValue)
            {
                return $"the match ended with an unreadable result ({_seen.EndedLine})";
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
            Debug.Log(LogTag + " step=" + _step + " " + _seen.Summary() + " errors=" + _errors.Count.ToString(CultureInfo.InvariantCulture));

            var passed = problems.Count == 0 && _step == Step.Done && _seen.Passed(_errors.Count);
            if (passed)
            {
                Finish(
                    SuccessExitCode,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "passed (rounds={0} swaps={1} winner={2})",
                        _seen.Rounds,
                        _seen.Swaps,
                        _seen.Winner.Value.Id));
            }
            else
            {
                if (problems.Count == 0)
                {
                    problems.AddRange(_seen.Missing());
                }

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

        // ログの行から数えるだけにして Play Mode を要さないのは、成功の条件を EditMode のテストで行を渡して確かめるため。
        public sealed class Observations
        {
            private Side? _firstAttacker;

            public int Rounds { get; private set; }

            public int RoundEnds { get; private set; }

            public int Chosen { get; private set; }

            public int Swaps { get; private set; }

            public int BotExits { get; private set; }

            public int BotEscorts { get; private set; }

            public int BotTargets { get; private set; }

            // 入れ替えの行だけでなく、次のラウンドの始めの行で攻める側が替わったことまで見る。
            public bool SidesChanged { get; private set; }

            public string EndedLine { get; private set; }

            public Side? Winner => WinnerOf(EndedLine);

            public void Clear()
            {
                _firstAttacker = null;
                Rounds = 0;
                RoundEnds = 0;
                Chosen = 0;
                Swaps = 0;
                BotExits = 0;
                BotEscorts = 0;
                BotTargets = 0;
                SidesChanged = false;
                EndedLine = null;
            }

            public void Observe(string line)
            {
                switch (Classify(line))
                {
                    case LineKind.Round:
                        if (TryReadRound(line, out _, out var attacker, out _))
                        {
                            Rounds++;
                            if (!_firstAttacker.HasValue)
                            {
                                _firstAttacker = attacker;
                            }
                            else if (Swaps > 0 && attacker != _firstAttacker.Value)
                            {
                                SidesChanged = true;
                            }
                        }

                        break;
                    case LineKind.Chosen:
                        Chosen++;
                        break;
                    case LineKind.RoundEnd:
                        RoundEnds++;
                        break;
                    case LineKind.Swap:
                        Swaps++;
                        break;
                    case LineKind.BotExit:
                        BotExits++;
                        break;
                    case LineKind.BotEscort:
                        BotEscorts++;
                        break;
                    case LineKind.BotTarget:
                        BotTargets++;
                        break;
                    case LineKind.Ended:
                        EndedLine = line;
                        break;
                }
            }

            public bool Passed(int errors)
            {
                return errors == 0 && Missing().Count == 0;
            }

            public IReadOnlyList<string> Missing()
            {
                var missing = new List<string>();
                if (Rounds < 2 || RoundEnds < 2)
                {
                    missing.Add($"only {Rounds} round(s) started and {RoundEnds} ended; expected at least 2 of each");
                }

                if (Chosen < Rounds)
                {
                    missing.Add($"a VIP was chosen {Chosen} time(s) in {Rounds} round(s)");
                }

                if (Swaps < 1 || !SidesChanged)
                {
                    missing.Add($"attack and defence did not change sides (swaps={Swaps})");
                }

                if (BotExits < 1)
                {
                    missing.Add("no VIP bot headed for an exit point");
                }

                if (BotEscorts < 1)
                {
                    missing.Add("no defending bot escorted the VIP");
                }

                if (BotTargets < 1)
                {
                    missing.Add("no attacking bot went after the VIP");
                }

                if (!Winner.HasValue)
                {
                    missing.Add($"the match did not end with a winner ({EndedLine ?? "-"})");
                }

                return missing;
            }

            public string Summary()
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "rounds={0} round-ends={1} chosen={2} swaps={3} sides-changed={4} bot-exits={5} bot-escorts={6} bot-targets={7} ended={8}",
                    Rounds,
                    RoundEnds,
                    Chosen,
                    Swaps,
                    SidesChanged,
                    BotExits,
                    BotEscorts,
                    BotTargets,
                    EndedLine ?? "-");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using GameKit.Core;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class VipSmokeRunTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }

        // 2 ラウンドで決まり、1 ラウンド目の後に攻守が入れ替わる試合の行。
        private static IEnumerable<string> TwoRoundMatch()
        {
            yield return VipLog.SettingsLog(new VipSpec(200, 30f, 2, 3f, 1));
            yield return VipLog.ChosenLog(1, 4);
            yield return VipLog.BodyLog(4, 200);
            yield return VipLog.RoundLog(1, Side.Alpha, Side.Bravo);
            yield return VipLog.BotExitLog(4, 0);
            yield return VipLog.BotEscortLog(3, 4);
            yield return VipLog.BotTargetLog(1, 4);
            yield return VipLog.EscapedLog(4);
            yield return VipLog.RoundEndLog(1, Side.Bravo, Side.Bravo, VipRoundEnd.Escaped, 0, 1);
            yield return VipLog.RestoredLog(4, 100);
            yield return VipLog.SwapLog(Side.Bravo, Side.Alpha);
            yield return VipLog.SpawnSwapLog(10, Side.Bravo, Side.Alpha);
            yield return VipLog.ChosenLog(2, 0);
            yield return VipLog.RoundLog(2, Side.Bravo, Side.Alpha);
            yield return VipLog.RoundEndLog(2, Side.Bravo, Side.Alpha, VipRoundEnd.VipDown, 0, 2);
            yield return VipLog.EndedLog(Side.Bravo);
        }

        private static VipSmokeRun.Observations Observe(IEnumerable<string> lines)
        {
            var seen = new VipSmokeRun.Observations();
            foreach (var line in lines)
            {
                seen.Observe(line);
            }

            return seen;
        }

        private static List<string> Without(Func<string, bool> drop)
        {
            var lines = new List<string>();
            foreach (var line in TwoRoundMatch())
            {
                if (!drop(line))
                {
                    lines.Add(line);
                }
            }

            return lines;
        }

        [Test]
        public void ReportsUnderItsOwnTag()
        {
            Assert.That(VipSmokeRun.LogTag, Is.EqualTo("[VipSmoke]"));
            Assert.That(VipSmokeRun.SecondsArgument, Is.EqualTo("-vipSmokeRoundSeconds"));
        }

        [Test]
        public void ReadsTheRoundLengthArgument()
        {
            Assert.That(VipSmokeRun.ParseRoundSeconds(Array.Empty<string>()), Is.EqualTo(30f));
            Assert.That(VipSmokeRun.ParseRoundSeconds(new[] { "-batchmode", "-vipSmokeRoundSeconds", "45.5" }), Is.EqualTo(45.5f));
            Assert.That(VipSmokeRun.ParseRoundSeconds(new[] { "-vipSmokeRoundSeconds", "15" }), Is.EqualTo(15f));
        }

        [TestCase(null)]
        [TestCase("abc")]
        [TestCase("14.9")]
        [TestCase("-30")]
        [TestCase("Infinity")]
        [TestCase("NaN")]
        public void RefusesABadRoundLength(string value)
        {
            var args = value == null
                ? new[] { VipSmokeRun.SecondsArgument }
                : new[] { VipSmokeRun.SecondsArgument, value };

            Assert.Throws<ArgumentException>(() => VipSmokeRun.ParseRoundSeconds(args));
        }

        [Test]
        public void RefusesNullArguments()
        {
            Assert.Throws<ArgumentNullException>(() => VipSmokeRun.ParseRoundSeconds(null));
        }

        [Test]
        public void TheShortenedMatchAlwaysSwapsBeforeItEnds()
        {
            Assert.That(VipSmokeRun.WinsToWin, Is.EqualTo(2));
            Assert.That(VipSmokeRun.SwapEveryRounds, Is.EqualTo(1));
            Assert.That(VipSmokeRun.MaxRounds, Is.EqualTo(3));
            Assert.That(VipSmokeRun.MinimumRoundSeconds, Is.EqualTo(15f));
            Assert.That(VipSmokeRun.MinimumRoundSeconds, Is.GreaterThan(VipSpec.DefaultExitHoldSeconds));
            Assert.That(VipSmokeRun.DefaultRoundSeconds, Is.GreaterThanOrEqualTo(VipSmokeRun.MinimumRoundSeconds));
            Assert.That(VipSmokeRun.TimeoutSeconds(30f), Is.EqualTo(1f + (3f * 30f) + 30f));
        }

        [Test]
        public void ShortenCopiesTheDefinitionAndChangesOnlyTheRoundsToPlay()
        {
            var source = Track(ScriptableObject.CreateInstance<VipDefinition>());
            source.name = "Vip";
            source.CopyFrom(VipSpec.Default);
            source.VipMaxHealth = 350;
            source.ExitHoldSeconds = 5f;

            var shortened = Track(VipSmokeRun.Shorten(source, 20f));

            Assert.That(shortened, Is.Not.SameAs(source));
            Assert.That(shortened.name, Is.EqualTo("Vip (smoke)"));
            Assert.That(shortened.hideFlags, Is.EqualTo(HideFlags.DontSave));
            Assert.That(shortened.RoundSeconds, Is.EqualTo(20f));
            Assert.That(shortened.WinsToWin, Is.EqualTo(2));
            Assert.That(shortened.SwapEveryRounds, Is.EqualTo(1));
            Assert.That(shortened.VipMaxHealth, Is.EqualTo(350));
            Assert.That(shortened.ExitHoldSeconds, Is.EqualTo(5f));
            Assert.That(source.RoundSeconds, Is.EqualTo(VipSpec.DefaultRoundSeconds));
            Assert.That(source.WinsToWin, Is.EqualTo(VipSpec.DefaultWinsToWin));
        }

        [Test]
        public void ShortenTurnsSwappingBackOnWhenTheGameDisabledIt()
        {
            var source = Track(ScriptableObject.CreateInstance<VipDefinition>());
            source.CopyFrom(VipSpec.Default);
            source.SwapEveryRounds = 0;

            var shortened = Track(VipSmokeRun.Shorten(source, 30f));

            Assert.That(shortened.SwapEveryRounds, Is.EqualTo(1));
        }

        [Test]
        public void ShortenStartsFromTheTuningWithoutADefinition()
        {
            var tuning = KitTuning.Vip;

            var shortened = Track(VipSmokeRun.Shorten(null, 30f));

            Assert.That(shortened.name, Is.EqualTo("VipDefinition (smoke)"));
            Assert.That(shortened.RoundSeconds, Is.EqualTo(30f));
            Assert.That(shortened.WinsToWin, Is.EqualTo(2));
            Assert.That(shortened.VipMaxHealth, Is.EqualTo(tuning.VipMaxHealth));
            Assert.That(shortened.ExitHoldSeconds, Is.EqualTo(tuning.ExitHoldSeconds));
        }

        [Test]
        public void ShortenRefusesAnExitHoldAsLongAsTheRound()
        {
            var source = Track(ScriptableObject.CreateInstance<VipDefinition>());
            source.CopyFrom(VipSpec.Default);
            source.ExitHoldSeconds = 20f;

            Assert.Throws<InvalidOperationException>(() => VipSmokeRun.Shorten(source, 20f));
        }

        [Test]
        public void ClassifiesTheModeLinesItWaitsFor()
        {
            Assert.That(VipSmokeRun.Classify(VipLog.RoundLog(1, Side.Alpha, Side.Bravo)), Is.EqualTo(VipSmokeRun.LineKind.Round));
            Assert.That(VipSmokeRun.Classify(VipLog.ChosenLog(1, 4)), Is.EqualTo(VipSmokeRun.LineKind.Chosen));
            Assert.That(
                VipSmokeRun.Classify(VipLog.RoundEndLog(1, Side.Alpha, Side.Bravo, VipRoundEnd.TimeUp, 1, 0)),
                Is.EqualTo(VipSmokeRun.LineKind.RoundEnd));
            Assert.That(VipSmokeRun.Classify(VipLog.SwapLog(Side.Bravo, Side.Alpha)), Is.EqualTo(VipSmokeRun.LineKind.Swap));
            Assert.That(VipSmokeRun.Classify(VipLog.BotExitLog(4, 1)), Is.EqualTo(VipSmokeRun.LineKind.BotExit));
            Assert.That(VipSmokeRun.Classify(VipLog.BotEscortLog(3, 4)), Is.EqualTo(VipSmokeRun.LineKind.BotEscort));
            Assert.That(VipSmokeRun.Classify(VipLog.BotTargetLog(1, 4)), Is.EqualTo(VipSmokeRun.LineKind.BotTarget));
            Assert.That(VipSmokeRun.Classify(VipLog.EndedLog(Side.Bravo)), Is.EqualTo(VipSmokeRun.LineKind.Ended));
            Assert.That(VipSmokeRun.Classify("[Vip] round=2 attacker=1 defender=0"), Is.EqualTo(VipSmokeRun.LineKind.Round));
        }

        [Test]
        public void IgnoresOtherLines()
        {
            Assert.That(VipSmokeRun.Classify(null), Is.EqualTo(VipSmokeRun.LineKind.Other));
            Assert.That(VipSmokeRun.Classify(VipLog.SpawnSwapLog(10, Side.Bravo, Side.Alpha)), Is.EqualTo(VipSmokeRun.LineKind.Other));
            Assert.That(VipSmokeRun.Classify(VipLog.SettingsLog(VipSpec.Default)), Is.EqualTo(VipSmokeRun.LineKind.Other));
            Assert.That(VipSmokeRun.Classify(VipLog.EscapedLog(4)), Is.EqualTo(VipSmokeRun.LineKind.Other));
            Assert.That(VipSmokeRun.Classify(VipLog.BodyLog(4, 200)), Is.EqualTo(VipSmokeRun.LineKind.Other));
            Assert.That(VipSmokeRun.Classify(VipLog.RestoredLog(4, 100)), Is.EqualTo(VipSmokeRun.LineKind.Other));
            Assert.That(VipSmokeRun.Classify(VipLog.NoExitLog("ModeRunner")), Is.EqualTo(VipSmokeRun.LineKind.Other));
            Assert.That(VipSmokeRun.Classify(ModeLog.Ended(Side.Bravo)), Is.EqualTo(VipSmokeRun.LineKind.Other));
            Assert.That(VipSmokeRun.Classify("[VipSmoke] [Vip] ended winner=1"), Is.EqualTo(VipSmokeRun.LineKind.Other));
        }

        [Test]
        public void ReadsTheSidesOfARound()
        {
            Assert.That(VipSmokeRun.TryReadRound(VipLog.RoundLog(3, Side.Bravo, Side.Alpha), out var round, out var attacker, out var defender), Is.True);
            Assert.That(round, Is.EqualTo(3));
            Assert.That(attacker, Is.EqualTo(Side.Bravo));
            Assert.That(defender, Is.EqualTo(Side.Alpha));

            Assert.That(VipSmokeRun.TryReadRound("[Vip] round=0 attacker=0 defender=1", out _, out _, out _), Is.False, "rounds count from 1");
            Assert.That(VipSmokeRun.TryReadRound("[Vip] round=1 attacker=0 defender=0", out _, out _, out _), Is.False, "one side cannot do both");
            Assert.That(VipSmokeRun.TryReadRound("[Vip] round=1 attacker=0 defender=5", out _, out _, out _), Is.False);
            Assert.That(VipSmokeRun.TryReadRound("[Vip] round=1 defender=1", out _, out _, out _), Is.False);
            Assert.That(VipSmokeRun.TryReadRound(VipLog.SwapLog(Side.Bravo, Side.Alpha), out _, out _, out _), Is.False);
            Assert.That(VipSmokeRun.TryReadRound(null, out _, out _, out _), Is.False);
        }

        [Test]
        public void ReadsOnlyATeamAsTheWinner()
        {
            Assert.That(VipSmokeRun.WinnerOf(VipLog.EndedLog(Side.Alpha)), Is.EqualTo(Side.Alpha));
            Assert.That(VipSmokeRun.WinnerOf(VipLog.EndedLog(Side.Bravo)), Is.EqualTo(Side.Bravo));
            Assert.That(VipSmokeRun.WinnerOf(VipLog.EndedLog(new Side(5))), Is.Null);
            Assert.That(VipSmokeRun.WinnerOf("[Vip] ended winner="), Is.Null);
            Assert.That(VipSmokeRun.WinnerOf(ModeLog.Ended(Side.Alpha)), Is.Null);
            Assert.That(VipSmokeRun.WinnerOf(null), Is.Null);
        }

        [Test]
        public void PassesWhenTheRoundsRanTheSidesSwappedAndTheMatchEnded()
        {
            var seen = Observe(TwoRoundMatch());

            Assert.That(seen.Rounds, Is.EqualTo(2));
            Assert.That(seen.RoundEnds, Is.EqualTo(2));
            Assert.That(seen.Chosen, Is.EqualTo(2));
            Assert.That(seen.Swaps, Is.EqualTo(1));
            Assert.That(seen.SidesChanged, Is.True);
            Assert.That(seen.BotExits, Is.EqualTo(1));
            Assert.That(seen.BotEscorts, Is.EqualTo(1));
            Assert.That(seen.BotTargets, Is.EqualTo(1));
            Assert.That(seen.Winner, Is.EqualTo(Side.Bravo));
            Assert.That(seen.Missing(), Is.Empty);
            Assert.That(seen.Passed(0), Is.True);
            Assert.That(seen.Passed(1), Is.False, "an error was logged");
            Assert.That(
                seen.Summary(),
                Is.EqualTo("rounds=2 round-ends=2 chosen=2 swaps=1 sides-changed=True bot-exits=1 bot-escorts=1 bot-targets=1 ended=[Vip] ended winner=1"));
        }

        [Test]
        public void FailsWithoutASwap()
        {
            var seen = Observe(Without(line => VipSmokeRun.Classify(line) == VipSmokeRun.LineKind.Swap));

            Assert.That(seen.SidesChanged, Is.False, "a changed attacker only counts after a swap line");
            Assert.That(seen.Passed(0), Is.False);
        }

        [Test]
        public void FailsWhenTheSwapLineDoesNotChangeTheAttacker()
        {
            var lines = new List<string>(TwoRoundMatch());
            lines[lines.IndexOf(VipLog.RoundLog(2, Side.Bravo, Side.Alpha))] = VipLog.RoundLog(2, Side.Alpha, Side.Bravo);

            var seen = Observe(lines);

            Assert.That(seen.Swaps, Is.EqualTo(1));
            Assert.That(seen.SidesChanged, Is.False);
            Assert.That(seen.Passed(0), Is.False);
        }

        [Test]
        public void FailsWhenTheMatchDidNotEnd()
        {
            var seen = Observe(Without(line => VipSmokeRun.Classify(line) == VipSmokeRun.LineKind.Ended));

            Assert.That(seen.Winner, Is.Null);
            Assert.That(seen.Passed(0), Is.False);
        }

        [TestCase(VipSmokeRun.LineKind.BotExit)]
        [TestCase(VipSmokeRun.LineKind.BotEscort)]
        [TestCase(VipSmokeRun.LineKind.BotTarget)]
        [TestCase(VipSmokeRun.LineKind.Chosen)]
        [TestCase(VipSmokeRun.LineKind.RoundEnd)]
        public void FailsWhenTheBotsOrTheRoundsDidNotDoTheirPart(VipSmokeRun.LineKind missing)
        {
            var seen = Observe(Without(line => VipSmokeRun.Classify(line) == missing));

            Assert.That(seen.Missing(), Is.Not.Empty);
            Assert.That(seen.Passed(0), Is.False);
        }

        [Test]
        public void ClearForgetsEverything()
        {
            var seen = Observe(TwoRoundMatch());

            seen.Clear();

            Assert.That(seen.Rounds, Is.Zero);
            Assert.That(seen.Swaps, Is.Zero);
            Assert.That(seen.SidesChanged, Is.False);
            Assert.That(seen.EndedLine, Is.Null);
            Assert.That(seen.Passed(0), Is.False);
        }
    }
}

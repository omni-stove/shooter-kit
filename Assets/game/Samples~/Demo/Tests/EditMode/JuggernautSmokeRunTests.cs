using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class JuggernautSmokeRunTests
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

        [Test]
        public void ReportsUnderItsOwnTag()
        {
            Assert.That(JuggernautSmokeRun.LogTag, Is.EqualTo("[JuggernautSmoke]"));
            Assert.That(JuggernautSmokeRun.SecondsArgument, Is.EqualTo("-juggernautSmokeSeconds"));
        }

        [Test]
        public void ReadsTheMatchLengthArgument()
        {
            Assert.That(JuggernautSmokeRun.ParseSeconds(Array.Empty<string>()), Is.EqualTo(60f));
            Assert.That(JuggernautSmokeRun.ParseSeconds(new[] { "-batchmode", "-juggernautSmokeSeconds", "75.5" }), Is.EqualTo(75.5f));
            Assert.That(JuggernautSmokeRun.ParseSeconds(new[] { "-juggernautSmokeSeconds", "12" }), Is.EqualTo(12f));
        }

        [TestCase(null)]
        [TestCase("abc")]
        [TestCase("11.9")]
        [TestCase("-30")]
        [TestCase("Infinity")]
        [TestCase("NaN")]
        public void RefusesABadMatchLength(string value)
        {
            var args = value == null
                ? new[] { JuggernautSmokeRun.SecondsArgument }
                : new[] { JuggernautSmokeRun.SecondsArgument, value };

            Assert.Throws<ArgumentException>(() => JuggernautSmokeRun.ParseSeconds(args));
        }

        [Test]
        public void RefusesNullArguments()
        {
            Assert.Throws<ArgumentNullException>(() => JuggernautSmokeRun.ParseSeconds(null));
        }

        [Test]
        public void ShortenCopiesTheDefinitionAndChangesOnlyTheTimes()
        {
            var source = Track(ScriptableObject.CreateInstance<JuggernautDefinition>());
            source.name = "Juggernaut";
            source.CopyFrom(JuggernautSpec.Default);
            source.JuggernautMaxHealth = 450;
            source.ScoreLimit = 7;
            source.RespawnSeconds = 2.5f;

            var shortened = Track(JuggernautSmokeRun.Shorten(source, 30f));

            Assert.That(shortened, Is.Not.SameAs(source));
            Assert.That(shortened.name, Is.EqualTo("Juggernaut (smoke)"));
            Assert.That(shortened.hideFlags, Is.EqualTo(HideFlags.DontSave));
            Assert.That(shortened.StartDelaySeconds, Is.EqualTo(2f));
            Assert.That(shortened.TimeLimitSeconds, Is.EqualTo(30f));
            Assert.That(shortened.JuggernautMaxHealth, Is.EqualTo(450));
            Assert.That(shortened.ScoreLimit, Is.EqualTo(7));
            Assert.That(shortened.RespawnSeconds, Is.EqualTo(2.5f));
            Assert.That(source.StartDelaySeconds, Is.EqualTo(JuggernautSpec.DefaultStartDelaySeconds));
            Assert.That(source.TimeLimitSeconds, Is.EqualTo(JuggernautSpec.DefaultTimeLimitSeconds));
        }

        [Test]
        public void ShortenStartsFromTheDefaultsWithoutADefinition()
        {
            var shortened = Track(JuggernautSmokeRun.Shorten(null, 60f));

            Assert.That(shortened.name, Is.EqualTo("JuggernautDefinition (smoke)"));
            Assert.That(shortened.StartDelaySeconds, Is.EqualTo(2f));
            Assert.That(shortened.TimeLimitSeconds, Is.EqualTo(60f));
            Assert.That(shortened.JuggernautMaxHealth, Is.EqualTo(JuggernautSpec.DefaultJuggernautMaxHealth));
            Assert.That(shortened.ScoreLimit, Is.EqualTo(JuggernautSpec.DefaultScoreLimit));
            Assert.That(shortened.RespawnSeconds, Is.EqualTo(JuggernautRunner.DefaultRespawnSeconds));
        }

        [Test]
        public void ShortenRefusesAnInvalidDefinition()
        {
            var source = Track(ScriptableObject.CreateInstance<JuggernautDefinition>());
            source.CopyFrom(JuggernautSpec.Default);
            source.ScoreLimit = 0;

            Assert.Throws<InvalidOperationException>(() => JuggernautSmokeRun.Shorten(source, 60f));
        }

        [Test]
        public void ClassifiesTheModeLinesItWaitsFor()
        {
            Assert.That(
                JuggernautSmokeRun.Classify(JuggernautLog.Changed(new JuggernautChange(3, null, JuggernautChangeReason.Chosen))),
                Is.EqualTo(JuggernautSmokeRun.LineKind.Chosen));
            Assert.That(
                JuggernautSmokeRun.Classify("[Mode] juggernaut change player=3 from=- reason=chosen"),
                Is.EqualTo(JuggernautSmokeRun.LineKind.Chosen));
            Assert.That(
                JuggernautSmokeRun.Classify(JuggernautLog.Changed(new JuggernautChange(1, 3, JuggernautChangeReason.TakenOver))),
                Is.EqualTo(JuggernautSmokeRun.LineKind.Change));
            Assert.That(
                JuggernautSmokeRun.Classify(JuggernautLog.Changed(new JuggernautChange(2, 3, JuggernautChangeReason.Repicked))),
                Is.EqualTo(JuggernautSmokeRun.LineKind.Change));
            Assert.That(
                JuggernautSmokeRun.Classify(JuggernautLog.Changed(new JuggernautChange(4, 3, JuggernautChangeReason.Left))),
                Is.EqualTo(JuggernautSmokeRun.LineKind.Change));
            Assert.That(JuggernautSmokeRun.Classify(JuggernautLog.Body(3, 300, 1.1f)), Is.EqualTo(JuggernautSmokeRun.LineKind.Body));
            Assert.That(JuggernautSmokeRun.Classify(JuggernautLog.Ended(3, 15)), Is.EqualTo(JuggernautSmokeRun.LineKind.Ended));
            Assert.That(JuggernautSmokeRun.Classify(JuggernautLog.Ended(null, 0)), Is.EqualTo(JuggernautSmokeRun.LineKind.Ended));
        }

        [Test]
        public void IgnoresOtherLines()
        {
            Assert.That(JuggernautSmokeRun.Classify(null), Is.EqualTo(JuggernautSmokeRun.LineKind.Other));
            Assert.That(JuggernautSmokeRun.Classify(ModeLog.Restart()), Is.EqualTo(JuggernautSmokeRun.LineKind.Other));
            Assert.That(JuggernautSmokeRun.Classify(ModeLog.Ended(new Side(3))), Is.EqualTo(JuggernautSmokeRun.LineKind.Other));
            Assert.That(JuggernautSmokeRun.Classify(JuggernautLog.Restored(3, 100, 1f)), Is.EqualTo(JuggernautSmokeRun.LineKind.Other));
            Assert.That(JuggernautSmokeRun.Classify(JuggernautLog.Score(3, 4)), Is.EqualTo(JuggernautSmokeRun.LineKind.Other));
            Assert.That(JuggernautSmokeRun.Classify(JuggernautLog.BotTarget(1, 3, false)), Is.EqualTo(JuggernautSmokeRun.LineKind.Other));
            Assert.That(
                JuggernautSmokeRun.Classify("[JuggernautSmoke] [Mode] juggernaut change player=3 from=- reason=chosen"),
                Is.EqualTo(JuggernautSmokeRun.LineKind.Other));
        }

        [Test]
        public void ReadsTheWinnerOrADrawFromTheEndLine()
        {
            Assert.That(JuggernautSmokeRun.WinnerOf(JuggernautLog.Ended(3, 15)), Is.EqualTo("3"));
            Assert.That(JuggernautSmokeRun.WinnerOf(JuggernautLog.Ended(null, 0)), Is.EqualTo(ModeLog.Draw));
            Assert.That(JuggernautSmokeRun.WinnerOf(ModeLog.Ended(new Side(3))), Is.Null);
            Assert.That(JuggernautSmokeRun.WinnerOf("[Mode] juggernaut end winner="), Is.Null);
            Assert.That(JuggernautSmokeRun.WinnerOf(null), Is.Null);
        }

        [Test]
        public void PassesOnlyWhenTheJuggernautChangedAndTheMatchEnded()
        {
            var ended = JuggernautLog.Ended(3, 15);

            Assert.That(JuggernautSmokeRun.Passed(true, 1, 2, ended, 0), Is.True);
            Assert.That(JuggernautSmokeRun.Passed(true, 4, 5, JuggernautLog.Ended(null, 0), 0), Is.True, "a tied time-out still ends the match");
            Assert.That(JuggernautSmokeRun.Passed(false, 1, 2, ended, 0), Is.False, "nobody was chosen");
            Assert.That(JuggernautSmokeRun.Passed(true, 0, 1, ended, 0), Is.False, "the juggernaut never changed hands");
            Assert.That(JuggernautSmokeRun.Passed(true, 1, 0, ended, 0), Is.False, "nobody was strengthened");
            Assert.That(JuggernautSmokeRun.Passed(true, 1, 2, null, 0), Is.False, "the match did not end");
            Assert.That(JuggernautSmokeRun.Passed(true, 1, 2, ended, 1), Is.False, "an error was logged");
        }

        [Test]
        public void TheShortestMatchStillLeavesTimeAfterTheFirstPick()
        {
            Assert.That(JuggernautSmokeRun.MinimumSeconds, Is.EqualTo(12f));
            Assert.That(JuggernautSmokeRun.StartDelaySeconds, Is.LessThan(JuggernautSmokeRun.MinimumSeconds));
            Assert.That(JuggernautSmokeRun.DefaultSeconds, Is.GreaterThanOrEqualTo(JuggernautSmokeRun.MinimumSeconds));
        }
    }
}

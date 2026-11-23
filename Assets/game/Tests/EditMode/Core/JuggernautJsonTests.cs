using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class JuggernautJsonTests
    {
        private const string _source = "juggernaut.json";

        private static readonly string[] _expectedItemNames =
        {
            "juggernautMaxHealth", "juggernautSpeedMultiplier", "scorePerKillAsJuggernaut", "scorePerJuggernautKill",
            "scoreLimit", "timeLimitSeconds", "startDelaySeconds",
        };

        [Test]
        public void ReadsEveryItem()
        {
            var spec = JuggernautJson.Parse(
                "{\"juggernautMaxHealth\": 450, \"juggernautSpeedMultiplier\": 0.9, \"scorePerKillAsJuggernaut\": 0,"
                + " \"scorePerJuggernautKill\": 4, \"scoreLimit\": 25, \"timeLimitSeconds\": 480, \"startDelaySeconds\": 0}",
                _source);

            Assert.That(spec.JuggernautMaxHealth, Is.EqualTo(450));
            Assert.That(spec.JuggernautSpeedMultiplier, Is.EqualTo(0.9f), "a game may make its juggernaut slower instead");
            Assert.That(spec.ScorePerKillAsJuggernaut, Is.EqualTo(0));
            Assert.That(spec.ScorePerJuggernautKill, Is.EqualTo(4));
            Assert.That(spec.ScoreLimit, Is.EqualTo(25));
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(480f));
            Assert.That(spec.StartDelaySeconds, Is.EqualTo(0f), "the first juggernaut may be picked at once");
        }

        [Test]
        public void LeftOutItemsUseTheDefaults()
        {
            var spec = JuggernautJson.Parse("{}", _source);

            Assert.That(spec.JuggernautMaxHealth, Is.EqualTo(300));
            Assert.That(spec.JuggernautSpeedMultiplier, Is.EqualTo(1.1f));
            Assert.That(spec.ScorePerKillAsJuggernaut, Is.EqualTo(1));
            Assert.That(spec.ScorePerJuggernautKill, Is.EqualTo(3));
            Assert.That(spec.ScoreLimit, Is.EqualTo(15));
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(600f));
            Assert.That(spec.StartDelaySeconds, Is.EqualTo(5f));
            Assert.That(spec.SameAs(JuggernautSpec.Default), Is.True);
        }

        [Test]
        public void TheItemNamesAreTheConstructorArguments()
        {
            Assert.That(JuggernautJson.ItemNames, Is.EqualTo(_expectedItemNames));
            Assert.That(JuggernautJson.ResourcePath, Is.EqualTo("Tuning/juggernaut"));
        }

        [Test]
        public void SameAsComparesEveryValue()
        {
            Assert.That(new JuggernautSpec().SameAs(JuggernautSpec.Default), Is.True);
            Assert.That(new JuggernautSpec(juggernautMaxHealth: 301).SameAs(JuggernautSpec.Default), Is.False);
            Assert.That(new JuggernautSpec(juggernautSpeedMultiplier: 1.2f).SameAs(JuggernautSpec.Default), Is.False);
            Assert.That(new JuggernautSpec(scorePerKillAsJuggernaut: 2).SameAs(JuggernautSpec.Default), Is.False);
            Assert.That(new JuggernautSpec(scorePerJuggernautKill: 2).SameAs(JuggernautSpec.Default), Is.False);
            Assert.That(new JuggernautSpec(scoreLimit: 16).SameAs(JuggernautSpec.Default), Is.False);
            Assert.That(new JuggernautSpec(timeLimitSeconds: 601f).SameAs(JuggernautSpec.Default), Is.False);
            Assert.That(new JuggernautSpec(startDelaySeconds: 6f).SameAs(JuggernautSpec.Default), Is.False);
            Assert.That(JuggernautSpec.Default.SameAs(null), Is.False);
        }

        [Test]
        public void ABadValueNamesItsItem()
        {
            var health = Reject("{\"juggernautMaxHealth\": 0}");
            var speed = Reject("{\"juggernautSpeedMultiplier\": 0}");
            var killAs = Reject("{\"scorePerKillAsJuggernaut\": -1}");
            var killOf = Reject("{\"scorePerJuggernautKill\": -1}");
            var noScore = Reject("{\"scorePerKillAsJuggernaut\": 0, \"scorePerJuggernautKill\": 0}");
            var limit = Reject("{\"scoreLimit\": 0}");
            var time = Reject("{\"timeLimitSeconds\": 0}");
            var delay = Reject("{\"startDelaySeconds\": -1}");
            var lateStart = Reject("{\"startDelaySeconds\": 60, \"timeLimitSeconds\": 60}");
            var fraction = Reject("{\"scoreLimit\": 1.5}");
            var unknown = Reject("{\"juggernautCount\": 1}");

            Assert.That(health.Item, Is.EqualTo("juggernautMaxHealth"));
            Assert.That(health.InnerException, Is.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(speed.Item, Is.EqualTo("juggernautSpeedMultiplier"));
            Assert.That(killAs.Item, Is.EqualTo("scorePerKillAsJuggernaut"));
            Assert.That(killOf.Item, Is.EqualTo("scorePerJuggernautKill"));
            Assert.That(noScore.Item, Is.EqualTo("scorePerJuggernautKill"));
            Assert.That(noScore.Message, Does.Contain("must be positive when scorePerKillAsJuggernaut is 0"));
            Assert.That(limit.Item, Is.EqualTo("scoreLimit"));
            Assert.That(time.Item, Is.EqualTo("timeLimitSeconds"));
            Assert.That(delay.Item, Is.EqualTo("startDelaySeconds"));
            Assert.That(lateStart.Item, Is.EqualTo("startDelaySeconds"));
            Assert.That(lateStart.Message, Does.Contain("shorter than timeLimitSeconds (60)"));
            Assert.That(fraction.Item, Is.EqualTo("scoreLimit"));
            Assert.That(unknown.Item, Is.EqualTo("juggernautCount"));
        }

        private static TuningJsonException Reject(string json)
        {
            return Assert.Throws<TuningJsonException>(() => JuggernautJson.Parse(json, _source));
        }
    }
}

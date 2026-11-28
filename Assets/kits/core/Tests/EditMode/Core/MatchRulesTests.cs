using System;
using NUnit.Framework;

namespace GameKit.Core.Tests
{
    // Unity の EditMode テストと dotnet/GameKit.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class MatchRulesTests
    {
        private static readonly Type[] _movedWithTheRules =
        {
            typeof(MatchRules),
            typeof(FriendlyFire),
            typeof(RespawnMode),
            typeof(Spectating),
            typeof(RespawnPolicy),
            typeof(IndividualRespawn),
            typeof(WaveRespawn),
            typeof(NoRespawn),
            typeof(HealthRegenerator),
        };

        [Test]
        public void TheGenreNeutralRulesLiveInTheGameKitCore()
        {
            foreach (var type in _movedWithTheRules)
            {
                Assert.That(type.Assembly.GetName().Name, Is.EqualTo("GameKit.Core"), type.Name);
                Assert.That(type.Namespace, Is.EqualTo("GameKit.Core"), type.Name);
                Assert.That(type.IsPublic, Is.True, type.Name);
            }
        }

        [Test]
        public void DefaultMatchesTheShooterKitDefaults()
        {
            var rules = MatchRules.Default;

            Assert.That(rules.FriendlyFire, Is.EqualTo(FriendlyFire.Off));
            Assert.That(rules.RespawnMode, Is.EqualTo(RespawnMode.Individual));
            Assert.That(rules.WaveIntervalSeconds, Is.EqualTo(10f));
            Assert.That(rules.RegenDelaySeconds, Is.Zero);
            Assert.That(rules.RegenPerSecond, Is.Zero);
            Assert.That(rules.SpawnProtectionSeconds, Is.Zero);
            Assert.That(rules.MinPlayers, Is.Zero);
            Assert.That(rules.IdleKickSeconds, Is.Zero);
            Assert.That(rules.IdleWarningSeconds, Is.EqualTo(15f));
            Assert.That(rules.PlayerMaxHealth, Is.EqualTo(100));
            Assert.That(rules.TextChat, Is.EqualTo(TextChat.On));
            Assert.That(rules.Spectating, Is.EqualTo(Spectating.On));
            Assert.That(rules.RespawnAnchors, Is.EqualTo(new[] { RespawnAnchor.Base }));
            Assert.That(MatchRules.MaxMinPlayers, Is.EqualTo(64));
        }

        [Test]
        public void KeepsEveryGivenValue()
        {
            var rules = new MatchRules(
                FriendlyFire.Reflect,
                RespawnMode.Wave,
                7f,
                2f,
                5f,
                1.5f,
                4,
                60f,
                20f,
                150,
                TextChat.Off,
                Spectating.Off,
                new[] { RespawnAnchor.Squad, RespawnAnchor.Base });

            Assert.That(rules.FriendlyFire, Is.EqualTo(FriendlyFire.Reflect));
            Assert.That(rules.RespawnMode, Is.EqualTo(RespawnMode.Wave));
            Assert.That(rules.WaveIntervalSeconds, Is.EqualTo(7f));
            Assert.That(rules.RegenDelaySeconds, Is.EqualTo(2f));
            Assert.That(rules.RegenPerSecond, Is.EqualTo(5f));
            Assert.That(rules.SpawnProtectionSeconds, Is.EqualTo(1.5f));
            Assert.That(rules.MinPlayers, Is.EqualTo(4));
            Assert.That(rules.IdleKickSeconds, Is.EqualTo(60f));
            Assert.That(rules.IdleWarningSeconds, Is.EqualTo(20f));
            Assert.That(rules.PlayerMaxHealth, Is.EqualTo(150));
            Assert.That(rules.TextChat, Is.EqualTo(TextChat.Off));
            Assert.That(rules.Spectating, Is.EqualTo(Spectating.Off));
            Assert.That(rules.RespawnAnchors, Is.EqualTo(new[] { RespawnAnchor.Squad, RespawnAnchor.Base }));
        }

        [Test]
        public void WithChangesOnlyTheGivenValue()
        {
            var rules = MatchRules.Default
                .WithFriendlyFire(FriendlyFire.On)
                .WithRespawnMode(RespawnMode.None)
                .WithWaveIntervalSeconds(3f)
                .WithRegenDelaySeconds(1f)
                .WithRegenPerSecond(8f)
                .WithSpawnProtectionSeconds(2f)
                .WithMinPlayers(6)
                .WithIdleKickSeconds(90f)
                .WithIdleWarningSeconds(30f)
                .WithPlayerMaxHealth(250)
                .WithTextChat(TextChat.Off)
                .WithSpectating(Spectating.Off)
                .WithRespawnAnchors(new[] { RespawnAnchor.Beacon });

            Assert.That(rules.FriendlyFire, Is.EqualTo(FriendlyFire.On));
            Assert.That(rules.RespawnMode, Is.EqualTo(RespawnMode.None));
            Assert.That(rules.WaveIntervalSeconds, Is.EqualTo(3f));
            Assert.That(rules.RegenDelaySeconds, Is.EqualTo(1f));
            Assert.That(rules.RegenPerSecond, Is.EqualTo(8f));
            Assert.That(rules.SpawnProtectionSeconds, Is.EqualTo(2f));
            Assert.That(rules.MinPlayers, Is.EqualTo(6));
            Assert.That(rules.IdleKickSeconds, Is.EqualTo(90f));
            Assert.That(rules.IdleWarningSeconds, Is.EqualTo(30f));
            Assert.That(rules.PlayerMaxHealth, Is.EqualTo(250));
            Assert.That(rules.TextChat, Is.EqualTo(TextChat.Off));
            Assert.That(rules.Spectating, Is.EqualTo(Spectating.Off));
            Assert.That(rules.RespawnAnchors, Is.EqualTo(new[] { RespawnAnchor.Beacon }));
            Assert.That(MatchRules.Default.FriendlyFire, Is.EqualTo(FriendlyFire.Off));
            Assert.That(MatchRules.Default.PlayerMaxHealth, Is.EqualTo(100));
        }

        [Test]
        public void KeepsACopyOfTheGivenAnchors()
        {
            var anchors = new[] { RespawnAnchor.Squad, RespawnAnchor.Base };
            var rules = new MatchRules(respawnAnchor: anchors);

            anchors[0] = RespawnAnchor.Beacon;

            Assert.That(rules.RespawnAnchors, Is.EqualTo(new[] { RespawnAnchor.Squad, RespawnAnchor.Base }));
        }

        [Test]
        public void RejectsABadValueWithTheJsonItemName()
        {
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => new MatchRules(friendlyFire: (FriendlyFire)99)).ParamName, Is.EqualTo("friendlyFire"));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => new MatchRules(respawnMode: (RespawnMode)99)).ParamName, Is.EqualTo("respawnMode"));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => new MatchRules(waveIntervalSeconds: 0f)).ParamName, Is.EqualTo("waveIntervalSeconds"));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => new MatchRules(regenDelaySeconds: -1f)).ParamName, Is.EqualTo("regenDelaySeconds"));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => new MatchRules(regenPerSecond: float.NaN)).ParamName, Is.EqualTo("regenPerSecond"));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => new MatchRules(spawnProtectionSeconds: float.PositiveInfinity)).ParamName, Is.EqualTo("spawnProtectionSeconds"));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => new MatchRules(minPlayers: 65)).ParamName, Is.EqualTo("minPlayers"));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => new MatchRules(idleKickSeconds: -1f)).ParamName, Is.EqualTo("idleKickSeconds"));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => new MatchRules(idleWarningSeconds: 0f)).ParamName, Is.EqualTo("idleWarningSeconds"));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => new MatchRules(playerMaxHealth: 0)).ParamName, Is.EqualTo("playerMaxHealth"));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => new MatchRules(textChat: (TextChat)99)).ParamName, Is.EqualTo("textChat"));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => new MatchRules(spectating: (Spectating)99)).ParamName, Is.EqualTo("spectating"));
            Assert.That(Assert.Throws<ArgumentException>(() => new MatchRules(respawnAnchor: Array.Empty<RespawnAnchor>())).ParamName, Is.EqualTo("respawnAnchor"));
            Assert.That(Assert.Throws<ArgumentException>(() => new MatchRules(respawnAnchor: new[] { RespawnAnchor.Base, RespawnAnchor.Base })).ParamName, Is.EqualTo("respawnAnchor"));
        }

        [Test]
        public void WithRespawnAnchorsRejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => MatchRules.Default.WithRespawnAnchors(null));
        }

        [Test]
        public void HealthRegeneratorReadsTheDelayAndRateFromTheRules()
        {
            var regenerator = new HealthRegenerator(new Health(100), new MatchRules(regenDelaySeconds: 2f, regenPerSecond: 5f));

            Assert.That(regenerator.RegenDelaySeconds, Is.EqualTo(2f));
            Assert.That(regenerator.RegenPerSecond, Is.EqualTo(5f));
        }
    }
}

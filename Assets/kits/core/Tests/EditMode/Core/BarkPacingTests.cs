using System;
using System.Collections.Generic;
using NUnit.Framework;
using Vector3 = System.Numerics.Vector3;

namespace GameKit.Core.Tests
{
    // Unity の EditMode テストと dotnet/GameKit.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class BarkPacingTests
    {
        private static readonly Vector3 _origin = Vector3.Zero;

        private static readonly Vector3 _far = new Vector3(100f, 0f, 0f);

        // 種類はゲームが決めるキーなので、シューターの種類とは別の名前で確かめる。
        private static BarkPacing Pacing(float speakerCooldownSeconds = 3f, float minIntervalSeconds = 0.75f, int maxSpeakersInCrowd = 2, float speakSeconds = 2f) =>
            new BarkPacing(
                new Dictionary<string, float> { ["greet"] = 5f, ["warn"] = 1f },
                speakerCooldownSeconds,
                minIntervalSeconds,
                maxSpeakersInCrowd: maxSpeakersInCrowd,
                speakSeconds: speakSeconds);

        [Test]
        public void TheDefaultPacingHasTheDefaultsAndNoKinds()
        {
            var pacing = BarkPacing.Default;

            Assert.That(pacing.KindCooldownSeconds, Is.Empty);
            Assert.That(pacing.SpeakerCooldownSeconds, Is.EqualTo(3f));
            Assert.That(pacing.MinIntervalSeconds, Is.EqualTo(0.75f));
            Assert.That(pacing.CrowdRadius, Is.EqualTo(12f));
            Assert.That(pacing.MaxSpeakersInCrowd, Is.EqualTo(2));
            Assert.That(pacing.HearingDistance, Is.EqualTo(30f));
            Assert.That(pacing.SpeakSeconds, Is.EqualTo(2f));
        }

        [Test]
        public void KindsAreLookedUpByTheirKey()
        {
            var pacing = Pacing();

            Assert.That(pacing.Knows("greet"), Is.True);
            Assert.That(pacing.Knows("Greet"), Is.False, "keys are compared exactly");
            Assert.That(pacing.Knows(null), Is.False);
            Assert.That(pacing.CooldownSecondsFor("greet"), Is.EqualTo(5f));
            Assert.That(pacing.CooldownSecondsFor("warn"), Is.EqualTo(1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => pacing.CooldownSecondsFor("cheer"));
            Assert.Throws<ArgumentNullException>(() => pacing.CooldownSecondsFor(null));
        }

        [Test]
        public void ThePacingKeepsItsOwnCopyOfTheCooldowns()
        {
            var cooldowns = new Dictionary<string, float> { ["greet"] = 5f };
            var pacing = new BarkPacing(cooldowns);

            cooldowns["greet"] = 50f;
            cooldowns["warn"] = 1f;

            Assert.That(pacing.CooldownSecondsFor("greet"), Is.EqualTo(5f));
            Assert.That(pacing.Knows("warn"), Is.False);
        }

        [Test]
        public void BadValuesAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BarkPacing(new Dictionary<string, float> { ["greet"] = -1f }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BarkPacing(new Dictionary<string, float> { ["greet"] = float.PositiveInfinity }));
            Assert.Throws<ArgumentException>(() => new BarkPacing(new Dictionary<string, float> { [string.Empty] = 1f }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BarkPacing(speakerCooldownSeconds: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BarkPacing(minIntervalSeconds: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BarkPacing(crowdRadius: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BarkPacing(maxSpeakersInCrowd: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BarkPacing(hearingDistance: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BarkPacing(speakSeconds: -1f));
        }

        [Test]
        public void TheSelectorKeepsEachKindsCooldownByKey()
        {
            var selector = new BarkSelector(Pacing(speakerCooldownSeconds: 0f, minIntervalSeconds: 0f, maxSpeakersInCrowd: 10));

            Assert.That(selector.TryBark("a", "greet", 0f, _origin), Is.EqualTo(BarkVerdict.Allowed));
            Assert.That(selector.TryBark("b", "greet", 4.9f, _far), Is.EqualTo(BarkVerdict.KindCooldown));
            Assert.That(selector.TryBark("b", "warn", 4.9f, _far), Is.EqualTo(BarkVerdict.Allowed), "another kind has its own cooldown");
            Assert.That(selector.TryBark("c", "warn", 5.5f, _far), Is.EqualTo(BarkVerdict.KindCooldown));
            Assert.That(selector.TryBark("c", "greet", 5f, _origin), Is.EqualTo(BarkVerdict.Allowed));
        }

        [Test]
        public void TheSelectorSharesTheSpeakerIntervalAndCrowdRules()
        {
            var selector = new BarkSelector(Pacing(maxSpeakersInCrowd: 1));

            Assert.That(selector.TryBark("a", "greet", 0f, _origin), Is.EqualTo(BarkVerdict.Allowed));
            Assert.That(selector.TryBark("a", "warn", 1f, _far), Is.EqualTo(BarkVerdict.SpeakerCooldown));
            Assert.That(selector.TryBark("b", "warn", 0.5f, _far), Is.EqualTo(BarkVerdict.GlobalInterval));
            Assert.That(selector.TryBark("b", "warn", 1f, _origin), Is.EqualTo(BarkVerdict.TooManyNearby));
            Assert.That(selector.TryBark("b", "warn", 1f, _far), Is.EqualTo(BarkVerdict.Allowed));
            Assert.That(selector.CountSpeakingNear(_origin, 1f), Is.EqualTo(1));
            Assert.That(selector.Pacing.MaxSpeakersInCrowd, Is.EqualTo(1));
        }

        [Test]
        public void TheSelectorRefusesKindsThePacingDoesNotKnow()
        {
            var selector = new BarkSelector(Pacing());

            Assert.Throws<ArgumentOutOfRangeException>(() => selector.TryBark("a", "cheer", 0f, _origin));
            Assert.Throws<ArgumentNullException>(() => selector.TryBark("a", null, 0f, _origin));
            Assert.Throws<ArgumentNullException>(() => new BarkSelector(null));
            Assert.That(selector.TryBark("a", "greet", 0f, _origin), Is.EqualTo(BarkVerdict.Allowed), "a refused kind records nothing");
        }
    }
}

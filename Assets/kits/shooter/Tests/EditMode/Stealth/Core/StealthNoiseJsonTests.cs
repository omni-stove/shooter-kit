using System;
using GameKit.Core;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class StealthNoiseJsonTests
    {
        private const float _tolerance = 1e-5f;

        private static readonly string[] _expectedItemNames =
        {
            "footstepNoiseRadius", "walkStride", "sprintStride", "crouchStride", "walkVolume", "sprintVolume", "crouchVolume",
            "minLandingFallSpeed", "hardLandingFallSpeed", "minLandingVolume", "armedGainFactor",
            "litGainFactor",
        };

        [Test]
        public void TheItemNamesAreTheSpecArgumentNames()
        {
            Assert.That(StealthNoiseJson.ItemNames, Is.EqualTo(_expectedItemNames));
        }

        [Test]
        public void TheDefaultsAreTheFormerConstants()
        {
            var spec = StealthNoiseSpec.Default;

            Assert.That(spec.FootstepNoiseRadius, Is.EqualTo(12f));
            Assert.That(spec.WalkStride, Is.EqualTo(2.2f));
            Assert.That(spec.SprintStride, Is.EqualTo(2.6f));
            Assert.That(spec.CrouchStride, Is.EqualTo(1.5f));
            Assert.That(spec.WalkVolume, Is.EqualTo(0.6f));
            Assert.That(spec.SprintVolume, Is.EqualTo(0.85f));
            Assert.That(spec.CrouchVolume, Is.EqualTo(0.2f));
            Assert.That(spec.MinLandingFallSpeed, Is.EqualTo(2.5f));
            Assert.That(spec.HardLandingFallSpeed, Is.EqualTo(12f));
            Assert.That(spec.MinLandingVolume, Is.EqualTo(0.35f));
        }

        [Test]
        public void ReadsEveryItem()
        {
            var spec = StealthNoiseJson.Parse(
                "{\"footstepNoiseRadius\": 30, \"walkStride\": 2, \"sprintStride\": 3, \"crouchStride\": 1,"
                + " \"walkVolume\": 0.5, \"sprintVolume\": 0.9, \"crouchVolume\": 0.1,"
                + " \"minLandingFallSpeed\": 3, \"hardLandingFallSpeed\": 15, \"minLandingVolume\": 0.4}",
                "stealth.json");

            Assert.That(spec.FootstepNoiseRadius, Is.EqualTo(30f));
            Assert.That(spec.WalkStride, Is.EqualTo(2f));
            Assert.That(spec.SprintStride, Is.EqualTo(3f));
            Assert.That(spec.CrouchStride, Is.EqualTo(1f));
            Assert.That(spec.WalkVolume, Is.EqualTo(0.5f));
            Assert.That(spec.SprintVolume, Is.EqualTo(0.9f));
            Assert.That(spec.CrouchVolume, Is.EqualTo(0.1f));
            Assert.That(spec.MinLandingFallSpeed, Is.EqualTo(3f));
            Assert.That(spec.HardLandingFallSpeed, Is.EqualTo(15f));
            Assert.That(spec.MinLandingVolume, Is.EqualTo(0.4f));
        }

        [Test]
        public void OmittedItemsUseTheDefaults()
        {
            var spec = StealthNoiseJson.Parse("{\"footstepNoiseRadius\": 30}", "stealth.json");

            Assert.That(spec.FootstepNoiseRadius, Is.EqualTo(30f));
            Assert.That(spec.WalkStride, Is.EqualTo(FootstepCadence.DefaultWalkStride));
            Assert.That(spec.CrouchVolume, Is.EqualTo(FootstepCadence.DefaultCrouchVolume));
            Assert.That(spec.MinLandingFallSpeed, Is.EqualTo(FootstepCadence.DefaultMinLandingFallSpeed));

            var empty = StealthNoiseJson.Parse("{}", "stealth.json");
            Assert.That(empty.FootstepNoiseRadius, Is.EqualTo(StealthNoiseSpec.DefaultFootstepNoiseRadius));
        }

        [Test]
        public void ReadsTheArmedGainFactor()
        {
            var tuning = StealthNoiseJson.ParseTuning("{\"armedGainFactor\": 2, \"footstepNoiseRadius\": 30}", "stealth.json");

            Assert.That(tuning.ArmedGainFactor, Is.EqualTo(2f));
            Assert.That(tuning.Noise.FootstepNoiseRadius, Is.EqualTo(30f));
            Assert.That(tuning.ApplyTo(AwarenessSpec.Default).ArmedGainFactor, Is.EqualTo(2f));
            Assert.That(
                StealthNoiseJson.Parse("{\"armedGainFactor\": 2}", "stealth.json").FootstepNoiseRadius,
                Is.EqualTo(StealthNoiseSpec.DefaultFootstepNoiseRadius),
                "the noise reader accepts the awareness item");
            Assert.That(StealthNoiseJson.ParseTuning("{\"armedGainFactor\": 0}", "stealth.json").ArmedGainFactor, Is.Zero);
        }

        [Test]
        public void AnOmittedArmedGainFactorKeepsTheEnemysOwnValue()
        {
            var tuning = StealthNoiseJson.ParseTuning("{}", "stealth.json");
            var own = new AwarenessSpec(armedGainFactor: 3f);

            Assert.That(tuning.ArmedGainFactor, Is.Null);
            Assert.That(tuning.ApplyTo(own), Is.SameAs(own));
            Assert.That(StealthTuningSpec.Default.ArmedGainFactor, Is.Null);
            Assert.That(StealthTuningSpec.Default.Noise, Is.SameAs(StealthNoiseSpec.Default));
            Assert.Throws<ArgumentNullException>(() => tuning.ApplyTo(null));
            Assert.Throws<ArgumentNullException>(() => _ = new StealthTuningSpec(null, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new StealthTuningSpec(StealthNoiseSpec.Default, -1f));
        }

        [Test]
        public void ReadsTheLitGainFactor()
        {
            var tuning = StealthNoiseJson.ParseTuning("{\"litGainFactor\": 1.5, \"armedGainFactor\": 2}", "stealth.json");

            Assert.That(tuning.LitGainFactor, Is.EqualTo(1.5f));
            Assert.That(tuning.ArmedGainFactor, Is.EqualTo(2f));
            var applied = tuning.ApplyTo(AwarenessSpec.Default);
            Assert.That(applied.LitGainFactor, Is.EqualTo(1.5f));
            Assert.That(applied.ArmedGainFactor, Is.EqualTo(2f), "both factors are applied together");
            Assert.That(
                StealthNoiseJson.Parse("{\"litGainFactor\": 1.5}", "stealth.json").FootstepNoiseRadius,
                Is.EqualTo(StealthNoiseSpec.DefaultFootstepNoiseRadius),
                "the noise reader accepts the awareness item");
            Assert.That(StealthNoiseJson.ParseTuning("{\"litGainFactor\": 0}", "stealth.json").LitGainFactor, Is.Zero);
        }

        [Test]
        public void AnOmittedLitGainFactorKeepsTheEnemysOwnValue()
        {
            var own = new AwarenessSpec(armedGainFactor: 2f, litGainFactor: 3f);

            Assert.That(StealthNoiseJson.ParseTuning("{}", "stealth.json").LitGainFactor, Is.Null);
            Assert.That(StealthTuningSpec.Default.LitGainFactor, Is.Null);

            var armedOnly = StealthNoiseJson.ParseTuning("{\"armedGainFactor\": 4}", "stealth.json").ApplyTo(own);
            Assert.That(armedOnly.ArmedGainFactor, Is.EqualTo(4f));
            Assert.That(armedOnly.LitGainFactor, Is.EqualTo(3f), "the enemy keeps its own lit factor");

            var litOnly = StealthNoiseJson.ParseTuning("{\"litGainFactor\": 1.5}", "stealth.json").ApplyTo(own);
            Assert.That(litOnly.LitGainFactor, Is.EqualTo(1.5f));
            Assert.That(litOnly.ArmedGainFactor, Is.EqualTo(2f), "the enemy keeps its own armed factor");

            Assert.That(new StealthTuningSpec(StealthNoiseSpec.Default, null, 1.5f).LitGainFactor, Is.EqualTo(1.5f));
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(() => _ = new StealthTuningSpec(StealthNoiseSpec.Default, null, -1f)).ParamName,
                Is.EqualTo("litGainFactor"));
        }

        [Test]
        public void ARadiusOfZeroIsSilent()
        {
            Assert.That(StealthNoiseJson.Parse("{\"footstepNoiseRadius\": 0}", "stealth.json").FootstepNoiseRadius, Is.Zero);
        }

        [TestCase("{\"footstepNoiseRadius\": -1}", "footstepNoiseRadius")]
        [TestCase("{\"walkStride\": 0}", "walkStride")]
        [TestCase("{\"sprintStride\": -2}", "sprintStride")]
        [TestCase("{\"crouchVolume\": 1.5}", "crouchVolume")]
        [TestCase("{\"minLandingFallSpeed\": 0}", "minLandingFallSpeed")]
        [TestCase("{\"hardLandingFallSpeed\": 2}", "hardLandingFallSpeed")]
        [TestCase("{\"minLandingVolume\": -0.1}", "minLandingVolume")]
        [TestCase("{\"walkVolume\": \"loud\"}", "walkVolume")]
        [TestCase("{\"walkStride\": null}", "walkStride")]
        [TestCase("{\"armedGainFactor\": -1}", "armedGainFactor")]
        [TestCase("{\"armedGainFactor\": \"high\"}", "armedGainFactor")]
        [TestCase("{\"armedGainFactor\": null}", "armedGainFactor")]
        [TestCase("{\"litGainFactor\": -1}", "litGainFactor")]
        [TestCase("{\"litGainFactor\": \"bright\"}", "litGainFactor")]
        [TestCase("{\"litGainFactor\": null}", "litGainFactor")]
        public void InvalidItemsAreNamed(string json, string item)
        {
            var e = Assert.Throws<TuningJsonException>(() => StealthNoiseJson.Parse(json, "stealth.json"));

            Assert.That(e.SourceName, Is.EqualTo("stealth.json"));
            Assert.That(e.Item, Is.EqualTo(item));
            Assert.That(e.Message, Does.Contain("stealth JSON 'stealth.json'"));
            Assert.That(e.Message, Does.Contain(item));
        }

        [Test]
        public void UnknownItemsAndBrokenJsonAreRejected()
        {
            var unknown = Assert.Throws<TuningJsonException>(() => StealthNoiseJson.Parse("{\"stride\": 2}", "stealth.json"));
            Assert.That(unknown.Item, Is.EqualTo("stride"));
            Assert.That(unknown.Message, Does.Contain("footstepNoiseRadius"), "lists the known items");

            var broken = Assert.Throws<TuningJsonException>(() => StealthNoiseJson.Parse("{\"walkStride\": ", "stealth.json"));
            Assert.That(broken.Item, Is.Null);

            var array = Assert.Throws<TuningJsonException>(() => StealthNoiseJson.Parse("[]", null));
            Assert.That(array.SourceName, Is.EqualTo("(unnamed stealth JSON)"));
        }

        [Test]
        public void TheCadenceUsesTheSpecStridesVolumesAndLanding()
        {
            var cadence = new StealthNoiseSpec(
                walkStride: 1f, sprintStride: 3f, crouchStride: 0.5f, walkVolume: 0.5f, sprintVolume: 1f, crouchVolume: 0.1f,
                minLandingFallSpeed: 4f, hardLandingFallSpeed: 8f, minLandingVolume: 0.5f).CreateCadence();

            Assert.That(cadence.StrideFor(false, false), Is.EqualTo(1f));
            Assert.That(cadence.StrideFor(false, true), Is.EqualTo(3f));
            Assert.That(cadence.StrideFor(true, false), Is.EqualTo(0.5f));
            Assert.That(cadence.VolumeFor(false, false), Is.EqualTo(0.5f));
            Assert.That(cadence.VolumeFor(false, true), Is.EqualTo(1f));
            Assert.That(cadence.VolumeFor(true, false), Is.EqualTo(0.1f));
            Assert.That(cadence.LandingVolume(3.9f), Is.Zero, "below the configured threshold, though above the default 2.5");
            Assert.That(cadence.LandingVolume(4f), Is.EqualTo(0.5f).Within(_tolerance));
            Assert.That(cadence.LandingVolume(6f), Is.EqualTo(0.75f).Within(_tolerance));
            Assert.That(cadence.LandingVolume(8f), Is.EqualTo(1f).Within(_tolerance));
            Assert.That(cadence.LandingVolume(20f), Is.EqualTo(1f).Within(_tolerance));
        }

        [Test]
        public void EachCadenceStartsFresh()
        {
            var spec = new StealthNoiseSpec(walkStride: 1f);
            var first = spec.CreateCadence();
            first.Advance(0.3f, 0f, 0.02f, true, false, false);

            var second = spec.CreateCadence();

            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(second.Progress, Is.EqualTo(FootstepCadence.FirstStepProgress));
        }

        [Test]
        public void TheCadenceRejectsAHardLandingNotAboveTheMinimum()
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => new FootstepCadence(minLandingFallSpeed: 5f, hardLandingFallSpeed: 5f));
            Assert.That(e.ParamName, Is.EqualTo("hardLandingFallSpeed"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FootstepCadence(minLandingFallSpeed: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FootstepCadence(hardLandingFallSpeed: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FootstepCadence(minLandingVolume: 1.1f));
        }
    }
}

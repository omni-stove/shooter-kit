using System;
using GameKit.Core;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Stealth.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class SecurityCameraJsonTests
    {
        private static readonly string[] _expectedTopLevelItemNames =
        {
            "footstepNoiseRadius", "walkStride", "sprintStride", "crouchStride", "walkVolume", "sprintVolume", "crouchVolume",
            "minLandingFallSpeed", "hardLandingFallSpeed", "minLandingVolume", "armedGainFactor", "litGainFactor",
            "securityCamera", "alarm",
        };

        private static readonly string[] _expectedCameraItemNames =
        {
            "sweepDegrees", "sweepDegreesPerSecond", "edgePauseSeconds", "viewDistance", "fieldOfViewDegrees", "awareness",
            "disableHoldSeconds", "disabledSeconds", "maxHealth", "disableRadius",
        };

        private static readonly string[] _expectedCameraAwarenessItemNames =
        {
            "gainPerSecond", "fullGainDistance", "farGainFactor", "peripheralGainFactor", "crouchGainFactor", "proneGainFactor",
            "stillGainFactor", "fullMotionSpeed", "decayDelaySeconds", "decayPerSecond", "noticeThreshold", "alertThreshold",
            "alarmRadius", "engagedHoldThreshold", "gunshotGain", "footstepGain", "investigateLevel", "armedGainFactor",
            "litGainFactor",
        };

        private static readonly string[] _expectedAlarmItemNames = { "durationSeconds", "radius" };

        [Test]
        public void TheItemNamesAreTheSpecArgumentNames()
        {
            Assert.That(StealthNoiseJson.TopLevelItemNames, Is.EqualTo(_expectedTopLevelItemNames));
            Assert.That(StealthNoiseJson.CameraItemNames, Is.EqualTo(_expectedCameraItemNames));
            Assert.That(StealthNoiseJson.CameraAwarenessItemNames, Is.EqualTo(_expectedCameraAwarenessItemNames));
            Assert.That(StealthNoiseJson.AlarmItemNames, Is.EqualTo(_expectedAlarmItemNames));
        }

        [Test]
        public void TheCameraDefaults()
        {
            var camera = SecurityCameraSpec.Default;

            Assert.That(camera.SweepDegrees, Is.EqualTo(90f));
            Assert.That(camera.SweepDegreesPerSecond, Is.EqualTo(20f));
            Assert.That(camera.EdgePauseSeconds, Is.EqualTo(2f));
            Assert.That(camera.ViewDistance, Is.EqualTo(15f));
            Assert.That(camera.FieldOfViewDegrees, Is.EqualTo(60f));
            Assert.That(camera.DisableHoldSeconds, Is.EqualTo(1.5f));
            Assert.That(camera.DisabledSeconds, Is.EqualTo(30f));
            Assert.That(camera.MaxHealth, Is.EqualTo(50));
            Assert.That(camera.DisableRadius, Is.EqualTo(2.5f));
            Assert.That(camera.Awareness.FieldOfViewDegrees, Is.EqualTo(60f), "the awareness sees through the camera's own view");
            Assert.That(camera.Awareness.GainPerSecond, Is.EqualTo(AwarenessSpec.DefaultGainPerSecond));
            Assert.That(camera.Awareness.AlertThreshold, Is.EqualTo(AwarenessSpec.DefaultAlertThreshold));
        }

        [Test]
        public void TheAlarmDefaults()
        {
            Assert.That(SecurityAlarmSpec.Default.DurationSeconds, Is.EqualTo(20f));
            Assert.That(SecurityAlarmSpec.Default.Radius, Is.EqualTo(30f));
        }

        [Test]
        public void TheTuningDefaultsHaveTheCameraAndAlarmDefaults()
        {
            Assert.That(StealthTuningSpec.Default.Camera, Is.SameAs(SecurityCameraSpec.Default));
            Assert.That(StealthTuningSpec.Default.Alarm, Is.SameAs(SecurityAlarmSpec.Default));

            var withoutThem = new StealthTuningSpec(StealthNoiseSpec.Default, null);
            Assert.That(withoutThem.Camera, Is.SameAs(SecurityCameraSpec.Default));
            Assert.That(withoutThem.Alarm, Is.SameAs(SecurityAlarmSpec.Default));

            var camera = new SecurityCameraSpec(sweepDegrees: 0f);
            var alarm = new SecurityAlarmSpec(radius: 0f);
            var given = new StealthTuningSpec(StealthNoiseSpec.Default, null, camera, alarm);
            Assert.That(given.Camera, Is.SameAs(camera));
            Assert.That(given.Alarm, Is.SameAs(alarm));
        }

        [Test]
        public void ReadsEveryCameraAndAlarmItem()
        {
            var tuning = StealthNoiseJson.ParseTuning(
                "{\"securityCamera\": {\"sweepDegrees\": 120, \"sweepDegreesPerSecond\": 30, \"edgePauseSeconds\": 0.5,"
                + " \"viewDistance\": 25, \"fieldOfViewDegrees\": 45, \"disableHoldSeconds\": 3, \"disabledSeconds\": 10,"
                + " \"maxHealth\": 120, \"disableRadius\": 4,"
                + " \"awareness\": {\"gainPerSecond\": 2, \"fullGainDistance\": 5, \"farGainFactor\": 0.3, \"peripheralGainFactor\": 0.6,"
                + " \"crouchGainFactor\": 0.7, \"proneGainFactor\": 0.1, \"stillGainFactor\": 0.9, \"fullMotionSpeed\": 4,"
                + " \"decayDelaySeconds\": 1, \"decayPerSecond\": 0.2, \"noticeThreshold\": 0.3, \"alertThreshold\": 0.7,"
                + " \"alarmRadius\": 8, \"engagedHoldThreshold\": 0.9, \"gunshotGain\": 0.5, \"footstepGain\": 0.05,"
                + " \"investigateLevel\": 0.8, \"armedGainFactor\": 2, \"litGainFactor\": 1.5}},"
                + " \"alarm\": {\"durationSeconds\": 45, \"radius\": 50}}",
                "stealth.json");

            var camera = tuning.Camera;
            Assert.That(camera.SweepDegrees, Is.EqualTo(120f));
            Assert.That(camera.SweepDegreesPerSecond, Is.EqualTo(30f));
            Assert.That(camera.EdgePauseSeconds, Is.EqualTo(0.5f));
            Assert.That(camera.ViewDistance, Is.EqualTo(25f));
            Assert.That(camera.FieldOfViewDegrees, Is.EqualTo(45f));
            Assert.That(camera.DisableHoldSeconds, Is.EqualTo(3f));
            Assert.That(camera.DisabledSeconds, Is.EqualTo(10f));
            Assert.That(camera.MaxHealth, Is.EqualTo(120));
            Assert.That(camera.DisableRadius, Is.EqualTo(4f));

            var awareness = camera.Awareness;
            Assert.That(awareness.FieldOfViewDegrees, Is.EqualTo(45f));
            Assert.That(awareness.GainPerSecond, Is.EqualTo(2f));
            Assert.That(awareness.FullGainDistance, Is.EqualTo(5f));
            Assert.That(awareness.FarGainFactor, Is.EqualTo(0.3f));
            Assert.That(awareness.PeripheralGainFactor, Is.EqualTo(0.6f));
            Assert.That(awareness.CrouchGainFactor, Is.EqualTo(0.7f));
            Assert.That(awareness.ProneGainFactor, Is.EqualTo(0.1f));
            Assert.That(awareness.StillGainFactor, Is.EqualTo(0.9f));
            Assert.That(awareness.FullMotionSpeed, Is.EqualTo(4f));
            Assert.That(awareness.DecayDelaySeconds, Is.EqualTo(1f));
            Assert.That(awareness.DecayPerSecond, Is.EqualTo(0.2f));
            Assert.That(awareness.NoticeThreshold, Is.EqualTo(0.3f));
            Assert.That(awareness.AlertThreshold, Is.EqualTo(0.7f));
            Assert.That(awareness.AlarmRadius, Is.EqualTo(8f));
            Assert.That(awareness.EngagedHoldThreshold, Is.EqualTo(0.9f));
            Assert.That(awareness.GunshotGain, Is.EqualTo(0.5f));
            Assert.That(awareness.FootstepGain, Is.EqualTo(0.05f));
            Assert.That(awareness.InvestigateLevel, Is.EqualTo(0.8f));
            Assert.That(awareness.ArmedGainFactor, Is.EqualTo(2f));
            Assert.That(awareness.LitGainFactor, Is.EqualTo(1.5f));

            Assert.That(tuning.Alarm.DurationSeconds, Is.EqualTo(45f));
            Assert.That(tuning.Alarm.Radius, Is.EqualTo(50f));
        }

        [Test]
        public void TheCameraAwarenessDoesNotChangeThePlayerSideValues()
        {
            var tuning = StealthNoiseJson.ParseTuning(
                "{\"securityCamera\": {\"awareness\": {\"armedGainFactor\": 3, \"litGainFactor\": 2}}, \"alarm\": {\"radius\": 5}}", "stealth.json");

            Assert.That(tuning.ArmedGainFactor, Is.Null, "the guards keep their own armed factor");
            Assert.That(tuning.LitGainFactor, Is.Null, "the guards keep their own lit factor");
            Assert.That(tuning.Camera.Awareness.LitGainFactor, Is.EqualTo(2f));
            Assert.That(tuning.Noise.FootstepNoiseRadius, Is.EqualTo(StealthNoiseSpec.DefaultFootstepNoiseRadius));
            Assert.That(tuning.Camera.Awareness.ArmedGainFactor, Is.EqualTo(3f));
        }

        [Test]
        public void OmittedItemsUseTheDefaults()
        {
            var empty = StealthNoiseJson.ParseTuning("{}", "stealth.json");
            Assert.That(empty.Camera, Is.SameAs(SecurityCameraSpec.Default));
            Assert.That(empty.Alarm, Is.SameAs(SecurityAlarmSpec.Default));

            var partial = StealthNoiseJson.ParseTuning(
                "{\"securityCamera\": {\"viewDistance\": 20, \"awareness\": {\"gainPerSecond\": 3}}, \"alarm\": {\"durationSeconds\": 5}}",
                "stealth.json");
            Assert.That(partial.Camera.ViewDistance, Is.EqualTo(20f));
            Assert.That(partial.Camera.SweepDegrees, Is.EqualTo(SecurityCameraSpec.DefaultSweepDegrees));
            Assert.That(partial.Camera.SweepDegreesPerSecond, Is.EqualTo(SecurityCameraSpec.DefaultSweepDegreesPerSecond));
            Assert.That(partial.Camera.EdgePauseSeconds, Is.EqualTo(SecurityCameraSpec.DefaultEdgePauseSeconds));
            Assert.That(partial.Camera.FieldOfViewDegrees, Is.EqualTo(SecurityCameraSpec.DefaultFieldOfViewDegrees));
            Assert.That(partial.Camera.DisableHoldSeconds, Is.EqualTo(SecurityCameraSpec.DefaultDisableHoldSeconds));
            Assert.That(partial.Camera.DisabledSeconds, Is.EqualTo(SecurityCameraSpec.DefaultDisabledSeconds));
            Assert.That(partial.Camera.MaxHealth, Is.EqualTo(SecurityCameraSpec.DefaultMaxHealth));
            Assert.That(partial.Camera.DisableRadius, Is.EqualTo(SecurityCameraSpec.DefaultDisableRadius));
            Assert.That(partial.Camera.Awareness.GainPerSecond, Is.EqualTo(3f));
            Assert.That(partial.Camera.Awareness.AlertThreshold, Is.EqualTo(AwarenessSpec.DefaultAlertThreshold));
            Assert.That(partial.Camera.Awareness.FieldOfViewDegrees, Is.EqualTo(SecurityCameraSpec.DefaultFieldOfViewDegrees));
            Assert.That(partial.Alarm.DurationSeconds, Is.EqualTo(5f));
            Assert.That(partial.Alarm.Radius, Is.EqualTo(SecurityAlarmSpec.DefaultRadius));

            var noAwareness = StealthNoiseJson.ParseTuning("{\"securityCamera\": {\"fieldOfViewDegrees\": 90}, \"alarm\": {}}", "stealth.json");
            Assert.That(noAwareness.Camera.Awareness.FieldOfViewDegrees, Is.EqualTo(90f), "the default awareness follows the camera's view");
            Assert.That(noAwareness.Camera.Awareness.GainPerSecond, Is.EqualTo(AwarenessSpec.DefaultGainPerSecond));
            Assert.That(noAwareness.Alarm.DurationSeconds, Is.EqualTo(SecurityAlarmSpec.DefaultDurationSeconds));
            Assert.That(noAwareness.Alarm.Radius, Is.EqualTo(SecurityAlarmSpec.DefaultRadius));
        }

        [Test]
        public void BoundaryValuesAreAccepted()
        {
            var tuning = StealthNoiseJson.ParseTuning(
                "{\"securityCamera\": {\"sweepDegrees\": 0, \"edgePauseSeconds\": 0, \"fieldOfViewDegrees\": 360}, \"alarm\": {\"radius\": 0}}",
                "stealth.json");

            Assert.That(tuning.Camera.SweepDegrees, Is.Zero, "a fixed camera");
            Assert.That(tuning.Camera.EdgePauseSeconds, Is.Zero);
            Assert.That(tuning.Camera.FieldOfViewDegrees, Is.EqualTo(360f));
            Assert.That(tuning.Alarm.Radius, Is.Zero, "an alarm that calls nobody");
            Assert.That(StealthNoiseJson.ParseTuning("{\"securityCamera\": {\"sweepDegrees\": 360}}", "stealth.json").Camera.SweepDegrees, Is.EqualTo(360f));
        }

        [TestCase("{\"securityCamera\": {\"sweepDegrees\": -1}}", "securityCamera.sweepDegrees")]
        [TestCase("{\"securityCamera\": {\"sweepDegrees\": 361}}", "securityCamera.sweepDegrees")]
        [TestCase("{\"securityCamera\": {\"sweepDegreesPerSecond\": 0}}", "securityCamera.sweepDegreesPerSecond")]
        [TestCase("{\"securityCamera\": {\"edgePauseSeconds\": -0.5}}", "securityCamera.edgePauseSeconds")]
        [TestCase("{\"securityCamera\": {\"viewDistance\": 0}}", "securityCamera.viewDistance")]
        [TestCase("{\"securityCamera\": {\"fieldOfViewDegrees\": 0}}", "securityCamera.fieldOfViewDegrees")]
        [TestCase("{\"securityCamera\": {\"fieldOfViewDegrees\": 400}}", "securityCamera.fieldOfViewDegrees")]
        [TestCase("{\"securityCamera\": {\"fieldOfViewDegrees\": 0, \"awareness\": {\"gainPerSecond\": 2}}}", "securityCamera.fieldOfViewDegrees")]
        [TestCase("{\"securityCamera\": {\"disableHoldSeconds\": 0}}", "securityCamera.disableHoldSeconds")]
        [TestCase("{\"securityCamera\": {\"disabledSeconds\": -1}}", "securityCamera.disabledSeconds")]
        [TestCase("{\"securityCamera\": {\"maxHealth\": 0}}", "securityCamera.maxHealth")]
        [TestCase("{\"securityCamera\": {\"maxHealth\": 2.5}}", "securityCamera.maxHealth")]
        [TestCase("{\"securityCamera\": {\"disableRadius\": 0}}", "securityCamera.disableRadius")]
        [TestCase("{\"securityCamera\": {\"viewDistance\": \"far\"}}", "securityCamera.viewDistance")]
        [TestCase("{\"securityCamera\": {\"viewDistance\": null}}", "securityCamera.viewDistance")]
        [TestCase("{\"securityCamera\": {\"awareness\": {\"gainPerSecond\": 0}}}", "securityCamera.awareness.gainPerSecond")]
        [TestCase("{\"securityCamera\": {\"awareness\": {\"farGainFactor\": 1.5}}}", "securityCamera.awareness.farGainFactor")]
        [TestCase("{\"securityCamera\": {\"awareness\": {\"noticeThreshold\": 0.7}}}", "securityCamera.awareness.noticeThreshold")]
        [TestCase("{\"securityCamera\": {\"awareness\": {\"alertThreshold\": 0.9}}}", "securityCamera.awareness.engagedHoldThreshold")]
        [TestCase("{\"securityCamera\": {\"awareness\": {\"armedGainFactor\": -1}}}", "securityCamera.awareness.armedGainFactor")]
        [TestCase("{\"securityCamera\": {\"awareness\": {\"armedGainFactor\": null}}}", "securityCamera.awareness.armedGainFactor")]
        [TestCase("{\"securityCamera\": {\"awareness\": {\"fieldOfViewDegrees\": 90}}}", "securityCamera.awareness.fieldOfViewDegrees")]
        [TestCase("{\"securityCamera\": {\"awareness\": []}}", "securityCamera.awareness")]
        [TestCase("{\"securityCamera\": {\"range\": 10}}", "securityCamera.range")]
        [TestCase("{\"securityCamera\": 5}", "securityCamera")]
        [TestCase("{\"securityCamera\": null}", "securityCamera")]
        [TestCase("{\"alarm\": {\"durationSeconds\": 0}}", "alarm.durationSeconds")]
        [TestCase("{\"alarm\": {\"durationSeconds\": -3}}", "alarm.durationSeconds")]
        [TestCase("{\"alarm\": {\"radius\": -1}}", "alarm.radius")]
        [TestCase("{\"alarm\": {\"radius\": \"wide\"}}", "alarm.radius")]
        [TestCase("{\"alarm\": {\"volume\": 1}}", "alarm.volume")]
        [TestCase("{\"alarm\": []}", "alarm")]
        public void InvalidItemsAreNamed(string json, string item)
        {
            var e = Assert.Throws<TuningJsonException>(() => StealthNoiseJson.ParseTuning(json, "stealth.json"));

            Assert.That(e.SourceName, Is.EqualTo("stealth.json"));
            Assert.That(e.Item, Is.EqualTo(item));
            Assert.That(e.Message, Does.Contain("stealth JSON 'stealth.json'"));
            Assert.That(e.Message, Does.Contain(item));
        }

        [Test]
        public void AnUnknownCameraItemListsTheCameraItems()
        {
            var e = Assert.Throws<TuningJsonException>(() => StealthNoiseJson.ParseTuning("{\"securityCamera\": {\"range\": 10}}", "stealth.json"));

            Assert.That(e.Message, Does.Contain("sweepDegrees"));
            Assert.That(e.Message, Does.Contain("disabledSeconds"));
        }

        [Test]
        public void TheSpecsRejectInvalidValues()
        {
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => _ = new SecurityCameraSpec(sweepDegrees: float.NaN)).ParamName, Is.EqualTo("sweepDegrees"));
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(() => _ = new SecurityCameraSpec(sweepDegreesPerSecond: float.PositiveInfinity)).ParamName,
                Is.EqualTo("sweepDegreesPerSecond"));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => _ = new SecurityCameraSpec(viewDistance: float.NaN)).ParamName, Is.EqualTo("viewDistance"));
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(() => _ = new SecurityCameraSpec(disabledSeconds: float.PositiveInfinity)).ParamName,
                Is.EqualTo("disabledSeconds"));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => _ = new SecurityCameraSpec(maxHealth: -1)).ParamName, Is.EqualTo("maxHealth"));
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(() => _ = new SecurityCameraSpec(disableRadius: float.NaN)).ParamName,
                Is.EqualTo("disableRadius"));
            Assert.That(new SecurityCameraSpec(maxHealth: 1).MaxHealth, Is.EqualTo(1), "one hit breaks it");
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => _ = new SecurityAlarmSpec(durationSeconds: float.NaN)).ParamName, Is.EqualTo("durationSeconds"));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => _ = new SecurityAlarmSpec(radius: float.PositiveInfinity)).ParamName, Is.EqualTo("radius"));
        }

        [Test]
        public void TheCameraAwarenessMustSeeThroughTheCamerasView()
        {
            var matching = new AwarenessSpec(fieldOfViewDegrees: 45f, gainPerSecond: 2f);
            var camera = new SecurityCameraSpec(fieldOfViewDegrees: 45f, awareness: matching);
            Assert.That(camera.Awareness, Is.SameAs(matching));

            var e = Assert.Throws<ArgumentException>(() => _ = new SecurityCameraSpec(fieldOfViewDegrees: 45f, awareness: AwarenessSpec.Default));
            Assert.That(e.ParamName, Is.EqualTo("awareness"));

            Assert.That(new SecurityCameraSpec(fieldOfViewDegrees: 30f).Awareness.FieldOfViewDegrees, Is.EqualTo(30f));
        }
    }
}

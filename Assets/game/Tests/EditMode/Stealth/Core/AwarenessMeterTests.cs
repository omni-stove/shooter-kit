using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class AwarenessMeterTests
    {
        private const float _sightRange = 20f;

        private static AwarenessSighting Seen(
            float distance = 10f,
            float angle = 0f,
            AwarenessPosture posture = AwarenessPosture.Standing,
            float light = 1f,
            float speed = 0f,
            bool armed = true,
            bool lit = false) =>
            new AwarenessSighting(true, distance, angle, posture, light, speed, armed, lit);

        private static float Gain(AwarenessSighting sighting) => AwarenessMeter.GainPerSecond(AwarenessSpec.Default, _sightRange, sighting);

        [Test]
        public void NothingFillsWithoutLineOfSightOutsideTheViewOrBeyondTheRange()
        {
            Assert.That(Gain(new AwarenessSighting(false, 5f, 0f, AwarenessPosture.Standing, 1f, 0f)), Is.EqualTo(0f));
            Assert.That(Gain(Seen(angle: (AwarenessSpec.DefaultFieldOfViewDegrees / 2f) + 1f)), Is.EqualTo(0f));
            Assert.That(Gain(Seen(distance: _sightRange + 0.1f)), Is.EqualTo(0f));
            Assert.That(Gain(AwarenessSighting.None), Is.EqualTo(0f));
            Assert.That(Gain(Seen(light: 0f)), Is.EqualTo(0f), "complete darkness");
        }

        [Test]
        public void EachFactorSlowsTheMeterDown()
        {
            var baseline = Gain(Seen());

            Assert.That(baseline, Is.GreaterThan(0f));
            Assert.That(Gain(Seen(distance: 15f)), Is.LessThan(baseline), "farther");
            Assert.That(Gain(Seen(distance: 1f)), Is.EqualTo(Gain(Seen(distance: AwarenessSpec.DefaultFullGainDistance))), "full gain up close");
            Assert.That(Gain(Seen(angle: 50f)), Is.LessThan(baseline), "off-center");
            Assert.That(Gain(Seen(posture: AwarenessPosture.Crouching)), Is.LessThan(baseline), "crouching");
            Assert.That(Gain(Seen(posture: AwarenessPosture.Prone)), Is.LessThan(Gain(Seen(posture: AwarenessPosture.Crouching))), "prone");
            Assert.That(Gain(Seen(light: 0.5f)), Is.EqualTo(baseline * 0.5f).Within(1e-6f), "darker");
            Assert.That(Gain(Seen(speed: 3f)), Is.GreaterThan(baseline), "moving");
            Assert.That(Gain(Seen(speed: 100f)), Is.EqualTo(Gain(Seen(speed: AwarenessSpec.DefaultFullMotionSpeed))), "speed is capped");
        }

        [Test]
        public void GainMatchesTheFactorsAtTheEdges()
        {
            var spec = AwarenessSpec.Default;
            var edge = Gain(Seen(distance: _sightRange, angle: spec.FieldOfViewDegrees / 2f, speed: spec.FullMotionSpeed));

            Assert.That(edge, Is.EqualTo(spec.GainPerSecond * spec.FarGainFactor * spec.PeripheralGainFactor).Within(1e-6f));
            Assert.That(
                Gain(Seen(distance: 0f)),
                Is.EqualTo(spec.GainPerSecond * spec.StillGainFactor).Within(1e-6f),
                "close, centered and still");
        }

        [Test]
        public void StagesRiseInOrderAndAreReportedOnce()
        {
            var meter = new AwarenessMeter(AwarenessSpec.Default);
            var changes = new List<(AwarenessStage, AwarenessStage)>();
            meter.StageChanged += (from, to) => changes.Add((from, to));

            for (var i = 0; i < 200 && meter.Stage != AwarenessStage.Engaged; i++)
            {
                meter.Tick(0.05f, _sightRange, Seen(distance: 3f, speed: 6f));
            }

            Assert.That(meter.Level, Is.EqualTo(AwarenessMeter.Full));
            Assert.That(changes, Is.EqualTo(new[]
            {
                (AwarenessStage.Unaware, AwarenessStage.Noticed),
                (AwarenessStage.Noticed, AwarenessStage.Alerted),
                (AwarenessStage.Alerted, AwarenessStage.Engaged),
            }));
        }

        [Test]
        public void CrouchingFarAwayTakesLongerToNoticeThanStandingClose()
        {
            Assert.That(
                SecondsToNotice(Seen(distance: 15f, posture: AwarenessPosture.Crouching, speed: 2.5f)),
                Is.GreaterThan(SecondsToNotice(Seen(distance: 4f, speed: 5f)) * 3f));
        }

        [Test]
        public void TheMeterWaitsThenDrainsWhenTheTargetIsGone()
        {
            var spec = new AwarenessSpec(decayDelaySeconds: 1f, decayPerSecond: 0.2f);
            var meter = new AwarenessMeter(spec);
            meter.Raise(0.5f);

            meter.Tick(0.9f, _sightRange, AwarenessSighting.None);
            Assert.That(meter.Level, Is.EqualTo(0.5f), "still within the delay");
            Assert.That(meter.IsSeeing, Is.False);

            meter.Tick(0.6f, _sightRange, AwarenessSighting.None);
            Assert.That(meter.Level, Is.EqualTo(0.5f - (0.2f * 0.5f)).Within(1e-5f), "only the time past the delay drains");

            meter.Tick(10f, _sightRange, AwarenessSighting.None);
            Assert.That(meter.Level, Is.EqualTo(0f));
            Assert.That(meter.Stage, Is.EqualTo(AwarenessStage.Unaware));
        }

        [Test]
        public void SeeingAgainRestartsTheDelay()
        {
            var spec = new AwarenessSpec(decayDelaySeconds: 1f, decayPerSecond: 0.2f);
            var meter = new AwarenessMeter(spec);
            meter.Raise(0.5f);
            meter.Tick(0.9f, _sightRange, AwarenessSighting.None);

            meter.Tick(0f, _sightRange, Seen());
            meter.Tick(0.9f, _sightRange, AwarenessSighting.None);

            Assert.That(meter.Level, Is.EqualTo(0.5f));
        }

        [Test]
        public void EngagedHoldsThenStepsDownThroughAlertAndNotice()
        {
            var spec = new AwarenessSpec(
                decayDelaySeconds: 0f, decayPerSecond: 0.1f, noticeThreshold: 0.2f, alertThreshold: 0.6f, engagedHoldThreshold: 0.8f);
            var meter = new AwarenessMeter(spec);
            var changes = new List<(AwarenessStage, AwarenessStage)>();
            meter.StageChanged += (from, to) => changes.Add((from, to));
            meter.Raise(AwarenessMeter.Full);

            meter.Tick(1.5f, _sightRange, AwarenessSighting.None);
            Assert.That(meter.Level, Is.EqualTo(0.85f).Within(1e-5f));
            Assert.That(meter.Stage, Is.EqualTo(AwarenessStage.Engaged), "a short loss of sight keeps the fight going");

            meter.Tick(1f, _sightRange, AwarenessSighting.None);
            Assert.That(meter.Stage, Is.EqualTo(AwarenessStage.Alerted));

            meter.Tick(2f, _sightRange, AwarenessSighting.None);
            Assert.That(meter.Stage, Is.EqualTo(AwarenessStage.Noticed));

            Assert.That(changes, Is.EqualTo(new[]
            {
                (AwarenessStage.Unaware, AwarenessStage.Engaged),
                (AwarenessStage.Engaged, AwarenessStage.Alerted),
                (AwarenessStage.Alerted, AwarenessStage.Noticed),
            }));
        }

        [Test]
        public void StageForUsesTheThresholds()
        {
            var spec = AwarenessSpec.Default;

            Assert.That(AwarenessMeter.StageFor(spec, 0f, AwarenessStage.Unaware), Is.EqualTo(AwarenessStage.Unaware));
            Assert.That(AwarenessMeter.StageFor(spec, spec.NoticeThreshold, AwarenessStage.Unaware), Is.EqualTo(AwarenessStage.Noticed));
            Assert.That(AwarenessMeter.StageFor(spec, spec.AlertThreshold, AwarenessStage.Noticed), Is.EqualTo(AwarenessStage.Alerted));
            Assert.That(AwarenessMeter.StageFor(spec, 0.99f, AwarenessStage.Alerted), Is.EqualTo(AwarenessStage.Alerted));
            Assert.That(AwarenessMeter.StageFor(spec, 0.99f, AwarenessStage.Engaged), Is.EqualTo(AwarenessStage.Engaged));
            Assert.That(AwarenessMeter.StageFor(spec, spec.EngagedHoldThreshold, AwarenessStage.Engaged), Is.EqualTo(AwarenessStage.Engaged));
            Assert.That(AwarenessMeter.StageFor(spec, spec.AlertThreshold, AwarenessStage.Engaged), Is.EqualTo(AwarenessStage.Alerted));
            Assert.That(AwarenessMeter.StageFor(spec, 1f, AwarenessStage.Unaware), Is.EqualTo(AwarenessStage.Engaged));
        }

        [Test]
        public void RaiseNeverLowersAndResetClears()
        {
            var meter = new AwarenessMeter(AwarenessSpec.Default);
            meter.Raise(0.7f);
            meter.Raise(0.3f);
            Assert.That(meter.Level, Is.EqualTo(0.7f));
            Assert.That(meter.Stage, Is.EqualTo(AwarenessStage.Alerted));

            meter.Reset();
            Assert.That(meter.Level, Is.EqualTo(0f));
            Assert.That(meter.Stage, Is.EqualTo(AwarenessStage.Unaware));

            Assert.Throws<ArgumentOutOfRangeException>(() => meter.Raise(1.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => meter.Raise(float.NaN));
        }

        [Test]
        public void InvalidInputsAreRejected()
        {
            var meter = new AwarenessMeter(AwarenessSpec.Default);

            Assert.Throws<ArgumentNullException>(() => _ = new AwarenessMeter(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => meter.Tick(-1f, _sightRange, Seen()));
            Assert.Throws<ArgumentOutOfRangeException>(() => meter.Tick(float.PositiveInfinity, _sightRange, Seen()));
            Assert.Throws<ArgumentOutOfRangeException>(() => meter.Tick(0.1f, 0f, Seen()));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new AwarenessSighting(true, -1f, 0f, AwarenessPosture.Standing, 1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new AwarenessSighting(true, 1f, 181f, AwarenessPosture.Standing, 1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new AwarenessSighting(true, 1f, 0f, AwarenessPosture.Standing, 1.5f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new AwarenessSighting(true, 1f, 0f, (AwarenessPosture)9, 1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new AwarenessSighting(true, 1f, 0f, AwarenessPosture.Standing, 1f, float.NaN));
        }

        [Test]
        public void SpecRejectsValuesOutOfRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new AwarenessSpec(fieldOfViewDegrees: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new AwarenessSpec(fieldOfViewDegrees: 361f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new AwarenessSpec(gainPerSecond: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new AwarenessSpec(crouchGainFactor: 1.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new AwarenessSpec(fullMotionSpeed: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new AwarenessSpec(decayPerSecond: -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new AwarenessSpec(noticeThreshold: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new AwarenessSpec(noticeThreshold: 0.7f, alertThreshold: 0.6f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new AwarenessSpec(alertThreshold: 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new AwarenessSpec(alertThreshold: 0.6f, engagedHoldThreshold: 0.6f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new AwarenessSpec(engagedHoldThreshold: 1.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new AwarenessSpec(alarmRadius: float.PositiveInfinity));
            Assert.That(AwarenessSpec.Default.PostureFactor(AwarenessPosture.Standing), Is.EqualTo(1f));
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(() => _ = new AwarenessSpec(armedGainFactor: -0.1f)).ParamName,
                Is.EqualTo("armedGainFactor"));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new AwarenessSpec(armedGainFactor: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new AwarenessSpec(armedGainFactor: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => AwarenessSpec.Default.WithArmedGainFactor(-1f));
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(() => _ = new AwarenessSpec(litGainFactor: -0.1f)).ParamName,
                Is.EqualTo("litGainFactor"));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new AwarenessSpec(litGainFactor: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new AwarenessSpec(litGainFactor: float.PositiveInfinity));
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(() => AwarenessSpec.Default.WithLitGainFactor(-1f)).ParamName,
                Is.EqualTo("litGainFactor"));
        }

        [Test]
        public void TheLitFactorDefaultsToOneSoLitAndDarkFillAlike()
        {
            Assert.That(AwarenessSpec.DefaultLitGainFactor, Is.EqualTo(1f));
            Assert.That(AwarenessSpec.Default.LitGainFactor, Is.EqualTo(1f));
            Assert.That(AwarenessSpec.Default.LitFactor(true), Is.EqualTo(1f));
            Assert.That(AwarenessSpec.Default.LitFactor(false), Is.EqualTo(1f));
            Assert.That(Gain(Seen(lit: true)), Is.EqualTo(Gain(Seen(lit: false))));
            Assert.That(new AwarenessSighting(true, 1f, 0f, AwarenessPosture.Standing, 1f, 0f).IsLit, Is.False, "targets without a light count as dark");
            Assert.That(AwarenessSighting.None.IsLit, Is.False);
        }

        [Test]
        public void ALitFactorOfOneAndAHalfFillsOneAndAHalfTimesAsFast()
        {
            var spec = new AwarenessSpec(litGainFactor: 1.5f);
            var lit = AwarenessMeter.GainPerSecond(spec, _sightRange, Seen(lit: true));
            var dark = AwarenessMeter.GainPerSecond(spec, _sightRange, Seen(lit: false));

            Assert.That(spec.LitFactor(true), Is.EqualTo(1.5f));
            Assert.That(spec.LitFactor(false), Is.EqualTo(1f));
            Assert.That(dark, Is.EqualTo(Gain(Seen(lit: false))).Within(1e-6f), "a dark light fills as before");
            Assert.That(lit, Is.EqualTo(dark * 1.5f).Within(1e-6f));
            Assert.That(SecondsToNotice(spec, Seen(lit: true)), Is.LessThan(SecondsToNotice(spec, Seen(lit: false))));

            var litMeter = new AwarenessMeter(spec);
            var darkMeter = new AwarenessMeter(spec);
            litMeter.Tick(0.1f, _sightRange, Seen(lit: true));
            darkMeter.Tick(0.1f, _sightRange, Seen(lit: false));
            Assert.That(litMeter.Level, Is.EqualTo(darkMeter.Level * 1.5f).Within(1e-6f));
        }

        [Test]
        public void TheLitFactorMultipliesWithTheArmedFactor()
        {
            var spec = new AwarenessSpec(armedGainFactor: 2f, litGainFactor: 1.5f);
            var neither = AwarenessMeter.GainPerSecond(spec, _sightRange, Seen(armed: false, lit: false));

            Assert.That(AwarenessMeter.GainPerSecond(spec, _sightRange, Seen(armed: true, lit: false)), Is.EqualTo(neither * 2f).Within(1e-6f));
            Assert.That(AwarenessMeter.GainPerSecond(spec, _sightRange, Seen(armed: false, lit: true)), Is.EqualTo(neither * 1.5f).Within(1e-6f));
            Assert.That(AwarenessMeter.GainPerSecond(spec, _sightRange, Seen(armed: true, lit: true)), Is.EqualTo(neither * 3f).Within(1e-6f));
            Assert.That(
                AwarenessMeter.GainPerSecond(spec, _sightRange, Seen(armed: true, lit: true, light: 0f)),
                Is.Zero,
                "a light on the weapon does not make a pitch-dark spot visible on its own");
            Assert.That(
                AwarenessMeter.GainPerSecond(spec, _sightRange, new AwarenessSighting(false, 10f, 0f, AwarenessPosture.Standing, 1f, 0f, true, true)),
                Is.Zero,
                "without line of sight the light adds nothing");
        }

        [Test]
        public void WithLitGainFactorKeepsTheOtherValues()
        {
            var spec = new AwarenessSpec(fieldOfViewDegrees: 90f, gainPerSecond: 2f, alarmRadius: 8f, investigateLevel: 0.7f, armedGainFactor: 2f);

            var lit = spec.WithLitGainFactor(3f);

            Assert.That(lit, Is.Not.SameAs(spec));
            Assert.That(lit.LitGainFactor, Is.EqualTo(3f));
            Assert.That(spec.LitGainFactor, Is.EqualTo(AwarenessSpec.DefaultLitGainFactor));
            Assert.That(lit.ArmedGainFactor, Is.EqualTo(2f));
            Assert.That(lit.FieldOfViewDegrees, Is.EqualTo(90f));
            Assert.That(lit.GainPerSecond, Is.EqualTo(2f));
            Assert.That(lit.AlarmRadius, Is.EqualTo(8f));
            Assert.That(lit.InvestigateLevel, Is.EqualTo(0.7f));
            Assert.That(lit.FootstepGain, Is.EqualTo(spec.FootstepGain));
            Assert.That(lit.WithArmedGainFactor(4f).LitGainFactor, Is.EqualTo(3f), "changing the armed factor keeps the lit factor");
        }

        [Test]
        public void TheArmedFactorDefaultsToOneSoArmedAndUnarmedFillAlike()
        {
            Assert.That(AwarenessSpec.DefaultArmedGainFactor, Is.EqualTo(1f));
            Assert.That(AwarenessSpec.Default.ArmedGainFactor, Is.EqualTo(1f));
            Assert.That(AwarenessSpec.Default.ArmedFactor(true), Is.EqualTo(1f));
            Assert.That(AwarenessSpec.Default.ArmedFactor(false), Is.EqualTo(1f));
            Assert.That(Gain(Seen(armed: true)), Is.EqualTo(Gain(Seen(armed: false))));
            Assert.That(new AwarenessSighting(true, 1f, 0f, AwarenessPosture.Standing, 1f, 0f).IsArmed, Is.True, "targets that cannot holster count as armed");
        }

        [Test]
        public void TheArmedFactorScalesOnlyTheArmedGain()
        {
            var spec = new AwarenessSpec(armedGainFactor: 2f);
            var armed = AwarenessMeter.GainPerSecond(spec, _sightRange, Seen(armed: true));
            var unarmed = AwarenessMeter.GainPerSecond(spec, _sightRange, Seen(armed: false));

            Assert.That(spec.ArmedFactor(true), Is.EqualTo(2f));
            Assert.That(spec.ArmedFactor(false), Is.EqualTo(1f));
            Assert.That(unarmed, Is.EqualTo(Gain(Seen(armed: false))).Within(1e-6f), "holstered fills as before");
            Assert.That(armed, Is.EqualTo(unarmed * 2f).Within(1e-6f));
            Assert.That(SecondsToNotice(spec, Seen(armed: true)), Is.LessThan(SecondsToNotice(spec, Seen(armed: false))));
            Assert.That(
                AwarenessMeter.GainPerSecond(new AwarenessSpec(armedGainFactor: 0.5f), _sightRange, Seen(armed: true)),
                Is.EqualTo(unarmed * 0.5f).Within(1e-6f),
                "a game may also make armed targets harder to notice");
        }

        [Test]
        public void WithArmedGainFactorKeepsTheOtherValues()
        {
            var spec = new AwarenessSpec(fieldOfViewDegrees: 90f, gainPerSecond: 2f, alarmRadius: 8f, investigateLevel: 0.7f);

            var armed = spec.WithArmedGainFactor(3f);

            Assert.That(armed, Is.Not.SameAs(spec));
            Assert.That(armed.ArmedGainFactor, Is.EqualTo(3f));
            Assert.That(spec.ArmedGainFactor, Is.EqualTo(AwarenessSpec.DefaultArmedGainFactor));
            Assert.That(armed.FieldOfViewDegrees, Is.EqualTo(90f));
            Assert.That(armed.GainPerSecond, Is.EqualTo(2f));
            Assert.That(armed.AlarmRadius, Is.EqualTo(8f));
            Assert.That(armed.InvestigateLevel, Is.EqualTo(0.7f));
            Assert.That(armed.FootstepGain, Is.EqualTo(spec.FootstepGain));
        }

        private static float SecondsToNotice(AwarenessSpec spec, AwarenessSighting sighting)
        {
            var meter = new AwarenessMeter(spec);
            var seconds = 0f;
            while (meter.Stage == AwarenessStage.Unaware && seconds < 100f)
            {
                meter.Tick(0.01f, _sightRange, sighting);
                seconds += 0.01f;
            }

            return seconds;
        }

        private static float SecondsToNotice(AwarenessSighting sighting)
        {
            var meter = new AwarenessMeter(AwarenessSpec.Default);
            var seconds = 0f;
            while (meter.Stage == AwarenessStage.Unaware && seconds < 100f)
            {
                meter.Tick(0.01f, _sightRange, sighting);
                seconds += 0.01f;
            }

            return seconds;
        }
    }
}

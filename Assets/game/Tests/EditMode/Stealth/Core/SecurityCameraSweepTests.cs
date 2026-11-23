using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Stealth.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class SecurityCameraSweepTests
    {
        private const float _step = 0.25f;

        private static readonly Vector3 _where = new Vector3(1f, 0f, 2f);

        private static SecurityCameraTarget Still(float bearing, float distance = 2f) =>
            new SecurityCameraTarget(true, distance, bearing, _where, AwarenessPosture.Standing, 1f, 0f);

        private static SecurityCameraTarget Running(float bearing, Vector3? at = null) =>
            new SecurityCameraTarget(true, 2f, bearing, at ?? _where, AwarenessPosture.Standing, 1f, AwarenessSpec.DefaultFullMotionSpeed);

        private static void Run(SecurityCameraSweep camera, float seconds, SecurityCameraTarget target)
        {
            var ticks = (int)Math.Round(seconds / _step);
            for (var i = 0; i < ticks; i++)
            {
                camera.Tick(_step, target);
            }
        }

        private static List<(SecurityCameraPhase, SecurityCameraPhase)> Record(SecurityCameraSweep camera)
        {
            var changes = new List<(SecurityCameraPhase, SecurityCameraPhase)>();
            camera.PhaseChanged += (from, to) => changes.Add((from, to));
            return changes;
        }

        [Test]
        public void StartsSweepingFromTheCenter()
        {
            var camera = new SecurityCameraSweep(SecurityCameraSpec.Default);

            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Sweeping));
            Assert.That(camera.YawDegrees, Is.Zero);
            Assert.That(camera.Level, Is.Zero);
            Assert.That(camera.Stage, Is.EqualTo(AwarenessStage.Unaware));
            Assert.That(camera.IsActive, Is.True);
            Assert.That(camera.IsPausedAtEdge, Is.False);
        }

        [Test]
        public void TheSweepTurnsAtAConstantSpeedAndPausesAtEachEdge()
        {
            var camera = new SecurityCameraSweep(SecurityCameraSpec.Default);

            Run(camera, 1f, SecurityCameraTarget.None);
            Assert.That(camera.YawDegrees, Is.EqualTo(20f), "1 s at 20 deg/s");

            Run(camera, 1.25f, SecurityCameraTarget.None);
            Assert.That(camera.YawDegrees, Is.EqualTo(45f), "reaches the edge at 2.25 s");
            Assert.That(camera.IsPausedAtEdge, Is.True);

            Run(camera, 1.75f, SecurityCameraTarget.None);
            Assert.That(camera.YawDegrees, Is.EqualTo(45f), "still holding at 4.0 s");
            Assert.That(camera.IsPausedAtEdge, Is.True);

            Run(camera, 0.25f, SecurityCameraTarget.None);
            Assert.That(camera.YawDegrees, Is.EqualTo(45f), "the 2 s hold ends at 4.25 s");
            Assert.That(camera.IsPausedAtEdge, Is.False);

            Run(camera, 1f, SecurityCameraTarget.None);
            Assert.That(camera.YawDegrees, Is.EqualTo(25f), "turned back toward the other edge");

            Run(camera, 3.5f, SecurityCameraTarget.None);
            Assert.That(camera.YawDegrees, Is.EqualTo(-45f), "the far edge at 8.75 s");
            Assert.That(camera.IsPausedAtEdge, Is.True);

            Run(camera, 2f, SecurityCameraTarget.None);
            Assert.That(camera.YawDegrees, Is.EqualTo(-45f), "holds until 10.75 s");

            Run(camera, 1f, SecurityCameraTarget.None);
            Assert.That(camera.YawDegrees, Is.EqualTo(-25f));
            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Sweeping));
        }

        [Test]
        public void OneLongTickSweepsTheSameAsManyShortOnes()
        {
            var once = new SecurityCameraSweep(SecurityCameraSpec.Default);
            var many = new SecurityCameraSweep(SecurityCameraSpec.Default);

            once.Tick(5f, SecurityCameraTarget.None);
            Run(many, 5f, SecurityCameraTarget.None);

            Assert.That(once.YawDegrees, Is.EqualTo(30f));
            Assert.That(many.YawDegrees, Is.EqualTo(30f));
        }

        [Test]
        public void WithoutAnEdgePauseTheSweepTurnsBackAtOnce()
        {
            var camera = new SecurityCameraSweep(new SecurityCameraSpec(edgePauseSeconds: 0f));

            camera.Tick(2.25f, SecurityCameraTarget.None);
            Assert.That(camera.YawDegrees, Is.EqualTo(45f));
            Assert.That(camera.IsPausedAtEdge, Is.False);

            camera.Tick(0.25f, SecurityCameraTarget.None);
            Assert.That(camera.YawDegrees, Is.EqualTo(40f));

            var straight = new SecurityCameraSweep(new SecurityCameraSpec(edgePauseSeconds: 0f));
            straight.Tick(3f, SecurityCameraTarget.None);
            Assert.That(straight.YawDegrees, Is.EqualTo(30f));
        }

        [Test]
        public void AFixedCameraDoesNotTurn()
        {
            var camera = new SecurityCameraSweep(new SecurityCameraSpec(sweepDegrees: 0f));

            camera.Tick(10f, SecurityCameraTarget.None);
            Assert.That(camera.YawDegrees, Is.Zero);

            camera.Tick(0.25f, Still(20f));
            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Tracking));
            Assert.That(camera.YawDegrees, Is.Zero, "cannot turn to follow either");
        }

        [Test]
        public void StandingStillInTheMiddleOfTheViewIsSpottedInTwoSeconds()
        {
            var camera = new SecurityCameraSweep(SecurityCameraSpec.Default);
            var changes = Record(camera);

            Run(camera, 1.75f, Still(0f));
            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Tracking));
            Assert.That(camera.Level, Is.EqualTo(0.875f));
            Assert.That(camera.YawDegrees, Is.Zero, "stops sweeping while it tracks");

            Run(camera, 0.25f, Still(0f));
            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Spotted));
            Assert.That(camera.Level, Is.EqualTo(AwarenessMeter.Full));
            Assert.That(camera.SpottedAt, Is.EqualTo(_where));
            Assert.That(changes, Is.EqualTo(new[]
            {
                (SecurityCameraPhase.Sweeping, SecurityCameraPhase.Tracking),
                (SecurityCameraPhase.Tracking, SecurityCameraPhase.Spotted),
            }));
        }

        [Test]
        public void ARunningTargetIsSpottedInOneSecond()
        {
            var camera = new SecurityCameraSweep(SecurityCameraSpec.Default);

            Run(camera, 0.75f, Running(0f));
            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Tracking));

            Run(camera, 0.25f, Running(0f));
            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Spotted));
        }

        [Test]
        public void TheCameraTurnsToFollowWhatItSees()
        {
            var camera = new SecurityCameraSweep(SecurityCameraSpec.Default);

            camera.Tick(_step, Still(20f));
            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Tracking));
            Assert.That(camera.YawDegrees, Is.EqualTo(5f), "turns at the sweep speed");
            Assert.That(camera.Level, Is.EqualTo(0.0875f).Within(1e-6f), "counted at the 15 deg left after turning, not the 20 deg before");

            Run(camera, 0.75f, Still(20f));
            Assert.That(camera.YawDegrees, Is.EqualTo(20f), "faces the target after 1 s");

            Run(camera, 1f, Still(20f));
            Assert.That(camera.YawDegrees, Is.EqualTo(20f), "stays on it");
        }

        [Test]
        public void TheSweepBringsATargetIntoViewAndThenTracksIt()
        {
            var camera = new SecurityCameraSweep(SecurityCameraSpec.Default);

            Run(camera, 0.75f, Still(44f));
            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Sweeping));
            Assert.That(camera.YawDegrees, Is.EqualTo(15f));
            Assert.That(camera.Level, Is.Zero);

            camera.Tick(_step, Still(44f));
            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Tracking));
            Assert.That(camera.YawDegrees, Is.EqualTo(20f));
            Assert.That(camera.Level, Is.GreaterThan(0f));
        }

        [Test]
        public void TrackingDoesNotTurnPastTheEdge()
        {
            var camera = new SecurityCameraSweep(SecurityCameraSpec.Default);
            camera.Tick(2.25f, SecurityCameraTarget.None);

            Run(camera, 1f, Still(60f));
            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Tracking));
            Assert.That(camera.YawDegrees, Is.EqualTo(45f));
        }

        [Test]
        public void TargetsOutsideTheViewAreIgnored()
        {
            var reference = new SecurityCameraSweep(SecurityCameraSpec.Default);
            Run(reference, 20f, SecurityCameraTarget.None);

            var cases = new[]
            {
                ("behind the sweep", Still(-120f)),
                ("beyond the view distance", Still(0f, SecurityCameraSpec.DefaultViewDistance + 0.5f)),
                ("no line of sight", new SecurityCameraTarget(false, 2f, 0f, _where, AwarenessPosture.Standing, 1f, 0f)),
                ("in the dark", new SecurityCameraTarget(true, 2f, 0f, _where, AwarenessPosture.Standing, 0f, 0f)),
            };

            foreach (var (label, target) in cases)
            {
                var camera = new SecurityCameraSweep(SecurityCameraSpec.Default);
                var changes = Record(camera);

                Run(camera, 20f, target);

                Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Sweeping), label);
                Assert.That(camera.Level, Is.Zero, label);
                Assert.That(changes, Is.Empty, label);
                Assert.That(camera.YawDegrees, Is.EqualTo(reference.YawDegrees), label + ": keeps sweeping as if nobody were there");
            }
        }

        [Test]
        public void TheViewDistanceAndTheViewEdgeAreInside()
        {
            var far = new SecurityCameraSweep(SecurityCameraSpec.Default);
            far.Tick(_step, Still(0f, SecurityCameraSpec.DefaultViewDistance));
            Assert.That(far.Phase, Is.EqualTo(SecurityCameraPhase.Tracking));

            var edge = new SecurityCameraSweep(SecurityCameraSpec.Default);
            edge.Tick(0f, Still(SecurityCameraSpec.DefaultFieldOfViewDegrees / 2f));
            Assert.That(edge.Phase, Is.EqualTo(SecurityCameraPhase.Tracking));

            var outside = new SecurityCameraSweep(SecurityCameraSpec.Default);
            outside.Tick(0f, Still((SecurityCameraSpec.DefaultFieldOfViewDegrees / 2f) + 1f));
            Assert.That(outside.Phase, Is.EqualTo(SecurityCameraPhase.Sweeping));
        }

        [Test]
        public void LosingTheTargetDrainsTheMeterAndResumesTheSweep()
        {
            var camera = new SecurityCameraSweep(SecurityCameraSpec.Default);
            var changes = Record(camera);

            Run(camera, 1f, Still(0f));
            Assert.That(camera.Level, Is.EqualTo(0.5f));

            camera.Tick(_step, SecurityCameraTarget.None);
            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Sweeping));
            Assert.That(camera.YawDegrees, Is.EqualTo(5f), "sweeps on from where it tracked");

            Run(camera, 2.75f, SecurityCameraTarget.None);
            Assert.That(camera.Level, Is.EqualTo(0.5f), "the meter holds for DecayDelaySeconds (3 s)");

            Run(camera, 1f, SecurityCameraTarget.None);
            Assert.That(camera.Level, Is.EqualTo(0.4f).Within(1e-5f), "then drains 0.1 per second");

            Run(camera, 10f, SecurityCameraTarget.None);
            Assert.That(camera.Level, Is.Zero);
            Assert.That(changes, Is.EqualTo(new[]
            {
                (SecurityCameraPhase.Sweeping, SecurityCameraPhase.Tracking),
                (SecurityCameraPhase.Tracking, SecurityCameraPhase.Sweeping),
            }));
        }

        [Test]
        public void SeeingTheTargetAgainWhileTheMeterIsStillFullSpotsItAtOnce()
        {
            var camera = new SecurityCameraSweep(SecurityCameraSpec.Default);
            Run(camera, 2f, Still(0f));
            camera.Tick(_step, SecurityCameraTarget.None);
            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Sweeping));
            Assert.That(camera.YawDegrees, Is.EqualTo(5f));

            var changes = Record(camera);
            var moved = new Vector3(4f, 0f, 6f);
            camera.Tick(_step, Running(5f, moved));

            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Spotted));
            Assert.That(camera.SpottedAt, Is.EqualTo(moved));
            Assert.That(changes, Is.EqualTo(new[] { (SecurityCameraPhase.Sweeping, SecurityCameraPhase.Spotted) }));
        }

        [Test]
        public void TheSpottedPlaceFollowsTheTargetWhileItIsSeen()
        {
            var camera = new SecurityCameraSweep(SecurityCameraSpec.Default);
            Run(camera, 1f, Running(0f));
            Assert.That(camera.SpottedAt, Is.EqualTo(_where));

            var next = new Vector3(-3f, 1f, 5f);
            camera.Tick(_step, Running(0f, next));
            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Spotted));
            Assert.That(camera.SpottedAt, Is.EqualTo(next));
        }

        [Test]
        public void ADisabledCameraIgnoresTheTargetForTheGivenSeconds()
        {
            var camera = new SecurityCameraSweep(SecurityCameraSpec.Default);
            Run(camera, 1f, Still(0f));
            var changes = Record(camera);

            Assert.That(camera.Disable(5f), Is.True);
            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Disabled));
            Assert.That(camera.IsActive, Is.False);
            Assert.That(camera.DisabledSecondsLeft, Is.EqualTo(5f));

            Run(camera, 4.75f, Still(0f));
            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Disabled), "seen for 4.75 s while off");
            Assert.That(camera.Level, Is.EqualTo(0.5f), "the meter is frozen");
            Assert.That(camera.DisabledSecondsLeft, Is.EqualTo(0.25f));

            camera.Tick(_step, Still(0f));
            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Tracking), "back on at 5 s");
            Assert.That(camera.DisabledSecondsLeft, Is.Zero);

            Run(camera, 1f, Still(0f));
            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Spotted), "fills from where it was frozen");
            Assert.That(changes, Is.EqualTo(new[]
            {
                (SecurityCameraPhase.Tracking, SecurityCameraPhase.Disabled),
                (SecurityCameraPhase.Disabled, SecurityCameraPhase.Tracking),
                (SecurityCameraPhase.Tracking, SecurityCameraPhase.Spotted),
            }));
        }

        [Test]
        public void ADisabledCameraStopsTurningAndResumesWhereItStopped()
        {
            var camera = new SecurityCameraSweep(SecurityCameraSpec.Default);
            camera.Tick(1f, SecurityCameraTarget.None);
            Assert.That(camera.YawDegrees, Is.EqualTo(20f));

            camera.Disable(3f);
            camera.Tick(2f, SecurityCameraTarget.None);
            Assert.That(camera.YawDegrees, Is.EqualTo(20f));
            Assert.That(camera.DisabledSecondsLeft, Is.EqualTo(1f));

            camera.Tick(1.5f, SecurityCameraTarget.None);
            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Sweeping));
            Assert.That(camera.YawDegrees, Is.EqualTo(30f));
        }

        [Test]
        public void TheTimeLeftAfterComingBackOnCounts()
        {
            var camera = new SecurityCameraSweep(SecurityCameraSpec.Default);
            camera.Disable(5f);
            var changes = Record(camera);

            camera.Tick(6f, Running(0f));

            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Spotted), "5 s off, then 1 s of a running target");
            Assert.That(changes, Is.EqualTo(new[] { (SecurityCameraPhase.Disabled, SecurityCameraPhase.Spotted) }), "one change per tick");
        }

        [Test]
        public void DisablingAgainKeepsTheLongerTime()
        {
            var camera = new SecurityCameraSweep(SecurityCameraSpec.Default);
            var changes = Record(camera);

            camera.Disable(5f);
            camera.Tick(1f, SecurityCameraTarget.None);
            Assert.That(camera.DisabledSecondsLeft, Is.EqualTo(4f));

            Assert.That(camera.Disable(2f), Is.True);
            Assert.That(camera.DisabledSecondsLeft, Is.EqualTo(4f));

            camera.Disable(10f);
            Assert.That(camera.DisabledSecondsLeft, Is.EqualTo(10f));
            Assert.That(changes, Is.EqualTo(new[] { (SecurityCameraPhase.Sweeping, SecurityCameraPhase.Disabled) }));
        }

        [Test]
        public void DisableWithoutSecondsUsesTheConfiguredTime()
        {
            var camera = new SecurityCameraSweep(new SecurityCameraSpec(disabledSeconds: 12f));

            Assert.That(camera.Disable(), Is.True);
            Assert.That(camera.DisabledSecondsLeft, Is.EqualTo(12f));
        }

        [Test]
        public void ADestroyedCameraNeverMovesOrSeesAgain()
        {
            var camera = new SecurityCameraSweep(SecurityCameraSpec.Default);
            Run(camera, 1f, Still(10f));
            var yaw = camera.YawDegrees;
            var changes = Record(camera);

            Assert.That(camera.Destroy(), Is.True);
            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Destroyed));
            Assert.That(camera.Level, Is.Zero);
            Assert.That(camera.IsActive, Is.False);

            Run(camera, 20f, Running(yaw));
            camera.Tick(100f, SecurityCameraTarget.None);

            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Destroyed));
            Assert.That(camera.YawDegrees, Is.EqualTo(yaw));
            Assert.That(camera.Level, Is.Zero);
            Assert.That(camera.Disable(5f), Is.False);
            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Destroyed));
            Assert.That(camera.DisabledSecondsLeft, Is.Zero);
            Assert.That(camera.Destroy(), Is.False);
            Assert.That(changes, Is.EqualTo(new[] { (SecurityCameraPhase.Tracking, SecurityCameraPhase.Destroyed) }));
        }

        [Test]
        public void DestroyingADisabledCameraClearsTheTimer()
        {
            var camera = new SecurityCameraSweep(SecurityCameraSpec.Default);
            camera.Disable(5f);

            camera.Destroy();
            camera.Tick(10f, Running(0f));

            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Destroyed));
            Assert.That(camera.DisabledSecondsLeft, Is.Zero);
        }

        [Test]
        public void TheAngleToTheTargetWrapsAround()
        {
            Assert.That(SecurityCameraSweep.AngleBetween(0f, 0f), Is.Zero);
            Assert.That(SecurityCameraSweep.AngleBetween(350f, -5f), Is.EqualTo(5f));
            Assert.That(SecurityCameraSweep.AngleBetween(-170f, 170f), Is.EqualTo(20f));
            Assert.That(SecurityCameraSweep.AngleBetween(180f, 0f), Is.EqualTo(180f));
            Assert.That(SecurityCameraSweep.AngleBetween(-30f, 15f), Is.EqualTo(45f));
        }

        [Test]
        public void InvalidInputIsRejected()
        {
            var camera = new SecurityCameraSweep(SecurityCameraSpec.Default);

            Assert.Throws<ArgumentNullException>(() => _ = new SecurityCameraSweep(null));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => camera.Tick(-0.1f, SecurityCameraTarget.None)).ParamName, Is.EqualTo("deltaSeconds"));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => camera.Tick(float.NaN, SecurityCameraTarget.None)).ParamName, Is.EqualTo("deltaSeconds"));
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(() => camera.Tick(float.PositiveInfinity, SecurityCameraTarget.None)).ParamName,
                Is.EqualTo("deltaSeconds"));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => camera.Disable(0f)).ParamName, Is.EqualTo("seconds"));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => camera.Disable(float.NaN)).ParamName, Is.EqualTo("seconds"));
            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Sweeping), "a rejected disable leaves the camera on");
        }

        [Test]
        public void TheTargetRejectsInvalidValues()
        {
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => _ = new SecurityCameraTarget(true, -1f, 0f, _where, AwarenessPosture.Standing, 1f, 0f)).ParamName,
                Is.EqualTo("distance"));
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => _ = new SecurityCameraTarget(true, 1f, float.NaN, _where, AwarenessPosture.Standing, 1f, 0f)).ParamName,
                Is.EqualTo("bearingDegrees"));
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => _ = new SecurityCameraTarget(true, 1f, 0f, new Vector3(float.PositiveInfinity, 0f, 0f), AwarenessPosture.Standing, 1f, 0f)).ParamName,
                Is.EqualTo("position"));
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => _ = new SecurityCameraTarget(true, 1f, 0f, _where, (AwarenessPosture)99, 1f, 0f)).ParamName,
                Is.EqualTo("posture"));
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => _ = new SecurityCameraTarget(true, 1f, 0f, _where, AwarenessPosture.Standing, 1.5f, 0f)).ParamName,
                Is.EqualTo("lightFactor"));
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => _ = new SecurityCameraTarget(true, 1f, 0f, _where, AwarenessPosture.Standing, 1f, -1f)).ParamName,
                Is.EqualTo("speed"));
        }
    }
}

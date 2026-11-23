using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class SecurityAlarmStateTests
    {
        private static readonly int[] _expectedOnRadius = { 0, 1, 5, 6 };
        private static readonly int[] _expectedFirst = { 0 };
        private static readonly int[] _expectedFirstTwo = { 0, 1 };

        [Test]
        public void ANewAlarmIsSilent()
        {
            var state = new SecurityAlarmState(SecurityAlarmSpec.Default);

            Assert.That(state.Ringing, Is.False);
            Assert.That(state.SecondsLeft, Is.Zero);
            Assert.That(state.Status, Is.EqualTo(SecurityAlarmStatus.Silent));
            Assert.That(state.Tick(1f), Is.False, "a silent alarm has nothing to stop");
        }

        [Test]
        public void ARaisedAlarmStopsAfterTheSpecSeconds()
        {
            var state = new SecurityAlarmState(new SecurityAlarmSpec(4f, 10f));
            var at = new Vector3(1f, 0f, 2f);

            Assert.That(state.Raise(at, " cam-a "), Is.EqualTo(SecurityAlarmRaise.Started));
            Assert.That(state.Status, Is.EqualTo(new SecurityAlarmStatus(true, 4f, at, "cam-a")));

            Assert.That(state.Tick(1.5f), Is.False);
            Assert.That(state.Ringing, Is.True);
            Assert.That(state.SecondsLeft, Is.EqualTo(2.5f));

            Assert.That(state.Tick(2.5f), Is.True, "it stops exactly when the seconds run out");
            Assert.That(state.Ringing, Is.False);
            Assert.That(state.SecondsLeft, Is.Zero);
            Assert.That(state.SpottedAt, Is.EqualTo(Vector3.Zero));
            Assert.That(state.CameraId, Is.Empty);
            Assert.That(state.Status, Is.EqualTo(SecurityAlarmStatus.Silent));
            Assert.That(state.Tick(1f), Is.False, "stopping is reported once");
        }

        [Test]
        public void ALongTickStopsTheAlarmWithoutGoingNegative()
        {
            var state = new SecurityAlarmState(new SecurityAlarmSpec(4f, 10f));
            state.Raise(Vector3.Zero, "cam-a");

            Assert.That(state.Tick(10f), Is.True);
            Assert.That(state.SecondsLeft, Is.Zero);
        }

        [Test]
        public void RaisingWhileRingingRefillsTheSecondsAndMovesTheSpot()
        {
            var state = new SecurityAlarmState(new SecurityAlarmSpec(4f, 10f));
            var first = new Vector3(1f, 0f, 0f);
            var second = new Vector3(-3f, 0f, 5f);

            state.Raise(first, "cam-a");
            Assert.That(state.Tick(3f), Is.False);
            Assert.That(state.SecondsLeft, Is.EqualTo(1f));

            Assert.That(state.Raise(second, "cam-b"), Is.EqualTo(SecurityAlarmRaise.Extended));
            Assert.That(state.Status, Is.EqualTo(new SecurityAlarmStatus(true, 4f, second, "cam-b")), "refilled, not added on top");

            Assert.That(state.Tick(1f), Is.False, "the first raise alone would have stopped here");
            Assert.That(state.SecondsLeft, Is.EqualTo(3f));
            Assert.That(state.SpottedAt, Is.EqualTo(second));
            Assert.That(state.Tick(3f), Is.True, "it stops the spec seconds after the latest raise");
        }

        [Test]
        public void RaisingAfterItStoppedStartsAgain()
        {
            var state = new SecurityAlarmState(new SecurityAlarmSpec(4f, 10f));
            state.Raise(Vector3.Zero, "cam-a");
            state.Tick(4f);

            Assert.That(state.Raise(Vector3.One, null), Is.EqualTo(SecurityAlarmRaise.Started));
            Assert.That(state.Status, Is.EqualTo(new SecurityAlarmStatus(true, 4f, Vector3.One, string.Empty)));
        }

        [Test]
        public void ResetSilencesTheAlarm()
        {
            var state = new SecurityAlarmState(new SecurityAlarmSpec(4f, 10f));
            state.Raise(Vector3.One, "cam-a");

            state.Reset();

            Assert.That(state.Status, Is.EqualTo(SecurityAlarmStatus.Silent));
            Assert.That(state.Tick(1f), Is.False);
        }

        [Test]
        public void InvalidInputsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new SecurityAlarmState(null));

            var state = new SecurityAlarmState(SecurityAlarmSpec.Default);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Raise(new Vector3(float.NaN, 0f, 0f), "cam-a"));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Raise(new Vector3(0f, float.PositiveInfinity, 0f), "cam-a"));
            Assert.That(state.Ringing, Is.False, "a rejected raise does not ring");
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidTicksAreRejected(float delta)
        {
            var state = new SecurityAlarmState(SecurityAlarmSpec.Default);

            Assert.Throws<ArgumentOutOfRangeException>(() => state.Tick(delta));
        }

        [Test]
        public void CandidatesOnTheRadiusAreChosenAndThoseOutsideAreNot()
        {
            var candidates = new[]
            {
                new Vector3(5f, 0f, 0f),
                new Vector3(3f, 0f, 4f),
                new Vector3(5.001f, 0f, 0f),
                new Vector3(0f, 0f, -6f),
                new Vector3(float.NaN, 0f, 0f),
                new Vector3(1f, 1f, 1f),
                new Vector3(0f, 5f, 0f),
            };
            var selected = new List<int> { 99 };

            var count = SecurityAlarmState.SelectWithin(Vector3.Zero, 5f, candidates, selected);

            Assert.That(count, Is.EqualTo(4));
            Assert.That(selected, Is.EqualTo(_expectedOnRadius), "exactly on the radius counts, height included, and the old contents are cleared");
        }

        [Test]
        public void AZeroRadiusChoosesNobody()
        {
            var selected = new List<int>();

            Assert.That(SecurityAlarmState.SelectWithin(Vector3.One, 0f, new[] { Vector3.One }, selected), Is.Zero);
            Assert.That(selected, Is.Empty);
        }

        [Test]
        public void TheRingingAlarmChoosesAroundTheLatestSpotWithTheSpecRadius()
        {
            var state = new SecurityAlarmState(new SecurityAlarmSpec(4f, 10f));
            var candidates = new[] { new Vector3(10f, 0f, 0f), new Vector3(20f, 0f, 0f), new Vector3(30.5f, 0f, 0f) };
            var selected = new List<int>();

            Assert.That(state.SelectRecipients(candidates, selected), Is.Zero, "a silent alarm calls nobody");

            state.Raise(Vector3.Zero, "cam-a");
            Assert.That(state.SelectRecipients(candidates, selected), Is.EqualTo(1));
            Assert.That(selected, Is.EqualTo(_expectedFirst));

            state.Raise(new Vector3(20f, 0f, 0f), "cam-b");
            Assert.That(state.SelectRecipients(candidates, selected), Is.EqualTo(2));
            Assert.That(selected, Is.EqualTo(_expectedFirstTwo));

            state.Tick(4f);
            Assert.That(state.SelectRecipients(candidates, selected), Is.Zero);
            Assert.That(selected, Is.Empty);
        }

        [Test]
        public void InvalidSelectionsAreRejected()
        {
            var selected = new List<int>();
            var candidates = new[] { Vector3.Zero };

            Assert.Throws<ArgumentOutOfRangeException>(() => SecurityAlarmState.SelectWithin(new Vector3(float.NaN, 0f, 0f), 1f, candidates, selected));
            Assert.Throws<ArgumentOutOfRangeException>(() => SecurityAlarmState.SelectWithin(Vector3.Zero, -1f, candidates, selected));
            Assert.Throws<ArgumentOutOfRangeException>(() => SecurityAlarmState.SelectWithin(Vector3.Zero, float.NaN, candidates, selected));
            Assert.Throws<ArgumentNullException>(() => SecurityAlarmState.SelectWithin(Vector3.Zero, 1f, null, selected));
            Assert.Throws<ArgumentNullException>(() => SecurityAlarmState.SelectWithin(Vector3.Zero, 1f, candidates, null));

            var state = new SecurityAlarmState(SecurityAlarmSpec.Default);
            Assert.Throws<ArgumentNullException>(() => state.SelectRecipients(null, selected));
            Assert.Throws<ArgumentNullException>(() => state.SelectRecipients(candidates, null));
        }

        [Test]
        public void TheLogLinesNameTheCameraAndTheSpot()
        {
            var spec = new SecurityAlarmSpec(20f, 30f);
            var at = new Vector3(1f, 0f, -2.5f);

            Assert.That(
                SecurityAlarmLog.Started("cam-a", at, 20f, 30f),
                Is.EqualTo("[SecurityAlarm] camera=cam-a raised at=(1.00, 0.00, -2.50) seconds=20.0 radius=30.0"));
            Assert.That(
                SecurityAlarmLog.Extended("cam-b", at, 20f),
                Is.EqualTo("[SecurityAlarm] camera=cam-b extended at=(1.00, 0.00, -2.50) seconds=20.0"));
            Assert.That(
                SecurityAlarmLog.Stopped(null, Vector3.Zero),
                Is.EqualTo("[SecurityAlarm] camera=- stopped at=(0.00, 0.00, 0.00)"));
            Assert.That(
                SecurityAlarmLog.Raised(SecurityAlarmRaise.Started, "cam-a", at, spec),
                Is.EqualTo(SecurityAlarmLog.Started("cam-a", at, 20f, 30f)));
            Assert.That(
                SecurityAlarmLog.Raised(SecurityAlarmRaise.Extended, "cam-a", at, spec),
                Is.EqualTo(SecurityAlarmLog.Extended("cam-a", at, 20f)));
            Assert.That(SecurityAlarmLog.Stopped("cam-a", at), Does.StartWith(SecurityAlarmLog.Prefix("cam-a")));
            Assert.Throws<ArgumentNullException>(() => SecurityAlarmLog.Raised(SecurityAlarmRaise.Started, "cam-a", at, null));
        }
    }
}

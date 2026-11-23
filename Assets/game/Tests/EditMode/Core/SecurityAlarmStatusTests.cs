using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class SecurityAlarmStatusTests
    {
        [Test]
        public void TheDefaultIsSilent()
        {
            var status = SecurityAlarmStatus.Silent;

            Assert.That(status.Ringing, Is.False);
            Assert.That(status.SecondsLeft, Is.Zero);
            Assert.That(status.SpottedAt, Is.EqualTo(Vector3.Zero));
            Assert.That(status.CameraId, Is.EqualTo(string.Empty));
            Assert.That(status, Is.EqualTo(default(SecurityAlarmStatus)));
        }

        [Test]
        public void ARingingAlarmKeepsItsValues()
        {
            var status = new SecurityAlarmStatus(true, 12.5f, new Vector3(1f, 2f, 3f), " cam-lobby ");

            Assert.That(status.Ringing, Is.True);
            Assert.That(status.SecondsLeft, Is.EqualTo(12.5f));
            Assert.That(status.SpottedAt, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(status.CameraId, Is.EqualTo("cam-lobby"), "the Id is trimmed");
            Assert.That(new SecurityAlarmStatus(true, 1f, Vector3.Zero, null).CameraId, Is.EqualTo(string.Empty));
        }

        [Test]
        public void ASilentAlarmDropsTheMeaninglessValues()
        {
            var status = new SecurityAlarmStatus(false, 5f, new Vector3(1f, 2f, 3f), "cam-lobby");

            Assert.That(status, Is.EqualTo(SecurityAlarmStatus.Silent));
            Assert.That(status.SecondsLeft, Is.Zero);
            Assert.That(status.SpottedAt, Is.EqualTo(Vector3.Zero));
            Assert.That(status.CameraId, Is.EqualTo(string.Empty));
        }

        [Test]
        public void EqualityComparesEveryValue()
        {
            var status = new SecurityAlarmStatus(true, 3f, new Vector3(1f, 0f, 0f), "a");

            Assert.That(status == new SecurityAlarmStatus(true, 3f, new Vector3(1f, 0f, 0f), "a"), Is.True);
            Assert.That(status.GetHashCode(), Is.EqualTo(new SecurityAlarmStatus(true, 3f, new Vector3(1f, 0f, 0f), "a").GetHashCode()));
            Assert.That(status != new SecurityAlarmStatus(true, 2f, new Vector3(1f, 0f, 0f), "a"), Is.True);
            Assert.That(status != new SecurityAlarmStatus(true, 3f, new Vector3(0f, 0f, 1f), "a"), Is.True);
            Assert.That(status != new SecurityAlarmStatus(true, 3f, new Vector3(1f, 0f, 0f), "b"), Is.True);
            Assert.That(status != SecurityAlarmStatus.Silent, Is.True);
            Assert.That(status.Equals((object)status), Is.True);
            Assert.That(status.Equals("a"), Is.False);
        }

        [Test]
        public void InvalidValuesAreRejected()
        {
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(() => _ = new SecurityAlarmStatus(true, -1f, Vector3.Zero, "a")).ParamName,
                Is.EqualTo("secondsLeft"));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new SecurityAlarmStatus(true, float.NaN, Vector3.Zero, "a"));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new SecurityAlarmStatus(true, float.PositiveInfinity, Vector3.Zero, "a"));
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(() => _ = new SecurityAlarmStatus(true, 1f, new Vector3(float.NaN, 0f, 0f), "a")).ParamName,
                Is.EqualTo("spottedAt"));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new SecurityAlarmStatus(false, 0f, new Vector3(0f, float.PositiveInfinity, 0f), "a"));
        }
    }
}

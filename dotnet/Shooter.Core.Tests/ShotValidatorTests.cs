using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class ShotValidatorTests
    {
        // float は 2^24 より上の奇数も小数部も表せないので、そこを越えた奇数の tick で確かめる。
        private const int _lateTick = (1 << 24) + 1;

        private static readonly ShotTime _valid = new ShotTime(100, 0.5f);

        [Test]
        public void AcceptsShotNearTheShooter()
        {
            var result = ShotValidator.Check(new Vector3(0, 0.6f, 0), Vector3.UnitZ, _valid, Vector3.Zero, 3f);

            Assert.That(result, Is.EqualTo(ShotRejection.None));
        }

        [Test]
        public void AcceptsOriginExactlyAtTheLimit()
        {
            var result = ShotValidator.Check(new Vector3(3f, 0, 0), Vector3.UnitZ, _valid, Vector3.Zero, 3f);

            Assert.That(result, Is.EqualTo(ShotRejection.None));
        }

        [Test]
        public void RejectsOriginFarFromTheShooter()
        {
            var result = ShotValidator.Check(new Vector3(0, 0, 3.01f), Vector3.UnitZ, _valid, Vector3.Zero, 3f);

            Assert.That(result, Is.EqualTo(ShotRejection.OriginTooFar));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsNonFiniteOrigin(float value)
        {
            var result = ShotValidator.Check(new Vector3(value, 0, 0), Vector3.UnitZ, _valid, Vector3.Zero, 3f);

            Assert.That(result, Is.EqualTo(ShotRejection.InvalidOrigin));
        }

        [Test]
        public void RejectsZeroOrNonFiniteDirection()
        {
            Assert.That(ShotValidator.Check(Vector3.Zero, Vector3.Zero, _valid, Vector3.Zero, 3f), Is.EqualTo(ShotRejection.InvalidDirection));
            Assert.That(
                ShotValidator.Check(Vector3.Zero, new Vector3(float.NaN, 0, 1), _valid, Vector3.Zero, 3f),
                Is.EqualTo(ShotRejection.InvalidDirection));
            Assert.That(
                ShotValidator.Check(Vector3.Zero, new Vector3(0, float.NegativeInfinity, 0), _valid, Vector3.Zero, 3f),
                Is.EqualTo(ShotRejection.InvalidDirection));
        }

        [Test]
        public void AcceptsDirectionOfAnyNonZeroLength()
        {
            Assert.That(ShotValidator.Check(Vector3.Zero, new Vector3(0, 0, 50), _valid, Vector3.Zero, 3f), Is.EqualTo(ShotRejection.None));
            Assert.That(ShotValidator.Check(Vector3.Zero, new Vector3(0, 0, 0.01f), _valid, Vector3.Zero, 3f), Is.EqualTo(ShotRejection.None));
        }

        [Test]
        public void RejectsInvalidTime()
        {
            Assert.That(
                ShotValidator.Check(Vector3.Zero, Vector3.UnitZ, new ShotTime(-1, 0f), Vector3.Zero, 3f),
                Is.EqualTo(ShotRejection.InvalidTime));
            Assert.That(
                ShotValidator.Check(Vector3.Zero, Vector3.UnitZ, new ShotTime(5, 1.5f), Vector3.Zero, 3f),
                Is.EqualTo(ShotRejection.InvalidTime));
            Assert.That(
                ShotValidator.Check(Vector3.Zero, Vector3.UnitZ, new ShotTime(5, float.NaN), Vector3.Zero, 3f),
                Is.EqualTo(ShotRejection.InvalidTime));
        }

        [Test]
        public void RejectsInvalidLimit()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ShotValidator.Check(Vector3.Zero, Vector3.UnitZ, _valid, Vector3.Zero, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => ShotValidator.Check(Vector3.Zero, Vector3.UnitZ, _valid, Vector3.Zero, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => ShotValidator.Check(Vector3.Zero, Vector3.UnitZ, _valid, Vector3.Zero, float.PositiveInfinity));
        }

        [Test]
        public void AShotTimeKeepsItsFractionPastTheFloatRange()
        {
            Assert.That(new ShotTime(_lateTick, 0.25f).ToTick(), Is.EqualTo(16777217.25d));

            var time = ShotTime.FromTick(16777217.25d);

            Assert.That(time.Tick, Is.EqualTo(16777217));
            Assert.That(time.Alpha, Is.EqualTo(0.25f));
            Assert.That(time.IsValid, Is.True);
        }

        [Test]
        public void AShotTimeFromATickJustBelowTheNextStaysValid()
        {
            var time = ShotTime.FromTick(6d - 1e-12d);

            Assert.That(time.Tick, Is.EqualTo(5));
            Assert.That(time.Alpha, Is.EqualTo(0.99999994f));
            Assert.That(time.IsValid, Is.True);
            Assert.That(ShotTime.FromTick(0d).Tick, Is.EqualTo(0));
            Assert.That(ShotTime.FromTick(0d).Alpha, Is.EqualTo(0f));
        }
    }
}

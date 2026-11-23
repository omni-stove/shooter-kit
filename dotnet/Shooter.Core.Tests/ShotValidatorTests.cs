using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class ShotValidatorTests
    {
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

        [TestCase(1e20f, 0f, 0f)]
        [TestCase(0f, -1e20f, 0f)]
        [TestCase(1.5e19f, 1.5e19f, 1.5e19f)]
        [TestCase(float.MaxValue, float.MaxValue, float.MaxValue)]
        public void RejectsDirectionWhoseSquaredLengthOverflows(float x, float y, float z)
        {
            var direction = new Vector3(x, y, z);

            Assert.That(float.IsPositiveInfinity(direction.LengthSquared()), Is.True, "the case must overflow to be a regression test");
            Assert.That(ShotValidator.Check(Vector3.Zero, direction, _valid, Vector3.Zero, 3f), Is.EqualTo(ShotRejection.InvalidDirection));
            Assert.That(ShotValidator.TryNormalizeDirection(direction, 1e-12f, out var unit), Is.False);
            Assert.That(unit, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void AcceptsTheLongestDirectionWhoseSquaredLengthIsFinite()
        {
            var direction = new Vector3(0f, 0f, 1e19f);

            Assert.That(ShotValidator.Check(Vector3.Zero, direction, _valid, Vector3.Zero, 3f), Is.EqualTo(ShotRejection.None));
            Assert.That(ShotValidator.TryNormalizeDirection(direction, 1e-12f, out var unit), Is.True);
            Assert.That(unit.X, Is.EqualTo(0f));
            Assert.That(unit.Y, Is.EqualTo(0f));
            Assert.That(unit.Z, Is.EqualTo(1f).Within(1e-6f));
        }

        [Test]
        public void NormalizesAnAcceptedDirectionToAFiniteUnitVector()
        {
            Assert.That(ShotValidator.TryNormalizeDirection(new Vector3(3f, 0f, 4f), 1e-12f, out var unit), Is.True);
            Assert.That(unit.X, Is.EqualTo(0.6f).Within(1e-6f));
            Assert.That(unit.Y, Is.EqualTo(0f));
            Assert.That(unit.Z, Is.EqualTo(0.8f).Within(1e-6f));

            Assert.That(ShotValidator.TryNormalizeDirection(new Vector3(0f, -2e-6f, 0f), 1e-12f, out var tiny), Is.True);
            Assert.That(tiny.Y, Is.EqualTo(-1f).Within(1e-6f));
            Assert.That(tiny.Length(), Is.EqualTo(1f).Within(1e-6f));
        }

        [Test]
        public void TryNormalizeDirectionRejectsZeroShortAndNonFiniteInput()
        {
            Assert.That(ShotValidator.TryNormalizeDirection(Vector3.Zero, 1e-12f, out _), Is.False);
            Assert.That(ShotValidator.TryNormalizeDirection(new Vector3(0f, 0f, 1e-5f), 1e-8f, out _), Is.False);
            Assert.That(ShotValidator.TryNormalizeDirection(new Vector3(0f, 0f, 1e-3f), 1e-8f, out _), Is.True);
            Assert.That(ShotValidator.TryNormalizeDirection(new Vector3(float.NaN, 0f, 1f), 1e-12f, out _), Is.False);
            Assert.That(ShotValidator.TryNormalizeDirection(new Vector3(0f, float.PositiveInfinity, 0f), 1e-12f, out _), Is.False);
            Assert.That(ShotValidator.TryNormalizeDirection(Vector3.Zero, 0f, out _), Is.False, "a zero limit must not let the zero vector through");
            Assert.That(ShotValidator.TryNormalizeDirection(Vector3.UnitZ, float.NaN, out _), Is.False);
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
    }
}

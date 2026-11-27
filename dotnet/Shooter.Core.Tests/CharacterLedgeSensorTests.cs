using System;
using System.Collections.Generic;
using System.Numerics;
using GameKit.World;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class CharacterLedgeSensorTests
    {
        private const float _grabReach = 0.6f;
        private const float _tolerance = 1e-4f;

        // 中心の高さが 0 の体にして、手の高さ (立った形の上端) を位置の 1 m 上にする
        private static readonly StanceGeometry _stance = new StanceGeometry(2f, 0f, 0.6f, 1.2f, 0.5f);
        private static readonly Vector3 _wallNormal = new Vector3(0f, 0f, -1f);

        private readonly List<WorldCollider> _colliders = new List<WorldCollider>();

        [SetUp]
        public void SetUp()
        {
            _colliders.Clear();
        }

        [Test]
        public void TheSensorMeasuresItsReachFromTheBody()
        {
            var sensor = new CharacterLedgeSensor(NewBody());

            Assert.That(sensor.HandHeight, Is.EqualTo(1f));
            Assert.That(sensor.Reach, Is.EqualTo(0.8f).Within(1e-6f));
            Assert.That(sensor.SideReach, Is.EqualTo(0.55f).Within(1e-6f));
            AssertVector(CharacterLedgeSensor.RightAlong(_wallNormal), new Vector3(1f, 0f, 0f));
        }

        [Test]
        public void TheSensorFindsTheLedgeInFrontOfTheHands()
        {
            AddLedge();
            var sensor = new CharacterLedgeSensor(NewBody());

            var ledge = sensor.Probe(new Vector3(0f, 2f, 0.4f), 0f, _grabReach);

            Assert.That(ledge.IsFound, Is.True);
            AssertVector(ledge.Point, new Vector3(0f, 2.9f, 1f));
            AssertVector(ledge.Normal, _wallNormal);
            Assert.That(ledge.ContinuesLeft, Is.True);
            Assert.That(ledge.ContinuesRight, Is.True);
        }

        [Test]
        public void TheSensorSeesWhereTheLedgeEndsOnTheRight()
        {
            AddLedge();
            var sensor = new CharacterLedgeSensor(NewBody());

            var ledge = sensor.Probe(new Vector3(0.6f, 2f, 0.4f), 0f, _grabReach);

            Assert.That(ledge.IsFound, Is.True);
            AssertVector(ledge.Point, new Vector3(0.6f, 2.9f, 1f));
            Assert.That(ledge.ContinuesRight, Is.False, "0.6 + 0.55 is past the end at 1");
            Assert.That(ledge.ContinuesLeft, Is.True);
        }

        [Test]
        public void TheSensorSeesWhereTheLedgeEndsOnTheLeft()
        {
            AddLedge();
            var sensor = new CharacterLedgeSensor(NewBody());

            var ledge = sensor.Probe(new Vector3(-0.6f, 2f, 0.4f), 0f, _grabReach);

            Assert.That(ledge.IsFound, Is.True);
            AssertVector(ledge.Point, new Vector3(-0.6f, 2.9f, 1f));
            Assert.That(ledge.ContinuesLeft, Is.False);
            Assert.That(ledge.ContinuesRight, Is.True);
        }

        [Test]
        public void TheSensorFindsALedgeAboveTheHandsWithinReach()
        {
            AddLedge();
            var sensor = new CharacterLedgeSensor(NewBody());

            var ledge = sensor.Probe(new Vector3(0f, 1.4f, 0.4f), 0f, _grabReach);

            Assert.That(ledge.IsFound, Is.True, "the hands at 2.4 reach up to 3.0");
            AssertVector(ledge.Point, new Vector3(0f, 2.9f, 1f));
        }

        [Test]
        public void TheSensorIgnoresALedgeOutOfReach()
        {
            AddLedge();
            var sensor = new CharacterLedgeSensor(NewBody());

            Assert.That(sensor.Probe(new Vector3(0f, 2.6f, 0.4f), 0f, _grabReach).IsFound, Is.False, "the hands at 3.6 reach down only to 3.0");
            Assert.That(sensor.Probe(new Vector3(0f, 1.2f, 0.4f), 0f, _grabReach).IsFound, Is.False, "the hands at 2.2 reach up only to 2.8");
            Assert.That(sensor.Probe(new Vector3(0f, 2f, 0.1f), 0f, _grabReach).IsFound, Is.False, "0.9 m away is past 0.5 + 0.3");
            Assert.That(sensor.Probe(new Vector3(0f, 2f, 0.4f), MathF.PI, _grabReach).IsFound, Is.False, "facing away");
        }

        [Test]
        public void TheSensorIgnoresATopTooSteepToHold()
        {
            const float angle = 0.8f;
            _colliders.Add(WorldCollider.Box(
                _colliders.Count + 1,
                0,
                WorldCollider.StaticGroup,
                new Vector3(0f, 2.9f, 1.5f),
                new Vector3(1f, 0.5f, 0.5f),
                Quaternion.CreateFromAxisAngle(Vector3.UnitX, angle)));
            var sensor = new CharacterLedgeSensor(NewBody());

            Assert.That(sensor.Probe(new Vector3(0f, 2f, 0.4f), 0f, _grabReach).IsFound, Is.False, "a face tilted about 46 degrees is neither a wall nor a top");
        }

        [Test]
        public void TheSensorFollowsTheLedgeItHolds()
        {
            AddLedge();
            var sensor = new CharacterLedgeSensor(NewBody());

            var ledge = sensor.Follow(new Vector3(0.3f, 2.9f, 1f), _wallNormal);

            Assert.That(ledge.IsFound, Is.True);
            AssertVector(ledge.Point, new Vector3(0.3f, 2.9f, 1f));
            AssertVector(ledge.Normal, _wallNormal);
            Assert.That(ledge.ContinuesLeft, Is.True);
            Assert.That(ledge.ContinuesRight, Is.True);
        }

        [Test]
        public void TheSensorLosesTheLedgePastItsEnd()
        {
            AddLedge();
            var sensor = new CharacterLedgeSensor(NewBody());

            Assert.That(sensor.Follow(new Vector3(1.2f, 2.9f, 1f), _wallNormal).IsFound, Is.False);
            Assert.That(sensor.Follow(new Vector3(0f, 2.9f, 1f), Vector3.UnitY).IsFound, Is.False, "no horizontal normal");
        }

        [Test]
        public void TheSensorLosesTheLedgeWhenItsTopMovesMoreThanAStep()
        {
            AddLedge();
            var sensor = new CharacterLedgeSensor(NewBody());

            Assert.That(sensor.Follow(new Vector3(0f, 2.75f, 1f), _wallNormal).IsFound, Is.False, "the top is 0.15 above, past the 0.1 step");
            Assert.That(sensor.Follow(new Vector3(0f, 2.85f, 1f), _wallNormal).IsFound, Is.True, "the top is 0.05 above");
        }

        [Test]
        public void TheClimbOntoAnOpenLedgeIsClear()
        {
            AddLedge();
            var sensor = new CharacterLedgeSensor(NewBody());
            var hang = LedgeHangStep.HangPosition(new Vector3(0f, 2.9f, 1f), _wallNormal, _stance);
            var path = LedgeHangStep.ClimbPath(_wallNormal, _stance, new LedgeHangTuning(true));

            Assert.That(sensor.IsClimbClear(hang, path), Is.True);
        }

        [Test]
        public void TheClimbUnderACeilingIsBlocked()
        {
            AddLedge();
            AddBox(new Vector3(0f, 4.5f, 1.5f), new Vector3(2f, 0.25f, 2f));
            var sensor = new CharacterLedgeSensor(NewBody());
            var hang = LedgeHangStep.HangPosition(new Vector3(0f, 2.9f, 1f), _wallNormal, _stance);
            var path = LedgeHangStep.ClimbPath(_wallNormal, _stance, new LedgeHangTuning(true));

            Assert.That(sensor.IsClimbClear(hang, path), Is.False);
        }

        [Test]
        public void TheSensorRejectsAnInvalidReach()
        {
            var sensor = new CharacterLedgeSensor(NewBody());

            Assert.Throws<ArgumentOutOfRangeException>(() => sensor.Probe(Vector3.Zero, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => sensor.Probe(Vector3.Zero, 0f, float.PositiveInfinity));
            Assert.Throws<ArgumentNullException>(() => sensor.IsClimbClear(Vector3.Zero, null));
            Assert.Throws<ArgumentNullException>(() => new CharacterLedgeSensor(null));
        }

        private static void AssertVector(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(_tolerance));
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(_tolerance));
            Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(_tolerance));
        }

        private CharacterBody NewBody()
        {
            return new CharacterBody(new BvhWorldCollision(new WorldMap(_colliders)), CapsuleMoverSpec.Default, _stance);
        }

        // 上端が高さ 2.9、手前の面が z = 1、幅が x = -1 から 1 までの台
        private void AddLedge()
        {
            AddBox(new Vector3(0f, 1.45f, 1.5f), new Vector3(1f, 1.45f, 0.5f));
        }

        private void AddBox(Vector3 center, Vector3 halfExtents)
        {
            _colliders.Add(WorldCollider.Box(_colliders.Count + 1, 0, WorldCollider.StaticGroup, center, halfExtents, Quaternion.Identity));
        }
    }
}

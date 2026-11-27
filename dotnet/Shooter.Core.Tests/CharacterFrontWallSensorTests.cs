using System;
using System.Collections.Generic;
using System.Numerics;
using GameKit.World;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class CharacterFrontWallSensorTests
    {
        // 中心の高さが 0 の体にして、壁を調べる高さを本体の位置と同じに、足元を 1 m 下にする
        private static readonly StanceGeometry _stance = new StanceGeometry(2f, 0f, 0.6f, 1.2f, 0.5f);
        private static readonly Vector3 _standing = new Vector3(0f, 1f, 0f);
        private static readonly Vector3 _facingNormal = new Vector3(0f, 0f, -1f);
        private static readonly WallClimbTuning _tuning = new WallClimbTuning(true);

        private readonly List<WorldCollider> _colliders = new List<WorldCollider>();

        [SetUp]
        public void SetUp()
        {
            _colliders.Clear();
        }

        [Test]
        public void FindsTheWallAheadWithItsNormalAndTop()
        {
            AddFrontWall(4f);
            var sensor = new CharacterFrontWallSensor(NewBody());

            var contact = sensor.Probe(_standing, 0f, MotorSpec.Default, _tuning);

            Assert.That(contact.IsFound, Is.True);
            AssertClose(contact.Normal, _facingNormal);
            Assert.That(contact.Distance, Is.EqualTo(0.7f).Within(1e-4f));
            Assert.That(contact.HasTop, Is.True);
            Assert.That(contact.TopHeight, Is.EqualTo(4f).Within(1e-4f), "the top is 4 m above the feet");
            AssertClose(contact.Ledge, new Vector3(0f, 4f, 0.7f));
            Assert.That(sensor.Reach, Is.EqualTo(0.8f).Within(1e-6f));
        }

        [Test]
        public void MeasuresALowWallTop()
        {
            AddFrontWall(1.5f);
            var contact = new CharacterFrontWallSensor(NewBody()).Probe(_standing, 0f, MotorSpec.Default, _tuning);

            Assert.That(contact.IsFound, Is.True);
            Assert.That(contact.TopHeight, Is.EqualTo(1.5f).Within(1e-4f));
            Assert.That(WallClimbStep.CanReachLedge(contact, MotorSpec.Default), Is.True, "low enough to mantle");
        }

        [Test]
        public void HasNoTopWhenTheWallIsTallerThanTheClimbCanReach()
        {
            AddFrontWall(8f);
            var contact = new CharacterFrontWallSensor(NewBody()).Probe(_standing, 0f, MotorSpec.Default, _tuning);

            Assert.That(contact.IsFound, Is.True);
            AssertClose(contact.Normal, _facingNormal);
            Assert.That(contact.HasTop, Is.False);
            Assert.That(contact.TopHeight, Is.EqualTo(float.PositiveInfinity));
            Assert.That(contact.Ledge, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void MeasuresTheTopOnlyAsHighAsTheClimbReaches()
        {
            AddFrontWall(4f);
            var shortClimb = new WallClimbTuning(true, maxHeight: 1f);

            var contact = new CharacterFrontWallSensor(NewBody()).Probe(_standing, 0f, MotorSpec.Default, shortClimb);

            Assert.That(contact.IsFound, Is.True);
            Assert.That(contact.HasTop, Is.False, "2.2 m mantle + 1 m climb + 0.3 m margin stays below the 4 m top");
        }

        [Test]
        public void FindsNothingWithoutAWallAhead()
        {
            AddBox(new Vector3(0f, -1f, 0f), new Vector3(10f, 1f, 10f));
            var open = new CharacterFrontWallSensor(NewBody());

            Assert.That(open.Probe(_standing, 0f, MotorSpec.Default, _tuning).IsFound, Is.False, "only the floor");

            AddFrontWall(4f);
            var sensor = new CharacterFrontWallSensor(NewBody());
            Assert.That(sensor.Probe(_standing, MathF.PI, MotorSpec.Default, _tuning).IsFound, Is.False, "facing away");
            Assert.That(sensor.Probe(_standing, MathF.PI * 0.5f, MotorSpec.Default, _tuning).IsFound, Is.False, "facing along the wall");
            Assert.That(
                sensor.Probe(new Vector3(0f, 1f, -0.2f), 0f, MotorSpec.Default, _tuning).IsFound, Is.False, "0.9 m away is past 0.5 + 0.3");
            Assert.That(sensor.Probe(new Vector3(0f, 1f, -0.2f), 0f, MotorSpec.Default, _tuning), Is.EqualTo(FrontWallContact.None));
        }

        [Test]
        public void TakesAWallOnlyWhenFacingItClosely()
        {
            AddFrontWall(4f);
            var sensor = new CharacterFrontWallSensor(NewBody());
            var close = new Vector3(0f, 1f, 0.4f);

            var head = sensor.Probe(close, MathF.PI / 6f, MotorSpec.Default, _tuning);
            var glancing = sensor.Probe(close, MathF.PI / 3f, MotorSpec.Default, _tuning);

            Assert.That(head.IsFound, Is.True, "30 degrees off the wall's normal");
            AssertClose(head.Normal, _facingNormal);
            Assert.That(glancing.IsFound, Is.False, "60 degrees off the wall's normal is a glance along it");
        }

        [Test]
        public void IgnoresASlantedFace()
        {
            const float angle = 0.5f;
            var center = new Vector3(0f, 1f - (0.25f * MathF.Sin(angle)), 0.6f + (0.25f * MathF.Cos(angle)));
            _colliders.Add(WorldCollider.Box(
                _colliders.Count + 1, 0, WorldCollider.StaticGroup, center, new Vector3(5f, 3f, 0.25f), Quaternion.CreateFromAxisAngle(Vector3.UnitX, angle)));
            var sensor = new CharacterFrontWallSensor(NewBody());

            Assert.That(sensor.Probe(_standing, 0f, MotorSpec.Default, _tuning).IsFound, Is.False, "a face tilted about 29 degrees is not a wall");
        }

        [Test]
        public void RejectsMissingArguments()
        {
            var sensor = new CharacterFrontWallSensor(NewBody());

            Assert.Throws<ArgumentNullException>(() => new CharacterFrontWallSensor(null));
            Assert.Throws<ArgumentNullException>(() => sensor.Probe(_standing, 0f, null, _tuning));
            Assert.Throws<ArgumentNullException>(() => sensor.Probe(_standing, 0f, MotorSpec.Default, null));
        }

        [Test]
        public void TheContactDropsValuesWithoutAWall()
        {
            var none = new FrontWallContact(Vector3.Zero, 0.6f, 3f, new Vector3(1f, 2f, 3f));
            var noTop = new FrontWallContact(_facingNormal, 0.6f, float.PositiveInfinity, new Vector3(1f, 2f, 3f));

            Assert.That(none.IsFound, Is.False);
            Assert.That(none.HasTop, Is.False);
            Assert.That(none.Distance, Is.Zero);
            Assert.That(none.TopHeight, Is.Zero);
            Assert.That(none.Ledge, Is.EqualTo(Vector3.Zero));
            Assert.That(noTop.Ledge, Is.EqualTo(Vector3.Zero));
            Assert.That(FrontWallContact.None.HasTop, Is.False);
        }

        private static void AssertClose(Vector3 actual, Vector3 expected)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(1e-4f), $"{actual} should be {expected}");
        }

        private CharacterBody NewBody()
        {
            return new CharacterBody(new BvhWorldCollision(new WorldMap(_colliders)), CapsuleMoverSpec.Default, _stance);
        }

        // 手前の面を z = 0.7、下端を足元 (y = 0) にそろえた壁を置く
        private void AddFrontWall(float top)
        {
            AddBox(new Vector3(0f, top * 0.5f, 0.95f), new Vector3(5f, top * 0.5f, 0.25f));
        }

        private void AddBox(Vector3 center, Vector3 halfExtents)
        {
            _colliders.Add(WorldCollider.Box(_colliders.Count + 1, 0, WorldCollider.StaticGroup, center, halfExtents, Quaternion.Identity));
        }
    }
}

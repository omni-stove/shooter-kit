using System;
using System.Linq;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Editor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class ProjectileLookAssetBuilderTests
    {
        private static readonly ProjectileLookKind[] _kinds =
        {
            ProjectileLookKind.Arrow,
            ProjectileLookKind.Grenade,
            ProjectileLookKind.Rocket,
            ProjectileLookKind.Slug,
        };

        private GameObject _root;
        private Material _material;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Look");
            _material = new Material(Shader.Find("Hidden/InternalErrorShader"));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_material);
        }

        [TestCase("Crossbow", ProjectileLookKind.Arrow)]
        [TestCase("GrenadeLauncher", ProjectileLookKind.Grenade)]
        [TestCase("RocketLauncher", ProjectileLookKind.Rocket)]
        [TestCase("GuidedLauncher", ProjectileLookKind.Rocket)]
        [TestCase("GrenadeLauncher40mm", ProjectileLookKind.Grenade)]
        [TestCase("BreakActionGrenadeLauncher", ProjectileLookKind.Grenade)]
        [TestCase("QuadRocketLauncher", ProjectileLookKind.Rocket)]
        [TestCase("RocketPropelledGrenade", ProjectileLookKind.Rocket)]
        [TestCase("ServiceRifle", ProjectileLookKind.None)]
        [TestCase("FiftyCalSniper", ProjectileLookKind.None)]
        public void SlowSampleRoundsGetALookAndFastOnesKeepTheRod(string weaponName, ProjectileLookKind expected)
        {
            var spec = WeaponAssetBuilder.AllWeaponSources().Single(source => source.Name == weaponName).Spec;

            Assert.That(ProjectileLookAssetBuilder.KindFor(spec), Is.EqualTo(expected));
        }

        [Test]
        public void RoundsThatDoNotFlyHaveNoLook()
        {
            var flying = 0;
            foreach (var (_, spec) in WeaponAssetBuilder.AllWeaponSources())
            {
                if (spec.Round.Delivery != RoundDelivery.Projectile)
                {
                    Assert.That(ProjectileLookAssetBuilder.KindFor(spec), Is.EqualTo(ProjectileLookKind.None), spec.Id);
                }
                else if (spec.Round.Projectile.Speed <= ProjectileLookAssetBuilder.MaxTrailSpeed)
                {
                    flying++;
                }
            }

            Assert.That(flying, Is.EqualTo(12));
        }

        [Test]
        public void KindForRejectsNoSpec()
        {
            Assert.Throws<ArgumentNullException>(() => ProjectileLookAssetBuilder.KindFor(null));
        }

        [Test]
        public void EveryLookSitsBehindTheTipAndHasItsOwnPrefab()
        {
            var names = _kinds.Select(ProjectileLookAssetBuilder.PrefabName).ToArray();
            Assert.That(names, Is.Unique);
            Assert.That(names, Has.All.StartsWith(ProjectileLookAssetBuilder.PrefabPrefix));

            foreach (var kind in _kinds)
            {
                var trail = ProjectileLookAssetBuilder.TrailFor(kind);
                Assert.That(trail.TailZ, Is.LessThan(0f), kind.ToString());
                Assert.That(trail.Seconds, Is.GreaterThan(0f), kind.ToString());
                Assert.That(trail.StartColor.a, Is.GreaterThan(trail.EndColor.a), kind.ToString());
                Assert.That(ProjectileLookAssetBuilder.PartsFor(kind), Is.Not.Empty, kind.ToString());
                Assert.That(ProjectileLookAssetBuilder.PartsFor(kind).Select(part => part.Center.z), Has.All.LessThan(0f), kind.ToString());
            }
        }

        [Test]
        public void NoneHasNoPrefabTrailOrParts()
        {
            Assert.Throws<ArgumentException>(() => ProjectileLookAssetBuilder.PrefabName(ProjectileLookKind.None));
            Assert.Throws<ArgumentException>(() => ProjectileLookAssetBuilder.TrailFor(ProjectileLookKind.None));
            Assert.Throws<ArgumentException>(() => ProjectileLookAssetBuilder.PartsFor(ProjectileLookKind.None));
        }

        [Test]
        public void BuildLookPutsTheBodyAndATrailAtTheTail()
        {
            ProjectileLookAssetBuilder.BuildLook(_root, ProjectileLookKind.Arrow, (_, _) => _material, _material);

            var body = _root.transform.Find(ProjectileLookAssetBuilder.BodyName);
            Assert.That(body, Is.Not.Null);
            Assert.That(body.GetComponentsInChildren<MeshRenderer>().Length, Is.EqualTo(4));
            Assert.That(_root.GetComponentsInChildren<Collider>(), Is.Empty);

            var tail = _root.transform.Find(ProjectileLookAssetBuilder.TrailName);
            Assert.That(tail, Is.Not.Null);
            Assert.That(tail.localPosition, Is.EqualTo(new Vector3(0f, 0f, -0.62f)));
            var trail = tail.GetComponent<TrailRenderer>();
            Assert.That(trail, Is.Not.Null);
            Assert.That(trail.time, Is.EqualTo(0.35f).Within(1e-5f));
            Assert.That(trail.startWidth, Is.EqualTo(0.025f).Within(1e-5f));
            Assert.That(trail.endWidth, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(trail.emitting, Is.True);
            Assert.That(trail.autodestruct, Is.False);
            Assert.That(trail.sharedMaterial, Is.SameAs(_material));
        }

        [Test]
        public void BuildLookRejectsNoRootOrPaint()
        {
            Assert.Throws<ArgumentNullException>(() => ProjectileLookAssetBuilder.BuildLook(null, ProjectileLookKind.Arrow, (_, _) => _material, _material));
            Assert.Throws<ArgumentNullException>(() => ProjectileLookAssetBuilder.BuildLook(_root, ProjectileLookKind.Arrow, null, _material));
        }
    }
}

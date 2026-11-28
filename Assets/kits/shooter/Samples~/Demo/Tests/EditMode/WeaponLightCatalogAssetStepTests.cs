using System;
using System.Linq;
using GameKit.Core;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using UnityEditor;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    public sealed class WeaponLightCatalogAssetStepTests
    {
        private const string WideLightId = "0d3c5e1a-7b29-4f86-a1c4-5e9b2d7f3a10";
        private const string NarrowLightId = "6a1f4c8e-2d57-4b93-8e0a-3c6d9f1b4e27";
        private const string SameVisualLightId = "b4e7a2c9-1f36-4d58-9a0b-7c2e5f8d1a63";
        private const string PlainLightId = "e2a9c6f1-4b83-4d7e-b05a-1f8c3d6e9b42";

        private string _folder;
        private string _path;
        private string _assetFolder;

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/__WeaponLightCatalogAssetStepTests_" + Guid.NewGuid().ToString("N");
            _path = _folder + "/Resources/" + WeaponLightCatalog.ResourcePath + ".asset";
            _assetFolder = _folder + "/WeaponLight";
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(_folder))
            {
                AssetDatabase.DeleteAsset(_folder);
            }
        }

        [Test]
        public void TheBatchEntryRunsTheStepAfterThePresentationCatalog()
        {
            var steps = PresentationAssetSteps.Discover();

            var index = steps.ToList().FindIndex(step => step is WeaponLightCatalogAssetStep);
            Assert.That(index, Is.GreaterThan(0));
            Assert.That(steps[index].Name, Is.EqualTo("WeaponLightCatalog"));
            Assert.That(steps[index].Order, Is.GreaterThan(PresentationCatalogAssetStep.StepOrder));
        }

        [Test]
        public void TheAssetSitsWhereTheRuntimeLoadsIt()
        {
            Assert.That(
                WeaponLightCatalogAssetStep.AssetPath,
                Does.EndWith("/Presentation/Resources/Presentation/WeaponLightCatalog.asset"));
            Assert.That(WeaponLightCatalogAssetStep.AssetFolder, Does.EndWith("/Presentation/WeaponLight"));
            Assert.That(WeaponLightCatalogAssetStep.MenuPath, Is.EqualTo("Shooter/Build Weapon Light Catalog"));
        }

        [Test]
        public void TheTuningLightHasOneVisualWithItsConfiguredValues()
        {
            var visuals = WeaponLightCatalogAssetStep.Visuals(KitTuning.Items);

            Assert.That(visuals.Select(v => v.VisualId), Is.EqualTo(new[] { "weaponLight" }));
            var spec = visuals[0].Spec;
            Assert.That(spec.Range, Is.EqualTo(25f));
            Assert.That(spec.SpotAngle, Is.EqualTo(45f));
            Assert.That(spec.Intensity, Is.EqualTo(3f));
            Assert.That(WeaponLightCatalogAssetStep.PrefabName("weaponLight"), Is.EqualTo("WeaponLight_weaponLight"));
        }

        [Test]
        public void VisualsSkipLightsWithoutAVisualAndKeepTheFirstOfASharedVisual()
        {
            var visuals = WeaponLightCatalogAssetStep.Visuals(Items());

            Assert.That(visuals.Select(v => v.VisualId), Is.EqualTo(new[] { "wide", "narrow" }));
            Assert.That(visuals[0].Spec.SpotAngle, Is.EqualTo(70f), "the first item with the visual wins");
            Assert.That(visuals[1].Spec.Range, Is.EqualTo(40f));
            Assert.Throws<ArgumentNullException>(() => WeaponLightCatalogAssetStep.Visuals(null));
        }

        [TestCase("weaponLight", "WeaponLight_weaponLight")]
        [TestCase("  tac/light.v2 ", "WeaponLight_tac_light_v2")]
        public void PrefabNamesAreSafeFileNames(string visualId, string expected)
        {
            Assert.That(WeaponLightCatalogAssetStep.PrefabName(visualId), Is.EqualTo(expected));
        }

        [Test]
        public void PrefabNameRejectsAnEmptyId()
        {
            Assert.Throws<ArgumentException>(() => WeaponLightCatalogAssetStep.PrefabName(null));
            Assert.Throws<ArgumentException>(() => WeaponLightCatalogAssetStep.PrefabName(" "));
        }

        [Test]
        public void TheBeamStopsShortOfTheRangeAndStaysInsideTheCone()
        {
            var tuned = new WeaponLightSpec(25f, 45f, 3f, "weaponLight");
            var shortRange = new WeaponLightSpec(4f, 60f, 1f, "short");

            Assert.That(WeaponLightCatalogAssetStep.BeamLength(tuned), Is.EqualTo(WeaponLightCatalogAssetStep.MaxBeamLength));
            Assert.That(WeaponLightCatalogAssetStep.BeamLength(shortRange), Is.EqualTo(4f));
            Assert.That(WeaponLightCatalogAssetStep.BeamEndWidth(tuned), Is.EqualTo(2f * 6f * Mathf.Tan(22.5f * Mathf.Deg2Rad)).Within(1e-4f));
            Assert.That(WeaponLightCatalogAssetStep.BeamEndWidth(shortRange), Is.EqualTo(2f * 4f * Mathf.Tan(30f * Mathf.Deg2Rad)).Within(1e-4f));
            Assert.Throws<ArgumentNullException>(() => WeaponLightCatalogAssetStep.BeamLength(null));
        }

        [Test]
        public void BuildAtSavesASpotLightWithABeamAndDustForEveryVisual()
        {
            var catalog = WeaponLightCatalogAssetStep.BuildAt(_path, _assetFolder, Items());

            Assert.That(catalog.Lights.Select(e => e.Id), Is.EqualTo(new[] { "wide", "narrow" }));
            foreach (var entry in catalog.Lights)
            {
                Assert.That(entry.Prefab, Is.Not.Null, entry.Id);
                Assert.That(entry.Prefab.name, Is.EqualTo(WeaponLightCatalogAssetStep.PrefabName(entry.Id)));
                Assert.That(AssetDatabase.GetAssetPath(entry.Prefab), Does.StartWith(_assetFolder + "/"));
                Assert.That(entry.Prefab.GetComponentsInChildren<Collider>(true), Is.Empty, entry.Id);
                Assert.That(WeaponLightCatalog.PrefabFor(catalog, entry.Id), Is.SameAs(entry.Prefab));
            }

            Assert.That(catalog.TryGet("wide", out var wide), Is.True);
            var light = wide.Prefab.GetComponent<Light>();
            Assert.That(light, Is.Not.Null, "the light sits on the root so its +Z is the beam direction");
            Assert.That(light.type, Is.EqualTo(LightType.Spot));
            Assert.That(light.range, Is.EqualTo(30f));
            Assert.That(light.spotAngle, Is.EqualTo(70f));
            Assert.That(light.innerSpotAngle, Is.EqualTo(70f * WeaponLightCatalogAssetStep.InnerSpotAngleRatio).Within(1e-4f));
            Assert.That(light.intensity, Is.EqualTo(2f));
            Assert.That(light.shadows, Is.EqualTo(LightShadows.None));
            Assert.That(light.color, Is.EqualTo(WeaponLightCatalogAssetStep.LightColor));

            var beam = wide.Prefab.transform.Find(WeaponLightCatalogAssetStep.BeamName).GetComponent<ParticleSystem>();
            Assert.That(beam.main.loop, Is.True, "the beam keeps shining while the light is on");
            Assert.That(beam.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.Local), "the beam follows the gun");
            Assert.That(beam.emission.rateOverTime.constant, Is.EqualTo(WeaponLightCatalogAssetStep.BeamRate));
            Assert.That(beam.emission.burstCount, Is.EqualTo(0));
            Assert.That(beam.shape.enabled, Is.False, "the beam goes straight along +Z");

            var dust = wide.Prefab.transform.Find(WeaponLightCatalogAssetStep.DustName).GetComponent<ParticleSystem>();
            Assert.That(dust.main.loop, Is.True);
            Assert.That(dust.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World), "dust hangs in the air when the gun moves");
            Assert.That(dust.shape.shapeType, Is.EqualTo(ParticleSystemShapeType.ConeVolume));
            Assert.That(dust.shape.angle, Is.EqualTo(35f));
            Assert.That(dust.shape.length, Is.EqualTo(WeaponLightCatalogAssetStep.MaxBeamLength));
            Assert.That(dust.noise.enabled, Is.True);

            Assert.That(catalog.TryGet("narrow", out var narrow), Is.True);
            Assert.That(narrow.Prefab.GetComponent<Light>().spotAngle, Is.EqualTo(20f));
            Assert.That(narrow.Prefab.transform.Find(WeaponLightCatalogAssetStep.DustName).GetComponent<ParticleSystem>().shape.length, Is.EqualTo(WeaponLightCatalogAssetStep.MaxBeamLength));
        }

        [Test]
        public void BuildAtKeepsTheAssetsOnASecondRun()
        {
            var first = WeaponLightCatalogAssetStep.BuildAt(_path, _assetFolder, Items());
            var guid = AssetDatabase.AssetPathToGUID(_path);
            var prefabGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(first.Lights[0].Prefab));

            var second = WeaponLightCatalogAssetStep.BuildAt(_path, _assetFolder, Items());

            Assert.That(AssetDatabase.LoadAssetAtPath<WeaponLightCatalog>(_path), Is.SameAs(second));
            Assert.That(second, Is.SameAs(first));
            Assert.That(AssetDatabase.AssetPathToGUID(_path), Is.EqualTo(guid));
            Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(second.Lights[0].Prefab)), Is.EqualTo(prefabGuid));
            Assert.That(second.Lights.Select(e => e.Id), Is.EqualTo(new[] { "wide", "narrow" }));
        }

        [Test]
        public void BuildAtWithTheTuningWritesTheWeaponLightVisual()
        {
            var catalog = WeaponLightCatalogAssetStep.BuildAt(_path, _assetFolder);

            Assert.That(catalog.Lights.Select(e => e.Id), Is.EqualTo(new[] { "weaponLight" }));
            var prefab = WeaponLightCatalog.PrefabFor(catalog, "weaponLight");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<Light>().range, Is.EqualTo(25f));
            Assert.That(prefab.GetComponent<Light>().spotAngle, Is.EqualTo(45f));
            Assert.That(prefab.GetComponent<Light>().intensity, Is.EqualTo(3f));
        }

        [Test]
        public void BuildAtRejectsAPathOutsideAssets()
        {
            Assert.Throws<ArgumentException>(() => WeaponLightCatalogAssetStep.BuildAt(_path, "Library/WeaponLight", Items()));
            Assert.That(AssetDatabase.IsValidFolder(_folder), Is.False, "nothing is written before the paths are checked");
        }

        private static ItemCatalog Items()
        {
            return InventoryJson.Parse(
                "{ \"items\": ["
                + " { \"id\": \"" + WideLightId + "\", \"attachment\": { \"slot\": \"light\", \"light\": { \"range\": 30, \"spotAngle\": 70, \"intensity\": 2, \"visualId\": \"wide\" } } },"
                + " { \"id\": \"" + NarrowLightId + "\", \"attachment\": { \"slot\": \"light\", \"light\": { \"range\": 40, \"spotAngle\": 20, \"intensity\": 5, \"visualId\": \"narrow\" } } },"
                + " { \"id\": \"" + SameVisualLightId + "\", \"attachment\": { \"slot\": \"light\", \"light\": { \"range\": 10, \"spotAngle\": 30, \"visualId\": \"wide\" } } },"
                + " { \"id\": \"" + PlainLightId + "\", \"attachment\": { \"slot\": \"light\", \"light\": {} } }"
                + " ] }",
                "inventory.json");
        }
    }
}

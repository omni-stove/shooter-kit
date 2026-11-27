using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shooter.Editor;
using Shooter.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class StealthContentSlotsTests
    {
        private readonly List<Object> _created = new List<Object>();
        private GameContent _content;

        [SetUp]
        public void SetUp()
        {
            StealthActionInstaller.RegisterContentSlots();
            _content = Track(ScriptableObject.CreateInstance<GameContent>());
        }

        [TearDown]
        public void TearDown()
        {
            GameContent.Use(null);
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void TheAwarenessIndicatorSlotReadsAndWritesTheGameContent()
        {
            var slot = AwarenessIndicatorSlot();
            var indicator = Track(ScriptableObject.CreateInstance<AwarenessIndicatorCatalog>());

            Assert.That(slot.AssetType, Is.EqualTo(typeof(AwarenessIndicatorCatalog)));
            Assert.That(slot.Get(_content), Is.Null);

            slot.Set(_content, indicator);

            Assert.That(_content.AwarenessIndicator, Is.SameAs(indicator));
            Assert.That(slot.Get(_content), Is.SameAs(indicator));
        }

        [Test]
        public void AnotherTypeInTheAwarenessIndicatorFieldIsNotReadAsAnIndicator()
        {
            var slot = AwarenessIndicatorSlot();
            _content.AwarenessIndicator = Track(ScriptableObject.CreateInstance<MotorSettings>());

            Assert.That(slot.Get(_content), Is.Null);
        }

        [Test]
        public void FillEmptyLeavesTheAwarenessIndicatorSlotWhenTheCandidateIsAmbiguous()
        {
            var indicatorA = Track(ScriptableObject.CreateInstance<AwarenessIndicatorCatalog>());
            var indicatorB = Track(ScriptableObject.CreateInstance<AwarenessIndicatorCatalog>());
            var candidates = new Dictionary<System.Type, Object[]>
            {
                { typeof(AwarenessIndicatorCatalog), new Object[] { indicatorA, indicatorB } },
            };

            var left = GameContentAssets.FillEmpty(_content, t => candidates.TryGetValue(t, out var found) ? found : new Object[0], _ => new TextAsset[0]);

            Assert.That(_content.AwarenessIndicator, Is.Null);
            Assert.That(left, Does.Contain(StealthActionInstaller.AwarenessIndicatorSlotName));
        }

        [Test]
        public void FillEmptyFillsTheAwarenessIndicatorSlotWithTheOnlyCatalog()
        {
            var indicator = Track(ScriptableObject.CreateInstance<AwarenessIndicatorCatalog>());
            var candidates = new Dictionary<System.Type, Object[]>
            {
                { typeof(AwarenessIndicatorCatalog), new Object[] { indicator } },
            };

            var left = GameContentAssets.FillEmpty(_content, t => candidates.TryGetValue(t, out var found) ? found : new Object[0], _ => new TextAsset[0]);

            Assert.That(_content.AwarenessIndicator, Is.SameAs(indicator));
            Assert.That(left, Does.Not.Contain(StealthActionInstaller.AwarenessIndicatorSlotName));
        }

        [Test]
        public void TheAwarenessIndicatorComesFromTheEntry()
        {
            _content.AwarenessIndicator = Track(ScriptableObject.CreateInstance<AwarenessIndicatorCatalog>());
            GameContent.Use(_content);

            Assert.That(AwarenessIndicatorCatalog.Load(), Is.SameAs(_content.AwarenessIndicator));
        }

        [Test]
        public void TheSecurityCameraSlotIsListed()
        {
            Assert.That(StealthActionInstaller.SecurityCameraSlotName, Is.EqualTo("SecurityCamera"));
            Assert.That(GameContentSlots.All.Select(s => s.Name), Has.Member("SecurityCamera"));
            Assert.That(GameContentSlots.All.Count(s => s.Name == "SecurityCamera"), Is.EqualTo(1));

            StealthActionInstaller.RegisterContentSlots();

            Assert.That(GameContentSlots.All.Count(s => s.Name == "SecurityCamera"), Is.EqualTo(1), "registering again keeps one slot");
            Assert.That(GameContentSlots.All.Select(s => s.Name), Has.Member(StealthActionInstaller.AwarenessIndicatorSlotName));
        }

        [Test]
        public void TheSecurityCameraSlotReadsAndWritesTheGameContent()
        {
            var slot = SecurityCameraSlot();
            var catalog = Track(ScriptableObject.CreateInstance<SecurityCameraCatalog>());

            Assert.That(slot.AssetType, Is.EqualTo(typeof(SecurityCameraCatalog)));
            Assert.That(slot.Get(_content), Is.Null);

            slot.Set(_content, catalog);

            Assert.That(_content.SecurityCamera, Is.SameAs(catalog));
            Assert.That(slot.Get(_content), Is.SameAs(catalog));
            Assert.That(_content.AwarenessIndicator, Is.Null, "the camera has its own field");
        }

        [Test]
        public void AnotherTypeInTheSecurityCameraFieldIsNotReadAsACamera()
        {
            var slot = SecurityCameraSlot();
            _content.SecurityCamera = Track(ScriptableObject.CreateInstance<AwarenessIndicatorCatalog>());

            Assert.That(slot.Get(_content), Is.Null);
        }

        [Test]
        public void FillEmptyFillsTheSecurityCameraSlotWithTheOnlyCatalog()
        {
            var catalog = Track(ScriptableObject.CreateInstance<SecurityCameraCatalog>());
            var candidates = new Dictionary<System.Type, Object[]>
            {
                { typeof(SecurityCameraCatalog), new Object[] { catalog } },
            };

            var left = GameContentAssets.FillEmpty(_content, t => candidates.TryGetValue(t, out var found) ? found : new Object[0], _ => new TextAsset[0]);

            Assert.That(_content.SecurityCamera, Is.SameAs(catalog));
            Assert.That(left, Does.Not.Contain(StealthActionInstaller.SecurityCameraSlotName));
        }

        [Test]
        public void FillEmptyLeavesTheSecurityCameraSlotWhenTheCandidateIsAmbiguous()
        {
            var catalogA = Track(ScriptableObject.CreateInstance<SecurityCameraCatalog>());
            var catalogB = Track(ScriptableObject.CreateInstance<SecurityCameraCatalog>());
            var candidates = new Dictionary<System.Type, Object[]>
            {
                { typeof(SecurityCameraCatalog), new Object[] { catalogA, catalogB } },
            };

            var left = GameContentAssets.FillEmpty(_content, t => candidates.TryGetValue(t, out var found) ? found : new Object[0], _ => new TextAsset[0]);

            Assert.That(_content.SecurityCamera, Is.Null);
            Assert.That(left, Does.Contain(StealthActionInstaller.SecurityCameraSlotName));
        }

        [Test]
        public void TheSecurityCameraCatalogComesFromTheEntry()
        {
            var catalog = Track(ScriptableObject.CreateInstance<SecurityCameraCatalog>());
            var prefab = Track(new GameObject("CameraLook"));
            var clip = Track(AudioClip.Create("Alarm", 441, 1, 44100, false));
            catalog.CameraPrefab = prefab;
            catalog.AlarmClip = clip;
            _content.SecurityCamera = catalog;
            GameContent.Use(_content);

            var loaded = SecurityCameraCatalog.Load();

            Assert.That(loaded, Is.SameAs(catalog));
            Assert.That(loaded.CameraPrefab, Is.SameAs(prefab));
            Assert.That(loaded.AlarmClip, Is.SameAs(clip));
            Assert.That(SecurityCameraCatalog.ResourcePath, Is.EqualTo(PresentationCatalog.CatalogResourcePath(SecurityCameraCatalog.CatalogName)));
        }

        private static GameContentSlot SecurityCameraSlot()
        {
            return GameContentSlots.All.Single(s => s.Name == StealthActionInstaller.SecurityCameraSlotName);
        }

        private static GameContentSlot AwarenessIndicatorSlot()
        {
            return GameContentSlots.All.Single(s => s.Name == StealthActionInstaller.AwarenessIndicatorSlotName);
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}

using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Shooter.Tests.EditMode
{
    public sealed class HolsterTuningFileTests
    {
        private GameContent _content;
        private TextAsset _asset;

        [TearDown]
        public void TearDown()
        {
            GameContent.Use(null);
            if (_asset != null)
            {
                Object.DestroyImmediate(_asset);
            }

            if (_content != null)
            {
                Object.DestroyImmediate(_content);
            }

            KitTuning.Reload();
        }

        [Test]
        public void TheBundledFileHoldsTheDefaults()
        {
            var asset = Resources.Load<TextAsset>(HolsterJson.ResourcePath);
            Assert.That(asset, Is.Not.Null, "the kit ships Resources/Tuning/holster.json");

            var spec = HolsterJson.Parse(asset.text, "Resources/Tuning/holster.json");

            Assert.That(spec.MoveSpeedMultiplier, Is.EqualTo(HolsterSpec.DefaultMoveSpeedMultiplier));
            Assert.That(spec.ArmedPhases, Is.EqualTo(HolsterSpec.DefaultArmedPhases));
            Assert.That(spec.ArmedWhileHeldByOthers, Is.EqualTo(HolsterSpec.DefaultArmedWhileHeldByOthers));
        }

        [Test]
        public void KitTuningReadsTheBundledFile()
        {
            KitTuning.Reload();

            Assert.That(KitTuning.HolsterResourcePath, Is.EqualTo("Tuning/holster"));
            Assert.That(KitTuning.Holster.MoveSpeedMultiplier, Is.EqualTo(1f));
            Assert.That(KitTuning.Holster.IsArmedPhase(HolsterPhase.Drawn), Is.True);
            Assert.That(KitTuning.Holster.IsArmedPhase(HolsterPhase.Holstered), Is.False);
        }

        [Test]
        public void TheGameContentFileIsReadBeforeResources()
        {
            _content = ScriptableObject.CreateInstance<GameContent>();
            _content.name = "Holster";
            _asset = new TextAsset("{\"moveSpeedMultiplier\": 1.1, \"armedPhases\": [\"Drawn\"], \"armedWhileHeldByOthers\": true}") { name = "holster" };
            _content.HolsterTuning = _asset;
            GameContent.Use(_content);
            KitTuning.Reload();

            LogAssert.Expect(LogType.Log, "[Tuning] read GameContent 'Holster' (holster)");
            var spec = KitTuning.Holster;

            Assert.That(spec.MoveSpeedMultiplier, Is.EqualTo(1.1f));
            Assert.That(spec.ArmedPhases, Is.EqualTo(new[] { HolsterPhase.Drawn }));
            Assert.That(spec.ArmedWhileHeldByOthers, Is.True);
        }

        [Test]
        public void ABrokenFileFallsBackToTheDefaults()
        {
            _content = ScriptableObject.CreateInstance<GameContent>();
            _content.name = "Broken";
            _asset = new TextAsset("{\"moveSpeedMultiplier\": -1}") { name = "holster" };
            _content.HolsterTuning = _asset;
            GameContent.Use(_content);
            KitTuning.Reload();

            LogAssert.Expect(LogType.Log, "[Tuning] read GameContent 'Broken' (holster)");
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"(?s)^\[Tuning\] holster JSON .* item 'moveSpeedMultiplier' is invalid: .*; using the default values instead$"));
            var spec = KitTuning.Holster;

            Assert.That(spec, Is.SameAs(HolsterSpec.Default));
        }
    }
}

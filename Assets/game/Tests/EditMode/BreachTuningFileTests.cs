using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Shooter.Tests.EditMode
{
    public sealed class BreachTuningFileTests
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
            var asset = Resources.Load<TextAsset>(BreachJson.ResourcePath);
            Assert.That(asset, Is.Not.Null, "the kit ships Resources/Tuning/breach.json");

            var spec = BreachJson.Parse(asset.text, "Resources/Tuning/breach.json");

            Assert.That(BreachJson.Write(spec), Is.EqualTo(BreachJson.Write(BreachSpec.Default)));
            Assert.That(spec.ChargeItemId, Is.Empty, "the kit has no breaching charge item, so none is needed");
        }

        [Test]
        public void TheBundledFileListsEveryItem()
        {
            var text = Resources.Load<TextAsset>(BreachJson.ResourcePath).text;

            foreach (var item in BreachJson.ItemNames)
            {
                Assert.That(text, Does.Contain("\"" + item + "\""), item);
            }
        }

        [Test]
        public void KitTuningReadsTheBundledFile()
        {
            KitTuning.Reload();

            Assert.That(KitTuning.BreachResourcePath, Is.EqualTo("Tuning/breach"));
            Assert.That(KitTuning.Breach.PlantSeconds, Is.EqualTo(2.5f));
            Assert.That(KitTuning.Breach.Reach, Is.EqualTo(1.5f));
            Assert.That(KitTuning.Breach.NeedsChargeItem, Is.False);
        }

        [Test]
        public void TheGameContentFileIsReadBeforeResources()
        {
            _content = ScriptableObject.CreateInstance<GameContent>();
            _content.name = "Breach";
            _asset = new TextAsset("{\"plantSeconds\": 4, \"chargeItemId\": \"31d5979e-6067-49d9-b70c-fdcf44dc77d1\"}") { name = "breach" };
            _content.BreachTuning = _asset;
            GameContent.Use(_content);
            KitTuning.Reload();

            LogAssert.Expect(LogType.Log, "[Tuning] read GameContent 'Breach' (breach)");
            var spec = KitTuning.Breach;

            Assert.That(spec.PlantSeconds, Is.EqualTo(4f));
            Assert.That(spec.ChargeItemId, Is.EqualTo("31d5979e-6067-49d9-b70c-fdcf44dc77d1"));
            Assert.That(spec.FuseSeconds, Is.EqualTo(BreachSpec.DefaultFuseSeconds));
        }

        [Test]
        public void ABrokenFileFallsBackToTheDefaults()
        {
            _content = ScriptableObject.CreateInstance<GameContent>();
            _content.name = "Broken";
            _asset = new TextAsset("{\"reach\": 0}") { name = "breach" };
            _content.BreachTuning = _asset;
            GameContent.Use(_content);
            KitTuning.Reload();

            LogAssert.Expect(LogType.Log, "[Tuning] read GameContent 'Broken' (breach)");
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"(?s)^\[Tuning\] breach JSON .* item 'reach' is invalid: .*; using the default values instead$"));
            var spec = KitTuning.Breach;

            Assert.That(spec, Is.SameAs(BreachSpec.Default));
        }
    }
}

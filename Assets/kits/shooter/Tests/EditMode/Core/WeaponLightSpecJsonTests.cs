using System;
using GameKit.Core;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class WeaponLightSpecJsonTests
    {
        private const string _source = "light.json";
        private const string _kind = "test JSON";

        [Test]
        public void ReadsEveryItem()
        {
            var light = Read("{ \"range\": 30, \"spotAngle\": 60, \"intensity\": 1.5, \"visualId\": \" tacLight \" }");

            Assert.That(light.Range, Is.EqualTo(30f));
            Assert.That(light.SpotAngle, Is.EqualTo(60f));
            Assert.That(light.Intensity, Is.EqualTo(1.5f));
            Assert.That(light.VisualId, Is.EqualTo("tacLight"), "the id is trimmed like the other reference ids");
            Assert.That(light.HasVisual, Is.True);
        }

        [Test]
        public void AnEmptyObjectIsTheDefaultLight()
        {
            var light = Read("{}");

            Assert.That(light.Range, Is.EqualTo(WeaponLightSpec.DefaultRange));
            Assert.That(light.SpotAngle, Is.EqualTo(WeaponLightSpec.DefaultSpotAngle));
            Assert.That(light.Intensity, Is.EqualTo(WeaponLightSpec.DefaultIntensity));
            Assert.That(light.VisualId, Is.Empty);
            Assert.That(light.HasVisual, Is.False, "no visual id leaves the light to be built in code");
        }

        [Test]
        public void TheDefaultsAreTheDocumentedOnes()
        {
            Assert.That(WeaponLightSpec.DefaultRange, Is.EqualTo(25f));
            Assert.That(WeaponLightSpec.DefaultSpotAngle, Is.EqualTo(45f));
            Assert.That(WeaponLightSpec.DefaultIntensity, Is.EqualTo(3f));
            Assert.That(WeaponLightSpec.Default.Range, Is.EqualTo(WeaponLightSpec.DefaultRange));
            Assert.That(WeaponLightSpec.Default.HasVisual, Is.False);
        }

        [TestCase("{ \"range\": 0 }", "light.range")]
        [TestCase("{ \"range\": -1 }", "light.range")]
        [TestCase("{ \"spotAngle\": 0 }", "light.spotAngle")]
        [TestCase("{ \"spotAngle\": 180 }", "light.spotAngle")]
        [TestCase("{ \"intensity\": -0.5 }", "light.intensity")]
        [TestCase("{ \"range\": \"far\" }", "light.range")]
        [TestCase("{ \"color\": 1 }", "light.color")]
        public void ABadValueNamesItsItem(string json, string item)
        {
            var e = Assert.Throws<ServerDataJsonException>(() => Read(json));

            Assert.That(e.Item, Is.EqualTo(item));
        }

        [Test]
        public void TheWidestAndDimmestLightsAreAllowed()
        {
            var light = Read("{ \"spotAngle\": 179, \"intensity\": 0 }");

            Assert.That(light.SpotAngle, Is.EqualTo(WeaponLightSpec.MaxSpotAngle));
            Assert.That(light.Intensity, Is.Zero);
        }

        [Test]
        public void WhatIsWrittenReadsBackTheSame()
        {
            var written = new WeaponLightSpec(18.5f, 35f, 2.25f, "tacLight");
            var writer = new JsonWriter();

            WeaponLightSpecJson.Write(writer, written);
            var read = ReadDocument(writer.Finish());

            Assert.That(read.Range, Is.EqualTo(written.Range));
            Assert.That(read.SpotAngle, Is.EqualTo(written.SpotAngle));
            Assert.That(read.Intensity, Is.EqualTo(written.Intensity));
            Assert.That(read.VisualId, Is.EqualTo(written.VisualId));
        }

        [Test]
        public void ADefaultLightIsStillWrittenSoItStaysALight()
        {
            var writer = new JsonWriter();

            WeaponLightSpecJson.Write(writer, WeaponLightSpec.Default);
            var text = writer.Finish();

            Assert.That(text, Does.Contain("\"light\""));
            Assert.That(text, Does.Not.Contain("\"visualId\""), "an empty visual id is left out");
            Assert.That(ReadDocument(text).Range, Is.EqualTo(WeaponLightSpec.DefaultRange));
        }

        [Test]
        public void NoLightWritesNothing()
        {
            var writer = new JsonWriter();

            WeaponLightSpecJson.Write(writer, null);

            Assert.That(writer.Finish(), Does.Not.Contain("light"));
            Assert.Throws<ArgumentNullException>(() => WeaponLightSpecJson.Write(null, WeaponLightSpec.Default));
            Assert.Throws<ArgumentNullException>(() => WeaponLightSpecJson.Read(null));
        }

        private static WeaponLightSpec Read(string lightJson)
        {
            return ReadDocument("{ \"" + WeaponLightSpecJson.LightItem + "\": " + lightJson + " }");
        }

        private static WeaponLightSpec ReadDocument(string text)
        {
            var root = new ServerDataJson.Fields(
                _source, _kind, ServerDataJson.ParseDocument(text, _source, _kind), string.Empty, new[] { WeaponLightSpecJson.LightItem });
            return WeaponLightSpecJson.Read(root.Object(WeaponLightSpecJson.LightItem, WeaponLightSpecJson.ItemNames));
        }
    }
}

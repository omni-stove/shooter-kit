using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class HolsterJsonTests
    {
        private static readonly string[] _expectedItemNames = { "moveSpeedMultiplier", "armedPhases", "armedWhileHeldByOthers" };

        [Test]
        public void ReadsEveryItem()
        {
            var spec = HolsterJson.Parse(
                "{\"moveSpeedMultiplier\": 1.15, \"armedPhases\": [\"Drawn\", \"Holstering\"], \"armedWhileHeldByOthers\": true}",
                "holster.json");

            Assert.That(spec.MoveSpeedMultiplier, Is.EqualTo(1.15f));
            Assert.That(spec.ArmedPhases, Is.EqualTo(new[] { HolsterPhase.Drawn, HolsterPhase.Holstering }));
            Assert.That(spec.ArmedWhileHeldByOthers, Is.True);
        }

        [Test]
        public void LeftOutItemsUseTheDefaults()
        {
            var empty = HolsterJson.Parse("{}", "holster.json");

            Assert.That(empty.MoveSpeedMultiplier, Is.EqualTo(HolsterSpec.DefaultMoveSpeedMultiplier));
            Assert.That(empty.ArmedPhases, Is.EqualTo(new[] { HolsterPhase.Drawn, HolsterPhase.Drawing }));
            Assert.That(empty.ArmedWhileHeldByOthers, Is.False);
            Assert.That(HolsterSpec.DefaultMoveSpeedMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void AnEmptyArmedListMeansNeverArmed()
        {
            var spec = HolsterJson.Parse("{\"armedPhases\": []}", "holster.json");

            Assert.That(spec.ArmedPhases, Is.Empty);
            Assert.That(spec.IsArmedPhase(HolsterPhase.Drawn), Is.False);
        }

        [Test]
        public void TheItemNamesAreTheConstructorArguments()
        {
            Assert.That(HolsterJson.ItemNames, Is.EqualTo(_expectedItemNames));
            Assert.That(HolsterJson.ResourcePath, Is.EqualTo("Tuning/holster"));
        }

        [Test]
        public void ANegativeMultiplierNamesItsItem()
        {
            var negative = Assert.Throws<TuningJsonException>(() => HolsterJson.Parse("{\"moveSpeedMultiplier\": -1}", "holster.json"));

            Assert.That(negative.SourceName, Is.EqualTo("holster.json"));
            Assert.That(negative.Item, Is.EqualTo("moveSpeedMultiplier"));
            Assert.That(negative.Message, Does.StartWith("holster JSON 'holster.json' item 'moveSpeedMultiplier' is invalid: "));
            Assert.That(negative.InnerException, Is.InstanceOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void ARepeatedOrUnknownPhaseNamesTheArmedList()
        {
            var repeated = Assert.Throws<TuningJsonException>(() => HolsterJson.Parse("{\"armedPhases\": [\"Drawn\", \"Drawn\"]}", "holster.json"));
            var unknown = Assert.Throws<TuningJsonException>(() => HolsterJson.Parse("{\"armedPhases\": [\"Sheathed\"]}", "holster.json"));
            var number = Assert.Throws<TuningJsonException>(() => HolsterJson.Parse("{\"armedPhases\": [\"1\"]}", "holster.json"));

            Assert.That(repeated.Item, Is.EqualTo("armedPhases"));
            Assert.That(repeated.InnerException, Is.InstanceOf<ArgumentException>());
            Assert.That(unknown.Item, Is.EqualTo("armedPhases"));
            Assert.That(unknown.Message, Does.Contain("Holstering"), "lists the phase names");
            Assert.That(number.Item, Is.EqualTo("armedPhases"), "only names are phases");
        }

        [Test]
        public void AnUnknownItemAWrongTypeOrBrokenJsonIsRejected()
        {
            var unknown = Assert.Throws<TuningJsonException>(() => HolsterJson.Parse("{\"speed\": 2}", "holster.json"));
            var wrongType = Assert.Throws<TuningJsonException>(() => HolsterJson.Parse("{\"armedWhileHeldByOthers\": \"yes\"}", "holster.json"));
            var notAList = Assert.Throws<TuningJsonException>(() => HolsterJson.Parse("{\"armedPhases\": \"Drawn\"}", "holster.json"));
            var broken = Assert.Throws<TuningJsonException>(() => HolsterJson.Parse("{\"moveSpeedMultiplier\": ", "holster.json"));

            Assert.That(unknown.Item, Is.EqualTo("speed"));
            Assert.That(unknown.Message, Does.Contain("moveSpeedMultiplier"), "lists the known items");
            Assert.That(wrongType.Item, Is.EqualTo("armedWhileHeldByOthers"));
            Assert.That(notAList.Item, Is.EqualTo("armedPhases"));
            Assert.That(broken.Item, Is.Null);
            Assert.Throws<ArgumentNullException>(() => HolsterJson.Parse(null, "holster.json"));
        }
    }
}

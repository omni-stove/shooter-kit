using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class BreachJsonTests
    {
        private const string _itemId = "31d5979e-6067-49d9-b70c-fdcf44dc77d1";

        private static readonly string[] _expectedItemNames =
        {
            "kickSeconds",
            "plantSeconds",
            "fuseSeconds",
            "reach",
            "roomDepth",
            "roomWidth",
            "roomHeight",
            "kickStaggerSeconds",
            "chargeStaggerSeconds",
            "chargeDamage",
            "chargeRadius",
            "kickSoundRadius",
            "chargeSoundRadius",
            "slowMotionScale",
            "slowMotionSeconds",
            "chargeItemId",
        };

        [Test]
        public void ReadsEveryItem()
        {
            var spec = BreachJson.Parse(
                "{\"kickSeconds\": 0.4, \"plantSeconds\": 3, \"fuseSeconds\": 1.5, \"reach\": 2, \"roomDepth\": 6, \"roomWidth\": 5, \"roomHeight\": 3,"
                + " \"kickStaggerSeconds\": 1, \"chargeStaggerSeconds\": 2.5, \"chargeDamage\": 75, \"chargeRadius\": 3.5,"
                + " \"kickSoundRadius\": 20, \"chargeSoundRadius\": 80, \"slowMotionScale\": 0.5, \"slowMotionSeconds\": 2,"
                + " \"chargeItemId\": \"" + _itemId + "\"}",
                "breach.json");

            Assert.That(spec.KickSeconds, Is.EqualTo(0.4f));
            Assert.That(spec.PlantSeconds, Is.EqualTo(3f));
            Assert.That(spec.FuseSeconds, Is.EqualTo(1.5f));
            Assert.That(spec.Reach, Is.EqualTo(2f));
            Assert.That(spec.RoomDepth, Is.EqualTo(6f));
            Assert.That(spec.RoomWidth, Is.EqualTo(5f));
            Assert.That(spec.RoomHeight, Is.EqualTo(3f));
            Assert.That(spec.KickStaggerSeconds, Is.EqualTo(1f));
            Assert.That(spec.ChargeStaggerSeconds, Is.EqualTo(2.5f));
            Assert.That(spec.ChargeDamage, Is.EqualTo(75));
            Assert.That(spec.ChargeRadius, Is.EqualTo(3.5f));
            Assert.That(spec.KickSoundRadius, Is.EqualTo(20f));
            Assert.That(spec.ChargeSoundRadius, Is.EqualTo(80f));
            Assert.That(spec.SlowMotionScale, Is.EqualTo(0.5f));
            Assert.That(spec.SlowMotionSeconds, Is.EqualTo(2f));
            Assert.That(spec.ChargeItemId, Is.EqualTo(_itemId));
            Assert.That(spec.NeedsChargeItem, Is.True);
        }

        [Test]
        public void LeftOutItemsUseTheDefaults()
        {
            var empty = BreachJson.Parse("{}", "breach.json");

            Assert.That(BreachJson.Write(empty), Is.EqualTo(BreachJson.Write(BreachSpec.Default)));
            Assert.That(empty.KickSeconds, Is.EqualTo(0.6f));
            Assert.That(empty.PlantSeconds, Is.EqualTo(2.5f));
            Assert.That(empty.FuseSeconds, Is.EqualTo(2f));
            Assert.That(empty.Reach, Is.EqualTo(1.5f));
            Assert.That(empty.KickStaggerSeconds, Is.EqualTo(1.5f));
            Assert.That(empty.ChargeStaggerSeconds, Is.EqualTo(3f));
            Assert.That(empty.ChargeDamage, Is.EqualTo(40));
            Assert.That(empty.SlowMotionScale, Is.EqualTo(0.35f));
            Assert.That(empty.SlowMotionSeconds, Is.EqualTo(1.5f));
            Assert.That(empty.ChargeItemId, Is.Empty);
            Assert.That(empty.NeedsChargeItem, Is.False, "without an item id the charge needs nothing from the inventory");
            Assert.That(empty.SlowsMotion, Is.True);
        }

        [Test]
        public void LeftOutItemsUseTheGivenFallbackNotTheDefaults()
        {
            var configured = new BreachSpec(0.6f, 2.5f, 2f, 1.5f, 8f, 3f, 2f, 1.5f, 3f, 80, 4f, 40f, 25f, 0.35f, 1.5f, string.Empty);

            var spec = BreachJson.Parse("{\"plantSeconds\": 1}", "door.json", configured);

            Assert.That(spec.PlantSeconds, Is.EqualTo(1f));
            Assert.That(spec.RoomDepth, Is.EqualTo(8f));
            Assert.That(spec.ChargeDamage, Is.EqualTo(80));
            Assert.That(spec.KickSoundRadius, Is.EqualTo(40f));
        }

        [Test]
        public void WhatIsWrittenReadsBackTheSame()
        {
            var spec = new BreachSpec(0.3f, 1.25f, 0f, 1.75f, 7f, 3f, 2f, 0f, 4f, 0, 0f, 0f, 45f, 1f, 0f, " " + _itemId + " ");

            var written = BreachJson.Write(spec);
            var read = BreachJson.Parse(written, "breach.json");

            Assert.That(BreachJson.Write(read), Is.EqualTo(written));
            Assert.That(read.ChargeItemId, Is.EqualTo(_itemId), "the id is trimmed");
            Assert.That(read.FuseSeconds, Is.Zero);
            Assert.That(read.KickStaggerSeconds, Is.Zero, "0 turns the kick stagger off");
            Assert.That(read.ChargeDamage, Is.Zero);
            Assert.That(read.SlowsMotion, Is.False);
            foreach (var item in _expectedItemNames)
            {
                Assert.That(written, Does.Contain("\"" + item + "\""), item);
            }
        }

        [Test]
        public void TheItemNamesAreTheConstructorArguments()
        {
            Assert.That(BreachJson.ItemNames, Is.EqualTo(_expectedItemNames));
            Assert.That(BreachJson.ResourcePath, Is.EqualTo("Tuning/breach"));
        }

        [Test]
        public void TheMethodPicksItsStaggerAndSound()
        {
            var spec = BreachSpec.Default;

            Assert.That(BreachSpec.StaggerKindFor(BreachMethod.Kick), Is.EqualTo(StaggerKind.Stumble));
            Assert.That(BreachSpec.StaggerKindFor(BreachMethod.Charge), Is.EqualTo(StaggerKind.Blast));
            Assert.That(spec.StaggerSecondsFor(BreachMethod.Kick), Is.EqualTo(BreachSpec.DefaultKickStaggerSeconds));
            Assert.That(spec.StaggerSecondsFor(BreachMethod.Charge), Is.EqualTo(BreachSpec.DefaultChargeStaggerSeconds));
            Assert.That(spec.SoundRadiusFor(BreachMethod.Kick), Is.EqualTo(BreachSpec.DefaultKickSoundRadius));
            Assert.That(spec.SoundRadiusFor(BreachMethod.Charge), Is.EqualTo(BreachSpec.DefaultChargeSoundRadius));
            Assert.Throws<ArgumentOutOfRangeException>(() => BreachSpec.StaggerKindFor((BreachMethod)9));
            Assert.Throws<ArgumentOutOfRangeException>(() => spec.StaggerSecondsFor((BreachMethod)9));
        }

        [TestCase("{\"kickSeconds\": -1}", "kickSeconds")]
        [TestCase("{\"plantSeconds\": -0.5}", "plantSeconds")]
        [TestCase("{\"fuseSeconds\": -1}", "fuseSeconds")]
        [TestCase("{\"reach\": 0}", "reach")]
        [TestCase("{\"roomDepth\": 0}", "roomDepth")]
        [TestCase("{\"roomWidth\": -2}", "roomWidth")]
        [TestCase("{\"roomHeight\": 0}", "roomHeight")]
        [TestCase("{\"kickStaggerSeconds\": -1}", "kickStaggerSeconds")]
        [TestCase("{\"chargeStaggerSeconds\": -1}", "chargeStaggerSeconds")]
        [TestCase("{\"chargeDamage\": -1}", "chargeDamage")]
        [TestCase("{\"chargeRadius\": -1}", "chargeRadius")]
        [TestCase("{\"kickSoundRadius\": -1}", "kickSoundRadius")]
        [TestCase("{\"chargeSoundRadius\": -1}", "chargeSoundRadius")]
        [TestCase("{\"slowMotionScale\": 0}", "slowMotionScale")]
        [TestCase("{\"slowMotionScale\": 1.5}", "slowMotionScale")]
        [TestCase("{\"slowMotionSeconds\": -1}", "slowMotionSeconds")]
        [TestCase("{\"chargeItemId\": \"Breach-Charge\"}", "chargeItemId")]
        public void AValueOutOfRangeNamesItsItem(string json, string item)
        {
            var rejected = Assert.Throws<TuningJsonException>(() => BreachJson.Parse(json, "breach.json"));

            Assert.That(rejected.SourceName, Is.EqualTo("breach.json"));
            Assert.That(rejected.Item, Is.EqualTo(item));
            Assert.That(rejected.Message, Does.StartWith("breach JSON 'breach.json' item '" + item + "' is invalid: "));
            Assert.That(rejected.InnerException, Is.InstanceOf<ArgumentException>());
        }

        [Test]
        public void AnUnknownItemAWrongTypeOrBrokenJsonIsRejected()
        {
            var unknown = Assert.Throws<TuningJsonException>(() => BreachJson.Parse("{\"kickDamage\": 2}", "breach.json"));
            var wrongType = Assert.Throws<TuningJsonException>(() => BreachJson.Parse("{\"reach\": \"far\"}", "breach.json"));
            var notAnId = Assert.Throws<TuningJsonException>(() => BreachJson.Parse("{\"chargeItemId\": 3}", "breach.json"));
            var fractionalDamage = Assert.Throws<TuningJsonException>(() => BreachJson.Parse("{\"chargeDamage\": 1.5}", "breach.json"));
            var broken = Assert.Throws<TuningJsonException>(() => BreachJson.Parse("{\"reach\": ", "breach.json"));

            Assert.That(unknown.Item, Is.EqualTo("kickDamage"));
            Assert.That(unknown.Message, Does.Contain("plantSeconds"), "lists the known items");
            Assert.That(wrongType.Item, Is.EqualTo("reach"));
            Assert.That(notAnId.Item, Is.EqualTo("chargeItemId"));
            Assert.That(fractionalDamage.Item, Is.EqualTo("chargeDamage"));
            Assert.That(broken.Item, Is.Null);
            Assert.Throws<ArgumentNullException>(() => BreachJson.Parse(null, "breach.json"));
            Assert.Throws<ArgumentNullException>(() => BreachJson.Write(null));
        }

        [Test]
        public void TheLogLineListsTheSpec()
        {
            Assert.That(
                BreachLog.Loaded(BreachSpec.Default),
                Is.EqualTo("[Breach] spec kickSeconds=0.60 plantSeconds=2.50 fuseSeconds=2.00 reach=1.50 room=(5.00, 4.00, 2.50) "
                    + "stagger=(1.50, 3.00) charge=(40, 2.50) sound=(25.00, 60.00) slowMotion=(0.35, 1.50) chargeItem=-"));
            Assert.That(BreachLog.Kicked("door-1", "player-1"), Is.EqualTo("[Breach] door=door-1 kicked by=player-1 phase=Opened"));
            Assert.That(
                BreachLog.PlantCancelled("door-1", null, "staggered"),
                Is.EqualTo("[Breach] door=door-1 plant-cancelled by=- reason=staggered"));
            Assert.That(
                BreachLog.Detonated("door-1", "player-1", 40, 2.5f),
                Is.EqualTo("[Breach] door=door-1 detonated by=player-1 damage=40 radius=2.50 phase=Destroyed"));
            Assert.That(
                BreachLog.Staggered("door-1", BreachMethod.Kick, StaggerKind.Stumble, 1.5f, 2),
                Is.EqualTo("[Breach] door=door-1 staggered method=Kick kind=Stumble seconds=1.50 count=2"));
        }
    }
}

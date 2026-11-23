using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class NetMapWeaponFreeZonesTests
    {
        [Test]
        public void WrittenZonesReadBackInTheSameOrder()
        {
            var zones = new NetMapWeaponFreeZones(new[]
            {
                new MapWeaponFreeZone("Lobby", new Vector3(-4f, 0f, -4f), new Vector3(4f, 3f, 4f), new WeaponFreeZoneSpec(WeaponFreeZoneReaction.AutoHolster)),
                new MapWeaponFreeZone(
                    "Gate", new Vector3(10f, -0.5f, 2f), new Vector3(12.25f, 2f, 6f), new WeaponFreeZoneSpec(WeaponFreeZoneReaction.WarnThenHostile, 2.5f)),
                new MapWeaponFreeZone("Vault", new Vector3(-8f, 0f, 0f), new Vector3(-6f, 3f, 2f), new WeaponFreeZoneSpec(WeaponFreeZoneReaction.BlockDraw)),
            });

            var text = NetMapWeaponFreeZones.Write(zones);
            var read = NetMapWeaponFreeZones.Parse(text, "test");

            Assert.That(read.Zones, Has.Count.EqualTo(3));
            Assert.That(read.Zones[0].Id, Is.EqualTo("Lobby"));
            Assert.That(read.Zones[0].Reaction, Is.EqualTo(WeaponFreeZoneReaction.AutoHolster));
            Assert.That(read.Zones[1].Id, Is.EqualTo("Gate"));
            Assert.That(read.Zones[1].Min, Is.EqualTo(new Vector3(10f, -0.5f, 2f)));
            Assert.That(read.Zones[1].Max, Is.EqualTo(new Vector3(12.25f, 2f, 6f)));
            Assert.That(read.Zones[1].Reaction, Is.EqualTo(WeaponFreeZoneReaction.WarnThenHostile));
            Assert.That(read.Zones[1].Spec.HostileAfterSeconds, Is.EqualTo(2.5f));
            Assert.That(read.Zones[2].Reaction, Is.EqualTo(WeaponFreeZoneReaction.BlockDraw));
            Assert.That(NetMapWeaponFreeZones.Write(read), Is.EqualTo(text));
        }

        [Test]
        public void TheReactionIsWrittenByName()
        {
            var zones = new NetMapWeaponFreeZones(new[]
            {
                new MapWeaponFreeZone("Shop", Vector3.Zero, Vector3.One, new WeaponFreeZoneSpec(WeaponFreeZoneReaction.BlockDraw)),
            });

            Assert.That(NetMapWeaponFreeZones.Write(zones), Does.Contain("\"reaction\": \"BlockDraw\""));
        }

        [Test]
        public void ALeftOutReactionAndSecondsReadAsTheDefaults()
        {
            var read = NetMapWeaponFreeZones.Parse("{ \"zones\": [ { \"id\": \"Shop\", \"min\": [0, 0, 0], \"max\": [1, 1, 1] } ] }", "test");

            Assert.That(read.Zones[0].Reaction, Is.EqualTo(WeaponFreeZoneSpec.DefaultReaction));
            Assert.That(read.Zones[0].Spec.HostileAfterSeconds, Is.EqualTo(WeaponFreeZoneSpec.DefaultHostileAfterSeconds));
        }

        [Test]
        public void AnEmptyFileHasNoZones()
        {
            Assert.That(NetMapWeaponFreeZones.Parse("{ \"zones\": [] }", "test").Zones, Is.Empty);
            Assert.That(NetMapWeaponFreeZones.Parse("{}", "test").Zones, Is.Empty);
            Assert.That(NetMapWeaponFreeZones.None.Zones, Is.Empty);
        }

        [Test]
        public void AZoneContainsOnlyPointsInsideItsBox()
        {
            var zone = new MapWeaponFreeZone("Shop", Vector3.Zero, new Vector3(4f, 3f, 4f), WeaponFreeZoneSpec.Default);

            Assert.That(zone.Contains(new Vector3(1f, 1f, 1f)), Is.True);
            Assert.That(zone.Contains(new Vector3(4f, 3f, 4f)), Is.True, "the edge is inside");
            Assert.That(zone.Contains(new Vector3(5f, 1f, 1f)), Is.False);
            Assert.That(zone.Contains(new Vector3(1f, 3.5f, 1f)), Is.False);
            Assert.That(zone.Contains(new Vector3(1f, 1f, -0.1f)), Is.False);
        }

        [Test]
        public void BadZonesAreRefused()
        {
            Assert.Throws<ArgumentException>(() => _ = new MapWeaponFreeZone(" ", Vector3.Zero, Vector3.One, WeaponFreeZoneSpec.Default));
            Assert.Throws<ArgumentException>(() => _ = new MapWeaponFreeZone("a", Vector3.One, Vector3.Zero, WeaponFreeZoneSpec.Default));
            Assert.Throws<ArgumentException>(() => _ = new MapWeaponFreeZone("a", new Vector3(float.NaN, 0f, 0f), Vector3.One, WeaponFreeZoneSpec.Default));
            Assert.Throws<ArgumentNullException>(() => _ = new MapWeaponFreeZone("a", Vector3.Zero, Vector3.One, null));
        }

        [Test]
        public void TheListIsCopiedAndIdsAreUnique()
        {
            var zone = new MapWeaponFreeZone("Shop", Vector3.Zero, Vector3.One, WeaponFreeZoneSpec.Default);
            var list = new[] { zone };
            var zones = new NetMapWeaponFreeZones(list);

            list[0] = null;

            Assert.That(zones.Zones[0], Is.SameAs(zone));
            Assert.Throws<ArgumentNullException>(() => _ = new NetMapWeaponFreeZones(null));
            Assert.Throws<ArgumentException>(() => _ = new NetMapWeaponFreeZones(new MapWeaponFreeZone[] { null }));
            Assert.Throws<ArgumentException>(() => _ = new NetMapWeaponFreeZones(new[] { zone, zone }));
            Assert.Throws<ArgumentNullException>(() => NetMapWeaponFreeZones.Write(null));
        }

        [TestCase("{ \"zones\": [ { \"min\": [0, 0, 0], \"max\": [1, 1, 1] } ] }", "zones[0].id")]
        [TestCase("{ \"zones\": [ { \"id\": \"a\", \"max\": [1, 1, 1] } ] }", "zones[0].min")]
        [TestCase("{ \"zones\": [ { \"id\": \"a\", \"min\": [2, 0, 0], \"max\": [1, 1, 1] } ] }", "zones[0].min")]
        [TestCase("{ \"zones\": [ { \"id\": \"\", \"min\": [0, 0, 0], \"max\": [1, 1, 1] } ] }", "zones[0].id")]
        [TestCase("{ \"zones\": [ { \"id\": \"a\", \"min\": [0, 0, 0], \"max\": [1, 1, 1], \"reaction\": \"Shout\" } ] }", "zones[0].reaction")]
        [TestCase("{ \"zones\": [ { \"id\": \"a\", \"min\": [0, 0, 0], \"max\": [1, 1, 1], \"reaction\": \"2\" } ] }", "zones[0].reaction")]
        [TestCase("{ \"zones\": [ { \"id\": \"a\", \"min\": [0, 0, 0], \"max\": [1, 1, 1], \"hostileAfterSeconds\": -1 } ] }", "zones[0].hostileAfterSeconds")]
        [TestCase("{ \"zones\": [ { \"id\": \"a\", \"min\": [0, 0, 0], \"max\": [1, 1, 1], \"size\": 1 } ] }", "size")]
        [TestCase(
            "{ \"zones\": [ { \"id\": \"a\", \"min\": [0, 0, 0], \"max\": [1, 1, 1] }, { \"id\": \"a\", \"min\": [0, 0, 0], \"max\": [1, 1, 1] } ] }",
            "zones[1].id")]
        [TestCase("{ \"areas\": [] }", "areas")]
        public void BadZonesNameTheItem(string text, string item)
        {
            var e = Assert.Throws<ServerDataJsonException>(() => NetMapWeaponFreeZones.Parse(text, "map.weapon-free-zones.json"));
            Assert.That(e.Message, Does.Contain(item));
        }
    }
}

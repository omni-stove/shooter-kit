using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class NetMapDoorsTests
    {
        [Test]
        public void WrittenDoorsReadBackInTheSameOrder()
        {
            var doors = new NetMapDoors(new[]
            {
                new MapDoor(new Vector3(4f, 1.1f, -2f), new Vector3(0f, 0f, 3f), new Vector3(1.2f, 2.2f, 0.1f), MapColliderGroups.Door(0)),
                new MapDoor(new Vector3(-8.5f, 1f, 6f), new Vector3(-1f, 0.5f, 0f), new Vector3(0.9f, 2f, 0.05f), MapColliderGroups.Door(1)),
            });

            var text = NetMapDoors.Write(doors);
            var read = NetMapDoors.Parse(text, "test");

            Assert.That(read.Doors, Has.Count.EqualTo(2));
            Assert.That(read.Doors[0].Position, Is.EqualTo(new Vector3(4f, 1.1f, -2f)));
            Assert.That(read.Doors[0].Facing, Is.EqualTo(Vector3.UnitZ));
            Assert.That(read.Doors[0].PanelSize, Is.EqualTo(new Vector3(1.2f, 2.2f, 0.1f)));
            Assert.That(read.Doors[0].ColliderGroup, Is.EqualTo(-1));
            Assert.That(read.Doors[1].Position, Is.EqualTo(new Vector3(-8.5f, 1f, 6f)));
            Assert.That(read.Doors[1].Facing, Is.EqualTo(-Vector3.UnitX), "a tilted panel faces horizontally");
            Assert.That(read.Doors[1].PanelSize, Is.EqualTo(new Vector3(0.9f, 2f, 0.05f)));
            Assert.That(read.Doors[1].ColliderGroup, Is.EqualTo(-2));
            Assert.That(NetMapDoors.Write(read), Is.EqualTo(text));
        }

        [Test]
        public void AMapWithoutDoorsWritesAnEmptyList()
        {
            var text = NetMapDoors.Write(NetMapDoors.None);
            var read = NetMapDoors.Parse(text, "test");

            Assert.That(read.Doors, Is.Empty);
            Assert.That(NetMapDoors.Parse("{ \"doors\": [] }", "test").Doors, Is.Empty);
            Assert.That(NetMapDoors.Parse("{ }", "test").Doors, Is.Empty);
        }

        [Test]
        public void TwoDoorsCannotShareAColliderGroup()
        {
            var first = new MapDoor(Vector3.Zero, Vector3.UnitZ, Vector3.One, MapColliderGroups.Door(0));
            var second = new MapDoor(Vector3.One, Vector3.UnitZ, Vector3.One, MapColliderGroups.Door(0));

            Assert.Throws<System.ArgumentException>(() => new NetMapDoors(new[] { first, second }));
            Assert.Throws<System.ArgumentException>(() => new NetMapDoors(new MapDoor[] { first, null }));
            Assert.Throws<System.ArgumentNullException>(() => new NetMapDoors(null));
        }

        [Test]
        public void ADoorNeedsADoorGroupAndAPanel()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new MapDoor(Vector3.Zero, Vector3.UnitZ, Vector3.One, MapColliderGroups.Static));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new MapDoor(Vector3.Zero, Vector3.UnitZ, Vector3.One, MapColliderGroups.Piece(0, 0)));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new MapDoor(Vector3.Zero, Vector3.UnitY, Vector3.One, MapColliderGroups.Door(0)));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new MapDoor(Vector3.Zero, Vector3.UnitZ, new Vector3(1f, 2f, 0f), MapColliderGroups.Door(0)));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new MapDoor(new Vector3(float.NaN, 0f, 0f), Vector3.UnitZ, Vector3.One, MapColliderGroups.Door(0)));
        }

        [TestCase("not json", "map.doors.json")]
        [TestCase("{ \"doors\": {} }", "doors")]
        [TestCase("{ \"doors\": [ { \"facing\": [0, 0, 1], \"panelSize\": [1, 2, 0.1], \"colliderGroup\": -1 } ] }", "doors[0].position")]
        [TestCase("{ \"doors\": [ { \"position\": [0, 1, 0], \"facing\": [0, 1, 0], \"panelSize\": [1, 2, 0.1], \"colliderGroup\": -1 } ] }", "doors[0].facing")]
        [TestCase("{ \"doors\": [ { \"position\": [0, 1, 0], \"facing\": [0, 0, 1], \"panelSize\": [1, -2, 0.1], \"colliderGroup\": -1 } ] }", "doors[0].panelSize")]
        [TestCase("{ \"doors\": [ { \"position\": [0, 1, 0], \"facing\": [0, 0, 1], \"panelSize\": [1, 2], \"colliderGroup\": -1 } ] }", "doors[0].panelSize")]
        [TestCase("{ \"doors\": [ { \"position\": [0, 1, 0], \"facing\": [0, 0, 1], \"panelSize\": [1, 2, 0.1] } ] }", "doors[0].colliderGroup")]
        [TestCase("{ \"doors\": [ { \"position\": [0, 1, 0], \"facing\": [0, 0, 1], \"panelSize\": [1, 2, 0.1], \"colliderGroup\": 0 } ] }", "doors[0].colliderGroup")]
        [TestCase("{ \"doors\": [ { \"position\": [0, 1, 0], \"facing\": [0, 0, 1], \"panelSize\": [1, 2, 0.1], \"colliderGroup\": -1.5 } ] }", "doors[0].colliderGroup")]
        [TestCase(
            "{ \"doors\": [ { \"position\": [0, 1, 0], \"facing\": [0, 0, 1], \"panelSize\": [1, 2, 0.1], \"colliderGroup\": -1 },"
            + " { \"position\": [3, 1, 0], \"facing\": [0, 0, 1], \"panelSize\": [1, 2, 0.1], \"colliderGroup\": -1 } ] }",
            "doors[1].colliderGroup")]
        [TestCase("{ \"doors\": [ { \"position\": [0, 1, 0], \"facing\": [0, 0, 1], \"panelSize\": [1, 2, 0.1], \"colliderGroup\": -1, \"hinge\": 1 } ] }", "hinge")]
        public void BadDoorsNameTheItem(string text, string item)
        {
            var e = Assert.Throws<ServerDataJsonException>(() => NetMapDoors.Parse(text, "map.doors.json"));
            Assert.That(e.Message, Does.Contain(item));
        }

        [TestCaseSource(nameof(CommittedMaps))]
        public void EveryCommittedMapHasDoorsWrittenByTheExporter(string collisionPath)
        {
            var path = Path.ChangeExtension(collisionPath, null) + NetMapDoors.Extension;
            Assert.That(File.Exists(path), Is.True, "run make export-server-data and commit " + path);
            var text = File.ReadAllText(path);
            Assert.That(NetMapDoors.Write(NetMapDoors.Parse(text, path)), Is.EqualTo(text));
        }

        private static IEnumerable<string> CommittedMaps()
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "Assets")))
            {
                directory = directory.Parent;
            }

            var maps = directory == null ? null : Path.Combine(directory.FullName, "server-data", "maps");
            return maps != null && Directory.Exists(maps)
                ? Directory.GetFiles(maps, "*.fpsmap").OrderBy(p => p, System.StringComparer.Ordinal)
                : Enumerable.Empty<string>();
        }
    }
}

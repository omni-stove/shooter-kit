using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class MapColliderGroupsTests
    {
        [Test]
        public void StaticBodiesAndPiecesDoNotOverlap()
        {
            Assert.That(MapColliderGroups.Static, Is.EqualTo(WorldCollider.StaticGroup));
            Assert.That(MapColliderGroups.Body(0), Is.EqualTo(1));
            Assert.That(MapColliderGroups.Body(MapColliderGroups.MaxBodies - 1), Is.LessThan(MapColliderGroups.Piece(0, 0)));

            Assert.That(MapColliderGroups.IsBody(MapColliderGroups.Static), Is.False);
            Assert.That(MapColliderGroups.IsPiece(MapColliderGroups.Static), Is.False);
            Assert.That(MapColliderGroups.IsBody(MapColliderGroups.Body(5)), Is.True);
            Assert.That(MapColliderGroups.IsPiece(MapColliderGroups.Body(MapColliderGroups.MaxBodies - 1)), Is.False);
            Assert.That(MapColliderGroups.IsPiece(MapColliderGroups.Piece(0, 0)), Is.True);
            Assert.That(MapColliderGroups.IsBody(MapColliderGroups.Piece(0, 0)), Is.False);
        }

        [Test]
        public void PieceGroupsRoundTripTheirStructureAndPiece()
        {
            var pairs = new[] { (0, 0), (0, 1), (1, 0), (3, 117), (MapColliderGroups.MaxStructures - 1, MapColliderGroups.MaxPiecesPerStructure - 1) };
            foreach (var (structure, piece) in pairs)
            {
                var group = MapColliderGroups.Piece(structure, piece);
                Assert.That(MapColliderGroups.TryGetPiece(group, out var s, out var p), Is.True);
                Assert.That((s, p), Is.EqualTo((structure, piece)));
            }

            Assert.That(MapColliderGroups.Piece(0, 1), Is.Not.EqualTo(MapColliderGroups.Piece(1, 0)));
            Assert.That(MapColliderGroups.TryGetPiece(MapColliderGroups.Body(0), out var none, out var nonePiece), Is.False);
            Assert.That((none, nonePiece), Is.EqualTo((-1, -1)));
        }

        [Test]
        public void RejectsIndicesOutOfRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MapColliderGroups.Body(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => MapColliderGroups.Body(MapColliderGroups.MaxBodies));
            Assert.Throws<ArgumentOutOfRangeException>(() => MapColliderGroups.Piece(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => MapColliderGroups.Piece(MapColliderGroups.MaxStructures, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => MapColliderGroups.Piece(0, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => MapColliderGroups.Piece(0, MapColliderGroups.MaxPiecesPerStructure));
            Assert.Throws<ArgumentOutOfRangeException>(() => MapColliderGroups.Door(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => MapColliderGroups.Door(MapColliderGroups.MaxDoors));
        }

        [Test]
        public void DoorGroupsAreNegativeAndRoundTripTheirDoor()
        {
            Assert.That(MapColliderGroups.Door(0), Is.EqualTo(-1));
            Assert.That(MapColliderGroups.Door(4), Is.EqualTo(-5));
            Assert.That(MapColliderGroups.Door(MapColliderGroups.MaxDoors - 1), Is.EqualTo(-MapColliderGroups.MaxDoors));

            foreach (var door in new[] { 0, 1, 37, MapColliderGroups.MaxDoors - 1 })
            {
                var group = MapColliderGroups.Door(door);
                Assert.That(MapColliderGroups.IsDoor(group), Is.True);
                Assert.That(MapColliderGroups.IsBody(group), Is.False);
                Assert.That(MapColliderGroups.IsPiece(group), Is.False);
                Assert.That(MapColliderGroups.TryGetDoor(group, out var index), Is.True);
                Assert.That(index, Is.EqualTo(door));
            }

            Assert.That(MapColliderGroups.IsDoor(MapColliderGroups.Static), Is.False);
            Assert.That(MapColliderGroups.IsDoor(MapColliderGroups.Body(0)), Is.False);
            Assert.That(MapColliderGroups.IsDoor(MapColliderGroups.Piece(0, 0)), Is.False);
            Assert.That(MapColliderGroups.IsDoor(-MapColliderGroups.MaxDoors - 1), Is.False);
            Assert.That(MapColliderGroups.TryGetDoor(MapColliderGroups.Piece(0, 0), out var none), Is.False);
            Assert.That(none, Is.EqualTo(-1));
        }
    }
}

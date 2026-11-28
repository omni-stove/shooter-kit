using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class PickupKindsTests
    {
        // 列挙 PickupKind だった頃の名前と数の順。マップのファイルは名前で、シーンとプレハブは数で種類を持つ
        private static readonly string[] _oldNames = { "Weapon", "ReserveAmmo", "Health", "Armor", "Tag", "Item" };

        [Test]
        public void TheKeysAreTheOldEnumNamesInTheOldOrder()
        {
            Assert.That(PickupKinds.Names, Is.EqualTo(_oldNames));
        }

        [TestCase(0, "Weapon")]
        [TestCase(2, "Health")]
        [TestCase(5, "Item")]
        public void ANumberSavedByTheOldEnumIsReadAsItsKey(int number, string kind)
        {
            Assert.That(PickupKinds.FromNumber(number), Is.EqualTo(kind));
        }

        [Test]
        public void AnUnknownNumberOrKeyIsRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PickupKinds.FromNumber(6));
            Assert.Throws<ArgumentOutOfRangeException>(() => PickupKinds.FromNumber(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => PickupKinds.Require("health", "kind"));
            Assert.That(PickupKinds.IsKnown(PickupKinds.Armor), Is.True);
            Assert.That(PickupKinds.IsKnown(null), Is.False);
        }
    }
}

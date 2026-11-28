using System;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Protocol.Tests
{
    public sealed class PickupItemKindsTests
    {
        [TestCase(PickupKinds.Weapon, ObjectiveItemKind.WeaponPickup)]
        [TestCase(PickupKinds.ReserveAmmo, ObjectiveItemKind.AmmoPickup)]
        [TestCase(PickupKinds.Health, ObjectiveItemKind.HealthPickup)]
        [TestCase(PickupKinds.Armor, ObjectiveItemKind.ArmorPickup)]
        [TestCase(PickupKinds.Item, ObjectiveItemKind.ItemPickup)]
        public void EachPickupKindHasItsItemKindBothWays(string kind, ObjectiveItemKind itemKind)
        {
            Assert.That(PickupItemKinds.ItemKindOf(kind), Is.EqualTo(itemKind));
            Assert.That(PickupItemKinds.TryGetPickupKind(itemKind, out var back), Is.True);
            Assert.That(back, Is.EqualTo(kind));
            Assert.That(PickupItemKinds.IsPickup(itemKind), Is.True);
            Assert.That(PickupItemKinds.IsCollectable(itemKind), Is.True);
        }

        [Test]
        public void ATagIsNotSentAsAPickup()
        {
            Assert.That(PickupItemKinds.TryGetItemKind(PickupKinds.Tag, out _), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => PickupItemKinds.ItemKindOf(PickupKinds.Tag));
        }

        [TestCase(ObjectiveItemKind.Flag)]
        [TestCase(ObjectiveItemKind.Tag)]
        [TestCase(ObjectiveItemKind.LootBox)]
        [TestCase(ObjectiveItemKind.DeathBox)]
        [TestCase(ObjectiveItemKind.ExtractionPoint)]
        public void ObjectiveItemsAndBoxesAreNotPickups(ObjectiveItemKind itemKind)
        {
            Assert.That(PickupItemKinds.IsPickup(itemKind), Is.False);
            Assert.That(PickupItemKinds.IsCollectable(itemKind), Is.False);
        }

        [Test]
        public void AnAmmoCrateIsCollectableButHoldsNoPickupKind()
        {
            Assert.That(PickupItemKinds.IsCollectable(ObjectiveItemKind.AmmoCrate), Is.True);
            Assert.That(PickupItemKinds.IsPickup(ObjectiveItemKind.AmmoCrate), Is.False);
            Assert.That(PickupItemKinds.TryGetPickupKind(ObjectiveItemKind.AmmoCrate, out _), Is.False);
        }
    }
}

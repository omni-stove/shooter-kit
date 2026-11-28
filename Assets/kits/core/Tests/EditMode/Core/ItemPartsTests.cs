using System;
using System.Linq;
using NUnit.Framework;

namespace GameKit.Core.Tests
{
    public sealed class ItemPartsTests
    {
        private const string _swordId = "1a2b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c5d";
        private const string _gemId = "2b3c4d5e-6f7a-4b8c-9d0e-1f2a3b4c5d6e";
        private const string _potionId = "3c4d5e6f-7a8b-4c9d-8e1f-2a3b4c5d6e7f";
        private const string _missingId = "99999999-9999-4999-8999-999999999999";
        private static readonly string[] _swordOnly = { _swordId };
        private static readonly string[] _swordHeld = { "sword" };

        private sealed class HandPart : IHeldItemPart
        {
            public HandPart(string heldId)
            {
                HeldId = heldId;
            }

            public string HeldId { get; }

            public void Check(ItemSpec item)
            {
                if (item.MaxStack != 1)
                {
                    throw new ArgumentException("a hand item must have maxStack 1", nameof(item));
                }
            }

            public void CheckIn(ItemSpec item, ItemCatalog catalog)
            {
            }
        }

        private sealed class OtherHandPart : IHeldItemPart
        {
            public string HeldId => "other";

            public void Check(ItemSpec item)
            {
            }

            public void CheckIn(ItemSpec item, ItemCatalog catalog)
            {
            }
        }

        private sealed class NeedsPart : IItemPart
        {
            public NeedsPart(string itemId)
            {
                ItemId = itemId;
            }

            public string ItemId { get; }

            public void Check(ItemSpec item)
            {
            }

            public void CheckIn(ItemSpec item, ItemCatalog catalog)
            {
                if (!catalog.TryFind(ItemId, out _))
                {
                    throw new ArgumentException("item " + ItemId + " is not one of the items", nameof(catalog));
                }
            }
        }

        // 2x1・5 kg の剣を手に持つ
        private static ItemSpec Sword() => new ItemSpec(_swordId, "item.sword", weight: 5f, width: 2, parts: new IItemPart[] { new HandPart("sword") });

        // 1x1・10 個まで重なる・1 個 1 kg
        private static ItemSpec Gem() => new ItemSpec(_gemId, "item.gem", maxStack: 10, weight: 1f);

        private static Inventory Make(InventoryRule rule, params ItemSpec[] items)
        {
            return new Inventory(new ItemCatalog(items, rule));
        }

        [Test]
        public void AnItemWithoutPartsIsNotHeld()
        {
            var gem = Gem();

            Assert.That(gem.Parts, Is.Empty);
            Assert.That(gem.IsHeld, Is.False);
            Assert.That(gem.HeldId, Is.Null);
            Assert.That(gem.PartOf<HandPart>(), Is.Null);
        }

        [Test]
        public void AHeldPartGivesTheItemItsHeldId()
        {
            var sword = Sword();

            Assert.That(sword.IsHeld, Is.True);
            Assert.That(sword.HeldId, Is.EqualTo("sword"));
            Assert.That(sword.TryGetPart<HandPart>(out var part), Is.True);
            Assert.That(part.HeldId, Is.EqualTo("sword"));
        }

        [Test]
        public void TwoPartsOfOneKindAreRejected()
        {
            Assert.Throws<ArgumentException>(() => new ItemSpec(_swordId, parts: new IItemPart[] { new HandPart("a"), new HandPart("b") }));
        }

        [Test]
        public void AnItemIsHeldAsOnlyOneThing()
        {
            Assert.Throws<ArgumentException>(() => new ItemSpec(_swordId, parts: new IItemPart[] { new HandPart("a"), new OtherHandPart() }));
        }

        [Test]
        public void AHeldPartWithoutAnIdIsRejected()
        {
            Assert.Throws<ArgumentException>(() => new ItemSpec(_swordId, parts: new IItemPart[] { new HandPart(null) }));
        }

        [Test]
        public void APartChecksTheItemItIsOn()
        {
            var error = Assert.Throws<ArgumentException>(() => new ItemSpec(_swordId, maxStack: 2, parts: new IItemPart[] { new HandPart("sword") }));

            Assert.That(error.Message, Does.StartWith("a hand item must have maxStack 1"));
        }

        [Test]
        public void TheCatalogFindsAnItemByWhatIsHeld()
        {
            var catalog = new ItemCatalog(new[] { Sword(), Gem() }, InventoryRule.Default);

            Assert.That(catalog.TryFindHeld("sword", out var sword), Is.True);
            Assert.That(sword.Id, Is.EqualTo(_swordId));
            Assert.That(catalog.TryFindHeld("axe", out _), Is.False);
            Assert.That(catalog.TryFindHeld(null, out _), Is.False);
        }

        [Test]
        public void TheCatalogRejectsTwoItemsHeldAsTheSameThing()
        {
            var other = new ItemSpec(_gemId, parts: new IItemPart[] { new HandPart("sword") });

            Assert.Throws<ArgumentException>(() => new ItemCatalog(new[] { Sword(), other }, InventoryRule.Default));
        }

        [Test]
        public void TheCatalogRejectsAHeldItemAmongTheStartingItems()
        {
            var error = Assert.Throws<ArgumentException>(() => new ItemCatalog(new[] { Sword() }, InventoryRule.Default, new[] { new ItemCount(_swordId, 1) }));

            Assert.That(error.ParamName, Is.EqualTo("starting"));
        }

        [Test]
        public void ThePartsCheckTheOtherItemsOfTheCatalog()
        {
            var potion = new ItemSpec(_potionId, parts: new IItemPart[] { new NeedsPart(_gemId) });
            var broken = new ItemSpec(_potionId, parts: new IItemPart[] { new NeedsPart(_missingId) });

            Assert.That(() => new ItemCatalog(new[] { potion, Gem() }, InventoryRule.Default), Throws.Nothing);
            Assert.Throws<ArgumentException>(() => new ItemCatalog(new[] { broken, Gem() }, InventoryRule.Default));
        }

        [Test]
        public void AHeldItemIsNotAddedAsAnItem()
        {
            var inventory = Make(InventoryRule.Default, Sword(), Gem());

            Assert.That(inventory.Add(_swordId, 1), Is.EqualTo(0));
            Assert.That(inventory.RoomFor(_swordId), Is.EqualTo(0));
            Assert.That(inventory.Stacks, Is.Empty);
        }

        [Test]
        public void HoldPlacesAStackForWhatIsHeldWithTheGivenWeight()
        {
            var inventory = Make(InventoryRule.Default, Sword(), Gem());
            var changes = 0;
            inventory.Changed += () => changes++;

            var missing = inventory.Hold(new[] { new HeldItem("sword", 7f, new[] { _gemId }) });

            Assert.That(missing, Is.Empty);
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(inventory.CountOf(_swordId), Is.EqualTo(1));
            Assert.That(inventory.IsHolding("sword"), Is.True);
            Assert.That(inventory.HeldWeight, Is.EqualTo(7f));
            Assert.That(inventory.TotalWeight, Is.EqualTo(7f));
            Assert.That(inventory.AttachmentsOf("sword"), Is.EqualTo(new[] { _gemId }));
            Assert.That(inventory.HeldIdOf(inventory.Stacks[0].Handle), Is.EqualTo("sword"));
        }

        [Test]
        public void HoldTakesAwayTheStackOfWhatIsNoLongerHeld()
        {
            var inventory = Make(InventoryRule.Default, Sword(), Gem());
            inventory.Hold(new[] { new HeldItem("sword", 5f) });

            inventory.Hold(Array.Empty<HeldItem>());

            Assert.That(inventory.Stacks, Is.Empty);
            Assert.That(inventory.IsHolding("sword"), Is.False);
        }

        [Test]
        public void HoldReturnsWhatHadNoRoom()
        {
            var inventory = Make(new InventoryRule(maxWeight: 4f), Sword(), Gem());

            var missing = inventory.Hold(new[] { new HeldItem("sword", 5f) });

            Assert.That(missing, Is.EqualTo(_swordHeld));
            Assert.That(inventory.Stacks, Is.Empty);
        }

        [Test]
        public void SomethingNotInTheCatalogCanAlwaysBeHeld()
        {
            var inventory = Make(new InventoryRule(maxWeight: 4f), Sword(), Gem());

            Assert.That(inventory.CanHold("axe", 100f), Is.True);
            Assert.That(inventory.CanHold("sword", 5f), Is.False);
            Assert.That(inventory.CanHold("sword", 4f), Is.True);
        }

        [Test]
        public void SetHeldWeightChangesOnlyWhatIsHeld()
        {
            var inventory = Make(InventoryRule.Default, Sword(), Gem());
            inventory.Hold(new[] { new HeldItem("sword", 5f) });

            inventory.SetHeldWeight("sword", 6f);
            inventory.SetHeldWeight("axe", 9f);

            Assert.That(inventory.TryGetHeldWeight("sword", out var weight), Is.True);
            Assert.That(weight, Is.EqualTo(6f));
            Assert.That(inventory.TryGetHeldWeight("axe", out _), Is.False);
        }

        [Test]
        public void FillPlacesWhatIsHeldBeforeTheStartingItems()
        {
            var catalog = new ItemCatalog(new[] { Sword(), Gem() }, new InventoryRule(gridWidth: 2, gridHeight: 1), new[] { new ItemCount(_gemId, 3) });
            var inventory = new Inventory(catalog);
            inventory.Hold(new[] { new HeldItem("sword", 5f) });

            inventory.Fill();

            Assert.That(inventory.Stacks.Select(s => s.ItemId), Is.EqualTo(_swordOnly));
        }
    }
}

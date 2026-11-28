using System;
using GameKit.Core;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class ItemSpecsTests
    {
        private const string _herbId = "0d3c6f1a-5b2e-4c7d-8a9f-1e2b3c4d5e6f";
        private const string _ammoId = "7a8b9c0d-1e2f-4a3b-9c4d-5e6f7a8b9c0d";
        private const string _rifleItemId = "4b6d8f0a-2c4e-4a6b-8c0d-2e4f6a8b0c2d";
        private const string _scopeId = "5c7e9a1b-3d5f-4b7c-9d1e-3f5a7b9c1d3e";
        private const string _rifleId = "6d8f0a2c-4e6a-4c8d-9e0f-4a6b8c0d2e4f";
        private static readonly string[] _opticSlot = { "optic" };

        private sealed class OtherHeldPart : IHeldItemPart
        {
            public string HeldId => _rifleId;

            public void Check(ItemSpec item)
            {
            }

            public void CheckIn(ItemSpec item, ItemCatalog catalog)
            {
            }
        }

        [Test]
        public void APlainItemGetsNoParts()
        {
            var ammo = ItemSpecs.Create(_ammoId, maxStack: 30);

            Assert.That(ammo.Parts, Is.Empty);
            Assert.That(ammo.IsWeapon(), Is.False);
            Assert.That(ammo.IsUsable(), Is.False);
            Assert.That(ammo.UseHealth(), Is.EqualTo(ItemSpecs.DefaultUseHealth));
            Assert.That(ammo.AttachmentSlots(), Is.Empty);
        }

        [Test]
        public void AWeaponItemIsHeldAsItsWeapon()
        {
            var rifle = ItemSpecs.Create(_rifleItemId, weaponId: _rifleId, ammoItemId: _ammoId, attachmentSlots: _opticSlot);

            Assert.That(rifle.IsHeld, Is.True);
            Assert.That(rifle.HeldId, Is.EqualTo(_rifleId));
            Assert.That(rifle.PartOf<WeaponItemPart>().AmmoItemId, Is.EqualTo(_ammoId));
            Assert.That(rifle.WeaponId(), Is.EqualTo(_rifleId));
            Assert.That(rifle.AmmoItemId(), Is.EqualTo(_ammoId));
            Assert.That(rifle.HasSlot("optic"), Is.True);
            Assert.That(rifle.HasSlot("muzzle"), Is.False);
        }

        [Test]
        public void HealingAndAttachingArePartsToo()
        {
            var herb = ItemSpecs.Create(_herbId, useHealth: 25, useArmor: 5);
            var scope = ItemSpecs.Create(_scopeId, attachment: new AttachmentSpec("optic", new[] { _rifleId }));

            Assert.That(herb.PartOf<ItemUsePart>().Health, Is.EqualTo(25));
            Assert.That(herb.UseArmor(), Is.EqualTo(5));
            Assert.That(herb.IsUsable(), Is.True);
            Assert.That(scope.IsAttachment(), Is.True);
            Assert.That(scope.Attachment().Slot, Is.EqualTo("optic"));
            Assert.That(scope.IsHeld, Is.False);
        }

        // InventoryJson は ParamName を項目名として読んだ項目を指すので、部品に分けても同じ名前で断る
        [TestCase(2, 0, null, null, "maxStack")]
        [TestCase(1, 10, null, null, "weaponId")]
        [TestCase(1, 0, _rifleItemId, null, "ammoItemId")]
        [TestCase(1, 0, null, "optic", "attachment")]
        public void AWeaponItemIsRefusedByTheItemOfTheJson(int maxStack, int useHealth, string ammoItemId, string attachmentSlot, string item)
        {
            var attachment = attachmentSlot == null ? null : new AttachmentSpec(attachmentSlot);

            var error = Assert.Throws<ArgumentException>(
                () => ItemSpecs.Create(_rifleItemId, maxStack: maxStack, useHealth: useHealth, weaponId: _rifleId, ammoItemId: ammoItemId, attachment: attachment));

            Assert.That(error.ParamName, Is.EqualTo(item));
        }

        [Test]
        public void SlotsWithoutAWeaponAreRefused()
        {
            var error = Assert.Throws<ArgumentException>(() => ItemSpecs.Create(_scopeId, attachmentSlots: _opticSlot));

            Assert.That(error.ParamName, Is.EqualTo("attachmentSlots"));
        }

        [Test]
        public void TheCatalogFindsOnlyWeaponItemsAsWeapons()
        {
            var other = new ItemSpec(_rifleItemId, parts: new IItemPart[] { new OtherHeldPart() });
            var catalog = new ItemCatalog(new[] { other }, InventoryRule.Default);

            Assert.That(catalog.TryFindHeld(_rifleId, out _), Is.True);
            Assert.That(catalog.TryFindWeapon(_rifleId, out _), Is.False);
        }

        [Test]
        public void TheAmmoOfAWeaponMustBeAnItemThatIsNotAWeapon()
        {
            var rifle = ItemSpecs.Create(_rifleItemId, weaponId: _rifleId, ammoItemId: _ammoId);
            var ammoAsWeapon = ItemSpecs.Create(_ammoId, weaponId: "pistol");

            var missing = Assert.Throws<ArgumentException>(() => new ItemCatalog(new[] { rifle }, InventoryRule.Default));
            var weapon = Assert.Throws<ArgumentException>(() => new ItemCatalog(new[] { rifle, ammoAsWeapon }, InventoryRule.Default));

            Assert.That(missing.ParamName, Is.EqualTo("items"));
            Assert.That(weapon.Message, Does.Contain("is a weapon"));
        }
    }
}

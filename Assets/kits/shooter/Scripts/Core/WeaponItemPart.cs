using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GameKit.Core;

namespace Shooter.Core
{
    // 持ち物の武器の山は持ち武器から作るので、手に持つ物の部品として武器の Id を持ち物に渡す。
    public sealed class WeaponItemPart : IHeldItemPart
    {
        public WeaponItemPart(string weaponId, string ammoItemId = null, IEnumerable<string> attachmentSlots = null)
        {
            if (string.IsNullOrWhiteSpace(weaponId))
            {
                throw new ArgumentException("weaponId must not be empty", nameof(weaponId));
            }

            WeaponId = weaponId.Trim();
            AmmoItemId = string.IsNullOrWhiteSpace(ammoItemId) ? null : SpecGuard.LowercaseUuid(ammoItemId, nameof(ammoItemId));
            AttachmentSlots = Slots(attachmentSlots);
        }

        public string WeaponId { get; }

        public string AmmoItemId { get; }

        public IReadOnlyList<string> AttachmentSlots { get; }

        string IHeldItemPart.HeldId => WeaponId;

        public bool HasSlot(string slot)
        {
            foreach (var candidate in AttachmentSlots)
            {
                if (string.Equals(candidate, slot, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public void Check(ItemSpec item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            // 武器の残弾と弾薬の種類は 1 本ごとに持つので、2 本を 1 つの山に重ねられない
            if (item.MaxStack != 1)
            {
                throw Rejected("maxStack", "a weapon item must have maxStack 1");
            }

            if (item.IsUsable())
            {
                throw Rejected("weaponId", "a weapon item cannot heal or add armor when used (using it equips the weapon)");
            }

            if (AmmoItemId != null && string.Equals(AmmoItemId, item.Id, StringComparison.Ordinal))
            {
                throw Rejected("ammoItemId", "a weapon item cannot be its own ammo");
            }
        }

        public void CheckIn(ItemSpec item, ItemCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (AmmoItemId == null)
            {
                return;
            }

            if (!catalog.TryFind(AmmoItemId, out var ammo))
            {
                throw Rejected("items", "ammo item " + AmmoItemId + " of weapon item " + item.Id + " is not one of the items");
            }

            if (ammo.IsWeapon())
            {
                throw Rejected("items", "ammo item " + AmmoItemId + " of weapon item " + item.Id + " is a weapon");
            }
        }

        private static ReadOnlyCollection<string> Slots(IEnumerable<string> attachmentSlots)
        {
            var list = new List<string>();
            if (attachmentSlots == null)
            {
                return list.AsReadOnly();
            }

            foreach (var slot in attachmentSlots)
            {
                var name = AttachmentSpec.SlotName(slot, nameof(attachmentSlots));
                if (list.Contains(name))
                {
                    throw new ArgumentException("attachment slot '" + name + "' is listed twice", nameof(attachmentSlots));
                }

                list.Add(name);
            }

            return list.AsReadOnly();
        }

        // 断る引数名を JSON の項目名 (maxStack・weaponId など) にするのは、InventoryJson がそれで読んだ項目を指して断るため
        private static ArgumentException Rejected(string paramName, string message)
        {
            return new ArgumentException(message, paramName);
        }
    }
}

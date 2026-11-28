using System;
using System.Collections.Generic;
using GameKit.Core;

namespace Shooter.Core
{
    public static class ItemCatalogWeapons
    {
        // 手に持つ物はシューターでは武器だけだが、ほかのキットの部品で持つ物と取り違えないよう武器の部品まで確かめる
        public static bool TryFindWeapon(this ItemCatalog catalog, string weaponId, out ItemSpec item)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            item = catalog.TryFindHeld(weaponId, out var held) && held.IsWeapon() ? held : null;
            return item != null;
        }

        public static bool TryFindAmmo(this ItemCatalog catalog, string weaponId, out ItemSpec ammo)
        {
            ammo = null;
            return TryFindWeapon(catalog, weaponId, out var weapon) && weapon.AmmoItemId() != null && catalog.TryFind(weapon.AmmoItemId(), out ammo);
        }

        public static bool TryFindAttachment(this ItemCatalog catalog, string itemId, out ItemSpec attachment)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            attachment = catalog.TryFind(itemId, out var item) && item.IsAttachment() ? item : null;
            return attachment != null;
        }

        public static IReadOnlyList<string> SlotsOf(this ItemCatalog catalog, string weaponId)
        {
            return TryFindWeapon(catalog, weaponId, out var weapon) ? weapon.AttachmentSlots() : Array.Empty<string>();
        }

        public static bool CanAttach(this ItemCatalog catalog, string weaponId, string attachmentItemId)
        {
            return TryFindAttachment(catalog, attachmentItemId, out var attachment)
                && TryFindWeapon(catalog, weaponId, out var weapon)
                && weapon.HasSlot(attachment.Attachment().Slot)
                && attachment.Attachment().Fits(weaponId);
        }

        // サーバーから届いた表は一部のアイテムしか持たないので、作るときではなく読むときに確かめる
        public static void CheckAttachments(this ItemCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            foreach (var item in catalog.Items)
            {
                var attachment = item.Attachment();
                if (attachment == null)
                {
                    continue;
                }

                foreach (var weaponId in attachment.WeaponIds)
                {
                    if (!TryFindWeapon(catalog, weaponId, out var weapon))
                    {
                        throw Rejected("items", "attachment item " + item.Id + " fits weapon " + weaponId + ", which is not one of the items");
                    }

                    if (!weapon.HasSlot(attachment.Slot))
                    {
                        throw Rejected(
                            "items", "attachment item " + item.Id + " goes on slot '" + attachment.Slot + "', which weapon item " + weapon.Id + " does not have");
                    }
                }
            }
        }

        // 断る引数名を JSON の項目名 (maxStack・weaponId など) にするのは、InventoryJson がそれで読んだ項目を指して断るため
        private static ArgumentException Rejected(string paramName, string message)
        {
            return new ArgumentException(message, paramName);
        }
    }
}

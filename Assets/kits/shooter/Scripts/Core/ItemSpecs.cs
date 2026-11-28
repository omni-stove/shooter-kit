using System;
using System.Collections.Generic;
using GameKit.Core;

namespace Shooter.Core
{
    // シューターのアイテム (武器・弾薬・アタッチメント・回復) を GameKit.Core の ItemSpec に部品として載せる。
    public static class ItemSpecs
    {
        public const int DefaultUseHealth = 0;
        public const int DefaultUseArmor = 0;

        private static readonly IReadOnlyList<string> _noSlots = Array.AsReadOnly(Array.Empty<string>());

        public static ItemSpec Create(
            string id,
            string nameKey = null,
            string iconId = null,
            int maxStack = ItemSpec.DefaultMaxStack,
            int maxCarried = ItemSpec.DefaultMaxCarried,
            float weight = ItemSpec.DefaultWeight,
            int width = ItemSpec.DefaultWidth,
            int height = ItemSpec.DefaultHeight,
            int useHealth = DefaultUseHealth,
            int useArmor = DefaultUseArmor,
            string weaponId = null,
            string ammoItemId = null,
            AttachmentSpec attachment = null,
            IEnumerable<string> attachmentSlots = null)
        {
            var parts = new List<IItemPart>();
            if (!string.IsNullOrWhiteSpace(weaponId))
            {
                parts.Add(new WeaponItemPart(weaponId, ammoItemId, attachmentSlots));
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(ammoItemId))
                {
                    throw new ArgumentException("ammoItemId needs a weaponId", nameof(ammoItemId));
                }

                if (HasAny(attachmentSlots))
                {
                    throw new ArgumentException("attachmentSlots needs a weaponId", nameof(attachmentSlots));
                }
            }

            var use = new ItemUsePart(useHealth, useArmor);
            if (use.IsUsable)
            {
                parts.Add(use);
            }

            if (attachment != null)
            {
                parts.Add(attachment);
            }

            return new ItemSpec(id, nameKey, iconId, maxStack, maxCarried, weight, width, height, parts);
        }

        public static string WeaponId(this ItemSpec item) => Need(item).PartOf<WeaponItemPart>()?.WeaponId;

        public static bool IsWeapon(this ItemSpec item) => Need(item).PartOf<WeaponItemPart>() != null;

        public static string AmmoItemId(this ItemSpec item) => Need(item).PartOf<WeaponItemPart>()?.AmmoItemId;

        public static IReadOnlyList<string> AttachmentSlots(this ItemSpec item) => Need(item).PartOf<WeaponItemPart>()?.AttachmentSlots ?? _noSlots;

        public static bool HasSlot(this ItemSpec item, string slot) => Need(item).PartOf<WeaponItemPart>()?.HasSlot(slot) ?? false;

        public static AttachmentSpec Attachment(this ItemSpec item) => Need(item).PartOf<AttachmentSpec>();

        public static bool IsAttachment(this ItemSpec item) => Need(item).PartOf<AttachmentSpec>() != null;

        public static int UseHealth(this ItemSpec item) => Need(item).PartOf<ItemUsePart>()?.Health ?? DefaultUseHealth;

        public static int UseArmor(this ItemSpec item) => Need(item).PartOf<ItemUsePart>()?.Armor ?? DefaultUseArmor;

        public static bool IsUsable(this ItemSpec item) => Need(item).PartOf<ItemUsePart>()?.IsUsable ?? false;

        private static ItemSpec Need(ItemSpec item)
        {
            return item ?? throw new ArgumentNullException(nameof(item));
        }

        private static bool HasAny(IEnumerable<string> values)
        {
            if (values == null)
            {
                return false;
            }

            foreach (var unused in values)
            {
                return true;
            }

            return false;
        }
    }
}

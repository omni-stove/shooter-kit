using System;
using GameKit.Core;

namespace Shooter.Core
{
    // 使うと体力と防具が戻るアイテムの部品。体力と防具の戻り方はシューターの決まりなので、持ち物の core の ItemSpec には持たせない。
    public sealed class ItemUsePart : IItemPart
    {
        public ItemUsePart(int health, int armor)
        {
            Health = SpecGuard.NonNegative(health, "useHealth");
            Armor = SpecGuard.NonNegative(armor, "useArmor");
        }

        public int Health { get; }

        public int Armor { get; }

        public bool IsUsable => Health > 0 || Armor > 0;

        public void Check(ItemSpec item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }
        }

        public void CheckIn(ItemSpec item, ItemCatalog catalog)
        {
        }
    }
}

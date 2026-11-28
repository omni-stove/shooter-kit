using System;
using System.Collections.Generic;
using GameKit.Core;

namespace Shooter.Core
{
    // キーをこれまでの列挙 (BuyItemKind) の名前と同じにするのは、economy.json などに書いた "Weapon"・"Throwable"・"Armor" をそのまま読むため。
    public static class BuyItemKinds
    {
        public const string Weapon = "Weapon";
        public const string Throwable = "Throwable";
        public const string Armor = "Armor";

        public static IReadOnlyList<string> Names { get; } = Array.AsReadOnly(new[] { Weapon, Throwable, Armor });

        public static bool IsKnown(string kind)
        {
            foreach (var name in Names)
            {
                if (string.Equals(name, kind, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        // 武器は枠を取り、防具だけが量を持つ。シューターの品の決まりなので、GameKit.Core の BuyItem ではなくここで確かめる。
        public static BuyItem Create(string id, string kind, int price, int slot = 0, int amount = 0, string nameKey = "")
        {
            if (!IsKnown(kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "kind must be one of " + string.Join(", ", Names));
            }

            if (kind != Weapon && slot != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slot), slot, "slot is only for weapons; leave it 0 for " + kind);
            }

            if (kind == Armor)
            {
                SpecGuard.AtLeast(amount, 1, nameof(amount));
            }
            else if (amount != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "amount is only for armor; leave it 0 for " + kind);
            }

            return new BuyItem(id, kind, price, kind == Weapon, slot, amount, nameKey);
        }
    }
}

using System;
using System.Collections.Generic;
using GameKit.Core;

namespace Shooter.Core
{
    public static class InventoryWeapons
    {
        public static float WeaponWeight(this Inventory inventory)
        {
            return Need(inventory).HeldWeight;
        }

        public static bool TryGetWeaponWeight(this Inventory inventory, string weaponId, out float weight)
        {
            return Need(inventory).TryGetHeldWeight(weaponId, out weight);
        }

        public static void SetWeaponWeight(this Inventory inventory, string weaponId, float weight)
        {
            Need(inventory).SetHeldWeight(weaponId, weight);
        }

        public static string WeaponIdOf(this Inventory inventory, int handle)
        {
            return Need(inventory).HeldIdOf(handle);
        }

        public static bool HoldsWeapon(this Inventory inventory, string weaponId)
        {
            return Need(inventory).IsHolding(weaponId);
        }

        public static bool CanHoldWeapon(this Inventory inventory, string weaponId, float weight, string replacedWeaponId = null)
        {
            return Need(inventory).CanHold(weaponId, weight, replacedWeaponId);
        }

        public static IReadOnlyList<string> HoldWeapons(this Inventory inventory, IReadOnlyList<WeaponSpec> held)
        {
            return HoldWeapons(inventory, held, null);
        }

        public static IReadOnlyList<string> HoldWeapons(this Inventory inventory, IReadOnlyList<WeaponSpec> held, Func<string, IReadOnlyList<string>> attachmentsOf)
        {
            if (held == null)
            {
                throw new ArgumentNullException(nameof(held));
            }

            var items = new List<HeldItem>(held.Count);
            foreach (var spec in held)
            {
                if (spec != null)
                {
                    items.Add(new HeldItem(spec.Id, spec.Weight, attachmentsOf?.Invoke(spec.Id)));
                }
            }

            return Need(inventory).Hold(items);
        }

        public static IAmmoPool PoolFor(Inventory inventory, WeaponSpec spec)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            return inventory.Catalog.TryFindAmmo(spec.Id, out var ammo) ? new InventoryAmmoPool(inventory, ammo.Id) : null;
        }

        // 足りない分だけ足すのは、何度呼んでも (出現と試合の開始が重なっても) 増え続けないようにするため。
        // 同じ弾薬を使う武器が何本あっても一番多い武器の分までにするのは、弾薬は武器ではなく持ち物にあり、セーブから戻した武器の
        // 予備弾 (どれも同じ持ち物の数) を足し合わせて増やさないため
        public static int Restock(Inventory inventory, IEnumerable<(string WeaponId, int StartingReserveAmmo)> weapons)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            if (weapons == null)
            {
                throw new ArgumentNullException(nameof(weapons));
            }

            var wanted = new Dictionary<string, long>(StringComparer.Ordinal);
            var order = new List<string>();
            foreach (var (weaponId, startingReserveAmmo) in weapons)
            {
                if (startingReserveAmmo <= 0 || !inventory.Catalog.TryFindAmmo(weaponId, out var ammo))
                {
                    continue;
                }

                if (!wanted.TryGetValue(ammo.Id, out var total))
                {
                    order.Add(ammo.Id);
                }

                wanted[ammo.Id] = Math.Max(total, startingReserveAmmo);
            }

            var added = 0;
            foreach (var ammoId in order)
            {
                var missing = wanted[ammoId] - inventory.CountOf(ammoId);
                if (missing > 0)
                {
                    added += inventory.Add(ammoId, (int)Math.Min(missing, int.MaxValue));
                }
            }

            return added;
        }

        private static Inventory Need(Inventory inventory)
        {
            return inventory ?? throw new ArgumentNullException(nameof(inventory));
        }
    }
}

using System;
using System.Collections.Generic;
using GameKit.Core;

namespace Shooter.Core
{
    // シューターでは枠を取る品は武器だけなので、財布の枠を武器の名前で読めるようにする。
    public static class EconomyWeapons
    {
        public static string WeaponIn(this Economy economy, int playerId, int slot)
        {
            return Need(economy).ItemInSlot(playerId, slot);
        }

        public static IReadOnlyList<KeyValuePair<int, string>> WeaponsOf(this Economy economy, int playerId)
        {
            return Need(economy).SlotItemsOf(playerId);
        }

        public static bool LoseWeapons(this Economy economy, int playerId)
        {
            return Need(economy).LoseSlotItems(playerId);
        }

        private static Economy Need(Economy economy)
        {
            return economy ?? throw new ArgumentNullException(nameof(economy));
        }
    }
}

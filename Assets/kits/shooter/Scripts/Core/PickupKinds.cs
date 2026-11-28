using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    // キーをこれまでの列挙 (PickupKind) の名前と同じにするのは、マップのファイルに書いた "Weapon"・"Health" などをそのまま読むため。
    public static class PickupKinds
    {
        public const string Weapon = "Weapon";
        public const string ReserveAmmo = "ReserveAmmo";
        public const string Health = "Health";
        public const string Armor = "Armor";
        public const string Tag = "Tag";
        public const string Item = "Item";

        // 並びは列挙だった頃の数の順。シーンやプレハブに数で保存された種類 (PickupContents) をこの順で読み戻す。
        public static IReadOnlyList<string> Names { get; } = Array.AsReadOnly(new[] { Weapon, ReserveAmmo, Health, Armor, Tag, Item });

        public static bool IsKnown(string kind)
        {
            return IndexOf(kind) >= 0;
        }

        public static string FromNumber(int number)
        {
            if (number < 0 || number >= Names.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(number), number, "number is not a pickup kind saved as a number");
            }

            return Names[number];
        }

        public static string Require(string kind, string paramName)
        {
            if (!IsKnown(kind))
            {
                throw new ArgumentOutOfRangeException(paramName, kind, paramName + " must be one of " + string.Join(", ", Names));
            }

            return kind;
        }

        private static int IndexOf(string kind)
        {
            for (var i = 0; i < Names.Count; i++)
            {
                if (string.Equals(Names[i], kind, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}

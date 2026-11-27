using System;
using System.Collections.Generic;
using GameKit.Core;

namespace Shooter.Core
{
    public static class GamepadLayoutJson
    {
        public const string ResourcePath = "Tuning/gamepad-layouts";

        public const string LayoutsItem = "layouts";
        public const string SwapsItem = "swaps";

        public const string IdItem = "id";
        public const string NameKeyItem = "nameKey";
        public const string BindingsItem = "bindings";
        public const string PairsItem = "pairs";

        public const string ActionItem = "action";
        public const string PathItem = "path";

        public const string AItem = "a";
        public const string BItem = "b";

        private const string _kind = "gamepad layouts JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[] { LayoutsItem, SwapsItem };

        // 項目名はコンストラクターの引数名にそろえ、値の検査で断られた項目を ParamName から示せるようにする。
        public static IReadOnlyList<string> LayoutItemNames { get; } = new[] { IdItem, NameKeyItem, BindingsItem };

        public static IReadOnlyList<string> SwapItemNames { get; } = new[] { IdItem, NameKeyItem, PairsItem };

        public static IReadOnlyList<string> BindingItemNames { get; } = new[] { ActionItem, PathItem };

        public static IReadOnlyList<string> PairItemNames { get; } = new[] { AItem, BItem };

        public static GamepadLayoutSpec Parse(string text, string sourceName)
        {
            return SpecJsonReader.Read(text, sourceName, _kind, ItemNames, fields =>
            {
                var layouts = SpecJsonReader.ReadList(fields, LayoutsItem, LayoutItemNames, ReadLayout);
                var swaps = SpecJsonReader.ReadList(fields, SwapsItem, SwapItemNames, ReadSwap);
                return SpecJsonReader.Build(fields, ItemNames, () => new GamepadLayoutSpec(
                    (IReadOnlyList<GamepadLayout>)layouts ?? GamepadLayoutSpec.Default.Layouts,
                    (IReadOnlyList<GamepadSwap>)swaps ?? GamepadLayoutSpec.Default.Swaps));
            });
        }

        private static GamepadLayout ReadLayout(ServerDataJson.Fields entry)
        {
            var bindings = SpecJsonReader.ReadList(entry, BindingsItem, BindingItemNames, ReadBinding);
            return new GamepadLayout(
                SpecJsonReader.RequireText(entry, IdItem),
                SpecJsonReader.RequireText(entry, NameKeyItem),
                (IReadOnlyList<GamepadLayoutBinding>)bindings ?? Array.Empty<GamepadLayoutBinding>());
        }

        private static GamepadSwap ReadSwap(ServerDataJson.Fields entry)
        {
            var pairs = SpecJsonReader.ReadList(entry, PairsItem, PairItemNames, ReadPair);
            return new GamepadSwap(
                SpecJsonReader.RequireText(entry, IdItem),
                SpecJsonReader.RequireText(entry, NameKeyItem),
                (IReadOnlyList<GamepadSwapPair>)pairs ?? Array.Empty<GamepadSwapPair>());
        }

        private static GamepadLayoutBinding ReadBinding(ServerDataJson.Fields entry)
        {
            return new GamepadLayoutBinding(SpecJsonReader.RequireText(entry, ActionItem), SpecJsonReader.RequireText(entry, PathItem));
        }

        private static GamepadSwapPair ReadPair(ServerDataJson.Fields entry)
        {
            return new GamepadSwapPair(SpecJsonReader.RequireText(entry, AItem), SpecJsonReader.RequireText(entry, BItem));
        }
    }
}

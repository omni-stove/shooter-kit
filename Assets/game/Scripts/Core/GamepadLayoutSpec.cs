using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class GamepadLayoutSpec
    {
        public const string DefaultLayoutId = "default";
        public const string DefaultLayoutNameKey = "gamepadLayout.default";
        public const string DefaultSwapId = "none";
        public const string DefaultSwapNameKey = "gamepadSwap.none";

        public GamepadLayoutSpec(IReadOnlyList<GamepadLayout> layouts, IReadOnlyList<GamepadSwap> swaps)
        {
            if (layouts == null)
            {
                throw new ArgumentNullException(nameof(layouts));
            }

            if (swaps == null)
            {
                throw new ArgumentNullException(nameof(swaps));
            }

            // 選ぶ画面は一覧の先頭を「今の割り当てがどれでもないとき」の戻り先にするので、空の一覧は断る。
            if (layouts.Count == 0)
            {
                throw new ArgumentException("layouts must have at least one layout", nameof(layouts));
            }

            if (swaps.Count == 0)
            {
                throw new ArgumentException("swaps must have at least one swap", nameof(swaps));
            }

            Layouts = SpecLists.UniqueById(layouts, l => l.Id, nameof(layouts));
            Swaps = SpecLists.UniqueById(swaps, s => s.Id, nameof(swaps));
        }

        public static GamepadLayoutSpec Default { get; } = new GamepadLayoutSpec(
            new[] { new GamepadLayout(DefaultLayoutId, DefaultLayoutNameKey, Array.Empty<GamepadLayoutBinding>()) },
            new[] { new GamepadSwap(DefaultSwapId, DefaultSwapNameKey, Array.Empty<GamepadSwapPair>()) });

        public IReadOnlyList<GamepadLayout> Layouts { get; }

        public IReadOnlyList<GamepadSwap> Swaps { get; }

        public GamepadLayout FindLayout(string id)
        {
            return SpecLists.FindById(Layouts, l => l.Id, id);
        }

        public GamepadSwap FindSwap(string id)
        {
            return SpecLists.FindById(Swaps, s => s.Id, id);
        }

        // 配置を書いた後に入れ替えを掛ける。defaultPath は Input Actions に書かれたそのアクションの既定のパス。
        public static string PathFor(GamepadLayout layout, GamepadSwap swap, string action, string defaultPath)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            if (swap == null)
            {
                throw new ArgumentNullException(nameof(swap));
            }

            var path = layout.TryGetPath(action, out var placed) ? placed : defaultPath ?? string.Empty;
            return swap.Apply(path);
        }
    }
}

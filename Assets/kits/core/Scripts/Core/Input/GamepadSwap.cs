using System;
using System.Collections.Generic;

namespace GameKit.Core
{
    public sealed class GamepadSwap
    {
        public GamepadSwap(string id, string nameKey, IReadOnlyList<GamepadSwapPair> pairs)
        {
            Id = SpecLists.Id(id, nameof(id));
            NameKey = SpecLists.Id(nameKey, nameof(nameKey));
            if (pairs == null)
            {
                throw new ArgumentNullException(nameof(pairs));
            }

            var copy = new GamepadSwapPair[pairs.Count];
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < copy.Length; i++)
            {
                var pair = pairs[i] ?? throw new ArgumentException("pairs must not contain null", nameof(pairs));

                // 同じ入力が 2 つの組にあると、どちらへ入れ替わるかが組の順番で決まってしまう。
                foreach (var path in new[] { pair.A, pair.B })
                {
                    if (!seen.Add(path))
                    {
                        throw new ArgumentException("pairs swaps '" + path + "' more than once", nameof(pairs));
                    }
                }

                copy[i] = pair;
            }

            Pairs = copy;
        }

        public string Id { get; }

        public string NameKey { get; }

        public IReadOnlyList<GamepadSwapPair> Pairs { get; }

        public string Apply(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            foreach (var pair in Pairs)
            {
                var swapped = Swap(path, pair.A, pair.B) ?? Swap(path, pair.B, pair.A);
                if (swapped != null)
                {
                    return swapped;
                }
            }

            return path;
        }

        // スティックの方向 (<Gamepad>/leftStick/up) も、スティックごと入れ替える。
        private static string Swap(string path, string from, string to)
        {
            if (string.Equals(path, from, StringComparison.OrdinalIgnoreCase))
            {
                return to;
            }

            if (path.Length > from.Length
                && path[from.Length] == '/'
                && path.StartsWith(from, StringComparison.OrdinalIgnoreCase))
            {
                return to + path.Substring(from.Length);
            }

            return null;
        }
    }
}

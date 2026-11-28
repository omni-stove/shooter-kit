using System;
using System.Collections.Generic;

namespace GameKit.Core
{
    public sealed class GamepadLayout
    {
        private readonly Dictionary<string, string> _paths;

        public GamepadLayout(string id, string nameKey, IReadOnlyList<GamepadLayoutBinding> bindings)
        {
            Id = SpecLists.Id(id, nameof(id));
            NameKey = SpecLists.Id(nameKey, nameof(nameKey));
            if (bindings == null)
            {
                throw new ArgumentNullException(nameof(bindings));
            }

            var copy = new GamepadLayoutBinding[bindings.Count];
            // Input System はアクション名を大文字小文字を区別せずに引くので、ここも同じ引き方にそろえる。
            _paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < copy.Length; i++)
            {
                var binding = bindings[i] ?? throw new ArgumentException("bindings must not contain null", nameof(bindings));
                if (_paths.ContainsKey(binding.Action))
                {
                    throw new ArgumentException("bindings contains the action '" + binding.Action + "' twice", nameof(bindings));
                }

                _paths.Add(binding.Action, binding.Path);
                copy[i] = binding;
            }

            Bindings = copy;
        }

        public string Id { get; }

        public string NameKey { get; }

        // 書かれていないアクションは Input Actions の既定のパスのままにする。
        public IReadOnlyList<GamepadLayoutBinding> Bindings { get; }

        public bool TryGetPath(string action, out string path)
        {
            if (string.IsNullOrWhiteSpace(action))
            {
                path = null;
                return false;
            }

            return _paths.TryGetValue(action.Trim(), out path);
        }
    }
}

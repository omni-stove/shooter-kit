using System;

namespace GameKit.Core
{
    public sealed class GamepadLayoutBinding
    {
        public const string GamepadPathPrefix = "<Gamepad>/";

        public GamepadLayoutBinding(string action, string path)
        {
            Action = SpecLists.Id(action, nameof(action));
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            var trimmed = path.Trim();
            if (trimmed.Length > 0 && !IsGamepadPath(trimmed))
            {
                throw new ArgumentException("path '" + trimmed + "' must be empty or start with " + GamepadPathPrefix, nameof(path));
            }

            Path = trimmed;
        }

        public string Action { get; }

        // 空なら、そのアクションをゲームパッドでは押せないようにする。
        public string Path { get; }

        public bool IsUnbound => Path.Length == 0;

        public static bool IsGamepadPath(string path)
        {
            return path != null
                && path.Length > GamepadPathPrefix.Length
                && path.StartsWith(GamepadPathPrefix, StringComparison.OrdinalIgnoreCase);
        }
    }
}

using System;

namespace GameKit.Core
{
    public sealed class GamepadSwapPair
    {
        public GamepadSwapPair(string a, string b)
        {
            A = Require(a, nameof(a));
            B = Require(b, nameof(b));
            if (string.Equals(A, B, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("a and b must be different inputs, but both are '" + A + "'", nameof(b));
            }
        }

        public string A { get; }

        public string B { get; }

        private static string Require(string path, string paramName)
        {
            var trimmed = path?.Trim();
            if (!GamepadLayoutBinding.IsGamepadPath(trimmed))
            {
                throw new ArgumentException(paramName + " '" + path + "' must start with " + GamepadLayoutBinding.GamepadPathPrefix, paramName);
            }

            return trimmed;
        }
    }
}

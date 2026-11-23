using System;

namespace Shooter.Core
{
    // 文でなく値で持つのは、文にするのが UI だから。長押しの秒数を持たせるのは、案内に出す秒数を調整値 (SecurityCameraSpec.DisableHoldSeconds) と食い違わせないため。
    public readonly struct SecurityCameraHint : IEquatable<SecurityCameraHint>
    {
        private readonly string _inputActionPath;

        public SecurityCameraHint(float holdSeconds, string inputActionPath)
        {
            SpecGuard.PositiveFinite(holdSeconds, nameof(holdSeconds));
            HoldSeconds = holdSeconds;
            _inputActionPath = inputActionPath ?? string.Empty;
        }

        public float HoldSeconds { get; }

        public string InputActionPath => _inputActionPath ?? string.Empty;

        public bool Equals(SecurityCameraHint other)
        {
            return HoldSeconds.Equals(other.HoldSeconds) && string.Equals(InputActionPath, other.InputActionPath, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is SecurityCameraHint other && Equals(other);
        }

        public static bool operator ==(SecurityCameraHint left, SecurityCameraHint right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SecurityCameraHint left, SecurityCameraHint right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(HoldSeconds, InputActionPath);
        }

        public override string ToString()
        {
            return $"securityCamera hold={HoldSeconds} input={InputActionPath}";
        }
    }
}

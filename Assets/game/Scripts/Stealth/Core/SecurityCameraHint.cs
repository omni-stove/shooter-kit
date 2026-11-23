using System;

namespace Shooter.Core
{
    // 文でなく値で持つのは、文にするのが UI だから。長押しの秒数を持たせるのは、案内に出す秒数を調整値 (SecurityCameraSpec.DisableHoldSeconds) と食い違わせないため。
    public readonly struct SecurityCameraHint : IEquatable<SecurityCameraHint>
    {
        private readonly string _inputActionPath;

        public SecurityCameraHint(float holdSeconds, string inputActionPath)
            : this(holdSeconds, 0f, true, inputActionPath)
        {
        }

        public SecurityCameraHint(float holdSeconds, float progress, bool canDisable, string inputActionPath)
        {
            SpecGuard.PositiveFinite(holdSeconds, nameof(holdSeconds));
            SpecGuard.Fraction(progress, nameof(progress));
            HoldSeconds = holdSeconds;
            Progress = progress;
            CanDisable = canDisable;
            _inputActionPath = inputActionPath ?? string.Empty;
        }

        public float HoldSeconds { get; }

        // 押し始めからの 0〜1。秒でなく割合で持つのは、UI が輪や棒の満ち具合にそのまま使うため。
        public float Progress { get; }

        // 止まっている間も案内を消さずに出すのは、近づいたプレイヤーに止めたカメラだと分からせるため。
        public bool CanDisable { get; }

        public string InputActionPath => _inputActionPath ?? string.Empty;

        public bool Equals(SecurityCameraHint other)
        {
            return HoldSeconds.Equals(other.HoldSeconds)
                && Progress.Equals(other.Progress)
                && CanDisable == other.CanDisable
                && string.Equals(InputActionPath, other.InputActionPath, StringComparison.Ordinal);
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
            return HashCode.Combine(HoldSeconds, Progress, CanDisable, InputActionPath);
        }

        public override string ToString()
        {
            return $"securityCamera hold={HoldSeconds} progress={Progress} canDisable={CanDisable} input={InputActionPath}";
        }
    }
}

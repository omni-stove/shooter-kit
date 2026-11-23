using Shooter.Core;

namespace Shooter.Runtime
{
    public static class SecurityCameraPrompt
    {
        public const string DisableKey = "hud.securityCamera.disable";

        public static LocalizedText Describe(SecurityCameraHint hint, string keyLabel)
        {
            return new LocalizedText(DisableKey, keyLabel ?? string.Empty, HoldSeconds(hint.HoldSeconds));
        }

        // 小数 1 桁で出すのは、長押しの秒数が 1.5 秒のように端数を持つ調整値だから。
        public static string HoldSeconds(float seconds)
        {
            return HudText.AbilityWaitSeconds(seconds);
        }
    }
}

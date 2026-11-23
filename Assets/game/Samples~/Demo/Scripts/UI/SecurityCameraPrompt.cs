using System;
using Shooter.Core;

namespace Shooter.Runtime
{
    public static class SecurityCameraPrompt
    {
        public const string DisableKey = "hud.securityCamera.disable";

        public const string DisablingKey = "hud.securityCamera.disabling";

        public const string DisabledKey = "hud.securityCamera.disabled";

        public static LocalizedText Describe(SecurityCameraHint hint, string keyLabel)
        {
            if (!hint.CanDisable)
            {
                return new LocalizedText(DisabledKey);
            }

            if (hint.Progress > 0f)
            {
                return new LocalizedText(DisablingKey, ProgressPercent(hint.Progress));
            }

            return new LocalizedText(DisableKey, keyLabel ?? string.Empty, HoldSeconds(hint.HoldSeconds));
        }

        // 小数 1 桁で出すのは、長押しの秒数が 1.5 秒のように端数を持つ調整値だから。
        public static string HoldSeconds(float seconds)
        {
            return HudText.AbilityWaitSeconds(seconds);
        }

        // 切り捨てるのは、押し終える前に 100% と出さないため。
        public static int ProgressPercent(float progress)
        {
            return (int)Math.Floor(Math.Clamp(progress, 0f, 1f) * 100f);
        }
    }
}

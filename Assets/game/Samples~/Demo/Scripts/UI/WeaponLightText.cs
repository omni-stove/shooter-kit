using Shooter.Core;

namespace Shooter.Runtime
{
    public static class WeaponLightText
    {
        public const string OnKey = "hud.weaponLight.on";

        public const string OffKey = "hud.weaponLight.off";

        public static readonly string[] Keys = { OnKey, OffKey };

        // 値が無いのと HasLight が false なのを同じに扱うのは、どちらも今の銃にライトが付いておらず、点けられないことを出す必要が無いため。
        public static bool IsShown(WeaponLightStatus? status)
        {
            return status.HasValue && status.Value.HasLight;
        }

        public static LocalizedText Message(WeaponLightStatus? status)
        {
            if (!IsShown(status))
            {
                return default;
            }

            return new LocalizedText(status.Value.IsOn ? OnKey : OffKey);
        }
    }
}

using Shooter.Core;

namespace Shooter.Runtime
{
    public static class SecurityAlarmText
    {
        public const string RingingKey = "hud.securityAlarm.ringing";

        public static bool IsShown(SecurityAlarmStatus? status)
        {
            return status.HasValue && status.Value.Ringing;
        }

        public static LocalizedText Message(SecurityAlarmStatus status)
        {
            return status.Ringing ? new LocalizedText(RingingKey, HudFormat.DisplayedSeconds(status.SecondsLeft)) : default;
        }
    }
}

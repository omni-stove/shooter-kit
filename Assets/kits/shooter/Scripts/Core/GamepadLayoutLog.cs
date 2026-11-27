namespace Shooter.Core
{
    public static class GamepadLayoutLog
    {
        public const string LogTag = "[Input]";

        public static string Applied(string layoutId, string swapId, int changed)
        {
            return $"{LogTag} applied gamepad layout '{layoutId}' with swap '{swapId}' (changed={changed})";
        }

        public static string UnknownAction(string layoutId, string action)
        {
            return $"{LogTag} gamepad layout '{layoutId}' names '{action}', which has no gamepad binding in the Player map; it is skipped";
        }
    }
}

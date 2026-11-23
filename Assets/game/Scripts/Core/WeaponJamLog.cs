using System.Globalization;

namespace Shooter.Core
{
    // 書式を 1 か所に置くのは、smoke とテストがログの行を同じ関数で作って突き合わせるため。
    public static class WeaponJamLog
    {
        public const string Tag = "[Jam]";

        public static string Jammed(string weaponId, int sustainedShots, float chance)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} weapon={1} jammed sustained={2} chance={3:0.000}",
                Tag,
                Name(weaponId),
                sustainedShots,
                chance);
        }

        public static string ClearStarted(string weaponId, float clearSeconds)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} weapon={1} clear started seconds={2:0.00}", Tag, Name(weaponId), clearSeconds);
        }

        public static string ClearCanceled(string weaponId, float remainingSeconds)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} weapon={1} clear canceled remaining={2:0.00}", Tag, Name(weaponId), remainingSeconds);
        }

        public static string Cleared(string weaponId)
        {
            return $"{Tag} weapon={Name(weaponId)} cleared";
        }

        private static string Name(string id)
        {
            return string.IsNullOrEmpty(id) ? "-" : id;
        }
    }
}

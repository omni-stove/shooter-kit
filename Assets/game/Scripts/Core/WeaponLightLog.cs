using System;
using System.Globalization;

namespace Shooter.Core
{
    public static class WeaponLightLog
    {
        public const string Prefix = "[Combat] weaponLight";

        public static string Toggled(string holderId, string weaponId, bool on)
        {
            return $"{Prefix} id={Name(holderId)} weapon={Name(weaponId)} on={(on ? "true" : "false")}";
        }

        public static string TurnedOff(string holderId, string weaponId, WeaponLightOffCause cause)
        {
            return $"{Prefix} id={Name(holderId)} weapon={Name(weaponId)} on=false cause={cause}";
        }

        public static string NotLit(string holderId, string weaponId, string reason)
        {
            return $"{Prefix} id={Name(holderId)} weapon={Name(weaponId)} not-lit reason={Name(reason)}";
        }

        public static string NetRequested(bool on)
        {
            return $"{Prefix} net requested on={Bool(on)}";
        }

        public static string NetNotSent(bool on, string reason)
        {
            return $"{Prefix} net not-sent on={Bool(on)} reason={Name(reason)}";
        }

        public static string NetChanged(bool from, bool to)
        {
            return $"{Prefix} net on={Bool(from)}->{Bool(to)}";
        }

        public static string NoInput(string actionPath)
        {
            return $"{Prefix} input action '{Name(actionPath)}' is missing; the light key is not read";
        }

        public static string Spec(string itemId, WeaponLightSpec light)
        {
            if (light == null)
            {
                throw new ArgumentNullException(nameof(light));
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} item={1} range={2:0.##} spotAngle={3:0.##} intensity={4:0.##} visual={5}",
                Prefix,
                Name(itemId),
                light.Range,
                light.SpotAngle,
                light.Intensity,
                Name(light.VisualId));
        }

        private static string Bool(bool value)
        {
            return value ? "true" : "false";
        }

        private static string Name(string id)
        {
            return string.IsNullOrEmpty(id) ? "-" : id;
        }
    }
}

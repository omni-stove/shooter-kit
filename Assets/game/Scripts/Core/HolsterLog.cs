using System;
using System.Globalization;

namespace Shooter.Core
{
    public static class HolsterLog
    {
        public const string Tag = "[Holster]";

        public static string Began(string holderId, HolsterPhase phase, float seconds)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} holder={1} began={2} seconds={3:0.00}", Tag, Name(holderId), phase, seconds);
        }

        public static string Settled(string holderId, HolsterPhase phase)
        {
            return $"{Tag} holder={Name(holderId)} settled={phase}";
        }

        public static string Reset(string holderId, string reason)
        {
            return $"{Tag} holder={Name(holderId)} reset=Drawn reason={Name(reason)}";
        }

        public static string Loaded(HolsterSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} spec moveSpeedMultiplier={1:0.00} armed={2} armedWhileHeldByOthers={3}",
                Tag,
                spec.MoveSpeedMultiplier,
                spec.ArmedPhases.Count == 0 ? "-" : string.Join(",", spec.ArmedPhases),
                spec.ArmedWhileHeldByOthers ? "true" : "false");
        }

        private static string Name(string id)
        {
            return string.IsNullOrEmpty(id) ? "-" : id;
        }
    }
}

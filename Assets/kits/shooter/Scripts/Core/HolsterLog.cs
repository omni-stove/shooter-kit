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

        // オンラインは秒数をサーバーが持つので、送った向き (Holstering か Drawing) だけを残す
        public static string NetRequested(HolsterPhase toward)
        {
            return $"{Tag} net requested={toward}";
        }

        public static string NetNotSent(HolsterPhase toward, string reason)
        {
            return $"{Tag} net not-sent={toward} reason={Name(reason)}";
        }

        public static string NetChanged(HolsterPhase from, HolsterPhase to)
        {
            return $"{Tag} net phase={from}->{to}";
        }

        public static string NoInput(string actionPath)
        {
            return $"{Tag} input action '{Name(actionPath)}' is missing; the holster key is not read";
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

using System;
using System.Globalization;

namespace Shooter.Core
{
    public static class BreachLog
    {
        public const string Tag = "[Breach]";

        public static string Kicked(string doorId, string breacherId)
        {
            return $"{Prefix(doorId)} kicked by={Name(breacherId)} phase={BreachDoorPhase.Opened}";
        }

        public static string PlantBegan(string doorId, string planterId, float plantSeconds, float fuseSeconds)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} plant-began by={1} plantSeconds={2:0.00} fuseSeconds={3:0.00}",
                Prefix(doorId),
                Name(planterId),
                plantSeconds,
                fuseSeconds);
        }

        public static string PlantCancelled(string doorId, string planterId, string reason)
        {
            return $"{Prefix(doorId)} plant-cancelled by={Name(planterId)} reason={Name(reason)}";
        }

        public static string Armed(string doorId, string planterId, float fuseSeconds)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} armed by={1} fuseSeconds={2:0.00}", Prefix(doorId), Name(planterId), fuseSeconds);
        }

        public static string Detonated(string doorId, string breacherId, int damage, float radius)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} detonated by={1} damage={2} radius={3:0.00} phase={4}",
                Prefix(doorId),
                Name(breacherId),
                damage,
                radius,
                BreachDoorPhase.Destroyed);
        }

        public static string Staggered(string doorId, BreachMethod method, StaggerKind kind, float seconds, int count)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} staggered method={1} kind={2} seconds={3:0.00} count={4}",
                Prefix(doorId),
                method,
                kind,
                seconds,
                count);
        }

        public static string Refused(string doorId, string actorId, BreachMethod method, BreachDoorPhase phase, string reason)
        {
            return $"{Prefix(doorId)} refused by={Name(actorId)} method={method} phase={phase} reason={Name(reason)}";
        }

        public static string Loaded(BreachSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} spec kickSeconds={1:0.00} plantSeconds={2:0.00} fuseSeconds={3:0.00} reach={4:0.00} room=({5:0.00}, {6:0.00}, {7:0.00}) "
                + "stagger=({8:0.00}, {9:0.00}) charge=({10}, {11:0.00}) sound=({12:0.00}, {13:0.00}) slowMotion=({14:0.00}, {15:0.00}) chargeItem={16}",
                Tag,
                spec.KickSeconds,
                spec.PlantSeconds,
                spec.FuseSeconds,
                spec.Reach,
                spec.RoomDepth,
                spec.RoomWidth,
                spec.RoomHeight,
                spec.KickStaggerSeconds,
                spec.ChargeStaggerSeconds,
                spec.ChargeDamage,
                spec.ChargeRadius,
                spec.KickSoundRadius,
                spec.ChargeSoundRadius,
                spec.SlowMotionScale,
                spec.SlowMotionSeconds,
                Name(spec.ChargeItemId));
        }

        private static string Prefix(string doorId)
        {
            return $"{Tag} door={Name(doorId)}";
        }

        private static string Name(string id)
        {
            return string.IsNullOrEmpty(id) ? "-" : id;
        }
    }
}

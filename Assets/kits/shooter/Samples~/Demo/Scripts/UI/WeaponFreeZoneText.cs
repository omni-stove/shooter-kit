using Shooter.Core;

namespace Shooter.Runtime
{
    public static class WeaponFreeZoneText
    {
        public const string WarnKey = "hud.weaponFree.warn";

        public const string WarnThenHostileKey = "hud.weaponFree.warnThenHostile";

        public const string AutoHolsterKey = "hud.weaponFree.autoHolster";

        public const string BlockDrawKey = "hud.weaponFree.blockDraw";

        // しまう状態の知らせが無いときは武装と数える。HolsterController の無いプレイヤーは常に構えているため。
        public static bool IsArmed(HolsterStatus? holster)
        {
            return !holster.HasValue || holster.Value.IsArmed;
        }

        // 警告する反応は Warning の間だけ出し、しまわせる・構えさせない反応は警告を待たずに中で構えている間ずっと出す。後の 2 つは警告の段を持たないため。
        public static bool IsShown(WeaponFreeZoneStatus? zone, HolsterStatus? holster)
        {
            if (!zone.HasValue || !zone.Value.Inside || !IsArmed(holster))
            {
                return false;
            }

            switch (zone.Value.Reaction)
            {
                case WeaponFreeZoneReaction.WarnOnly:
                case WeaponFreeZoneReaction.WarnThenHostile:
                    return zone.Value.Warning;
                case WeaponFreeZoneReaction.AutoHolster:
                case WeaponFreeZoneReaction.BlockDraw:
                    return true;
                default:
                    return false;
            }
        }

        public static LocalizedText Message(WeaponFreeZoneStatus status)
        {
            switch (status.Reaction)
            {
                case WeaponFreeZoneReaction.WarnOnly:
                    return new LocalizedText(WarnKey);
                case WeaponFreeZoneReaction.WarnThenHostile:
                    return new LocalizedText(WarnThenHostileKey, HudFormat.DisplayedSeconds(status.SecondsToHostile));
                case WeaponFreeZoneReaction.AutoHolster:
                    return new LocalizedText(AutoHolsterKey);
                case WeaponFreeZoneReaction.BlockDraw:
                    return new LocalizedText(BlockDrawKey);
                default:
                    return default;
            }
        }
    }
}

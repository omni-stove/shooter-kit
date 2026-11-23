namespace Shooter.Core
{
    // ほかの撃つ・リロードの行と並べて読めるよう、戦いのログと同じ [Combat] に出す。
    public static class AkimboLog
    {
        public const string LogTag = "[Combat]";

        public static string Fire(AkimboHand hand, int ammoInMagazine)
        {
            return $"{LogTag} akimbo fire hand={HandName(hand)} ammo={ammoInMagazine}";
        }

        public static string Reload(AkimboHand hand)
        {
            return $"{LogTag} akimbo reload hand={HandName(hand)}";
        }

        // enum の ToString() を使わないのは、知らない値が来ても数で残し、ログを読むツールの綴りを列挙の名前の変更に縛らないため。
        // サーバーの行 (ServerCombatLog) も同じ綴りで手を書くよう公開する。
        public static string HandName(AkimboHand hand)
        {
            switch (hand)
            {
                case AkimboHand.Right:
                    return "right";
                case AkimboHand.Left:
                    return "left";
                default:
                    return ((int)hand).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }
    }
}

namespace Shooter.Core
{
    // Right を 0 にするのは、撃った手を運ばない前の版の要求 (FireRequestMessage) と既定値を、1 丁で持つときの手にそろえるため。
    public enum AkimboHand : byte
    {
        Right = 0,
        Left = 1,
    }
}

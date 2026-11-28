namespace GameKit.Core
{
    // 持ち物に Add で入れるのではなく、持ち主が手に持つ物 (シューターなら持ち武器) から山を作るアイテムの部品。
    public interface IHeldItemPart : IItemPart
    {
        // 手に持つ物の Id。持ち物はこの Id で重さと付けた物を覚える。
        string HeldId { get; }
    }
}

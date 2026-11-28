namespace GameKit.Core
{
    // アイテムの種類ごとの値 (武器・アタッチメント・使って回復するなど) を ItemSpec の項目にしないのは、どんな種類があるかをジャンルのキットが決めるため。
    public interface IItemPart
    {
        // 持ち主のアイテムの値と、同じアイテムのほかの部品とを見て、一緒に持てなければ ArgumentException を投げる。
        void Check(ItemSpec item);

        // 表のほかのアイテムを指す部品 (弾薬のアイテムなど) が、表にそのアイテムがあるかを確かめる。無ければ ArgumentException を投げる。
        void CheckIn(ItemSpec item, ItemCatalog catalog);
    }
}

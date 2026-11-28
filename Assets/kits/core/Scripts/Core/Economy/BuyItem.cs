using System;

namespace GameKit.Core
{
    // 種類を列挙にせずキーの文字列で持つのは、どんな品 (武器・防具・回復薬など) があるかをゲームが決めるため。
    // 防具などの Id は何も引かず、量だけを持ち主に渡すことがあるので、一覧の中で区別できればよく、UUID を求めない。
    public sealed class BuyItem
    {
        public BuyItem(string id, string kind, int price, bool takesSlot = false, int slot = 0, int amount = 0, string nameKey = "")
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("id must not be empty", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(kind))
            {
                throw new ArgumentException("kind must not be empty", nameof(kind));
            }

            Kind = kind.Trim();
            Price = SpecGuard.NonNegative(price, nameof(price));
            Slot = SpecGuard.NonNegative(slot, nameof(slot));
            if (!takesSlot && slot != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slot), slot, "slot is only for items that take a slot; leave it 0 for " + Kind);
            }

            Amount = SpecGuard.NonNegative(amount, nameof(amount));
            TakesSlot = takesSlot;
            Id = id.Trim();
            NameKey = SpecGuard.NameKey(nameKey);
        }

        public string Id { get; }

        public string Kind { get; }

        public int Price { get; }

        // 枠を取る品は財布が枠ごとに覚え、同じ枠に同じ品をもう一度は買わせない。
        public bool TakesSlot { get; }

        public int Slot { get; }

        public int Amount { get; }

        public string NameKey { get; }
    }
}

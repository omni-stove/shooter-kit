using System;
using System.Collections.Generic;

namespace GameKit.Core
{
    public sealed class ItemSpec
    {
        public const int DefaultMaxStack = 1;
        public const int DefaultMaxCarried = 0;
        public const float DefaultWeight = 0f;
        public const int DefaultWidth = 1;
        public const int DefaultHeight = 1;

        // 持ち物を送る電文が 1 山の数を ushort で送る
        public const int MaxStackLimit = ushort.MaxValue;

        public const int MaxSize = InventoryRule.MaxGridWidth;

        private static readonly IReadOnlyList<IItemPart> _noParts = Array.AsReadOnly(Array.Empty<IItemPart>());

        public ItemSpec(
            string id,
            string nameKey = null,
            string iconId = null,
            int maxStack = DefaultMaxStack,
            int maxCarried = DefaultMaxCarried,
            float weight = DefaultWeight,
            int width = DefaultWidth,
            int height = DefaultHeight,
            IEnumerable<IItemPart> parts = null)
        {
            Id = SpecGuard.LowercaseUuid(id, nameof(id));
            NameKey = SpecGuard.NameKey(nameKey);
            IconId = SpecGuard.ReferenceId(iconId);
            MaxStack = Between(maxStack, 1, MaxStackLimit, nameof(maxStack));
            MaxCarried = SpecGuard.NonNegative(maxCarried, nameof(maxCarried));
            Weight = SpecGuard.NonNegativeFinite(weight, nameof(weight));
            Width = Between(width, 1, MaxSize, nameof(width));
            Height = Between(height, 1, MaxSize, nameof(height));
            Parts = Listed(parts);
            foreach (var part in Parts)
            {
                if (part is IHeldItemPart held)
                {
                    if (HeldId != null)
                    {
                        throw new ArgumentException("an item can be held as only one thing", nameof(parts));
                    }

                    if (string.IsNullOrEmpty(held.HeldId))
                    {
                        throw new ArgumentException("a held part must have a held id", nameof(parts));
                    }

                    HeldId = held.HeldId;
                }
            }

            foreach (var part in Parts)
            {
                part.Check(this);
            }
        }

        public string Id { get; }

        public string NameKey { get; }

        public string IconId { get; }

        public int MaxStack { get; }

        public int MaxCarried { get; }

        public float Weight { get; }

        public int Width { get; }

        public int Height { get; }

        public IReadOnlyList<IItemPart> Parts { get; }

        // 手に持つ物から山を作るアイテムなら、その物の Id。そうでなければ null。
        public string HeldId { get; }

        public bool IsHeld => HeldId != null;

        public bool TryGetPart<T>(out T part)
            where T : class, IItemPart
        {
            foreach (var candidate in Parts)
            {
                if (candidate is T found)
                {
                    part = found;
                    return true;
                }
            }

            part = null;
            return false;
        }

        public T PartOf<T>()
            where T : class, IItemPart
        {
            return TryGetPart<T>(out var part) ? part : null;
        }

        public int WidthWhen(bool rotated) => rotated ? Height : Width;

        public int HeightWhen(bool rotated) => rotated ? Width : Height;

        private static IReadOnlyList<IItemPart> Listed(IEnumerable<IItemPart> parts)
        {
            if (parts == null)
            {
                return _noParts;
            }

            var list = new List<IItemPart>();
            foreach (var part in parts)
            {
                if (part == null)
                {
                    continue;
                }

                // 同じ種類の部品が 2 つあると、TryGetPart がどちらを返すかで値が変わってしまう
                foreach (var other in list)
                {
                    if (other.GetType() == part.GetType())
                    {
                        throw new ArgumentException("an item cannot have two " + part.GetType().Name + " parts", nameof(parts));
                    }
                }

                list.Add(part);
            }

            return list.Count == 0 ? _noParts : list.AsReadOnly();
        }

        private static int Between(int value, int min, int max, string name)
        {
            if (value < min || value > max)
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be between " + min + " and " + max);
            }

            return value;
        }
    }
}

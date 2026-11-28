using System;
using System.Collections.Generic;

namespace GameKit.Core
{
    public readonly struct HeldItem
    {
        public HeldItem(string id, float weight, IReadOnlyList<string> attachments = null)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("id must not be null or empty", nameof(id));
            }

            Id = id;
            Weight = SpecGuard.NonNegativeFinite(weight, nameof(weight));
            Attachments = attachments ?? Inventory.NoAttachments;
        }

        public string Id { get; }

        // 手に持つ物の重さは付けた物で変わるので、アイテムの表の重さではなくここで渡す。
        public float Weight { get; }

        public IReadOnlyList<string> Attachments { get; }
    }
}

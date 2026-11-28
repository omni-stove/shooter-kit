using System;
using GameKit.Core;

namespace Shooter.Core
{
    public sealed class PickupSpec
    {
        public const int NoOwner = -1;

        private PickupSpec(
            string kind, int amount, string weaponId, int tagOwnerId, Side? tagOwnerSide, PickupEligibility eligibility, string itemId = null, float weaponWeight = 0f)
        {
            ItemId = itemId;
            WeaponWeight = weaponWeight;
            Kind = kind;
            Amount = amount;
            WeaponId = weaponId;
            TagOwnerId = tagOwnerId;
            TagOwnerSide = tagOwnerSide;
            Eligibility = eligibility;
        }

        public string Kind { get; }

        public int Amount { get; }

        public string WeaponId { get; }

        public string ItemId { get; }

        // 持ち物に入れる武器の重さ。持ち物に入るかを、まだ持っていない武器の重さで決めるのに使う
        public float WeaponWeight { get; }

        public int TagOwnerId { get; }

        public Side? TagOwnerSide { get; }

        public PickupEligibility Eligibility { get; }

        public static PickupSpec Weapon(string weaponId, int ammoIfOwned, PickupEligibility eligibility = default, float weaponWeight = 0f)
        {
            if (string.IsNullOrEmpty(weaponId))
            {
                throw new ArgumentException("weaponId must not be null or empty", nameof(weaponId));
            }

            if (ammoIfOwned < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ammoIfOwned), ammoIfOwned, "ammoIfOwned must not be negative");
            }

            SpecGuard.NonNegativeFinite(weaponWeight, nameof(weaponWeight));
            return new PickupSpec(PickupKinds.Weapon, ammoIfOwned, weaponId, NoOwner, null, eligibility, null, weaponWeight);
        }

        public static PickupSpec ReserveAmmo(int amount, string weaponId = null, PickupEligibility eligibility = default)
        {
            return new PickupSpec(
                PickupKinds.ReserveAmmo,
                RequirePositive(amount, nameof(amount)),
                string.IsNullOrEmpty(weaponId) ? null : weaponId,
                NoOwner,
                null,
                eligibility);
        }

        public static PickupSpec Health(int amount, PickupEligibility eligibility = default)
        {
            return new PickupSpec(PickupKinds.Health, RequirePositive(amount, nameof(amount)), null, NoOwner, null, eligibility);
        }

        public static PickupSpec Armor(int amount, PickupEligibility eligibility = default)
        {
            return new PickupSpec(PickupKinds.Armor, RequirePositive(amount, nameof(amount)), null, NoOwner, null, eligibility);
        }

        public static PickupSpec Tag(int ownerId, Side? ownerSide, PickupEligibility eligibility = default)
        {
            if (ownerId < NoOwner)
            {
                throw new ArgumentOutOfRangeException(nameof(ownerId), ownerId, "ownerId must be 0 or more, or NoOwner");
            }

            return new PickupSpec(PickupKinds.Tag, 1, null, ownerId, ownerSide, eligibility);
        }

        public static PickupSpec Item(string itemId, int amount, PickupEligibility eligibility = default)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                throw new ArgumentException("itemId must not be null or empty", nameof(itemId));
            }

            return new PickupSpec(PickupKinds.Item, RequirePositive(amount, nameof(amount)), null, NoOwner, null, eligibility, itemId);
        }

        public override string ToString()
        {
            return PickupLog.Describe(this);
        }

        private static int RequirePositive(int value, string name)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be positive");
            }

            return value;
        }
    }
}

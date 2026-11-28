using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GameKit.Core;

namespace Shooter.Core
{
    // 重さは持ち物のマスに置いたときと武器に付けたときで同じにするため、ここには持たずアイテムの重さ (ItemSpec.Weight) を使う
    public sealed class AttachmentSpec : IItemPart
    {
        public const float DefaultMultiplier = 1f;
        public const int DefaultMagazineSizeBonus = 0;

        public AttachmentSpec(
            string slot,
            IEnumerable<string> weaponIds = null,
            float rangeMultiplier = DefaultMultiplier,
            float hipSpreadMultiplier = DefaultMultiplier,
            float adsSpreadMultiplier = DefaultMultiplier,
            float moveSpreadMultiplier = DefaultMultiplier,
            float recoilMultiplier = DefaultMultiplier,
            float reloadSecondsMultiplier = DefaultMultiplier,
            int magazineSizeBonus = DefaultMagazineSizeBonus,
            float shotNoiseRadiusMultiplier = DefaultMultiplier,
            float moveSpeedMultiplier = DefaultMultiplier,
            IEnumerable<float> zoomLevels = null,
            SightSpec sight = null,
            WeaponLightSpec light = null)
        {
            Slot = SlotName(slot, nameof(slot));
            WeaponIds = Ids(weaponIds);
            RangeMultiplier = SpecGuard.PositiveFinite(rangeMultiplier, nameof(rangeMultiplier));
            HipSpreadMultiplier = SpecGuard.NonNegativeFinite(hipSpreadMultiplier, nameof(hipSpreadMultiplier));
            AdsSpreadMultiplier = SpecGuard.NonNegativeFinite(adsSpreadMultiplier, nameof(adsSpreadMultiplier));
            MoveSpreadMultiplier = SpecGuard.NonNegativeFinite(moveSpreadMultiplier, nameof(moveSpreadMultiplier));
            RecoilMultiplier = SpecGuard.NonNegativeFinite(recoilMultiplier, nameof(recoilMultiplier));
            ReloadSecondsMultiplier = SpecGuard.NonNegativeFinite(reloadSecondsMultiplier, nameof(reloadSecondsMultiplier));
            MagazineSizeBonus = magazineSizeBonus;
            ShotNoiseRadiusMultiplier = SpecGuard.NonNegativeFinite(shotNoiseRadiusMultiplier, nameof(shotNoiseRadiusMultiplier));
            MoveSpeedMultiplier = SpecGuard.NonNegativeFinite(moveSpeedMultiplier, nameof(moveSpeedMultiplier));
            ZoomLevels = Zooms(zoomLevels);
            Sight = sight;
            Light = light;
        }

        public string Slot { get; }

        public IReadOnlyList<string> WeaponIds { get; }

        public float RangeMultiplier { get; }

        public float HipSpreadMultiplier { get; }

        public float AdsSpreadMultiplier { get; }

        public float MoveSpreadMultiplier { get; }

        public float RecoilMultiplier { get; }

        public float ReloadSecondsMultiplier { get; }

        public int MagazineSizeBonus { get; }

        public float ShotNoiseRadiusMultiplier { get; }

        public float MoveSpeedMultiplier { get; }

        public IReadOnlyList<float> ZoomLevels { get; }

        public SightSpec Sight { get; }

        public bool IsOptic => ZoomLevels != null || Sight != null;

        public WeaponLightSpec Light { get; }

        public bool IsLight => Light != null;

        public void Check(ItemSpec item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (item.IsHeld || item.IsUsable())
            {
                throw Rejected("attachment", "an attachment item cannot be a weapon or be used up (using it attaches it)");
            }
        }

        // 付ける武器が表にあるかは、サーバーから届いた一部だけの表でも作れるように、作るときではなく CheckAttachments で読むときに確かめる
        public void CheckIn(ItemSpec item, ItemCatalog catalog)
        {
        }

        public bool Fits(string weaponId)
        {
            if (WeaponIds.Count == 0)
            {
                return !string.IsNullOrEmpty(weaponId);
            }

            foreach (var id in WeaponIds)
            {
                if (string.Equals(id, weaponId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        // 武器の取り付け口の名前。見た目は Socket_<slot> の位置に付けるので、名前に使える文字だけにする
        public static string SlotName(string slot, string paramName)
        {
            var trimmed = slot?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                throw new ArgumentException(paramName + " must not be empty", paramName);
            }

            foreach (var c in trimmed)
            {
                if (!(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z') && !(c >= '0' && c <= '9') && c != '_')
                {
                    throw new ArgumentException(paramName + " '" + trimmed + "' may use only letters, digits and _", paramName);
                }
            }

            return trimmed;
        }

        private static ReadOnlyCollection<string> Ids(IEnumerable<string> weaponIds)
        {
            var list = new List<string>();
            if (weaponIds == null)
            {
                return list.AsReadOnly();
            }

            foreach (var id in weaponIds)
            {
                var normalized = SpecGuard.LowercaseUuid(id, nameof(weaponIds));
                if (!list.Contains(normalized))
                {
                    list.Add(normalized);
                }
            }

            return list.AsReadOnly();
        }

        private static ReadOnlyCollection<float> Zooms(IEnumerable<float> zoomLevels)
        {
            if (zoomLevels == null)
            {
                return null;
            }

            var list = new List<float>();
            foreach (var level in zoomLevels)
            {
                list.Add(AdsZoom.ValidateFieldOfView(level, nameof(zoomLevels)));
            }

            if (list.Count == 0)
            {
                throw new ArgumentException("zoomLevels must not be empty; leave it out to keep the weapon's zoom", nameof(zoomLevels));
            }

            return list.AsReadOnly();
        }

        // 断る引数名を JSON の項目名 (maxStack・weaponId など) にするのは、InventoryJson がそれで読んだ項目を指して断るため
        private static ArgumentException Rejected(string paramName, string message)
        {
            return new ArgumentException(message, paramName);
        }
    }
}

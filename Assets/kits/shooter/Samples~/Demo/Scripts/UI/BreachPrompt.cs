using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public static class BreachPrompt
    {
        public const string KickOrPlantKey = "hud.breach.kickOrPlant";

        public const string KickKey = "hud.breach.kick";

        public const string PlantKey = "hud.breach.plant";

        public const string KickNoChargeKey = "hud.breach.kickNoCharge";

        public const string NoChargeKey = "hud.breach.noCharge";

        public const string PlantingKey = "hud.breach.planting";

        public const string ArmedKey = "hud.breach.armed";

        public static bool TryDescribe(BreachHint hint, string keyLabel, out LocalizedText text)
        {
            return TryDescribe(hint, keyLabel, KitTuning.Items, out text);
        }

        public static bool TryDescribe(BreachHint hint, string keyLabel, ItemCatalog items, out LocalizedText text)
        {
            var key = keyLabel ?? string.Empty;
            if (hint.CanPlant)
            {
                text = hint.CanKick ? new LocalizedText(KickOrPlantKey, key) : new LocalizedText(PlantKey, key);
                return true;
            }

            if (hint.LacksCharge)
            {
                var charge = ChargeName(hint.ChargeItemId, items);
                text = hint.CanKick ? new LocalizedText(KickNoChargeKey, key, charge) : new LocalizedText(NoChargeKey, charge);
                return true;
            }

            if (hint.CanKick)
            {
                text = new LocalizedText(KickKey, key);
                return true;
            }

            text = default;
            return false;
        }

        // 閉じている・破れた後は進み具合を出さず、扉の案内に任せる。
        public static bool TryDescribeProgress(BreachStatus? status, out LocalizedText text)
        {
            if (!status.HasValue)
            {
                text = default;
                return false;
            }

            var value = status.Value;
            if (value.IsPlanting)
            {
                text = new LocalizedText(PlantingKey, Percent(value.Progress));
                return true;
            }

            if (value.IsArmed)
            {
                text = new LocalizedText(ArmedKey, Mathf.CeilToInt(value.SecondsRemaining));
                return true;
            }

            text = default;
            return false;
        }

        // 品が表に無いときも Id を出し、どの品を足せばよいかが画面で分かるようにする。
        public static LocalizedText ChargeName(string chargeItemId, ItemCatalog items)
        {
            var id = chargeItemId ?? string.Empty;
            if (items != null && items.TryFind(id, out var item))
            {
                return DisplayNames.Of(item.NameKey, item.Id);
            }

            return LocalizedText.Literal(id);
        }

        // 切り捨てるのは、仕掛け終わる前に 100% と出さないため。
        private static int Percent(float progress)
        {
            return Mathf.Clamp(Mathf.FloorToInt(progress * 100f), 0, 100);
        }
    }
}

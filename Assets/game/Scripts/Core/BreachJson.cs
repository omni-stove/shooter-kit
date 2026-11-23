using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public static class BreachJson
    {
        public const string ResourcePath = "Tuning/breach";

        public const string KickSecondsItem = "kickSeconds";

        public const string PlantSecondsItem = "plantSeconds";

        public const string FuseSecondsItem = "fuseSeconds";

        public const string ReachItem = "reach";

        public const string RoomDepthItem = "roomDepth";

        public const string RoomWidthItem = "roomWidth";

        public const string RoomHeightItem = "roomHeight";

        public const string KickStaggerSecondsItem = "kickStaggerSeconds";

        public const string ChargeStaggerSecondsItem = "chargeStaggerSeconds";

        public const string ChargeDamageItem = "chargeDamage";

        public const string ChargeRadiusItem = "chargeRadius";

        public const string KickSoundRadiusItem = "kickSoundRadius";

        public const string ChargeSoundRadiusItem = "chargeSoundRadius";

        public const string SlowMotionScaleItem = "slowMotionScale";

        public const string SlowMotionSecondsItem = "slowMotionSeconds";

        public const string ChargeItemIdItem = "chargeItemId";

        private const string _kind = "breach JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[]
        {
            KickSecondsItem,
            PlantSecondsItem,
            FuseSecondsItem,
            ReachItem,
            RoomDepthItem,
            RoomWidthItem,
            RoomHeightItem,
            KickStaggerSecondsItem,
            ChargeStaggerSecondsItem,
            ChargeDamageItem,
            ChargeRadiusItem,
            KickSoundRadiusItem,
            ChargeSoundRadiusItem,
            SlowMotionScaleItem,
            SlowMotionSecondsItem,
            ChargeItemIdItem,
        };

        public static BreachSpec Parse(string text, string sourceName)
        {
            var source = ServerDataJson.SourceOf(sourceName, _kind);
            try
            {
                var fields = new ServerDataJson.Fields(source, _kind, ServerDataJson.ParseDocument(text, source, _kind), string.Empty, ItemNames);
                var kickSeconds = fields.ReadFloat(KickSecondsItem, BreachSpec.DefaultKickSeconds);
                var plantSeconds = fields.ReadFloat(PlantSecondsItem, BreachSpec.DefaultPlantSeconds);
                var fuseSeconds = fields.ReadFloat(FuseSecondsItem, BreachSpec.DefaultFuseSeconds);
                var reach = fields.ReadFloat(ReachItem, BreachSpec.DefaultReach);
                var roomDepth = fields.ReadFloat(RoomDepthItem, BreachSpec.DefaultRoomDepth);
                var roomWidth = fields.ReadFloat(RoomWidthItem, BreachSpec.DefaultRoomWidth);
                var roomHeight = fields.ReadFloat(RoomHeightItem, BreachSpec.DefaultRoomHeight);
                var kickStaggerSeconds = fields.ReadFloat(KickStaggerSecondsItem, BreachSpec.DefaultKickStaggerSeconds);
                var chargeStaggerSeconds = fields.ReadFloat(ChargeStaggerSecondsItem, BreachSpec.DefaultChargeStaggerSeconds);
                var chargeDamage = fields.ReadInt(ChargeDamageItem, BreachSpec.DefaultChargeDamage);
                var chargeRadius = fields.ReadFloat(ChargeRadiusItem, BreachSpec.DefaultChargeRadius);
                var kickSoundRadius = fields.ReadFloat(KickSoundRadiusItem, BreachSpec.DefaultKickSoundRadius);
                var chargeSoundRadius = fields.ReadFloat(ChargeSoundRadiusItem, BreachSpec.DefaultChargeSoundRadius);
                var slowMotionScale = fields.ReadFloat(SlowMotionScaleItem, BreachSpec.DefaultSlowMotionScale);
                var slowMotionSeconds = fields.ReadFloat(SlowMotionSecondsItem, BreachSpec.DefaultSlowMotionSeconds);
                var chargeItemId = fields.ReadString(ChargeItemIdItem, BreachSpec.DefaultChargeItemId);
                try
                {
                    return new BreachSpec(
                        kickSeconds,
                        plantSeconds,
                        fuseSeconds,
                        reach,
                        roomDepth,
                        roomWidth,
                        roomHeight,
                        kickStaggerSeconds,
                        chargeStaggerSeconds,
                        chargeDamage,
                        chargeRadius,
                        kickSoundRadius,
                        chargeSoundRadius,
                        slowMotionScale,
                        slowMotionSeconds,
                        chargeItemId);
                }
                catch (ArgumentException e)
                {
                    throw fields.Rejected(ItemOf(e.ParamName), e.Message, e);
                }
            }
            catch (ServerDataJsonException e)
            {
                // KitTuning は TuningJsonException だけを「読めないので既定値で続ける」と扱う。
                var inner = e.InnerException is ArgumentException rejected ? (Exception)rejected : e;
                throw new TuningJsonException(e.SourceName, e.Item, e.Message, inner);
            }
        }

        public static string Write(BreachSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var writer = new JsonWriter();
            writer.Float(KickSecondsItem, spec.KickSeconds);
            writer.Float(PlantSecondsItem, spec.PlantSeconds);
            writer.Float(FuseSecondsItem, spec.FuseSeconds);
            writer.Float(ReachItem, spec.Reach);
            writer.Float(RoomDepthItem, spec.RoomDepth);
            writer.Float(RoomWidthItem, spec.RoomWidth);
            writer.Float(RoomHeightItem, spec.RoomHeight);
            writer.Float(KickStaggerSecondsItem, spec.KickStaggerSeconds);
            writer.Float(ChargeStaggerSecondsItem, spec.ChargeStaggerSeconds);
            writer.Int(ChargeDamageItem, spec.ChargeDamage);
            writer.Float(ChargeRadiusItem, spec.ChargeRadius);
            writer.Float(KickSoundRadiusItem, spec.KickSoundRadius);
            writer.Float(ChargeSoundRadiusItem, spec.ChargeSoundRadius);
            writer.Float(SlowMotionScaleItem, spec.SlowMotionScale);
            writer.Float(SlowMotionSecondsItem, spec.SlowMotionSeconds);
            writer.String(ChargeItemIdItem, spec.ChargeItemId);
            return writer.Finish();
        }

        private static string ItemOf(string paramName)
        {
            foreach (var item in ItemNames)
            {
                if (string.Equals(item, paramName, StringComparison.Ordinal))
                {
                    return item;
                }
            }

            return null;
        }
    }
}

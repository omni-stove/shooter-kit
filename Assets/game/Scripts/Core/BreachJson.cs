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
            return Parse(text, sourceName, BreachSpec.Default);
        }

        public static BreachSpec Parse(string text, string sourceName, BreachSpec fallback)
        {
            if (fallback == null)
            {
                throw new ArgumentNullException(nameof(fallback));
            }

            var source = ServerDataJson.SourceOf(sourceName, _kind);
            try
            {
                var fields = new ServerDataJson.Fields(source, _kind, ServerDataJson.ParseDocument(text, source, _kind), string.Empty, ItemNames);
                var kickSeconds = fields.ReadFloat(KickSecondsItem, fallback.KickSeconds);
                var plantSeconds = fields.ReadFloat(PlantSecondsItem, fallback.PlantSeconds);
                var fuseSeconds = fields.ReadFloat(FuseSecondsItem, fallback.FuseSeconds);
                var reach = fields.ReadFloat(ReachItem, fallback.Reach);
                var roomDepth = fields.ReadFloat(RoomDepthItem, fallback.RoomDepth);
                var roomWidth = fields.ReadFloat(RoomWidthItem, fallback.RoomWidth);
                var roomHeight = fields.ReadFloat(RoomHeightItem, fallback.RoomHeight);
                var kickStaggerSeconds = fields.ReadFloat(KickStaggerSecondsItem, fallback.KickStaggerSeconds);
                var chargeStaggerSeconds = fields.ReadFloat(ChargeStaggerSecondsItem, fallback.ChargeStaggerSeconds);
                var chargeDamage = fields.ReadInt(ChargeDamageItem, fallback.ChargeDamage);
                var chargeRadius = fields.ReadFloat(ChargeRadiusItem, fallback.ChargeRadius);
                var kickSoundRadius = fields.ReadFloat(KickSoundRadiusItem, fallback.KickSoundRadius);
                var chargeSoundRadius = fields.ReadFloat(ChargeSoundRadiusItem, fallback.ChargeSoundRadius);
                var slowMotionScale = fields.ReadFloat(SlowMotionScaleItem, fallback.SlowMotionScale);
                var slowMotionSeconds = fields.ReadFloat(SlowMotionSecondsItem, fallback.SlowMotionSeconds);
                var chargeItemId = fields.ReadString(ChargeItemIdItem, fallback.ChargeItemId);
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

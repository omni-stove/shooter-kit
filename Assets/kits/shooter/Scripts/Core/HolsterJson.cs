using System;
using System.Collections.Generic;
using GameKit.Core;

namespace Shooter.Core
{
    public static class HolsterJson
    {
        public const string ResourcePath = "Tuning/holster";

        public const string MoveSpeedMultiplierItem = "moveSpeedMultiplier";

        public const string ArmedPhasesItem = "armedPhases";

        public const string ArmedWhileHeldByOthersItem = "armedWhileHeldByOthers";

        private const string _kind = "holster JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[]
        {
            MoveSpeedMultiplierItem,
            ArmedPhasesItem,
            ArmedWhileHeldByOthersItem,
        };

        public static HolsterSpec Parse(string text, string sourceName)
        {
            var source = ServerDataJson.SourceOf(sourceName, _kind);
            try
            {
                var fields = new ServerDataJson.Fields(source, _kind, ServerDataJson.ParseDocument(text, source, _kind), string.Empty, ItemNames);
                var moveSpeedMultiplier = fields.ReadFloat(MoveSpeedMultiplierItem, HolsterSpec.DefaultMoveSpeedMultiplier);
                var armedPhases = fields.ReadEnumArray(ArmedPhasesItem, HolsterSpec.DefaultArmedPhases);
                var armedWhileHeldByOthers = fields.ReadBool(ArmedWhileHeldByOthersItem, HolsterSpec.DefaultArmedWhileHeldByOthers);
                try
                {
                    return new HolsterSpec(moveSpeedMultiplier, armedPhases, armedWhileHeldByOthers);
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

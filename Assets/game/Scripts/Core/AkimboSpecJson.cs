using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    // 武器の JSON の akimbo を読み書きする。項目名は AkimboSpec のコンストラクター引数と同じにし、投げた不正をそのまま項目に結び付ける。
    internal static class AkimboSpecJson
    {
        public const string AkimboItem = "akimbo";
        public const string SpreadMultiplierItem = "spreadMultiplier";
        public const string BlocksAdsItem = "blocksAds";
        public const string ReloadSecondsMultiplierItem = "reloadSecondsMultiplier";
        public const string MoveSpeedMultiplierItem = "moveSpeedMultiplier";
        public const string OffhandShotDelaySecondsItem = "offhandShotDelaySeconds";

        public static IReadOnlyList<string> ItemNames { get; } = new[]
        {
            SpreadMultiplierItem, BlocksAdsItem, ReloadSecondsMultiplierItem, MoveSpeedMultiplierItem, OffhandShotDelaySecondsItem,
        };

        public static AkimboSpec Read(ServerDataJson.Fields fields)
        {
            if (fields == null)
            {
                throw new ArgumentNullException(nameof(fields));
            }

            try
            {
                return new AkimboSpec(
                    fields.ReadFloat(SpreadMultiplierItem, AkimboSpec.DefaultSpreadMultiplier),
                    fields.ReadBool(BlocksAdsItem, AkimboSpec.DefaultBlocksAds),
                    fields.ReadFloat(ReloadSecondsMultiplierItem, AkimboSpec.DefaultReloadSecondsMultiplier),
                    fields.ReadFloat(MoveSpeedMultiplierItem, AkimboSpec.DefaultMoveSpeedMultiplier),
                    fields.ReadFloat(OffhandShotDelaySecondsItem, AkimboSpec.DefaultOffhandShotDelaySeconds));
            }
            catch (ArgumentException e)
            {
                throw fields.Rejected(Known(e.ParamName) ? fields.ItemPath(e.ParamName) : fields.Path, e.Message, e);
            }
        }

        public static void Write(JsonWriter writer, AkimboSpec akimbo)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            // 二丁持ちでない武器の JSON を、この項目を足す前と同じ文字列のまま書き戻す。
            if (akimbo == null)
            {
                return;
            }

            writer.BeginObject(AkimboItem);
            writer.Float(SpreadMultiplierItem, akimbo.SpreadMultiplier);
            writer.Bool(BlocksAdsItem, akimbo.BlocksAds);
            writer.Float(ReloadSecondsMultiplierItem, akimbo.ReloadSecondsMultiplier);
            writer.Float(MoveSpeedMultiplierItem, akimbo.MoveSpeedMultiplier);
            writer.Float(OffhandShotDelaySecondsItem, akimbo.OffhandShotDelaySeconds);
            writer.EndObject();
        }

        private static bool Known(string item)
        {
            foreach (var name in ItemNames)
            {
                if (string.Equals(name, item, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

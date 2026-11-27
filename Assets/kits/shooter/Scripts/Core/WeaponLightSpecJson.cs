using System;
using System.Collections.Generic;
using GameKit.Core;

namespace Shooter.Core
{
    // アタッチメントの attachment.light を読み書きする。武器の側に付くライトも後から同じ形で読めるよう、SightSpecJson と同じくここにまとめる。
    internal static class WeaponLightSpecJson
    {
        public const string LightItem = "light";
        public const string RangeItem = "range";
        public const string SpotAngleItem = "spotAngle";
        public const string IntensityItem = "intensity";
        public const string VisualIdItem = "visualId";

        public static IReadOnlyList<string> ItemNames { get; } = new[]
        {
            RangeItem, SpotAngleItem, IntensityItem, VisualIdItem,
        };

        public static WeaponLightSpec Read(ServerDataJson.Fields fields)
        {
            if (fields == null)
            {
                throw new ArgumentNullException(nameof(fields));
            }

            try
            {
                return new WeaponLightSpec(
                    fields.ReadFloat(RangeItem, WeaponLightSpec.DefaultRange),
                    fields.ReadFloat(SpotAngleItem, WeaponLightSpec.DefaultSpotAngle),
                    fields.ReadFloat(IntensityItem, WeaponLightSpec.DefaultIntensity),
                    fields.ReadString(VisualIdItem, null));
            }
            catch (ArgumentException e)
            {
                var item = Known(e.ParamName) ? fields.ItemPath(e.ParamName) : fields.Path;
                throw fields.Rejected(item, e.Message, e);
            }
        }

        // 照準器と違い、既定の値だけのライトでも「ライトである」ことを伝えるので、null でなければいつも書き出す。
        public static void Write(JsonWriter writer, WeaponLightSpec light)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            if (light == null)
            {
                return;
            }

            writer.BeginObject(LightItem);
            writer.Float(RangeItem, light.Range);
            writer.Float(SpotAngleItem, light.SpotAngle);
            writer.Float(IntensityItem, light.Intensity);
            if (light.HasVisual)
            {
                writer.String(VisualIdItem, light.VisualId);
            }

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

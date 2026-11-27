using System;
using System.Collections.Generic;
using GameKit.Core;

namespace Shooter.Core
{
    // Unity 抜きのサーバーはシーンのトリガーを読めないので、Unity の Editor がシーンの WeaponFreeZone から書き出したものを読む。
    // 並びはシーンの階層順で、重なったゾーンでどの状態を配るかの決め手になる。
    public sealed class NetMapWeaponFreeZones
    {
        public const string Extension = ".weapon-free-zones.json";

        public const string ZonesItem = "zones";
        public const string IdItem = "id";
        public const string MinItem = "min";
        public const string MaxItem = "max";
        public const string ReactionItem = "reaction";
        public const string HostileAfterSecondsItem = "hostileAfterSeconds";

        private const string _kind = "map weapon free zones JSON";

        private static readonly string[] _topItems = { ZonesItem };

        private static readonly string[] _zoneItems = { IdItem, MinItem, MaxItem, ReactionItem, HostileAfterSecondsItem };

        public NetMapWeaponFreeZones(IReadOnlyList<MapWeaponFreeZone> zones)
        {
            if (zones == null)
            {
                throw new ArgumentNullException(nameof(zones));
            }

            var copy = new List<MapWeaponFreeZone>(zones.Count);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var zone in zones)
            {
                if (zone == null)
                {
                    throw new ArgumentException("zones must not contain null", nameof(zones));
                }

                // ログで同じ Id の 2 つを見分けられないうえ、オフラインの中立の警備も Id で 1 つ目にしか結び付かないため
                if (!ids.Add(zone.Id))
                {
                    throw new ArgumentException($"two zones share the id '{zone.Id}'", nameof(zones));
                }

                copy.Add(zone);
            }

            Zones = copy.AsReadOnly();
        }

        public static NetMapWeaponFreeZones None { get; } = new NetMapWeaponFreeZones(Array.Empty<MapWeaponFreeZone>());

        public IReadOnlyList<MapWeaponFreeZone> Zones { get; }

        public static NetMapWeaponFreeZones Parse(string text, string sourceName)
        {
            var source = ServerDataJson.SourceOf(sourceName, _kind);
            var root = ServerDataJson.ParseDocument(text, source, _kind);
            var top = new ServerDataJson.Fields(source, _kind, root, string.Empty, _topItems);

            var zones = new List<MapWeaponFreeZone>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var nodes = top.Array(ZonesItem);
            for (var i = 0; i < nodes.Count; i++)
            {
                var fields = top.Element(nodes[i], ZonesItem, i, _zoneItems);
                var id = fields.ToText(fields.ItemPath(IdItem), fields.Require(IdItem));
                if (!ids.Add(id))
                {
                    throw fields.Rejected(fields.ItemPath(IdItem), $"another zone already has the id '{id}'");
                }

                WeaponFreeZoneSpec spec;
                try
                {
                    spec = new WeaponFreeZoneSpec(
                        fields.ReadEnum(ReactionItem, WeaponFreeZoneSpec.DefaultReaction),
                        fields.ReadFloat(HostileAfterSecondsItem, WeaponFreeZoneSpec.DefaultHostileAfterSeconds));
                }
                catch (ArgumentException e)
                {
                    // WeaponFreeZoneSpec の引数名は項目名と同じ
                    throw fields.Rejected(e.ParamName == null ? fields.Path : fields.ItemPath(e.ParamName), e.Message, e);
                }

                try
                {
                    zones.Add(new MapWeaponFreeZone(id, fields.ReadVector3(MinItem), fields.ReadVector3(MaxItem), spec));
                }
                catch (ArgumentException e)
                {
                    // MapWeaponFreeZone の引数名は項目名と同じ
                    throw fields.Rejected(e.ParamName == null ? fields.Path : fields.ItemPath(e.ParamName), e.Message, e);
                }
            }

            return new NetMapWeaponFreeZones(zones);
        }

        public static string Write(NetMapWeaponFreeZones zones)
        {
            if (zones == null)
            {
                throw new ArgumentNullException(nameof(zones));
            }

            var writer = new JsonWriter();
            writer.BeginArray(ZonesItem);
            foreach (var zone in zones.Zones)
            {
                writer.BeginElement();
                writer.String(IdItem, zone.Id);
                writer.Vector3(MinItem, zone.Min);
                writer.Vector3(MaxItem, zone.Max);
                writer.Enum(ReactionItem, zone.Spec.Reaction);
                writer.Float(HostileAfterSecondsItem, zone.Spec.HostileAfterSeconds);
                writer.EndObject();
            }

            writer.EndArray();
            return writer.Finish();
        }
    }
}

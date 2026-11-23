using System;
using System.Collections.Generic;
using System.Globalization;

namespace Shooter.Core
{
    // 板の大きさと当たりの組は NavMesh や .fpsmap の形からは引けないので、Unity の Editor がシーンのドアから書き出したものを読む。
    // 並びはシーンの階層順で、ドアの状態のやりとりはドアをこの並びの番号で指す。
    public sealed class NetMapDoors
    {
        public const string Extension = ".doors.json";

        public const string DoorsItem = "doors";
        public const string PositionItem = "position";
        public const string FacingItem = "facing";
        public const string PanelSizeItem = "panelSize";
        public const string ColliderGroupItem = "colliderGroup";

        private const string _kind = "map doors JSON";

        private static readonly string[] _topItems = { DoorsItem };

        private static readonly string[] _doorItems = { PositionItem, FacingItem, PanelSizeItem, ColliderGroupItem };

        public NetMapDoors(IReadOnlyList<MapDoor> doors)
        {
            if (doors == null)
            {
                throw new ArgumentNullException(nameof(doors));
            }

            var copy = new List<MapDoor>(doors.Count);
            var groups = new HashSet<int>();
            foreach (var door in doors)
            {
                if (door == null)
                {
                    throw new ArgumentException("doors must not contain null", nameof(doors));
                }

                // 2 枚のドアが同じ組を指すと、片方を破るともう片方の板の当たりまで外れる
                if (!groups.Add(door.ColliderGroup))
                {
                    throw new ArgumentException(
                        "doors[" + copy.Count.ToString(CultureInfo.InvariantCulture) + "] shares colliderGroup "
                        + door.ColliderGroup.ToString(CultureInfo.InvariantCulture) + " with another door",
                        nameof(doors));
                }

                copy.Add(door);
            }

            Doors = copy.AsReadOnly();
        }

        public static NetMapDoors None { get; } = new NetMapDoors(Array.Empty<MapDoor>());

        public IReadOnlyList<MapDoor> Doors { get; }

        public static NetMapDoors Parse(string text, string sourceName)
        {
            var source = ServerDataJson.SourceOf(sourceName, _kind);
            var root = ServerDataJson.ParseDocument(text, source, _kind);
            var top = new ServerDataJson.Fields(source, _kind, root, string.Empty, _topItems);

            var doors = new List<MapDoor>();
            var groups = new HashSet<int>();
            var nodes = top.Array(DoorsItem);
            for (var i = 0; i < nodes.Count; i++)
            {
                var fields = top.Element(nodes[i], DoorsItem, i, _doorItems);
                var group = fields.ToInt(fields.ItemPath(ColliderGroupItem), fields.Require(ColliderGroupItem));
                MapDoor door;
                try
                {
                    door = new MapDoor(fields.ReadVector3(PositionItem), fields.ReadVector3(FacingItem), fields.ReadVector3(PanelSizeItem), group);
                }
                catch (ArgumentException e)
                {
                    // MapDoor の引数名は項目名と同じ
                    throw fields.Rejected(e.ParamName == null ? fields.Path : fields.ItemPath(e.ParamName), e.Message, e);
                }

                if (!groups.Add(group))
                {
                    throw fields.Rejected(fields.ItemPath(ColliderGroupItem), "is used by another door; each door needs its own collider group");
                }

                doors.Add(door);
            }

            return new NetMapDoors(doors);
        }

        public static string Write(NetMapDoors doors)
        {
            if (doors == null)
            {
                throw new ArgumentNullException(nameof(doors));
            }

            var writer = new JsonWriter();
            writer.BeginArray(DoorsItem);
            foreach (var door in doors.Doors)
            {
                writer.BeginElement();
                writer.Vector3(PositionItem, door.Position);
                writer.Vector3(FacingItem, door.Facing);
                writer.Vector3(PanelSizeItem, door.PanelSize);
                writer.Int(ColliderGroupItem, door.ColliderGroup);
                writer.EndObject();
            }

            writer.EndArray();
            return writer.Finish();
        }
    }
}

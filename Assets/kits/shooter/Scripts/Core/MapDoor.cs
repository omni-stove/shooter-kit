using System;
using System.Numerics;
using GameKit.World;

namespace Shooter.Core
{
    // Unity 抜きのサーバーがシーンのドアと同じ所で突入を確かめ、同じ板の当たりを外せるよう、シーンからではなく NetMapDoors で書き出したものを読む。
    public sealed class MapDoor
    {
        public MapDoor(Vector3 position, Vector3 facing, Vector3 panelSize, int colliderGroup)
        {
            if (!IsFinite(position))
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, "position must be finite");
            }

            // 突入の向こう側の箱は水平に置くので、傾いた板でも水平な向きに直す
            var flat = new Vector3(facing.X, 0f, facing.Z);
            var length = flat.Length();
            if (!IsFinite(facing) || !(length > 1e-4f))
            {
                throw new ArgumentOutOfRangeException(nameof(facing), facing, "facing must have a finite, non-zero horizontal part");
            }

            if (!IsFinite(panelSize) || !(panelSize.X > 0f) || !(panelSize.Y > 0f) || !(panelSize.Z > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(panelSize), panelSize, "panelSize must be finite and greater than 0 (width, height, thickness)");
            }

            if (!MapColliderGroups.IsDoor(colliderGroup))
            {
                throw new ArgumentOutOfRangeException(nameof(colliderGroup), colliderGroup, "colliderGroup must be a door group (MapColliderGroups.Door)");
            }

            Position = position;
            Facing = flat / length;
            PanelSize = panelSize;
            ColliderGroup = colliderGroup;
        }

        public Vector3 Position { get; }

        public Vector3 Facing { get; }

        public Vector3 PanelSize { get; }

        public int ColliderGroup { get; }

        private static bool IsFinite(Vector3 v)
        {
            return !float.IsNaN(v.X) && !float.IsInfinity(v.X)
                && !float.IsNaN(v.Y) && !float.IsInfinity(v.Y)
                && !float.IsNaN(v.Z) && !float.IsInfinity(v.Z);
        }
    }
}

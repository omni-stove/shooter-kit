using System;

namespace GameKit.World
{
    // .fpsmap には名前を持たせないので、サーバーはシーンの並び順 (剛体・建物の何番目か) と部品の添字から番号を引く。
    // 0 は動かない物、1〜65535 は剛体、65536 以上は (建物 + 1) × 65536 + 部品、負の数は -(ドア + 1)。
    // ドアを負の数にしたのは、ドアの無いマップの書き出した .fpsmap の番号を変えずに足すため。
    public static class MapColliderGroups
    {
        public const int Static = WorldCollider.StaticGroup;

        public const int MaxBodies = _pieceStride - 1;

        public const int MaxDoors = _pieceStride - 1;

        public const int MaxPiecesPerStructure = _pieceStride;

        public const int MaxStructures = (int.MaxValue / _pieceStride) - 1;

        private const int _pieceStride = 1 << 16;

        public static int Body(int bodyIndex)
        {
            if (bodyIndex < 0 || bodyIndex >= MaxBodies)
            {
                throw new ArgumentOutOfRangeException(nameof(bodyIndex), bodyIndex, "body index must be 0-" + (MaxBodies - 1));
            }

            return bodyIndex + 1;
        }

        public static int Piece(int structureIndex, int pieceIndex)
        {
            if (structureIndex < 0 || structureIndex >= MaxStructures)
            {
                throw new ArgumentOutOfRangeException(nameof(structureIndex), structureIndex, "structure index must be 0-" + (MaxStructures - 1));
            }

            if (pieceIndex < 0 || pieceIndex >= MaxPiecesPerStructure)
            {
                throw new ArgumentOutOfRangeException(nameof(pieceIndex), pieceIndex, "piece index must be 0-" + (MaxPiecesPerStructure - 1));
            }

            return ((structureIndex + 1) * _pieceStride) + pieceIndex;
        }

        public static int Door(int doorIndex)
        {
            if (doorIndex < 0 || doorIndex >= MaxDoors)
            {
                throw new ArgumentOutOfRangeException(nameof(doorIndex), doorIndex, "door index must be 0-" + (MaxDoors - 1));
            }

            return -(doorIndex + 1);
        }

        public static bool IsBody(int group)
        {
            return group > Static && group < _pieceStride;
        }

        public static bool IsDoor(int group)
        {
            return group < Static && group >= -MaxDoors;
        }

        public static bool TryGetDoor(int group, out int doorIndex)
        {
            if (!IsDoor(group))
            {
                doorIndex = -1;
                return false;
            }

            doorIndex = -group - 1;
            return true;
        }

        public static bool IsPiece(int group)
        {
            return group >= _pieceStride;
        }

        public static bool TryGetPiece(int group, out int structureIndex, out int pieceIndex)
        {
            if (!IsPiece(group))
            {
                structureIndex = -1;
                pieceIndex = -1;
                return false;
            }

            structureIndex = (group / _pieceStride) - 1;
            pieceIndex = group % _pieceStride;
            return true;
        }
    }
}

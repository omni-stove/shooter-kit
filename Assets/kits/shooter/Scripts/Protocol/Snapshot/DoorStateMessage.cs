using System;
using GameKit.Net;
using Shooter.Core;

namespace Shooter.Protocol
{
    // ドアは破るときと仕掛けるときにしか変わらないので、StructureStateMessage と同じく毎 tick のスナップショットに載せず、
    // 変わったときと途中参加のときに確実な届け方で送る。1 枚ずつ送らずに全部を載せるのは、届く順が入れ違っても最後の 1 通で揃うため
    public sealed class DoorStateMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        // 偽の数で読み手に大きな配列を作らせないための上限。1 つのマップに置くドアはこれより十分少ない
        public const int MaxDoors = 1024;

        private Door[] _doors = Array.Empty<Door>();

        public ushort Id => MessageIds.DoorState;

        public byte Version => CurrentVersion;

        public int DoorCount { get; private set; }

        public Door this[int index]
        {
            get
            {
                if ((uint)index >= (uint)DoorCount)
                {
                    throw new ArgumentOutOfRangeException(nameof(index), index, "index must be below " + DoorCount);
                }

                return _doors[index];
            }
        }

        public void Clear()
        {
            DoorCount = 0;
        }

        public void Add(BreachDoorPhase phase, bool farSideFront)
        {
            if (!Enum.IsDefined(typeof(BreachDoorPhase), phase))
            {
                throw new ArgumentOutOfRangeException(nameof(phase), phase, "phase is not a defined BreachDoorPhase");
            }

            if (DoorCount >= MaxDoors)
            {
                throw new InvalidOperationException("at most " + MaxDoors + " doors fit");
            }

            Grow(DoorCount + 1);
            _doors[DoorCount] = new Door(phase, farSideFront);
            DoorCount++;
        }

        public void Write(MessageWriter writer)
        {
            writer.WriteUInt16((ushort)DoorCount);
            for (var i = 0; i < DoorCount; i++)
            {
                writer.WriteByte((byte)_doors[i].Phase);
                writer.WriteBool(_doors[i].FarSideFront);
            }
        }

        public void Read(MessageReader reader)
        {
            DoorCount = 0;
            var count = reader.ReadUInt16();
            if (count > MaxDoors)
            {
                throw new MessageReadException("door count must be at most " + MaxDoors + " (got " + count + ")");
            }

            Grow(count);
            for (var i = 0; i < count; i++)
            {
                var phase = (BreachDoorPhase)reader.ReadByteBelow(EnumRange<BreachDoorPhase>.Count, "door phase");
                _doors[i] = new Door(phase, reader.ReadBool());
            }

            DoorCount = count;
        }

        private void Grow(int count)
        {
            if (count > _doors.Length)
            {
                Array.Resize(ref _doors, Math.Max(count, _doors.Length * 2));
            }
        }

        // 向こう側 (破った人のいない側) が板の前 (+Z) か。板を開く向きと爆薬を貼る面を、クライアントがオフラインの BreachDoor と同じに選ぶため
        public readonly struct Door
        {
            public Door(BreachDoorPhase phase, bool farSideFront)
            {
                Phase = phase;
                FarSideFront = farSideFront;
            }

            public BreachDoorPhase Phase { get; }

            public bool FarSideFront { get; }
        }
    }
}

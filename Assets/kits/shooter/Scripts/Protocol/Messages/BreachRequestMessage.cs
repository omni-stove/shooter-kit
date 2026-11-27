using System;
using GameKit.Net;
using Shooter.Core;

namespace Shooter.Protocol
{
    // 始めるか取りやめるかを切り替えにせず向きで送るのは、落ちた要求や重なった要求をサーバーが受け直しても、仕掛けが逆転しないため。
    // 位置と秒数は載せない。サーバーが体の位置と breach.json の秒数で確かめて進める
    public sealed class BreachRequestMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.BreachRequest;

        public byte Version => CurrentVersion;

        public int DoorIndex { get; set; }

        public BreachMethod Method { get; set; }

        public bool Cancel { get; set; }

        public void Write(MessageWriter writer)
        {
            if (DoorIndex < 0 || DoorIndex > ushort.MaxValue)
            {
                throw new InvalidOperationException("door index must be 0-" + ushort.MaxValue);
            }

            if ((int)Method < 0 || (int)Method >= EnumRange<BreachMethod>.Count)
            {
                throw new InvalidOperationException("breach method " + (int)Method + " is not defined");
            }

            writer.WriteUInt16((ushort)DoorIndex);
            writer.WriteByte((byte)Method);
            writer.WriteBool(Cancel);
        }

        public void Read(MessageReader reader)
        {
            DoorIndex = reader.ReadUInt16();
            Method = (BreachMethod)reader.ReadByteBelow(EnumRange<BreachMethod>.Count, "breach method");
            Cancel = reader.ReadBool();
        }
    }
}

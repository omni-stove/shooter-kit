namespace Shooter.Protocol
{
    // 押したことを切り替えにせず向きで送るのは、落ちた要求や重なった要求をサーバーが受け直しても、しまう・構えるが逆転しないため。
    // 秒数は載せない。サーバーが持っている武器の秒数で進める
    public sealed class HolsterRequestMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.HolsterRequest;

        public byte Version => CurrentVersion;

        public HolsterAction Action { get; set; }

        public void Write(MessageWriter writer)
        {
            if ((int)Action >= EnumRange<HolsterAction>.Count)
            {
                throw new System.InvalidOperationException("holster action " + (int)Action + " is not defined");
            }

            writer.WriteByte((byte)Action);
        }

        public void Read(MessageReader reader)
        {
            Action = (HolsterAction)reader.ReadByteBelow(EnumRange<HolsterAction>.Count, "holster action");
        }
    }
}

namespace Shooter.Protocol
{
    // オフラインのキーは押すたびに切り替えるが、ここでは点けたい向きで送る。落ちた要求や重なった要求をサーバーが受け直しても、点灯が逆転しないため。
    // ライトの明るさや向きは載せない。サーバーが今の武器に付けた物から確かめる
    public sealed class WeaponLightRequestMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.WeaponLightRequest;

        public byte Version => CurrentVersion;

        public bool On { get; set; }

        public void Write(MessageWriter writer)
        {
            writer.WriteBool(On);
        }

        public void Read(MessageReader reader)
        {
            On = reader.ReadBool();
        }
    }
}

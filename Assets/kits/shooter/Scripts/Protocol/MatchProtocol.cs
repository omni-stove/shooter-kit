using GameKit.Net;
using Shooter.Core;

namespace Shooter.Protocol
{
    public static class MatchProtocol
    {
        // メッセージの組み合わせの版をつなぐときに確かめるのは、個々のメッセージの版 (IMessage.Version) だけだと、
        // つないだ後に初めて読めない種類が届くまで食い違いに気付けないため。
        public const ushort Version = 42;

        // Game Kit の既定ではなくシューターの鍵にするのは、ほかのゲームのサーバーにつながらないようにするため。
        public const string ConnectionKey = "shooter-match";

        public const int MaxReasonBytes = 128;

        public const int MaxContentIdBytes = 64;

        public const int MaxChatBytes = 512;

        public const int MaxReportCommentBytes = 512;

        // 断った理由を DisconnectNotice.Classify が分けられるよう、シューターの切断理由の形で送らせる。
        public static UdpTransportOptions TransportOptions()
        {
            return new UdpTransportOptions
            {
                ConnectionKey = ConnectionKey,
                ProtocolMismatchReason = DisconnectNotice.ProtocolMismatchReason,
            };
        }
    }
}

using GameKit.Core;

namespace Shooter.Core
{
    // データ (Tuning/server-lobby・server-data/tuning/server-lobby.json) が無いか壊れているときにシューターキットが使う値。
    // core の中立の既定値 (ServerLobbySpec.Default) に落とさないのは、それだと既存のサーバーとクライアントのバケット名が変わり、互いに見つからなくなるため。
    public static class ShooterServerLobby
    {
        public const string BucketPrefix = "shooter-server:";

        public const string DisplayName = "shooter-server";

        public static ServerLobbySpec Default { get; } = new ServerLobbySpec(BucketPrefix, DisplayName);

        // 書いていない項目もシューターキットの値で埋める。
        public static ServerLobbySpec Parse(string text, string sourceName)
        {
            return ServerLobbyJson.Parse(text, sourceName, Default);
        }
    }
}

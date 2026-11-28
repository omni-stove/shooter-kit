using System;

namespace GameKit.Core
{
    // バケットの接頭辞とサーバーの表示名はゲームごとに替える値なので、ServerLobby の定数にせずデータ (Tuning/server-lobby) から読む。
    public sealed class ServerLobbySpec
    {
        // キットに依らない既定値。キットは自分の値を自分のデータで与える (シューターキットは "shooter-server:")。
        public const string DefaultBucketPrefix = "game-server:";

        public const string DefaultDisplayName = "game-server";

        // 版を足したバケット名が長くなりすぎないよう、接頭辞に上限を置く。
        public const int MaxBucketPrefixLength = 32;

        public const int MaxDisplayNameLength = 32;

        public static ServerLobbySpec Default { get; } = new ServerLobbySpec();

        // 引数名は JSON の項目名と同じにして、例外の ParamName をそのまま項目名として返せるようにする (ServerLobbyJson が返す)。
        public ServerLobbySpec(string bucketPrefix = DefaultBucketPrefix, string displayName = DefaultDisplayName)
        {
            if (!IsBucketPrefix(bucketPrefix))
            {
                throw new ArgumentException(nameof(bucketPrefix) + " must be " + BucketPrefixRule, nameof(bucketPrefix));
            }

            if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > MaxDisplayNameLength || displayName.Trim() != displayName)
            {
                throw new ArgumentException(
                    nameof(displayName) + " must be 1-" + MaxDisplayNameLength + " characters without leading or trailing spaces", nameof(displayName));
            }

            BucketPrefix = bucketPrefix;
            DisplayName = displayName;
        }

        // 同じ EOS のサンドボックスを使う別のゲームのロビーと混ざらないよう、バケット名の前に付ける。
        public string BucketPrefix { get; }

        // プレイヤーと見分けるため、サーバーのユーザーの表示名は分ける。
        public string DisplayName { get; }

        public static string BucketPrefixRule => "1-" + MaxBucketPrefixLength + " characters of a-z, 0-9, '-', '_', '.' and ':'";

        // 検索の鍵にそのまま使うので、大文字や空白で同じゲームのバケットが割れないよう書き方を絞る。
        public static bool IsBucketPrefix(string bucketPrefix)
        {
            if (string.IsNullOrEmpty(bucketPrefix) || bucketPrefix.Length > MaxBucketPrefixLength)
            {
                return false;
            }

            foreach (var c in bucketPrefix)
            {
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_' || c == '.' || c == ':'))
                {
                    return false;
                }
            }

            return true;
        }
    }
}

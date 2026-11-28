namespace GameKit.Core
{
    // 地域の名前の決まりを設定 (Shooter.Core.ControlSettings) から分けて持つのは、サーバーのロビー (ServerLobby・LobbySelector) が設定の型に縛られないようにするため。
    public static class ServerRegion
    {
        // 保存した文字列を見て地域を決めていないと分かるよう、空文字や null でなく名前で持つ。
        public const string Auto = "auto";

        public const int MaxLength = 32;

        // サーバーが --region で載せる名前と突き合わせるので、大文字小文字や空白の揺れで別の地域にならないよう、書ける文字を絞る。
        public static bool IsValid(string region)
        {
            if (string.IsNullOrEmpty(region) || region.Length > MaxLength)
            {
                return false;
            }

            for (var i = 0; i < region.Length; i++)
            {
                var c = region[i];
                var letterOrDigit = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
                if (!letterOrDigit && (c != '-' || i == 0 || i == region.Length - 1))
                {
                    return false;
                }
            }

            return true;
        }
    }
}

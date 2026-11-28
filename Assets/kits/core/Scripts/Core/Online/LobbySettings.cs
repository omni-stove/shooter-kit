using System;

namespace GameKit.Core
{
    // ジャンルの試合の設定 (Shooter.Core.NetMatchSettings など) に項目を並べずに分けて持つのは、ロビーと接続の層 (ServerLobby・ConnectionApproval) が、モードの設定の型に縛られないようにするため。
    public sealed class LobbySettings
    {
        public const int DefaultMaxPlayers = 8;

        public const int MinMaxPlayers = 1;

        // GameKit.Core からはキットの Protocol を参照できないので、Shooter.Protocol.SnapshotMessage.MaxPlayers の値を写す
        public const int MaxMaxPlayers = 64;

        public static LobbySettings Default { get; } = new LobbySettings();

        // 引数名は試合のファイルの項目名と同じにして、例外の ParamName をそのまま項目名として返せるようにする (Shooter.Core.NetMatchFile が返す)。
        public LobbySettings(int maxPlayers = DefaultMaxPlayers, bool privateMatch = false, string joinCode = null)
        {
            if (maxPlayers < MinMaxPlayers || maxPlayers > MaxMaxPlayers)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxPlayers), maxPlayers, nameof(maxPlayers) + " must be between " + MinMaxPlayers + " and " + MaxMaxPlayers);
            }

            joinCode = string.IsNullOrEmpty(joinCode) ? null : joinCode;
            if (joinCode != null && !ConnectionExtras.IsValidField(joinCode, ConnectionExtras.MaxJoinCodeLength))
            {
                throw new ArgumentException(
                    nameof(joinCode) + " must be 1-" + ConnectionExtras.MaxJoinCodeLength + " characters of [0-9A-Za-z_-]", nameof(joinCode));
            }

            MaxPlayers = maxPlayers;
            PrivateMatch = privateMatch;
            JoinCode = joinCode;
        }

        public int MaxPlayers { get; }

        public bool PrivateMatch { get; }

        public string JoinCode { get; }

        public LobbySettings WithMaxPlayers(int maxPlayers) => new LobbySettings(maxPlayers, PrivateMatch, JoinCode);

        public LobbySettings WithPrivacy(bool privateMatch, string joinCode) => new LobbySettings(MaxPlayers, privateMatch, joinCode);
    }
}

using System;
using System.Collections.Generic;

namespace GameKit.Core
{
    public static class ServerLobby
    {
        public const string ServerKey = "server";

        public const string ModeKey = "mode";

        public const string VersionKey = "version";

        public const string PlayersKey = "players";

        public const string PrivateKey = "private";

        public const string JoinCodeKey = "join_code";

        public const string SkillAverageKey = "skill_average";

        public const string VoiceEnabledKey = "voice_enabled";

        public const string RegionKey = "region";

        public const string MapKey = "map";

        // mode を渡さない呼び出しのロビーに載せるキー。シューターキットの目的の無い倒し合い (Shooter.Core.ServerLobbyModes.TeamDeathmatchMode) と同じ値なのは、目的を載せる前からこの値だったので、古いクライアントの一覧でも同じに見えるようにするため。
        public const string DefaultMode = "tdm";

        public const int MaxModeLength = 32;

        public const float RetryIntervalSeconds = 5f;

        public const float TimeoutSeconds = 60f;

        // 版の違うビルド同士が同じバケットに入らないよう、版を含める。
        public static string BucketId(ServerLobbySpec lobby, string version)
        {
            if (lobby == null)
            {
                throw new ArgumentNullException(nameof(lobby));
            }

            return lobby.BucketPrefix + LobbyLaunchOptions.BucketVersion(version);
        }

        public static IReadOnlyList<LobbyAttribute> Attributes(string endpoint, string version, int players)
        {
            return Attributes(endpoint, version, players, LobbySettings.Default);
        }

        public static IReadOnlyList<LobbyAttribute> Attributes(string endpoint, string version, int players, LobbySettings match)
        {
            return Attributes(endpoint, version, players, match, null);
        }

        public static IReadOnlyList<LobbyAttribute> Attributes(
            string endpoint, string version, int players, LobbySettings match, int? skillAverage)
        {
            return Attributes(endpoint, version, players, match, skillAverage, null);
        }

        public static IReadOnlyList<LobbyAttribute> Attributes(
            string endpoint, string version, int players, LobbySettings match, int? skillAverage, string region)
        {
            return Attributes(endpoint, version, players, match, skillAverage, region, null);
        }

        public static IReadOnlyList<LobbyAttribute> Attributes(
            string endpoint, string version, int players, LobbySettings match, int? skillAverage, string region, string map)
        {
            return Attributes(endpoint, version, players, match, skillAverage, region, map, null);
        }

        // null を拒まず DefaultMode にするのは、mode を渡さない呼び出しが目的を載せる前と同じロビーを作るようにするため。
        public static IReadOnlyList<LobbyAttribute> Attributes(
            string endpoint, string version, int players, LobbySettings match, int? skillAverage, string region, string map, string mode)
        {
            if (mode != null && !IsModeName(mode))
            {
                throw new ArgumentException("'" + mode + "' is not a server mode (" + ModeRule + ")", nameof(mode));
            }

            if (region != null && !IsRegionName(region))
            {
                throw new ArgumentException("'" + region + "' is not a server region (" + RegionRule + ")", nameof(region));
            }

            if (map != null && !NetLaunchOptions.IsMapName(map))
            {
                throw new ArgumentException("'" + map + "' is not a map name", nameof(map));
            }

            if (string.IsNullOrWhiteSpace(endpoint))
            {
                throw new ArgumentException("must not be null or blank", nameof(endpoint));
            }

            if (match == null)
            {
                throw new ArgumentNullException(nameof(match));
            }

            var attributes = new List<LobbyAttribute>
            {
                LobbyAttribute.OfText(ServerKey, endpoint),
                LobbyAttribute.OfText(ModeKey, mode ?? DefaultMode),
                LobbyAttribute.OfText(VersionKey, LobbyLaunchOptions.BucketVersion(version)),
                PlayersAttribute(players),
                // サーバーのロビーはいつも RTC 部屋を開いて作るので 1。
                LobbyAttribute.OfNumber(VoiceEnabledKey, 1),
            };

            if (match.PrivateMatch)
            {
                // コードが無くても非公開の印は付け、誰も入れないサーバーを Quick Match が選ばないようにする
                attributes.Add(LobbyAttribute.OfNumber(PrivateKey, 1));
                if (JoinCode.TryNormalize(match.JoinCode, out var code))
                {
                    attributes.Add(LobbyAttribute.OfText(JoinCodeKey, code));
                }
            }

            if (skillAverage.HasValue)
            {
                attributes.Add(SkillAverageAttribute(skillAverage.Value));
            }

            if (region != null)
            {
                attributes.Add(LobbyAttribute.OfText(RegionKey, region));
            }

            if (map != null)
            {
                attributes.Add(MapAttribute(map));
            }

            return attributes;
        }

        // 地図は投票や回しで試合ごとに替わるので、地域と違って作った後も書き直す (ServerLobbyRegistrar.SetMap)。
        public static LobbyAttribute MapAttribute(string map)
        {
            if (!NetLaunchOptions.IsMapName(map))
            {
                throw new ArgumentException("'" + map + "' is not a map name", nameof(map));
            }

            return LobbyAttribute.OfText(MapKey, map);
        }

        // 目的は次の試合のルールで替わるので、地図と同じく作った後も書き直す (ServerLobbyRegistrar.SetMode)。
        public static LobbyAttribute ModeAttribute(string mode)
        {
            if (!IsModeName(mode))
            {
                throw new ArgumentException("'" + mode + "' is not a server mode (" + ModeRule + ")", nameof(mode));
            }

            return LobbyAttribute.OfText(ModeKey, mode);
        }

        public static string ModeRule => "a-z, 0-9 and '-' inside, up to " + MaxModeLength + " characters";

        // 表示の鍵とログの行にそのまま使うので、空白や記号の入ったキーは載せない。
        public static bool IsModeName(string mode)
        {
            if (string.IsNullOrEmpty(mode) || mode.Length > MaxModeLength || mode[0] == '-' || mode[mode.Length - 1] == '-')
            {
                return false;
            }

            foreach (var c in mode)
            {
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-'))
                {
                    return false;
                }
            }

            return true;
        }

        public static string RegionRule =>
            "a-z, 0-9 and '-' inside, up to " + ServerRegion.MaxLength + " characters, not '" + ServerRegion.Auto + "'";

        // 設定の地域と同じ書き方に絞るのは、名前の揺れで同じ地域のサーバーを別の地域として後回しにしないため。
        // auto は「どこでもよい」という設定の値なので、サーバーの地域の名前には使わせない。
        public static bool IsRegionName(string region)
        {
            return ServerRegion.IsValid(region) && region != ServerRegion.Auto;
        }

        public static LobbyAttribute SkillAverageAttribute(int skillAverage)
        {
            return LobbyAttribute.OfNumber(SkillAverageKey, skillAverage);
        }

        // 値を送らなかった人は数えない。0 として数えると、値の無い人がいるだけで平均が下がるため。
        public static int? SkillAverage(IEnumerable<int?> ratings)
        {
            if (ratings == null)
            {
                throw new ArgumentNullException(nameof(ratings));
            }

            long sum = 0;
            var count = 0;
            foreach (var rating in ratings)
            {
                if (!rating.HasValue)
                {
                    continue;
                }

                sum += rating.Value;
                count++;
            }

            if (count == 0)
            {
                return null;
            }

            return (int)Math.Round((double)sum / count, MidpointRounding.AwayFromZero);
        }

        // ボイスのためにプレイヤーもメンバーとして入り、オーナーのサーバー自身も 1 人分を使うので 1 足す。
        public static int MaxMembers(int maxPlayers)
        {
            if (maxPlayers < LobbySettings.MinMaxPlayers || maxPlayers > LobbySettings.MaxMaxPlayers)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxPlayers), maxPlayers,
                    "must be between " + LobbySettings.MinMaxPlayers + " and " + LobbySettings.MaxMaxPlayers);
            }

            return Math.Min(maxPlayers + 1, LobbyLaunchOptions.MaxMaxPlayers);
        }

        // ボイスのあるロビーではサーバー自身もメンバーとして 1 人分を使うので、その分を除く。
        public static int PlayerSlots(LobbyInfo lobby)
        {
            if (lobby == null)
            {
                throw new ArgumentNullException(nameof(lobby));
            }

            return lobby.VoiceEnabled ? lobby.MaxMembers - 1 : lobby.MaxMembers;
        }

        public static LobbyAttribute PlayersAttribute(int players)
        {
            if (players < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(players), players, "must not be negative");
            }

            return LobbyAttribute.OfNumber(PlayersKey, players);
        }

        // サーバーは UDP で待ち受けるときだけロビーに載る (-register-lobby は -transport udp だけ) ので、UDP でつなぐ。
        public static NetLaunchOptions ConnectOptions(LobbyInfo lobby)
        {
            if (lobby == null)
            {
                throw new ArgumentNullException(nameof(lobby));
            }

            lobby.TryGetText(ServerKey, out var endpoint);
            if (!NetLaunchOptions.TryParseEndpoint(endpoint, out var address, out var port))
            {
                throw new ArgumentException("the server address '" + endpoint + "' is not an IPv4 address:port", nameof(lobby));
            }

            return new NetLaunchOptions(NetLaunchMode.Client, address, port, NetTransportKind.Udp, null);
        }
    }
}

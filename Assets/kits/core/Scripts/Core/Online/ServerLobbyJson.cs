using System;
using System.Collections.Generic;

namespace GameKit.Core
{
    public static class ServerLobbyJson
    {
        public const string ResourcePath = "Tuning/server-lobby";

        public const string FileName = "server-lobby.json";

        public const string BucketPrefixItem = "bucketPrefix";

        public const string DisplayNameItem = "displayName";

        private const string _kind = "server lobby JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[]
        {
            BucketPrefixItem,
            DisplayNameItem,
        };

        public static ServerLobbySpec Parse(string text, string sourceName)
        {
            return Parse(text, sourceName, ServerLobbySpec.Default);
        }

        // 書いていない項目をキットの既定値で埋められるよう、既定値を受け取る口を持つ。
        public static ServerLobbySpec Parse(string text, string sourceName, ServerLobbySpec defaults)
        {
            if (defaults == null)
            {
                throw new ArgumentNullException(nameof(defaults));
            }

            var source = ServerDataJson.SourceOf(sourceName, _kind);
            try
            {
                var fields = new ServerDataJson.Fields(source, _kind, ServerDataJson.ParseDocument(text, source, _kind), string.Empty, ItemNames);
                try
                {
                    return new ServerLobbySpec(
                        fields.ReadString(BucketPrefixItem, defaults.BucketPrefix),
                        fields.ReadString(DisplayNameItem, defaults.DisplayName));
                }
                catch (ArgumentException e)
                {
                    throw fields.Rejected(ItemOf(e.ParamName), e.Message, e);
                }
            }
            catch (ServerDataJsonException e)
            {
                // KitTuning は TuningJsonException だけを「読めないので既定値で続ける」と扱う。
                var inner = e.InnerException is ArgumentException rejected ? (Exception)rejected : e;
                throw new TuningJsonException(e.SourceName, e.Item, e.Message, inner);
            }
        }

        private static string ItemOf(string paramName)
        {
            foreach (var item in ItemNames)
            {
                if (string.Equals(item, paramName, StringComparison.Ordinal))
                {
                    return item;
                }
            }

            return null;
        }
    }
}

using System;
using System.Collections.Generic;
using GameKit.Core;

namespace Shooter.Core
{
    public static class BarkJson
    {
        public const string ResourcePath = "Tuning/barks";

        public const string KindCooldownSecondsItem = "kindCooldownSeconds";

        public const string SpeakerCooldownSecondsItem = "speakerCooldownSeconds";

        public const string MinIntervalSecondsItem = "minIntervalSeconds";

        public const string CrowdRadiusItem = "crowdRadius";

        public const string MaxSpeakersInCrowdItem = "maxSpeakersInCrowd";

        public const string HearingDistanceItem = "hearingDistance";

        public const string GrenadeWarningRadiusItem = "grenadeWarningRadius";

        public const string SpeakSecondsItem = "speakSeconds";

        private const string _kind = "bark JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[]
        {
            KindCooldownSecondsItem,
            SpeakerCooldownSecondsItem,
            MinIntervalSecondsItem,
            CrowdRadiusItem,
            MaxSpeakersInCrowdItem,
            HearingDistanceItem,
            GrenadeWarningRadiusItem,
            SpeakSecondsItem,
        };

        public static IReadOnlyList<string> KindItemNames { get; } = KindNames();

        // 列挙の名前から作らないのは、BarkKind の名前を変えても barks.json の項目名が変わらないようにするため。
        public static string KindItem(BarkKind kind)
        {
            switch (kind)
            {
                case BarkKind.Spotted:
                    return "spotted";
                case BarkKind.LostTarget:
                    return "lostTarget";
                case BarkKind.Reloading:
                    return "reloading";
                case BarkKind.Grenade:
                    return "grenade";
                case BarkKind.TakingFire:
                    return "takingFire";
                case BarkKind.MovingToCover:
                    return "movingToCover";
                case BarkKind.AllyDown:
                    return "allyDown";
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "kind is not a BarkKind");
            }
        }

        public static BarkSpec Parse(string text, string sourceName)
        {
            var source = ServerDataJson.SourceOf(sourceName, _kind);
            try
            {
                var fields = new ServerDataJson.Fields(source, _kind, ServerDataJson.ParseDocument(text, source, _kind), string.Empty, ItemNames);
                var kindCooldownSeconds = ReadKindCooldownSeconds(fields);
                var speakerCooldownSeconds = fields.ReadFloat(SpeakerCooldownSecondsItem, BarkSpec.DefaultSpeakerCooldownSeconds);
                var minIntervalSeconds = fields.ReadFloat(MinIntervalSecondsItem, BarkSpec.DefaultMinIntervalSeconds);
                var crowdRadius = fields.ReadFloat(CrowdRadiusItem, BarkSpec.DefaultCrowdRadius);
                var maxSpeakersInCrowd = fields.ReadInt(MaxSpeakersInCrowdItem, BarkSpec.DefaultMaxSpeakersInCrowd);
                var hearingDistance = fields.ReadFloat(HearingDistanceItem, BarkSpec.DefaultHearingDistance);
                var grenadeWarningRadius = fields.ReadFloat(GrenadeWarningRadiusItem, BarkSpec.DefaultGrenadeWarningRadius);
                var speakSeconds = fields.ReadFloat(SpeakSecondsItem, BarkSpec.DefaultSpeakSeconds);
                try
                {
                    return new BarkSpec(
                        kindCooldownSeconds,
                        speakerCooldownSeconds,
                        minIntervalSeconds,
                        crowdRadius,
                        maxSpeakersInCrowd,
                        hearingDistance,
                        grenadeWarningRadius,
                        speakSeconds);
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

        private static Dictionary<BarkKind, float> ReadKindCooldownSeconds(ServerDataJson.Fields fields)
        {
            var kinds = fields.Object(KindCooldownSecondsItem, KindItemNames);
            if (kinds == null)
            {
                return null;
            }

            var values = new Dictionary<BarkKind, float>();
            foreach (var kind in BarkSpec.Kinds)
            {
                var item = KindItem(kind);
                if (!kinds.Has(item))
                {
                    continue;
                }

                var seconds = kinds.ReadFloat(item, BarkSpec.DefaultCooldownSecondsFor(kind));

                // 種類ごとに項目名を付けて断るのは、BarkSpec の検査では kindCooldownSeconds までしか示せないため。
                if (!(seconds >= 0f) || float.IsInfinity(seconds))
                {
                    throw kinds.Rejected(kinds.ItemPath(item), "must be a non-negative finite number");
                }

                values[kind] = seconds;
            }

            return values;
        }

        private static System.Collections.ObjectModel.ReadOnlyCollection<string> KindNames()
        {
            var names = new string[BarkSpec.Kinds.Count];
            for (var i = 0; i < names.Length; i++)
            {
                names[i] = KindItem(BarkSpec.Kinds[i]);
            }

            return Array.AsReadOnly(names);
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

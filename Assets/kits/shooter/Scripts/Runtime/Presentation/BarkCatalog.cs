using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 枠は任意 (PresentationCatalog の規約)。無いか、種類の声が空なら、その種類の掛け声は鳴らさない。
    [CreateAssetMenu(fileName = "BarkCatalog", menuName = "Shooter/Bark Catalog")]
    public sealed class BarkCatalog : ScriptableObject
    {
        public const string CatalogName = "Bark";

        // PresentationCatalog.CatalogResourcePath(CatalogName) と同じ値。SuppressionCatalog と同じく const にしてログにも使えるようにする。
        public const string ResourcePath = PresentationCatalog.ResourceFolder + "/" + CatalogName + PresentationCatalog.CatalogSuffix;

        [SerializeField, Tooltip("掛け声の種類ごとの声。種類は重複させない。鳴らすたびにこの中から選ぶ")]
        private List<BarkVoices> _voices = new List<BarkVoices>();

        public IReadOnlyList<BarkVoices> Voices => _voices ??= new List<BarkVoices>();

        public static BarkCatalog Load()
        {
            var fromContent = GameContent.Pick(c => c.Barks);
            return fromContent != null ? fromContent : Resources.Load<BarkCatalog>(ResourcePath);
        }

        public void SetVoices(IEnumerable<BarkVoices> voices)
        {
            _voices = voices == null ? new List<BarkVoices>() : new List<BarkVoices>(voices);
        }

        // 種類が重なっていたら先のものを使うのは、Validate が知らせるまでの間も同じ声を選び続けるため。
        public IReadOnlyList<AudioClip> ClipsFor(BarkKind kind)
        {
            if (!Enum.IsDefined(typeof(BarkKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "kind is not a BarkKind");
            }

            foreach (var entry in Voices)
            {
                if (entry != null && entry.Kind == kind)
                {
                    return entry.Clips;
                }
            }

            return Array.Empty<AudioClip>();
        }

        public static IReadOnlyList<AudioClip> ClipsFor(BarkCatalog catalog, BarkKind kind)
        {
            // 破棄されたアセットも空として扱うため、Unity の == null で比べる。
            return catalog == null ? Array.Empty<AudioClip>() : catalog.ClipsFor(kind);
        }

        public void Validate()
        {
            var source = string.IsNullOrEmpty(name) ? "(unnamed bark catalog)" : name;
            var problems = new List<string>();
            var seen = new HashSet<BarkKind>();
            for (var i = 0; i < Voices.Count; i++)
            {
                var entry = Voices[i];
                if (entry == null)
                {
                    problems.Add($"voice #{i} is empty");
                    continue;
                }

                var label = $"voice #{i} ({entry.Kind})";
                if (!Enum.IsDefined(typeof(BarkKind), entry.Kind))
                {
                    problems.Add($"voice #{i} has a kind that is not a BarkKind");
                    continue;
                }

                if (!seen.Add(entry.Kind))
                {
                    problems.Add($"{label} uses a kind already used by another entry");
                }

                for (var c = 0; c < entry.Clips.Count; c++)
                {
                    if (entry.Clips[c] == null)
                    {
                        problems.Add($"{label} clip #{c} is missing");
                    }
                }
            }

            if (problems.Count > 0)
            {
                throw new PresentationCatalogException($"bark catalog '{source}': {string.Join("; ", problems)}");
            }
        }
    }

    [Serializable]
    public sealed class BarkVoices
    {
        [SerializeField, Tooltip("掛け声の種類")]
        private BarkKind _kind = BarkKind.Spotted;

        [SerializeField, Tooltip("この種類の声。鳴らすたびにこの中から選ぶ。空ならこの種類は鳴らさない")]
        private List<AudioClip> _clips = new List<AudioClip>();

        public BarkVoices()
        {
        }

        public BarkVoices(BarkKind kind, IEnumerable<AudioClip> clips)
        {
            _kind = kind;
            _clips = clips == null ? new List<AudioClip>() : new List<AudioClip>(clips);
        }

        public BarkKind Kind => _kind;

        public IReadOnlyList<AudioClip> Clips => _clips ??= new List<AudioClip>();
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "WeaponLightCatalog", menuName = "Shooter/Weapon Light Catalog")]
    public sealed class WeaponLightCatalog : ScriptableObject
    {
        public const string CatalogName = "WeaponLight";

        public static readonly string ResourcePath = PresentationCatalog.CatalogResourcePath(CatalogName);

        [SerializeField, Tooltip("ライトの見た目ごとのプレハブ。Id は重複させない")]
        private List<WeaponLightEntry> _lights = new List<WeaponLightEntry>();

        public IReadOnlyList<WeaponLightEntry> Lights => _lights ??= new List<WeaponLightEntry>();

        public static WeaponLightCatalog Load()
        {
            var fromContent = GameContent.Pick(c => c.WeaponLights);
            return fromContent != null ? fromContent : Resources.Load<WeaponLightCatalog>(ResourcePath);
        }

        // 表が無い・Id が無い・プレハブが空を分けずに null にするのは、どれでも呼ぶ側がコードで Spot の Light を作るだけで扱いが変わらないため。
        public static GameObject PrefabFor(WeaponLightCatalog catalog, string visualId)
        {
            return catalog != null && catalog.TryGet(visualId, out var entry) && entry.Prefab != null ? entry.Prefab : null;
        }

        public void SetLights(IEnumerable<WeaponLightEntry> lights)
        {
            _lights = lights == null ? new List<WeaponLightEntry>() : new List<WeaponLightEntry>(lights);
        }

        public bool TryGet(string visualId, out WeaponLightEntry entry)
        {
            if (!string.IsNullOrEmpty(visualId))
            {
                foreach (var candidate in Lights)
                {
                    if (candidate != null && string.Equals(candidate.Id, visualId, StringComparison.Ordinal))
                    {
                        entry = candidate;
                        return true;
                    }
                }
            }

            entry = null;
            return false;
        }
    }

    [Serializable]
    public sealed class WeaponLightEntry
    {
        [SerializeField, Tooltip("ライトの見た目の Id (Tuning/inventory.json の attachment.light.visualId)")]
        private string _id;

        [SerializeField, Tooltip("光と光の筋の見た目。原点を光の出る点に、+Z を照らす向きにする。空ならコードで Spot の Light を作る")]
        private GameObject _prefab;

        public WeaponLightEntry(string id, GameObject prefab)
        {
            _id = id;
            _prefab = prefab;
        }

        public string Id => _id;

        public GameObject Prefab => _prefab;
    }
}

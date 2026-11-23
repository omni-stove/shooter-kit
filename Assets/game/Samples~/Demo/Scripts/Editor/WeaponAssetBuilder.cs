using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEngine;

namespace Shooter.Editor
{
    // サンプルの武器を足すときは JsonFolder に JSON を 1 つ置いて走らせ直すだけでよいよう、JSON から武器のアセットと射撃場の一覧を作る。
    // 既存のアセットは GUID を保って中身だけ更新するので、参照しているシーンやプレハブは壊れない。
    public static class WeaponAssetBuilder
    {
        public static string Folder => WeaponAssets.Folder;

        public static string CatalogPath => WeaponAssets.CatalogPath;
        public static string LoadoutPath => Folder + "/DefaultLoadout.asset";
        public static string LauncherPath => Folder + "/Launcher.asset";
        public static string KnifePath => Folder + "/Knife.asset";

        // JsonFolder の RiotShield.json から作るので、ほかの JSON の武器と同じく Folder に置かれる。
        public static string RiotShieldPath => Folder + "/RiotShield.asset";

        public static string JsonFolder => Folder + "/Json";

        public static string RangeWeaponsPath => Folder + "/RangeWeapons.asset";

        public static string ReferenceJsonFolder => Folder + "/ReferenceJson";

        public static string ReferenceFolder => Folder + "/Reference";

        public static string ReferenceWeaponsPath => Folder + "/ReferenceWeapons.asset";

        public static string RealisticJsonFolder => Folder + "/RealisticJson";

        public static string RealisticFolder => Folder + "/Realistic";

        public static string RealisticWeaponsPath => Folder + "/RealisticWeapons.asset";

        // 既定の武器は手で値を決めず、ゲームの値を参考にした Reference と JsonFolder の武器から選ぶ。
        public static string CarbinePath => ReferenceFolder + "/Carbine556.asset";
        public static string PistolPath => ReferenceFolder + "/CompactPistol.asset";
        public static string MachetePath => Folder + "/Machete.asset";

        public const string MenuPath = "Shooter/Build Weapon Assets";
        public const string LogTag = "[WeaponAssetBuilder]";

        // 再ビルドしても Id が変わらないよう固定する。サーバーとクライアントはこの Id で武器を突き合わせる
        public const string CarbineId = "25974cbc-c194-4f98-b22d-c9d34c3ba6e4";
        public const string PistolId = "343b6024-75f2-4d68-8ac6-d31fad86aa47";
        public const string MacheteId = "e1920316-c182-4151-9273-26f94c3adaf9";
        public const string LauncherId = "e7a3c5d1-2f84-4b69-a0d3-5c8e1b7f2a96";
        public const string KnifeId = "f4df3778-7da1-49c7-88a6-3f4769c23f9e";
        public const string RiotShieldId = "3e7b9c41-5a2d-4f86-b1c3-9d4e7a2f6b18";

        public const string LauncherNameKey = "weapon.launcher";
        public const string KnifeNameKey = "weapon.knife";

        public const float LauncherExplosionRadius = 4f;

        // 弾倉が 1 発なので、実際の間隔はリロード時間で決まる。
        public const float LauncherFireInterval = 0.5f;

        public const float LauncherWeight = 7f;

        public static WeaponDefinition LoadDefinition(string path) => WeaponAssets.LoadDefinition(path);

        public static WeaponCatalogAsset LoadCatalog() => WeaponAssets.LoadCatalog(CatalogPath);

        public static WeaponDefinition FindInCatalog(WeaponCatalogAsset catalog, string placementId, string weaponId) =>
            WeaponAssets.FindInCatalog(catalog, placementId, weaponId);

        // 建造物を壊す確認用なので、既定のロードアウトとカタログには入れない。
        public static WeaponSpec LauncherSpec()
        {
            var flat = new WeaponSpec(
                LauncherId,
                LauncherNameKey,
                WeaponCategory.Launcher,
                FireMode.Semi,
                1,
                4,
                LauncherFireInterval,
                3f,
                120,
                150f,
                DamageFalloff.None,
                explosionRadius: LauncherExplosionRadius,
                weight: LauncherWeight);
            var impact = new ImpactSpec(flat.Impact.Damage, flat.Impact.Falloff, flat.Impact.StructureRadius, LauncherExplosion());
            return new WeaponSpec(flat.Id, flat.NameKey, flat.Category, flat.Trigger, flat.Ammo, flat.Round, impact, flat.Aim, flat.AmmoTypes, flat.ShotNoiseRadius, flat.Weight, flat.MoveSpeedMultiplier);
        }

        public static ExplosionSpec LauncherExplosion()
        {
            return new ExplosionSpec(new ExplosionDamage(100, 5f, 1f), 100);
        }

        // ナイフは近接 (MeleeController.MeleeWeapon) で倒したときの武器の Id と名前に使うだけで銃としては持たせず、
        // 当たりとダメージは MeleeController の値 (MeleeSpec) で決まる。
        public static WeaponSpec KnifeSpec()
        {
            // WeaponCategory に近接が無く、銃としては撃たないので、射撃の値は近接の既定の値に寄せるだけにする。
            return new WeaponSpec(
                KnifeId,
                KnifeNameKey,
                WeaponCategory.Handgun,
                FireMode.Semi,
                1,
                0,
                MeleeSpec.DefaultSwingSeconds,
                MeleeSpec.DefaultSwingSeconds,
                MeleeSpec.DefaultDamage,
                MeleeSpec.DefaultReach,
                DamageFalloff.None);
        }

        public static WeaponDefinition LoadKnife()
        {
            return LoadDefinition(KnifePath);
        }

        public static WeaponDefinition LoadLauncher()
        {
            return LoadDefinition(LauncherPath);
        }

        // 名前でなく Id で射撃場の一覧から引き、JSON のファイル名を変えても同じ盾を拾えるようにする。
        public static WeaponDefinition LoadRiotShield()
        {
            var shield = FindInCatalog(LoadRangeWeapons(), "riot shield", RiotShieldId);
            if (!shield.ToSpec().IsShield)
            {
                throw new InvalidOperationException(
                    $"{LogTag} weapon '{RiotShieldId}' in '{RangeWeaponsPath}' has no shield; give its JSON in '{JsonFolder}' a \"shield\" and rerun {MenuPath}");
            }

            return shield;
        }

        public static LoadoutDefinition LoadDefaultLoadout()
        {
            return LoadLoadout(LoadoutPath);
        }

        // ネット対戦と同じアセットから引くので、並びを変えるときは DefaultLoadoutSlots だけを直せばよい。
        public static WeaponDefinition[] LoadPlayerLoadout()
        {
            var loadout = LoadDefaultLoadout();
            var weapons = new WeaponDefinition[loadout.Slots.Count];
            for (var i = 0; i < weapons.Length; i++)
            {
                // LoadDefaultLoadout が ToSpecs を通しているので、どのスロットにも武器がある。
                weapons[i] = loadout.Slots[i].Weapon;
            }

            return weapons;
        }

        public static LoadoutDefinition LoadLoadout(string path) => WeaponAssets.LoadLoadout(path);

        // 種別が増えたら自動で入るよう、列挙から Handgun 以外のすべてを集める。
        public static WeaponCategory[] DefaultPrimaryCategories()
        {
            var categories = new List<WeaponCategory>();
            foreach (WeaponCategory category in Enum.GetValues(typeof(WeaponCategory)))
            {
                if (category != WeaponCategory.Handgun)
                {
                    categories.Add(category);
                }
            }

            return categories.ToArray();
        }

        public static WeaponCategory[] DefaultSecondaryCategories()
        {
            return new[] { WeaponCategory.Handgun };
        }

        public static LoadoutSlotDefinition[] DefaultLoadoutSlots(WeaponDefinition melee, WeaponDefinition pistol)
        {
            return new[]
            {
                new LoadoutSlotDefinition(DefaultPrimaryCategories(), melee),
                new LoadoutSlotDefinition(DefaultSecondaryCategories(), pistol),
            };
        }

        // 1 枠目は近接武器なので、銃が要る場所 (乗り物の砲・射撃の確かめ) は装備から最初の銃を選ぶ。
        public static WeaponDefinition FirstGun(IReadOnlyList<WeaponDefinition> weapons)
        {
            if (weapons == null)
            {
                throw new ArgumentNullException(nameof(weapons));
            }

            foreach (var weapon in weapons)
            {
                if (weapon != null && !weapon.ToSpec().IsMelee)
                {
                    return weapon;
                }
            }

            throw new InvalidOperationException($"{LogTag} the loadout has no gun; give '{LoadoutPath}' a gun and rerun {MenuPath}");
        }

        public static WeaponCatalogAsset LoadRangeWeapons() => WeaponAssets.LoadCatalog(RangeWeaponsPath);

        public static WeaponCatalogAsset LoadReferenceWeapons() => WeaponAssets.LoadCatalog(ReferenceWeaponsPath);

        public static WeaponCatalogAsset LoadRealisticWeapons() => WeaponAssets.LoadCatalog(RealisticWeaponsPath);

        // 演出の仮素材は武器のアセットより先に作られることがあるので、アセットでなく JSON から読む。
        public static IReadOnlyList<(string Name, WeaponSpec Spec)> RangeWeaponSources()
        {
            var sources = new List<(string Name, WeaponSpec Spec)>();
            AddSources(sources, JsonFolder);
            return sources;
        }

        // プレイヤーの初期武器と射撃場の台の武器は Reference と Realistic から取るので、見た目と音はこの全部に作る。
        public static IReadOnlyList<(string Name, WeaponSpec Spec)> AllWeaponSources()
        {
            var sources = new List<(string Name, WeaponSpec Spec)>();
            AddSources(sources, JsonFolder);
            AddSources(sources, ReferenceJsonFolder);
            AddSources(sources, RealisticJsonFolder);
            return sources;
        }

        private static void AddSources(List<(string Name, WeaponSpec Spec)> sources, string jsonFolder)
        {
            foreach (var file in WeaponJsonImporter.FindJsonFiles(jsonFolder))
            {
                var name = System.IO.Path.GetFileNameWithoutExtension(file);
                sources.Add((name, WeaponJsonImporter.ParseSpec(System.IO.File.ReadAllText(file), name + ".json")));
            }
        }

        [MenuItem(MenuPath)]
        public static void Build()
        {
            BuildFromJsonAt(JsonFolder, Folder, RangeWeaponsPath);
            BuildFromJsonAt(ReferenceJsonFolder, ReferenceFolder, ReferenceWeaponsPath);
            BuildFromJsonAt(RealisticJsonFolder, RealisticFolder, RealisticWeaponsPath);
            WindUpSoundAssetBuilder.Build();

            // 既定の武器はコードの値でなく JSON から作ったアセットを並べ、JSON を直せば既定の武器も変わるようにする。
            BuildCatalogAndLoadoutAt(
                LoadDefinition(MachetePath), LoadDefinition(PistolPath), LoadDefinition(CarbinePath), CatalogPath, LoadoutPath);
            BuildKnifeAt(KnifePath);
        }

        public static IReadOnlyList<WeaponDefinition> BuildFromJsonAt(string jsonFolder, string folder, string rangeWeaponsPath)
        {
            rangeWeaponsPath = SceneBuildHelpers.ValidateAssetPath(rangeWeaponsPath);

            var weapons = WeaponJsonImporter.ImportFrom(jsonFolder, folder, null);
            SceneBuildHelpers.CreateOrUpdateAsset<WeaponCatalogAsset>(rangeWeaponsPath, c => Configure(c, weapons));

            Debug.Log($"{LogTag} saved {weapons.Count.ToString(CultureInfo.InvariantCulture)} weapon(s) from '{jsonFolder}' and '{rangeWeaponsPath}'");
            return weapons;
        }

        public static WeaponDefinition BuildLauncherAt(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);

            var spec = LauncherSpec();
            var launcher = SceneBuildHelpers.CreateOrUpdateAsset<WeaponDefinition>(path, d => Configure(d, spec));

            Debug.Log($"{LogTag} saved '{path}'");
            return launcher;
        }

        public static WeaponDefinition BuildKnifeAt(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);

            var spec = KnifeSpec();
            var knife = SceneBuildHelpers.CreateOrUpdateAsset<WeaponDefinition>(path, d => Configure(d, spec));

            Debug.Log($"{LogTag} saved '{path}'");
            return knife;
        }

        // 一覧には装備の 2 本に加えて、敵・タレット・購入・ガンゲームが撃たせる carbine も入れる。
        public static WeaponCatalogAsset BuildCatalogAndLoadoutAt(
            WeaponDefinition melee, WeaponDefinition pistol, WeaponDefinition carbine, string catalogPath, string loadoutPath)
        {
            if (melee == null || pistol == null || carbine == null)
            {
                throw new ArgumentNullException(melee == null ? nameof(melee) : pistol == null ? nameof(pistol) : nameof(carbine));
            }

            catalogPath = SceneBuildHelpers.ValidateAssetPath(catalogPath);
            loadoutPath = SceneBuildHelpers.ValidateAssetPath(loadoutPath);
            RequireDistinct(catalogPath, loadoutPath);

            var catalog = SceneBuildHelpers.CreateOrUpdateAsset<WeaponCatalogAsset>(
                catalogPath, c => Configure(c, new[] { carbine, pistol, melee }));
            SceneBuildHelpers.CreateOrUpdateAsset<LoadoutDefinition>(
                loadoutPath, l => Configure(l, DefaultLoadoutSlots(melee, pistol), LoadoutDefinition.DefaultSwitchSeconds));

            Debug.Log($"{LogTag} saved '{catalogPath}' and '{loadoutPath}'");
            return catalog;
        }

        public static void Configure(WeaponDefinition definition, WeaponSpec spec) => WeaponAssets.Configure(definition, spec);

        public static void Configure(WeaponCatalogAsset catalog, IEnumerable<WeaponDefinition> weapons) =>
            WeaponAssets.Configure(catalog, weapons);

        public static void Configure(LoadoutDefinition loadout, IEnumerable<LoadoutSlotDefinition> slots, float switchSeconds) =>
            WeaponAssets.Configure(loadout, slots, switchSeconds);

        private static void RequireDistinct(params string[] paths)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in paths)
            {
                if (!seen.Add(path))
                {
                    throw new ArgumentException($"weapon asset path '{path}' is used more than once");
                }
            }
        }
    }
}

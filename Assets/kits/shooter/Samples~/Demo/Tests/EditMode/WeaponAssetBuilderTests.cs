using System.Linq;
using System;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // リポジトリの Assets/kits/shooter/Weapons を触らないよう、一時フォルダに走らせる。
    public sealed class WeaponAssetBuilderTests
    {
        private string _folder;
        private string _carbinePath;
        private string _pistolPath;
        private string _machetePath;
        private string _catalogPath;
        private string _loadoutPath;

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/__WeaponAssetBuilderTests_" + Guid.NewGuid().ToString("N");
            _carbinePath = _folder + "/Carbine.asset";
            _pistolPath = _folder + "/Pistol.asset";
            _machetePath = _folder + "/Machete.asset";
            _catalogPath = _folder + "/WeaponCatalog.asset";
            _loadoutPath = _folder + "/DefaultLoadout.asset";
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(_folder))
            {
                AssetDatabase.DeleteAsset(_folder);
            }
        }

        [Test]
        public void DefaultPathsAreUnderGameWeapons()
        {
            Assert.That(WeaponAssetBuilder.MachetePath, Is.EqualTo(DemoSample.Root + "/Weapons/Machete.asset"));
            Assert.That(WeaponAssetBuilder.CarbinePath, Is.EqualTo(DemoSample.Root + "/Weapons/Reference/Carbine556.asset"));
            Assert.That(WeaponAssetBuilder.PistolPath, Is.EqualTo(DemoSample.Root + "/Weapons/Reference/CompactPistol.asset"));
            Assert.That(WeaponAssetBuilder.CatalogPath, Is.EqualTo(DemoSample.Root + "/Weapons/WeaponCatalog.asset"));
            Assert.That(WeaponAssetBuilder.LoadoutPath, Is.EqualTo(DemoSample.Root + "/Weapons/DefaultLoadout.asset"));
        }

        [Test]
        public void TheDefaultWeaponIdsAreTheIdsOfTheirJson()
        {
            Assert.That(new[] { WeaponAssetBuilder.CarbineId, WeaponAssetBuilder.PistolId, WeaponAssetBuilder.MacheteId }, Is.Unique);
            Assert.That(JsonSpec(WeaponAssetBuilder.ReferenceJsonFolder, "Carbine556").Id, Is.EqualTo(WeaponAssetBuilder.CarbineId));
            Assert.That(JsonSpec(WeaponAssetBuilder.ReferenceJsonFolder, "CompactPistol").Id, Is.EqualTo(WeaponAssetBuilder.PistolId));
            Assert.That(JsonSpec(WeaponAssetBuilder.JsonFolder, "Machete").Id, Is.EqualTo(WeaponAssetBuilder.MacheteId));
            Assert.That(JsonSpec(WeaponAssetBuilder.JsonFolder, "Machete").IsMelee, Is.True, "the loadout starts with a melee weapon");
            Assert.That(JsonSpec(WeaponAssetBuilder.ReferenceJsonFolder, "CompactPistol").Category, Is.EqualTo(WeaponCategory.Handgun));
        }

        [Test]
        public void TheSampleJsonIsTheBuiltInWeapons()
        {
            Assert.That(WeaponAssetBuilder.JsonFolder, Is.EqualTo(DemoSample.Root + "/Weapons/Json"));
            Assert.That(WeaponAssetBuilder.RangeWeaponsPath, Is.EqualTo(DemoSample.Root + "/Weapons/RangeWeapons.asset"));

            Assert.That(
                WeaponSpecJson.Write(JsonSpec(WeaponAssetBuilder.JsonFolder, "Launcher")), Is.EqualTo(WeaponSpecJson.Write(WeaponAssetBuilder.LauncherSpec())));
        }

        [Test]
        public void TheReferenceAndRealisticSamplesAreBuiltIntoTheirOwnFoldersAndLists()
        {
            Assert.That(WeaponAssetBuilder.ReferenceJsonFolder, Is.EqualTo(DemoSample.Root + "/Weapons/ReferenceJson"));
            Assert.That(WeaponAssetBuilder.ReferenceFolder, Is.EqualTo(DemoSample.Root + "/Weapons/Reference"));
            Assert.That(WeaponAssetBuilder.ReferenceWeaponsPath, Is.EqualTo(DemoSample.Root + "/Weapons/ReferenceWeapons.asset"));
            Assert.That(WeaponAssetBuilder.RealisticJsonFolder, Is.EqualTo(DemoSample.Root + "/Weapons/RealisticJson"));
            Assert.That(WeaponAssetBuilder.RealisticFolder, Is.EqualTo(DemoSample.Root + "/Weapons/Realistic"));
            Assert.That(WeaponAssetBuilder.RealisticWeaponsPath, Is.EqualTo(DemoSample.Root + "/Weapons/RealisticWeapons.asset"));

            foreach (var (jsonFolder, folder, catalog) in new[]
            {
                (WeaponAssetBuilder.ReferenceJsonFolder, WeaponAssetBuilder.ReferenceFolder, WeaponAssetBuilder.LoadReferenceWeapons()),
                (WeaponAssetBuilder.RealisticJsonFolder, WeaponAssetBuilder.RealisticFolder, WeaponAssetBuilder.LoadRealisticWeapons()),
            })
            {
                var names = WeaponJsonImporter.FindJsonFiles(jsonFolder).Select(System.IO.Path.GetFileNameWithoutExtension).ToArray();
                Assert.That(names, Has.Length.EqualTo(32), jsonFolder);
                Assert.That(catalog.Weapons.Select(d => d.name), Is.EqualTo(names), $"rerun {nameof(WeaponAssetBuilder)}.Build");
                foreach (var definition in catalog.Weapons)
                {
                    Assert.That(AssetDatabase.GetAssetPath(definition), Is.EqualTo(folder + "/" + definition.name + ".asset"));
                    var json = System.IO.File.ReadAllText(ShooterKitRoot.PhysicalPath(jsonFolder + "/" + definition.name + ".json"));
                    Assert.That(
                        WeaponSpecJson.Write(definition.ToSpec()),
                        Is.EqualTo(WeaponSpecJson.Write(WeaponJsonImporter.ParseSpec(json, definition.name + ".json"))),
                        $"{definition.name}: rerun {nameof(WeaponAssetBuilder)}.Build");
                }
            }
        }

        [Test]
        public void BuildsEveryJsonWeaponAndListsThemForTheRange()
        {
            var jsonFolder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WeaponAssetBuilderTests_" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(jsonFolder);
            try
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(jsonFolder, "B_Everything.json"), WeaponSpecJson.Write(PartsSpec()));
                System.IO.File.WriteAllText(System.IO.Path.Combine(jsonFolder, "A_Rifle.json"), WeaponSpecJson.Write(DemoTestWeapons.CarbineSpec()));
                var rangePath = _folder + "/RangeWeapons.asset";

                var built = WeaponAssetBuilder.BuildFromJsonAt(jsonFolder, _folder, rangePath);

                Assert.That(built.Select(d => d.name), Is.EqualTo(new[] { "A_Rifle", "B_Everything" }));
                var range = AssetDatabase.LoadAssetAtPath<WeaponCatalogAsset>(rangePath);
                Assert.That(range.Weapons, Is.EqualTo(built));
                Assert.That(
                    WeaponSpecJson.Write(built[1].ToSpec()), Is.EqualTo(WeaponSpecJson.Write(PartsSpec())), "every part survives the asset");

                var guid = AssetDatabase.AssetPathToGUID(rangePath);
                WeaponAssetBuilder.BuildFromJsonAt(jsonFolder, _folder, rangePath);
                Assert.That(AssetDatabase.AssetPathToGUID(rangePath), Is.EqualTo(guid), "a second run keeps the list asset");
            }
            finally
            {
                System.IO.Directory.Delete(jsonFolder, true);
            }
        }

        [Test]
        public void TheDmrSampleSwitchesFromStandardToArmorPiercingRounds()
        {
            var json = System.IO.File.ReadAllText(ShooterKitRoot.PhysicalPath(WeaponAssetBuilder.JsonFolder + "/Dmr.json"));
            var spec = WeaponJsonImporter.ParseSpec(json, "Dmr.json");
            var asset = WeaponAssetBuilder.LoadRangeWeapons().Weapons.Single(d => d.ToSpec().Id == spec.Id);

            Assert.That(WeaponSpecJson.Write(asset.ToSpec()), Is.EqualTo(WeaponSpecJson.Write(spec)), "the committed asset is the JSON");
            Assert.That(spec.AmmoTypes.Select(t => t.NameKey), Is.EqualTo(new[] { "ammo.dmr.standard", "ammo.dmr.armorPiercing" }));
            foreach (var type in spec.AmmoTypes)
            {
                foreach (var language in GameLanguages.All)
                {
                    Assert.That(Localizer.TableOf(language).Contains(type.NameKey), Is.True, $"{GameLanguages.Code(language)}: {type.NameKey}");
                }
            }

            var weapon = new Weapon(spec);
            var standard = weapon.AmmoTypes.Impact.Damage;
            Assert.That(weapon.AmmoTypes.Round.Penetration, Is.Null);

            Assert.That(weapon.TrySwitchAmmoType(), Is.True);

            Assert.That(weapon.AmmoTypes.Current.NameKey, Is.EqualTo("ammo.dmr.armorPiercing"));
            Assert.That(weapon.AmmoTypes.Round.Penetration, Is.Not.Null, "armor-piercing rounds carry the penetration values");
            Assert.That(weapon.AmmoTypes.Impact.Damage, Is.Not.EqualTo(standard));
        }

        [TestCase(LauncherSmokeRun.RocketLauncherJson, false, WorldHitResponse.Stop, DetonationTrigger.OnImpact)]
        [TestCase(LauncherSmokeRun.GrenadeLauncherJson, true, WorldHitResponse.Bounce, DetonationTrigger.OnLifetimeEnd)]
        public void TheLauncherSamplesFireExplodingRoundsAndTheirCommittedAssetsAreTheJson(
            string file, bool falls, WorldHitResponse onWorldHit, DetonationTrigger detonation)
        {
            var json = System.IO.File.ReadAllText(ShooterKitRoot.PhysicalPath(WeaponAssetBuilder.JsonFolder + "/" + file));
            var spec = WeaponJsonImporter.ParseSpec(json, file);

            Assert.That(spec.Round.Delivery, Is.EqualTo(RoundDelivery.Projectile));
            Assert.That(spec.Round.Projectile.GravityMultiplier > 0f, Is.EqualTo(falls));
            Assert.That(spec.Round.Projectile.OnWorldHit, Is.EqualTo(onWorldHit));
            Assert.That(spec.Impact.Explosion, Is.Not.Null);
            Assert.That(spec.Impact.Explosion.Detonation, Is.EqualTo(detonation));
            foreach (var language in GameLanguages.All)
            {
                Assert.That(Localizer.TableOf(language).Contains(spec.NameKey), Is.True, $"{GameLanguages.Code(language)}: {spec.NameKey}");
            }

            Assert.That(WeaponAssetBuilder.LoadRangeWeapons().TryFind(spec.Id, out var committed), Is.True);
            Assert.That(WeaponSpecJson.Write(committed.ToSpec()), Is.EqualTo(WeaponSpecJson.Write(spec)), "the committed asset is the JSON");
        }

        [Test]
        public void TheCommittedRangeListHoldsEverySampleWeapon()
        {
            var range = WeaponAssetBuilder.LoadRangeWeapons();
            var names = System.IO.Directory.GetFiles(ShooterKitRoot.PhysicalPath(WeaponAssetBuilder.JsonFolder), "*.json")
                .Select(System.IO.Path.GetFileNameWithoutExtension)
                .OrderBy(n => n, StringComparer.Ordinal);

            Assert.That(range.Weapons.Select(d => d.name), Is.EqualTo(names));
            Assert.That(range.Weapons.Count, Is.LessThanOrEqualTo(RangeWeaponRackFixture.Columns * RangeWeaponRackFixture.Rows));
        }

        [Test]
        public void TheRiotShieldSampleCarriesAShieldThatBlocksAndBashesAndItsCommittedAssetIsTheJson()
        {
            var json = System.IO.File.ReadAllText(ShooterKitRoot.PhysicalPath(WeaponAssetBuilder.JsonFolder + "/RiotShield.json"));
            var spec = WeaponJsonImporter.ParseSpec(json, "RiotShield.json");

            Assert.That(spec.Id, Is.EqualTo(WeaponAssetBuilder.RiotShieldId));
            Assert.That(spec.NameKey, Is.EqualTo("weapon.riotShield"));
            Assert.That(spec.IsShield, Is.True);
            Assert.That(spec.Shield.BlockArcDegrees, Is.EqualTo(120f));
            Assert.That(spec.Shield.Width, Is.EqualTo(0.6f));
            Assert.That(spec.Shield.Height, Is.EqualTo(1.2f));
            Assert.That(spec.Shield.MoveSpeedMultiplier, Is.EqualTo(0.8f));
            Assert.That(spec.Shield.Bash.Damage, Is.EqualTo(40));
            Assert.That(spec.Shield.Bash.Reach, Is.EqualTo(1.6f));
            Assert.That(spec.Shield.Bash.SwingSeconds, Is.EqualTo(0.6f));
            Assert.That(spec.MagazineSize, Is.EqualTo(1), "the HUD shows no magazine of rounds for a shield");
            Assert.That(spec.ShotNoiseRadius, Is.Zero);
            foreach (var language in GameLanguages.All)
            {
                Assert.That(Localizer.TableOf(language).Contains(spec.NameKey), Is.True, $"{GameLanguages.Code(language)}: {spec.NameKey}");
            }

            var committed = WeaponAssetBuilder.LoadRiotShield();
            Assert.That(AssetDatabase.GetAssetPath(committed), Is.EqualTo(WeaponAssetBuilder.RiotShieldPath), $"rerun {nameof(WeaponAssetBuilder)}.Build");
            Assert.That(WeaponSpecJson.Write(committed.ToSpec()), Is.EqualTo(WeaponSpecJson.Write(spec)), "the committed asset is the JSON");
            Assert.That(committed.Shield.Prefab, Is.Null, "the held shield is built in code until a game sets its look");
            Assert.That(WeaponAssetBuilder.LoadCatalog().TryFind(spec.Id, out _), Is.False, "the shield stays out of the default catalog and loadout");
        }

        [Test]
        public void BuildingTheRiotShieldJsonKeepsItsShieldOnTheAsset()
        {
            var jsonFolder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WeaponAssetBuilderTests_" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(jsonFolder);
            try
            {
                System.IO.File.Copy(
                    ShooterKitRoot.PhysicalPath(WeaponAssetBuilder.JsonFolder + "/RiotShield.json"), System.IO.Path.Combine(jsonFolder, "RiotShield.json"));
                var rangePath = _folder + "/RangeWeapons.asset";

                var built = WeaponAssetBuilder.BuildFromJsonAt(jsonFolder, _folder, rangePath);

                Assert.That(built.Select(d => d.name), Is.EqualTo(new[] { "RiotShield" }));
                Assert.That(built[0].Shield.Enabled, Is.True);
                Assert.That(built[0].ToSpec().IsShield, Is.True);
                Assert.That(built[0].ToSpec().Shield.Bash.Damage, Is.EqualTo(40));

                var range = AssetDatabase.LoadAssetAtPath<WeaponCatalogAsset>(rangePath);
                Assert.That(WeaponAssetBuilder.FindInCatalog(range, "test", WeaponAssetBuilder.RiotShieldId), Is.SameAs(built[0]));
            }
            finally
            {
                System.IO.Directory.Delete(jsonFolder, true);
            }
        }

        [Test]
        public void TheScopedRifleSampleHasSeveralZoomLevelsAndItsCommittedAssetMatchesTheJson()
        {
            var json = System.IO.File.ReadAllText(ShooterKitRoot.PhysicalPath(WeaponAssetBuilder.JsonFolder + "/ScopedRifle.json"));
            var spec = WeaponJsonImporter.ParseSpec(json, "ScopedRifle.json");

            Assert.That(spec.Aim.ZoomLevels.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(spec.Aim.ZoomLevels, Is.Ordered.Descending, "each step zooms in further");

            var committed = WeaponAssetBuilder.LoadRangeWeapons().Weapons.Single(d => d.ToSpec().Id == spec.Id);
            Assert.That(WeaponSpecJson.Write(committed.ToSpec()), Is.EqualTo(WeaponSpecJson.Write(spec)));
        }

        [Test]
        public void BuildsTheKnifeOutsideTheCatalogAndLoadout()
        {
            Build();
            var knifePath = _folder + "/Knife.asset";

            var built = WeaponAssetBuilder.BuildKnifeAt(knifePath);

            Assert.That(built, Is.SameAs(AssetDatabase.LoadAssetAtPath<WeaponDefinition>(knifePath)));
            Assert.That(WeaponAssetBuilder.KnifePath, Is.EqualTo(DemoSample.Root + "/Weapons/Knife.asset"));
            Assert.That(new[] { WeaponAssetBuilder.CarbineId, WeaponAssetBuilder.PistolId, WeaponAssetBuilder.MacheteId, WeaponAssetBuilder.LauncherId }, Has.No.Member(WeaponAssetBuilder.KnifeId));

            var knife = built.ToSpec();
            Assert.That(knife.Id, Is.EqualTo(WeaponAssetBuilder.KnifeId));
            Assert.That(knife.NameKey, Is.EqualTo(WeaponAssetBuilder.KnifeNameKey));
            Assert.That(Localizer.TableOf(GameLanguage.Japanese).Resolve(DisplayNames.Of(knife)), Is.EqualTo("ナイフ"));

            var catalog = AssetDatabase.LoadAssetAtPath<WeaponCatalogAsset>(_catalogPath);
            Assert.That(catalog.TryFind(WeaponAssetBuilder.KnifeId, out _), Is.False, "the knife is not a gun to carry");

            var guid = AssetDatabase.AssetPathToGUID(knifePath);
            Assert.That(WeaponAssetBuilder.BuildKnifeAt(knifePath), Is.SameAs(built));
            Assert.That(AssetDatabase.AssetPathToGUID(knifePath), Is.EqualTo(guid));
        }

        [Test]
        public void BuildsTheLauncherOutsideTheCatalogAndLoadout()
        {
            Build();
            var launcherPath = _folder + "/Launcher.asset";

            var built = WeaponAssetBuilder.BuildLauncherAt(launcherPath);

            Assert.That(built, Is.SameAs(AssetDatabase.LoadAssetAtPath<WeaponDefinition>(launcherPath)));
            Assert.That(WeaponAssetBuilder.LauncherPath, Is.EqualTo(DemoSample.Root + "/Weapons/Launcher.asset"));
            Assert.That(WeaponAssetBuilder.LauncherId, Is.Not.EqualTo(WeaponAssetBuilder.CarbineId));
            Assert.That(WeaponAssetBuilder.LauncherId, Is.Not.EqualTo(WeaponAssetBuilder.PistolId));

            var launcher = built.ToSpec();
            Assert.That(launcher.Id, Is.EqualTo(WeaponAssetBuilder.LauncherId));
            Assert.That(launcher.Category, Is.EqualTo(WeaponCategory.Launcher));
            Assert.That(launcher.FireMode, Is.EqualTo(FireMode.Semi));
            Assert.That(launcher.MagazineSize, Is.EqualTo(1));
            Assert.That(launcher.ReserveAmmo, Is.EqualTo(4));
            Assert.That(launcher.Damage, Is.EqualTo(120));
            Assert.That(launcher.Range, Is.EqualTo(150f));
            Assert.That(launcher.ExplosionRadius, Is.EqualTo(4f));
            Assert.That(launcher.ReloadSeconds, Is.EqualTo(3f));
            Assert.That(built.ExplosionRadius, Is.EqualTo(4f), "the asset stores the value");

            var catalog = AssetDatabase.LoadAssetAtPath<WeaponCatalogAsset>(_catalogPath);
            Assert.That(catalog.TryFind(WeaponAssetBuilder.LauncherId, out _), Is.False, "the catalog stays carbine, pistol, machete");

            var guid = AssetDatabase.AssetPathToGUID(launcherPath);
            built.Damage = 1;
            Assert.That(WeaponAssetBuilder.BuildLauncherAt(launcherPath), Is.SameAs(built));
            Assert.That(AssetDatabase.AssetPathToGUID(launcherPath), Is.EqualTo(guid));
            Assert.That(built.Damage, Is.EqualTo(120), "a second run restores the values");
        }

        [Test]
        public void ThePlayerLoadoutIsTheCommittedMacheteThenPistol()
        {
            Assume.That(
                AssetDatabase.LoadAssetAtPath<LoadoutDefinition>(WeaponAssetBuilder.LoadoutPath),
                Is.Not.Null,
                $"run {nameof(WeaponAssetBuilder)}.Build first");

            var weapons = WeaponAssetBuilder.LoadPlayerLoadout();

            Assert.That(weapons, Is.EqualTo(TestScenes.ExpectedPlayerWeapons()));
            Assert.That(weapons, Has.None.Null);
            Assert.That(weapons[0].ToSpec().Id, Is.EqualTo(WeaponAssetBuilder.MacheteId), $"rerun {nameof(WeaponAssetBuilder)}.Build");
            Assert.That(weapons[1].ToSpec().Id, Is.EqualTo(WeaponAssetBuilder.PistolId));
            Assert.That(WeaponAssetBuilder.FirstGun(weapons), Is.SameAs(weapons[1]), "the pistol is the first gun");
        }

        [Test]
        public void LoadDefinitionReturnsTheBuiltAsset()
        {
            Build();

            var rifle = WeaponAssetBuilder.LoadDefinition(_carbinePath);

            Assert.That(rifle, Is.SameAs(AssetDatabase.LoadAssetAtPath<WeaponDefinition>(_carbinePath)));
            Assert.That(rifle.ToSpec().Id, Is.EqualTo(WeaponAssetBuilder.CarbineId));
        }

        [Test]
        public void LoadDefinitionRejectsAMissingOrWrongAsset()
        {
            Assert.Throws<InvalidOperationException>(() => WeaponAssetBuilder.LoadDefinition(_carbinePath));
            Assert.Throws<ArgumentException>(() => WeaponAssetBuilder.LoadDefinition("Rifle.asset"));

            Build();
            Assert.Throws<InvalidOperationException>(
                () => WeaponAssetBuilder.LoadDefinition(_catalogPath), "a catalog is not a weapon definition");
        }

        [Test]
        public void LoadDefinitionRejectsAnInvalidAsset()
        {
            Build();
            var rifle = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(_carbinePath);
            rifle.MagazineSize = 0;

            Assert.Throws<WeaponDefinitionException>(() => WeaponAssetBuilder.LoadDefinition(_carbinePath));
        }

        [Test]
        public void BuildsTheCatalogAsCarbinePistolThenMachete()
        {
            var catalogAsset = Build();

            var carbineAsset = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(_carbinePath);
            var pistolAsset = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(_pistolPath);
            var macheteAsset = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(_machetePath);
            Assert.That(catalogAsset, Is.SameAs(AssetDatabase.LoadAssetAtPath<WeaponCatalogAsset>(_catalogPath)));
            Assert.That(catalogAsset.Weapons, Is.EqualTo(new[] { carbineAsset, pistolAsset, macheteAsset }));

            var catalog = catalogAsset.ToCatalog();
            Assert.That(catalog.All.Select(w => w.Id), Is.EqualTo(new[] { WeaponAssetBuilder.CarbineId, WeaponAssetBuilder.PistolId, WeaponAssetBuilder.MacheteId }));
            Assert.That(catalog.Get(WeaponAssetBuilder.MacheteId).IsMelee, Is.True);
        }

        [Test]
        public void EveryBuiltWeaponNameIsInEveryLanguageTable()
        {
            var carbine = JsonSpec(WeaponAssetBuilder.ReferenceJsonFolder, "Carbine556");
            var pistol = JsonSpec(WeaponAssetBuilder.ReferenceJsonFolder, "CompactPistol");
            var machete = JsonSpec(WeaponAssetBuilder.JsonFolder, "Machete");
            foreach (var spec in new[] { carbine, pistol, machete, WeaponAssetBuilder.LauncherSpec(), WeaponAssetBuilder.KnifeSpec() })
            {
                foreach (var language in GameLanguages.All)
                {
                    Assert.That(Localizer.TableOf(language).Contains(spec.NameKey), Is.True, $"{GameLanguages.Code(language)}: {spec.NameKey}");
                }
            }

            Assert.That(Localizer.TableOf(GameLanguage.Japanese).Resolve(DisplayNames.Of(carbine)), Is.EqualTo("5.56mm カービン"));
            Assert.That(Localizer.TableOf(GameLanguage.Japanese).Resolve(DisplayNames.Of(pistol)), Is.EqualTo("コンパクトピストル"));
            Assert.That(Localizer.TableOf(GameLanguage.Japanese).Resolve(DisplayNames.Of(machete)), Is.EqualTo("マチェーテ"));
            Assert.That(Localizer.TableOf(GameLanguage.Japanese).Resolve(DisplayNames.Of(WeaponAssetBuilder.LauncherSpec())), Is.EqualTo("Launcher"));
        }

        [Test]
        public void BuildsTheDefaultLoadoutWithMacheteThenPistol()
        {
            Build();

            var pistolAsset = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(_pistolPath);
            var macheteAsset = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(_machetePath);
            var loadout = AssetDatabase.LoadAssetAtPath<LoadoutDefinition>(_loadoutPath);
            Assert.That(loadout, Is.Not.Null);
            Assert.That(loadout.SwitchSeconds, Is.EqualTo(0.4f));
            Assert.That(loadout.Slots.Count(), Is.EqualTo(2));
            Assert.That(loadout.Slots[0].Weapon, Is.SameAs(macheteAsset));
            Assert.That(loadout.Slots[1].Weapon, Is.SameAs(pistolAsset));

            var rule = loadout.ToRule();
            Assert.That(rule.SlotCount, Is.EqualTo(2));
            foreach (WeaponCategory category in Enum.GetValues(typeof(WeaponCategory)))
            {
                var isHandgun = category == WeaponCategory.Handgun;
                Assert.That(rule.Accepts(0, category), Is.EqualTo(!isHandgun), "slot 0 accepts " + category);
                Assert.That(rule.Accepts(1, category), Is.EqualTo(isHandgun), "slot 1 accepts " + category);
            }

            var specs = loadout.ToSpecs();
            Assert.That(specs[0].Id, Is.EqualTo(WeaponAssetBuilder.MacheteId));
            Assert.That(specs[1].Id, Is.EqualTo(WeaponAssetBuilder.PistolId));
        }

        [Test]
        public void LoadLoadoutReturnsTheBuiltAsset()
        {
            Build();

            var loadout = WeaponAssetBuilder.LoadLoadout(_loadoutPath);

            Assert.That(loadout, Is.SameAs(AssetDatabase.LoadAssetAtPath<LoadoutDefinition>(_loadoutPath)));
        }

        [Test]
        public void LoadLoadoutRejectsAMissingWrongOrInvalidAsset()
        {
            Assert.Throws<InvalidOperationException>(() => WeaponAssetBuilder.LoadLoadout(_loadoutPath));
            Assert.Throws<ArgumentException>(() => WeaponAssetBuilder.LoadLoadout("DefaultLoadout.asset"));

            Build();
            Assert.Throws<InvalidOperationException>(
                () => WeaponAssetBuilder.LoadLoadout(_catalogPath), "a catalog is not a loadout");

            var loadout = AssetDatabase.LoadAssetAtPath<LoadoutDefinition>(_loadoutPath);
            var pistol = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(_pistolPath);
            var machete = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(_machetePath);
            loadout.SetSlots(WeaponAssetBuilder.DefaultLoadoutSlots(pistol, machete));
            Assert.Throws<WeaponDefinitionException>(() => WeaponAssetBuilder.LoadLoadout(_loadoutPath));
        }

        [Test]
        public void SecondRunKeepsTheLoadoutGuidAndRestoresItsValues()
        {
            Build();
            var loadoutGuid = AssetDatabase.AssetPathToGUID(_loadoutPath);
            Assert.That(loadoutGuid, Is.Not.Empty);

            var loadout = AssetDatabase.LoadAssetAtPath<LoadoutDefinition>(_loadoutPath);
            var handgun = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(_pistolPath);
            loadout.SetSlots(new[] { new LoadoutSlotDefinition(new[] { WeaponCategory.Handgun }, handgun) });
            loadout.SwitchSeconds = 3f;
            EditorUtility.SetDirty(loadout);
            AssetDatabase.SaveAssets();

            Build();

            Assert.That(AssetDatabase.AssetPathToGUID(_loadoutPath), Is.EqualTo(loadoutGuid));
            var rebuilt = AssetDatabase.LoadAssetAtPath<LoadoutDefinition>(_loadoutPath);
            Assert.That(rebuilt, Is.SameAs(loadout));
            Assert.That(rebuilt.SwitchSeconds, Is.EqualTo(0.4f));
            Assert.That(rebuilt.Slots.Count(), Is.EqualTo(2));
            Assert.That(rebuilt.ToSpecs()[0].Id, Is.EqualTo(WeaponAssetBuilder.MacheteId));
        }

        [Test]
        public void ConfigureDoesNotSaveAnInvalidLoadout()
        {
            Build();
            var otherPath = _folder + "/BrokenLoadout.asset";
            var pistol = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(_pistolPath);
            var machete = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(_machetePath);

            Assert.Throws<WeaponDefinitionException>(
                () => SceneBuildHelpers.CreateOrUpdateAsset<LoadoutDefinition>(
                    otherPath,
                    l => WeaponAssetBuilder.Configure(l, WeaponAssetBuilder.DefaultLoadoutSlots(pistol, machete), 0.4f)));
            Assert.Throws<WeaponDefinitionException>(
                () => SceneBuildHelpers.CreateOrUpdateAsset<LoadoutDefinition>(
                    otherPath,
                    l => WeaponAssetBuilder.Configure(l, WeaponAssetBuilder.DefaultLoadoutSlots(machete, pistol), -1f)));
            Assert.That(AssetDatabase.LoadMainAssetAtPath(otherPath), Is.Null);
        }

        [Test]
        public void SecondRunKeepsTheCatalogGuidAndRestoresItsWeapons()
        {
            Build();
            var catalogGuid = AssetDatabase.AssetPathToGUID(_catalogPath);
            Assert.That(catalogGuid, Is.Not.Empty);

            var catalogAsset = AssetDatabase.LoadAssetAtPath<WeaponCatalogAsset>(_catalogPath);
            catalogAsset.SetWeapons(new[] { AssetDatabase.LoadAssetAtPath<WeaponDefinition>(_pistolPath) });
            EditorUtility.SetDirty(catalogAsset);
            AssetDatabase.SaveAssets();

            var rebuilt = Build();

            Assert.That(AssetDatabase.AssetPathToGUID(_catalogPath), Is.EqualTo(catalogGuid));
            Assert.That(rebuilt, Is.SameAs(catalogAsset));
            Assert.That(rebuilt.Weapons.Count(), Is.EqualTo(3));
            Assert.That(rebuilt.ToCatalog().Get(WeaponAssetBuilder.CarbineId).Category, Is.EqualTo(WeaponCategory.AssaultRifle));
        }

        [Test]
        public void BuiltWeaponsStoreTheirRecoilAndSpread()
        {
            Build();

            AssertRecoilAndSpread(
                AssetDatabase.LoadAssetAtPath<WeaponDefinition>(_carbinePath), DemoTestWeapons.CarbineSpec(), _carbinePath);
            AssertRecoilAndSpread(
                AssetDatabase.LoadAssetAtPath<WeaponDefinition>(_pistolPath), DemoTestWeapons.PistolSpec(), _pistolPath);

            var rifle = DemoTestWeapons.CarbineSpec();
            Assert.That(rifle.RecoilPitchDegrees, Is.EqualTo(0.5f));
            Assert.That(rifle.RecoilYawDegrees, Is.EqualTo(0.2f));
            Assert.That(rifle.RecoilRecoveryPerSecond, Is.EqualTo(10f));
            Assert.That(rifle.HipSpreadDegrees, Is.EqualTo(3f));
            Assert.That(rifle.AdsSpreadDegrees, Is.EqualTo(0.5f));
            Assert.That(rifle.MoveSpreadDegrees, Is.EqualTo(3f));
            Assert.That(rifle.AdsMoveSpreadDegrees, Is.EqualTo(0.9f));

            var handgun = DemoTestWeapons.PistolSpec();
            Assert.That(handgun.RecoilPitchDegrees, Is.EqualTo(2.5f));
            Assert.That(handgun.RecoilYawDegrees, Is.EqualTo(0.6f));
            Assert.That(handgun.RecoilRecoveryPerSecond, Is.EqualTo(60f));
            Assert.That(handgun.HipSpreadDegrees, Is.EqualTo(2f));
            Assert.That(handgun.AdsSpreadDegrees, Is.EqualTo(0.8f));
            Assert.That(handgun.MoveSpreadDegrees, Is.EqualTo(1.5f));
            Assert.That(handgun.AdsMoveSpreadDegrees, Is.EqualTo(0.45f));
        }

        [Test]
        public void TheRifleClimbsWhileFiringAndRecoversAfterLettingGo()
        {
            var rifle = DemoTestWeapons.CarbineSpec();
            var recoil = new RecoilState();

            for (var shot = 0; shot < 10; shot++)
            {
                recoil.Kick(rifle, 1, shot);
                recoil.Tick(rifle.FireInterval);
            }

            Assert.That(
                recoil.PitchUpDegrees,
                Is.EqualTo(10f * rifle.RecoilPitchDegrees).Within(1e-4f),
                "the fire interval is shorter than the recovery delay, so a burst does not recover");
            Assert.That(
                recoil.PitchUpDegrees,
                Is.GreaterThan(Math.Abs(recoil.YawDegrees)),
                "the burst climbs up more than it drifts sideways");

            recoil.Tick(recoil.RecoveryDelaySeconds + (recoil.PitchUpDegrees / rifle.RecoilRecoveryPerSecond) + 0.5f);
            Assert.That(recoil.IsAtRest, Is.True, "letting go brings the aim back");
        }

        [Test]
        public void TheHandgunKicksHarderThanTheRifleAndRecoversBeforeTheNextFewShots()
        {
            var rifle = DemoTestWeapons.CarbineSpec();
            var handgun = DemoTestWeapons.PistolSpec();
            Assert.That(handgun.RecoilPitchDegrees, Is.GreaterThan(rifle.RecoilPitchDegrees * 2f), "one handgun shot jumps more");
            Assert.That(handgun.RecoilRecoveryPerSecond, Is.GreaterThan(rifle.RecoilRecoveryPerSecond), "the handgun settles faster");

            var recoil = new RecoilState();
            recoil.Kick(handgun, 1, 0);
            Assert.That(recoil.PitchUpDegrees, Is.EqualTo(handgun.RecoilPitchDegrees));

            recoil.Tick(0.3f);
            Assert.That(recoil.IsAtRest, Is.True, "a single kick is gone within 0.3 s");
        }

        [Test]
        public void TheHandgunDoesNotClimbWhenFiredAtItsFastestInterval()
        {
            var handgun = DemoTestWeapons.PistolSpec();
            var recoil = new RecoilState();
            for (var shot = 0; shot < 12; shot++)
            {
                recoil.Kick(handgun, 1, 0);
                Assert.That(recoil.PitchUpDegrees, Is.EqualTo(handgun.RecoilPitchDegrees), "shot " + shot);
                recoil.Tick(0.2f);
            }
        }

        [Test]
        public void AimingTightensTheSpreadAndMovingWidensIt()
        {
            foreach (var spec in new[] { DemoTestWeapons.CarbineSpec(), DemoTestWeapons.PistolSpec() })
            {
                var hip = SpreadPattern.ConeDegrees(spec, 0f, 0f);
                var ads = SpreadPattern.ConeDegrees(spec, 1f, 0f);
                var moving = SpreadPattern.ConeDegrees(spec, 0f, 1f);

                Assert.That(ads, Is.GreaterThan(0f).And.LessThan(hip), spec.LogName);
                Assert.That(moving, Is.GreaterThan(hip), spec.LogName);
                Assert.That(spec.BlindFireSpreadDegrees, Is.GreaterThan(hip), spec.LogName + ": blind fire is still the widest");
            }

            Assert.That(
                DemoTestWeapons.CarbineSpec().AdsSpreadDegrees,
                Is.LessThan(DemoTestWeapons.PistolSpec().AdsSpreadDegrees),
                "the rifle groups tighter when aimed");
        }

        [Test]
        public void TheRepositoryDefaultWeaponAssetsStoreTheRecoilAndSpreadOfTheirJson()
        {
            AssertRecoilAndSpread(
                WeaponAssetBuilder.LoadDefinition(WeaponAssetBuilder.CarbinePath),
                JsonSpec(WeaponAssetBuilder.ReferenceJsonFolder, "Carbine556"),
                WeaponAssetBuilder.CarbinePath);
            AssertRecoilAndSpread(
                WeaponAssetBuilder.LoadDefinition(WeaponAssetBuilder.PistolPath),
                JsonSpec(WeaponAssetBuilder.ReferenceJsonFolder, "CompactPistol"),
                WeaponAssetBuilder.PistolPath);
        }

        private static void AssertRecoilAndSpread(WeaponDefinition asset, WeaponSpec expected, string path)
        {
            var message = $"{path}; run {nameof(WeaponAssetBuilder)}.Build if the committed asset predates the values";
            Assert.That(asset, Is.Not.Null, message);
            Assert.That(asset.RecoilPitchDegrees, Is.EqualTo(expected.RecoilPitchDegrees), message);
            Assert.That(asset.RecoilYawDegrees, Is.EqualTo(expected.RecoilYawDegrees), message);
            Assert.That(asset.RecoilRecoveryPerSecond, Is.EqualTo(expected.RecoilRecoveryPerSecond), message);
            Assert.That(asset.RecoilRecoveryDelaySeconds, Is.EqualTo(expected.RecoilRecoveryDelaySeconds), message);
            Assert.That(asset.HipSpreadDegrees, Is.EqualTo(expected.HipSpreadDegrees), message);
            Assert.That(asset.AdsSpreadDegrees, Is.EqualTo(expected.AdsSpreadDegrees), message);
            Assert.That(asset.MoveSpreadDegrees, Is.EqualTo(expected.MoveSpreadDegrees), message);
            Assert.That(asset.AdsMoveSpreadDegrees, Is.EqualTo(expected.AdsMoveSpreadDegrees), message);

            var spec = asset.ToSpec();
            Assert.That(spec.RecoilPitchDegrees, Is.EqualTo(expected.RecoilPitchDegrees), message);
            Assert.That(spec.RecoilYawDegrees, Is.EqualTo(expected.RecoilYawDegrees), message);
            Assert.That(spec.RecoilRecoveryPerSecond, Is.EqualTo(expected.RecoilRecoveryPerSecond), message);
            Assert.That(spec.RecoilRecoveryDelaySeconds, Is.EqualTo(expected.RecoilRecoveryDelaySeconds), message);
            Assert.That(spec.HipSpreadDegrees, Is.EqualTo(expected.HipSpreadDegrees), message);
            Assert.That(spec.AdsSpreadDegrees, Is.EqualTo(expected.AdsSpreadDegrees), message);
            Assert.That(spec.MoveSpreadDegrees, Is.EqualTo(expected.MoveSpreadDegrees), message);
            Assert.That(spec.AdsMoveSpreadDegrees, Is.EqualTo(expected.AdsMoveSpreadDegrees), message);
        }

        [Test]
        public void RefusesToOverwriteAnAssetOfAnotherType()
        {
            Build();
            AssetDatabase.DeleteAsset(_pistolPath);
            var other = ScriptableObject.CreateInstance<WeaponCatalogAsset>();
            AssetDatabase.CreateAsset(other, _pistolPath);

            Assert.Throws<InvalidOperationException>(() => Build());
            Assert.That(AssetDatabase.LoadMainAssetAtPath(_pistolPath), Is.InstanceOf<WeaponCatalogAsset>());
        }

        [Test]
        public void RejectsMissingWeaponsAndInvalidOrSharedPaths()
        {
            Build();
            var carbine = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(_carbinePath);
            var pistol = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(_pistolPath);
            var machete = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(_machetePath);

            Assert.Throws<ArgumentNullException>(() => WeaponAssetBuilder.BuildCatalogAndLoadoutAt(null, pistol, carbine, _catalogPath, _loadoutPath));
            Assert.Throws<ArgumentException>(() => WeaponAssetBuilder.BuildCatalogAndLoadoutAt(machete, pistol, carbine, "WeaponCatalog.asset", _loadoutPath));
            Assert.Throws<ArgumentException>(() => WeaponAssetBuilder.BuildCatalogAndLoadoutAt(machete, pistol, carbine, _catalogPath, "DefaultLoadout.asset"));
            Assert.Throws<ArgumentException>(() => WeaponAssetBuilder.BuildCatalogAndLoadoutAt(machete, pistol, carbine, _catalogPath, _catalogPath));
        }

        [Test]
        public void ConfigureDoesNotSaveAnInvalidCatalog()
        {
            var catalog = ScriptableObject.CreateInstance<WeaponCatalogAsset>();
            try
            {
                Assert.Throws<WeaponDefinitionException>(
                    () => WeaponAssetBuilder.Configure(catalog, Array.Empty<WeaponDefinition>()));
                Assert.Throws<WeaponDefinitionException>(
                    () => SceneBuildHelpers.CreateOrUpdateAsset<WeaponCatalogAsset>(
                        _catalogPath, c => WeaponAssetBuilder.Configure(c, Array.Empty<WeaponDefinition>())));
                Assert.That(AssetDatabase.LoadMainAssetAtPath(_catalogPath), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }

        // 既定の武器のアセットはテストの値 (DemoTestWeapons) で一時フォルダに作り、一覧と装備だけを builder に作らせる。
        private WeaponCatalogAsset Build()
        {
            var carbine = SceneBuildHelpers.CreateOrUpdateAsset<WeaponDefinition>(_carbinePath, d => WeaponAssetBuilder.Configure(d, DemoTestWeapons.CarbineSpec()));
            var pistol = SceneBuildHelpers.CreateOrUpdateAsset<WeaponDefinition>(_pistolPath, d => WeaponAssetBuilder.Configure(d, DemoTestWeapons.PistolSpec()));
            var machete = SceneBuildHelpers.CreateOrUpdateAsset<WeaponDefinition>(
                _machetePath, d => WeaponAssetBuilder.Configure(d, JsonSpec(WeaponAssetBuilder.JsonFolder, "Machete")));
            return WeaponAssetBuilder.BuildCatalogAndLoadoutAt(machete, pistol, carbine, _catalogPath, _loadoutPath);
        }

        private static WeaponSpec JsonSpec(string folder, string name)
        {
            var json = System.IO.File.ReadAllText(ShooterKitRoot.PhysicalPath(folder + "/" + name + ".json"));
            return WeaponJsonImporter.ParseSpec(json, name + ".json");
        }

        private static WeaponSpec PartsSpec()
        {
            return new WeaponSpec(
                "5d4c3b2a-1f0e-4d9c-8b7a-6f5e4d3c2b1a",
                "weapon.grenadeLauncher",
                WeaponCategory.Launcher,
                new TriggerSpec(FireMode.Semi, 0.6f, burst: new BurstSpec(2, 0.2f)),
                new AmmoSpec(6, 12, 0f, reloadStyle: ReloadStyle.PerRound, reloadSecondsPerRound: 0.5f),
                new RoundSpec(RoundDelivery.Projectile, 120f, projectile: new ProjectileSpec(30f, 1f, 4f, WorldHitResponse.Bounce, "fx.grenade")),
                new ImpactSpec(20, explosion: new ExplosionSpec(new ExplosionDamage(100, 5f, 1f), 50, DetonationTrigger.OnLifetimeEnd)),
                new AimSpec(zoomLevels: new[] { 45f, 30f }),
                new[] { new AmmoTypeSpec("1a2b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c5d", "ammo.grenade.smoke", impact: new ImpactSpec(0)) });
        }
    }
}

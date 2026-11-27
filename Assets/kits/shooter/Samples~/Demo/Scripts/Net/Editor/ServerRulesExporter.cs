using System;
using System.Collections.Generic;
using System.IO;
using GameKit.Core;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Shooter.Editor
{
    // Unity 抜きのサーバー (dotnet/Shooter.Server) はアセットとシーンを読めないので、試合の設定・ルール・出現地点・調整値を JSON に書き出して渡す。
    // 調整値をオフラインと揃えるため、KitTuning と同じ順 (入口の欄、無ければ Resources) で読む。
    public static class ServerRulesExporter
    {
        public const string MenuPath = "Shooter/Server Data/Export Rules And Points";
        public const string LogTag = "[ServerRulesExporter]";
        public const string DataFolder = "server-data";
        public const string RulesFolder = DataFolder + "/rules";
        public const string MapsFolder = DataFolder + "/maps";
        public const string TuningFolder = DataFolder + "/tuning";

        // サーバーの ServerRulesData.PlaylistsDirectoryName と同じ名前にする
        public const string PlaylistsFolder = DataFolder + "/playlists";

        public static string ProjectRoot => Path.GetDirectoryName(Application.dataPath) ?? ".";

        public const string PingFileName = "ping.json";
        public const string GrenadeFileName = "grenade.json";
        public const string InventoryFileName = "inventory.json";
        public const string KnockbackFileName = "knockback.json";
        public const string BotsFileName = "bots.json";
        public const string AssistFileName = "assist.json";

        // サーバーの ServerRulesData.LootTuningFileName と同じ名前にする
        public const string LootFileName = "loot.json";

        // サーバーの ServerCombatTuning.MeleeFileName・StreaksFileName と同じ名前にする
        public const string MeleeFileName = "melee.json";
        public const string StreaksFileName = "streaks.json";

        // サーバーの ServerRulesData.ExtractionTuningFileName・StashTuningFileName と同じ名前にする
        public const string ExtractionFileName = "extraction.json";
        public const string StashFileName = "stash.json";

        // サーバーの ServerPlayerStatusTuning.StaggerFileName・SuppressionFileName と同じ名前にする
        public const string StaggerFileName = "stagger.json";
        public const string SuppressionFileName = "suppression.json";

        // サーバーの ServerEmotes.EmotesFileName と同じ名前にする
        public const string EmotesFileName = "emotes.json";

        // サーバーの ServerCombatTuning.HolsterFileName と同じ名前にする
        public const string HolsterFileName = "holster.json";

        // サーバーの ServerDoors.BreachFileName と同じ名前にする
        public const string BreachFileName = "breach.json";

        public const string SupplyDropsFileName = "supply-drops.json";
        public const string RedeployFileName = "redeploy.json";

        // サーバーの ServerDeployables.CatalogFileName と同じ名前にする。手で書くファイルなので、書き出さずに読むだけにする
        public const string DeployablesFileName = "deployables.json";

        public static string DeployablesPath => Path.Combine(ProjectRoot, TuningFolder, DeployablesFileName);

        public const string AllDefaultsJson = "{}\n";

        // rules フォルダーに置く、書き出さずに手で保つルールの名前の一覧。scripts/server-rules.sh も同じファイルを読む
        public const string HandWrittenFileName = "hand-written.txt";

        public static string HandWrittenPath => Path.Combine(ProjectRoot, RulesFolder, HandWrittenFileName);

        // 試合の設定 (NetMatch) の並びに無くても書き出すモードのルール。サーバーはファイルの Objective で目的を選ぶので、
        // Objective が None のアセットは書き出しても並びのルールと同じ試合にしかならず、対象にしない。
        // 順を名前で決めるのは、アセットを探した順が変わっても書き出す順とログが変わらないようにするため
        public static IReadOnlyList<GameRulesDefinition> ModeRulesToExport(
            IEnumerable<GameRulesDefinition> assets, ICollection<string> handWritten)
        {
            if (assets == null)
            {
                throw new ArgumentNullException(nameof(assets));
            }

            if (handWritten == null)
            {
                throw new ArgumentNullException(nameof(handWritten));
            }

            var picked = new List<GameRulesDefinition>();
            foreach (var asset in assets)
            {
                if (asset != null && asset.Objective != MatchObjective.None && !handWritten.Contains(asset.name) && !picked.Contains(asset))
                {
                    picked.Add(asset);
                }
            }

            picked.Sort((x, y) => string.CompareOrdinal(x.name, y.name));
            return picked;
        }

        // 行の最初の語だけを名前にし、# から行末までを読まないのは、同じファイルを読む scripts/server-rules.sh と対象を揃えるため
        public static HashSet<string> ParseHandWritten(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var line in text.Split('\n'))
            {
                var comment = line.IndexOf('#');
                var words = (comment >= 0 ? line.Substring(0, comment) : line).Split(
                    new[] { ' ', '\t', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length > 0)
                {
                    names.Add(words[0]);
                }
            }

            return names;
        }

        // 一覧の無いゲームは手で保つルールが無い
        public static HashSet<string> LoadHandWritten(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("path must not be blank", nameof(path));
            }

            return File.Exists(path) ? ParseHandWritten(File.ReadAllText(path)) : new HashSet<string>(StringComparer.Ordinal);
        }

        // 書き出しも一覧も触れない *.json は誰も作り直さず、古いルールのままサーバーに読まれる
        public static IReadOnlyList<string> LeftoverRulesFiles(string directory, IReadOnlyList<string> written, ICollection<string> handWritten)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("directory must not be blank", nameof(directory));
            }

            if (written == null)
            {
                throw new ArgumentNullException(nameof(written));
            }

            if (handWritten == null)
            {
                throw new ArgumentNullException(nameof(handWritten));
            }

            var writtenNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var path in written)
            {
                writtenNames.Add(Path.GetFileName(path));
            }

            var leftover = new List<string>();
            if (!Directory.Exists(directory))
            {
                return leftover;
            }

            foreach (var path in Directory.GetFiles(directory, "*" + NetMatchFile.Extension))
            {
                var fileName = Path.GetFileName(path);
                if (!writtenNames.Contains(fileName) && !handWritten.Contains(Path.GetFileNameWithoutExtension(path)))
                {
                    leftover.Add(fileName);
                }
            }

            leftover.Sort(string.CompareOrdinal);
            return leftover;
        }

        public static IReadOnlyList<GameRulesDefinition> RulesToExport(NetMatchDefinition match)
        {
            if (match == null)
            {
                throw new ArgumentNullException(nameof(match));
            }

            var rules = new List<GameRulesDefinition>();
            void Add(GameRulesDefinition candidate)
            {
                if (candidate != null && !rules.Contains(candidate))
                {
                    rules.Add(candidate);
                }
            }

            Add(GameContent.Pick(c => c.Rules));
            foreach (var candidate in match.Rules)
            {
                Add(candidate);
            }

            return rules;
        }

        public static void Export()
        {
            var root = ProjectRoot;
            var written = new List<string>();
            written.AddRange(ExportRules(Path.Combine(root, RulesFolder)));
            written.AddRange(ExportPoints(Path.Combine(root, MapsFolder)));
            written.AddRange(ExportTuning(Path.Combine(root, TuningFolder)));
            Debug.Log(LogTag + " wrote " + written.Count + " file(s) under " + Path.Combine(root, DataFolder) + "; commit them");
        }

        public static IReadOnlyList<string> ExportRules(string directory)
        {
            var match = NetSceneBuilder.LoadOrBuildMatch();
            var rules = RulesToExport(match);
            if (rules.Count == 0)
            {
                throw new InvalidOperationException(
                    LogTag + " no rules to export; set Rules in GameContent or in " + AssetDatabase.GetAssetPath(match));
            }

            var handWritten = LoadHandWritten(HandWrittenPath);
            var written = WriteRules(directory, match, WithModeRules(rules, LoadModeRules(handWritten)));
            var leftover = LeftoverRulesFiles(directory, written, handWritten);
            if (leftover.Count > 0)
            {
                throw new InvalidOperationException(
                    LogTag + " " + directory + " holds rules this export does not write: " + string.Join(", ", leftover)
                    + "; delete them, give their rules asset an Objective, or list their names in " + HandWrittenPath + " and keep them by hand");
            }

            return written;
        }

        // 同じアセットを 2 度書かないよう、並びに既にあるルールは足さない
        public static IReadOnlyList<GameRulesDefinition> WithModeRules(
            IReadOnlyList<GameRulesDefinition> rules, IReadOnlyList<GameRulesDefinition> modeRules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            if (modeRules == null)
            {
                throw new ArgumentNullException(nameof(modeRules));
            }

            var combined = new List<GameRulesDefinition>(rules);
            foreach (var candidate in modeRules)
            {
                if (candidate != null && !combined.Contains(candidate))
                {
                    combined.Add(candidate);
                }
            }

            return combined;
        }

        // 対象を名前で持たずにルールのフォルダーから探すのは、ゲームが足したモードのアセットも Objective を付けるだけで書き出されるようにするため
        public static IReadOnlyList<GameRulesDefinition> LoadModeRules(ICollection<string> handWritten)
        {
            return ModeRulesToExport(LoadRulesAssets(GameRulesAssetBuilder.Folder), handWritten);
        }

        public static IReadOnlyList<GameRulesDefinition> LoadRulesAssets(string folder)
        {
            var assets = new List<GameRulesDefinition>();
            if (!AssetDatabase.IsValidFolder(folder))
            {
                return assets;
            }

            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(GameRulesDefinition), new[] { folder }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameRulesDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null)
                {
                    assets.Add(asset);
                }
            }

            return assets;
        }

        public static IReadOnlyList<string> WriteRules(string directory, NetMatchDefinition match, IReadOnlyList<GameRulesDefinition> rules)
        {
            return WriteRules(directory, match, rules, LoadDeployables(DeployablesPath));
        }

        public static IReadOnlyList<string> WriteRules(
            string directory, NetMatchDefinition match, IReadOnlyList<GameRulesDefinition> rules, DeployCatalog deployables)
        {
            if (match == null)
            {
                throw new ArgumentNullException(nameof(match));
            }

            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            if (deployables == null)
            {
                throw new ArgumentNullException(nameof(deployables));
            }

            // ロードアウトが無いときは ToFile が理由を添えて断るので、ここでは組まずに進める
            var loadout = NetMatchDefinition.PickLoadout(match);
            IReadOnlyList<string> abilities = null;
            if (loadout != null)
            {
                abilities = ToLoadoutAbilities(loadout, GameContent.Pick(c => c.AbilityCatalog), deployables);
                Debug.Log(LogTag + " loadout.abilities = [" + string.Join(", ", abilities) + "] from '" + loadout.name + "'");
            }

            var motor = match.ToMotor();
            Directory.CreateDirectory(directory);
            var written = new List<string>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var asset in rules)
            {
                // ファイル名がルールの名前なので、同じ名前の 2 つ目は 1 つ目を上書きしてしまう
                if (!names.Add(asset.name))
                {
                    throw new InvalidOperationException(LogTag + " two rules are named " + asset.name + "; rename one of " + AssetDatabase.GetAssetPath(asset));
                }

                var file = ToFile(match, asset, motor, abilities);
                var output = Path.Combine(directory, asset.name + NetMatchFile.Extension);
                File.WriteAllText(output, NetMatchFile.Write(file));
                written.Add(output);
            }

            return written;
        }

        // loadoutAbilities が null なら loadout.abilities を書かない (サーバーは deployables.json の全部を候補にする)
        public static NetMatchFile ToFile(
            NetMatchDefinition match, GameRulesDefinition asset, NetMotorSettings motor, IReadOnlyList<string> loadoutAbilities = null)
        {
            if (match == null)
            {
                throw new ArgumentNullException(nameof(match));
            }

            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            if (!asset.TryToRules(out var rules, out var error))
            {
                throw new InvalidOperationException(LogTag + " invalid game rules: " + error);
            }

            if (!match.TryCreateLoadout(out _, out error))
            {
                throw new InvalidOperationException(LogTag + " " + AssetDatabase.GetAssetPath(match) + " cannot be used by the match: " + error);
            }

            NetMatchSettings settings;
            try
            {
                settings = match.ToSettings();
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException || e is WeaponDefinitionException)
            {
                throw new InvalidOperationException(
                    LogTag + " " + AssetDatabase.GetAssetPath(match) + " has match settings the server cannot use: " + e.Message, e);
            }

            var loadout = NetMatchDefinition.PickLoadout(match);
            var catalog = NetMatchDefinition.PickCatalog(match);
            try
            {
                return new NetMatchFile(
                    asset.name,
                    rules,
                    settings,
                    loadout.name,
                    loadout.ToRule(),
                    loadout.ToSpecs(),
                    loadout.SwitchSeconds,
                    catalog != null ? catalog.name : null,
                    catalog != null ? catalog.ToCatalog() : null,
                    motor,
                    loadoutAbilities);
            }
            catch (ArgumentException e) when (e.ParamName == "settings")
            {
                throw new InvalidOperationException(
                    LogTag + " rules " + AssetDatabase.GetAssetPath(asset) + " cannot be played with " + AssetDatabase.GetAssetPath(match) + ": " + e.Message, e);
            }
            catch (ArgumentException e) when (e.ParamName == "abilities")
            {
                throw new InvalidOperationException(
                    LogTag + " the ability slots of '" + loadout.name + "' cannot be written to " + asset.name + NetMatchFile.Extension + ": " + e.Message, e);
            }
        }

        // サーバーもファイルが無ければ何も置かせない (ServerDeployables.TryLoadCatalog) ので、それに合わせて空の一覧にする
        public static DeployCatalog LoadDeployables(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("path must not be blank", nameof(path));
            }

            if (!File.Exists(path))
            {
                Debug.Log(LogTag + " " + path + " is missing, so the server places nothing and loadout.abilities is written empty");
                return DeployCatalog.Empty;
            }

            try
            {
                return DeployCatalogJson.Parse(File.ReadAllText(path), path);
            }
            catch (TuningJsonException e)
            {
                throw new InvalidOperationException(LogTag + " cannot read the deployables the server places: " + e.Message, e);
            }
        }

        public static bool IsPlaceableOnline(DeployCatalog deployables, string abilityId)
        {
            if (deployables == null)
            {
                throw new ArgumentNullException(nameof(deployables));
            }

            return !string.IsNullOrEmpty(abilityId) && deployables.TryGet(abilityId, out _);
        }

        // サーバーは loadout.abilities を選べる候補として読み、選んでいない人には先頭から枠の数だけを持たせるので、
        // 装備画面 (LoadoutSelectionAssets.TryCreateAbilities) と同じ AbilitySelection で組んだ既定の枠を先に並べてから、残りの候補を足す。
        // カタログの無いゲームは能力を選ばせないので、ロードアウトの能力だけを候補にする
        public static IReadOnlyList<string> ToLoadoutAbilities(LoadoutDefinition loadout, AbilityCatalogAsset catalog, DeployCatalog deployables)
        {
            if (loadout == null)
            {
                throw new ArgumentNullException(nameof(loadout));
            }

            if (deployables == null)
            {
                throw new ArgumentNullException(nameof(deployables));
            }

            var defaults = LoadoutSelectionAssets.DefaultAbilityIds(loadout);
            IReadOnlyList<string> offered;
            if (catalog != null)
            {
                try
                {
                    offered = catalog.ToIds();
                }
                catch (AbilityDefinitionException e)
                {
                    throw new InvalidOperationException(LogTag + " the ability catalog cannot be offered online: " + e.Message, e);
                }
            }
            else
            {
                var ids = new List<string>(defaults.Count);
                foreach (var id in defaults)
                {
                    if (!string.IsNullOrEmpty(id) && !ids.Contains(id))
                    {
                        ids.Add(id);
                    }
                }

                offered = ids;
            }

            var selection = new AbilitySelection(offered, null, defaults, id => IsPlaceableOnline(deployables, id), AbilityHost.SlotCount);
            var candidates = new List<string>(offered.Count);
            foreach (var id in selection.AbilityIds)
            {
                if (!string.IsNullOrEmpty(id))
                {
                    candidates.Add(id);
                }
            }

            foreach (var id in offered)
            {
                if (IsPlaceableOnline(deployables, id) && !candidates.Contains(id))
                {
                    candidates.Add(id);
                }
            }

            return candidates;
        }

        public static IReadOnlyList<string> ExportPoints(string directory)
        {
            EnsureScenes();
            Directory.CreateDirectory(directory);
            var written = new List<string>();
            foreach (var scenePath in BuildSceneList.ServerScenes)
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var spawns = new List<SpawnPoint>();
                var areas = new List<ControlPointArea>();
                var flagBases = new List<FlagBasePoint>();
                var carts = new List<PayloadCart>();
                var lootContainers = new List<LootContainer>();
                var extractionZones = new List<ExtractionZone>();
                var shrinkingZones = new List<ShrinkingZone>();
                var battleRoyaleRunners = new List<BattleRoyaleRunner>();
                var weaponFreeZones = new List<WeaponFreeZone>();
                foreach (var rootObject in scene.GetRootGameObjects())
                {
                    weaponFreeZones.AddRange(rootObject.GetComponentsInChildren<WeaponFreeZone>(false));
                    carts.AddRange(rootObject.GetComponentsInChildren<PayloadCart>(false));
                    lootContainers.AddRange(rootObject.GetComponentsInChildren<LootContainer>(false));
                    extractionZones.AddRange(rootObject.GetComponentsInChildren<ExtractionZone>(false));
                    shrinkingZones.AddRange(rootObject.GetComponentsInChildren<ShrinkingZone>(false));
                    battleRoyaleRunners.AddRange(rootObject.GetComponentsInChildren<BattleRoyaleRunner>(false));
                }

                // 並びは階層順にする (サーバーの SpawnSelector の同点の決め手になる)
                foreach (var rootObject in scene.GetRootGameObjects())
                {
                    foreach (var marker in rootObject.GetComponentsInChildren<NetSpawnPoint>())
                    {
                        spawns.Add(marker.ToSpawnPoint());
                    }

                    foreach (var zone in rootObject.GetComponentsInChildren<ControlPointZone>(false))
                    {
                        // 台車と一緒に動く範囲は拠点ではないので、拠点の並びに入れない
                        if (!IsCartZone(zone, carts))
                        {
                            areas.Add(zone.Area);
                        }
                    }

                    foreach (var flagBase in rootObject.GetComponentsInChildren<FlagBase>(false))
                    {
                        if (!flagBase.Side.TryGetTeam(out var team))
                        {
                            throw new InvalidOperationException(
                                LogTag + " FlagBase '" + flagBase.name + "' in " + scenePath + " belongs to side " + flagBase.Side.Id
                                + "; only sides 0 (Alpha) and 1 (Bravo) have flags");
                        }

                        flagBases.Add(new FlagBasePoint(team, flagBase.Position.ToNumerics()));
                    }
                }

                var payload = ToPayload(scenePath, carts, attackerSideId: FindAttackerSideId(scene));
                var output = Path.Combine(directory, scene.name + NetMapPoints.Extension);
                var points = new NetMapPoints(
                    spawns,
                    areas,
                    flagBases,
                    payload,
                    ServerDataMaps.ToLootContainers(scenePath, lootContainers),
                    ServerDataMaps.ToExtractionPoints(scenePath, extractionZones),
                    ServerDataMaps.ToZone(scenePath, shrinkingZones),
                    ServerDataMaps.ToDropHeight(scenePath, battleRoyaleRunners));
                File.WriteAllText(output, NetMapPoints.Write(points));
                written.Add(output);

                // ゾーンの無いシーンにも空のファイルを置くのは、前に書き出したゾーンが消したあとも残ってサーバーだけで効き続けないため
                var zones = ToWeaponFreeZones(scenePath, weaponFreeZones);
                var zonesOutput = Path.Combine(directory, scene.name + NetMapWeaponFreeZones.Extension);
                File.WriteAllText(zonesOutput, NetMapWeaponFreeZones.Write(zones));
                written.Add(zonesOutput);
                Debug.Log(LogTag + " wrote " + zonesOutput + " from " + scenePath + " (" + zones.Zones.Count + " weapon free zones)");
            }

            return written;
        }

        // 無効にしたゾーンはオフラインで何もしない (WeaponFreeZone.OnTriggerEnter) ので書き出さない。
        // サーバーは軸にそろえた箱で判定するので、回したゾーンは回した範囲を囲む箱になる
        public static NetMapWeaponFreeZones ToWeaponFreeZones(string scenePath, IReadOnlyList<WeaponFreeZone> zones)
        {
            if (zones == null)
            {
                throw new ArgumentNullException(nameof(zones));
            }

            var written = new List<MapWeaponFreeZone>(zones.Count);
            var owners = new Dictionary<string, WeaponFreeZone>(StringComparer.Ordinal);
            foreach (var zone in zones)
            {
                if (zone == null || !zone.enabled)
                {
                    continue;
                }

                var id = zone.ZoneId;
                if (owners.TryGetValue(id, out var other))
                {
                    throw new InvalidOperationException(
                        LogTag + " WeaponFreeZones '" + other.name + "' and '" + zone.name + "' in " + scenePath + " share the zone id '" + id
                        + "'; give each its own Zone Id");
                }

                owners.Add(id, zone);
                var box = WorldBox(zone);
                if (!IsAxisAligned(zone.transform.rotation))
                {
                    Debug.LogWarning(
                        LogTag + " WeaponFreeZone '" + zone.name + "' in " + scenePath + " is rotated; the server checks the axis-aligned box around it "
                        + box.min + " to " + box.max, zone);
                }

                try
                {
                    written.Add(new MapWeaponFreeZone(id, box.min.ToNumerics(), box.max.ToNumerics(), zone.Spec));
                }
                catch (ArgumentException e)
                {
                    throw new InvalidOperationException(LogTag + " WeaponFreeZone '" + zone.name + "' in " + scenePath + " cannot be written for the server: " + e.Message, e);
                }
            }

            return new NetMapWeaponFreeZones(written);
        }

        // シーンを開いただけでは Awake が走らず、Collider の無いゾーンは範囲の箱をまだ持たないので、遊ぶときに EnsureTrigger が作る箱と同じ形で数える。
        // 箱でない Collider は形を箱に直せないので、その Collider の囲む箱を使う
        public static Bounds WorldBox(WeaponFreeZone zone)
        {
            if (zone == null)
            {
                throw new ArgumentNullException(nameof(zone));
            }

            var collider = zone.GetComponent<Collider>();
            if (collider is BoxCollider box)
            {
                return Enclose(zone.transform, box.center, box.size);
            }

            if (collider != null)
            {
                return collider.bounds;
            }

            var size = zone.Size;
            return Enclose(zone.transform, new Vector3(0f, size.y * 0.5f, 0f), size);
        }

        private static Bounds Enclose(Transform transform, Vector3 localCenter, Vector3 localSize)
        {
            var half = localSize * 0.5f;
            var bounds = new Bounds(transform.TransformPoint(localCenter), Vector3.zero);
            for (var i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? -half.x : half.x,
                    (i & 2) == 0 ? -half.y : half.y,
                    (i & 4) == 0 ? -half.z : half.z);
                bounds.Encapsulate(transform.TransformPoint(localCenter + corner));
            }

            return bounds;
        }

        private static bool IsAxisAligned(Quaternion rotation)
        {
            return IsAxis(rotation * Vector3.right) && IsAxis(rotation * Vector3.up) && IsAxis(rotation * Vector3.forward);
        }

        private static bool IsAxis(Vector3 direction)
        {
            const float aligned = 0.9999f;
            return Mathf.Abs(direction.x) > aligned || Mathf.Abs(direction.y) > aligned || Mathf.Abs(direction.z) > aligned;
        }

        // 台車の無いシーンは null。サーバーは Payload のルールで道筋の無いマップを起動のときに断る
        public static NetMapPayload ToPayload(string scenePath, IReadOnlyList<PayloadCart> carts, int attackerSideId)
        {
            if (carts == null)
            {
                throw new ArgumentNullException(nameof(carts));
            }

            if (carts.Count == 0)
            {
                return null;
            }

            // サーバーは 1 台だけを押すので、どれを書き出すかを黙って選ばない
            if (carts.Count > 1)
            {
                throw new InvalidOperationException(
                    LogTag + " " + scenePath + " has " + carts.Count + " PayloadCarts; the server pushes one, so keep one");
            }

            var cart = carts[0];
            if (!new Side(attackerSideId).TryGetTeam(out var attackers))
            {
                throw new InvalidOperationException(
                    LogTag + " the PayloadRunner in " + scenePath + " makes side " + attackerSideId + " attack; only sides 0 (Alpha) and 1 (Bravo) can push");
            }

            var points = cart.Points;
            var positions = new System.Numerics.Vector3[points.Count];
            for (var i = 0; i < positions.Length; i++)
            {
                if (points[i] == null)
                {
                    throw new InvalidOperationException(LogTag + " PayloadCart '" + cart.name + "' in " + scenePath + " route point #" + i + " is not set");
                }

                positions[i] = points[i].position.ToNumerics();
            }

            var zone = CartZone(cart);
            var radius = zone != null ? zone.Radius : ControlPointZone.DefaultRadius;
            var height = zone != null ? zone.Height : ControlPointZone.DefaultHeight;
            try
            {
                return new NetMapPayload(attackers, positions, cart.Checkpoints, radius, height);
            }
            catch (ArgumentException e)
            {
                throw new InvalidOperationException(LogTag + " PayloadCart '" + cart.name + "' in " + scenePath + " has an invalid route: " + e.Message, e);
            }
        }

        // シーンの PayloadRunner の攻め側 (アセットがあればそちら)。Runner を置かないネット用のシーンは Runner の既定と同じ側にする
        public static int FindAttackerSideId(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (var rootObject in scene.GetRootGameObjects())
            {
                var runner = rootObject.GetComponentInChildren<PayloadRunner>(false);
                if (runner != null)
                {
                    return runner.Definition != null ? runner.Definition.AttackerSideId : runner.AttackerSideId;
                }
            }

            return PayloadRunner.DefaultAttackerSideId;
        }

        // PayloadCart.BuildRoute と同じ順 (欄、子) で範囲を探す。どちらも無ければ遊ぶときに既定の半径で足される
        private static ControlPointZone CartZone(PayloadCart cart)
        {
            return cart.Zone != null ? cart.Zone : cart.GetComponentInChildren<ControlPointZone>(true);
        }

        private static bool IsCartZone(ControlPointZone zone, IReadOnlyList<PayloadCart> carts)
        {
            foreach (var cart in carts)
            {
                if (CartZone(cart) == zone)
                {
                    return true;
                }
            }

            return false;
        }

        // サーバーは Resources も入口も読めないので、オフラインと同じ値で動くよう KitTuning が読むのと同じ JSON を写す
        public static IReadOnlyList<string> ExportTuning(string directory)
        {
            return new[]
            {
                ExportTuningFile(directory, PingFileName, c => c.PingTuning, KitTuning.PingResourcePath, TuningJson.ParsePing),
                ExportTuningFile(directory, GrenadeFileName, c => c.GrenadeTuning, KitTuning.GrenadeResourcePath, TuningJson.ParseGrenades),
                ExportTuningFile(directory, InventoryFileName, c => c.InventoryTuning, KitTuning.InventoryResourcePath, InventoryJson.Parse),
                ExportTuningFile(directory, KnockbackFileName, c => c.KnockbackTuning, KitTuning.KnockbackResourcePath, TuningJson.ParseKnockback),
                ExportTuningFile(directory, BotsFileName, c => c.BotsTuning, KitTuning.BotsResourcePath, TuningJson.ParseBots),
                ExportTuningFile(directory, AssistFileName, c => c.AssistTuning, KitTuning.AssistResourcePath, AssistJson.Parse),
                ExportTuningFile(directory, LootFileName, c => c.LootTuning, LootJson.ResourcePath, LootJson.Parse),
                ExportTuningFile(directory, SupplyDropsFileName, c => c.SupplyDropsTuning, KitTuning.SupplyDropsResourcePath, SupplyDropJson.Parse),
                ExportTuningFile(directory, RedeployFileName, c => c.RedeployTuning, RedeployJson.ResourcePath, RedeployJson.Parse),
                ExportTuningFile(directory, MeleeFileName, c => c.MeleeTuning, MeleeActionInstaller.TuningResourcePath, MeleeJson.Parse),
                ExportTuningFile(directory, StreaksFileName, c => c.StreaksTuning, StreakJson.ResourcePath, StreakJson.Parse),
                ExportTuningFile(directory, ExtractionFileName, c => c.ExtractionTuning, ExtractionJson.ResourcePath, ExtractionJson.Parse),
                ExportTuningFile(directory, StashFileName, c => c.StashTuning, StashJson.ResourcePath, StashJson.Parse),
                ExportTuningFile(directory, StaggerFileName, c => c.StaggerTuning, StaggerActionInstaller.TuningResourcePath, StaggerJson.Parse),
                ExportTuningFile(directory, SuppressionFileName, c => c.SuppressionTuning, KitTuning.SuppressionResourcePath, SuppressionJson.Parse),
                ExportTuningFile(directory, EmotesFileName, c => c.EmotesTuning, EmoteJson.ResourcePath, EmoteJson.Parse),
                ExportTuningFile(directory, HolsterFileName, c => c.HolsterTuning, KitTuning.HolsterResourcePath, HolsterJson.Parse),
                ExportTuningFile(directory, BreachFileName, c => c.BreachTuning, KitTuning.BreachResourcePath, BreachJson.Parse),
            };
        }

        public static string ExportTuningFile<T>(
            string directory,
            string fileName,
            Func<GameContent, TextAsset> select,
            string resourcePath,
            Func<string, string, T> parse)
        {
            if (parse == null)
            {
                throw new ArgumentNullException(nameof(parse));
            }

            var text = KitTuning.FindText(select, resourcePath, out var source);
            if (text == null)
            {
                // ファイルを消さずに項目を省いた JSON を置くのは、どの調整値もサーバーへ渡していることを server-data で見せるため (省いた項目は KitTuning が無いときに使うのと同じ既定値になる)
                text = AllDefaultsJson;
                source += " (missing; every item left out, so the defaults KitTuning uses)";
            }

            parse(text, source);
            var output = Path.Combine(directory, fileName);
            Directory.CreateDirectory(directory);
            File.WriteAllText(output, text);
            Debug.Log(LogTag + " wrote " + output + " from " + source);
            return output;
        }

        // まだ生成していないマップがあっても書き出せるよう、無いシーンを作ってから読む
        private static void EnsureScenes()
        {
            ServerDataMaps.EnsureScenes();
        }

        [MenuItem(MenuPath)]
        private static void ExportFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Export();
            }
            finally
            {
                if (setup.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                }
            }
        }
    }
}

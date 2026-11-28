using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GameKit.Core;
using GameKit.Net;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Server.Tests
{
    // server-data/rules の Juggernaut・Vip と書き出したマップを、make server-dotnet と同じ口
    // (ServerRulesData.TryLoad・ServerMapData.TryLoad・ServerModules.Create) で読み、Bot だけで試合が終わるまで進むことをログで確かめる。
    // Bot は人が 1 人もいないと置かれない (BotRoster.DesiredBots) ので、動かない人を 1 人だけ入れて、あとは Bot に任せる
    public sealed class AsymmetricModesEndToEndTests
    {
        private const string _arenaMap = "NetArena01";

        // VIP の向かう脱出地点を書き出したマップはこれだけ
        private const string _exitsMap = "NetBattleRoyale";
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private const int _watcher = 1;
        private const string _bots = "7";

        // 例の 600 秒を Bot だけで回し切ると遅いので、写した方の時間の制限だけを縮める。時間切れでも勝者 (か引き分け) の行は出る
        private const float _juggernautTimeLimitSeconds = 180f;

        // 時間の制限を過ぎてからこれだけ待っても終わらなければ失敗にする
        private const float _graceSeconds = 10f;

        private static readonly DateTime _time = new DateTime(2026, 11, 24, 9, 0, 0, DateTimeKind.Utc);
        private static readonly Regex _juggernautChanged = new Regex(
            "^" + Regex.Escape(JuggernautRulesLog.Tag) + @" change player=(\d+) from=(\S+) reason=\S+$", RegexOptions.CultureInvariant);
        private static readonly Regex _juggernautWinner = new Regex(
            "^" + Regex.Escape(JuggernautRulesLog.Tag) + @" winner( player=\d+ score=\d+|=" + Regex.Escape(MatchLog.Draw) + ")$",
            RegexOptions.CultureInvariant);
        private static readonly Regex _vipChosen = new Regex(
            "^" + Regex.Escape(VipLog.Tag) + @" chosen round=\d+ vip=(\d+) maxHealth=(\d+)$", RegexOptions.CultureInvariant);
        private static readonly Regex _vipRoundEnd = new Regex(
            "^" + Regex.Escape(VipLog.Tag) + @" round_end round=\d+ winner=(\d+) role=(attacker|defender) reason=\S+ wins="
            + Text(Side.Alpha.Id) + @":(\d+)," + Text(Side.Bravo.Id) + @":(\d+)$",
            RegexOptions.CultureInvariant);
        private static readonly Regex _vipEnded = new Regex(
            "^" + Regex.Escape(VipLog.Tag) + @" ended winner=(\d+)$", RegexOptions.CultureInvariant);

        private TempDirectory _directory;
        private LineLog _log;
        private DiscardingTransport _transport;
        private List<IServerModule> _modules;
        private ServerMatch _match;
        private ServerHost _host;
        private ServerRulesData _data;
        private int _scanned;

        [SetUp]
        public void SetUp()
        {
            _directory = new TempDirectory();
            _log = new LineLog();
            _transport = new DiscardingTransport();
            _modules = new List<IServerModule>();
            _scanned = 0;
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null && _host.State == ServerHostState.Running)
            {
                _host.Stop();
            }

            foreach (var module in _modules)
            {
                (module as IDisposable)?.Dispose();
            }

            _transport.Dispose();
            _log.Dispose();
            _directory.Dispose();
        }

        [Test]
        public void BotsPlayAJuggernautMatchOnTheArenaUntilItHasAWinner()
        {
            var root = CopyServerData("Juggernaut", _arenaMap);
            LimitJuggernautTime(root);

            Start(root, "Juggernaut", _arenaMap, "-bots", _bots);
            JoinWatcher();

            var spec = _data.Settings.Juggernaut;
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(_juggernautTimeLimitSeconds));
            var winner = RunUntil(line => _juggernautWinner.IsMatch(line), spec.StartDelaySeconds + spec.TimeLimitSeconds + _graceSeconds);

            Assert.That(winner, Is.Not.Null, "the juggernaut match did not end\n" + Tail());
            Assert.That(_log.Lines, Has.Some.StartsWith(JuggernautRulesLog.Tag + " net respawn="));
            var changes = _log.Lines.Select(line => _juggernautChanged.Match(line)).Where(match => match.Success).ToList();
            Assert.That(changes, Is.Not.Empty, "nobody became the juggernaut\n" + Tail());
            Assert.That(changes[0].Groups[2].Value, Is.EqualTo(JuggernautLog.Nobody), "the first juggernaut takes over from nobody");
            var first = int.Parse(changes[0].Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture);
            Assert.That(
                _log.Lines,
                Has.Some.EqualTo(JuggernautRulesLog.Body(first, spec)),
                "the juggernaut gets the body of the rules file");
            Assert.That(
                _log.Lines.IndexOf(changes[0].Value),
                Is.LessThan(_log.Lines.IndexOf(winner)),
                "the juggernaut is chosen before the match ends");
            Assert.That(_log.Lines, Has.Some.StartsWith("[Bot] added player=" + Text(BotRoster.FirstBotPlayerId)));
        }

        [Test]
        public void BotsEscortAndHuntTheVipOnTheMapWithExitsUntilATeamWinsTheRounds()
        {
            var root = CopyServerData("Vip", _exitsMap);

            Start(root, "Vip", _exitsMap, "-bots", _bots);
            JoinWatcher();

            var spec = _data.Settings.Vip;

            // 決着までに戦うラウンドは多くとも勝ちに要る数の倍から 1 を引いた数で、どのラウンドもラウンドの秒で終わる
            var rounds = (2 * spec.WinsToWin) - 1;
            var ended = RunUntil(line => _vipEnded.IsMatch(line), (rounds * spec.RoundSeconds) + _graceSeconds);

            Assert.That(ended, Is.Not.Null, "the vip match did not end\n" + Tail());
            Assert.That(_log.Lines, Has.Some.EqualTo(VipLog.SettingsLog(spec)));
            Assert.That(_log.Lines, Has.Some.StartsWith(VipLog.Tag + " net exits="));
            var chosen = _log.Lines.Select(line => _vipChosen.Match(line)).Where(match => match.Success).ToList();
            Assert.That(chosen, Is.Not.Empty, "no vip was chosen\n" + Tail());
            Assert.That(chosen[0].Groups[2].Value, Is.EqualTo(Text(spec.VipMaxHealth)), "the vip gets the health of the rules file");

            var roundEnds = _log.Lines.Select(line => _vipRoundEnd.Match(line)).Where(match => match.Success).ToList();
            Assert.That(roundEnds.Count, Is.InRange(spec.WinsToWin, rounds), "the match takes between the wins it needs and every round it can last");
            var last = roundEnds[roundEnds.Count - 1];
            var winner = _vipEnded.Match(ended).Groups[1].Value;
            Assert.That(last.Groups[1].Value, Is.EqualTo(winner), "the team that wins the last round wins the match");
            var alphaWins = int.Parse(last.Groups[3].Value, NumberStyles.None, CultureInfo.InvariantCulture);
            var bravoWins = int.Parse(last.Groups[4].Value, NumberStyles.None, CultureInfo.InvariantCulture);
            Assert.That(Math.Max(alphaWins, bravoWins), Is.EqualTo(spec.WinsToWin));
            Assert.That(alphaWins + bravoWins, Is.EqualTo(roundEnds.Count), "every round has a winner");
        }

        // 例のルール・調整値と書き出したマップを一時フォルダに写し、テストが書き換えても checkout の server-data を汚さない
        private string CopyServerData(string rulesName, string mapName)
        {
            var source = Path.Combine(RepositoryRoot(), ServerMapData.DataDirectoryName);
            var maps = Path.Combine(source, ServerMapData.MapsDirectoryName);
            foreach (var required in new[] { mapName + NetMapPoints.Extension, mapName + ServerMapData.CollisionExtension })
            {
                if (!File.Exists(Path.Combine(maps, required)))
                {
                    Assert.Ignore(Path.Combine(maps, required) + " has not been exported; run make export-server-rules in Unity and commit server-data");
                }
            }

            var rulesFile = Path.Combine(source, ServerRulesData.RulesDirectoryName, rulesName + NetMatchFile.Extension);
            Assert.That(File.Exists(rulesFile), Is.True, rulesFile);

            var root = Path.Combine(_directory.Path, "root");
            var target = Path.Combine(root, ServerMapData.DataDirectoryName);
            Copy(rulesFile, Path.Combine(target, ServerRulesData.RulesDirectoryName));
            var tuning = Path.Combine(source, ServerRulesData.TuningDirectoryName);
            if (Directory.Exists(tuning))
            {
                foreach (var file in Directory.GetFiles(tuning))
                {
                    Copy(file, Path.Combine(target, ServerRulesData.TuningDirectoryName));
                }
            }

            foreach (var file in Directory.GetFiles(maps, mapName + ".*"))
            {
                Copy(file, Path.Combine(target, ServerMapData.MapsDirectoryName));
            }

            return root;
        }

        private static void LimitJuggernautTime(string root)
        {
            var path = Path.Combine(root, ServerMapData.DataDirectoryName, ServerRulesData.RulesDirectoryName, "Juggernaut" + NetMatchFile.Extension);
            var file = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path)).AsObject();
            file["match"]["juggernaut"]["timeLimitSeconds"] = _juggernautTimeLimitSeconds;
            File.WriteAllText(path, file.ToJsonString());
        }

        private void Start(string root, string rulesName, string mapName, params string[] args)
        {
            var roots = new[] { root };
            Assert.That(ServerRulesData.TryLoad(rulesName, roots, out _data, out var error), Is.True, error);
            Assert.That(ServerMapData.TryLoad(mapName, roots, out var map, out error), Is.True, error);
            Assert.That(map.Points, Is.Not.Null);
            var launchArgs = new List<string> { ServerLaunch.RulesFlag, rulesName };
            launchArgs.AddRange(args);
            Assert.That(ServerLaunch.TryParse(launchArgs, _ => null, out var launch, out error), Is.True, error);
            var runtime = new ServerRuntime(TextWriter.Null, TextWriter.Null, _ => null, new ImmediateStopSignal(), roots, roots, () => _transport);
            Assert.That(ServerModules.Create(launch, runtime, _data, _modules, out error), Is.True, error);

            _match = new ServerMatch(_data.Settings.RestartDelaySeconds, _log, new ServerEventWriter(false, TextWriter.Null, _log), () => _time);
            var context = new ServerContext(
                launch.Options,
                "1.0.0",
                _log,
                map,
                _match,
                new ServerMessenger(_transport),
                new StubControl(),
                _data);
            _host = new ServerHost();
            foreach (var module in _modules)
            {
                _host.Register(module);
            }

            _host.Start(context);
        }

        // MatchServer と同じく、試合に加えてからモジュールに接続を知らせる。入力を送らないので、出た所で動かない
        private void JoinWatcher()
        {
            var connection = new ConnectionId((ulong)_watcher);
            _match.Join(connection, "watcher");
            _host.OnConnected(connection);
        }

        // 1 tick ずつ進め、その tick に増えた行だけを見る。見つけた行を返し、試合の時間で limitSeconds を過ぎたら null
        private string RunUntil(Func<string, bool> found, float limitSeconds)
        {
            for (var elapsed = 0f; elapsed < limitSeconds; elapsed += _tickSeconds)
            {
                _match.Tick(_tickSeconds);
                _host.Tick(_tickSeconds);
                var lines = _log.Lines;
                for (; _scanned < lines.Count; _scanned++)
                {
                    if (found(lines[_scanned]))
                    {
                        return lines[_scanned];
                    }
                }
            }

            return null;
        }

        private string Tail()
        {
            return string.Join("\n", _log.Lines.Where(line => !line.StartsWith("[Bot] pos", StringComparison.Ordinal)).TakeLast(60));
        }

        private static void Copy(string file, string directory)
        {
            Directory.CreateDirectory(directory);
            File.Copy(file, Path.Combine(directory, Path.GetFileName(file)));
        }

        private static string RepositoryRoot()
        {
            for (var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); directory != null; directory = directory.Parent)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, ServerMapData.DataDirectoryName, ServerRulesData.RulesDirectoryName)))
                {
                    return directory.FullName;
                }
            }

            Assert.Fail(ServerMapData.DataDirectoryName + " is not above " + TestContext.CurrentContext.TestDirectory);
            return null;
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}

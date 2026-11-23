using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // ジャガーノートは乱数の代わりに Pick (いつも並びの決まった添字を選ぶ) で決める。並びは ID の順で、ID 0 がプレイヤー。
    // Edit Mode では Update が走らないので、モードは Tick で、bot の知覚は EnemyAgent.Perceive で手で進める。
    public sealed class JuggernautRunnerTests
    {
        private const float StartDelaySeconds = 5f;
        private const float TimeLimitSeconds = 120f;
        private const float RespawnSeconds = 2f;
        private const int JuggernautMaxHealth = 250;
        private const float JuggernautSpeedMultiplier = 1.25f;
        private const int ScorePerKillAsJuggernaut = 1;
        private const int ScorePerJuggernautKill = 3;
        private const int ScoreLimit = 10;

        private readonly List<Object> _objects = new List<Object>();
        private JuggernautRunner _runner;
        private WeaponDefinition _rifle;
        private SideSpawnPoint[] _spawns;
        private int _pick;

        [SetUp]
        public void SetUp()
        {
            _rifle = Track(ScriptableObject.CreateInstance<WeaponDefinition>());
            _rifle.name = "Rifle";
            WeaponAssets.Configure(_rifle, TestWeapons.RifleSpec());
            _runner = Track(new GameObject("Runner")).AddComponent<JuggernautRunner>();
            _pick = 0;
            _runner.JuggernautPicker = count => Math.Min(_pick, count - 1);
            _spawns = new[]
            {
                SpawnPoint("Spawn-0", new Side(0), new Vector3(0f, 0f, 0f)),
                SpawnPoint("Spawn-1", new Side(1), new Vector3(20f, 0f, 0f)),
                SpawnPoint("Spawn-2", new Side(2), new Vector3(-20f, 0f, 0f)),
            };
        }

        [TearDown]
        public void TearDown()
        {
            _runner.Stop();
            GameContent.Use(null);
            for (var i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null)
                {
                    Object.DestroyImmediate(_objects[i]);
                }
            }

            _objects.Clear();
        }

        [Test]
        public void NobodyIsTheJuggernautBeforeTheStartDelayAndTheScenesSidesAreKept()
        {
            var player = Member("Player", 0);
            var bot = Member("Bot-1", 1);

            Begin(player, bot);

            Assert.That(_runner.Juggernaut.HasJuggernaut, Is.False);
            Assert.That(bot.Side, Is.EqualTo(new Side(1)), "the scene side is used as in free for all");
            Assert.That(player.Damageable.MaxHealth, Is.EqualTo(Damageable.DefaultMaxHealth));
            Assert.That(_runner.Marker, Is.Null);
            var detail = Detail();
            Assert.That(detail.JuggernautId, Is.Null);
            Assert.That(detail.SecondsUntilChosen, Is.EqualTo(StartDelaySeconds));
            Assert.That(detail.IsLocalPlayerJuggernaut, Is.EqualTo((bool?)false));
        }

        [Test]
        public void TheFirstJuggernautGetsTheMaxHealthOfTheSpecAndKeepsTheirWeapon()
        {
            var player = Member("Player", 0);
            var bot = Member("Bot-1", 1);
            Begin(player, bot);

            LogAssert.Expect(LogType.Log, "[Mode] juggernaut change player=0 from=- reason=chosen");
            LogAssert.Expect(LogType.Log, "[Mode] juggernaut body player=0 max-health=250 speed=-");
            _runner.Tick(StartDelaySeconds);

            Assert.That(_runner.JuggernautMember, Is.SameAs(player));
            Assert.That(_runner.IsJuggernaut(player), Is.True);
            Assert.That(player.Damageable.MaxHealth, Is.EqualTo(JuggernautMaxHealth));
            Assert.That(player.Damageable.Health.Current, Is.EqualTo(JuggernautMaxHealth), "a standing juggernaut is filled to the new max");
            Assert.That(bot.Damageable.MaxHealth, Is.EqualTo(Damageable.DefaultMaxHealth));
            Assert.That(Controller(player).Definitions, Is.EqualTo(new[] { _rifle }), "the juggernaut keeps their weapon");
            Assert.That(_runner.Marker, Is.Null, "the local player is told by the HUD, not by a marker over their own head");
            Assert.That(Detail().JuggernautId, Is.EqualTo((int?)0));
            Assert.That(Detail().IsLocalPlayerJuggernaut, Is.EqualTo((bool?)true));
        }

        [Test]
        public void ABotJuggernautIsFasterAndWearsThePlaceholderMarker()
        {
            var player = Member("Player", 0);
            var bot = Bot("Bot-1", 1, new Vector3(10f, 0f, 0f));
            _pick = 1;
            Begin(player, bot);

            LogAssert.Expect(LogType.Log, "[Mode] juggernaut body player=1 max-health=250 speed=1.25");
            _runner.Tick(StartDelaySeconds);

            var agent = bot.GetComponent<EnemyAgent>();
            Assert.That(_runner.JuggernautMember, Is.SameAs(bot));
            Assert.That(bot.Damageable.MaxHealth, Is.EqualTo(JuggernautMaxHealth));
            Assert.That(agent.MoveSpeedMultiplier, Is.EqualTo(JuggernautSpeedMultiplier));
            var marker = _runner.Marker;
            Assert.That(marker, Is.Not.Null);
            Assert.That(marker.Target, Is.SameAs(bot));
            Assert.That(marker.IsPlaceholder, Is.True);
            Assert.That(marker.GetComponent<Collider>(), Is.Null, "the marker does not block shots");
            Assert.That(marker.transform.parent.name, Is.EqualTo(JuggernautRunner.MarkerRootName));
            Assert.That(Detail().IsLocalPlayerJuggernaut, Is.EqualTo((bool?)false));
        }

        [Test]
        public void TheMarkerPrefabReplacesThePlaceholder()
        {
            var art = Track(GameObject.CreatePrimitive(PrimitiveType.Sphere));
            art.name = "JuggernautMarkerArt";
            _runner.MarkerPrefab = art;
            var player = Member("Player", 0);
            var bot = Member("Bot-1", 1);
            _pick = 1;
            Begin(player, bot);

            _runner.Tick(StartDelaySeconds);

            Assert.That(_runner.Marker, Is.Not.Null);
            Assert.That(_runner.Marker.IsPlaceholder, Is.False);
            Assert.That(_runner.Marker.GetComponentInChildren<Collider>(), Is.Null);
        }

        [Test]
        public void HidingTheMarkerLeavesNoMarker()
        {
            _runner.ShowsMarker = false;
            var player = Member("Player", 0);
            var bot = Member("Bot-1", 1);
            _pick = 1;
            Begin(player, bot);

            _runner.Tick(StartDelaySeconds);

            Assert.That(_runner.JuggernautMember, Is.SameAs(bot));
            Assert.That(_runner.Marker, Is.Null);
        }

        [Test]
        public void KillingTheJuggernautHandsItToTheKillerAndRestoresThePreviousOne()
        {
            var player = Member("Player", 0);
            var bot = Bot("Bot-1", 1, new Vector3(10f, 0f, 0f));
            var agent = bot.GetComponent<EnemyAgent>();
            var speed = agent.MoveSpeedMultiplier;
            _pick = 1;
            Begin(player, bot);
            _runner.Tick(StartDelaySeconds);
            player.Damageable.TakeDamage(30);

            LogAssert.Expect(LogType.Log, "[Mode] juggernaut change player=0 from=1 reason=taken-over");
            LogAssert.Expect(LogType.Log, "[Mode] juggernaut restore player=1 max-health=100 speed=1");
            LogAssert.Expect(LogType.Log, "[Mode] juggernaut body player=0 max-health=250 speed=-");
            Kill(bot, player);

            Assert.That(_runner.JuggernautMember, Is.SameAs(player));
            Assert.That(player.Damageable.MaxHealth, Is.EqualTo(JuggernautMaxHealth));
            Assert.That(player.Damageable.Health.Current, Is.EqualTo(JuggernautMaxHealth), "the killer takes over at full health");
            Assert.That(bot.Damageable.MaxHealth, Is.EqualTo(Damageable.DefaultMaxHealth));
            Assert.That(agent.MoveSpeedMultiplier, Is.EqualTo(speed));
            Assert.That(_runner.Session.GetScore(player.Side), Is.EqualTo((double)ScorePerJuggernautKill), "the kill is scored for the juggernaut it was");
            Assert.That(_runner.Marker, Is.Null, "the marker leaves the fallen bot and the player is told by the HUD");
            Assert.That(Detail().IsLocalPlayerJuggernaut, Is.EqualTo((bool?)true));
        }

        [Test]
        public void TheMarkerMovesToTheNextBotJuggernaut()
        {
            var player = Member("Player", 0);
            var first = Member("Bot-1", 1);
            var second = Member("Bot-2", 2);
            _pick = 1;
            Begin(player, first, second);
            _runner.Tick(StartDelaySeconds);
            var oldMarker = _runner.Marker;

            Kill(first, second);

            Assert.That(_runner.JuggernautMember, Is.SameAs(second));
            Assert.That(oldMarker == null, Is.True, "the old marker is removed");
            Assert.That(_runner.Marker.Target, Is.SameAs(second));
        }

        [Test]
        public void AFallenJuggernautComesBackWithTheirOwnHealth()
        {
            var player = Member("Player", 0);
            var bot = Member("Bot-1", 1);
            Begin(player, bot);
            _runner.Tick(StartDelaySeconds);

            Kill(player, bot);
            _runner.Tick(RespawnSeconds);

            Assert.That(player.IsAlive, Is.True);
            Assert.That(player.Damageable.MaxHealth, Is.EqualTo(Damageable.DefaultMaxHealth));
            Assert.That(player.Damageable.Health.Current, Is.EqualTo(Damageable.DefaultMaxHealth));
            Assert.That(bot.Damageable.MaxHealth, Is.EqualTo(JuggernautMaxHealth));
        }

        [Test]
        public void AKillByTheJuggernautKeepsIt()
        {
            var player = Member("Player", 0);
            var bot = Member("Bot-1", 1);
            Begin(player, bot);
            _runner.Tick(StartDelaySeconds);

            Kill(bot, player);

            Assert.That(_runner.JuggernautMember, Is.SameAs(player));
            Assert.That(_runner.Session.GetScore(player.Side), Is.EqualTo((double)ScorePerKillAsJuggernaut));
            Assert.That(bot.Damageable.MaxHealth, Is.EqualTo(Damageable.DefaultMaxHealth));
        }

        [Test]
        public void BotsThatAreNotTheJuggernautHuntItAndTheJuggernautBotShootsTheNearest()
        {
            var player = Member("Player", 0);
            player.transform.position = new Vector3(0f, 0f, 30f);
            var hunter = Bot("Bot-1", 1, new Vector3(4f, 0f, 0f));
            var juggernaut = Bot("Bot-2", 2, new Vector3(-4f, 0f, 0f));
            Physics.SyncTransforms();
            _pick = 2;
            Begin(player, hunter, juggernaut);

            var hunterAgent = hunter.GetComponent<EnemyAgent>();
            var juggernautAgent = juggernaut.GetComponent<EnemyAgent>();
            Assert.That(hunterAgent.PerceptionContributors.OfType<JuggernautTargetPerception>().Count(), Is.EqualTo(1));
            Assert.That(_runner.TargetPerceptions.Count, Is.EqualTo(2), "only bots get the contributor");

            hunterAgent.Perceive();
            Assert.That(hunterAgent.PerceivedTarget, Is.Null, "before the juggernaut is chosen the bot keeps its own target");

            _runner.Tick(StartDelaySeconds);

            LogAssert.Expect(LogType.Log, "[Mode] juggernaut bot-target player=1 target=2 role=hunter");
            var hunted = hunterAgent.Perceive();
            Assert.That(hunterAgent.PerceivedTarget, Is.SameAs(juggernaut.Damageable));
            Assert.That(hunted.Target.HasValue, Is.True);
            Assert.That(hunted.Target.Value.ToUnity(), Is.EqualTo(juggernaut.Damageable.GroundPoint));
            Assert.That(hunterAgent.Target, Is.Null, "the bot's own target is left alone");

            LogAssert.Expect(LogType.Log, "[Mode] juggernaut bot-target player=2 target=1 role=juggernaut");
            juggernautAgent.Perceive();
            Assert.That(juggernautAgent.PerceivedTarget, Is.SameAs(hunter.Damageable), "the hunter is nearer than the player");
        }

        [Test]
        public void AHunterThatCannotSeeTheJuggernautHearsWhereItIs()
        {
            var player = Member("Player", 0);
            var hunter = Bot("Bot-1", 1, new Vector3(4f, 0f, 0f));
            var juggernaut = Bot("Bot-2", 2, new Vector3(-4f, 0f, 0f));
            var wall = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            wall.transform.position = new Vector3(0f, 1.5f, 0f);
            wall.transform.localScale = new Vector3(1f, 3f, 6f);
            Physics.SyncTransforms();
            _pick = 2;
            Begin(player, hunter, juggernaut);
            _runner.Tick(StartDelaySeconds);

            var perception = hunter.GetComponent<EnemyAgent>().Perceive();

            Assert.That(perception.HasLineOfSight, Is.False);
            Assert.That(perception.HeardNoise, Is.True);
            Assert.That(perception.NoisePosition.HasValue, Is.True);
            Assert.That(perception.NoisePosition.Value.ToUnity(), Is.EqualTo(juggernaut.Damageable.GroundPoint), "the bot goes to where the juggernaut is");
        }

        [Test]
        public void SelectTargetPicksTheJuggernautOrTheNearestStandingOther()
        {
            var self = Member("Self", 0);
            var near = Member("Near", 1);
            var far = Member("Far", 2);
            self.transform.position = Vector3.zero;
            near.transform.position = new Vector3(3f, 0f, 0f);
            far.transform.position = new Vector3(9f, 0f, 0f);
            Physics.SyncTransforms();
            var members = new[] { self, near, far };

            Assert.That(JuggernautTargetPerception.SelectTarget(self, null, members), Is.Null);
            Assert.That(JuggernautTargetPerception.SelectTarget(self, far, members), Is.SameAs(far));
            Assert.That(JuggernautTargetPerception.SelectTarget(self, self, members), Is.SameAs(near));

            near.Damageable.Kill();
            Assert.That(JuggernautTargetPerception.SelectTarget(self, self, members), Is.SameAs(far), "fallen players are skipped");
            Assert.That(JuggernautTargetPerception.SelectTarget(far, near, members), Is.Null, "a fallen juggernaut is not hunted");
            Assert.Throws<ArgumentNullException>(() => JuggernautTargetPerception.SelectTarget(null, self, members));
            Assert.Throws<ArgumentNullException>(() => JuggernautTargetPerception.SelectTarget(self, self, null));
        }

        [Test]
        public void StopRestoresTheJuggernautAndRemovesTheContributorsAndTheMarker()
        {
            var player = Member("Player", 0);
            var bot = Bot("Bot-1", 1, new Vector3(10f, 0f, 0f));
            var agent = bot.GetComponent<EnemyAgent>();
            var speed = agent.MoveSpeedMultiplier;
            _pick = 1;
            Begin(player, bot);
            _runner.Tick(StartDelaySeconds);
            var marker = _runner.Marker;
            var juggernaut = _runner.Juggernaut;

            _runner.Stop();

            Assert.That(_runner.Juggernaut, Is.Null);
            Assert.That(bot.Damageable.MaxHealth, Is.EqualTo(Damageable.DefaultMaxHealth));
            Assert.That(bot.Damageable.Health.Current, Is.EqualTo(Damageable.DefaultMaxHealth));
            Assert.That(agent.MoveSpeedMultiplier, Is.EqualTo(speed));
            Assert.That(agent.PerceptionContributors.OfType<JuggernautTargetPerception>(), Is.Empty);
            Assert.That(marker == null, Is.True);

            juggernaut.Session.Tick(1f);
            Assert.That(bot.Damageable.MaxHealth, Is.EqualTo(Damageable.DefaultMaxHealth), "a stopped runner does not change anyone");
        }

        [Test]
        public void TheEndLineNamesTheWinner()
        {
            var player = Member("Player", 0);
            var first = Member("Bot-1", 1);
            Begin(player, first);
            _runner.Tick(StartDelaySeconds);
            Kill(first, player);

            LogAssert.Expect(LogType.Log, "[Mode] juggernaut end winner=0 score=1");
            _runner.Tick(TimeLimitSeconds);

            Assert.That(_runner.IsMatchOver, Is.True);
            Assert.That(_runner.Juggernaut.WinnerId, Is.EqualTo((int?)0));
        }

        [Test]
        public void TheSpecComesFromTheTuningJsonWithoutADefinition()
        {
            UseEmptyContent();

            var spec = _runner.CreateSpec();

            Assert.That(spec.JuggernautMaxHealth, Is.EqualTo(JuggernautRunner.DefaultJuggernautMaxHealth));
            Assert.That(spec.JuggernautSpeedMultiplier, Is.EqualTo(JuggernautRunner.DefaultJuggernautSpeedMultiplier));
            Assert.That(spec.ScoreLimit, Is.EqualTo(JuggernautRunner.DefaultScoreLimit));
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(JuggernautRunner.DefaultTimeLimitSeconds));
            Assert.That(spec.StartDelaySeconds, Is.EqualTo(JuggernautRunner.DefaultStartDelaySeconds));
            Assert.That(_runner.EffectiveRespawnSeconds, Is.EqualTo(JuggernautRunner.DefaultRespawnSeconds));

            _runner.RespawnSeconds = 7f;
            Assert.That(_runner.EffectiveRespawnSeconds, Is.EqualTo(7f), "the runner's own respawn time is read without a definition");
        }

        [Test]
        public void AWiredDefinitionGivesTheSpecAndTheRespawnTime()
        {
            _runner.Definition = Definition();

            var spec = _runner.CreateSpec();

            Assert.That(spec.JuggernautMaxHealth, Is.EqualTo(JuggernautMaxHealth));
            Assert.That(spec.JuggernautSpeedMultiplier, Is.EqualTo(JuggernautSpeedMultiplier));
            Assert.That(spec.ScorePerKillAsJuggernaut, Is.EqualTo(ScorePerKillAsJuggernaut));
            Assert.That(spec.ScorePerJuggernautKill, Is.EqualTo(ScorePerJuggernautKill));
            Assert.That(spec.ScoreLimit, Is.EqualTo(ScoreLimit));
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(TimeLimitSeconds));
            Assert.That(spec.StartDelaySeconds, Is.EqualTo(StartDelaySeconds));
            Assert.That(_runner.EffectiveRespawnSeconds, Is.EqualTo(RespawnSeconds));
        }

        [Test]
        public void ANewDefinitionHasTheRunnerDefaultsAndCopiesASpec()
        {
            var definition = Track(ScriptableObject.CreateInstance<JuggernautDefinition>());

            var spec = definition.ToSpec();
            Assert.That(spec.JuggernautMaxHealth, Is.EqualTo(JuggernautRunner.DefaultJuggernautMaxHealth));
            Assert.That(spec.JuggernautSpeedMultiplier, Is.EqualTo(JuggernautRunner.DefaultJuggernautSpeedMultiplier));
            Assert.That(spec.ScorePerKillAsJuggernaut, Is.EqualTo(JuggernautRunner.DefaultScorePerKillAsJuggernaut));
            Assert.That(spec.ScorePerJuggernautKill, Is.EqualTo(JuggernautRunner.DefaultScorePerJuggernautKill));
            Assert.That(spec.ScoreLimit, Is.EqualTo(JuggernautRunner.DefaultScoreLimit));
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(JuggernautRunner.DefaultTimeLimitSeconds));
            Assert.That(spec.StartDelaySeconds, Is.EqualTo(JuggernautRunner.DefaultStartDelaySeconds));
            Assert.That(definition.RespawnSeconds, Is.EqualTo(JuggernautRunner.DefaultRespawnSeconds));

            definition.CopyFrom(new JuggernautSpec(400, 1.3f, 2, 4, 20, 300f, 8f));
            spec = definition.ToSpec();
            Assert.That(spec.JuggernautMaxHealth, Is.EqualTo(400));
            Assert.That(spec.JuggernautSpeedMultiplier, Is.EqualTo(1.3f));
            Assert.That(spec.ScorePerKillAsJuggernaut, Is.EqualTo(2));
            Assert.That(spec.ScorePerJuggernautKill, Is.EqualTo(4));
            Assert.That(spec.ScoreLimit, Is.EqualTo(20));
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(300f));
            Assert.That(spec.StartDelaySeconds, Is.EqualTo(8f));
            Assert.Throws<ArgumentNullException>(() => definition.CopyFrom(null));
        }

        [Test]
        public void TryBeginFailsWithAnInvalidDefinitionAndNamesIt()
        {
            var definition = Definition();
            definition.name = "BrokenJuggernaut";
            definition.StartDelaySeconds = TimeLimitSeconds;
            _runner.Definition = definition;

            var error = Assert.Throws<InvalidOperationException>(() => _runner.CreateSpec());
            StringAssert.Contains("BrokenJuggernaut", error.Message);

            LogAssert.Expect(LogType.Error, new Regex("BrokenJuggernaut"));
            Assert.That(_runner.TryBegin(), Is.False);
            Assert.That(_runner.IsStarted, Is.False);
        }

        [Test]
        public void ANegativeRespawnTimeIsRejectedWithTheDefinitionsName()
        {
            var definition = Definition();
            definition.name = "SlowJuggernaut";
            definition.RespawnSeconds = -1f;
            _runner.Definition = definition;

            var error = Assert.Throws<InvalidOperationException>(() => _runner.CreateJuggernaut());
            StringAssert.Contains("SlowJuggernaut", error.Message);
        }

        [Test]
        public void AFailedBeginLeavesNoModeAttached()
        {
            _runner.Definition = Definition();
            Assert.Throws<ArgumentException>(
                () => _runner.Begin(_runner.CreateJuggernaut(), Array.Empty<SideMember>(), _spawns),
                "there is nobody to play");
            Assert.That(_runner.IsStarted, Is.False);
            Assert.That(_runner.Juggernaut, Is.Null);
            Assert.Throws<ArgumentNullException>(() => _runner.Begin(null, Array.Empty<SideMember>(), _spawns));
        }

        private void Begin(params SideMember[] members)
        {
            _runner.Definition = Definition();
            _runner.Begin(_runner.CreateJuggernaut(), members, _spawns, members[0]);
        }

        // 入口 (GameContent) をプロジェクトの Resources のものから空のものに替え、置いてある調整値を読ませない。
        private void UseEmptyContent()
        {
            var content = Track(ScriptableObject.CreateInstance<GameContent>());
            content.name = "EmptyContent";
            GameContent.Use(content);
        }

        private JuggernautDefinition Definition()
        {
            var definition = Track(ScriptableObject.CreateInstance<JuggernautDefinition>());
            definition.name = "Juggernaut";
            definition.JuggernautMaxHealth = JuggernautMaxHealth;
            definition.JuggernautSpeedMultiplier = JuggernautSpeedMultiplier;
            definition.ScorePerKillAsJuggernaut = ScorePerKillAsJuggernaut;
            definition.ScorePerJuggernautKill = ScorePerJuggernautKill;
            definition.ScoreLimit = ScoreLimit;
            definition.TimeLimitSeconds = TimeLimitSeconds;
            definition.StartDelaySeconds = StartDelaySeconds;
            definition.RespawnSeconds = RespawnSeconds;
            return definition;
        }

        private static void Kill(SideMember victim, SideMember killer)
        {
            if (killer == null)
            {
                victim.Damageable.Kill();
                return;
            }

            using (CombatEvents.Attribute(killer.gameObject, "rifle"))
            {
                victim.Damageable.Kill();
            }
        }

        private static WeaponController Controller(SideMember member)
        {
            return member.GetComponentInChildren<WeaponController>(true);
        }

        private JuggernautStatus Detail()
        {
            Assert.That(_runner.Status, Is.Not.Null);
            Assert.That(_runner.Status.Detail, Is.InstanceOf<JuggernautStatus>());
            return (JuggernautStatus)_runner.Status.Detail;
        }

        private SideMember Member(string name, int side)
        {
            var go = Track(new GameObject(name, typeof(CapsuleCollider)));
            go.transform.position = new Vector3(side * 5f, 0f, 10f);

            // 戻す位置は Collider の範囲から足元を測るので、物理を動かさない Edit Mode では移した位置を物理へ渡しておく
            Physics.SyncTransforms();
            go.AddComponent<Damageable>().Initialize(name, Damageable.DefaultMaxHealth);
            var member = go.AddComponent<SideMember>();
            member.Side = new Side(side);

            var view = new GameObject(name + "-Camera");
            view.transform.SetParent(go.transform, false);
            view.AddComponent<WeaponController>().Definitions = new[] { _rifle };
            return member;
        }

        private SideMember Bot(string name, int side, Vector3 ground)
        {
            var host = Track(GameObject.CreatePrimitive(PrimitiveType.Capsule));
            host.name = name;
            var member = host.AddComponent<SideMember>();
            member.Side = new Side(side);
            var agent = host.AddComponent<EnemyAgent>();
            agent.WeaponDefinition = _rifle;
            agent.Id = name;

            // 足元を ground に揃える。NavMeshAgent の baseOffset の分だけ持ち上げる。
            host.transform.SetPositionAndRotation(ground + (Vector3.up * host.GetComponent<NavMeshAgent>().baseOffset), Quaternion.identity);
            Assert.That(agent.TryInitialize(), Is.True);
            Physics.SyncTransforms();
            return member;
        }

        private SideSpawnPoint SpawnPoint(string name, Side side, Vector3 position)
        {
            var point = Track(new GameObject(name)).AddComponent<SideSpawnPoint>();
            point.transform.position = position;
            point.Side = side;
            return point;
        }

        private T Track<T>(T obj)
            where T : Object
        {
            _objects.Add(obj);
            return obj;
        }
    }
}

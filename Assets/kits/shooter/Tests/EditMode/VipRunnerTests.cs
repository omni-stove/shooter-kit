using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using GameKit.Core;
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
    // VIP は乱数の代わりに Pick (守る側を ID の順に並べたいつも決まった添字) で決める。並びは ID の順で、ID 0 がプレイヤー。
    // 最初のラウンドは Alpha (0) が攻め、Bravo (1) が守る。Edit Mode では Update が走らないので、モードは Tick で、bot の知覚は EnemyAgent.Perceive で手で進める。
    public sealed class VipRunnerTests
    {
        private const int VipMaxHealth = 250;
        private const float RoundSeconds = 60f;
        private const int WinsToWin = 2;
        private const float ExitHoldSeconds = 3f;
        private const int SwapEveryRounds = 1;
        private const float ExitRadius = 3f;

        private static readonly Vector3 _nearExit = new Vector3(30f, 0f, 0f);
        private static readonly Vector3 _farExit = new Vector3(-40f, 0f, 0f);

        private readonly List<Object> _objects = new List<Object>();
        private VipRunner _runner;
        private WeaponDefinition _rifle;
        private SideSpawnPoint[] _spawns;
        private int _pick;

        [SetUp]
        public void SetUp()
        {
            _rifle = Track(ScriptableObject.CreateInstance<WeaponDefinition>());
            _rifle.name = "Rifle";
            WeaponAssets.Configure(_rifle, TestWeapons.RifleSpec());
            _runner = Track(new GameObject("Runner")).AddComponent<VipRunner>();
            _pick = 0;
            _runner.VipPicker = count => Math.Min(_pick, count - 1);
            _spawns = new[]
            {
                SpawnPoint("Spawn-A0", Side.Alpha, new Vector3(0f, 0f, 0f)),
                SpawnPoint("Spawn-A1", Side.Alpha, new Vector3(0f, 0f, 4f)),
                SpawnPoint("Spawn-B0", Side.Bravo, new Vector3(20f, 0f, 0f)),
                SpawnPoint("Spawn-B1", Side.Bravo, new Vector3(20f, 0f, 4f)),
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
        public void TheVipGetsTheMaxHealthOfTheSpecAndAPlaceholderMarker()
        {
            var player = Member("Player", 0);
            var vip = Member("Def-1", 1);
            var escort = Member("Def-2", 1);

            LogAssert.Expect(LogType.Log, "[Vip] settings vipHealth=250 winsToWin=2 round=60s exitHold=3s swapEvery=1");
            LogAssert.Expect(LogType.Log, "[Vip] chosen round=1 vip=1");
            LogAssert.Expect(LogType.Log, "[Vip] body player=1 max-health=250");
            LogAssert.Expect(LogType.Log, "[Vip] round=1 attacker=0 defender=1");
            Begin(player, vip, escort);

            Assert.That(_runner.VipMember, Is.SameAs(vip));
            Assert.That(_runner.IsVip(vip), Is.True);
            Assert.That(_runner.IsDefender(vip), Is.True);
            Assert.That(_runner.IsAttacker(player), Is.True);
            Assert.That(vip.Damageable.MaxHealth, Is.EqualTo(VipMaxHealth));
            Assert.That(vip.Damageable.Health.Current, Is.EqualTo(VipMaxHealth), "the VIP starts the round at the new max");
            Assert.That(escort.Damageable.MaxHealth, Is.EqualTo(Damageable.DefaultMaxHealth));
            Assert.That(player.Damageable.MaxHealth, Is.EqualTo(Damageable.DefaultMaxHealth));

            var marker = _runner.Marker;
            Assert.That(marker, Is.Not.Null);
            Assert.That(marker.Target, Is.SameAs(vip));
            Assert.That(marker.IsPlaceholder, Is.True);
            Assert.That(marker.GetComponent<Collider>(), Is.Null, "the marker does not block shots");
            Assert.That(marker.transform.parent.name, Is.EqualTo(VipRunner.MarkerRootName));

            var detail = Detail();
            Assert.That(detail.VipId, Is.EqualTo((int?)1));
            Assert.That(detail.DefenderSide, Is.EqualTo(Side.Bravo));
            Assert.That(detail.IsLocalVip, Is.EqualTo((bool?)false));
        }

        [Test]
        public void TheMarkerPrefabReplacesThePlaceholderAndHidingItLeavesNone()
        {
            var art = Track(GameObject.CreatePrimitive(PrimitiveType.Sphere));
            art.name = "VipMarkerArt";
            _runner.MarkerPrefab = art;
            Begin(Member("Player", 0), Member("Def-1", 1));

            Assert.That(_runner.Marker, Is.Not.Null);
            Assert.That(_runner.Marker.IsPlaceholder, Is.False);
            Assert.That(_runner.Marker.GetComponentInChildren<Collider>(), Is.Null);

            _runner.Stop();
            Assert.That(_runner.Marker, Is.Null, "stopping removes the marker");
            _runner.ShowsMarker = false;
            Begin(Member("Player-2", 0), Member("Def-2", 1));
            Assert.That(_runner.VipMember, Is.Not.Null);
            Assert.That(_runner.Marker, Is.Null);
        }

        [Test]
        public void TheVipStayingAtTheExitWinsTheRoundAndTheNextRoundSwapsSidesAndSpawns()
        {
            var player = Member("Player", 0);
            var vip = Member("Def-1", 1);
            var escort = Member("Def-2", 1);
            Begin(player, vip, escort);

            MoveTo(vip, _nearExit);
            _runner.Tick(1f);
            Assert.That(_runner.Vip.ExitProgress, Is.EqualTo(1f), "the VIP has to stay for the hold seconds");
            Assert.That(_runner.Vip.RoundNumber, Is.EqualTo(1));

            LogAssert.Expect(LogType.Log, "[Vip] escaped vip=1");
            LogAssert.Expect(LogType.Log, "[Vip] round_end round=1 winner=1 role=defender reason=Escaped wins=0:0,1:1");
            LogAssert.Expect(LogType.Log, "[Vip] restore player=1 max-health=100");
            LogAssert.Expect(LogType.Log, "[Vip] swap attacker=1 defender=0");
            LogAssert.Expect(LogType.Log, "[Vip] spawn_swap points=4 attacker=1 defender=0");
            LogAssert.Expect(LogType.Log, "[Vip] chosen round=2 vip=0");
            LogAssert.Expect(LogType.Log, "[Vip] body player=0 max-health=250");
            LogAssert.Expect(LogType.Log, "[Vip] round=2 attacker=1 defender=0");
            _runner.Tick(2f);

            Assert.That(_runner.Vip.GetRoundWins(Side.Bravo), Is.EqualTo(1));
            Assert.That(_runner.Vip.RoundNumber, Is.EqualTo(2));
            Assert.That(_runner.Vip.Attacker, Is.EqualTo(Side.Bravo));
            Assert.That(_runner.Vip.Defender, Is.EqualTo(Side.Alpha));
            Assert.That(_runner.IsMatchOver, Is.False);

            Assert.That(vip.Damageable.MaxHealth, Is.EqualTo(Damageable.DefaultMaxHealth), "the last round's VIP goes back to the old max");
            Assert.That(vip.Damageable.Health.Current, Is.EqualTo(Damageable.DefaultMaxHealth));
            Assert.That(_runner.VipMember, Is.SameAs(player));
            Assert.That(player.Damageable.MaxHealth, Is.EqualTo(VipMaxHealth));
            Assert.That(player.Damageable.Health.Current, Is.EqualTo(VipMaxHealth));
            Assert.That(_runner.Marker, Is.Null, "the local player is told by the HUD, not by a marker over their own head");
            Assert.That(Detail().IsLocalVip, Is.EqualTo((bool?)true));
            Assert.That(Detail().DefenderSide, Is.EqualTo(Side.Alpha));

            Assert.That(_spawns[0].Side, Is.EqualTo(Side.Bravo), "the attackers' spawns now belong to the side that attacks");
            Assert.That(_spawns[1].Side, Is.EqualTo(Side.Bravo));
            Assert.That(_spawns[2].Side, Is.EqualTo(Side.Alpha));
            Assert.That(_spawns[3].Side, Is.EqualTo(Side.Alpha));
            Assert.That(player.Side, Is.EqualTo(Side.Alpha), "people keep their side; only the spawns swap");
            AssertGround(player, _spawns[2].Position);
            AssertGround(vip, _spawns[0].Position);
            AssertGround(escort, _spawns[1].Position);
        }

        [Test]
        public void LeavingTheExitBeforeTheHoldSecondsStartsTheHoldOver()
        {
            var vip = Member("Def-1", 1);
            Begin(Member("Player", 0), vip);

            MoveTo(vip, _nearExit);
            _runner.Tick(2f);
            MoveTo(vip, _nearExit + new Vector3(ExitRadius + 1f, 0f, 0f));
            _runner.Tick(0.5f);

            Assert.That(_runner.Vip.ExitProgress, Is.Zero);
            Assert.That(_runner.Vip.RoundNumber, Is.EqualTo(1));
            Assert.That(_runner.Vip.GetRoundWins(Side.Bravo), Is.Zero);
        }

        [Test]
        public void AnEscortAtTheExitDoesNotEscape()
        {
            var escort = Member("Def-2", 1);
            Begin(Member("Player", 0), Member("Def-1", 1), escort);

            MoveTo(escort, _nearExit);
            _runner.Tick(ExitHoldSeconds + 1f);

            Assert.That(_runner.Vip.ExitProgress, Is.Zero);
            Assert.That(_runner.Vip.RoundNumber, Is.EqualTo(1));
        }

        [Test]
        public void KillingTheVipWinsTheRoundForTheAttackersAndRestoresTheBody()
        {
            var player = Member("Player", 0);
            var vip = Member("Def-1", 1);
            Begin(player, vip, Member("Def-2", 1));

            LogAssert.Expect(LogType.Log, "[Vip] round_end round=1 winner=0 role=attacker reason=VipDown wins=0:1,1:0");
            LogAssert.Expect(LogType.Log, "[Vip] restore player=1 max-health=100");
            LogAssert.Expect(LogType.Log, "[Vip] swap attacker=1 defender=0");
            Kill(vip, player);
            _runner.Tick(0f);

            Assert.That(_runner.Vip.GetRoundWins(Side.Alpha), Is.EqualTo(1));
            Assert.That(_runner.Vip.LastRoundEnd, Is.EqualTo((VipRoundEnd?)VipRoundEnd.VipDown));
            Assert.That(_runner.Vip.RoundNumber, Is.EqualTo(2));
            Assert.That(vip.IsAlive, Is.True, "everyone is placed again for the next round");
            Assert.That(vip.Damageable.MaxHealth, Is.EqualTo(Damageable.DefaultMaxHealth));
            Assert.That(vip.Damageable.Health.Current, Is.EqualTo(Damageable.DefaultMaxHealth));
        }

        [Test]
        public void WinningWinsToWinRoundsEndsTheMatch()
        {
            var player = Member("Player", 0);
            var first = Member("Def-1", 1);
            Begin(player, first, Member("Def-2", 1));

            // 1 ラウンド目: 守る側 (Bravo) の VIP (ID 1) を倒して Alpha の勝ち。
            Kill(first, player);
            _runner.Tick(0f);

            // 2 ラウンド目: 守る側 (Alpha) の VIP はプレイヤーなので、倒されて Bravo の勝ち。
            Assert.That(_runner.VipMember, Is.SameAs(player));
            Kill(player, first);
            _runner.Tick(0f);
            Assert.That(_runner.Vip.GetRoundWins(Side.Bravo), Is.EqualTo(1));

            // 3 ラウンド目: また Bravo が守り、VIP (ID 1) を倒して Alpha が 2 勝。
            Assert.That(_runner.VipMember, Is.SameAs(first));
            LogAssert.Expect(LogType.Log, "[Vip] round_end round=3 winner=0 role=attacker reason=VipDown wins=0:2,1:1");
            LogAssert.Expect(LogType.Log, "[Vip] ended winner=0");
            Kill(first, player);
            _runner.Tick(0f);

            Assert.That(_runner.IsMatchOver, Is.True);
            Assert.That(_runner.Vip.Winner, Is.EqualTo((Side?)Side.Alpha));
            Assert.That(_runner.Status.IsEnded, Is.True);
            Assert.That(_runner.Status.Winner, Is.EqualTo((Side?)Side.Alpha));
            Assert.That(first.Damageable.MaxHealth, Is.EqualTo(Damageable.DefaultMaxHealth));
        }

        [Test]
        public void ABotVipHeadsForTheNearestExitAndBotEscortsFollowIt()
        {
            var player = Member("Player", 0);
            var hunter = Bot("Bot-A", 0, new Vector3(0f, 0f, 8f));
            var vip = Bot("Bot-D1", 1, new Vector3(22f, 0f, 8f));
            var escort = Bot("Bot-D2", 1, new Vector3(24f, 0f, 8f));
            var hunterRoute = hunter.GetComponent<EnemyAgent>().PatrolRoute.Count;

            LogAssert.Expect(LogType.Log, "[Vip] chosen round=1 vip=2");
            LogAssert.Expect(LogType.Log, "[Vip] bot_exit player=2 exit=1");
            LogAssert.Expect(LogType.Log, "[Vip] bot_escort player=3 vip=2");
            Begin(player, hunter, vip, escort);

            Assert.That(_runner.ExitPoints[0].Position, Is.EqualTo(_farExit), "exits are numbered by position");
            Assert.That(_runner.ExitPoints[1].Position, Is.EqualTo(_nearExit));
            Assert.That(_runner.NearestExitIndex(vip.GroundPosition), Is.EqualTo(1));
            Assert.That(vip.GetComponent<EnemyAgent>().PatrolRoute, Is.EqualTo(new[] { _nearExit }), "the VIP heads for the exit nearest to it");
            Assert.That(
                escort.GetComponent<EnemyAgent>().PatrolRoute,
                Is.EqualTo(HardpointRunner.RouteAround(vip.GroundPosition, VipRunner.DefaultEscortRadius, 0)),
                "a defender circles the VIP");
            Assert.That(hunter.GetComponent<EnemyAgent>().PatrolRoute.Count, Is.EqualTo(hunterRoute), "an attacker keeps its own route and hunts by perception");
        }

        [Test]
        public void EscortsAreReroutedOnlyAfterTheVipMovesFarEnough()
        {
            var vip = Bot("Bot-D1", 1, new Vector3(22f, 0f, 8f));
            var escort = Bot("Bot-D2", 1, new Vector3(24f, 0f, 8f));
            Begin(Member("Player", 0), vip, escort);
            var escortAgent = escort.GetComponent<EnemyAgent>();
            var first = Copy(escortAgent.PatrolRoute);

            MoveTo(vip, vip.GroundPosition + new Vector3(VipRunner.DefaultEscortRerouteDistance * 0.5f, 0f, 0f));
            _runner.Tick(0.1f);
            Assert.That(escortAgent.PatrolRoute, Is.EqualTo(first), "a short step does not route again");

            var moved = vip.GroundPosition + new Vector3(VipRunner.DefaultEscortRerouteDistance * 2f, 0f, 0f);
            MoveTo(vip, moved);
            _runner.Tick(0.1f);
            Assert.That(escortAgent.PatrolRoute, Is.EqualTo(HardpointRunner.RouteAround(vip.GroundPosition, VipRunner.DefaultEscortRadius, 0)));
        }

        [Test]
        public void AttackingBotsTargetTheVipAndDefendingBotsKeepTheirOwnTarget()
        {
            var player = Member("Player", 0);
            var hunter = Bot("Bot-A", 0, new Vector3(0f, 0f, 8f));
            var vip = Bot("Bot-D1", 1, new Vector3(22f, 0f, 8f));
            var escort = Bot("Bot-D2", 1, new Vector3(24f, 0f, 8f));
            Begin(player, hunter, vip, escort);
            Assert.That(_runner.TargetPerceptions.Count, Is.EqualTo(3));

            LogAssert.Expect(LogType.Log, "[Vip] bot_target player=1 vip=2");
            var hunterAgent = hunter.GetComponent<EnemyAgent>();
            hunterAgent.Perceive();
            Assert.That(hunterAgent.PerceivedTarget, Is.SameAs(vip.Damageable));

            var escortAgent = escort.GetComponent<EnemyAgent>();
            escortAgent.Perceive();
            Assert.That(escortAgent.PerceivedTarget, Is.Not.SameAs(vip.Damageable), "a defender never aims at its own VIP");

            _runner.Stop();
            Assert.That(hunterAgent.PerceptionContributors, Has.None.InstanceOf<VipTargetPerception>(), "stopping takes the contributor off the bot");
        }

        [Test]
        public void SelectTargetPicksTheVipOnlyForAStandingVipAndAnAttacker()
        {
            var attacker = Member("Player", 0);
            var vip = Member("Def-1", 1);

            Assert.That(VipTargetPerception.SelectTarget(attacker, vip, selfIsAttacker: true), Is.SameAs(vip));
            Assert.That(VipTargetPerception.SelectTarget(attacker, vip, selfIsAttacker: false), Is.Null);
            Assert.That(VipTargetPerception.SelectTarget(vip, vip, selfIsAttacker: true), Is.Null);
            Assert.That(VipTargetPerception.SelectTarget(attacker, null, selfIsAttacker: true), Is.Null);
            vip.Damageable.Kill();
            Assert.That(VipTargetPerception.SelectTarget(attacker, vip, selfIsAttacker: true), Is.Null);
            Assert.Throws<ArgumentNullException>(() => VipTargetPerception.SelectTarget(null, vip, true));
        }

        [Test]
        public void StoppingPutsBackTheSpawnSidesTheVipBodyAndTheBotRoutes()
        {
            var player = Member("Player", 0);
            var vip = Bot("Bot-D1", 1, new Vector3(22f, 0f, 8f));
            var vipAgent = vip.GetComponent<EnemyAgent>();
            var home = new[] { new Vector3(22f, 0f, 12f), new Vector3(26f, 0f, 12f) };
            vipAgent.SetPatrolRoute(home);
            Begin(player, vip);
            Kill(vip, player);
            _runner.Tick(0f);
            Assert.That(_spawns[0].Side, Is.EqualTo(Side.Bravo), "the second round has swapped the spawns");
            Assert.That(player.Damageable.MaxHealth, Is.EqualTo(VipMaxHealth));

            _runner.Stop();

            Assert.That(_spawns[0].Side, Is.EqualTo(Side.Alpha));
            Assert.That(_spawns[2].Side, Is.EqualTo(Side.Bravo));
            Assert.That(player.Damageable.MaxHealth, Is.EqualTo(Damageable.DefaultMaxHealth));
            Assert.That(vipAgent.PatrolRoute, Is.EqualTo(home));
            Assert.That(_runner.Vip, Is.Null);
        }

        [Test]
        public void ZeroSwapEveryKeepsTheSpawns()
        {
            var definition = Definition();
            definition.SwapEveryRounds = 0;
            var player = Member("Player", 0);
            var vip = Member("Def-1", 1);
            Begin(definition, player, vip);

            Kill(vip, player);
            _runner.Tick(0f);

            Assert.That(_runner.Vip.RoundNumber, Is.EqualTo(2));
            Assert.That(_runner.Vip.Defender, Is.EqualTo(Side.Bravo));
            Assert.That(_spawns[0].Side, Is.EqualTo(Side.Alpha));
            Assert.That(_runner.VipMember, Is.SameAs(vip));
            Assert.That(vip.Damageable.MaxHealth, Is.EqualTo(VipMaxHealth), "the same VIP is strengthened again");
        }

        [Test]
        public void TryBeginWithoutAnExitPointWarnsAndDoesNotStart()
        {
            Member("Player", 0);
            Member("Def-1", 1);

            LogAssert.Expect(LogType.Warning, "[Vip] not_started runner=Runner reason=no-exit-point");
            Assert.That(_runner.TryBegin(), Is.False);
            Assert.That(_runner.IsStarted, Is.False);
            Assert.That(_runner.Vip, Is.Null);
        }

        [Test]
        public void BeginWithoutAnExitPointIsRejected()
        {
            var player = Member("Player", 0);
            var members = new[] { player, Member("Def-1", 1) };
            _runner.Definition = Definition();

            Assert.Throws<ArgumentException>(() => _runner.Begin(_runner.CreateVip(), members, _spawns, Array.Empty<VipExitPoint>(), player));
            Assert.Throws<ArgumentNullException>(() => _runner.Begin(null, members, _spawns, new[] { Exit("Exit", _nearExit) }, player));
            Assert.That(_runner.IsStarted, Is.False);
            Assert.That(_runner.Vip, Is.Null);
        }

        [Test]
        public void ConfiguredExitPointsAreUsedBeforeTheScenesOnes()
        {
            var configured = Exit("Configured", _nearExit);
            Exit("InScene", _farExit);

            Assert.That(_runner.FindExitPoints(), Has.Count.EqualTo(2), "without configured exits the scene's are used");
            _runner.SetExitPoints(new[] { configured, null, configured });
            Assert.That(_runner.FindExitPoints(), Is.EqualTo(new[] { configured }));
        }

        [Test]
        public void TheExitPointCountsOnlyTheHorizontalRadius()
        {
            var exit = Exit("Exit", _nearExit);

            Assert.That(exit.Contains(_nearExit + new Vector3(ExitRadius, 50f, 0f)), Is.True, "height is not checked, as online");
            Assert.That(exit.Contains(_nearExit + new Vector3(ExitRadius + 0.01f, 0f, 0f)), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => exit.Radius = 0f);
            Assert.Throws<ArgumentOutOfRangeException>(() => exit.Radius = float.PositiveInfinity);
        }

        [Test]
        public void TheExitMarkerIsAPlaceholderDiscUnlessAPrefabIsGiven()
        {
            var exit = Exit("Exit", _nearExit);
            exit.RefreshMarker();

            Assert.That(exit.Marker, Is.Not.Null);
            Assert.That(exit.IsMarkerFromPrefab, Is.False);
            Assert.That(exit.Marker.GetComponent<Collider>(), Is.Null);
            Assert.That(exit.Marker.transform.localScale.x, Is.EqualTo(ExitRadius * 2f).Within(1e-4f));

            var art = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            var other = Exit("Exit-Art", _farExit);
            other.MarkerPrefab = art;
            other.RefreshMarker();
            Assert.That(other.IsMarkerFromPrefab, Is.True);
            Assert.That(other.Marker.GetComponentInChildren<Collider>(), Is.Null);
        }

        [Test]
        public void AnEmptyDefinitionReadsTheTuningFile()
        {
            UseEmptyContent();
            Assert.That(_runner.CreateSpec().SameAs(KitTuning.Vip), Is.True);
            Assert.That(_runner.CreateSpec().SameAs(VipSpec.Default), Is.True, "the bundled vip.json holds the defaults");

            var content = Track(ScriptableObject.CreateInstance<GameContent>());
            content.VipTuning = Track(new TextAsset("{\"vipMaxHealth\": 320}"));
            GameContent.Use(content);
            Assert.That(_runner.CreateSpec().VipMaxHealth, Is.EqualTo(320));

            _runner.Definition = Definition();
            Assert.That(_runner.CreateSpec().VipMaxHealth, Is.EqualTo(VipMaxHealth), "a wired definition is read instead");
        }

        [Test]
        public void TheDefinitionCopiesASpecAndNamesItselfWhenInvalid()
        {
            var definition = Definition();
            definition.CopyFrom(new VipSpec(vipMaxHealth: 180, roundSeconds: 90f, winsToWin: 3, exitHoldSeconds: 5f, swapEveryRounds: 2));
            var spec = definition.ToSpec();
            Assert.That(spec.VipMaxHealth, Is.EqualTo(180));
            Assert.That(spec.RoundSeconds, Is.EqualTo(90f));
            Assert.That(spec.WinsToWin, Is.EqualTo(3));
            Assert.That(spec.ExitHoldSeconds, Is.EqualTo(5f));
            Assert.That(spec.SwapEveryRounds, Is.EqualTo(2));
            Assert.Throws<ArgumentNullException>(() => definition.CopyFrom(null));

            definition.name = "BrokenVip";
            definition.ExitHoldSeconds = definition.RoundSeconds;
            var error = Assert.Throws<InvalidOperationException>(() => definition.ToSpec());
            StringAssert.Contains("BrokenVip", error.Message);
        }

        [Test]
        public void TryBeginFailsWithAnInvalidDefinitionAndNamesIt()
        {
            Exit("Exit", _nearExit);
            Member("Player", 0);
            Member("Def-1", 1);
            var definition = Definition();
            definition.name = "BrokenVip";
            definition.ExitHoldSeconds = RoundSeconds;
            _runner.Definition = definition;

            LogAssert.Expect(LogType.Error, new Regex("BrokenVip"));
            Assert.That(_runner.TryBegin(), Is.False);
            Assert.That(_runner.IsStarted, Is.False);
        }

        [Test]
        public void AnInvalidEscortRadiusIsRejected()
        {
            _runner.EscortRadius = 0f;
            _runner.Definition = Definition();
            var player = Member("Player", 0);

            Assert.Throws<InvalidOperationException>(
                () => _runner.Begin(_runner.CreateVip(), new[] { player, Member("Def-1", 1) }, _spawns, new[] { Exit("Exit", _nearExit) }, player));
            Assert.That(_runner.IsStarted, Is.False);
        }

        private void Begin(params SideMember[] members)
        {
            Begin(Definition(), members);
        }

        private void Begin(VipDefinition definition, params SideMember[] members)
        {
            _runner.Definition = definition;
            var exits = new[] { Exit("Exit-Near", _nearExit), Exit("Exit-Far", _farExit) };
            _runner.Begin(_runner.CreateVip(), members, _spawns, exits, members[0]);
        }

        // 入口 (GameContent) をプロジェクトの Resources のものから空のものに替え、置いてある調整値を読ませない。
        private void UseEmptyContent()
        {
            var content = Track(ScriptableObject.CreateInstance<GameContent>());
            content.name = "EmptyContent";
            GameContent.Use(content);
        }

        private VipDefinition Definition()
        {
            var definition = Track(ScriptableObject.CreateInstance<VipDefinition>());
            definition.name = "Vip";
            definition.VipMaxHealth = VipMaxHealth;
            definition.RoundSeconds = RoundSeconds;
            definition.WinsToWin = WinsToWin;
            definition.ExitHoldSeconds = ExitHoldSeconds;
            definition.SwapEveryRounds = SwapEveryRounds;
            return definition;
        }

        private static void Kill(SideMember victim, SideMember killer)
        {
            using (CombatEvents.Attribute(killer.gameObject, "rifle"))
            {
                victim.Damageable.Kill();
            }
        }

        private static void MoveTo(SideMember member, Vector3 ground)
        {
            var feetToRoot = member.transform.position - member.GroundPosition;
            member.transform.position = ground + feetToRoot;
            Physics.SyncTransforms();
        }

        private static void AssertGround(SideMember member, Vector3 expected)
        {
            var ground = member.GroundPosition;
            Assert.That(ground.x, Is.EqualTo(expected.x).Within(1e-3f), member.name);
            Assert.That(ground.z, Is.EqualTo(expected.z).Within(1e-3f), member.name);
        }

        private static Vector3[] Copy(IReadOnlyList<Vector3> route)
        {
            var copy = new Vector3[route.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = route[i];
            }

            return copy;
        }

        private VipStatus Detail()
        {
            Assert.That(_runner.Status, Is.Not.Null);
            Assert.That(_runner.Status.Detail, Is.InstanceOf<VipStatus>());
            return (VipStatus)_runner.Status.Detail;
        }

        private VipExitPoint Exit(string name, Vector3 position)
        {
            var exit = Track(new GameObject(name)).AddComponent<VipExitPoint>();
            exit.transform.position = position;
            exit.Radius = ExitRadius;
            return exit;
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

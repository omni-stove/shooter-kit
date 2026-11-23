using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // 移動・ルール・戦闘・持ち物・よろけ・ドアを同じホストで動かし、蹴る・仕掛ける要求を距離・秒数・品・状態で断るか進め、
    // 破ると板の当たりが外れて向こう側の人が怯み、ドアの状態が全員と途中から入った人に届くことを確かめる。
    // ドアは TestMaps.OneDoor (板の中心 (0, 1.5, 4)・前 +Z・幅 1.2・高さ 2・厚さ 0.1、床の上面は 0.5)
    public sealed class ServerDoorsTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private const string _chargeItemId = "5d0f8c3a-2b7e-4c19-9a46-1e8b3f7d2c50";

        private static readonly DateTime _time = new DateTime(2026, 11, 23, 9, 0, 0, DateTimeKind.Utc);

        // 板の奥 (-Z) で手の届く所・届かない所と、板の前 (+Z) の箱 (奥行き 5) の中・外
        private static readonly Vector3 _nearBehind = new Vector3(0f, 0.5f, 3f);
        private static readonly Vector3 _farBehind = new Vector3(0f, 0.5f, -2f);
        private static readonly Vector3 _inRoom = new Vector3(0f, 0.5f, 5.5f);
        private static readonly Vector3 _pastRoom = new Vector3(0f, 0.5f, 9.8f);

        private StringWriter _log;
        private StringWriter _json;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerContext _context;
        private ServerMapData _map;
        private ServerHost _host;
        private MatchRulesModule _rules;
        private ServerInventories _inventories;
        private ServerPlayerStatus _status;
        private ServerDoors _doors;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _json = new StringWriter();
            _transport = new RecordingTransport();
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null && _host.State == ServerHostState.Running)
            {
                _host.Stop();
            }

            _transport.Dispose();
            _log.Dispose();
            _json.Dispose();
        }

        [Test]
        public void TheSpecAndTheDoorsOfTheMapAreLoggedAtTheStart()
        {
            StartWithTwo();

            Assert.That(_doors.Count, Is.EqualTo(1));
            Assert.That(_doors.PhaseOf(0), Is.EqualTo(BreachDoorPhase.Closed));
            Assert.That(_log.ToString(), Does.Contain(BreachLog.Loaded(BreachSpec.Default) + " from=default"));
            Assert.That(_log.ToString(), Does.Contain("[Breach] server map=NetTest doors=1"));
            Assert.That(_map.Collision.IsGroupEnabled(DoorGroup), Is.True);
        }

        [Test]
        public void KickingFromTooFarIsRefused()
        {
            StartWithTwo();
            Place(1, _farBehind);

            Assert.That(_doors.Request(1, 0, BreachMethod.Kick, false), Is.EqualTo(ServerDoors.Refusal.OutOfReach));
            Step(1f);

            Assert.That(_doors.PhaseOf(0), Is.EqualTo(BreachDoorPhase.Closed));
            Assert.That(_doors.IsKicking(0), Is.False);
            Assert.That(_map.Collision.IsGroupEnabled(DoorGroup), Is.True);
            Assert.That(_log.ToString(), Does.Contain("[Breach] door=0 refused by=1 method=Kick phase=Closed reason=OutOfReach"));
        }

        [Test]
        public void PlantingFromTooFarIsRefused()
        {
            StartWithTwo();
            Place(1, _farBehind);

            Assert.That(_doors.Request(1, 0, BreachMethod.Charge, false), Is.EqualTo(ServerDoors.Refusal.OutOfReach));
            Assert.That(_doors.PhaseOf(0), Is.EqualTo(BreachDoorPhase.Closed));
        }

        [Test]
        public void AnUnknownDoorIsRefused()
        {
            StartWithTwo();

            Assert.That(_doors.Request(1, 1, BreachMethod.Kick, false), Is.EqualTo(ServerDoors.Refusal.UnknownDoor));
            Assert.That(_log.ToString(), Does.Contain("[Breach] door=1 refused by=1 method=Kick reason=UnknownDoor doors=1"));
        }

        [Test]
        public void ADeadPlayerCannotKick()
        {
            StartWithTwo();
            Place(1, _nearBehind);
            Kill(1);

            Assert.That(_doors.Request(1, 0, BreachMethod.Kick, false), Is.EqualTo(ServerDoors.Refusal.CannotAct));
        }

        [Test]
        public void AKickTakesTheKickSecondsAndThenOpensTheDoor()
        {
            StartWithTwo();
            Place(1, _nearBehind);

            Request(1, BreachMethod.Kick);
            Assert.That(_doors.IsKicking(0), Is.True);
            Step(BreachSpec.DefaultKickSeconds - 0.1f);
            Assert.That(_doors.PhaseOf(0), Is.EqualTo(BreachDoorPhase.Closed), "the foot has not reached the panel yet");
            Step(0.15f);

            Assert.That(_doors.PhaseOf(0), Is.EqualTo(BreachDoorPhase.Opened));
            Assert.That(_doors.IsKicking(0), Is.False);
            Assert.That(_doors.FarSignOf(0), Is.EqualTo(1f), "the kicker stood behind the panel, so the far side is its front");
            Assert.That(_log.ToString(), Does.Contain("[Breach] door=0 kick-began by=1 kickSeconds=0.60"));
            Assert.That(_log.ToString(), Does.Contain(BreachLog.Kicked("0", "1")));
        }

        [Test]
        public void ABreachedDoorCanBeWalkedAndShotThrough()
        {
            StartWithTwo();
            Place(1, _nearBehind);
            Assert.That(ThroughTheDoor(), Is.True, "the closed panel blocks the line");

            Request(1, BreachMethod.Kick);
            Step(1f);

            Assert.That(_map.Collision.IsGroupEnabled(DoorGroup), Is.False);
            Assert.That(ThroughTheDoor(), Is.False);
        }

        [Test]
        public void AKickStaggersWhoeverIsInTheRoomButNotTheKicker()
        {
            StartWithTwo();
            Place(1, _nearBehind);
            Place(2, _inRoom);

            Request(1, BreachMethod.Kick);
            Step(1f);

            Assert.That(StaggerOf(2), Is.EqualTo(StaggerKind.Stumble));
            Assert.That(StaggerOf(1), Is.EqualTo(StaggerKind.None));
            Assert.That(_log.ToString(), Does.Contain("[Breach] door=0 staggered method=Kick kind=Stumble seconds=1.50 count=1"));
            Assert.That(_log.ToString(), Does.Contain("[Stagger] imposed player=2 kind=Stumble seconds=1.50 source=breach"));
        }

        [Test]
        public void NobodyPastTheRoomIsStaggered()
        {
            StartWithTwo();
            Place(1, _nearBehind);
            Place(2, _pastRoom);

            Request(1, BreachMethod.Kick);
            Step(1f);

            Assert.That(StaggerOf(2), Is.EqualTo(StaggerKind.None));
            Assert.That(_log.ToString(), Does.Contain("staggered method=Kick kind=Stumble seconds=1.50 count=0"));
        }

        [Test]
        public void TheRoomIsOnTheOtherSideFromTheBreacher()
        {
            StartWithTwo();
            Place(1, new Vector3(0f, 0.5f, 5f));
            Place(2, new Vector3(0f, 0.5f, 2f));

            Request(1, BreachMethod.Kick);
            Step(1f);

            Assert.That(_doors.FarSignOf(0), Is.EqualTo(-1f));
            Assert.That(StaggerOf(2), Is.EqualTo(StaggerKind.Stumble));
        }

        [Test]
        public void NoOneIsStaggeredWhenTheStaggerRuleIsOff()
        {
            Start(DoorRules(Stagger.Off));
            JoinTwo();
            Place(1, _nearBehind);
            Place(2, _inRoom);

            Request(1, BreachMethod.Kick);
            Step(1f);

            Assert.That(_doors.PhaseOf(0), Is.EqualTo(BreachDoorPhase.Opened));
            Assert.That(StaggerOf(2), Is.EqualTo(StaggerKind.None));
            Assert.That(_log.ToString(), Does.Contain("count=0"));
        }

        [Test]
        public void MovingAwayDuringTheKickCancelsIt()
        {
            StartWithTwo();
            Place(1, _nearBehind);
            Request(1, BreachMethod.Kick);

            Place(1, _farBehind);
            Step(1f);

            Assert.That(_doors.PhaseOf(0), Is.EqualTo(BreachDoorPhase.Closed));
            Assert.That(_doors.IsKicking(0), Is.False);
            Assert.That(_map.Collision.IsGroupEnabled(DoorGroup), Is.True);
            Assert.That(_log.ToString(), Does.Contain("[Breach] door=0 kick-cancelled by=1 reason=OutOfReach"));
        }

        [Test]
        public void ABreachedDoorRefusesAnotherKick()
        {
            StartWithTwo();
            Place(1, _nearBehind);
            Request(1, BreachMethod.Kick);
            Step(1f);

            Assert.That(_doors.Request(1, 0, BreachMethod.Kick, false), Is.EqualTo(ServerDoors.Refusal.Breached));
            Assert.That(_doors.Request(1, 0, BreachMethod.Charge, false), Is.EqualTo(ServerDoors.Refusal.Breached));
        }

        [Test]
        public void ADoorSomeoneElseIsKickingIsBusy()
        {
            StartWithTwo();
            Place(1, _nearBehind);
            Place(2, new Vector3(0.3f, 0.5f, 3f));
            Request(1, BreachMethod.Kick);

            Assert.That(_doors.Request(2, 0, BreachMethod.Kick, false), Is.EqualTo(ServerDoors.Refusal.DoorBusy));
            Assert.That(_doors.Request(2, 0, BreachMethod.Charge, false), Is.EqualTo(ServerDoors.Refusal.DoorBusy));
            Assert.That(_doors.Request(1, 0, BreachMethod.Kick, false), Is.EqualTo(ServerDoors.Refusal.Unchanged), "a repeated request");
        }

        [Test]
        public void PlantingTakesThePlantSecondsAndTheChargeGoesOffAfterTheFuse()
        {
            Start(DoorRules(), spec: QuickCharge());
            JoinTwo();
            Place(1, _nearBehind);
            Place(2, _inRoom);

            Request(1, BreachMethod.Charge);
            Assert.That(_doors.PhaseOf(0), Is.EqualTo(BreachDoorPhase.Planting));
            Step(0.4f);
            Assert.That(_doors.PhaseOf(0), Is.EqualTo(BreachDoorPhase.Planting));
            Assert.That(_doors.StatusOf(0).Progress, Is.InRange(0.7f, 1f));
            Step(0.15f);
            Assert.That(_doors.PhaseOf(0), Is.EqualTo(BreachDoorPhase.Armed));
            Assert.That(_map.Collision.IsGroupEnabled(DoorGroup), Is.True, "an armed charge still blocks the way");
            Step(0.8f);
            Assert.That(_doors.PhaseOf(0), Is.EqualTo(BreachDoorPhase.Armed));
            Step(0.3f);

            Assert.That(_doors.PhaseOf(0), Is.EqualTo(BreachDoorPhase.Destroyed));
            Assert.That(_map.Collision.IsGroupEnabled(DoorGroup), Is.False);
            Assert.That(_log.ToString(), Does.Contain(BreachLog.PlantBegan("0", "1", 0.5f, 1f)));
            Assert.That(_log.ToString(), Does.Contain(BreachLog.Armed("0", "1", 1f)));
            Assert.That(_log.ToString(), Does.Contain(BreachLog.Detonated("0", "1", BreachSpec.DefaultChargeDamage, BreachSpec.DefaultChargeRadius)));
        }

        [Test]
        public void TheChargeHurtsAndBlastsWhoeverIsInTheRoom()
        {
            Start(DoorRules(), spec: QuickCharge());
            JoinTwo();
            Place(1, _nearBehind);
            Place(2, _inRoom);

            Request(1, BreachMethod.Charge);
            Step(2f);

            Assert.That(_rules.Session.GetHealth(2), Is.LessThan(NetMatchSettings.DefaultMaxHealth));
            Assert.That(_rules.Session.IsAlive(2), Is.True);
            Assert.That(StaggerOf(2), Is.EqualTo(StaggerKind.Blast));
            Assert.That(_log.ToString(), Does.Contain("[Breach] door=0 staggered method=Charge kind=Blast seconds=3.00 count=1"));
            Assert.That(_log.ToString(), Does.Contain("net exploded thrower=1"));
        }

        [Test]
        public void AChargeWithoutARadiusOnlyBreaksTheDoor()
        {
            Start(DoorRules(), spec: new BreachSpec(plantSeconds: 0f, fuseSeconds: 0f, chargeRadius: 0f));
            JoinTwo();
            Place(1, _nearBehind);
            Place(2, _inRoom);

            Request(1, BreachMethod.Charge);

            Assert.That(_doors.PhaseOf(0), Is.EqualTo(BreachDoorPhase.Destroyed), "no plant and no fuse go off at once");
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
            Assert.That(_log.ToString(), Does.Not.Contain("net exploded"));
            Assert.That(StaggerOf(2), Is.EqualTo(StaggerKind.Blast));
        }

        [Test]
        public void MovingAwayWhilePlantingCancelsIt()
        {
            Start(DoorRules(), spec: QuickCharge());
            JoinTwo();
            Place(1, _nearBehind);
            Request(1, BreachMethod.Charge);
            Step(0.2f);

            Place(1, _farBehind);
            Tick();

            Assert.That(_doors.PhaseOf(0), Is.EqualTo(BreachDoorPhase.Closed));
            Assert.That(_log.ToString(), Does.Contain(BreachLog.PlantCancelled("0", "1", "OutOfReach")));
            Step(2f);
            Assert.That(_map.Collision.IsGroupEnabled(DoorGroup), Is.True);
        }

        [Test]
        public void BeingStaggeredWhilePlantingCancelsIt()
        {
            Start(DoorRules(), spec: QuickCharge());
            JoinTwo();
            Place(1, _nearBehind);
            Request(1, BreachMethod.Charge);
            Step(0.2f);

            Assert.That(_status.Impose(1, StaggerKind.Flinch, 1f, "test"), Is.True);
            Tick();

            Assert.That(_doors.PhaseOf(0), Is.EqualTo(BreachDoorPhase.Closed));
            Assert.That(_log.ToString(), Does.Contain(BreachLog.PlantCancelled("0", "1", "Staggered")));
        }

        [Test]
        public void ACancelRequestStopsThePlantButNotAnArmedCharge()
        {
            Start(DoorRules(), spec: QuickCharge());
            JoinTwo();
            Place(1, _nearBehind);
            Request(1, BreachMethod.Charge);
            Step(0.2f);

            Request(1, BreachMethod.Charge, cancel: true);
            Assert.That(_doors.PhaseOf(0), Is.EqualTo(BreachDoorPhase.Closed));
            Assert.That(_log.ToString(), Does.Contain(BreachLog.PlantCancelled("0", "1", "Requested")));

            Request(1, BreachMethod.Charge);
            Step(0.6f);
            Assert.That(_doors.PhaseOf(0), Is.EqualTo(BreachDoorPhase.Armed));
            Assert.That(_doors.Request(1, 0, BreachMethod.Charge, true), Is.EqualTo(ServerDoors.Refusal.Unchanged));
            Step(1.1f);
            Assert.That(_doors.PhaseOf(0), Is.EqualTo(BreachDoorPhase.Destroyed));
        }

        [Test]
        public void TheArmedChargeGoesOffAfterThePlanterWalksAway()
        {
            Start(DoorRules(), spec: QuickCharge());
            JoinTwo();
            Place(1, _nearBehind);
            Request(1, BreachMethod.Charge);
            Step(0.6f);

            Place(1, _farBehind);
            Step(1.1f);

            Assert.That(_doors.PhaseOf(0), Is.EqualTo(BreachDoorPhase.Destroyed));
        }

        [Test]
        public void TheChargeNeedsTheItemWhenTheSpecNamesOne()
        {
            Start(DoorRules(), spec: QuickCharge(_chargeItemId), items: ChargeCatalog(starting: 0));
            JoinTwo();
            Place(1, _nearBehind);

            Assert.That(_doors.Request(1, 0, BreachMethod.Charge, false), Is.EqualTo(ServerDoors.Refusal.NoChargeItem));
            Assert.That(_doors.Request(1, 0, BreachMethod.Kick, false), Is.EqualTo(ServerDoors.Refusal.None), "kicking needs no item");
        }

        [Test]
        public void TheItemIsUsedWhenThePlantFinishesAndNotWhenItIsCancelled()
        {
            Start(DoorRules(), spec: QuickCharge(_chargeItemId), items: ChargeCatalog(starting: 1));
            JoinTwo();
            Place(1, _nearBehind);
            Assert.That(_inventories.InventoryOf(1).CountOf(_chargeItemId), Is.EqualTo(1));

            Request(1, BreachMethod.Charge);
            Step(0.2f);
            Request(1, BreachMethod.Charge, cancel: true);
            Assert.That(_inventories.InventoryOf(1).CountOf(_chargeItemId), Is.EqualTo(1), "a cancelled plant keeps the charge");

            Request(1, BreachMethod.Charge);
            Step(0.6f);

            Assert.That(_doors.PhaseOf(0), Is.EqualTo(BreachDoorPhase.Armed));
            Assert.That(_inventories.InventoryOf(1).CountOf(_chargeItemId), Is.Zero);
            Assert.That(_log.ToString(), Does.Contain("[Breach] door=0 charge-used by=1 item=" + _chargeItemId));
        }

        [Test]
        public void LosingTheItemWhilePlantingCancelsIt()
        {
            Start(DoorRules(), spec: QuickCharge(_chargeItemId), items: ChargeCatalog(starting: 1));
            JoinTwo();
            Place(1, _nearBehind);
            Request(1, BreachMethod.Charge);
            Step(0.2f);

            _inventories.InventoryOf(1).Remove(_chargeItemId, 1);
            Step(0.5f);

            Assert.That(_doors.PhaseOf(0), Is.EqualTo(BreachDoorPhase.Closed));
            Assert.That(_log.ToString(), Does.Contain(BreachLog.PlantCancelled("0", "1", "NoChargeItem")));
        }

        [Test]
        public void EveryoneIsSentTheDoorsWhenOneChanges()
        {
            StartWithTwo();
            Place(1, _nearBehind);

            Request(1, BreachMethod.Kick);
            Assert.That(States(2), Is.Empty, "the wind-up of a kick does not change the door");
            Step(1f);

            var states = States(2);
            Assert.That(states, Has.Count.EqualTo(1));
            Assert.That(states[0].DoorCount, Is.EqualTo(1));
            Assert.That(states[0][0].Phase, Is.EqualTo(BreachDoorPhase.Opened));
            Assert.That(states[0][0].FarSideFront, Is.True);
            Assert.That(States(1).Last()[0].Phase, Is.EqualTo(BreachDoorPhase.Opened));
        }

        [Test]
        public void EachStepOfAChargeIsSent()
        {
            Start(DoorRules(), spec: QuickCharge());
            JoinTwo();
            Place(1, _nearBehind);

            Request(1, BreachMethod.Charge);
            Step(2f);

            Assert.That(
                States(2).Select(s => s[0].Phase),
                Is.EqualTo(new[] { BreachDoorPhase.Planting, BreachDoorPhase.Armed, BreachDoorPhase.Destroyed }));
        }

        [Test]
        public void SomeoneWhoJoinsLaterIsSentTheBreachedDoor()
        {
            StartWithTwo();
            Place(1, _nearBehind);
            Request(1, BreachMethod.Kick);
            Step(1f);

            Join(3);

            var states = States(3);
            Assert.That(states, Has.Count.EqualTo(1));
            Assert.That(states[0].DoorCount, Is.EqualTo(1));
            Assert.That(states[0][0].Phase, Is.EqualTo(BreachDoorPhase.Opened));
            Assert.That(states[0][0].FarSideFront, Is.True);
        }

        [Test]
        public void SomeoneWhoJoinsBeforeAnyBreachIsSentNothing()
        {
            StartWithTwo();

            Join(3);

            Assert.That(States(3), Is.Empty);
        }

        [Test]
        public void ANewServerOnTheSameMapStartsWithEveryDoorClosed()
        {
            StartWithTwo();
            Place(1, _nearBehind);
            Request(1, BreachMethod.Kick);
            Step(1f);
            Assert.That(_map.Collision.IsGroupEnabled(DoorGroup), Is.False);
            _host.Stop();

            Start(DoorRules(), map: _map);

            Assert.That(_doors.PhaseOf(0), Is.EqualTo(BreachDoorPhase.Closed));
            Assert.That(_map.Collision.IsGroupEnabled(DoorGroup), Is.True);
            Assert.That(ThroughTheDoor(), Is.True);
        }

        [Test]
        public void AMapWithoutDoorsHasNone()
        {
            var plain = new ServerMapData("NetPlain", TestMaps.World(), TestMaps.NavMesh(), "memory", Points());
            Start(DoorRules(), map: plain);
            JoinTwo();

            Assert.That(_doors.Count, Is.Zero);
            Assert.That(_doors.Request(1, 0, BreachMethod.Kick, false), Is.EqualTo(ServerDoors.Refusal.UnknownDoor));
            Assert.That(_log.ToString(), Does.Contain("[Breach] server map=NetPlain doors=0"));
        }

        [Test]
        public void LeavingWhilePlantingCancelsIt()
        {
            Start(DoorRules(), spec: QuickCharge());
            JoinTwo();
            Place(1, _nearBehind);
            Request(1, BreachMethod.Charge);

            Assert.That(_match.Leave(new ConnectionId(1)), Is.True);
            _host.OnDisconnected(new ConnectionId(1));

            Assert.That(_doors.PhaseOf(0), Is.EqualTo(BreachDoorPhase.Closed));
            Assert.That(_log.ToString(), Does.Contain(BreachLog.PlantCancelled("0", "1", "Left")));
        }

        [Test]
        public void AMessageFromAConnectionNotInTheMatchIsIgnored()
        {
            StartWithTwo();

            _host.OnMessage(new ConnectionId(9), new BreachRequestMessage { DoorIndex = 0, Method = BreachMethod.Kick });

            Assert.That(_doors.IsKicking(0), Is.False);
        }

        [Test]
        public void TheSpecIsReadFromTheTuningFolderOrIsTheDefault()
        {
            using var root = new TempDirectory();
            Assert.That(ServerDoors.TryLoadSpec(new[] { root.Path }, out var fallback, out var noPath, out var noError), Is.True, noError);
            Assert.That(fallback, Is.SameAs(BreachSpec.Default));
            Assert.That(noPath, Is.Null);

            var tuning = Path.Combine(root.Path, ServerMapData.DataDirectoryName, ServerRulesData.TuningDirectoryName);
            Directory.CreateDirectory(tuning);
            File.WriteAllText(Path.Combine(tuning, ServerDoors.BreachFileName), "{ \"kickSeconds\": 0.25, \"reach\": 2 }");

            Assert.That(ServerDoors.TryLoadSpec(new[] { root.Path }, out var spec, out var path, out var error), Is.True, error);
            Assert.That(spec.KickSeconds, Is.EqualTo(0.25f));
            Assert.That(spec.Reach, Is.EqualTo(2f));
            Assert.That(spec.PlantSeconds, Is.EqualTo(BreachSpec.DefaultPlantSeconds));
            Assert.That(path, Is.EqualTo(Path.GetFullPath(Path.Combine(tuning, ServerDoors.BreachFileName))));
        }

        [Test]
        public void ABrokenSpecFileStopsTheLoad()
        {
            using var root = new TempDirectory();
            var tuning = Path.Combine(root.Path, ServerMapData.DataDirectoryName, ServerRulesData.TuningDirectoryName);
            Directory.CreateDirectory(tuning);
            File.WriteAllText(Path.Combine(tuning, ServerDoors.BreachFileName), "{ \"reach\": -1 }");

            Assert.That(ServerDoors.TryLoadSpec(new[] { root.Path }, out _, out _, out var error), Is.False);
            Assert.That(error, Does.Contain("cannot read breach tuning"));
        }

        [Test]
        public void TheModuleNeedsTheCombat()
        {
            Assert.Throws<ArgumentNullException>(() => new ServerDoors(null));
        }

        private static int DoorGroup => MapColliderGroups.Door(0);

        private static GameRules DoorRules(Stagger stagger = Stagger.On)
        {
            var actions = ActionRules.Default.WithDowned(Downed.Off).WithStagger(stagger);
            return GameRules.Default.WithActions(actions).WithSpawnProtectionSeconds(0f).WithIdleKickSeconds(0f).WithRegenPerSecond(0f);
        }

        // 0.5 秒で仕掛け、1 秒で爆ぜる
        private static BreachSpec QuickCharge(string chargeItemId = "")
        {
            return new BreachSpec(plantSeconds: 0.5f, fuseSeconds: 1f, chargeItemId: chargeItemId);
        }

        private static ItemCatalog ChargeCatalog(int starting)
        {
            var starts = starting > 0 ? new[] { new ItemCount(_chargeItemId, starting) } : Array.Empty<ItemCount>();
            return new ItemCatalog(
                new[] { new ItemSpec(_chargeItemId, "item.breachCharge", maxStack: 3, weight: 0.5f) },
                new InventoryRule(gridWidth: 4, gridHeight: 2),
                starts);
        }

        private static NetMapPoints Points()
        {
            return new NetMapPoints(
                new[] { new SpawnPoint(new Vector3(-5f, 0.5f, -5f), Team.Alpha), new SpawnPoint(new Vector3(5f, 0.5f, -5f), Team.Bravo) },
                Array.Empty<ControlPointArea>());
        }

        private void StartWithTwo()
        {
            Start(DoorRules());
            JoinTwo();
        }

        private void JoinTwo()
        {
            Join(1);
            Join(2);
            Step(0.2f);
        }

        private void Start(GameRules rules, BreachSpec spec = null, ItemCatalog items = null, ServerMapData map = null)
        {
            var doors = TestMaps.OneDoor();
            _map = map ?? new ServerMapData("NetTest", TestMaps.World(doors), TestMaps.NavMesh(), "memory", Points(), doors: doors);
            var data = new ServerRulesData("TeamDeathmatch", TestRules.File(rules), PingSpec.Default, "memory", null, items: items);
            _match = new ServerMatch(NetMatchSettings.DefaultRestartDelaySeconds, _log, new ServerEventWriter(true, _json, _log), () => _time);
            _context = new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "1.0.0",
                _log,
                _map,
                _match,
                new ServerMessenger(_transport),
                new StubControl(),
                data);
            var movement = new ServerMovement(ServerModules.MotorSettingsFrom(data));
            _rules = new MatchRulesModule();
            var combat = new ServerCombat(_rules, movement);
            _inventories = new ServerInventories(_rules, combat);
            _status = new ServerPlayerStatus(rules, null, _rules);
            movement.InputGate = _status;
            combat.PlayerStatus = _status;
            _doors = new ServerDoors(combat, _status, _inventories, spec);
            _host = new ServerHost();
            _host.Register(movement);
            _host.Register(_rules);
            _host.Register(combat);
            _host.Register(_inventories);
            _host.Register(_status);
            _host.Register(_doors);
            _host.Start(_context);
        }

        // MatchServer と同じく、試合に加えてからモジュールに接続を知らせる
        private void Join(int id)
        {
            var connection = new ConnectionId((ulong)id);
            _match.Join(connection, "device" + id);
            _host.OnConnected(connection);
        }

        // 移動のモジュールは入力が届いたときだけ体を書き直すので、入力を送らないこのテストでは置いた位置に留まる
        private void Place(int id, Vector3 position)
        {
            Assert.That(_context.Bodies.TryGet(id, out var body), Is.True);
            body.SetPose(position, 0f, PlayerStance.Standing);
        }

        private void Request(int id, BreachMethod method, bool cancel = false)
        {
            _host.OnMessage(new ConnectionId((ulong)id), new BreachRequestMessage { DoorIndex = 0, Method = method, Cancel = cancel });
        }

        private void Kill(int id)
        {
            _rules.ApplyDamage(null, id, NetMatchSettings.DefaultMaxHealth);
            Assert.That(_rules.Session.IsAlive(id), Is.False, _log.ToString());
        }

        private StaggerKind StaggerOf(int id)
        {
            return _status.TryGetMeters(id, out var meters) ? meters.StaggerKind : StaggerKind.None;
        }

        // 板の奥から前へ、板の中心の高さで線を引く
        private bool ThroughTheDoor()
        {
            return _map.Collision.Raycast(new Vector3(0f, 1.5f, 2f), Vector3.UnitZ, 4f, ServerHitboxes.RaycastLayers, out _);
        }

        private List<DoorStateMessage> States(int connection)
        {
            return _transport.Read<DoorStateMessage>(connection, m =>
            {
                var copy = new DoorStateMessage();
                for (var i = 0; i < m.DoorCount; i++)
                {
                    copy.Add(m[i].Phase, m[i].FarSideFront);
                }

                return copy;
            });
        }

        private void Step(float seconds)
        {
            for (var elapsed = 0f; elapsed < seconds; elapsed += _tickSeconds)
            {
                Tick();
            }
        }

        private void Tick()
        {
            _match.Tick(_tickSeconds);
            _host.Tick(_tickSeconds);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Numerics;
using GameKit.World;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientFeaturesTests
    {
        private const int _playerId = 3;
        private const float _ladderX = 3f;
        private const float _doorZ = 3f;
        private static readonly Vector3 _spawn = new Vector3(0f, 1.1f, 0f);

        private FakeTransport _transport;
        private MatchClient _client;

        [SetUp]
        public void SetUp()
        {
            _transport = new FakeTransport();
            _client = new MatchClient(_transport, "0.2.0", "device1", "tester", _ => { });
        }

        [TearDown]
        public void TearDown()
        {
            _client.Dispose();
            _transport.Dispose();
        }

        [Test]
        public void TheClientKeepsTheMotorSettingsTheServerAcceptedItWith()
        {
            var motor = new NetMotorSettings(
                new MotorSpec(7f, 10f, 1.2f, 20f, 0.1f, 2.5f),
                new StanceGeometry(1.8f, 0f, 0.6f, 1.2f, 1f, 0.4f),
                CapsuleMoverSpec.Default,
                new ProneSpec(1f, 1.5f));

            Connect(motor, -20f);

            Assert.That(_client.Motor.Motor.WalkSpeed, Is.EqualTo(7f));
            Assert.That(_client.Motor.Stance.Radius, Is.EqualTo(0.4f));
            Assert.That(_client.Motor.Prone.Speed, Is.EqualTo(1.5f));
            Assert.That(_client.KillPlaneY, Is.EqualTo(-20f));
        }

        [Test]
        public void GrabbingALadderTurnsTheViewToTheLadderWhenTheRulesAllowIt()
        {
            var match = Join(Rules(ladder: Ladder.On));
            var grabbed = new List<(int Index, float Yaw)>();
            match.LadderGrabbed += (index, yaw) => grabbed.Add((index, yaw));

            var towardLadder = MathF.PI / 2f;
            for (var i = 0; i < 180 && grabbed.Count == 0; i++)
            {
                match.Step(new Vector2(0f, 1f), towardLadder, InputButtons.None);
            }

            Assert.That(grabbed, Has.Count.EqualTo(1));
            Assert.That(grabbed[0].Index, Is.EqualTo(0));
            Assert.That(grabbed[0].Yaw, Is.EqualTo(MathF.PI / 2f).Within(1e-5f), "the ladder faces +X");
            Assert.That(match.LocalState.Ladder.IsClimbing, Is.True);

            match.Step(new Vector2(0f, 1f), towardLadder, InputButtons.None);
            Assert.That(grabbed, Has.Count.EqualTo(1), "climbing on does not turn the view again");
        }

        [Test]
        public void WithoutTheLadderRuleTheLadderIsNotGrabbed()
        {
            var match = Join(Rules(ladder: Ladder.Off));
            var grabbed = 0;
            match.LadderGrabbed += (_, _) => grabbed++;

            for (var i = 0; i < 180; i++)
            {
                match.Step(new Vector2(0f, 1f), MathF.PI / 2f, InputButtons.None);
            }

            Assert.That(grabbed, Is.Zero);
            Assert.That(match.LocalState.Ladder.IsClimbing, Is.False);
        }

        [TestCase(Prone.On, true)]
        [TestCase(Prone.Off, false)]
        public void TheProneButtonLiesDownOnlyWhenTheRulesAllowIt(Prone rule, bool expected)
        {
            var match = Join(Rules(prone: rule));
            for (var i = 0; i < 30; i++)
            {
                match.Step(Vector2.Zero, 0f, InputButtons.None);
            }

            var state = match.Step(Vector2.Zero, 0f, SnapshotMotion.Buttons(false, false, false, false, false, false, prone: true));

            Assert.That(state.IsProne, Is.EqualTo(expected));
            match.SendInput();
            Assert.That(_transport.LastInput[_transport.LastInput.Count - 1].Has(InputButtons.Prone), Is.True);
        }

        [Test]
        public void AProneRowReadsAsTheProneStanceForPredictionAndForTheRemoteView()
        {
            var row = new PlayerSnapshot { PlayerId = 4, Position = _spawn, IsProne = true, WeaponId = "rifle" };

            Assert.That(SnapshotMotion.ToState(row).Stance, Is.EqualTo(PlayerStance.Prone));
            var pose = RemotePose.From(row);
            Assert.That(pose.IsProne, Is.True);
            Assert.That(pose.IsCrouched, Is.False);
            Assert.That(pose.WeaponId, Is.EqualTo("rifle"));
        }

        [Test]
        public void ABrokenPieceLeavesThePredictionsCollisionAndARepairedOneComesBack()
        {
            var piece = MapColliderGroups.Piece(1, 70);
            var world = new ClientWorld(
                "Test",
                new WorldMap(new[]
                {
                    WorldCollider.Box(1, 0, WorldCollider.StaticGroup, new Vector3(0f, -0.5f, 0f), new Vector3(50f, 0.5f, 50f), Quaternion.Identity),
                    WorldCollider.Box(2, 0, piece, new Vector3(5f, 1f, 0f), new Vector3(0.2f, 1f, 2f), Quaternion.Identity),
                }),
                NetMapLadders.None);
            var origin = new Vector3(0f, 1f, 0f);
            Assert.That(world.Collision.Raycast(origin, Vector3.UnitX, 10f, ~0, out _), Is.True);

            var message = new StructureStateMessage { StructureId = 1 };
            message.SetWords(new ulong[] { 0UL, 1UL << 6 });
            Assert.That(world.Apply(message, out var changed), Is.True);
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(world.BrokenPieceCount, Is.EqualTo(1));
            Assert.That(world.Collision.Raycast(origin, Vector3.UnitX, 10f, ~0, out _), Is.False, "piece 70 is word 1 bit 6");

            Assert.That(world.Apply(message, out changed), Is.True);
            Assert.That(changed, Is.Zero, "the same state changes nothing");

            message.SetWords(Array.Empty<ulong>());
            Assert.That(world.Apply(message, out changed), Is.True);
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(world.Collision.Raycast(origin, Vector3.UnitX, 10f, ~0, out _), Is.True, "a new match repairs the building");

            Assert.That(world.Apply(new StructureStateMessage { StructureId = 9 }, out _), Is.False);
        }

        [Test]
        public void ThePredictionStopsAtAClosedDoorAndWalksThroughOnceTheServerSaysItIsBreached()
        {
            var world = DoorWorld();
            Assert.That(world.DoorCount, Is.EqualTo(2));
            Assert.That(world.StructureCount, Is.Zero, "door panels are not structure pieces");

            var blocked = WalkForward(world, 120);
            Assert.That(blocked.Z, Is.LessThan(_doorZ), "the closed panel of door 0 stops the predicted body");
            Assert.That(blocked.Z, Is.GreaterThan(_doorZ - 1f), "the body walked up to the panel");

            var message = new DoorStateMessage();
            message.Add(BreachDoorPhase.Opened, true);
            message.Add(BreachDoorPhase.Closed, false);
            Assert.That(world.Apply(message, out var changed), Is.True);
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(world.IsDoorOpen(0), Is.True);
            Assert.That(world.IsDoorOpen(1), Is.False);
            Assert.That(world.OpenDoorCount, Is.EqualTo(1));
            Assert.That(world.Collision.IsGroupEnabled(MapColliderGroups.Door(0)), Is.False);
            Assert.That(world.Collision.IsGroupEnabled(MapColliderGroups.Door(1)), Is.True);

            var through = WalkForward(world, 120);
            Assert.That(through.Z, Is.GreaterThan(_doorZ + 1f), "a kicked door no longer blocks the prediction");

            Assert.That(world.Apply(message, out changed), Is.True);
            Assert.That(changed, Is.Zero, "the same state changes nothing");
        }

        [TestCase(BreachDoorPhase.Closed, false)]
        [TestCase(BreachDoorPhase.Planting, false)]
        [TestCase(BreachDoorPhase.Armed, false)]
        [TestCase(BreachDoorPhase.Opened, true)]
        [TestCase(BreachDoorPhase.Destroyed, true)]
        public void OnlyABreachedDoorLeavesThePredictionsCollision(BreachDoorPhase phase, bool open)
        {
            var world = DoorWorld();
            var message = new DoorStateMessage();
            message.Add(phase, false);
            message.Add(BreachDoorPhase.Closed, false);

            Assert.That(world.Apply(message, out var changed), Is.True);

            Assert.That(world.IsDoorOpen(0), Is.EqualTo(open));
            Assert.That(changed, Is.EqualTo(open ? 1 : 0));
            Assert.That(world.Collision.IsGroupEnabled(MapColliderGroups.Door(0)), Is.EqualTo(!open));
        }

        [Test]
        public void ADoorTheStateLeavesOutIsClosedAndACountMismatchIsReported()
        {
            var world = DoorWorld();
            var both = new DoorStateMessage();
            both.Add(BreachDoorPhase.Destroyed, true);
            both.Add(BreachDoorPhase.Opened, false);
            Assert.That(world.Apply(both, out var changed), Is.True);
            Assert.That(changed, Is.EqualTo(2));

            var one = new DoorStateMessage();
            one.Add(BreachDoorPhase.Destroyed, true);
            Assert.That(world.Apply(one, out changed), Is.False, "the server knows fewer doors than the exported collision");
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(world.IsDoorOpen(0), Is.True);
            Assert.That(world.IsDoorOpen(1), Is.False, "a door the state does not list is treated as closed");

            var three = new DoorStateMessage();
            three.Add(BreachDoorPhase.Destroyed, true);
            three.Add(BreachDoorPhase.Closed, false);
            three.Add(BreachDoorPhase.Opened, false);
            Assert.That(world.Apply(three, out changed), Is.False, "the server knows more doors than the exported collision");
            Assert.That(changed, Is.Zero, "door 2 has no collision to remove");
            Assert.That(world.OpenDoorCount, Is.EqualTo(1));
        }

        [Test]
        public void AMapWithoutDoorsIgnoresTheDoorState()
        {
            var world = new ClientWorld(
                "Test",
                new WorldMap(new[]
                {
                    WorldCollider.Box(1, 0, WorldCollider.StaticGroup, new Vector3(0f, -0.5f, 0f), new Vector3(50f, 0.5f, 50f), Quaternion.Identity),
                }),
                NetMapLadders.None);

            Assert.That(world.DoorCount, Is.Zero);
            Assert.That(world.Apply(new DoorStateMessage(), out var changed), Is.True);
            Assert.That(changed, Is.Zero);
            Assert.That(world.LogLine, Does.Contain(" doors=0 "));
            Assert.Throws<ArgumentNullException>(() => world.Apply((DoorStateMessage)null, out _));
        }

        [Test]
        public void ThePingsTheServerSendsArePlacedMovedLostAndRemoved()
        {
            var log = new List<string>();
            var pings = new ClientPings(log.Add);

            Assert.That(pings.Handle(new PingPlacedMessage { PingId = 5, Kind = PingKind.Enemy, Position = Vector3.One, Tracking = true, RemainingSeconds = 4f }, 10f), Is.True);
            Assert.That(pings.Markers.TryGet(5, out var marker), Is.True);
            Assert.That(marker.IsTracking, Is.True);

            pings.Handle(new PingMovedMessage { PingId = 5, Position = new Vector3(2f, 0f, 0f) }, 10.5f);
            pings.Markers.TryGet(5, out marker);
            Assert.That(marker.Position, Is.EqualTo(new Vector3(2f, 0f, 0f)));

            pings.Handle(new PingLostMessage { PingId = 5, Position = new Vector3(3f, 0f, 0f) }, 11f);
            pings.Markers.TryGet(5, out marker);
            Assert.That(marker.IsTracking, Is.False);

            Assert.That(pings.Markers.Expire(13.9f), Is.Zero);
            Assert.That(pings.Handle(new PingRemovedMessage { PingId = 5, Reason = PingRemoval.Cleared }, 12f), Is.True);
            Assert.That(pings.Markers.Count, Is.Zero);
            Assert.That(pings.Handle(new KilledMessage(), 12f), Is.False);
            Assert.That(log, Has.Some.StartsWith(PingLog.Tag));
        }

        [Test]
        public void AShotIsSentOnlyWhenTheServerWouldTakeIt()
        {
            var standing = new CharacterMotorState(_spawn, Vector3.Zero, true, false);
            Assert.That(ClientFire.Decide(null, _playerId, standing), Is.EqualTo(FireDecision.Hold));

            var snapshot = Armed(ammo: 10);
            Assert.That(ClientFire.Decide(snapshot, _playerId, standing), Is.EqualTo(FireDecision.Fire));
            Assert.That(ClientFire.CurrentWeaponId(snapshot.Owner), Is.EqualTo("pistol"));

            Assert.That(ClientFire.Decide(Armed(ammo: 0), _playerId, standing), Is.EqualTo(FireDecision.Reload));
            Assert.That(ClientFire.Decide(Armed(ammo: 10, reloading: true), _playerId, standing), Is.EqualTo(FireDecision.Hold));
            Assert.That(ClientFire.Decide(Armed(ammo: 10, health: 0), _playerId, standing), Is.EqualTo(FireDecision.Hold));

            var climbing = new CharacterMotorState(
                _spawn, Vector3.Zero, false, PlayerStance.Standing, CharacterCoverState.Out, DodgeSnapshot.Ready, CharacterVaultState.None,
                CharacterProneState.None, Climbing());
            Assert.That(ClientFire.Decide(snapshot, _playerId, climbing), Is.EqualTo(FireDecision.Hold), "the ladder holsters the weapon");
        }

        private static CharacterLadderState Climbing()
        {
            var match = new ClientFeaturesTests();
            match.SetUp();
            try
            {
                var joined = match.Join(Rules(ladder: Ladder.On));
                for (var i = 0; i < 180 && !joined.LocalState.Ladder.IsClimbing; i++)
                {
                    joined.Step(new Vector2(0f, 1f), MathF.PI / 2f, InputButtons.None);
                }

                return joined.LocalState.Ladder;
            }
            finally
            {
                match.TearDown();
            }
        }

        // 床の上、+Z へ歩いた先の z = 3 に、横に回り込めず上にも登れない板のドア 0 を置く。ドア 1 は離れた所に置く
        private static ClientWorld DoorWorld()
        {
            return new ClientWorld(
                "Test",
                new WorldMap(new[]
                {
                    WorldCollider.Box(1, 0, WorldCollider.StaticGroup, new Vector3(0f, -0.5f, 0f), new Vector3(50f, 0.5f, 50f), Quaternion.Identity),
                    WorldCollider.Box(2, 0, MapColliderGroups.Door(0), new Vector3(0f, 2f, _doorZ), new Vector3(50f, 2f, 0.05f), Quaternion.Identity),
                    WorldCollider.Box(3, 0, MapColliderGroups.Door(1), new Vector3(0f, 2f, -20f), new Vector3(1f, 2f, 0.05f), Quaternion.Identity),
                }),
                NetMapLadders.None);
        }

        // 予測と同じシミュレーションで、立ったところから yaw 0 (+Z) へ歩かせた後の位置
        private static Vector3 WalkForward(ClientWorld world, int ticks)
        {
            var simulation = world.CreateSimulation(NetMotorSettings.Default, 60, ConnectAcceptedMessage.DefaultKillPlaneY);
            var state = new CharacterMotorState(_spawn, Vector3.Zero, false, false);
            var rules = Rules();
            for (var tick = 0; tick < ticks; tick++)
            {
                state = simulation.Step(state, SnapshotMotion.ToInput(new InputFrame(tick, new Vector2(0f, 1f), 0f, InputButtons.None), false, rules));
            }

            return state.Position;
        }

        private static SnapshotMessage Armed(int ammo, bool reloading = false, int health = 100)
        {
            var snapshot = new SnapshotMessage { Tick = 10, HasOwner = true };
            ref var row = ref snapshot.Players.Add();
            row.PlayerId = _playerId;
            row.Health = health;
            row.WeaponId = "pistol";
            snapshot.Owner.EquippedWeaponIds.Add("rifle");
            snapshot.Owner.EquippedWeaponIds.Add("pistol");
            snapshot.Owner.Weapon = new WeaponSync { AmmoInMagazine = ammo, WeaponCount = 2, EquippedIndex = 1, IsReloading = reloading };
            return snapshot;
        }

        private static MatchRulesSync Rules(Prone prone = Prone.Off, Ladder ladder = Ladder.Off)
        {
            return new MatchRulesSync { Prone = prone, Ladder = ladder };
        }

        private ClientMatch Join(MatchRulesSync rules)
        {
            Connect(NetMotorSettings.Default, ConnectAcceptedMessage.DefaultKillPlaneY);
            var world = new ClientWorld(
                "Test",
                new WorldMap(new[]
                {
                    WorldCollider.Box(1, 0, WorldCollider.StaticGroup, new Vector3(0f, -0.5f, 0f), new Vector3(50f, 0.5f, 50f), Quaternion.Identity),
                }),
                new NetMapLadders(new[] { new MapLadder(new LadderSpan(new Vector3(_ladderX, 0f, 0f), Vector3.UnitX, 3f, 0.8f), LadderSpec.Default) }));
            var simulation = world.CreateSimulation(_client.Motor, _client.TickRate, _client.KillPlaneY);
            Assert.That(simulation.Ladders, Has.Count.EqualTo(1));

            var snapshot = new SnapshotMessage { Tick = 1, HasOwner = true };
            var match = snapshot.Match;
            match.Rules = rules;
            snapshot.Match = match;
            ref var row = ref snapshot.Players.Add();
            row.PlayerId = _playerId;
            row.Position = _spawn;
            row.Health = 100;
            row.WeaponId = "";
            _transport.Deliver(snapshot);
            _client.Poll();
            Assert.That(_client.LatestSnapshot, Is.Not.Null);

            var clientMatch = new ClientMatch(_client, simulation);
            Assert.That(clientMatch.IsPredicting, Is.True);
            return clientMatch;
        }

        private void Connect(NetMotorSettings motor, float killPlaneY)
        {
            _client.Connect("127.0.0.1", 7777);
            _transport.Connected();
            _client.Poll();
            _transport.Deliver(new ConnectAcceptedMessage
            {
                PlayerId = _playerId,
                ServerVersion = "0.2.0",
                TickRate = 60,
                ServerTick = 0,
                Map = "NetArena01",
                MaxHealth = 100,
                Motor = motor,
                KillPlaneY = killPlaneY,
            });
            _client.Poll();
            Assert.That(_client.State, Is.EqualTo(MatchClientState.Connected));
        }
    }
}

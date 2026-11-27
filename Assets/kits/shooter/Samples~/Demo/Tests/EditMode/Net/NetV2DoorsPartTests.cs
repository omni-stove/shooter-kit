using System.Collections.Generic;
using GameKit.Net;
using GameKit.World;
using NUnit.Framework;
using Shooter.Core;
using Shooter.NetV2;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;
using Num = System.Numerics;

namespace Shooter.Net.Tests
{
    // サーバーが配るドアの状態で板を開ける・壊し、板の当たりを外すこと、押す・長押しを要求にして送ることを確かめる。
    // Update は Edit Mode では走らないので、Tick を直接呼んで進める。ドアは原点で +Z を向き、手元の目はドアの手前 (-Z) に置く
    public sealed class NetV2DoorsPartTests
    {
        private const string ChargeItemId = "0f8fad5b-d9cb-469f-a165-70867728950e";

        private readonly List<string> _logs = new List<string>();
        private readonly List<(int Door, BreachMethod Method, bool Cancel)> _sent = new List<(int, BreachMethod, bool)>();
        private readonly List<DoorBreach> _breaches = new List<DoorBreach>();
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private readonly List<BreachDoor> _sceneDoors = new List<BreachDoor>();
        private ClientWorld _world;
        private NetV2DoorsPart _part;
        private BreachDoor _door;
        private Ray? _aim;
        private bool _down;
        private bool _canAct;
        private int _charges;

        [SetUp]
        public void SetUp()
        {
            GameSignals.InteractHint.Clear();
            GameSignals.Breach.Clear();
            CombatEvents.DoorBreached += _breaches.Add;
            _logs.Clear();
            _sent.Clear();
            _breaches.Clear();
            _sceneDoors.Clear();
            _world = null;
            _aim = null;
            _down = false;
            _canAct = true;
            _charges = 0;
            _door = SpawnDoor("Door", Vector3.zero);
            Physics.SyncTransforms();
            _part = NewPart(BreachSpec.Default);
        }

        [TearDown]
        public void TearDown()
        {
            _part.Dispose();
            CombatEvents.DoorBreached -= _breaches.Add;
            foreach (var spawned in _spawned)
            {
                if (spawned != null)
                {
                    Object.DestroyImmediate(spawned);
                }
            }

            _spawned.Clear();
            GameSignals.InteractHint.Clear();
            GameSignals.Breach.Clear();
        }

        [Test]
        public void AKickedStateSwingsThePanelOpenAndLetsPeopleThroughWithoutRunningTheDoorsOwnState()
        {
            EnterMap();
            Assert.That(PanelBlocks(), Is.True, "the closed panel blocks the way");

            _part.Handle(State((BreachDoorPhase.Opened, true)));

            Assert.That(_part.PhaseOf(0), Is.EqualTo(BreachDoorPhase.Opened));
            Assert.That(_door.Phase, Is.EqualTo(BreachDoorPhase.Closed), "online the server owns the state; the scene door is only drawn");
            Assert.That(PanelBlocks(), Is.False, "the panel's colliders are off");
            Assert.That(_world.IsDoorOpen(0), Is.True, "the prediction's collision no longer has the panel");
            Assert.That(_door.Visuals.Panel.gameObject.activeSelf, Is.True);
            Assert.That(_door.Visuals.IsSwinging, Is.True, "a breach seen live swings the panel");

            _door.Visuals.Advance(1f);
            Assert.That(Quaternion.Angle(Quaternion.Euler(0f, -BreachDoorVisuals.DefaultSwingDegrees, 0f), _door.Visuals.Panel.localRotation), Is.LessThan(0.01f),
                "the far side is the front (+Z), so the panel swings toward +Z");

            Assert.That(_breaches, Has.Count.EqualTo(1));
            Assert.That(_breaches[0].Door, Is.SameAs(_door.gameObject));
            Assert.That(_breaches[0].Method, Is.EqualTo(BreachMethod.Kick));
            Assert.That(_breaches[0].Breacher, Is.Null, "the server does not say who breached it");
            Assert.That(_logs, Does.Contain("[Breach] net door=0 phase=Closed->Opened live=yes"));
            Assert.That(_logs, Does.Contain("[Breach] net collision changed=1 open=1"));
        }

        [Test]
        public void JoiningAfterTheDoorWasKickedShowsItAlreadyOpen()
        {
            _part.Handle(State((BreachDoorPhase.Opened, false)));
            Assert.That(_logs, Does.Contain("[Breach] net state doors=1 shown=later"));
            Assert.That(PanelBlocks(), Is.True, "nothing changes before the map is entered");

            EnterMap();

            Assert.That(_part.PhaseOf(0), Is.EqualTo(BreachDoorPhase.Opened));
            Assert.That(PanelBlocks(), Is.False);
            Assert.That(_world.IsDoorOpen(0), Is.True);
            Assert.That(_door.Visuals.IsSwinging, Is.False, "a late joiner sees the panel already swung");
            Assert.That(Quaternion.Angle(Quaternion.Euler(0f, BreachDoorVisuals.DefaultSwingDegrees, 0f), _door.Visuals.Panel.localRotation), Is.LessThan(0.01f),
                "the far side is the back (-Z), so the panel swings toward -Z");
            Assert.That(_breaches, Is.Empty, "a breach that happened before joining is not reported again");
            Assert.That(
                _logs.Exists(line => line.StartsWith("[Breach] net doors scene=") && line.EndsWith(" sceneDoors=1 collisionDoors=1 stateDoors=1")),
                Is.True,
                "entering logs the scene's doors, the collision's doors and the doors in the state received before");
            Assert.That(_logs, Does.Contain("[Breach] net door=0 phase=Closed->Opened live=no"));
        }

        [Test]
        public void JoiningAfterTheDoorWasBlownShowsNoPanelAndNoDebris()
        {
            _part.Handle(State((BreachDoorPhase.Destroyed, true)));

            EnterMap();

            Assert.That(_door.Visuals.Panel.gameObject.activeSelf, Is.False);
            Assert.That(_door.Visuals.Debris, Is.Null, "the debris of an old blast has long gone");
            Assert.That(_door.Visuals.Charge, Is.Null);
            Assert.That(_world.IsDoorOpen(0), Is.True);
            Assert.That(_breaches, Is.Empty);
        }

        [Test]
        public void APlantedChargeIsShownOnTheBreachersSideAndBlowsThePanelApart()
        {
            EnterMap();

            _part.Handle(State((BreachDoorPhase.Planting, true)));
            Assert.That(_door.Visuals.Charge, Is.Not.Null);
            Assert.That(_door.Visuals.Charge.transform.position.z, Is.LessThan(0f), "the far side is the front, so the charge sits on the back face");
            Assert.That(PanelBlocks(), Is.True, "a planted door still blocks");
            Assert.That(_world.IsDoorOpen(0), Is.False);

            _part.Handle(State((BreachDoorPhase.Armed, true)));
            Assert.That(_door.Visuals.Charge, Is.Not.Null);

            _part.Handle(State((BreachDoorPhase.Destroyed, true)));
            Assert.That(_door.Visuals.Charge, Is.Null);
            Assert.That(_door.Visuals.Panel.gameObject.activeSelf, Is.False);
            Assert.That(_door.Visuals.Debris, Is.Not.Null, "a blast seen live throws the debris");
            Assert.That(_world.IsDoorOpen(0), Is.True);
            Assert.That(_breaches, Has.Count.EqualTo(1));
            Assert.That(_breaches[0].Method, Is.EqualTo(BreachMethod.Charge));
        }

        [Test]
        public void ACancelledPlantTakesTheChargeOffAgain()
        {
            EnterMap();
            _part.Handle(State((BreachDoorPhase.Planting, false)));
            Assert.That(_door.Visuals.Charge, Is.Not.Null);
            Assert.That(_door.Visuals.Charge.transform.position.z, Is.GreaterThan(0f), "the far side is the back, so the charge sits on the front face");

            _part.Handle(State((BreachDoorPhase.Closed, false)));

            Assert.That(_door.Visuals.Charge, Is.Null);
            Assert.That(PanelBlocks(), Is.True);
        }

        [Test]
        public void ABreachedDoorIsNeverClosedAgain()
        {
            EnterMap();
            _part.Handle(State((BreachDoorPhase.Opened, true)));

            _part.Handle(State((BreachDoorPhase.Closed, true)));

            Assert.That(_part.PhaseOf(0), Is.EqualTo(BreachDoorPhase.Opened));
            Assert.That(PanelBlocks(), Is.False);
        }

        [Test]
        public void TappingInteractAtAClosedDoorSendsAKickAndShowsTheKickHint()
        {
            EnterMap();
            AimAtDoor();

            _part.Tick(0.016f);
            Assert.That(_part.Target, Is.EqualTo(0));
            var hint = GameSignals.InteractHint.Value;
            Assert.That(hint.HasValue, Is.True);
            Assert.That(hint.Value.Source, Is.SameAs(_part));
            Assert.That(hint.Value.Detail, Is.EqualTo(new BreachHint(true, true, string.Empty, InputActionPaths.Interact)));

            _down = true;
            _part.Tick(0.05f);
            Assert.That(_sent, Is.Empty, "a tap is sent when released, so a hold does not kick first");
            _down = false;
            _part.Tick(0.016f);

            Assert.That(_sent, Is.EqualTo(new[] { (0, BreachMethod.Kick, false) }));
            Assert.That(_logs, Does.Contain("[Breach] net request door=0 method=Kick sent=yes"));
        }

        [Test]
        public void HoldingInteractSendsAChargeAndPublishesTheProgressOfTheOwnCharge()
        {
            EnterMap();
            AimAtDoor();
            _part.Tick(0.016f);

            _down = true;
            _part.Tick(0.016f);
            _part.Tick(BreachSpec.DefaultHoldSeconds + 0.01f);

            Assert.That(_sent, Is.EqualTo(new[] { (0, BreachMethod.Charge, false) }));

            _part.Handle(State((BreachDoorPhase.Planting, true)));
            Assert.That(_part.OwnCharge, Is.EqualTo(0));
            _part.Tick(BreachSpec.DefaultPlantSeconds * 0.5f);

            var planting = GameSignals.Breach.Value;
            Assert.That(planting.HasValue, Is.True);
            Assert.That(planting.Value.Phase, Is.EqualTo(BreachDoorPhase.Planting));
            Assert.That(planting.Value.Progress, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(planting.Value.SecondsRemaining, Is.EqualTo(BreachSpec.DefaultPlantSeconds * 0.5f).Within(1e-4f));
            Assert.That(_part.Target, Is.EqualTo(-1), "no other door is offered while planting");

            _part.Handle(State((BreachDoorPhase.Armed, true)));
            _part.Tick(0f);
            var armed = GameSignals.Breach.Value;
            Assert.That(armed.HasValue, Is.True);
            Assert.That(armed.Value.Phase, Is.EqualTo(BreachDoorPhase.Armed));
            Assert.That(armed.Value.Progress, Is.Zero);
            Assert.That(armed.Value.SecondsRemaining, Is.EqualTo(BreachSpec.DefaultFuseSeconds).Within(1e-4f));

            _part.Handle(State((BreachDoorPhase.Destroyed, true)));
            _part.Tick(0.016f);
            Assert.That(_part.OwnCharge, Is.EqualTo(-1));
            Assert.That(GameSignals.Breach.HasValue, Is.False);
        }

        [Test]
        public void SomeoneElsesChargeIsDrawnButItsProgressIsNotPublished()
        {
            EnterMap();

            _part.Handle(State((BreachDoorPhase.Planting, true)));
            _part.Tick(0.5f);

            Assert.That(_part.OwnCharge, Is.EqualTo(-1));
            Assert.That(GameSignals.Breach.HasValue, Is.False);
            Assert.That(_door.Visuals.Charge, Is.Not.Null);
        }

        [Test]
        public void WithoutTheChargeItemTheHintSaysSoAndHoldingSendsNothing()
        {
            _part.Dispose();
            _part = NewPart(new BreachSpec(chargeItemId: ChargeItemId));
            EnterMap();
            AimAtDoor();

            _part.Tick(0.016f);
            var hint = (BreachHint)GameSignals.InteractHint.Value.Value.Detail;
            Assert.That(hint.LacksCharge, Is.True);

            _down = true;
            _part.Tick(0.016f);
            _part.Tick(BreachSpec.DefaultHoldSeconds + 0.01f);

            Assert.That(_sent, Is.Empty);
            Assert.That(_logs, Does.Contain("[Breach] door=door-0 refused by=- method=Charge phase=Closed reason=no-charge"));

            _charges = 1;
            _down = false;
            _part.Tick(0.016f);
            Assert.That(((BreachHint)GameSignals.InteractHint.Value.Value.Detail).CanPlant, Is.True);
        }

        [Test]
        public void ABreachedDoorOrOneOutOfReachIsNotOffered()
        {
            EnterMap();
            _aim = new Ray(new Vector3(0f, 1f, -(BreachSpec.DefaultReach + 0.5f)), Vector3.forward);
            _part.Tick(0.016f);
            Assert.That(_part.Target, Is.EqualTo(-1), "the door is beyond reach");
            Assert.That(GameSignals.InteractHint.HasValue, Is.False);

            AimAtDoor();
            _part.Handle(State((BreachDoorPhase.Opened, true)));
            _part.Tick(0.016f);
            Assert.That(_part.Target, Is.EqualTo(-1), "an open door cannot be kicked again");
        }

        [Test]
        public void NothingIsSentWhileUnableToAct()
        {
            EnterMap();
            AimAtDoor();
            _canAct = false;

            _part.Tick(0.016f);
            _down = true;
            _part.Tick(0.016f);
            _down = false;
            _part.Tick(0.016f);

            Assert.That(_part.Target, Is.EqualTo(-1));
            Assert.That(_sent, Is.Empty);
            Assert.That(GameSignals.InteractHint.HasValue, Is.False);
        }

        [Test]
        public void LeavingTheMapForgetsItsDoorStates()
        {
            EnterMap();
            _part.Handle(State((BreachDoorPhase.Opened, true)));

            _world = null;
            _part.Tick(0.016f);
            Assert.That(_part.IsInMap, Is.False);
            Assert.That(_part.PhaseOf(0), Is.EqualTo(BreachDoorPhase.Closed), "the next map numbers its doors from 0 again");

            _world = NewWorld();
            _part.Tick(0.016f);
            Assert.That(_world.IsDoorOpen(0), Is.False);
        }

        [Test]
        public void StateForADoorTheSceneDoesNotHaveIsLoggedOnce()
        {
            EnterMap();

            _part.Handle(State((BreachDoorPhase.Closed, false), (BreachDoorPhase.Opened, false)));
            _part.Handle(State((BreachDoorPhase.Closed, false), (BreachDoorPhase.Opened, false)));

            Assert.That(_logs.FindAll(line => line == "[Breach] net the scene has no door 1 (sceneDoors=1)"), Has.Count.EqualTo(1));
            Assert.That(_logs, Does.Contain("[Breach] net the server sent 2 doors but the map collision has 1; export the map again with make export-server-data"));
        }

        [Test]
        public void TheDoorsAreCountedLikeTheExportCountsThem()
        {
            var second = SpawnDoor("Second", new Vector3(5f, 0f, 0f));
            var disabled = SpawnDoor("Disabled", new Vector3(10f, 0f, 0f));
            disabled.enabled = false;
            var inactive = SpawnDoor("Inactive", new Vector3(15f, 0f, 0f));
            inactive.gameObject.SetActive(false);

            var doors = NetV2DoorsPart.DoorsIn(new[] { _door.gameObject, second.gameObject, disabled.gameObject, inactive.gameObject });

            Assert.That(doors, Is.EqualTo(new[] { _door, second }));
        }

        [Test]
        public void TheRegisteredFactoryFollowsTheContextsMapCollision()
        {
            var client = new MatchClient(new SilentTransport(), "0.2.0", RejoinKey.NewDeviceKey(), "tester", _logs.Add);
            ClientWorld world = null;
            var context = new NetV2ClientPartsContext(client, _ => true, _logs.Add) { World = () => world };
            var part = (NetV2DoorsPart)NetV2DoorsPart.Create(context);
            try
            {
                Assert.That(part.Spec, Is.SameAs(KitTuning.Breach));
                part.Handle(State((BreachDoorPhase.Opened, true)));
                Assert.That(part.IsInMap, Is.False);

                world = NewWorld();
                part.Tick(0f);

                Assert.That(part.IsInMap, Is.True);
                Assert.That(world.IsDoorOpen(0), Is.True, "the state that came before the map is applied on entering it");
            }
            finally
            {
                part.Dispose();
                client.Dispose();
            }
        }

        private NetV2DoorsPart NewPart(BreachSpec spec)
        {
            return new NetV2DoorsPart(
                Send,
                () => _canAct,
                () => _world,
                spec,
                () => _aim,
                interactDown: () => _down,
                chargeCount: id => id == ChargeItemId ? _charges : 0,
                sceneDoors: () => _sceneDoors,
                log: _logs.Add);
        }

        private bool Send(IMessage message)
        {
            var request = (BreachRequestMessage)message;
            _sent.Add((request.DoorIndex, request.Method, request.Cancel));
            return true;
        }

        private void EnterMap()
        {
            _world = NewWorld();
            _part.Tick(0f);
            Assert.That(_part.IsInMap, Is.True);
            Assert.That(_part.SceneDoorCount, Is.EqualTo(1));
        }

        private void AimAtDoor()
        {
            _aim = new Ray(new Vector3(0f, 1f, -1f), Vector3.forward);
        }

        private BreachDoor SpawnDoor(string name, Vector3 position)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            go.transform.position = position;
            var door = go.AddComponent<BreachDoor>();
            door.Id = "door-" + _sceneDoors.Count;
            door.Bind();
            _sceneDoors.Add(door);
            return door;
        }

        // 板の当たりがあれば、手前から奥への線が板で止まる
        private bool PanelBlocks()
        {
            Physics.SyncTransforms();
            return Physics.Raycast(new Vector3(0f, 1f, -1f), Vector3.forward, out var hit, 2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<BreachDoor>() == _door;
        }

        private static ClientWorld NewWorld()
        {
            return new ClientWorld(
                "Test",
                new WorldMap(new[]
                {
                    WorldCollider.Box(1, 0, WorldCollider.StaticGroup, new Num.Vector3(0f, -0.5f, 0f), new Num.Vector3(50f, 0.5f, 50f), Num.Quaternion.Identity),
                    WorldCollider.Box(2, 0, MapColliderGroups.Door(0), new Num.Vector3(0f, 1.05f, 0f), new Num.Vector3(0.5f, 1.05f, 0.04f), Num.Quaternion.Identity),
                }),
                NetMapLadders.None);
        }

        private sealed class SilentTransport : IMatchTransport
        {
            public bool IsActive => false;

            public void Listen(string address, ushort port)
            {
                throw new System.InvalidOperationException("a client transport does not listen");
            }

            public void Connect(string address, ushort port)
            {
            }

            public void Disconnect(ConnectionId connection, string reason)
            {
            }

            public void Send(ConnectionId connection, TransportChannel channel, System.ArraySegment<byte> payload)
            {
            }

            public int GetMaxPayloadSize(TransportChannel channel)
            {
                return MessageWriter.DefaultMaxLength;
            }

            public bool TryGetRoundTripTime(ConnectionId connection, out int milliseconds)
            {
                milliseconds = 0;
                return false;
            }

            public bool TryReceive(out TransportEvent transportEvent)
            {
                transportEvent = default;
                return false;
            }

            public void Shutdown(string reason)
            {
            }

            public void Dispose()
            {
            }
        }

        private static DoorStateMessage State(params (BreachDoorPhase Phase, bool FarSideFront)[] doors)
        {
            var message = new DoorStateMessage();
            foreach (var door in doors)
            {
                message.Add(door.Phase, door.FarSideFront);
            }

            return message;
        }
    }
}

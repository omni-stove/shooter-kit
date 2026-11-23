using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Update は Edit Mode では走らないので、Bind と Tick を直接呼んで進める。ドアの前 (+Z) の奥に far、手前に near を立てる。
    public sealed class BreachDoorTests
    {
        private const string TuningJson =
            "{\"plantSeconds\": 1, \"fuseSeconds\": 2, \"kickStaggerSeconds\": 1.5, \"chargeStaggerSeconds\": 3, \"chargeDamage\": 40, \"chargeRadius\": 2.5}";

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private readonly List<DoorBreach> _breaches = new List<DoorBreach>();
        private readonly List<SoundStimulus> _sounds = new List<SoundStimulus>();
        private Stagger _savedRule;
        private TextAsset _tuning;
        private BreachDoor _door;
        private GameObject _breacher;
        private StaggerReaction _far;
        private StaggerReaction _near;

        [SetUp]
        public void SetUp()
        {
            // 爆発のダメージで怯むと Impose で怯んだかが分からないので、撃たれて怯むルールを切る。
            _savedRule = StaggerReaction.ActiveRule;
            StaggerReaction.ActiveRule = Stagger.Off;
            CombatEvents.DoorBreached += OnBreached;
            SoundStimuli.Emitted += OnSound;

            _tuning = new TextAsset(TuningJson) { name = "breach-test" };
            _door = Spawn("Door").AddComponent<BreachDoor>();
            _door.Id = "door-1";
            _door.Tuning = _tuning;
            Assert.That(_door.Bind(), Is.True);

            _breacher = Spawn("Breacher");
            _breacher.transform.position = new Vector3(0f, 0f, -1f);
            _breacher.AddComponent<Damageable>().Initialize("breacher", 100);

            _far = Target("far", new Vector3(0f, 1f, 2f));
            _near = Target("near", new Vector3(0f, 1f, -2.5f));
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            CombatEvents.DoorBreached -= OnBreached;
            SoundStimuli.Emitted -= OnSound;
            StaggerReaction.ActiveRule = _savedRule;
            foreach (var spawned in _spawned)
            {
                if (spawned != null)
                {
                    Object.DestroyImmediate(spawned);
                }
            }

            _spawned.Clear();
            _breaches.Clear();
            _sounds.Clear();
            Object.DestroyImmediate(_tuning);
        }

        [Test]
        public void KickOpensThePanelAndLetsPeopleThrough()
        {
            Assert.That(_door.IsPassable, Is.False);
            Assert.That(BlockedThroughDoor(), Is.True);

            LogAssert.Expect(LogType.Log, "[Breach] door=door-1 kicked by=breacher phase=Opened");
            Assert.That(_door.Kick(_breacher), Is.True);

            Assert.That(_door.Phase, Is.EqualTo(BreachDoorPhase.Opened));
            Assert.That(_door.IsPassable, Is.True);
            Assert.That(BlockedThroughDoor(), Is.False);
            Assert.That(_door.Visuals.Panel.gameObject.activeSelf, Is.True);
            Assert.That(_door.Visuals.IsSwinging, Is.True);

            _door.Visuals.Advance(1f);
            Assert.That(_door.Visuals.IsSwinging, Is.False);
            Assert.That(Quaternion.Angle(Quaternion.identity, _door.Visuals.Panel.localRotation), Is.EqualTo(BreachDoorVisuals.DefaultSwingDegrees).Within(0.01f));
        }

        [Test]
        public void KickStaggersOnlyTheTargetsOnTheFarSideAndReportsTheBreach()
        {
            LogAssert.Expect(LogType.Log, "[Breach] door=door-1 kicked by=breacher phase=Opened");
            LogAssert.Expect(LogType.Log, "[Stagger] id=far imposed kind=Stumble seconds=1.50 source=breach");
            LogAssert.Expect(LogType.Log, "[Breach] door=door-1 staggered method=Kick kind=Stumble seconds=1.50 count=1");

            _door.Kick(_breacher);

            Assert.That(_far.Current, Is.EqualTo(StaggerKind.Stumble));
            Assert.That(_near.Current, Is.EqualTo(StaggerKind.None));

            Assert.That(_breaches.Count, Is.EqualTo(1));
            Assert.That(_breaches[0].Door, Is.SameAs(_door.gameObject));
            Assert.That(_breaches[0].Method, Is.EqualTo(BreachMethod.Kick));
            Assert.That(_breaches[0].Breacher, Is.SameAs(_breacher));

            Assert.That(_sounds.Count, Is.EqualTo(1));
            Assert.That(_sounds[0].Kind, Is.EqualTo(SoundCueKind.Clatter));
            Assert.That(_sounds[0].Radius, Is.EqualTo(25f));
        }

        [Test]
        public void TheFarSideIsTheSideTheBreacherIsNotOn()
        {
            _breacher.transform.position = new Vector3(0f, 0f, 1f);

            _door.Kick(_breacher);

            Assert.That(_near.Current, Is.EqualTo(StaggerKind.Stumble));
            Assert.That(_far.Current, Is.EqualTo(StaggerKind.None));
        }

        [Test]
        public void FarSignPointsAwayFromTheBreacherAndForwardWithoutOne()
        {
            var door = _door.transform;
            _breacher.transform.position = new Vector3(0f, 0f, -1f);
            Assert.That(BreachDoor.FarSignFrom(door, _breacher), Is.EqualTo(1f));
            _breacher.transform.position = new Vector3(0f, 0f, 1f);
            Assert.That(BreachDoor.FarSignFrom(door, _breacher), Is.EqualTo(-1f));
            Assert.That(BreachDoor.FarSignFrom(door, null), Is.EqualTo(1f));

            door.rotation = Quaternion.Euler(0f, 90f, 0f);
            _breacher.transform.position = new Vector3(1f, 0f, 0f);
            Assert.That(BreachDoor.FarSignFrom(door, _breacher), Is.EqualTo(-1f));
        }

        [Test]
        public void RoomBoxSitsBehindTheDoorWithTheSpecSize()
        {
            var spec = new BreachSpec(roomDepth: 6f, roomWidth: 4f, roomHeight: 3f);

            BreachDoor.RoomBox(_door.transform, 1f, spec, out var center, out var half);
            Assert.That(center, Is.EqualTo(new Vector3(0f, 1.5f, 3f)));
            Assert.That(half, Is.EqualTo(new Vector3(2f, 1.5f, 3f)));

            BreachDoor.RoomBox(_door.transform, -1f, spec, out center, out _);
            Assert.That(center, Is.EqualTo(new Vector3(0f, 1.5f, -3f)));
        }

        [Test]
        public void ChargeExplodesAfterThePlantAndFuseSeconds()
        {
            LogAssert.Expect(LogType.Log, "[Breach] door=door-1 plant-began by=breacher plantSeconds=1.00 fuseSeconds=2.00");
            Assert.That(_door.BeginCharge(_breacher), Is.True);
            Assert.That(_door.Phase, Is.EqualTo(BreachDoorPhase.Planting));
            Assert.That(_door.Visuals.Charge, Is.Not.Null);
            Assert.That(_door.Visuals.Charge.transform.position.z, Is.LessThan(0f));

            _door.Tick(0.5f);
            Assert.That(_door.Phase, Is.EqualTo(BreachDoorPhase.Planting));

            LogAssert.Expect(LogType.Log, "[Breach] door=door-1 armed by=breacher fuseSeconds=2.00");
            _door.Tick(0.5f);
            Assert.That(_door.Phase, Is.EqualTo(BreachDoorPhase.Armed));

            _door.Tick(1.9f);
            Assert.That(_door.Phase, Is.EqualTo(BreachDoorPhase.Armed));
            Assert.That(_door.IsPassable, Is.False);
            Assert.That(_far.Current, Is.EqualTo(StaggerKind.None));
            Assert.That(_breaches, Is.Empty);

            LogAssert.Expect(LogType.Log, "[Breach] door=door-1 detonated by=breacher damage=40 radius=2.50 phase=Destroyed");
            LogAssert.Expect(LogType.Log, "[Breach] door=door-1 staggered method=Charge kind=Blast seconds=3.00 count=1");
            _door.Tick(0.2f);

            Assert.That(_door.Phase, Is.EqualTo(BreachDoorPhase.Destroyed));
            Assert.That(_door.IsPassable, Is.True);
            Assert.That(BlockedThroughDoor(), Is.False);
            Assert.That(_door.Visuals.Panel.gameObject.activeSelf, Is.False);
            Assert.That(_door.Visuals.Charge, Is.Null);
            Assert.That(_door.Visuals.Debris, Is.Not.Null);
            Assert.That(_door.Visuals.Debris.GetComponentsInChildren<Collider>(), Is.Empty);

            // 板の当たりを先に外すので、板の向こうの相手にも爆発が届く。
            Assert.That(_far.GetComponent<Damageable>().Health.Current, Is.LessThan(100));
            Assert.That(_far.Current, Is.EqualTo(StaggerKind.Blast));
            Assert.That(_near.Current, Is.EqualTo(StaggerKind.None));

            Assert.That(_breaches.Count, Is.EqualTo(1));
            Assert.That(_breaches[0].Method, Is.EqualTo(BreachMethod.Charge));
            Assert.That(_breaches[0].Breacher, Is.SameAs(_breacher));
            Assert.That(_sounds.Count, Is.EqualTo(1));
            Assert.That(_sounds[0].Kind, Is.EqualTo(SoundCueKind.Explosion));
            Assert.That(_sounds[0].Radius, Is.EqualTo(60f));
        }

        [Test]
        public void ChargeWithZeroSecondsExplodesAtOnce()
        {
            var tuning = new TextAsset("{\"plantSeconds\": 0, \"fuseSeconds\": 0}");
            try
            {
                _door.Tuning = tuning;
                _door.Bind();

                Assert.That(_door.BeginCharge(_breacher), Is.True);

                Assert.That(_door.Phase, Is.EqualTo(BreachDoorPhase.Destroyed));
                Assert.That(_door.IsPassable, Is.True);
                Assert.That(_breaches.Count, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(tuning);
            }
        }

        [Test]
        public void ABreachedDoorCannotBeKickedOrChargedAgain()
        {
            _door.Kick(_breacher);

            LogAssert.Expect(LogType.Log, "[Breach] door=door-1 refused by=breacher method=Kick phase=Opened reason=breached");
            Assert.That(_door.Kick(_breacher), Is.False);
            LogAssert.Expect(LogType.Log, "[Breach] door=door-1 refused by=breacher method=Charge phase=Opened reason=breached");
            Assert.That(_door.BeginCharge(_breacher), Is.False);

            Assert.That(_door.Phase, Is.EqualTo(BreachDoorPhase.Opened));
            Assert.That(_door.Visuals.Charge, Is.Null);
            Assert.That(_breaches.Count, Is.EqualTo(1));
        }

        [Test]
        public void ADoorBeingChargedCannotBeKicked()
        {
            _door.BeginCharge(_breacher);

            LogAssert.Expect(LogType.Log, "[Breach] door=door-1 refused by=breacher method=Kick phase=Planting reason=charging");
            Assert.That(_door.Kick(_breacher), Is.False);
            Assert.That(_door.IsPassable, Is.False);
            Assert.That(_breaches, Is.Empty);
        }

        [Test]
        public void CancellingTheChargeStopsTheExplosion()
        {
            _door.BeginCharge(_breacher);
            _door.Tick(0.5f);

            LogAssert.Expect(LogType.Log, "[Breach] door=door-1 plant-cancelled by=breacher reason=cancelled");
            Assert.That(_door.CancelCharge(), Is.True);
            Assert.That(_door.Phase, Is.EqualTo(BreachDoorPhase.Closed));
            Assert.That(_door.Visuals.Charge, Is.Null);
            Assert.That(_door.Planter, Is.Null);

            _door.Tick(10f);

            Assert.That(_door.Phase, Is.EqualTo(BreachDoorPhase.Closed));
            Assert.That(_door.IsPassable, Is.False);
            Assert.That(BlockedThroughDoor(), Is.True);
            Assert.That(_far.Current, Is.EqualTo(StaggerKind.None));
            Assert.That(_far.GetComponent<Damageable>().Health.Current, Is.EqualTo(100));
            Assert.That(_breaches, Is.Empty);
            Assert.That(_door.CancelCharge(), Is.False);
        }

        [Test]
        public void AnArmedChargeCannotBeCancelled()
        {
            _door.BeginCharge(_breacher);
            _door.Tick(1f);

            Assert.That(_door.Phase, Is.EqualTo(BreachDoorPhase.Armed));
            Assert.That(_door.CancelCharge(), Is.False);

            _door.Tick(2f);
            Assert.That(_door.Phase, Is.EqualTo(BreachDoorPhase.Destroyed));
        }

        [Test]
        public void TheDoorReadsItsOwnTuningAndFallsBackToKitTuning()
        {
            Assert.That(_door.Spec.PlantSeconds, Is.EqualTo(1f));
            Assert.That(_door.Spec.FuseSeconds, Is.EqualTo(2f));

            var plain = Spawn("Plain").AddComponent<BreachDoor>();
            plain.Bind();
            Assert.That(plain.Spec, Is.SameAs(KitTuning.Breach));
        }

        [Test]
        public void ThePanelComesFromThePrefabWhenOneIsSet()
        {
            var prefab = Spawn("PanelPrefab");
            prefab.AddComponent<BoxCollider>();
            var door = Spawn("PrefabDoor");
            door.transform.position = new Vector3(20f, 0f, 0f);
            var visuals = door.AddComponent<BreachDoorVisuals>();
            visuals.PanelPrefab = prefab;
            var breach = door.AddComponent<BreachDoor>();

            breach.Bind();
            breach.Bind();

            Assert.That(visuals.Panel, Is.Not.Null);
            Assert.That(visuals.Panel.name, Is.EqualTo(BreachDoorVisuals.PanelObjectName));
            Assert.That(visuals.Panel.parent, Is.SameAs(door.transform));
            Assert.That(door.GetComponentsInChildren<BoxCollider>().Length, Is.EqualTo(1));
            Assert.That(breach.Visuals, Is.SameAs(visuals));
        }

        [Test]
        public void WithoutAPrefabThePanelIsOneBoxStandingOnTheDoorBase()
        {
            var panel = _door.Visuals.Panel;

            Assert.That(panel.name, Is.EqualTo(BreachDoorVisuals.PanelObjectName));
            Assert.That(panel.GetComponentsInChildren<Collider>().Length, Is.EqualTo(1));
            var center = _door.Visuals.PanelCenter();
            Assert.That(Vector3.Distance(center, new Vector3(0f, 1.05f, 0f)), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(_door.Visuals.ChargePoint(-1f), new Vector3(0f, 1.05f, -0.1f)), Is.LessThan(1e-4f));
        }

        private bool BlockedThroughDoor()
        {
            return Physics.Raycast(new Vector3(0f, 1f, -1f), Vector3.forward, 2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        private StaggerReaction Target(string id, Vector3 position)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = id;
            _spawned.Add(body);
            body.transform.position = position;
            body.AddComponent<Damageable>().Initialize(id, 100);
            var reaction = body.AddComponent<StaggerReaction>();
            Assert.That(reaction.Bind(), Is.True);
            return reaction;
        }

        private GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        private void OnBreached(DoorBreach breach)
        {
            _breaches.Add(breach);
        }

        private void OnSound(GameObject source, SoundStimulus sound)
        {
            _sounds.Add(sound);
        }
    }
}

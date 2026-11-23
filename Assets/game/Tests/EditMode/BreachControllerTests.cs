using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Update は Edit Mode では走らないので、ドアの Tick と BreachController.Tick を直接呼んで進める。
    // ドアは原点で +Z を向き、プレイヤーはその手前 (z = -1) の目の高さから +Z を向いて立つ。
    public sealed class BreachControllerTests
    {
        private const string ChargeItemId = "7c0f3c55-1b8e-4f0a-9d2a-6a1f0b6c2e11";

        // 爆発に仕掛けた本人が巻き込まれると死んで取りやめと区別できないので、爆発の半径は 0 にする。
        private const string TuningJson =
            "{\"kickSeconds\": 0.5, \"plantSeconds\": 1, \"fuseSeconds\": 1, \"holdSeconds\": 0.3, \"chargeRadius\": 0,"
            + " \"slowMotionScale\": 0.5, \"slowMotionSeconds\": 1.5}";

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private readonly List<Object> _assets = new List<Object>();
        private BreachDoor _door;
        private GameObject _player;
        private Damageable _self;
        private WeaponController _weapon;
        private BreachController _controller;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            KeySharing.Reset();
            TimeScaleArbiter.ResetState();
            TimeScaleArbiter.NetworkSessionSource = null;

            _door = Spawn("Door").AddComponent<BreachDoor>();
            _door.Id = "door-1";
            _door.Tuning = Tuning(TuningJson);
            Assert.That(_door.Bind(), Is.True);

            _player = Spawn("Player");
            _player.transform.position = new Vector3(0f, 1f, -1f);
            _self = _player.AddComponent<Damageable>();
            _self.Initialize("player", 100);
            var camera = new GameObject("Camera", typeof(Camera));
            camera.transform.SetParent(_player.transform, false);
            _weapon = camera.AddComponent<WeaponController>();
            _controller = _player.AddComponent<BreachController>();
            _controller.Configure(SlowMotion.Off);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            if (_controller != null)
            {
                _controller.Shutdown();
            }

            foreach (var spawned in _spawned)
            {
                if (spawned != null)
                {
                    Object.DestroyImmediate(spawned);
                }
            }

            foreach (var asset in _assets)
            {
                if (asset != null)
                {
                    Object.DestroyImmediate(asset);
                }
            }

            _spawned.Clear();
            _assets.Clear();
            TimeScaleArbiter.ResetState();
            GameSignals.ResetAll();
            KeySharing.Reset();
        }

        [Test]
        public void TheDoorInFrontShowsTheBreachHint()
        {
            _controller.Tick(false, 0f);

            Assert.That(_controller.Target, Is.SameAs(_door));
            Assert.That(GameSignals.InteractHint.Value?.Source, Is.SameAs(_controller));
            Assert.That(GameSignals.InteractHint.Value?.Detail, Is.EqualTo(new BreachHint(true, true, string.Empty, "Player/Interact")));
            Assert.That(KeySharing.IsClaimed(InputActionPaths.Interact), Is.True);
        }

        [Test]
        public void ADoorOutOfReachOrBehindShowsNothing()
        {
            _player.transform.position = new Vector3(0f, 1f, -3f);
            _controller.Tick(false, 0f);

            Assert.That(_controller.Target, Is.Null);
            Assert.That(GameSignals.InteractHint.HasValue, Is.False);

            _player.transform.position = new Vector3(0f, 1f, -1f);
            _player.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            _controller.Tick(false, 0f);

            Assert.That(_controller.Target, Is.Null, "the door is behind the player");
        }

        [Test]
        public void PressingAndReleasingKicksAfterTheKickSeconds()
        {
            _controller.Tick(true, 0.1f);
            LogAssert.Expect(LogType.Log, "[Breach] door=door-1 kick-began by=player kickSeconds=0.50");
            _controller.Tick(false, 0.1f);

            Assert.That(_controller.IsKicking, Is.True);
            Assert.That(_door.Phase, Is.EqualTo(BreachDoorPhase.Closed), "the foot has not reached the panel yet");
            Assert.That(_weapon.IsHolstered, Is.False, "kicking keeps the weapon up");

            _controller.Tick(false, 0.3f);
            Assert.That(_door.Phase, Is.EqualTo(BreachDoorPhase.Closed));

            LogAssert.Expect(LogType.Log, "[Breach] door=door-1 kicked by=player phase=Opened");
            _controller.Tick(false, 0.25f);

            Assert.That(_controller.IsKicking, Is.False);
            Assert.That(_door.Phase, Is.EqualTo(BreachDoorPhase.Opened));
            Assert.That(GameSignals.InteractHint.HasValue, Is.False, "a breached door offers nothing");
            Assert.That(GameSignals.Breach.HasValue, Is.False, "a kick has no progress to show");
        }

        [Test]
        public void HoldingPlantsTheChargeWithTheWeaponDown()
        {
            _controller.Tick(true, 0.1f);
            _controller.Tick(true, 0.2f);
            Assert.That(_controller.IsPlanting, Is.False, "0.2 s is shorter than holdSeconds");

            LogAssert.Expect(LogType.Log, "[Breach] door=door-1 plant-began by=player plantSeconds=1.00 fuseSeconds=1.00");
            _controller.Tick(true, 0.15f);

            Assert.That(_controller.IsPlanting, Is.True);
            Assert.That(_controller.PlantingDoor, Is.SameAs(_door));
            Assert.That(_door.Phase, Is.EqualTo(BreachDoorPhase.Planting));
            Assert.That(_weapon.IsHolstered, Is.True);
            Assert.That(_weapon.Holster.IsHeldBy(_controller), Is.True);
            Assert.That(GameSignals.Breach.Value?.Phase, Is.EqualTo(BreachDoorPhase.Planting));

            _controller.Tick(false, 0f);
            Assert.That(_controller.IsKicking, Is.False, "letting go after the hold does not kick");

            _door.Tick(0.5f);
            _controller.Tick(false, 0f);
            Assert.That(GameSignals.Breach.Value?.Progress, Is.EqualTo(0.5f).Within(1e-4f));

            _door.Tick(0.5f);
            _controller.Tick(false, 0f);

            Assert.That(_door.Phase, Is.EqualTo(BreachDoorPhase.Armed));
            Assert.That(_controller.IsPlanting, Is.False);
            Assert.That(_controller.ArmedDoor, Is.SameAs(_door));
            Assert.That(_weapon.IsHolstered, Is.False, "the weapon comes back up once the charge is set");
            Assert.That(GameSignals.Breach.Value?.Phase, Is.EqualTo(BreachDoorPhase.Armed));

            _door.Tick(1f);
            _controller.Tick(false, 0f);

            Assert.That(_door.Phase, Is.EqualTo(BreachDoorPhase.Destroyed));
            Assert.That(_controller.ArmedDoor, Is.Null);
            Assert.That(GameSignals.Breach.HasValue, Is.False);
        }

        [Test]
        public void WithoutTheChargeItemThePlantIsRefused()
        {
            _door.Tuning = Tuning("{\"plantSeconds\": 1, \"holdSeconds\": 0.3, \"chargeItemId\": \"" + ChargeItemId + "\"}");
            _door.Bind();
            var inventory = GiveInventory(0);

            _controller.Tick(false, 0f);
            var hint = (BreachHint)GameSignals.InteractHint.Value.Value.Detail;
            Assert.That(hint.CanKick, Is.True);
            Assert.That(hint.CanPlant, Is.False);
            Assert.That(hint.LacksCharge, Is.True);
            Assert.That(hint.ChargeItemId, Is.EqualTo(ChargeItemId));

            _controller.Tick(true, 0.1f);
            LogAssert.Expect(LogType.Log, "[Breach] door=door-1 refused by=player method=Charge phase=Closed reason=no-charge");
            _controller.Tick(true, 0.4f);

            Assert.That(_controller.IsPlanting, Is.False);
            Assert.That(_door.Phase, Is.EqualTo(BreachDoorPhase.Closed));
            Assert.That(_weapon.IsHolstered, Is.False);

            _controller.Tick(false, 0f);
            Assert.That(_controller.IsKicking, Is.False, "letting go after a refused hold does not kick");

            inventory.Inventory.Add(ChargeItemId, 1);
            _controller.Tick(false, 0f);
            Assert.That(((BreachHint)GameSignals.InteractHint.Value.Value.Detail).CanPlant, Is.True);

            _controller.Tick(true, 0.1f);
            LogAssert.Expect(LogType.Log, "[Breach] door=door-1 charge-spent by=player item=" + ChargeItemId + " left=0");
            _controller.Tick(true, 0.4f);

            Assert.That(_controller.IsPlanting, Is.True);
            Assert.That(inventory.Inventory.CountOf(ChargeItemId), Is.Zero);
        }

        [Test]
        public void WalkingAwayCancelsThePlantAndGivesTheItemBack()
        {
            _door.Tuning = Tuning("{\"plantSeconds\": 1, \"holdSeconds\": 0.3, \"chargeItemId\": \"" + ChargeItemId + "\"}");
            _door.Bind();
            var inventory = GiveInventory(1);
            Plant();
            Assert.That(inventory.Inventory.CountOf(ChargeItemId), Is.Zero);

            _player.transform.position = new Vector3(0f, 1f, -1.4f);
            _controller.Tick(false, 0.1f);
            Assert.That(_controller.IsPlanting, Is.True, "still within reach (1.5)");

            _player.transform.position = new Vector3(0f, 1f, -3f);
            LogAssert.Expect(LogType.Log, "[Breach] door=door-1 plant-cancelled by=player reason=left");
            LogAssert.Expect(LogType.Log, "[Breach] door=door-1 charge-returned by=player item=" + ChargeItemId + " returned=yes");
            _controller.Tick(false, 0.1f);

            Assert.That(_controller.IsPlanting, Is.False);
            Assert.That(_door.Phase, Is.EqualTo(BreachDoorPhase.Closed));
            Assert.That(_weapon.IsHolstered, Is.False);
            Assert.That(GameSignals.Breach.HasValue, Is.False);
            Assert.That(inventory.Inventory.CountOf(ChargeItemId), Is.EqualTo(1));

            _door.Tick(10f);
            Assert.That(_door.Phase, Is.EqualTo(BreachDoorPhase.Closed), "the cancelled charge never goes off");
        }

        [Test]
        public void DyingCancelsThePlant()
        {
            Plant();

            _self.Kill();
            LogAssert.Expect(LogType.Log, "[Breach] door=door-1 plant-cancelled by=player reason=dead");
            _controller.Tick(false, 0.1f);

            Assert.That(_controller.IsPlanting, Is.False);
            Assert.That(_door.Phase, Is.EqualTo(BreachDoorPhase.Closed));
            Assert.That(_weapon.IsHolstered, Is.False);
            Assert.That(GameSignals.InteractHint.HasValue, Is.False, "the dead are offered nothing");
        }

        [Test]
        public void ShuttingDownCancelsThePlantAndReleasesTheWeapon()
        {
            Plant();

            LogAssert.Expect(LogType.Log, "[Breach] door=door-1 plant-cancelled by=player reason=disabled");
            _controller.Shutdown();

            Assert.That(_door.Phase, Is.EqualTo(BreachDoorPhase.Closed));
            Assert.That(_weapon.IsHolstered, Is.False);
            Assert.That(GameSignals.Breach.HasValue, Is.False);
            Assert.That(GameSignals.InteractHint.HasValue, Is.False);
        }

        [Test]
        public void APressWhileAnotherHintIsShownIsLeftToThatHint()
        {
            _controller.Tick(false, 0f);
            var other = new InteractHintSlot(new object());
            other.Show(new AmmoCrateHint(true, "Player/Interact"));

            _controller.Tick(true, 0.1f);
            _controller.Tick(false, 0.1f);
            _controller.Tick(false, 1f);

            Assert.That(_controller.IsKicking, Is.False);
            Assert.That(_door.Phase, Is.EqualTo(BreachDoorPhase.Closed));
            other.Clear();
        }

        [Test]
        public void BreakingInWithSlowMotionOnSlowsTimeDown()
        {
            TimeScaleArbiter.ApplyRules(SlowMotion.On, _player);
            _controller.SlowMotion = SlowMotion.On;

            Kick();

            Assert.That(TimeScaleArbiter.Requests.Count, Is.EqualTo(1));
            Assert.That(TimeScaleArbiter.Requests[0].Reason, Is.EqualTo(BreachController.TimeScaleReason));
            Assert.That(TimeScaleArbiter.Requests[0].Scale, Is.EqualTo(0.5f));
            Assert.That(TimeScaleArbiter.Requests[0].RemainingSeconds, Is.EqualTo(1.5f));
        }

        [Test]
        public void TheChargeGoingOffAlsoSlowsTimeDown()
        {
            TimeScaleArbiter.ApplyRules(SlowMotion.On, _player);
            _controller.SlowMotion = SlowMotion.On;
            Plant();

            _door.Tick(1f);
            _controller.Tick(false, 0f);
            Assert.That(TimeScaleArbiter.Requests, Is.Empty, "armed is not yet breached");

            _door.Tick(1f);

            Assert.That(TimeScaleArbiter.Requests.Count, Is.EqualTo(1));
            Assert.That(TimeScaleArbiter.Requests[0].Reason, Is.EqualTo("breach"));
        }

        [Test]
        public void WithSlowMotionOffTimeKeepsItsPace()
        {
            TimeScaleArbiter.ApplyRules(SlowMotion.On, _player);
            _controller.SlowMotion = SlowMotion.Off;

            Kick();

            Assert.That(_door.Phase, Is.EqualTo(BreachDoorPhase.Opened));
            Assert.That(TimeScaleArbiter.Requests, Is.Empty);
        }

        [Test]
        public void ZeroSlowMotionSecondsKeepsTheTimeEvenWithTheRuleOn()
        {
            _door.Tuning = Tuning("{\"kickSeconds\": 0, \"slowMotionSeconds\": 0}");
            _door.Bind();
            TimeScaleArbiter.ApplyRules(SlowMotion.On, _player);
            _controller.SlowMotion = SlowMotion.On;

            _controller.Tick(true, 0.1f);
            _controller.Tick(false, 0.1f);

            Assert.That(_door.Phase, Is.EqualTo(BreachDoorPhase.Opened), "0 kickSeconds kicks on release");
            Assert.That(TimeScaleArbiter.Requests, Is.Empty);
        }

        [Test]
        public void SomeoneElseBreakingInDoesNotSlowThisPlayersTime()
        {
            TimeScaleArbiter.ApplyRules(SlowMotion.On, _player);
            _controller.SlowMotion = SlowMotion.On;

            _door.Kick(Spawn("Stranger"));

            Assert.That(TimeScaleArbiter.Requests, Is.Empty);
        }

        [Test]
        public void TheInstallerSkipsBodiesThatCannotMove()
        {
            var target = Spawn("Target");
            target.AddComponent<Damageable>().Initialize("target", 100);

            new BreachActionInstaller().Install(target, GameRules.Default);

            Assert.That(target.GetComponent<BreachController>(), Is.Null);
            Assert.That(new BreachActionInstaller().Order, Is.EqualTo(BreachActionInstaller.InstallOrder));
        }

        private void Kick()
        {
            _controller.Tick(true, 0.1f);
            _controller.Tick(false, 0.1f);
            _controller.Tick(false, 0.5f);
            Assert.That(_door.Phase, Is.EqualTo(BreachDoorPhase.Opened));
        }

        private void Plant()
        {
            _controller.Tick(true, 0.1f);
            _controller.Tick(true, 0.4f);
            Assert.That(_controller.IsPlanting, Is.True);
            _controller.Tick(false, 0f);
        }

        private PlayerInventory GiveInventory(int charges)
        {
            var inventory = _player.AddComponent<PlayerInventory>();
            inventory.Initialize(new ItemCatalog(new[] { new ItemSpec(ChargeItemId, maxStack: 5) }, InventoryRule.Default));
            if (charges > 0)
            {
                Assert.That(inventory.Inventory.Add(ChargeItemId, charges), Is.EqualTo(charges));
            }

            _controller.Configure(SlowMotion.Off);
            return inventory;
        }

        private TextAsset Tuning(string json)
        {
            var asset = new TextAsset(json) { name = "breach-test" };
            _assets.Add(asset);
            return asset;
        }

        private GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }
    }
}

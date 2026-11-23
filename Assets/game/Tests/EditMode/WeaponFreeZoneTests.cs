using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Edit Mode では Update とトリガーの通知が走らないので、出入りは HandleEnter / HandleExit で、時間は Tick で渡す。
    // 秒数は 2 の累乗の分数にして、丸めで敵対する tick がずれないようにする。
    public sealed class WeaponFreeZoneTests
    {
        private const string ZoneId = "Gate";
        private const float RifleDrawSeconds = 0.25f;
        private const float RifleHolsterSeconds = 0.5f;
        private const float HostileAfterSeconds = 2f;

        private readonly List<Object> _created = new List<Object>();

        private GameObject _body;
        private Collider _bodyCollider;
        private Damageable _damageable;
        private WeaponController _weapon;
        private HolsterController _holster;
        private InputAction _attack;
        private InputAction _reload;
        private WeaponFreeZone _zone;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            CombatEvents.ClearSubscribers();

            _body = new GameObject("Body") { tag = GameBootstrap.PlayerTag };
            _bodyCollider = _body.AddComponent<CapsuleCollider>();
            _damageable = _body.AddComponent<Damageable>();
            _damageable.Initialize("Player", Damageable.DefaultMaxHealth);
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.SetParent(_body.transform, false);

            var rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            rifle.name = "Rifle";
            WeaponAssets.Configure(rifle, WithTimes(TestWeapons.RifleSpec(), RifleDrawSeconds, RifleHolsterSeconds));
            _created.Add(rifle);
            _attack = new InputAction("Attack", InputActionType.Button);
            _reload = new InputAction("Reload", InputActionType.Button);
            _weapon = camera.gameObject.AddComponent<WeaponController>();
            _weapon.Initialize(camera.transform, _attack, _reload, null, null, new[] { rifle });

            _holster = _body.AddComponent<HolsterController>();
            _holster.Spec = HolsterSpec.Default;
            _holster.Initialize(_weapon, _damageable);

            var zoneObject = new GameObject("Zone");
            _created.Add(zoneObject);
            _zone = zoneObject.AddComponent<WeaponFreeZone>();
            _zone.ZoneId = ZoneId;
            _zone.Build();
        }

        [TearDown]
        public void TearDown()
        {
            _attack.Dispose();
            _reload.Dispose();
            Object.DestroyImmediate(_body);
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
            CombatEvents.ClearSubscribers();
            GameSignals.ResetAll();
        }

        [Test]
        public void BuildingMakesATriggerBoxAndAFloorMarkWithoutACollider()
        {
            var trigger = _zone.GetComponent<BoxCollider>();
            Assert.That(trigger, Is.Not.Null);
            Assert.That(trigger.isTrigger, Is.True);
            Assert.That(trigger.size, Is.EqualTo(WeaponFreeZone.DefaultSize));

            var visual = _zone.transform.Find(WeaponFreeZone.VisualName);
            Assert.That(visual, Is.Not.Null);
            Assert.That(visual.GetComponent<Collider>(), Is.Null, "the mark must not block the trigger or movement");
            Assert.That(visual.localScale.x, Is.EqualTo(WeaponFreeZone.DefaultSize.x));
            Assert.That(visual.localScale.z, Is.EqualTo(WeaponFreeZone.DefaultSize.z));

            _zone.Build();
            Assert.That(_zone.GetComponents<BoxCollider>().Length, Is.EqualTo(1), "building again does not add another trigger");
            Assert.That(CountChildren(_zone.transform, WeaponFreeZone.VisualName), Is.EqualTo(1), "building again does not add another mark");
        }

        [Test]
        public void AnExistingColliderIsTurnedIntoTheTrigger()
        {
            var host = new GameObject("Sphere zone");
            _created.Add(host);
            var sphere = host.AddComponent<SphereCollider>();
            var zone = host.AddComponent<WeaponFreeZone>();

            Assert.That(zone.EnsureTrigger(), Is.SameAs(sphere));
            Assert.That(sphere.isTrigger, Is.True);
            Assert.That(host.GetComponent<BoxCollider>(), Is.Null);
        }

        [Test]
        public void TheVisualPrefabReplacesTheBuiltMarkAndLosesItsColliders()
        {
            var look = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            look.name = "Gate look";
            _created.Add(look);
            var host = new GameObject("Prefab zone");
            _created.Add(host);
            var zone = host.AddComponent<WeaponFreeZone>();
            zone.VisualPrefab = look;

            zone.Build();

            var visual = host.transform.Find(WeaponFreeZone.VisualName);
            Assert.That(visual, Is.Not.Null);
            Assert.That(visual.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(look.GetComponent<MeshFilter>().sharedMesh));
            Assert.That(visual.GetComponentInChildren<Collider>(), Is.Null);
        }

        [Test]
        public void ConfiguringRejectsNullAndLogsTheSpec()
        {
            Assert.Throws<ArgumentNullException>(() => _zone.Configure(null));

            LogAssert.Expect(LogType.Log, "[WeaponFreeZone] zone=Gate reaction=BlockDraw hostileAfter=1.50");
            _zone.Configure(new WeaponFreeZoneSpec(WeaponFreeZoneReaction.BlockDraw, 1.5f));

            Assert.That(_zone.Spec.Reaction, Is.EqualTo(WeaponFreeZoneReaction.BlockDraw));
            Assert.That(_zone.State.Spec, Is.SameAs(_zone.Spec));
        }

        [Test]
        public void TheDefaultSpecComesFromTheSpecDefaults()
        {
            Assert.That(_zone.Spec.Reaction, Is.EqualTo(WeaponFreeZoneSpec.DefaultReaction));
            Assert.That(_zone.Spec.HostileAfterSeconds, Is.EqualTo(WeaponFreeZoneSpec.DefaultHostileAfterSeconds));
        }

        [Test]
        public void CollidersWithoutThePlayerTagAreIgnored()
        {
            var stranger = new GameObject("Stranger").AddComponent<BoxCollider>();
            _created.Add(stranger.gameObject);

            Assert.That(_zone.HandleEnter(stranger), Is.False);
            Assert.That(_zone.HandleEnter(null), Is.False);
            _zone.Tick(0f);

            Assert.That(_zone.IsPlayerInside, Is.False);
            Assert.That(GameSignals.WeaponFreeZone.Value, Is.Null);
        }

        [Test]
        public void StayingArmedInAWarnThenHostileZoneWarnsCountsDownAndTurnsHostile()
        {
            _zone.Configure(new WeaponFreeZoneSpec(WeaponFreeZoneReaction.WarnThenHostile, HostileAfterSeconds));

            LogAssert.Expect(LogType.Log, "[WeaponFreeZone] zone=Gate holder=Player entered armed=true");
            LogAssert.Expect(LogType.Log, "[WeaponFreeZone] zone=Gate warning reaction=WarnThenHostile secondsToHostile=2.00");
            Assert.That(_zone.HandleEnter(_bodyCollider), Is.True);
            _zone.Tick(0f);

            Assert.That(_zone.IsPlayerInside, Is.True);
            Assert.That(_zone.IsWarning, Is.True);
            Assert.That(GameSignals.WeaponFreeZone.Value, Is.EqualTo(new WeaponFreeZoneStatus(true, WeaponFreeZoneReaction.WarnThenHostile, true, 2f)));

            _zone.Tick(1.5f);
            Assert.That(GameSignals.WeaponFreeZone.Value, Is.EqualTo(new WeaponFreeZoneStatus(true, WeaponFreeZoneReaction.WarnThenHostile, true, 0.5f)));
            Assert.That(_zone.IsHostile, Is.False);

            LogAssert.Expect(LogType.Log, "[WeaponFreeZone] zone=Gate hostile");
            _zone.Tick(0.5f);
            Assert.That(_zone.IsHostile, Is.True);
            Assert.That(_zone.IsWarning, Is.False);
            Assert.That(GameSignals.WeaponFreeZone.Value, Is.EqualTo(new WeaponFreeZoneStatus(true, WeaponFreeZoneReaction.WarnThenHostile, false, 0f)));
        }

        [Test]
        public void HolsteringInTimeClearsTheWarningAndNeverTurnsHostile()
        {
            _zone.Configure(new WeaponFreeZoneSpec(WeaponFreeZoneReaction.WarnThenHostile, HostileAfterSeconds));
            _zone.HandleEnter(_bodyCollider);
            _zone.Tick(0f);
            _zone.Tick(1f);

            Assert.That(_holster.Holster(), Is.True);
            Assert.That(_holster.Status.IsArmed, Is.False, "holstering already counts as unarmed");
            LogAssert.Expect(LogType.Log, "[WeaponFreeZone] zone=Gate warning cleared reason=unarmed");
            _zone.Tick(0.5f);

            Assert.That(_zone.IsWarning, Is.False);
            Assert.That(GameSignals.WeaponFreeZone.Value, Is.EqualTo(new WeaponFreeZoneStatus(true, WeaponFreeZoneReaction.WarnThenHostile, false, 0f)));

            _zone.Tick(HostileAfterSeconds * 4f);
            Assert.That(_zone.IsHostile, Is.False);
        }

        [Test]
        public void LeavingClearsTheWarningAndTheSignal()
        {
            _zone.Configure(new WeaponFreeZoneSpec(WeaponFreeZoneReaction.WarnThenHostile, HostileAfterSeconds));
            _zone.HandleEnter(_bodyCollider);
            _zone.Tick(0f);

            Assert.That(_zone.HandleExit(_bodyCollider), Is.True);
            LogAssert.Expect(LogType.Log, "[WeaponFreeZone] zone=Gate holder=Player exited");
            LogAssert.Expect(LogType.Log, "[WeaponFreeZone] zone=Gate warning cleared reason=exited");
            _zone.Tick(0.5f);

            Assert.That(_zone.IsPlayerInside, Is.False);
            Assert.That(_zone.IsWarning, Is.False);
            Assert.That(_zone.Status, Is.EqualTo(WeaponFreeZoneStatus.Outside));
            Assert.That(GameSignals.WeaponFreeZone.Value, Is.Null);
            Assert.That(_zone.Holster, Is.Null, "the player is forgotten once it is out");
        }

        [Test]
        public void LeavingDoesNotClearAnotherZonesSignal()
        {
            _zone.HandleEnter(_bodyCollider);
            _zone.Tick(0f);
            var other = new WeaponFreeZoneStatus(true, WeaponFreeZoneReaction.BlockDraw, false, 0f);
            GameSignals.WeaponFreeZone.Publish(other);

            _zone.HandleExit(_bodyCollider);
            _zone.Tick(0f);

            Assert.That(GameSignals.WeaponFreeZone.Value, Is.EqualTo(other));
        }

        [Test]
        public void WarnOnlyKeepsWarningButNeverTurnsHostile()
        {
            _zone.Configure(new WeaponFreeZoneSpec(WeaponFreeZoneReaction.WarnOnly, 0.5f));
            _zone.HandleEnter(_bodyCollider);
            _zone.Tick(0f);
            _zone.Tick(8f);

            Assert.That(_zone.IsWarning, Is.True);
            Assert.That(_zone.IsHostile, Is.False);
            Assert.That(GameSignals.WeaponFreeZone.Value, Is.EqualTo(new WeaponFreeZoneStatus(true, WeaponFreeZoneReaction.WarnOnly, true, 0f)));
        }

        [Test]
        public void AnAutoHolsterZoneHolstersAnArmedPlayerOnEntryButLetsItDrawAgain()
        {
            _zone.Configure(new WeaponFreeZoneSpec(WeaponFreeZoneReaction.AutoHolster));

            LogAssert.Expect(LogType.Log, "[WeaponFreeZone] zone=Gate holder=Player autoHolster=began");
            _zone.HandleEnter(_bodyCollider);
            _zone.Tick(0f);

            Assert.That(_holster.Phase, Is.EqualTo(HolsterPhase.Holstering));
            Assert.That(_zone.IsWarning, Is.False);
            Assert.That(GameSignals.WeaponFreeZone.Value, Is.EqualTo(new WeaponFreeZoneStatus(true, WeaponFreeZoneReaction.AutoHolster, false, 0f)));

            _holster.Tick(RifleHolsterSeconds, false, false, false);
            Assert.That(_holster.Phase, Is.EqualTo(HolsterPhase.Holstered));
            Assert.That(_holster.Draw(), Is.True, "auto holster does not block drawing");
            _holster.Tick(RifleDrawSeconds, false, false, false);
            _zone.Tick(1f);

            Assert.That(_holster.Phase, Is.EqualTo(HolsterPhase.Drawn), "it only holsters on entry");
            Assert.That(_zone.IsHostile, Is.False);
        }

        [Test]
        public void ABlockDrawZoneKeepsAHolsteredPlayerFromDrawingUntilItLeaves()
        {
            _zone.Configure(new WeaponFreeZoneSpec(WeaponFreeZoneReaction.BlockDraw));
            _holster.Holster();
            _holster.Tick(RifleHolsterSeconds, false, false, false);

            LogAssert.Expect(LogType.Log, "[WeaponFreeZone] zone=Gate holder=Player blockDraw=on");
            _zone.HandleEnter(_bodyCollider);
            _zone.Tick(0f);

            Assert.That(_holster.IsDrawBlocked, Is.True);
            Assert.That(_holster.BlockDraw.IsHeldBy(_zone), Is.True);
            Assert.That(_holster.Draw(), Is.False);
            Assert.That(_holster.Phase, Is.EqualTo(HolsterPhase.Holstered));

            LogAssert.Expect(LogType.Log, "[WeaponFreeZone] zone=Gate holder=Player blockDraw=off");
            _zone.HandleExit(_bodyCollider);
            _zone.Tick(0f);

            Assert.That(_holster.IsDrawBlocked, Is.False);
            Assert.That(_holster.Draw(), Is.True);
        }

        [Test]
        public void ADeadPlayerCountsAsOutside()
        {
            _zone.Configure(new WeaponFreeZoneSpec(WeaponFreeZoneReaction.WarnThenHostile, HostileAfterSeconds));
            _zone.HandleEnter(_bodyCollider);
            _zone.Tick(0f);

            _damageable.Kill();
            _zone.Tick(HostileAfterSeconds * 2f);

            Assert.That(_zone.IsPlayerInside, Is.False);
            Assert.That(_zone.IsHostile, Is.False);
            Assert.That(GameSignals.WeaponFreeZone.Value, Is.Null);
        }

        [Test]
        public void ADisabledColliderNoLongerCountsAsInside()
        {
            _zone.HandleEnter(_bodyCollider);
            _zone.Tick(0f);
            Assert.That(_zone.IsPlayerInside, Is.True);

            _bodyCollider.enabled = false;
            _zone.Tick(0f);

            Assert.That(_zone.IsPlayerInside, Is.False, "a collider that went away sends no exit, so it is pruned");
        }

        [Test]
        public void ClearingThePlayerReleasesTheBlockAndTheSignal()
        {
            _zone.Configure(new WeaponFreeZoneSpec(WeaponFreeZoneReaction.BlockDraw));
            _zone.HandleEnter(_bodyCollider);
            _zone.Tick(0f);
            Assert.That(_holster.IsDrawBlocked, Is.True);

            _zone.ClearPlayer();

            Assert.That(_holster.IsDrawBlocked, Is.False);
            Assert.That(_zone.IsPlayerInside, Is.False);
            Assert.That(GameSignals.WeaponFreeZone.Value, Is.Null);
        }

        [Test]
        public void APlayerWithoutAHolsterCountsAsArmedAndIsWarnedAbout()
        {
            var bare = new GameObject("Bare") { tag = GameBootstrap.PlayerTag };
            _created.Add(bare);
            var collider = bare.AddComponent<BoxCollider>();
            bare.AddComponent<Damageable>().Initialize("Bare", Damageable.DefaultMaxHealth);
            _zone.Configure(new WeaponFreeZoneSpec(WeaponFreeZoneReaction.AutoHolster));

            LogAssert.Expect(LogType.Warning, "[WeaponFreeZone] zone=Gate holder=Bare has no HolsterController; reaction=AutoHolster cannot holster or block drawing");
            _zone.HandleEnter(collider);
            _zone.Tick(0f);

            Assert.That(_zone.IsPlayerInside, Is.True);
            Assert.That(_zone.State.Armed, Is.True);
        }

        [Test]
        public void ZonesAreFoundByTheirId()
        {
            Assert.That(WeaponFreeZone.FindById(ZoneId), Is.SameAs(_zone));
            Assert.That(WeaponFreeZone.FindById("Nowhere"), Is.Null);
            Assert.That(WeaponFreeZone.FindById(string.Empty), Is.Null);
        }

        private static int CountChildren(Transform parent, string childName)
        {
            var count = 0;
            foreach (Transform child in parent)
            {
                if (child.name == childName)
                {
                    count++;
                }
            }

            return count;
        }

        private static WeaponSpec WithTimes(WeaponSpec spec, float drawSeconds, float holsterSeconds)
        {
            return new WeaponSpec(
                spec.Id,
                spec.NameKey,
                spec.Category,
                spec.Trigger,
                spec.Ammo,
                spec.Round,
                spec.Impact,
                spec.Aim,
                spec.AmmoTypes,
                spec.ShotNoiseRadius,
                spec.Weight,
                spec.MoveSpeedMultiplier,
                spec.Description,
                drawSeconds,
                holsterSeconds);
        }
    }
}

using System.Collections.Generic;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Edit Mode では Update とトリガーの通知が走らないので、ゾーンは HandleEnter と Tick で、警備は Perceive と Refresh で進める。
    // プレイヤーには HolsterController を付けない。しまう状態を持たない体は構えていると数えるので、入れば警告が始まる。
    public sealed class NeutralGuardTests
    {
        private const string GuardId = "Guard";
        private const string ZoneId = "Gate";
        private const float HostileAfterSeconds = 1f;

        private readonly List<Object> _created = new List<Object>();

        private EnemyAgent _agent;
        private Damageable _player;
        private Collider _playerCollider;
        private WeaponFreeZone _zone;
        private NeutralGuard _guard;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            _agent = CreateEnemy();
            _player = CreatePlayer(new Vector3(0f, 1f, 3f));
            _playerCollider = _player.GetComponent<Collider>();
            _agent.Target = _player;

            var zoneObject = new GameObject("Zone");
            _created.Add(zoneObject);
            _zone = zoneObject.AddComponent<WeaponFreeZone>();
            _zone.ZoneId = ZoneId;
            _zone.Configure(new WeaponFreeZoneSpec(WeaponFreeZoneReaction.WarnThenHostile, HostileAfterSeconds));

            _guard = _agent.gameObject.AddComponent<NeutralGuard>();
            _guard.Zone = _zone;
            _guard.Attach();
        }

        [TearDown]
        public void TearDown()
        {
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
        public void AttachingAddsItAsAPerceptionContributorOnce()
        {
            _guard.Attach();

            Assert.That(_guard.IsAttached, Is.True);
            Assert.That(CountGuards(), Is.EqualTo(1));
        }

        [Test]
        public void WhileNeutralTheGuardNeitherSeesNorHearsThePlayer()
        {
            _agent.Alert(_player.GroundPoint);

            var perception = _agent.Perceive();

            Assert.That(_guard.Phase, Is.EqualTo(NeutralGuardPhase.Neutral));
            Assert.That(perception.HasLineOfSight, Is.False);
            Assert.That(perception.Target, Is.Null);
            Assert.That(perception.HeardNoise, Is.False);
            Assert.That(perception.NoisePosition, Is.Null);
            Assert.That(perception.Awareness, Is.Zero);
        }

        [Test]
        public void DetachingMakesItAnOrdinaryEnemyAgain()
        {
            _guard.Detach();

            Assert.That(_guard.IsAttached, Is.False);
            Assert.That(CountGuards(), Is.Zero);
            Assert.That(_agent.Perceive().HasLineOfSight, Is.True, "without the guard the enemy sees the player in front of it");
        }

        [Test]
        public void ArmedInTheZoneTheGuardWarnsButStillDoesNotSeeThePlayer()
        {
            _zone.HandleEnter(_playerCollider);
            _zone.Tick(0f);

            LogAssert.Expect(LogType.Log, "[WeaponFreeZone] guard=Guard zone=Gate phase=Warning reason=zone");
            var perception = _agent.Perceive();

            Assert.That(_guard.Phase, Is.EqualTo(NeutralGuardPhase.Warning));
            Assert.That(perception.HasLineOfSight, Is.False);
        }

        [Test]
        public void LeavingTheZoneDuringTheWarningCalmsTheGuardDown()
        {
            _zone.HandleEnter(_playerCollider);
            _zone.Tick(0f);
            _guard.Refresh();

            _zone.HandleExit(_playerCollider);
            _zone.Tick(0.5f);
            LogAssert.Expect(LogType.Log, "[WeaponFreeZone] guard=Guard zone=Gate phase=Neutral reason=zone");
            _guard.Refresh();

            Assert.That(_guard.Phase, Is.EqualTo(NeutralGuardPhase.Neutral));
        }

        [Test]
        public void WhenTheZoneTurnsHostileTheGuardSeesThePlayerAndStaysHostile()
        {
            _zone.HandleEnter(_playerCollider);
            _zone.Tick(0f);
            _zone.Tick(HostileAfterSeconds);
            Assert.That(_zone.IsHostile, Is.True);

            LogAssert.Expect(LogType.Log, "[WeaponFreeZone] guard=Guard zone=Gate phase=Hostile reason=zone");
            var perception = _agent.Perceive();

            Assert.That(_guard.IsHostile, Is.True);
            Assert.That(perception.HasLineOfSight, Is.True);
            Assert.That(perception.Target, Is.Not.Null);

            _zone.HandleExit(_playerCollider);
            _zone.Tick(0f);
            _guard.Refresh();
            Assert.That(_guard.IsHostile, Is.True, "hostility never goes back");
        }

        [Test]
        public void BeingShotTurnsTheGuardHostileRightAway()
        {
            LogAssert.Expect(LogType.Log, "[WeaponFreeZone] guard=Guard zone=Gate phase=Hostile reason=shot");
            _agent.GetComponent<Damageable>().TakeDamage(1, _player.GroundPoint);

            Assert.That(_guard.IsHostile, Is.True, "no warning step before turning on the shooter");
            Assert.That(_agent.Perceive().HasLineOfSight, Is.True);
        }

        [Test]
        public void ADetachedGuardNoLongerListensForShots()
        {
            _guard.Detach();

            _agent.GetComponent<Damageable>().TakeDamage(1, _player.GroundPoint);

            Assert.That(_guard.Phase, Is.EqualTo(NeutralGuardPhase.Neutral));
        }

        [Test]
        public void PhaseChangesAreRaised()
        {
            var phases = new List<NeutralGuardPhase>();
            _guard.PhaseChanged += (guard, phase) =>
            {
                Assert.That(guard, Is.SameAs(_guard));
                phases.Add(phase);
            };

            _zone.HandleEnter(_playerCollider);
            _zone.Tick(0f);
            _guard.Refresh();
            _guard.Provoke();
            _guard.Provoke();

            Assert.That(phases, Is.EqualTo(new[] { NeutralGuardPhase.Warning, NeutralGuardPhase.Hostile }));
        }

        [Test]
        public void TheZoneIsFoundByItsId()
        {
            _guard.ZoneId = ZoneId;

            Assert.That(_guard.Zone, Is.SameAs(_zone));
        }

        [Test]
        public void AGuardWhoseZoneIsMissingStaysNeutralUntilShot()
        {
            LogAssert.Expect(LogType.Warning, "[WeaponFreeZone] guard=Guard zone=Nowhere is not in the scene; the guard stays neutral until it is shot");
            _guard.ZoneId = "Nowhere";
            _guard.Refresh();
            _guard.Refresh();

            Assert.That(_guard.Zone, Is.Null);
            Assert.That(_guard.Phase, Is.EqualTo(NeutralGuardPhase.Neutral));
            Assert.That(_agent.Perceive().HasLineOfSight, Is.False);

            _guard.Provoke();
            Assert.That(_agent.Perceive().HasLineOfSight, Is.True);
        }

        private int CountGuards()
        {
            var count = 0;
            foreach (var contributor in _agent.PerceptionContributors)
            {
                if (ReferenceEquals(contributor, _guard))
                {
                    count++;
                }
            }

            return count;
        }

        private EnemyAgent CreateEnemy()
        {
            var rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            rifle.name = "Rifle";
            WeaponAssets.Configure(rifle, TestWeapons.RifleSpec());
            _created.Add(rifle);

            var host = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            host.name = GuardId;
            var agent = host.AddComponent<EnemyAgent>();
            agent.WeaponDefinition = rifle;
            agent.Id = GuardId;
            _created.Add(host);
            host.transform.SetPositionAndRotation(
                new Vector3(0f, 0f, 8f) + (Vector3.up * host.GetComponent<UnityEngine.AI.NavMeshAgent>().baseOffset), Quaternion.LookRotation(Vector3.back));
            Assert.That(agent.TryInitialize(), Is.True);
            Physics.SyncTransforms();
            return agent;
        }

        private Damageable CreatePlayer(Vector3 position)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(body);
            body.name = "Player";
            body.tag = GameBootstrap.PlayerTag;
            body.transform.position = position;
            body.transform.localScale = new Vector3(0.6f, 2f, 0.6f);
            var damageable = body.AddComponent<Damageable>();
            damageable.Initialize("Player", 100);
            body.AddComponent<Hitbox>().Initialize(HitZone.Body, damageable);
            Physics.SyncTransforms();
            return damageable;
        }
    }
}

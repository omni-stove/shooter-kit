using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Update は Edit Mode では走らないので、止めている間の動きは Core の EnemyInterruption のテストと Play での確認に任せる。
    public sealed class EnemyAgentInterruptTests
    {
        private GameObject _host;
        private EnemyAgent _agent;
        private WeaponDefinition _rifle;
        private readonly List<Object> _created = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            _rifle.name = "Rifle";
            WeaponAssets.Configure(_rifle, TestWeapons.RifleSpec());

            _host = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _host.name = "Enemy";
            _agent = _host.AddComponent<EnemyAgent>();
            _agent.WeaponDefinition = _rifle;
            _agent.Id = "enemy-1";
            _host.GetComponent<Damageable>().Initialize("enemy-1", 10);
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
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_rifle);
        }

        [Test]
        public void InterruptNeedsAnInitializedAgent()
        {
            Assert.Throws<InvalidOperationException>(() => _agent.Interrupt(1f, "flinch"));
        }

        [Test]
        public void InterruptStopsTheAgentAndLogsTheReason()
        {
            Assert.That(_agent.TryInitialize(), Is.True);

            LogAssert.Expect(LogType.Log, "[AI] id=enemy-1 interrupted reason=flinch seconds=0.50");
            Assert.That(_agent.Interrupt(0.5f, "flinch"), Is.True);

            Assert.That(_agent.IsInterrupted, Is.True);
            Assert.That(_agent.InterruptReason, Is.EqualTo("flinch"));
            Assert.That(_agent.InterruptSecondsRemaining, Is.EqualTo(0.5f));
        }

        [Test]
        public void ShorterInterruptDoesNotCutALongerOneShort()
        {
            Assert.That(_agent.TryInitialize(), Is.True);
            _agent.Interrupt(3f, "takedown");

            Assert.That(_agent.Interrupt(0.5f, "flinch"), Is.False);
            Assert.That(_agent.InterruptReason, Is.EqualTo("takedown"));
        }

        [Test]
        public void InvalidArgumentsAreRejected()
        {
            Assert.That(_agent.TryInitialize(), Is.True);

            Assert.Throws<ArgumentOutOfRangeException>(() => _agent.Interrupt(0f, "flinch"));
            Assert.Throws<ArgumentException>(() => _agent.Interrupt(1f, ""));
            Assert.That(_agent.IsInterrupted, Is.False);
        }

        [Test]
        public void DeadAgentIsNotInterruptedAndDyingClearsTheInterrupt()
        {
            Assert.That(_agent.TryInitialize(), Is.True);
            _agent.Interrupt(2f, "flinch");

            LogAssert.Expect(LogType.Log, "[Combat] target died id=enemy-1");
            LogAssert.Expect(LogType.Log, "[AI] id=enemy-1 state=Dead prev=Idle");
            _host.GetComponent<Damageable>().TakeDamage(10);

            Assert.That(_agent.IsInterrupted, Is.False);
            Assert.That(_agent.Interrupt(1f, "flinch"), Is.False);
        }

        [Test]
        public void HitDuringAStaggerIsDroppedSoTheEnemyShootsBackInsteadOfTakingCover()
        {
            var damageable = PrepareEnemyNearCover(out var registry, out var marker);
            var shooter = CreateVisibleShooter();
            var reason = StaggerReaction.InterruptReasonPrefix + StaggerKind.Flinch;
            Assert.That(_agent.Interrupt(0.5f, reason), Is.True);

            damageable.TakeDamage(10, shooter.transform.position);
            Assert.That(_agent.Resume(reason), Is.True);

            var perception = _agent.Perceive();
            Assert.That(perception.WasHit, Is.False, "the hit that caused the stagger is not a reason to retreat");
            Assert.That(perception.HitSource.HasValue, Is.False);
            Assert.That(perception.HeardNoise, Is.True, "it still turns towards where the shot came from");
            Assert.That(perception.NoisePosition, Is.EqualTo(shooter.transform.position.ToNumerics()));
            Assert.That(perception.HasLineOfSight, Is.True);

            var decision = _agent.Brain.Tick(0.1f, perception);
            Assert.That(_agent.State, Is.AnyOf(EnemyState.Attack, EnemyState.Chase));
            Assert.That(decision.LookAt, Is.EqualTo(shooter.GroundPoint.ToNumerics()));
            Assert.That(registry.IsOccupied(marker), Is.False);

            var next = _agent.Brain.Tick(EnemySpec.DefaultReactionSeconds, _agent.Perceive());
            Assert.That(_agent.State, Is.AnyOf(EnemyState.Attack, EnemyState.Chase));
            Assert.That(next.Fire, Is.True, "it shoots back once it has reacted");
        }

        [Test]
        public void HitWhileStoppedForAnotherReasonStillSendsTheEnemyToCover()
        {
            var damageable = PrepareEnemyNearCover(out var registry, out var marker);
            var shooter = CreateVisibleShooter();
            const string Reason = "takedown";
            Assert.That(_agent.Interrupt(0.5f, Reason), Is.True);

            damageable.TakeDamage(10, shooter.transform.position);
            Assert.That(_agent.Resume(Reason), Is.True);

            var perception = _agent.Perceive();
            Assert.That(perception.WasHit, Is.True);
            Assert.That(perception.HitSource, Is.EqualTo(shooter.transform.position.ToNumerics()));

            LogAssert.Expect(LogType.Log, "[AI] id=enemy-1 state=TakeCover prev=Idle");
            var decision = _agent.Brain.Tick(0.1f, perception);
            Assert.That(_agent.State, Is.EqualTo(EnemyState.TakeCover));
            Assert.That(registry.IsOccupied(marker), Is.True);
            Assert.That(decision.Fire, Is.False);
        }

        [Test]
        public void ArchetypeComesFromTheSerializedFieldAndTheSpec()
        {
            Assert.That(_agent.Archetype, Is.EqualTo(EnemyArchetype.Rifleman));

            var serialized = new SerializedObject(_agent);
            Assert.That(serialized.FindProperty("_archetype"), Is.Not.Null);

            _agent.ApplySpec(new EnemySpec(20f, 10f, 1f, 2f, 0.5f, archetype: EnemyArchetype.Rifleman));
            Assert.That(_agent.TryInitialize(), Is.True);
            Assert.That(_agent.Brain.Spec.Archetype, Is.EqualTo(EnemyArchetype.Rifleman));
        }

        // 敵は (0,1,0) で -z を向き、遮蔽点は撃ってくる -z 側へ向けて置く。
        private Damageable PrepareEnemyNearCover(out CoverPointRegistry registry, out Shooter.Runtime.CoverPoint marker)
        {
            _host.transform.SetPositionAndRotation(new Vector3(0f, 1f, 0f), Quaternion.LookRotation(Vector3.back));
            _host.GetComponent<NavMeshAgent>().baseOffset = 1f;
            var damageable = _host.GetComponent<Damageable>();
            damageable.Initialize("enemy-1", Damageable.DefaultMaxHealth);
            _host.AddComponent<Hitbox>().Initialize(HitZone.Body, damageable);
            Physics.SyncTransforms();
            Assert.That(_agent.TryInitialize(), Is.True);

            var registryObject = Track(new GameObject("CoverPoints"));
            marker = new GameObject("Cover-A").AddComponent<Shooter.Runtime.CoverPoint>();
            marker.transform.SetParent(registryObject.transform, false);
            marker.transform.SetPositionAndRotation(new Vector3(2f, 0f, -2f), Quaternion.LookRotation(Vector3.back));
            marker.Height = CoverHeight.Low;
            registry = registryObject.AddComponent<CoverPointRegistry>();
            registry.SetPoints(new[] { marker });
            _agent.CoverRegistry = registry;

            // Edit Mode では Update が走らないので、Update と同じく registry の selector を渡しておく。
            _agent.Brain.UseCover(registry.Selector);
            return damageable;
        }

        private Damageable CreateVisibleShooter()
        {
            var body = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            body.name = "Player";
            body.transform.SetPositionAndRotation(new Vector3(0f, 1f, -10f), Quaternion.LookRotation(Vector3.forward));
            body.transform.localScale = new Vector3(0.6f, 2f, 0.6f);
            var shooter = body.AddComponent<Damageable>();
            shooter.Initialize("player-1", 100);
            body.AddComponent<Hitbox>().Initialize(HitZone.Body, shooter);
            Physics.SyncTransforms();
            _agent.Target = shooter;
            return shooter;
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}

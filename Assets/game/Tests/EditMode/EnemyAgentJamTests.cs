using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class EnemyAgentJamTests
    {
        private const string _enemyId = "enemy-1";

        private readonly List<Object> _created = new List<Object>();
        private GameObject _host;
        private EnemyAgent _agent;
        private WeaponDefinition _rifle;
        private Damageable _target;

        [SetUp]
        public void SetUp()
        {
            CombatEvents.ClearSubscribers();
            _rifle = TestJam.JammingRifle(TestJam.HalfChance());
            _created.Add(_rifle);

            _host = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _created.Add(_host);
            _host.name = "Enemy";
            _host.transform.SetPositionAndRotation(new Vector3(0f, 1f, 0f), Quaternion.identity);
            _agent = _host.AddComponent<EnemyAgent>();
            _host.GetComponent<NavMeshAgent>().baseOffset = 1f;
            _host.GetComponent<Damageable>().Initialize(_enemyId, 100);
            _agent.Id = _enemyId;
            _agent.WeaponDefinition = _rifle;

            var target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _created.Add(target);
            target.name = "Player";
            target.transform.position = new Vector3(0f, 1f, 6f);
            _target = target.AddComponent<Damageable>();
            _target.Initialize("player", 1000);
            target.AddComponent<Hitbox>().Initialize(HitZone.Body, _target);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            CombatEvents.ClearSubscribers();
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void AJammedEnemyStopsFiringClearsTheJamAndFiresAgainAfterTheClearSeconds()
        {
            var roll = new FixedJamRoll(0f, 0.9f);
            Initialize();
            _agent.JamRolls = spec => spec.Id == TestWeapons.RifleId ? roll : null;
            _agent.EquipWeapon(_rifle, TestJam.JamOn);
            var events = new List<string>();
            var holders = new List<GameObject>();
            CombatEvents.WeaponJammed += info =>
            {
                events.Add("jammed " + info.WeaponId);
                holders.Add(info.Holder);
            };
            CombatEvents.WeaponJamClearStarted += info =>
            {
                events.Add("clear-started " + info.WeaponId);
                holders.Add(info.Holder);
            };
            CombatEvents.WeaponJamCleared += info =>
            {
                events.Add("cleared " + info.WeaponId);
                holders.Add(info.Holder);
            };
            var magazine = _rifle.MagazineSize;

            LogAssert.Expect(LogType.Log, $"{EnemyAgent.LogTag} id={_enemyId} [Jam] weapon={TestWeapons.RifleId} jammed sustained=0 chance=0.500");
            LogAssert.Expect(LogType.Log, $"{EnemyAgent.LogTag} id={_enemyId} [Jam] weapon={TestWeapons.RifleId} clear started seconds=1.00");
            _agent.ShootAt(_target);

            Assert.That(_agent.ShotCount, Is.EqualTo(1), "the shot that jams still fires");
            Assert.That(_agent.Weapon.IsJammed, Is.True);
            Assert.That(_agent.Weapon.IsClearingJam, Is.True, "the enemy starts the clear as soon as it jams");
            Assert.That(_agent.Weapon.IsReloading, Is.False);
            Assert.That(_agent.Weapon.AmmoInMagazine, Is.EqualTo(magazine - 1));
            Assert.That(events, Is.EqualTo(new[] { "jammed " + TestWeapons.RifleId, "clear-started " + TestWeapons.RifleId }));

            _agent.TickWeapon(TestJam.ClearSeconds / 2f);
            _agent.ShootAt(_target);
            Assert.That(_agent.ShotCount, Is.EqualTo(1), "it holds fire while clearing");
            Assert.That(_agent.Weapon.JamClearRemainingSeconds, Is.EqualTo(TestJam.ClearSeconds / 2f), "shooting again does not restart the clear");

            LogAssert.Expect(LogType.Log, $"{EnemyAgent.LogTag} id={_enemyId} [Jam] weapon={TestWeapons.RifleId} cleared");
            _agent.TickWeapon(TestJam.ClearSeconds / 2f);
            Assert.That(_agent.Weapon.IsJammed, Is.False);
            Assert.That(events, Has.Count.EqualTo(3));
            Assert.That(events[2], Is.EqualTo("cleared " + TestWeapons.RifleId));
            Assert.That(holders, Is.EqualTo(new[] { _host, _host, _host }));
            Assert.That(_agent.Weapon.AmmoInMagazine, Is.EqualTo(magazine - 1), "clearing keeps the magazine");

            _agent.ShootAt(_target);
            Assert.That(_agent.ShotCount, Is.EqualTo(2), "it fires again after the clear");
            Assert.That(_agent.Weapon.IsJammed, Is.False, "the 0.9 roll is above the 0.5 chance");
            Assert.That(_agent.Weapon.AmmoInMagazine, Is.EqualTo(magazine - 2));
            Assert.That(roll.Calls, Is.EqualTo(2));
        }

        [Test]
        public void AnEnemyWithTheJamRuleOffNeverJams()
        {
            Initialize();
            _agent.JamRolls = _ => new FixedJamRoll(0f);

            _agent.ShootAt(_target);

            Assert.That(_agent.ShotCount, Is.EqualTo(1));
            Assert.That(_agent.Weapon.Jam, Is.Null, "the default rules leave Jam off");
            Assert.That(_agent.Weapon.IsJammed, Is.False);
        }

        [Test]
        public void EachJamIsClearedBeforeTheNextShot()
        {
            Initialize();
            _agent.JamRolls = _ => new FixedJamRoll(0f);
            _agent.EquipWeapon(_rifle, TestJam.JamOn);
            _agent.ShootAt(_target);
            Assert.That(_agent.Weapon.IsClearingJam, Is.True);
            _agent.TickWeapon(TestJam.ClearSeconds);
            _agent.ShootAt(_target);
            Assert.That(_agent.Weapon.IsClearingJam, Is.True, "the second shot jams again and the clear starts again");
            Assert.That(_agent.ShotCount, Is.EqualTo(2));
        }

        private void Initialize()
        {
            Assert.That(_agent.TryInitialize(), Is.True);

            // NavMesh の無い Edit Mode で動いている扱いの速さを読ませないよう、撃つ前に足を止める。
            _host.GetComponent<NavMeshAgent>().enabled = false;
        }
    }
}

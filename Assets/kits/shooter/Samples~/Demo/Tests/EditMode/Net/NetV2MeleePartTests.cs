using System;
using System.Collections.Generic;
using GameKit.Net;
using NUnit.Framework;
using Shooter.Core;
using Shooter.NetV2;
using Shooter.Protocol;

namespace Shooter.Net.Tests
{
    // 手元の振りを出すかを、サーバーの ServerMelee と同じくルールの Melee と持っている武器で分けることを確かめる
    public sealed class NetV2MeleePartTests
    {
        // 承認される前の MatchClient の自分の Id
        private const int _localId = 0;

        private const string _rifleId = "7c1d2a54-3b8e-4f60-9a17-5e2d8c4b6a01";
        private const string _knifeId = "b3e90f7a-6d12-4c85-8e4a-1f5a9d0c7b22";

        private readonly List<string> _logs = new List<string>();
        private MatchClient _client;
        private NetV2MeleePart _part;

        [SetUp]
        public void SetUp()
        {
            _logs.Clear();
            _client = new MatchClient(new SilentTransport(), "0.2.0", RejoinKey.NewDeviceKey(), "tester", _logs.Add);
            _part = new NetV2MeleePart(_client, MeleeSpec.Default, () => null, _logs.Add, FindWeapon);
        }

        [TearDown]
        public void TearDown()
        {
            _part.Dispose();
            _client.Dispose();
        }

        [Test]
        public void WithMeleeOnAGunSwings()
        {
            _part.Apply(Snapshot(Shooter.Core.Melee.On, _rifleId));

            Assert.That(_part.CanSwing, Is.True);
        }

        [Test]
        public void WithMeleeOffAMeleeWeaponSwings()
        {
            _part.Apply(Snapshot(Shooter.Core.Melee.Off, _knifeId));

            Assert.That(_part.CanSwing, Is.True);
        }

        [Test]
        public void WithMeleeOffAGunDoesNotSwing()
        {
            _part.Apply(Snapshot(Shooter.Core.Melee.Off, _rifleId));

            Assert.That(_part.CanSwing, Is.False);
        }

        [Test]
        public void SwitchingFromTheMeleeWeaponToAGunStopsTheSwingWhenMeleeIsOff()
        {
            _part.Apply(Snapshot(Shooter.Core.Melee.Off, _knifeId));

            _part.Apply(Snapshot(Shooter.Core.Melee.Off, _rifleId));

            Assert.That(_part.CanSwing, Is.False);
        }

        [Test]
        public void WithMeleeOffAWeaponThatCannotBeFoundDoesNotSwing()
        {
            _part.Apply(Snapshot(Shooter.Core.Melee.Off, "unknown"));

            Assert.That(_part.CanSwing, Is.False);
        }

        [Test]
        public void WithoutAWeaponLookupOnlyTheRuleDecides()
        {
            _part.Dispose();
            _part = new NetV2MeleePart(_client, MeleeSpec.Default, () => null, _logs.Add);

            _part.Apply(Snapshot(Shooter.Core.Melee.Off, _knifeId));
            Assert.That(_part.CanSwing, Is.False);

            _part.Apply(Snapshot(Shooter.Core.Melee.On, _knifeId));
            Assert.That(_part.CanSwing, Is.True);
        }

        [Test]
        public void ADeadPlayerWithAMeleeWeaponDoesNotSwing()
        {
            _part.Apply(Snapshot(Shooter.Core.Melee.Off, _knifeId, health: 0));

            Assert.That(_part.CanSwing, Is.False);
        }

        [Test]
        public void OutsideAPlayingMatchAMeleeWeaponDoesNotSwing()
        {
            var snapshot = Snapshot(Shooter.Core.Melee.Off, _knifeId);
            snapshot.Match.State = GameModeState.Waiting;

            _part.Apply(snapshot);

            Assert.That(_part.CanSwing, Is.False);
        }

        private static WeaponSpec FindWeapon(string weaponId)
        {
            switch (weaponId)
            {
                case _rifleId:
                    return new WeaponSpec(_rifleId, "weapon.rifle", WeaponCategory.AssaultRifle);
                case _knifeId:
                    return new WeaponSpec(_knifeId, "weapon.knife", WeaponCategory.Melee, melee: new MeleeWeaponSpec());
                default:
                    return null;
            }
        }

        private static SnapshotMessage Snapshot(Shooter.Core.Melee melee, string weaponId, int health = 100)
        {
            var snapshot = new SnapshotMessage { HasOwner = true };
            snapshot.Match.State = GameModeState.Playing;
            snapshot.Match.Rules = MatchRulesSync.From(GameRules.Default.WithActions(ActionRules.Default.WithMelee(melee)));
            ref var own = ref snapshot.Players.Add();
            own.PlayerId = _localId;
            own.Team = Team.Alpha;
            own.Health = health;
            own.WeaponId = weaponId;
            return snapshot;
        }

        private sealed class SilentTransport : IMatchTransport
        {
            public bool IsActive => false;

            public void Listen(string address, ushort port)
            {
                throw new InvalidOperationException("a client transport does not listen");
            }

            public void Connect(string address, ushort port)
            {
            }

            public void Disconnect(ConnectionId connection, string reason)
            {
            }

            public void Send(ConnectionId connection, TransportChannel channel, ArraySegment<byte> payload)
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
    }
}

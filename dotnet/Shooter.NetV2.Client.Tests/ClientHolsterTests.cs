using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    // しまう・構えるはサーバーが持つので、手元はスナップショットの状態で撃つのを控え、相手の体の銃を下げて隠す
    public sealed class ClientHolsterTests
    {
        private const int _playerId = 3;
        private const string _rifleId = "3f2b8c1e-0a4d-4c6e-9b1a-7d2e5f8c0b13";

        private static readonly CharacterMotorState _standing = new CharacterMotorState(new Vector3(0f, 1.1f, 0f), Vector3.Zero, true, false);

        [Test]
        public void ADrawnWeaponFires()
        {
            Assert.That(ClientFire.Decide(Armed(HolsterPhase.Drawn, 0f, 30), _playerId, _standing), Is.EqualTo(FireDecision.Fire));
        }

        [TestCase(HolsterPhase.Holstering, 0.5f)]
        [TestCase(HolsterPhase.Holstered, 1f)]
        [TestCase(HolsterPhase.Drawing, 0.5f)]
        public void ALoweredWeaponNeitherFiresNorReloads(HolsterPhase phase, float lowering)
        {
            Assert.That(ClientFire.Decide(Armed(phase, lowering, 30), _playerId, _standing), Is.EqualTo(FireDecision.Hold));
            Assert.That(ClientFire.Decide(Armed(phase, lowering, 0), _playerId, _standing), Is.EqualTo(FireDecision.Hold), "an empty magazine waits too");
        }

        [TestCase(HolsterPhase.Holstering, 0.5f)]
        [TestCase(HolsterPhase.Holstered, 1f)]
        [TestCase(HolsterPhase.Drawing, 0.5f)]
        public void AVehicleWeaponSeatFiresAndReloadsWhileTheHandWeaponIsLowered(HolsterPhase phase, float lowering)
        {
            Assert.That(ClientFire.Decide(Armed(phase, lowering, 30), _playerId, _standing, null, true), Is.EqualTo(FireDecision.Fire));
            Assert.That(ClientFire.Decide(Armed(phase, lowering, 0), _playerId, _standing, null, true), Is.EqualTo(FireDecision.Reload));
        }

        [Test]
        public void ARemotePoseCarriesTheLoweringAndHidesAHolsteredWeapon()
        {
            var holstering = RemotePose.From(Row(HolsterPhase.Holstering, 0.4f));
            var holstered = RemotePose.From(Row(HolsterPhase.Holstered, 1f));
            var drawn = RemotePose.From(Row(HolsterPhase.Drawn, 0f));

            Assert.That(holstering.HolsterLowering, Is.EqualTo(0.4f));
            Assert.That(holstering.IsWeaponHidden, Is.False);
            Assert.That(holstering.ShownWeaponId, Is.EqualTo(_rifleId));
            Assert.That(holstered.HolsterLowering, Is.EqualTo(1f));
            Assert.That(holstered.IsWeaponHidden, Is.True);
            Assert.That(holstered.ShownWeaponId, Is.Empty);
            Assert.That(holstered.WeaponId, Is.EqualTo(_rifleId), "the held weapon is kept while holstered");
            Assert.That(drawn.HolsterLowering, Is.Zero);
            Assert.That(drawn.IsWeaponHidden, Is.False);
        }

        [TestCase(float.NaN, 0f)]
        [TestCase(-0.5f, 0f)]
        [TestCase(1.5f, 1f)]
        public void ALoweringOutOfRangeIsKeptInRange(float lowering, float expected)
        {
            var pose = new RemotePose(
                Vector3.Zero, 0f, PlayerStance.Standing, _rifleId, 0f, false, Shooter.Core.WallRunSide.None, false, Shooter.Core.ParkourMove.None, lowering);

            Assert.That(pose.HolsterLowering, Is.EqualTo(expected));
        }

        [Test]
        public void TheOlderConstructorsKeepTheWeaponRaised()
        {
            var pose = new RemotePose(Vector3.Zero, 0f, PlayerStance.Standing, _rifleId);

            Assert.That(pose.HolsterLowering, Is.Zero);
            Assert.That(pose.ShownWeaponId, Is.EqualTo(_rifleId));
        }

        [Test]
        public void TheTrackBlendsTheLoweringBetweenTwoSnapshots()
        {
            var track = new RemoteTrack();
            track.Record(10, RemotePose.From(Row(HolsterPhase.Holstering, 0.2f)));
            track.Record(12, RemotePose.From(Row(HolsterPhase.Holstering, 0.6f)));

            Assert.That(track.TrySample(11f, out var pose), Is.True);

            Assert.That(pose.HolsterLowering, Is.EqualTo(0.4f).Within(1e-5f));
        }

        private static PlayerSnapshot Row(HolsterPhase phase, float lowering)
        {
            return new PlayerSnapshot
            {
                PlayerId = _playerId,
                Health = 100,
                WeaponId = _rifleId,
                HolsterPhase = phase,
                HolsterLowering = lowering,
            };
        }

        private static SnapshotMessage Armed(HolsterPhase phase, float lowering, int ammo)
        {
            var snapshot = new SnapshotMessage { Tick = 10, HasOwner = true };
            ref var row = ref snapshot.Players.Add();
            row = Row(phase, lowering);
            snapshot.Owner.EquippedWeaponIds.Add(_rifleId);
            snapshot.Owner.Weapon = new WeaponSync { WeaponCount = 1, AmmoInMagazine = ammo };
            return snapshot;
        }
    }
}

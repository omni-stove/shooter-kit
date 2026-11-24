using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientAkimboTests
    {
        private const int _playerId = 3;
        private const string _pistolsId = "4d7e2a91-6c3b-4f58-9a0e-1b2c3d4e5f60";
        private const string _rifleId = "9e8d7c6b-5a49-4382-a1b0-c9d8e7f6a5b4";
        private const float _frame = 1f / 60f;
        private const float _delay = 0.05f;
        private const float _reloadSeconds = 2f;
        private const float _reloadMultiplier = 1.5f;

        private static readonly CharacterMotorState _standing = new CharacterMotorState(new Vector3(0f, 1.1f, 0f), Vector3.Zero, true, false);

        [Test]
        public void TheLeftTriggerSendsOneLeftHandRequest()
        {
            var spec = Pistols();
            var latest = Armed(new WeaponSync { WeaponCount = 1, AmmoInMagazine = 8, OffhandAmmoInMagazine = 8 }, _pistolsId);
            var offhand = new ClientOffhandTrigger();

            var requests = Frame(offhand, ClientAkimbo.OffhandTriggerFor(spec), latest, rightPressed: false, leftHeld: true, leftPressed: true);

            Assert.That(requests, Has.Count.EqualTo(1));
            Assert.That(requests[0].Hand, Is.EqualTo(AkimboHand.Left));
            Assert.That(requests[0].WeaponId, Is.EqualTo(_pistolsId));
        }

        [Test]
        public void AnEmptyLeftMagazineSendsNothingFromTheLeftButTheRightStillFires()
        {
            var spec = Pistols();
            var latest = Armed(new WeaponSync { WeaponCount = 1, AmmoInMagazine = 5, OffhandAmmoInMagazine = 0 }, _pistolsId);
            var offhand = new ClientOffhandTrigger();

            var left = offhand.Pull(ClientAkimbo.OffhandTriggerFor(spec), _frame, true, true, false, latest, _playerId, _standing, false);

            Assert.That(left.Decision, Is.EqualTo(FireDecision.Reload));
            Assert.That(left.Shots, Is.Zero, "an empty left hand sends no shot");
            Assert.That(left.Pressed, Is.True, "the press still plays the dry fire");
            Assert.That(ClientFire.Decide(latest, _playerId, _standing, spec, false, AkimboHand.Right), Is.EqualTo(FireDecision.Fire));
        }

        [Test]
        public void AnEmptyRightMagazineAsksToReloadButTheLeftStillFires()
        {
            var spec = Pistols();
            var latest = Armed(new WeaponSync { WeaponCount = 1, AmmoInMagazine = 0, OffhandAmmoInMagazine = 3 }, _pistolsId);

            Assert.That(ClientFire.Decide(latest, _playerId, _standing, spec, false, AkimboHand.Right), Is.EqualTo(FireDecision.Reload));
            Assert.That(ClientFire.Decide(latest, _playerId, _standing, spec, false, AkimboHand.Left), Is.EqualTo(FireDecision.Fire));
        }

        [Test]
        public void EachHandIsHeldOnlyByItsOwnReload()
        {
            var spec = Pistols();
            var leftReloading = Armed(
                new WeaponSync { WeaponCount = 1, AmmoInMagazine = 5, OffhandAmmoInMagazine = 0, IsOffhandReloading = true }, _pistolsId);
            var rightReloading = Armed(
                new WeaponSync { WeaponCount = 1, AmmoInMagazine = 0, IsReloading = true, OffhandAmmoInMagazine = 4 }, _pistolsId);

            Assert.That(ClientFire.Decide(leftReloading, _playerId, _standing, spec, false, AkimboHand.Left), Is.EqualTo(FireDecision.Hold));
            Assert.That(ClientFire.Decide(leftReloading, _playerId, _standing, spec, false, AkimboHand.Right), Is.EqualTo(FireDecision.Fire));
            Assert.That(ClientFire.Decide(rightReloading, _playerId, _standing, spec, false, AkimboHand.Right), Is.EqualTo(FireDecision.Hold));
            Assert.That(ClientFire.Decide(rightReloading, _playerId, _standing, spec, false, AkimboHand.Left), Is.EqualTo(FireDecision.Fire));
        }

        [Test]
        public void AWeaponThatIsNotAkimboSendsNoShotFromTheAimButton()
        {
            var rifle = Rifle();
            var latest = Armed(new WeaponSync { WeaponCount = 1, AmmoInMagazine = 30, OffhandAmmoInMagazine = 30 }, _rifleId);
            var offhand = new ClientOffhandTrigger();

            Assert.That(ClientAkimbo.OffhandTriggerFor(rifle), Is.Null);
            Assert.That(Frame(offhand, ClientAkimbo.OffhandTriggerFor(rifle), latest, rightPressed: false, leftHeld: true, leftPressed: true), Is.Empty);
            Assert.That(ClientFire.Decide(latest, _playerId, _standing, rifle, false, AkimboHand.Left), Is.EqualTo(FireDecision.Hold));
            Assert.That(ClientAkimbo.BlocksAds(rifle), Is.False, "the aim button still aims with a single weapon");
        }

        [Test]
        public void TheLeftHandDoesNotFireTheSeatWeapon()
        {
            var latest = Armed(new WeaponSync { WeaponCount = 1, AmmoInMagazine = 8, OffhandAmmoInMagazine = 8 }, _pistolsId);

            Assert.That(ClientFire.Decide(latest, _playerId, _standing, Pistols(), true, AkimboHand.Left), Is.EqualTo(FireDecision.Hold));
        }

        [Test]
        public void PressingBothTriggersInOneFrameDelaysTheLeftByTheOffhandDelay()
        {
            var spec = Pistols();
            var latest = Armed(new WeaponSync { WeaponCount = 1, AmmoInMagazine = 8, OffhandAmmoInMagazine = 8 }, _pistolsId);
            var trigger = ClientAkimbo.OffhandTriggerFor(spec);
            var offhand = new ClientOffhandTrigger();

            Assert.That(Frame(offhand, trigger, latest, rightPressed: true, leftHeld: true, leftPressed: true, deltaSeconds: 0.02f), Is.Empty);
            Assert.That(offhand.IsDelayed, Is.True);

            // 0.05 秒の遅れは、押した後の 0.02 秒のフレームで 0.03・0.01 と残り、3 フレーム目で過ぎて左を撃つ
            Assert.That(Frame(offhand, trigger, latest, rightPressed: false, leftHeld: true, leftPressed: false, deltaSeconds: 0.02f), Is.Empty);
            Assert.That(Frame(offhand, trigger, latest, rightPressed: false, leftHeld: true, leftPressed: false, deltaSeconds: 0.02f), Is.Empty);
            var delayed = Frame(offhand, trigger, latest, rightPressed: false, leftHeld: true, leftPressed: false, deltaSeconds: 0.02f);

            Assert.That(delayed, Has.Count.EqualTo(1));
            Assert.That(delayed[0].Hand, Is.EqualTo(AkimboHand.Left));
            Assert.That(offhand.IsDelayed, Is.False);
        }

        [Test]
        public void ATapReleasedDuringTheDelayStillFiresTheLeftOnce()
        {
            var spec = Pistols();
            var latest = Armed(new WeaponSync { WeaponCount = 1, AmmoInMagazine = 8, OffhandAmmoInMagazine = 8 }, _pistolsId);
            var trigger = ClientAkimbo.OffhandTriggerFor(spec);
            var offhand = new ClientOffhandTrigger();
            var shots = 0;

            shots += Frame(offhand, trigger, latest, rightPressed: true, leftHeld: false, leftPressed: true).Count;
            for (var i = 0; i < 10; i++)
            {
                shots += Frame(offhand, trigger, latest, rightPressed: false, leftHeld: false, leftPressed: false).Count;
            }

            Assert.That(shots, Is.EqualTo(1));
        }

        [Test]
        public void SwitchingWeaponsDropsTheDelayedLeftPress()
        {
            var latest = Armed(new WeaponSync { WeaponCount = 1, AmmoInMagazine = 8, OffhandAmmoInMagazine = 8 }, _pistolsId);
            var offhand = new ClientOffhandTrigger();

            Frame(offhand, ClientAkimbo.OffhandTriggerFor(Pistols()), latest, rightPressed: true, leftHeld: false, leftPressed: true);
            Assert.That(offhand.IsDelayed, Is.True);

            var next = ClientAkimbo.OffhandTriggerFor(Pistols());
            Assert.That(Frame(offhand, next, latest, rightPressed: false, leftHeld: false, leftPressed: false), Is.Empty);
            Assert.That(offhand.IsDelayed, Is.False);
            for (var i = 0; i < 10; i++)
            {
                Assert.That(Frame(offhand, next, latest, rightPressed: false, leftHeld: false, leftPressed: false), Is.Empty);
            }
        }

        [Test]
        public void TheLeftTriggerFiresAtTheWeaponsInterval()
        {
            var spec = Pistols(FireMode.Auto);
            var latest = Armed(new WeaponSync { WeaponCount = 1, AmmoInMagazine = 8, OffhandAmmoInMagazine = 8 }, _pistolsId);
            var trigger = ClientAkimbo.OffhandTriggerFor(spec);
            var offhand = new ClientOffhandTrigger();
            var shots = 0;

            // 0.2 秒ごとに 1 発撃つ武器を、押してから 0.1 秒のフレームで 1 秒押し続けると、押した 1 発と 0.2 秒ごとの 5 発の 6 発
            shots += Frame(offhand, trigger, latest, rightPressed: false, leftHeld: true, leftPressed: true, deltaSeconds: 0.1f).Count;
            for (var i = 0; i < 10; i++)
            {
                shots += Frame(offhand, trigger, latest, rightPressed: false, leftHeld: true, leftPressed: false, deltaSeconds: 0.1f).Count;
            }

            Assert.That(shots, Is.EqualTo(6));
        }

        [Test]
        public void AContinuousAkimboWeaponHasNoLeftTrigger()
        {
            var beam = new WeaponSpec(
                _pistolsId, "", WeaponCategory.Handgun, round: new RoundSpec(RoundDelivery.Beam, beam: new BeamSpec(0.1f)), akimbo: AkimboSpec.Default);

            Assert.That(ClientAkimbo.OffhandTriggerFor(beam), Is.Null);
        }

        [Test]
        public void TheAimButtonDoesNotAimWhileTheAkimboPartBlocksIt()
        {
            Assert.That(ClientAkimbo.BlocksAds(Pistols()), Is.True);
            Assert.That(ClientAkimbo.BlocksAds(Pistols(blocksAds: false)), Is.False);
            Assert.That(ClientAkimbo.BlocksAds(null), Is.False);
        }

        [Test]
        public void FillWritesTheHandEveryTime()
        {
            var request = new FireRequestMessage();
            var latest = Armed(new WeaponSync { WeaponCount = 1, AmmoInMagazine = 8, OffhandAmmoInMagazine = 8 }, _pistolsId);

            ClientFire.Fill(request, latest.Owner, _pistolsId, default, TriggerEdge.None, null, AkimboHand.Left);
            Assert.That(request.Hand, Is.EqualTo(AkimboHand.Left));

            ClientFire.Fill(request, latest.Owner, _pistolsId, default, TriggerEdge.None, null);
            Assert.That(request.Hand, Is.EqualTo(AkimboHand.Right), "a reused request does not keep the left hand");
        }

        [Test]
        public void TheHudShowsBothMagazinesAndTheSharedReserveInOneChange()
        {
            var model = new HudModel();
            var changes = 0;
            model.Changed += field =>
            {
                if (field == HudField.Ammo)
                {
                    changes++;
                }
            };

            ClientAkimbo.ShowAmmo(
                model, new WeaponSync { WeaponCount = 1, AmmoInMagazine = 6, ReserveAmmo = 24, OffhandAmmoInMagazine = 2, IsOffhandReloading = true }, Pistols(), false);

            Assert.That(changes, Is.EqualTo(1));
            Assert.That(model.MagazineAmmo, Is.EqualTo(6));
            Assert.That(model.ReserveAmmo, Is.EqualTo(24));
            Assert.That(model.IsReloading, Is.False);
            Assert.That(model.OffhandMagazineAmmo, Is.EqualTo(2));
            Assert.That(model.IsOffhandReloading, Is.True);
            Assert.That(model.IsAkimbo, Is.True);
        }

        [Test]
        public void TheHudDropsTheLeftMagazineForASingleWeapon()
        {
            var model = new HudModel();
            ClientAkimbo.ShowAmmo(model, new WeaponSync { WeaponCount = 1, AmmoInMagazine = 6, OffhandAmmoInMagazine = 2 }, Pistols(), false);

            ClientAkimbo.ShowAmmo(model, new WeaponSync { WeaponCount = 1, AmmoInMagazine = 30, ReserveAmmo = 90, IsOffhandReloading = true }, Rifle(), true);

            Assert.That(model.MagazineAmmo, Is.EqualTo(30));
            Assert.That(model.ReserveAmmo, Is.EqualTo(90));
            Assert.That(model.IsReserveInfinite, Is.True);
            Assert.That(model.OffhandMagazineAmmo, Is.Null);
            Assert.That(model.IsOffhandReloading, Is.False);
            Assert.That(model.IsAkimbo, Is.False);
        }

        [Test]
        public void TheReloadViewAdvancesEachHandFromWhenItStartedReloading()
        {
            var spec = Pistols();
            var reloads = new ClientAkimboReloads();
            var handSeconds = _reloadSeconds * _reloadMultiplier;

            var idle = reloads.Observe(Owner(new WeaponSync { WeaponCount = 1, AmmoInMagazine = 8, OffhandAmmoInMagazine = 8 }, _pistolsId), _pistolsId, spec, 10d);
            Assert.That(idle.HasValue, Is.True);
            Assert.That(idle.Value.IsHandReloading(AkimboHand.Left), Is.False);
            Assert.That(idle.Value.ReloadProgress(AkimboHand.Left), Is.Zero);

            var leftReloading = Owner(new WeaponSync { WeaponCount = 1, AmmoInMagazine = 8, IsOffhandReloading = true }, _pistolsId);
            var started = reloads.Observe(leftReloading, _pistolsId, spec, 11d);
            Assert.That(started.Value.IsHandReloading(AkimboHand.Left), Is.True);
            Assert.That(started.Value.ReloadProgress(AkimboHand.Left), Is.Zero);
            Assert.That(started.Value.IsHandReloading(AkimboHand.Right), Is.False);

            var halfway = reloads.Observe(leftReloading, _pistolsId, spec, 11d + (handSeconds / 2d));
            Assert.That(halfway.Value.ReloadProgress(AkimboHand.Left), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(halfway.Value.ReloadProgress(AkimboHand.Right), Is.Zero);

            var late = reloads.Observe(leftReloading, _pistolsId, spec, 11d + (handSeconds * 2d));
            Assert.That(late.Value.ReloadProgress(AkimboHand.Left), Is.EqualTo(1f), "the progress stops at the end until the snapshot says it is done");

            var done = reloads.Observe(Owner(new WeaponSync { WeaponCount = 1, AmmoInMagazine = 8, OffhandAmmoInMagazine = 8 }, _pistolsId), _pistolsId, spec, 20d);
            Assert.That(done.Value.IsHandReloading(AkimboHand.Left), Is.False);
        }

        [Test]
        public void TheReloadViewIsEmptyForAnotherOrASingleWeapon()
        {
            var reloads = new ClientAkimboReloads();
            var owner = Owner(new WeaponSync { WeaponCount = 1, AmmoInMagazine = 8, OffhandAmmoInMagazine = 8 }, _pistolsId);

            Assert.That(reloads.Observe(owner, _rifleId, Pistols(), 0d).HasValue, Is.False, "the shown weapon is not the held one");
            Assert.That(reloads.Observe(owner, _pistolsId, Rifle(), 0d).HasValue, Is.False, "the weapon is not akimbo");
            Assert.That(reloads.Observe(null, _pistolsId, Pistols(), 0d).HasValue, Is.False);
            Assert.That(reloads.Observe(owner, _pistolsId, Pistols(), 0d).HasValue, Is.True);
        }

        [Test]
        public void TheHandsViewClampsTheProgressAndHidesItWhileNotReloading()
        {
            var view = new AkimboHandsView(true, 2f, false, 0.7f);

            Assert.That(view.ReloadProgress(AkimboHand.Right), Is.EqualTo(1f));
            Assert.That(view.ReloadProgress(AkimboHand.Left), Is.Zero);
            Assert.That(new AkimboHandsView(true, float.NaN, true, -1f).ReloadProgress(AkimboHand.Right), Is.Zero);
            Assert.That(() => view.IsHandReloading((AkimboHand)2), Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }

        // 1 フレームぶんの左手の引き金を回し、オンラインのクライアント (NetV2Client.Trigger) と同じく撃つと決めた発数だけ要求を作る
        private static List<FireRequestMessage> Frame(
            ClientOffhandTrigger offhand,
            ClientTrigger trigger,
            SnapshotMessage latest,
            bool rightPressed,
            bool leftHeld,
            bool leftPressed,
            float deltaSeconds = _frame)
        {
            var requests = new List<FireRequestMessage>();
            var pull = offhand.Pull(trigger, deltaSeconds, leftHeld, leftPressed, rightPressed, latest, _playerId, _standing, false);
            for (var i = 0; i < pull.Shots; i++)
            {
                var request = new FireRequestMessage();
                ClientFire.Fill(request, latest.Owner, ClientFire.CurrentWeaponId(latest.Owner), pull.Pull, TriggerEdge.None, null, AkimboHand.Left);
                requests.Add(request);
            }

            return requests;
        }

        private static WeaponSpec Pistols(FireMode mode = FireMode.Semi, bool blocksAds = true)
        {
            return new WeaponSpec(
                _pistolsId,
                "",
                WeaponCategory.Handgun,
                new TriggerSpec(mode, 0.2f),
                new AmmoSpec(8, 24, _reloadSeconds),
                akimbo: new AkimboSpec(blocksAds: blocksAds, reloadSecondsMultiplier: _reloadMultiplier, offhandShotDelaySeconds: _delay));
        }

        private static WeaponSpec Rifle()
        {
            return new WeaponSpec(_rifleId, "", WeaponCategory.AssaultRifle, new TriggerSpec(FireMode.Auto, 0.1f));
        }

        private static OwnerSnapshot Owner(WeaponSync weapon, string weaponId)
        {
            return Armed(weapon, weaponId).Owner;
        }

        private static SnapshotMessage Armed(WeaponSync weapon, string weaponId)
        {
            var snapshot = new SnapshotMessage { Tick = 10, HasOwner = true };
            ref var row = ref snapshot.Players.Add();
            row.PlayerId = _playerId;
            row.Health = 100;
            row.WeaponId = weaponId;
            snapshot.Owner.EquippedWeaponIds.Add(weaponId);
            snapshot.Owner.Weapon = weapon;
            return snapshot;
        }
    }
}

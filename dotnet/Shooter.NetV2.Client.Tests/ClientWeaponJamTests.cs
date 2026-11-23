using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientWeaponJamTests
    {
        private const int _playerId = 3;
        private const int _tickRate = 30;
        private const string _rifleId = "3f2b8c1e-0a4d-4c6e-9b1a-7d2e5f8c0b13";
        private const string _pistolId = "4a3c9d2f-1b5e-4d7f-8a2b-6c3d4e5f6a71";

        private static readonly CharacterMotorState _standing = new CharacterMotorState(new Vector3(0f, 1.1f, 0f), Vector3.Zero, true, false);

        [Test]
        public void AJammedWeaponHoldsWithRoundsLeftAndDoesNotAskToReloadWhenEmpty()
        {
            var spec = Jamming();

            Assert.That(
                ClientFire.Decide(Armed(new WeaponSync { WeaponCount = 1, AmmoInMagazine = 20, IsJammed = true }), _playerId, _standing, spec),
                Is.EqualTo(FireDecision.Hold));
            Assert.That(
                ClientFire.Decide(
                    Armed(new WeaponSync { WeaponCount = 1, AmmoInMagazine = 20, IsJammed = true, JamClearEndsAt = 12.0 }), _playerId, _standing, spec),
                Is.EqualTo(FireDecision.Hold),
                "still clearing");
            Assert.That(
                ClientFire.Decide(Armed(new WeaponSync { WeaponCount = 1, AmmoInMagazine = 0, IsJammed = true }), _playerId, _standing, spec),
                Is.EqualTo(FireDecision.Hold),
                "the reload key clears the jam, so an empty magazine does not ask to reload");
            Assert.That(
                ClientFire.Decide(Armed(new WeaponSync { WeaponCount = 1, AmmoInMagazine = 20, IsJammed = true }), _playerId, _standing, null),
                Is.EqualTo(FireDecision.Hold),
                "the server's jam holds even before the weapon's values are known");
        }

        [Test]
        public void OnceTheJamIsClearedTheWeaponFiresAgainAndTheClearIsReported()
        {
            var spec = Jamming();
            var jam = new ClientWeaponJam();
            var clearing = new WeaponSync { WeaponCount = 1, AmmoInMagazine = 20, IsJammed = true, JamClearEndsAt = 12.0 };
            var cleared = new WeaponSync { WeaponCount = 1, AmmoInMagazine = 20 };

            Assert.That(jam.Observe(clearing, _rifleId), Is.EqualTo(ClientJamChange.None), "the first look has nothing to compare with");
            Assert.That(jam.Observe(cleared, _rifleId), Is.EqualTo(ClientJamChange.Cleared));
            Assert.That(ClientFire.Decide(Armed(cleared), _playerId, _standing, spec), Is.EqualTo(FireDecision.Fire));
            Assert.That(jam.Observe(cleared, _rifleId), Is.EqualTo(ClientJamChange.None), "reported once");
        }

        [Test]
        public void TheJamIsReportedWhenTheSameWeaponGoesFromFiringToJammed()
        {
            var jam = new ClientWeaponJam();
            var firing = new WeaponSync { WeaponCount = 1, AmmoInMagazine = 20 };
            var jammed = new WeaponSync { WeaponCount = 1, AmmoInMagazine = 19, IsJammed = true };

            Assert.That(jam.Observe(firing, _rifleId), Is.EqualTo(ClientJamChange.None));
            Assert.That(jam.Observe(jammed, _rifleId), Is.EqualTo(ClientJamChange.Jammed));
            Assert.That(jam.Observe(jammed, _rifleId), Is.EqualTo(ClientJamChange.None), "reported once");
            Assert.That(
                jam.Observe(new WeaponSync { WeaponCount = 1, AmmoInMagazine = 19, IsJammed = true, JamClearEndsAt = 12.0 }, _rifleId),
                Is.EqualTo(ClientJamChange.None),
                "starting the clear is not a new jam");
        }

        [Test]
        public void SwitchingWeaponsOrCancellingTheClearIsNeitherAJamNorAClear()
        {
            var jam = new ClientWeaponJam();
            var clearing = new WeaponSync { WeaponCount = 2, AmmoInMagazine = 19, IsJammed = true, JamClearEndsAt = 12.0 };
            var cancelled = new WeaponSync { WeaponCount = 2, AmmoInMagazine = 19, IsJammed = true };
            var other = new WeaponSync { WeaponCount = 2, EquippedIndex = 1, AmmoInMagazine = 12 };

            jam.Observe(clearing, _rifleId);
            Assert.That(jam.Observe(other, _pistolId), Is.EqualTo(ClientJamChange.None), "the jam stays with the rifle");
            Assert.That(jam.Observe(cancelled, _rifleId), Is.EqualTo(ClientJamChange.None), "back to the rifle that was already jammed");
            Assert.That(jam.Observe(new WeaponSync { WeaponCount = 2, AmmoInMagazine = 19 }, _rifleId), Is.EqualTo(ClientJamChange.None),
                "the clear was cancelled, so the jam did not end by clearing it");

            jam.Observe(clearing, _rifleId);
            jam.Reset();
            Assert.That(jam.Observe(new WeaponSync { WeaponCount = 2, AmmoInMagazine = 19 }, _rifleId), Is.EqualTo(ClientJamChange.None),
                "after a reset there is nothing to compare with");
        }

        [Test]
        public void TheHudShowsJammedThenTheClearShrinkingWithTheServerTimeThenNothing()
        {
            var model = new HudModel();
            var spec = Jamming(clearSeconds: 2f);

            ClientWeaponGauges.ShowJam(model, new WeaponSync { WeaponCount = 1, IsJammed = true }, spec, 9.0);

            Assert.That(model.IsJammed, Is.True);
            Assert.That(model.IsClearingJam, Is.False);
            Assert.That(model.JamClearRemaining, Is.Null);

            var clearing = new WeaponSync { WeaponCount = 1, IsJammed = true, JamClearEndsAt = 12.0 };
            ClientWeaponGauges.ShowJam(model, clearing, spec, ClientWeaponGauges.ServerSeconds(300, _tickRate));

            Assert.That(model.IsClearingJam, Is.True);
            Assert.That(model.JamClearRemaining, Is.EqualTo(1f), "10 s, 2 s before the end of a 2 s clear");

            ClientWeaponGauges.ShowJam(model, clearing, spec, ClientWeaponGauges.ServerSeconds(330, _tickRate));

            Assert.That(model.JamClearRemaining, Is.EqualTo(0.5f), "11 s");

            ClientWeaponGauges.ShowJam(model, clearing, spec, ClientWeaponGauges.ServerSeconds(390, _tickRate));

            Assert.That(model.JamClearRemaining, Is.EqualTo(0f), "13 s is past the end, waiting for the server");
            Assert.That(model.IsJammed, Is.True);

            ClientWeaponGauges.ShowJam(model, new WeaponSync { WeaponCount = 1 }, spec, 13.0);

            Assert.That(model.IsJammed, Is.False);
            Assert.That(model.JamClearRemaining, Is.Null);
        }

        [Test]
        public void TheClearRemainingIsWholeWhenTheClearLengthOrTheTimeIsUnknown()
        {
            Assert.That(ClientWeaponGauges.JamClearRemaining(12.0, null, 11.0), Is.EqualTo(1f), "no weapon values");
            Assert.That(
                ClientWeaponGauges.JamClearRemaining(12.0, new WeaponSpec(_rifleId, "", WeaponCategory.AssaultRifle), 11.0),
                Is.EqualTo(1f),
                "a weapon without a jam spec");
            Assert.That(ClientWeaponGauges.JamClearRemaining(double.NaN, Jamming(), 11.0), Is.EqualTo(1f));
            Assert.That(ClientWeaponGauges.JamClearRemaining(double.PositiveInfinity, Jamming(), 11.0), Is.EqualTo(1f));
            Assert.That(ClientWeaponGauges.JamClearRemaining(20.0, Jamming(clearSeconds: 2f), 11.0), Is.EqualTo(1f), "never above the whole clear");
        }

        [Test]
        public void NoWeaponShowsNoJam()
        {
            var model = new HudModel();
            model.SetJam();

            ClientWeaponGauges.ShowJam(model, new WeaponSync { WeaponCount = 0, IsJammed = true }, Jamming(), 9.0);

            Assert.That(model.IsJammed, Is.False);
            Assert.That(new ClientWeaponJam().Observe(new WeaponSync { WeaponCount = 0, IsJammed = true }, null), Is.EqualTo(ClientJamChange.None));
        }

        [Test]
        public void TheServerTimeIsTheTickOverTheTickRate()
        {
            Assert.That(ClientWeaponGauges.ServerSeconds(90, 30), Is.EqualTo(3.0));
            Assert.That(ClientWeaponGauges.ServerSeconds(45, 60), Is.EqualTo(0.75));
            Assert.That(ClientWeaponGauges.ServerSeconds(5, 0), Is.EqualTo(5.0), "a broken tick rate is read as 1");
        }

        private static WeaponSpec Jamming(float clearSeconds = JamSpec.DefaultClearSeconds)
        {
            return new WeaponSpec(
                _rifleId,
                "",
                WeaponCategory.AssaultRifle,
                new TriggerSpec(FireMode.Auto, 0.1f),
                ammo: new AmmoSpec(30, 90, 2f, jam: new JamSpec(clearSeconds: clearSeconds)));
        }

        private static SnapshotMessage Armed(WeaponSync weapon)
        {
            var snapshot = new SnapshotMessage { Tick = 10, HasOwner = true };
            ref var row = ref snapshot.Players.Add();
            row.PlayerId = _playerId;
            row.Health = 100;
            row.WeaponId = _rifleId;
            snapshot.Owner.EquippedWeaponIds.Add(_rifleId);
            snapshot.Owner.EquippedWeaponIds.Add(_pistolId);
            snapshot.Owner.Weapon = weapon;
            return snapshot;
        }
    }
}

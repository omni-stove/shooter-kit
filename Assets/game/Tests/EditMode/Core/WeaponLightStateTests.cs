using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class WeaponLightStateTests
    {
        private const string _rifle = "rifle";
        private const string _pistol = "pistol";

        [Test]
        public void ItStartsOff()
        {
            var state = new WeaponLightState();

            Assert.That(state.IsOn, Is.False);
            Assert.That(state.WeaponId, Is.Null);
            Assert.That(state.Status(true), Is.EqualTo(new WeaponLightStatus(true, false)));
            Assert.That(state.Status(false), Is.EqualTo(WeaponLightStatus.None));
        }

        [Test]
        public void PressingTogglesTheLightOfTheHeldWeapon()
        {
            var state = new WeaponLightState();

            Assert.That(state.Toggle(_rifle, hasLight: true, armed: true), Is.True);
            Assert.That(state.IsOn, Is.True);
            Assert.That(state.WeaponId, Is.EqualTo(_rifle));
            Assert.That(state.Status(true), Is.EqualTo(new WeaponLightStatus(true, true)));

            Assert.That(state.Toggle(_rifle, hasLight: true, armed: true), Is.True);
            Assert.That(state.IsOn, Is.False);
            Assert.That(state.WeaponId, Is.Null);
        }

        [TestCase(_rifle, false, true)]
        [TestCase(_rifle, true, false)]
        [TestCase(null, true, true)]
        [TestCase("", true, true)]
        public void ItDoesNotLightWithoutALightAWeaponOrWhileHolstered(string weaponId, bool hasLight, bool armed)
        {
            var state = new WeaponLightState();

            Assert.That(state.Toggle(weaponId, hasLight, armed), Is.False);
            Assert.That(state.IsOn, Is.False);
        }

        [Test]
        public void PressingAgainTurnsItOffEvenWhenTheLightIsGone()
        {
            var state = new WeaponLightState();
            state.Toggle(_rifle, true, true);

            Assert.That(state.Toggle(_rifle, hasLight: false, armed: false), Is.True);
            Assert.That(state.IsOn, Is.False);
        }

        [Test]
        public void SwitchingWeaponsTurnsItOffAndSwitchingBackKeepsItOff()
        {
            var state = new WeaponLightState();
            state.Toggle(_rifle, true, true);

            Assert.That(state.Follow(_pistol, hasLight: true, armed: true), Is.EqualTo((WeaponLightOffCause?)WeaponLightOffCause.Switched));
            Assert.That(state.IsOn, Is.False);

            Assert.That(state.Follow(_rifle, hasLight: true, armed: true), Is.Null, "nothing to turn off");
            Assert.That(state.IsOn, Is.False, "coming back to the rifle does not light it again");
            Assert.That(state.Status(true), Is.EqualTo(new WeaponLightStatus(true, false)));
        }

        [Test]
        public void HolsteringTurnsItOffAndDrawingKeepsItOff()
        {
            var state = new WeaponLightState();
            state.Toggle(_rifle, true, true);

            Assert.That(state.Follow(_rifle, hasLight: true, armed: false), Is.EqualTo((WeaponLightOffCause?)WeaponLightOffCause.Holstered));
            Assert.That(state.Follow(_rifle, hasLight: true, armed: true), Is.Null);
            Assert.That(state.IsOn, Is.False);
        }

        [Test]
        public void TakingTheLightOffTurnsItOffAndPuttingItBackKeepsItOff()
        {
            var state = new WeaponLightState();
            state.Toggle(_rifle, true, true);

            Assert.That(state.Follow(_rifle, hasLight: false, armed: true), Is.EqualTo((WeaponLightOffCause?)WeaponLightOffCause.NoLight));
            Assert.That(state.Follow(_rifle, hasLight: true, armed: true), Is.Null);
            Assert.That(state.IsOn, Is.False);
        }

        [Test]
        public void SwitchingIsReportedBeforeHolsteringOrALostLight()
        {
            var state = new WeaponLightState();
            state.Toggle(_rifle, true, true);

            Assert.That(state.Follow(null, hasLight: false, armed: false), Is.EqualTo((WeaponLightOffCause?)WeaponLightOffCause.Switched), "going unarmed leaves the lit weapon");
        }

        [Test]
        public void FollowingTheSameLitWeaponKeepsItOn()
        {
            var state = new WeaponLightState();
            state.Toggle(_rifle, true, true);

            Assert.That(state.Follow(_rifle, hasLight: true, armed: true), Is.Null);
            Assert.That(state.IsOn, Is.True);
        }

        [Test]
        public void TurnOffReportsWhetherItWasOn()
        {
            var state = new WeaponLightState();

            Assert.That(state.TurnOff(), Is.False);
            state.Toggle(_rifle, true, true);
            Assert.That(state.TurnOff(), Is.True);
            Assert.That(state.IsOn, Is.False);
            Assert.That(state.TurnOff(), Is.False);
        }

        [Test]
        public void TheStatusShowsOffForAWeaponWithoutALight()
        {
            var state = new WeaponLightState();
            state.Toggle(_rifle, true, true);

            Assert.That(state.Status(false), Is.EqualTo(WeaponLightStatus.None), "a weapon without a light is never shown lit");
        }

        [Test]
        public void AStatusCannotBeOnWithoutALight()
        {
            Assert.Throws<ArgumentException>(() => new WeaponLightStatus(false, true));
            Assert.That(WeaponLightStatus.None.HasLight, Is.False);
            Assert.That(WeaponLightStatus.None.IsOn, Is.False);
            Assert.That(new WeaponLightStatus(true, true) == new WeaponLightStatus(true, true), Is.True);
            Assert.That(new WeaponLightStatus(true, true) != new WeaponLightStatus(true, false), Is.True);
            var held = new WeaponLightStatus(true, false);
            var sameHeld = new WeaponLightStatus(true, false);
            Assert.That(held.GetHashCode(), Is.EqualTo(sameHeld.GetHashCode()));
        }

        [Test]
        public void TheLogLinesAreCombatLines()
        {
            Assert.That(WeaponLightLog.Toggled("p1", _rifle, true), Is.EqualTo("[Combat] weaponLight id=p1 weapon=rifle on=true"));
            Assert.That(WeaponLightLog.Toggled(null, null, false), Is.EqualTo("[Combat] weaponLight id=- weapon=- on=false"));
            Assert.That(
                WeaponLightLog.TurnedOff("p1", _rifle, WeaponLightOffCause.Holstered),
                Is.EqualTo("[Combat] weaponLight id=p1 weapon=rifle on=false cause=Holstered"));
            Assert.That(
                WeaponLightLog.NotLit("p1", _pistol, "no-light"),
                Is.EqualTo("[Combat] weaponLight id=p1 weapon=pistol not-lit reason=no-light"));
            Assert.That(
                WeaponLightLog.NoInput("Player/WeaponLight"),
                Is.EqualTo("[Combat] weaponLight input action 'Player/WeaponLight' is missing; the light key is not read"));
            Assert.That(
                WeaponLightLog.Spec("item-1", new WeaponLightSpec(25f, 45f, 3f, "weaponLight")),
                Is.EqualTo("[Combat] weaponLight item=item-1 range=25 spotAngle=45 intensity=3 visual=weaponLight"));
            Assert.That(
                WeaponLightLog.Spec(null, new WeaponLightSpec(12.5f, 30f, 0.75f)),
                Is.EqualTo("[Combat] weaponLight item=- range=12.5 spotAngle=30 intensity=0.75 visual=-"));
            Assert.Throws<ArgumentNullException>(() => WeaponLightLog.Spec("item-1", null));
        }

        [Test]
        public void TheNetLogLinesAreCombatLines()
        {
            Assert.That(WeaponLightLog.NetRequested(true), Is.EqualTo("[Combat] weaponLight net requested on=true"));
            Assert.That(WeaponLightLog.NetRequested(false), Is.EqualTo("[Combat] weaponLight net requested on=false"));
            Assert.That(
                WeaponLightLog.NetNotSent(true, "no-light"),
                Is.EqualTo("[Combat] weaponLight net not-sent on=true reason=no-light"));
            Assert.That(WeaponLightLog.NetNotSent(false, null), Is.EqualTo("[Combat] weaponLight net not-sent on=false reason=-"));
            Assert.That(WeaponLightLog.NetChanged(false, true), Is.EqualTo("[Combat] weaponLight net on=false->true"));
        }
    }
}

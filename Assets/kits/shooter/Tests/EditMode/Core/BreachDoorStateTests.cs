using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class BreachDoorStateTests
    {
        [Test]
        public void ANewDoorIsClosed()
        {
            var door = new BreachDoorState();

            Assert.That(door.Phase, Is.EqualTo(BreachDoorPhase.Closed));
            Assert.That(door.IsClosed, Is.True);
            Assert.That(door.IsBreached, Is.False);
            Assert.That(door.Status(), Is.EqualTo(new BreachStatus(BreachDoorPhase.Closed, 0f, 0f)));
        }

        [Test]
        public void KickingAClosedDoorOpensIt()
        {
            var door = new BreachDoorState();

            Assert.That(door.Kick(), Is.True);

            Assert.That(door.Phase, Is.EqualTo(BreachDoorPhase.Opened));
            Assert.That(door.IsBreached, Is.True);
            Assert.That(door.Status(), Is.EqualTo(new BreachStatus(BreachDoorPhase.Opened, 1f, 0f)));
            Assert.That(door.Tick(1f), Is.False, "an open door has nothing to count");
        }

        [Test]
        public void PlantingCountsUpThenArmsThenDestroysTheDoor()
        {
            var door = new BreachDoorState();

            Assert.That(door.BeginPlant(2f, 1f), Is.True);
            Assert.That(door.Phase, Is.EqualTo(BreachDoorPhase.Planting));
            Assert.That(door.SecondsRemaining, Is.EqualTo(2f));

            Assert.That(door.Tick(0.5f), Is.False);
            Assert.That(door.Status(), Is.EqualTo(new BreachStatus(BreachDoorPhase.Planting, 0.25f, 1.5f)));

            Assert.That(door.Tick(1.5f), Is.True, "the plant finishes on this tick");
            Assert.That(door.Phase, Is.EqualTo(BreachDoorPhase.Armed));
            Assert.That(door.Status(), Is.EqualTo(new BreachStatus(BreachDoorPhase.Armed, 0f, 1f)));

            Assert.That(door.Tick(0.5f), Is.False);
            Assert.That(door.Progress, Is.EqualTo(0.5f));

            Assert.That(door.Tick(0.5f), Is.True, "the charge goes off on this tick");
            Assert.That(door.Phase, Is.EqualTo(BreachDoorPhase.Destroyed));
            Assert.That(door.IsBreached, Is.True);
            Assert.That(door.Status(), Is.EqualTo(new BreachStatus(BreachDoorPhase.Destroyed, 1f, 0f)));
        }

        [Test]
        public void ALongTickCarriesTheLeftoverIntoTheFuse()
        {
            var door = new BreachDoorState();
            door.BeginPlant(1f, 2f);

            Assert.That(door.Tick(1.5f), Is.True);
            Assert.That(door.Phase, Is.EqualTo(BreachDoorPhase.Armed));
            Assert.That(door.SecondsRemaining, Is.EqualTo(1.5f));

            var skipping = new BreachDoorState();
            skipping.BeginPlant(1f, 2f);

            Assert.That(skipping.Tick(5f), Is.True);
            Assert.That(skipping.Phase, Is.EqualTo(BreachDoorPhase.Destroyed), "a tick past both steps lands on the end");
        }

        [Test]
        public void ManySmallTicksStillFinishOnTime()
        {
            var door = new BreachDoorState();
            door.BeginPlant(1f, 1f);

            for (var i = 0; i < 3; i++)
            {
                Assert.That(door.Tick(0.25f), Is.False);
            }

            Assert.That(door.Phase, Is.EqualTo(BreachDoorPhase.Planting));
            Assert.That(door.Tick(0.25f), Is.True, "the tick that reaches the plant seconds arms it");
            Assert.That(door.Phase, Is.EqualTo(BreachDoorPhase.Armed));
            Assert.That(door.SecondsRemaining, Is.EqualTo(1f));
        }

        [Test]
        public void ZeroSecondsSkipStraightThrough()
        {
            var instantPlant = new BreachDoorState();
            Assert.That(instantPlant.BeginPlant(0f, 2f), Is.True);
            Assert.That(instantPlant.Phase, Is.EqualTo(BreachDoorPhase.Armed));
            Assert.That(instantPlant.SecondsRemaining, Is.EqualTo(2f));

            var instantBlast = new BreachDoorState();
            Assert.That(instantBlast.BeginPlant(0f, 0f), Is.True);
            Assert.That(instantBlast.Phase, Is.EqualTo(BreachDoorPhase.Destroyed));
        }

        [Test]
        public void CancellingWhilePlantingClosesTheDoorAgain()
        {
            var door = new BreachDoorState();
            door.BeginPlant(2f, 1f);
            door.Tick(1f);

            Assert.That(door.Cancel(), Is.True);

            Assert.That(door.Phase, Is.EqualTo(BreachDoorPhase.Closed));
            Assert.That(door.Status(), Is.EqualTo(new BreachStatus(BreachDoorPhase.Closed, 0f, 0f)));
            Assert.That(door.Tick(5f), Is.False);
            Assert.That(door.BeginPlant(2f, 1f), Is.True, "a cancelled plant can start over");
            Assert.That(door.SecondsRemaining, Is.EqualTo(2f), "starting over counts from the beginning");
        }

        [Test]
        public void AnArmedChargeCannotBeCancelledOrKicked()
        {
            var door = new BreachDoorState();
            door.BeginPlant(0f, 2f);

            Assert.That(door.Cancel(), Is.False);
            Assert.That(door.Kick(), Is.False);
            Assert.That(door.BeginPlant(1f, 1f), Is.False);
            Assert.That(door.Phase, Is.EqualTo(BreachDoorPhase.Armed));
            Assert.That(door.SecondsRemaining, Is.EqualTo(2f));
        }

        [Test]
        public void ADoorBeingPlantedCannotBeKickedOrPlantedAgain()
        {
            var door = new BreachDoorState();
            door.BeginPlant(2f, 1f);

            Assert.That(door.Kick(), Is.False);
            Assert.That(door.BeginPlant(1f, 1f), Is.False);
            Assert.That(door.Phase, Is.EqualTo(BreachDoorPhase.Planting));
            Assert.That(door.SecondsRemaining, Is.EqualTo(2f));
        }

        [Test]
        public void ABreachedDoorRefusesEverything()
        {
            var kicked = new BreachDoorState();
            kicked.Kick();
            var blown = new BreachDoorState();
            blown.BeginPlant(0f, 0f);

            foreach (var door in new[] { kicked, blown })
            {
                var phase = door.Phase;
                Assert.That(door.Kick(), Is.False);
                Assert.That(door.BeginPlant(1f, 1f), Is.False);
                Assert.That(door.Cancel(), Is.False);
                Assert.That(door.Tick(1f), Is.False);
                Assert.That(door.Phase, Is.EqualTo(phase), "a breached door never closes again");
            }
        }

        [Test]
        public void CancellingWithNothingPlantedIsRefused()
        {
            Assert.That(new BreachDoorState().Cancel(), Is.False);
        }

        [Test]
        public void RejectsInvalidSeconds()
        {
            var door = new BreachDoorState();

            Assert.Throws<ArgumentOutOfRangeException>(() => door.BeginPlant(-1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => door.BeginPlant(1f, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => door.BeginPlant(float.PositiveInfinity, 1f));
            Assert.That(door.Phase, Is.EqualTo(BreachDoorPhase.Closed), "a refused plant does not start");
            Assert.Throws<ArgumentOutOfRangeException>(() => door.Tick(-0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => door.Tick(float.NaN));
        }
    }
}

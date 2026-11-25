using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class GrappleAimStatusTests
    {
        [Test]
        public void AGrabbableSurfaceWithAReadySlotCanGrapple()
        {
            var status = new GrappleAimStatus(1, GrappleMiss.None, AbilityUseBlock.None, true, new Vector3(0f, 5f, 10f), -Vector3.UnitZ, 10f, true);

            Assert.That(status.Slot, Is.EqualTo(1));
            Assert.That(status.IsGrabbable, Is.True);
            Assert.That(status.CanGrapple, Is.True);
            Assert.That(status.Point, Is.EqualTo(new Vector3(0f, 5f, 10f)));
            Assert.That(status.Normal, Is.EqualTo(-Vector3.UnitZ));
            Assert.That(status.Distance, Is.EqualTo(10f));
            Assert.That(status.SnappedToEdge, Is.True);
        }

        [Test]
        public void ASlotThatCannotStartKeepsTheSurfaceGrabbableButCannotGrapple()
        {
            var status = new GrappleAimStatus(0, GrappleMiss.None, AbilityUseBlock.Cooldown, true, Vector3.UnitZ, -Vector3.UnitZ, 1f, false);

            Assert.That(status.IsGrabbable, Is.True);
            Assert.That(status.CanGrapple, Is.False);
        }

        [Test]
        public void AMissCannotGrappleEvenWithAReadySlot()
        {
            var status = new GrappleAimStatus(0, GrappleMiss.NotGrabbable, AbilityUseBlock.None, true, Vector3.UnitZ, -Vector3.UnitZ, 1f, false);

            Assert.That(status.IsGrabbable, Is.False);
            Assert.That(status.CanGrapple, Is.False);
            Assert.That(status.HasSurface, Is.True);
        }

        [Test]
        public void WithoutASurfaceThePointIsDropped()
        {
            var status = new GrappleAimStatus(0, GrappleMiss.OutOfRange, AbilityUseBlock.None, false, Vector3.One, Vector3.UnitY, 3f, true);

            Assert.That(status.HasSurface, Is.False);
            Assert.That(status.Point, Is.EqualTo(Vector3.Zero));
            Assert.That(status.Normal, Is.EqualTo(Vector3.Zero));
            Assert.That(status.Distance, Is.EqualTo(0f));
            Assert.That(status.SnappedToEdge, Is.False);
            Assert.That(status, Is.EqualTo(new GrappleAimStatus(0, GrappleMiss.OutOfRange, AbilityUseBlock.None, false, Vector3.Zero, Vector3.Zero, 0f, false)));
        }

        [Test]
        public void EqualityComparesEveryField()
        {
            var a = new GrappleAimStatus(0, GrappleMiss.None, AbilityUseBlock.None, true, Vector3.UnitZ, -Vector3.UnitZ, 1f, false);

            Assert.That(a == new GrappleAimStatus(0, GrappleMiss.None, AbilityUseBlock.None, true, Vector3.UnitZ, -Vector3.UnitZ, 1f, false), Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(new GrappleAimStatus(0, GrappleMiss.None, AbilityUseBlock.None, true, Vector3.UnitZ, -Vector3.UnitZ, 1f, false).GetHashCode()));
            Assert.That(a != new GrappleAimStatus(1, GrappleMiss.None, AbilityUseBlock.None, true, Vector3.UnitZ, -Vector3.UnitZ, 1f, false), Is.True);
            Assert.That(a != new GrappleAimStatus(0, GrappleMiss.TooClose, AbilityUseBlock.None, true, Vector3.UnitZ, -Vector3.UnitZ, 1f, false), Is.True);
            Assert.That(a != new GrappleAimStatus(0, GrappleMiss.None, AbilityUseBlock.NoCharges, true, Vector3.UnitZ, -Vector3.UnitZ, 1f, false), Is.True);
            Assert.That(a != new GrappleAimStatus(0, GrappleMiss.None, AbilityUseBlock.None, true, Vector3.UnitY, -Vector3.UnitZ, 1f, false), Is.True);
            Assert.That(a != new GrappleAimStatus(0, GrappleMiss.None, AbilityUseBlock.None, true, Vector3.UnitZ, Vector3.UnitY, 1f, false), Is.True);
            Assert.That(a != new GrappleAimStatus(0, GrappleMiss.None, AbilityUseBlock.None, true, Vector3.UnitZ, -Vector3.UnitZ, 2f, false), Is.True);
            Assert.That(a != new GrappleAimStatus(0, GrappleMiss.None, AbilityUseBlock.None, true, Vector3.UnitZ, -Vector3.UnitZ, 1f, true), Is.True);
            Assert.That(a.Equals((object)a), Is.True);
            Assert.That(a.Equals("a"), Is.False);
        }

        [Test]
        public void ANegativeSlotIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new GrappleAimStatus(-1, GrappleMiss.None, AbilityUseBlock.None, false, Vector3.Zero, Vector3.Zero, 0f, false));
        }
    }
}

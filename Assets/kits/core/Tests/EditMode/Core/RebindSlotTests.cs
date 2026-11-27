using NUnit.Framework;

namespace GameKit.Core.Tests
{
    public sealed class RebindSlotTests
    {
        [Test]
        public void SlotsWithTheSameActionDeviceAndPartAreEqual()
        {
            var a = new RebindSlot("Jump", InputDeviceKind.Gamepad, "Up");
            var b = new RebindSlot("Jump", InputDeviceKind.Gamepad, "up");

            Assert.That(a == b, Is.True);
            Assert.That(a != b, Is.False);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }

        [Test]
        public void SlotsOnAnotherDeviceDiffer()
        {
            var keyboard = new RebindSlot("Jump", InputDeviceKind.KeyboardMouse);
            var gamepad = new RebindSlot("Jump", InputDeviceKind.Gamepad);

            Assert.That(keyboard == gamepad, Is.False);
            Assert.That(keyboard != gamepad, Is.True);
        }

        [Test]
        public void EmptyPartIsNoPart()
        {
            Assert.That(new RebindSlot("Jump", InputDeviceKind.Gamepad, "").Part, Is.Null);
            Assert.That(new RebindSlot("Jump", InputDeviceKind.Gamepad, "").ToString(), Is.EqualTo("Jump (Gamepad)"));
        }
    }
}

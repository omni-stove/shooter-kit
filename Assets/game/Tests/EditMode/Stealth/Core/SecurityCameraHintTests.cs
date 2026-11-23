using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Stealth.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class SecurityCameraHintTests
    {
        [Test]
        public void HintsWithTheSameHoldAndInputAreEqual()
        {
            var hint = new SecurityCameraHint(1.5f, "Player/Interact");

            Assert.That(hint.HoldSeconds, Is.EqualTo(1.5f));
            Assert.That(hint.InputActionPath, Is.EqualTo("Player/Interact"));
            Assert.That(hint, Is.EqualTo(new SecurityCameraHint(1.5f, "Player/Interact")));
            Assert.That(hint == new SecurityCameraHint(1.5f, "Player/Interact"), Is.True);
            Assert.That(hint.GetHashCode(), Is.EqualTo(new SecurityCameraHint(1.5f, "Player/Interact").GetHashCode()));
            Assert.That(hint, Is.Not.EqualTo(new SecurityCameraHint(2f, "Player/Interact")));
            Assert.That(hint != new SecurityCameraHint(1.5f, "Player/Use"), Is.True);
        }

        [Test]
        public void AMissingInputIsEmpty()
        {
            Assert.That(new SecurityCameraHint(1f, null).InputActionPath, Is.Empty);
            Assert.That(default(SecurityCameraHint).InputActionPath, Is.Empty);
        }

        [Test]
        public void TheHoldMustBePositiveAndFinite()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SecurityCameraHint(0f, "Player/Interact"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SecurityCameraHint(-1f, "Player/Interact"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SecurityCameraHint(float.NaN, "Player/Interact"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SecurityCameraHint(float.PositiveInfinity, "Player/Interact"));
        }

        [Test]
        public void TheDefaultHoldIsTheCameraTuningDefault()
        {
            var hint = new SecurityCameraHint(SecurityCameraSpec.Default.DisableHoldSeconds, "Player/Interact");

            Assert.That(hint.HoldSeconds, Is.EqualTo(1.5f));
        }
    }
}

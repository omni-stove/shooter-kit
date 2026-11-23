using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class WeaponJamLogTests
    {
        private const string _rifleId = "00000000-0000-4000-8000-000000000001";

        [Test]
        public void JammedNamesTheWeaponTheSustainedShotsAndTheChance()
        {
            Assert.That(
                WeaponJamLog.Jammed(_rifleId, 3, 0.125f),
                Is.EqualTo("[Jam] weapon=" + _rifleId + " jammed sustained=3 chance=0.125"));
        }

        [Test]
        public void ClearStartedSaysHowLongItTakes()
        {
            Assert.That(
                WeaponJamLog.ClearStarted(_rifleId, 1.2f),
                Is.EqualTo("[Jam] weapon=" + _rifleId + " clear started seconds=1.20"));
        }

        [Test]
        public void ClearCanceledSaysWhatWasLeft()
        {
            Assert.That(
                WeaponJamLog.ClearCanceled(_rifleId, 0.5f),
                Is.EqualTo("[Jam] weapon=" + _rifleId + " clear canceled remaining=0.50"));
        }

        [Test]
        public void ClearedNamesTheWeapon()
        {
            Assert.That(WeaponJamLog.Cleared(_rifleId), Is.EqualTo("[Jam] weapon=" + _rifleId + " cleared"));
        }

        [Test]
        public void AMissingIdIsWrittenAsADash()
        {
            Assert.That(WeaponJamLog.Cleared(null), Is.EqualTo("[Jam] weapon=- cleared"));
            Assert.That(WeaponJamLog.ClearStarted(string.Empty, 1f), Is.EqualTo("[Jam] weapon=- clear started seconds=1.00"));
        }
    }
}

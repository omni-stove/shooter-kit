using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class CloudSaveLogTests
    {
        [Test]
        public void OffCarriesTheReason()
        {
            Assert.That(CloudSaveLog.Off("no EOS credentials"), Is.EqualTo("[CloudSave] local only: no EOS credentials"));
        }

        [Test]
        public void OffRejectsNullReason()
        {
            Assert.Throws<ArgumentNullException>(() => CloudSaveLog.Off(null));
        }

        [Test]
        public void SignedOutSaysSavesStayLocal()
        {
            Assert.That(CloudSaveLog.SignedOut(), Is.EqualTo("[CloudSave] signed out; saving locally until the next sign-in"));
        }

        [Test]
        public void AccountChangedCountsTheSlotsMovedEachWay()
        {
            Assert.That(
                CloudSaveLog.AccountChanged(2, 1),
                Is.EqualTo("[CloudSave] another account signed in; set aside 2 local slot(s) of the previous account and restored 1 of this one"));
        }

        [Test]
        public void AccountChangeFailedCarriesTheProblemAndTheRetryDelay()
        {
            Assert.That(
                CloudSaveLog.AccountChangeFailed("disk full", 15d),
                Is.EqualTo("[CloudSave] could not set aside the previous account's local saves: disk full; staying local and retrying in 15s"));
            Assert.Throws<ArgumentNullException>(() => CloudSaveLog.AccountChangeFailed(null, 15d));
        }

        [Test]
        public void AccountSlotNotRestoredNamesTheSlot()
        {
            Assert.That(
                CloudSaveLog.AccountSlotNotRestored("profile", "disk full"),
                Is.EqualTo("[CloudSave] could not restore this account's local copy of slot=profile: disk full"));
            Assert.Throws<ArgumentNullException>(() => CloudSaveLog.AccountSlotNotRestored(null, "disk full"));
            Assert.Throws<ArgumentNullException>(() => CloudSaveLog.AccountSlotNotRestored("profile", null));
        }
    }
}

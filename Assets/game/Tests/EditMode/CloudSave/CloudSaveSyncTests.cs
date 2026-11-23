using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Shooter.Tests.EditMode
{
    public sealed class CloudSaveSyncTests
    {
        private const string Account = "account-1";
        private const string OtherAccount = "account-2";

        // SHA-256("account-1") の先頭 16 バイト。
        private const string AccountKey = "07e998012c1137decdf3efbbb1c3ee6d";
        private const string OtherAccountKey = "703039e88185964b380ef6ed7def548a";
        private const string Mission = "campaign-Mission01";
        private const string MissionFile = "campaign-Mission01.json";
        private const string MissionSave = "{\"checkpoint\":\"a\"}";

        // 本体はプロフィールの型を知らないので、進行と同じ名前のスロットと、その中身を読めるかの判定をテストで登録して使う。
        private const string ProfileSlot = "profile";
        private const string RejectedProfileSlot = "profile-rejected";
        private const string ProfileFile = "profile.json";
        private const string OldProfile = "{\"xp\":100}";
        private const string NewProfile = "{\"xp\":900}";

        // 本体はキャンペーンのスロットを知らないので、テストでもキャンペーンと同じ名前の付け方の判定を登録して使う。
        private const string LegacySlot = "campaign";
        private const string SlotPrefix = "campaign-";

        private long _clock;
        private double _time;

        [SetUp]
        public void SetUp()
        {
            _clock = 1000;
            _time = 0d;
            CloudSaveBootstrap.ClearRegisteredSlots();
            RegisterProfile();
            CloudSaveBootstrap.RegisterSlots(OwnsCampaignSlot, new[] { LegacySlot }, text => true);
        }

        // 進行の登録 (ProgressionCloudSave) は次の Play の開始時に入れ直されるので、ここでは本体が知る分だけ空にする。
        [TearDown]
        public void TearDown()
        {
            // 動いている同期へ遅れて登録したと知らせないよう、同期を手放してから登録を空にする。
            CloudSaveBootstrap.EndPlay();
            CloudSaveBootstrap.ClearRegisteredSlots();
        }

        [Test]
        public void RegisteringStartupSlotsWhileASyncRunsWarnsThatTheyAreCheckedNextStart()
        {
            CloudSaveBootstrap.UseCurrent(CloudSaveBootstrap.Create(new MemorySaveStorage(), null));

            LogAssert.Expect(LogType.Warning, CloudSaveLog.RegisteredLate(1));
            CloudSaveBootstrap.RegisterSlots(OwnsNotes, new[] { "notes" }, text => true);
        }

        [Test]
        public void TheSyncOfAnEndedPlayDoesNotMakeTheNextRegistrationLate()
        {
            CloudSaveBootstrap.UseCurrent(CloudSaveBootstrap.Create(new MemorySaveStorage(), null));
            CloudSaveBootstrap.EndPlay();

            CloudSaveBootstrap.RegisterSlots(OwnsNotes, new[] { "notes" }, text => true);

            Assert.That(CloudSaveBootstrap.Current, Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void WithoutABackendSavesStayLocalAndAreStamped()
        {
            var local = new MemorySaveStorage();
            var sync = Create(local, null);

            sync.Write(ProfileSlot, OldProfile);
            sync.Tick(_time);

            Assert.That(local.Text(ProfileSlot), Is.EqualTo(OldProfile));
            Assert.That(sync.TryRead(ProfileSlot, out var text), Is.True);
            Assert.That(text, Is.EqualTo(OldProfile));
            Assert.That(sync.TryGetStamp(ProfileSlot, out var stamp), Is.True);
            Assert.That(stamp, Is.EqualTo(new CloudSaveStamp(1000, false)));
            Assert.That(local.Text(CloudSaveSync.StampsSlot), Is.EqualTo("{\"version\":1,\"slots\":{\"profile\":{\"savedAt\":1000,\"deleted\":false}}}"));
            Assert.That(sync.PendingCount, Is.Zero, "nothing is queued for a cloud that is not in this build");
        }

        [Test]
        public void WhileSignedOutNothingIsSentAndSavesStayLocal()
        {
            var local = new MemorySaveStorage();
            var backend = new FakeCloudSaveBackend();
            var sync = Create(local, backend);

            sync.Write(ProfileSlot, OldProfile);
            sync.Tick(_time);
            sync.Tick(_time + 100d);

            Assert.That(backend.Calls, Is.Empty);
            Assert.That(backend.Ticks, Is.EqualTo(2), "the backend is still ticked so it can sign in");
            Assert.That(sync.IsSignedIn, Is.False);
            Assert.That(local.Text(ProfileSlot), Is.EqualTo(OldProfile));
            Assert.That(sync.PendingCount, Is.EqualTo(1), "the save waits for the sign-in");
        }

        [Test]
        public void SlotsOutsideTheSyncPassThroughUnstamped()
        {
            var local = new MemorySaveStorage();
            var backend = new FakeCloudSaveBackend { SignedInAccount = Account };
            var sync = Create(local, backend);
            Drain(sync);
            backend.Calls.Clear();

            sync.Write(RejectedProfileSlot, "broken");
            sync.Delete(RejectedProfileSlot);
            Drain(sync);

            Assert.That(sync.TryGetStamp(RejectedProfileSlot, out _), Is.False);
            Assert.That(local.Has(RejectedProfileSlot), Is.False);
            Assert.That(backend.Calls, Is.Empty);
        }

        [Test]
        public void SigningInUploadsTheLocalProfileTheCloudLacks()
        {
            var local = new MemorySaveStorage();
            var backend = new FakeCloudSaveBackend();
            var sync = Create(local, backend);
            sync.Write(ProfileSlot, OldProfile);

            backend.SignedInAccount = Account;
            LogAssert.Expect(LogType.Log, CloudSaveLog.SignedIn(2));
            Drain(sync);

            Assert.That(sync.IsSignedIn, Is.True);
            Assert.That(
                backend.Calls,
                Is.EqualTo(new[] { "list", "read profile.json", "write profile.json", "read campaign.json" }));
            Assert.That(backend.Files[ProfileFile], Is.EqualTo(CloudSaveEnvelope.Saved(ProfileSlot, 1000, OldProfile).ToJson()));
            Assert.That(backend.Files.ContainsKey("campaign.json"), Is.False, "a slot missing on both sides is not written");
        }

        [Test]
        public void AProfileSavedBeforeTheSyncIsUploadedWithTheTimeItIsFirstSeen()
        {
            var local = new MemorySaveStorage();
            local.Write(ProfileSlot, OldProfile);
            var backend = new FakeCloudSaveBackend { SignedInAccount = Account };
            var sync = Create(local, backend);
            _clock = 4242;

            Drain(sync);

            Assert.That(backend.Files[ProfileFile], Is.EqualTo(CloudSaveEnvelope.Saved(ProfileSlot, 4242, OldProfile).ToJson()));
            Assert.That(sync.TryGetStamp(ProfileSlot, out var stamp), Is.True);
            Assert.That(stamp, Is.EqualTo(new CloudSaveStamp(4242, false)));
        }

        [Test]
        public void TheNewerCloudProfileReplacesTheLocalOneAndKeepsTheOldAside()
        {
            var local = new MemorySaveStorage();
            var backend = new FakeCloudSaveBackend { SignedInAccount = Account };
            backend.Files[ProfileFile] = CloudSaveEnvelope.Saved(ProfileSlot, 2000, NewProfile).ToJson();
            var sync = Create(local, backend);
            sync.Write(ProfileSlot, OldProfile);
            var pulled = new List<string>();
            sync.SlotPulled += pulled.Add;

            Drain(sync);

            Assert.That(local.Text(ProfileSlot), Is.EqualTo(NewProfile));
            Assert.That(local.Text(CloudSaveSync.ReplacedPrefix + ProfileSlot), Is.EqualTo(OldProfile));
            Assert.That(sync.TryGetStamp(ProfileSlot, out var stamp), Is.True);
            Assert.That(stamp, Is.EqualTo(new CloudSaveStamp(2000, false)), "the cloud's time is kept so the next sync sees no change");
            Assert.That(pulled, Is.EqualTo(new[] { ProfileSlot }));
            Assert.That(backend.Calls, Does.Not.Contain("write profile.json"));
        }

        [Test]
        public void TheNewerLocalProfileReplacesTheCloudOne()
        {
            var local = new MemorySaveStorage();
            var backend = new FakeCloudSaveBackend { SignedInAccount = Account };
            backend.Files[ProfileFile] = CloudSaveEnvelope.Saved(ProfileSlot, 500, OldProfile).ToJson();
            var sync = Create(local, backend);
            sync.Write(ProfileSlot, NewProfile);
            var pulled = 0;
            sync.SlotPulled += _ => pulled++;

            Drain(sync);

            Assert.That(backend.Files[ProfileFile], Is.EqualTo(CloudSaveEnvelope.Saved(ProfileSlot, 1000, NewProfile).ToJson()));
            Assert.That(local.Text(ProfileSlot), Is.EqualTo(NewProfile));
            Assert.That(local.Has(CloudSaveSync.ReplacedPrefix + ProfileSlot), Is.False);
            Assert.That(pulled, Is.Zero);
        }

        [Test]
        public void TheSameTimeOnBothSidesIsLeftAlone()
        {
            var local = new MemorySaveStorage();
            var backend = new FakeCloudSaveBackend { SignedInAccount = Account };
            var sync = Create(local, backend);
            sync.Write(ProfileSlot, OldProfile);
            Drain(sync);
            backend.Calls.Clear();

            var restarted = Create(local, backend);
            Drain(restarted);

            Assert.That(backend.Calls, Is.EqualTo(new[] { "list", "read profile.json", "read campaign.json" }));
        }

        [Test]
        public void RegisteredSavesListedInTheCloudAreTakenEvenIfThisDeviceNeverHadThem()
        {
            var local = new MemorySaveStorage();
            var backend = new FakeCloudSaveBackend { SignedInAccount = Account };
            const string mission = "campaign-Mission02";
            backend.Files[mission + ".json"] = CloudSaveEnvelope.Saved(mission, 3000, "{\"checkpoint\":\"b\"}").ToJson();
            backend.Files["notes.json"] = "{}";
            backend.Files["campaign-Mission03.bin"] = "x";
            var sync = Create(local, backend);

            Drain(sync);

            Assert.That(local.Text(mission), Is.EqualTo("{\"checkpoint\":\"b\"}"));
            Assert.That(local.Has(CloudSaveSync.ReplacedPrefix + mission), Is.False, "nothing local was replaced");
            Assert.That(backend.Calls, Does.Not.Contain("read notes.json"));
            Assert.That(backend.Calls, Does.Not.Contain("read campaign-Mission03.bin"));
        }

        [Test]
        public void SavingWhileSignedInUploadsOnTheNextTick()
        {
            var local = new MemorySaveStorage();
            var backend = new FakeCloudSaveBackend { SignedInAccount = Account };
            var sync = Create(local, backend);
            Drain(sync);
            backend.Calls.Clear();
            _clock = 5000;

            sync.Write(ProfileSlot, NewProfile);

            Assert.That(backend.Calls, Is.Empty, "the save itself only writes locally");
            Drain(sync);
            Assert.That(backend.Calls, Is.EqualTo(new[] { "read profile.json", "write profile.json" }));
            Assert.That(backend.Files[ProfileFile], Is.EqualTo(CloudSaveEnvelope.Saved(ProfileSlot, 5000, NewProfile).ToJson()));
        }

        [Test]
        public void DeletingASaveSendsTheDeletionToOtherDevices()
        {
            var files = new Dictionary<string, string>(StringComparer.Ordinal);
            const string mission = "campaign-Mission01";
            var first = new MemorySaveStorage();
            var second = new MemorySaveStorage();
            var firstSync = Create(first, new FakeCloudSaveBackend(files) { SignedInAccount = Account });
            firstSync.Write(mission, "{\"checkpoint\":\"a\"}");
            Drain(firstSync);
            var secondSync = Create(second, new FakeCloudSaveBackend(files) { SignedInAccount = Account });
            Drain(secondSync);
            Assert.That(second.Text(mission), Is.EqualTo("{\"checkpoint\":\"a\"}"));

            _clock = 2000;
            firstSync.Delete(mission);
            Drain(firstSync);

            Assert.That(files[mission + ".json"], Is.EqualTo(CloudSaveEnvelope.Removed(mission, 2000).ToJson()));
            var restarted = Create(second, new FakeCloudSaveBackend(files) { SignedInAccount = Account });
            Drain(restarted);
            Assert.That(second.Has(mission), Is.False);
            Assert.That(second.Text(CloudSaveSync.ReplacedPrefix + mission), Is.EqualTo("{\"checkpoint\":\"a\"}"));
            Assert.That(restarted.TryGetStamp(mission, out var stamp), Is.True);
            Assert.That(stamp, Is.EqualTo(new CloudSaveStamp(2000, true)));
        }

        [Test]
        public void DeletingTwiceSendsTheDeletionOnce()
        {
            var local = new MemorySaveStorage();
            var backend = new FakeCloudSaveBackend { SignedInAccount = Account };
            var sync = Create(local, backend);
            Drain(sync);
            sync.Delete(LegacySlot);
            Drain(sync);
            backend.Calls.Clear();

            sync.Delete(LegacySlot);

            Assert.That(sync.PendingCount, Is.Zero);
            Assert.That(backend.Calls, Is.Empty);
        }

        [Test]
        public void TwoDevicesOnTheSameAccountShareTheProfile()
        {
            var files = new Dictionary<string, string>(StringComparer.Ordinal);
            var laptop = new MemorySaveStorage();
            var desktop = new MemorySaveStorage();

            var laptopSync = Create(laptop, new FakeCloudSaveBackend(files) { SignedInAccount = Account });
            laptopSync.Write(ProfileSlot, OldProfile);
            Drain(laptopSync);

            var desktopSync = Create(desktop, new FakeCloudSaveBackend(files) { SignedInAccount = Account });
            Drain(desktopSync);
            Assert.That(desktopSync.TryRead(ProfileSlot, out var onDesktop), Is.True);
            Assert.That(onDesktop, Is.EqualTo(OldProfile));

            _clock = 9000;
            desktopSync.Write(ProfileSlot, NewProfile);
            Drain(desktopSync);

            var laptopRestarted = Create(laptop, new FakeCloudSaveBackend(files) { SignedInAccount = Account });
            Drain(laptopRestarted);
            Assert.That(laptopRestarted.TryRead(ProfileSlot, out var onLaptop), Is.True);
            Assert.That(onLaptop, Is.EqualTo(NewProfile));
        }

        [Test]
        public void AFailedSyncIsRetriedAfterTheDelayAndTheDelayGrows()
        {
            var local = new MemorySaveStorage();
            var backend = new FakeCloudSaveBackend { SignedInAccount = Account };
            var sync = Create(local, backend);
            Drain(sync);
            backend.Calls.Clear();
            backend.Offline = true;
            sync.Write(ProfileSlot, OldProfile);

            LogAssert.Expect(LogType.Warning, CloudSaveLog.Failed(ProfileSlot, FakeCloudSaveBackend.OfflineError, 15d));
            sync.Tick(_time);
            sync.Tick(_time + 14d);
            Assert.That(backend.Calls, Is.EqualTo(new[] { "read profile.json" }));
            Assert.That(sync.PendingCount, Is.EqualTo(1));

            LogAssert.Expect(LogType.Warning, CloudSaveLog.Failed(ProfileSlot, FakeCloudSaveBackend.OfflineError, 30d));
            sync.Tick(_time + 15d);
            Assert.That(backend.Calls.Count, Is.EqualTo(2));

            backend.Offline = false;
            sync.Tick(_time + 44d);
            Assert.That(backend.Calls.Count, Is.EqualTo(2), "still waiting out the longer delay");
            _time += 45d;
            Drain(sync);

            Assert.That(backend.Files[ProfileFile], Is.EqualTo(CloudSaveEnvelope.Saved(ProfileSlot, 1000, OldProfile).ToJson()));
        }

        [Test]
        public void AFailedListIsRetried()
        {
            var local = new MemorySaveStorage();
            var backend = new FakeCloudSaveBackend { SignedInAccount = Account, Offline = true };
            var sync = Create(local, backend);

            LogAssert.Expect(LogType.Warning, CloudSaveLog.ListFailed(FakeCloudSaveBackend.OfflineError, 15d));
            sync.Tick(_time);

            Assert.That(backend.Calls, Is.EqualTo(new[] { "list" }));
            backend.Offline = false;
            _time += 15d;
            Drain(sync);
            Assert.That(backend.Calls, Is.EqualTo(new[] { "list", "list", "read profile.json", "read campaign.json" }));
        }

        [Test]
        public void AnOperationThatNeverAnswersTimesOutAndItsLateAnswerIsIgnored()
        {
            var local = new MemorySaveStorage();
            var backend = new FakeCloudSaveBackend { SignedInAccount = Account, Hold = true };
            var sync = Create(local, backend);

            sync.Tick(_time);
            Assert.That(sync.IsBusy, Is.True);
            sync.Tick(_time + 59d);
            Assert.That(sync.IsBusy, Is.True);

            LogAssert.Expect(LogType.Warning, new Regex(@"^\[CloudSave\] could not list the cloud saves: timed out after 60s; retrying in 15s$"));
            sync.Tick(_time + 60d);
            Assert.That(sync.IsBusy, Is.False);

            backend.ReleaseHeld();
            Assert.That(sync.PendingCount, Is.EqualTo(3), "the late list did not count; the list and both startup slots still wait");
        }

        [Test]
        public void ASaveMadeWhileTheCloudIsReadWinsOverAnOlderCloudCopy()
        {
            var local = new MemorySaveStorage();
            var backend = new FakeCloudSaveBackend { SignedInAccount = Account };
            backend.Files[ProfileFile] = CloudSaveEnvelope.Saved(ProfileSlot, 2000, OldProfile).ToJson();
            var sync = Create(local, backend);
            sync.Tick(_time);
            backend.Hold = true;
            sync.Tick(_time);
            Assert.That(backend.Calls, Is.EqualTo(new[] { "list", "read profile.json" }));

            _clock = 3000;
            sync.Write(ProfileSlot, NewProfile);
            backend.ReleaseHeld();
            backend.ReleaseHeld();
            backend.Hold = false;
            Drain(sync);

            Assert.That(local.Text(ProfileSlot), Is.EqualTo(NewProfile));
            Assert.That(backend.Files[ProfileFile], Is.EqualTo(CloudSaveEnvelope.Saved(ProfileSlot, 3000, NewProfile).ToJson()));
        }

        [Test]
        public void ASaveIsNewerThanTheLastOneEvenIfTheClockWentBack()
        {
            var sync = Create(new MemorySaveStorage(), null);
            _clock = 5000;
            sync.Write(ProfileSlot, OldProfile);

            _clock = 100;
            sync.Write(ProfileSlot, NewProfile);

            Assert.That(sync.TryGetStamp(ProfileSlot, out var stamp), Is.True);
            Assert.That(stamp, Is.EqualTo(new CloudSaveStamp(5001, false)));
        }

        [Test]
        public void ACloudFileThisVersionCannotReadIsLeftUntouched()
        {
            var local = new MemorySaveStorage();
            var backend = new FakeCloudSaveBackend { SignedInAccount = Account };
            const string newer = "{\"version\":2,\"slot\":\"profile\",\"savedAt\":1,\"deleted\":false,\"text\":\"x\"}";
            backend.Files[ProfileFile] = newer;
            var sync = Create(local, backend);
            sync.Write(ProfileSlot, OldProfile);

            LogAssert.Expect(LogType.Warning, CloudSaveLog.CloudRejected(ProfileSlot, "unsupported cloud save version (expected 1)"));
            Drain(sync);

            Assert.That(backend.Files[ProfileFile], Is.EqualTo(newer));
            Assert.That(local.Text(ProfileSlot), Is.EqualTo(OldProfile));
        }

        [Test]
        public void ACloudSaveWhoseContentThisVersionCannotReadIsLeftUntouched()
        {
            var local = new MemorySaveStorage();
            var backend = new FakeCloudSaveBackend { SignedInAccount = Account };
            var unreadable = CloudSaveEnvelope.Saved(ProfileSlot, 5000, "{\"version\":999}").ToJson();
            backend.Files[ProfileFile] = unreadable;
            var sync = Create(local, backend);
            sync.CanRead = CloudSaveBootstrap.CanRead;

            LogAssert.Expect(LogType.Warning, CloudSaveLog.CloudRejected(ProfileSlot, "the saved content cannot be read by this version"));
            Drain(sync);

            Assert.That(local.Has(ProfileSlot), Is.False);
            Assert.That(backend.Files[ProfileFile], Is.EqualTo(unreadable));
        }

        [Test]
        public void ACloudFileHoldingAnotherSlotIsLeftUntouched()
        {
            var local = new MemorySaveStorage();
            var backend = new FakeCloudSaveBackend { SignedInAccount = Account };
            var other = CloudSaveEnvelope.Saved(LegacySlot, 5000, "x").ToJson();
            backend.Files[ProfileFile] = other;
            var sync = Create(local, backend);

            LogAssert.Expect(LogType.Warning, CloudSaveLog.CloudRejected(ProfileSlot, "the file holds slot 'campaign'"));
            Drain(sync);

            Assert.That(local.Has(ProfileSlot), Is.False);
            Assert.That(backend.Files[ProfileFile], Is.EqualTo(other));
        }

        [Test]
        public void ASlotWhoseFileNameIsTooLongStaysLocal()
        {
            var local = new MemorySaveStorage();
            var backend = new FakeCloudSaveBackend { SignedInAccount = Account, MaxFileNameLength = 16 };
            var sync = Create(local, backend);
            Drain(sync);
            backend.Calls.Clear();
            const string mission = "campaign-Mission01";

            LogAssert.Expect(LogType.Warning, CloudSaveLog.NameTooLong(mission, 16));
            sync.Write(mission, "{}");
            sync.Write(mission, "{}");
            Drain(sync);

            Assert.That(local.Text(mission), Is.EqualTo("{}"));
            Assert.That(backend.Calls, Is.Empty);
        }

        [Test]
        public void SigningInAsAnotherAccountSyncsAgain()
        {
            var local = new MemorySaveStorage();
            var backend = new FakeCloudSaveBackend { SignedInAccount = Account };
            var sync = Create(local, backend);
            Drain(sync);

            backend.SignedInAccount = null;
            LogAssert.Expect(LogType.Log, CloudSaveLog.SignedOut());
            sync.Tick(_time);
            Assert.That(sync.IsSignedIn, Is.False);

            backend.Calls.Clear();
            backend.SignedInAccount = "account-2";
            Drain(sync);

            Assert.That(backend.Calls, Is.EqualTo(new[] { "list", "read profile.json", "read campaign.json" }));
        }

        [Test]
        public void AnotherAccountSigningInNeitherOverwritesNorDeletesItsCloudWithThePreviousAccountsSaves()
        {
            var local = new MemorySaveStorage();
            var backend = new FakeCloudSaveBackend { SignedInAccount = Account };
            var sync = Create(local, backend);
            var pulled = new List<string>();
            sync.SlotPulled += pulled.Add;
            _clock = 5000;
            sync.Write(ProfileSlot, NewProfile);
            sync.Write(LegacySlot, MissionSave);
            sync.Write(Mission, MissionSave);
            Drain(sync);
            _clock = 6000;
            sync.Delete(Mission);
            Drain(sync);
            Assert.That(backend.Files[MissionFile], Is.EqualTo(CloudSaveEnvelope.Removed(Mission, 6000).ToJson()));

            // 2 つ目のアカウントのクラウドは、1 つ目の手元より古い時刻のセーブを持つ。時刻だけで比べると手元の方が勝つ並び。
            var otherProfile = CloudSaveEnvelope.Saved(ProfileSlot, 500, OldProfile).ToJson();
            var otherMission = CloudSaveEnvelope.Saved(Mission, 500, "{\"checkpoint\":\"b\"}").ToJson();
            backend.Files.Clear();
            backend.Files[ProfileFile] = otherProfile;
            backend.Files[MissionFile] = otherMission;
            backend.Calls.Clear();
            pulled.Clear();
            backend.SignedInAccount = OtherAccount;
            LogAssert.Expect(LogType.Log, CloudSaveLog.AccountChanged(2, 0));
            Drain(sync);

            Assert.That(
                backend.Calls,
                Is.EqualTo(new[] { "list", "read profile.json", "read campaign.json", "read campaign-Mission01.json" }),
                "nothing is written to the second account's cloud");
            Assert.That(backend.Files.Count, Is.EqualTo(2), "the first account's 'campaign' save is not uploaded to a cloud that lacks it");
            Assert.That(backend.Files[ProfileFile], Is.EqualTo(otherProfile));
            Assert.That(backend.Files[MissionFile], Is.EqualTo(otherMission));
            Assert.That(local.Text(ProfileSlot), Is.EqualTo(OldProfile));
            Assert.That(local.Text(Mission), Is.EqualTo("{\"checkpoint\":\"b\"}"));
            Assert.That(local.Has(LegacySlot), Is.False);
            Assert.That(local.Has(CloudSaveSync.ReplacedPrefix + ProfileSlot), Is.False);
            Assert.That(sync.TryGetStamp(ProfileSlot, out var profileStamp), Is.True);
            Assert.That(profileStamp, Is.EqualTo(new CloudSaveStamp(500, false)));
            Assert.That(sync.TryGetStamp(Mission, out var missionStamp), Is.True);
            Assert.That(missionStamp, Is.EqualTo(new CloudSaveStamp(500, false)));
            Assert.That(sync.TryGetStamp(LegacySlot, out _), Is.False);
            Assert.That(
                local.Text(CloudSaveSync.StampsSlot),
                Is.EqualTo("{\"version\":1,\"account\":\"" + OtherAccountKey + "\",\"slots\":{"
                    + "\"campaign-Mission01\":{\"savedAt\":500,\"deleted\":false},"
                    + "\"profile\":{\"savedAt\":500,\"deleted\":false}}}"));

            // 1 つ目のアカウントの手元と記録は、そのアカウントの名前で取り置かれている。
            Assert.That(local.Text(CloudSaveSync.AccountSaveSlot(AccountKey, ProfileSlot)), Is.EqualTo(NewProfile));
            Assert.That(local.Text(CloudSaveSync.AccountSaveSlot(AccountKey, LegacySlot)), Is.EqualTo(MissionSave));
            Assert.That(
                local.Text(CloudSaveSync.AccountStampsSlot(AccountKey)),
                Is.EqualTo("{\"version\":1,\"account\":\"" + AccountKey + "\",\"slots\":{"
                    + "\"campaign\":{\"savedAt\":5000,\"deleted\":false},"
                    + "\"campaign-Mission01\":{\"savedAt\":6000,\"deleted\":true},"
                    + "\"profile\":{\"savedAt\":5000,\"deleted\":false}}}"));

            // 読み込み済みの側は、取り置きで手元から消えたときと、クラウドから採ったときに読み直す。
            Assert.That(pulled, Is.EqualTo(new[] { LegacySlot, ProfileSlot, ProfileSlot, Mission }));
        }

        [Test]
        public void AnotherAccountWithAnEmptyCloudStartsEmptyAndUploadsOnlyWhatItSaves()
        {
            var local = new MemorySaveStorage();
            var backend = new FakeCloudSaveBackend { SignedInAccount = Account };
            var sync = Create(local, backend);
            sync.Write(ProfileSlot, OldProfile);
            Drain(sync);

            backend.Files.Clear();
            backend.Calls.Clear();
            backend.SignedInAccount = OtherAccount;
            Drain(sync);

            Assert.That(backend.Calls, Is.EqualTo(new[] { "list", "read profile.json", "read campaign.json" }));
            Assert.That(backend.Files, Is.Empty);
            Assert.That(local.Has(ProfileSlot), Is.False);
            Assert.That(sync.TryGetStamp(ProfileSlot, out _), Is.False);

            _clock = 2000;
            sync.Write(ProfileSlot, NewProfile);
            Drain(sync);

            Assert.That(backend.Files[ProfileFile], Is.EqualTo(CloudSaveEnvelope.Saved(ProfileSlot, 2000, NewProfile).ToJson()));
            Assert.That(local.Text(CloudSaveSync.AccountSaveSlot(AccountKey, ProfileSlot)), Is.EqualTo(OldProfile));
        }

        [Test]
        public void ComingBackToTheFirstAccountRestoresItsLocalSavesAndSendsWhatItHadNotSent()
        {
            var local = new MemorySaveStorage();
            var backend = new FakeCloudSaveBackend { SignedInAccount = Account };
            var sync = Create(local, backend);
            sync.Write(ProfileSlot, OldProfile);
            sync.Write(Mission, MissionSave);
            Drain(sync);
            backend.SignedInAccount = null;
            sync.Tick(_time);
            _clock = 2000;
            sync.Write(ProfileSlot, NewProfile);
            sync.Delete(Mission);
            var firstCloud = new Dictionary<string, string>(backend.Files, StringComparer.Ordinal);

            backend.Files.Clear();
            backend.SignedInAccount = OtherAccount;
            Drain(sync);
            Assert.That(backend.Files, Is.Empty, "what the first account saved while signed out does not reach the second account's cloud");
            Assert.That(local.Has(ProfileSlot), Is.False);
            _clock = 3000;
            sync.Write(ProfileSlot, "{\"xp\":1}");
            Drain(sync);
            var secondCloud = new Dictionary<string, string>(backend.Files, StringComparer.Ordinal);

            backend.Files.Clear();
            foreach (var pair in firstCloud)
            {
                backend.Files[pair.Key] = pair.Value;
            }

            backend.Calls.Clear();
            var pulled = new List<string>();
            sync.SlotPulled += pulled.Add;
            backend.SignedInAccount = Account;
            LogAssert.Expect(LogType.Log, CloudSaveLog.AccountChanged(1, 1));
            Drain(sync);

            Assert.That(
                backend.Calls,
                Is.EqualTo(new[]
                {
                    "list", "read profile.json", "write profile.json", "read campaign.json",
                    "read campaign-Mission01.json", "write campaign-Mission01.json",
                }));
            Assert.That(local.Text(ProfileSlot), Is.EqualTo(NewProfile));
            Assert.That(local.Has(Mission), Is.False);
            Assert.That(backend.Files[ProfileFile], Is.EqualTo(CloudSaveEnvelope.Saved(ProfileSlot, 2000, NewProfile).ToJson()));
            Assert.That(backend.Files[MissionFile], Is.EqualTo(CloudSaveEnvelope.Removed(Mission, 2000).ToJson()));
            Assert.That(pulled, Is.EqualTo(new[] { ProfileSlot }));
            Assert.That(secondCloud[ProfileFile], Is.EqualTo(CloudSaveEnvelope.Saved(ProfileSlot, 3000, "{\"xp\":1}").ToJson()));
            Assert.That(local.Text(CloudSaveSync.AccountSaveSlot(OtherAccountKey, ProfileSlot)), Is.EqualTo("{\"xp\":1}"));
        }

        [Test]
        public void TheSameAccountSigningInAgainSendsWhatWasSavedAndDeletedWhileSignedOut()
        {
            var local = new MemorySaveStorage();
            var backend = new FakeCloudSaveBackend { SignedInAccount = Account };
            var sync = Create(local, backend);
            sync.Write(ProfileSlot, OldProfile);
            sync.Write(Mission, MissionSave);
            Drain(sync);
            backend.SignedInAccount = null;
            sync.Tick(_time);
            _clock = 2000;
            sync.Write(ProfileSlot, NewProfile);
            sync.Delete(Mission);
            backend.Calls.Clear();
            var pulled = new List<string>();
            sync.SlotPulled += pulled.Add;

            backend.SignedInAccount = Account;
            Drain(sync);

            Assert.That(
                backend.Calls,
                Is.EqualTo(new[]
                {
                    "list", "read profile.json", "write profile.json", "read campaign-Mission01.json",
                    "write campaign-Mission01.json", "read campaign.json",
                }));
            Assert.That(backend.Files[ProfileFile], Is.EqualTo(CloudSaveEnvelope.Saved(ProfileSlot, 2000, NewProfile).ToJson()));
            Assert.That(backend.Files[MissionFile], Is.EqualTo(CloudSaveEnvelope.Removed(Mission, 2000).ToJson()));
            Assert.That(local.Text(ProfileSlot), Is.EqualTo(NewProfile));
            Assert.That(pulled, Is.Empty);
            Assert.That(local.Has(CloudSaveSync.AccountStampsSlot(AccountKey)), Is.False, "nothing is set aside for the same account");
        }

        [Test]
        public void StampsWithoutAnOwnerGoToTheFirstAccountThatSignsInAndItsSavesAreSyncedAsBefore()
        {
            var local = new MemorySaveStorage();
            local.Write(ProfileSlot, NewProfile);
            local.Write(Mission, MissionSave);
            local.Write(
                CloudSaveSync.StampsSlot,
                "{\"version\":1,\"slots\":{\"campaign-Mission01\":{\"savedAt\":700,\"deleted\":false},\"profile\":{\"savedAt\":800,\"deleted\":false}}}");
            var backend = new FakeCloudSaveBackend { SignedInAccount = Account };
            backend.Files[ProfileFile] = CloudSaveEnvelope.Saved(ProfileSlot, 600, OldProfile).ToJson();
            var sync = Create(local, backend);
            var pulled = new List<string>();
            sync.SlotPulled += pulled.Add;

            Drain(sync);

            Assert.That(local.Text(ProfileSlot), Is.EqualTo(NewProfile));
            Assert.That(local.Text(Mission), Is.EqualTo(MissionSave));
            Assert.That(backend.Files[ProfileFile], Is.EqualTo(CloudSaveEnvelope.Saved(ProfileSlot, 800, NewProfile).ToJson()));
            Assert.That(backend.Files[MissionFile], Is.EqualTo(CloudSaveEnvelope.Saved(Mission, 700, MissionSave).ToJson()));
            Assert.That(pulled, Is.Empty);
            Assert.That(
                local.Text(CloudSaveSync.StampsSlot),
                Is.EqualTo("{\"version\":1,\"account\":\"" + AccountKey + "\",\"slots\":{"
                    + "\"campaign-Mission01\":{\"savedAt\":700,\"deleted\":false},\"profile\":{\"savedAt\":800,\"deleted\":false}}}"));
            Assert.That(local.Has(CloudSaveSync.AccountStampsSlot(AccountKey)), Is.False);
        }

        [Test]
        public void SavesMadeBeforeAnySignInGoToTheFirstAccountThatSignsIn()
        {
            var local = new MemorySaveStorage();
            var backend = new FakeCloudSaveBackend();
            var sync = Create(local, backend);
            sync.Write(ProfileSlot, OldProfile);
            Assert.That(local.Text(CloudSaveSync.StampsSlot), Is.EqualTo("{\"version\":1,\"slots\":{\"profile\":{\"savedAt\":1000,\"deleted\":false}}}"));

            backend.SignedInAccount = Account;
            Drain(sync);

            Assert.That(backend.Files[ProfileFile], Is.EqualTo(CloudSaveEnvelope.Saved(ProfileSlot, 1000, OldProfile).ToJson()));
            Assert.That(
                local.Text(CloudSaveSync.StampsSlot),
                Is.EqualTo("{\"version\":1,\"account\":\"" + AccountKey + "\",\"slots\":{\"profile\":{\"savedAt\":1000,\"deleted\":false}}}"));
        }

        [Test]
        public void TheOwnerOfTheStampsOutlivesTheSyncSoARestartAsAnotherAccountSendsNothing()
        {
            var local = new MemorySaveStorage();
            var first = Create(local, new FakeCloudSaveBackend { SignedInAccount = Account });
            first.Write(ProfileSlot, OldProfile);
            Drain(first);
            first.Delete(ProfileSlot);
            Drain(first);
            first.Write(LegacySlot, MissionSave);
            Drain(first);

            var backend = new FakeCloudSaveBackend { SignedInAccount = OtherAccount };
            var otherProfile = CloudSaveEnvelope.Saved(ProfileSlot, 500, NewProfile).ToJson();
            backend.Files[ProfileFile] = otherProfile;
            var restarted = Create(local, backend);
            Drain(restarted);

            Assert.That(backend.Calls, Is.EqualTo(new[] { "list", "read profile.json", "read campaign.json" }));
            Assert.That(backend.Files.Count, Is.EqualTo(1));
            Assert.That(backend.Files[ProfileFile], Is.EqualTo(otherProfile));
            Assert.That(local.Text(ProfileSlot), Is.EqualTo(NewProfile));
            Assert.That(local.Has(LegacySlot), Is.False);
        }

        [Test]
        public void IfThePreviousAccountsSavesCannotBeSetAsideNothingIsSyncedUntilTheyCan()
        {
            var local = new MemorySaveStorage();
            var backend = new FakeCloudSaveBackend { SignedInAccount = Account };
            var sync = Create(local, backend);
            sync.Write(ProfileSlot, OldProfile);
            Drain(sync);
            var pulled = new List<string>();
            sync.SlotPulled += pulled.Add;
            backend.Files.Clear();
            backend.Calls.Clear();

            local.FailWrites = true;
            backend.SignedInAccount = OtherAccount;
            LogAssert.Expect(LogType.Error, CloudSaveLog.AccountChangeFailed(MemorySaveStorage.FailureMessage, CloudSaveSync.DefaultRetrySeconds));
            sync.Tick(_time);
            sync.Tick(_time + 1d);

            Assert.That(sync.IsSignedIn, Is.False);
            Assert.That(backend.Calls, Is.Empty);
            Assert.That(local.Text(ProfileSlot), Is.EqualTo(OldProfile));
            Assert.That(pulled, Is.Empty);

            local.FailWrites = false;
            _time += CloudSaveSync.DefaultRetrySeconds;
            Drain(sync);

            Assert.That(sync.IsSignedIn, Is.True);
            Assert.That(backend.Files, Is.Empty);
            Assert.That(local.Has(ProfileSlot), Is.False);
            Assert.That(local.Text(CloudSaveSync.AccountSaveSlot(AccountKey, ProfileSlot)), Is.EqualTo(OldProfile));
            Assert.That(pulled, Is.EqualTo(new[] { ProfileSlot }));
        }

        [Test]
        public void IfOnlyTheStampsCannotBeWrittenWhileSwappingTheNewAccountsSavesAreNotRestoredUntilTheyCan()
        {
            var local = new MemorySaveStorage();
            var backend = new FakeCloudSaveBackend { SignedInAccount = OtherAccount };
            var sync = Create(local, backend);
            sync.Write(ProfileSlot, NewProfile);
            Drain(sync);
            backend.SignedInAccount = Account;
            Drain(sync);
            sync.Write(ProfileSlot, OldProfile);
            Drain(sync);
            backend.SignedInAccount = OtherAccount;
            Drain(sync);
            Assert.That(local.Text(ProfileSlot), Is.EqualTo(NewProfile));
            backend.Files.Clear();
            backend.Calls.Clear();
            var pulled = new List<string>();
            sync.SlotPulled += pulled.Add;

            local.FailWriteSlot = CloudSaveSync.StampsSlot;
            backend.SignedInAccount = Account;
            LogAssert.Expect(LogType.Error, CloudSaveLog.AccountChangeFailed(MemorySaveStorage.FailureMessage, CloudSaveSync.DefaultRetrySeconds));
            sync.Tick(_time);
            sync.Tick(_time + 1d);

            Assert.That(sync.IsSignedIn, Is.False);
            Assert.That(backend.Calls, Is.Empty);
            Assert.That(local.Has(ProfileSlot), Is.False);
            Assert.That(local.Text(CloudSaveSync.AccountSaveSlot(AccountKey, ProfileSlot)), Is.EqualTo(OldProfile));
            Assert.That(local.Text(CloudSaveSync.StampsSlot), Does.Contain("\"account\":\"" + OtherAccountKey + "\""));
            Assert.That(pulled, Is.Empty);

            local.FailWriteSlot = null;
            _time += CloudSaveSync.DefaultRetrySeconds;
            Drain(sync);

            Assert.That(sync.IsSignedIn, Is.True);
            Assert.That(local.Text(ProfileSlot), Is.EqualTo(OldProfile));
            Assert.That(local.Text(CloudSaveSync.StampsSlot), Does.Contain("\"account\":\"" + AccountKey + "\""));
            Assert.That(pulled, Does.Contain(ProfileSlot));
        }

        [Test]
        public void IfTheOwnerOfUnownedStampsCannotBeWrittenItStaysUnownedUntilItCan()
        {
            var local = new MemorySaveStorage();
            var backend = new FakeCloudSaveBackend();
            var sync = Create(local, backend);
            sync.Write(ProfileSlot, OldProfile);
            var unowned = local.Text(CloudSaveSync.StampsSlot);

            local.FailWriteSlot = CloudSaveSync.StampsSlot;
            backend.SignedInAccount = Account;
            LogAssert.Expect(LogType.Error, CloudSaveLog.AccountChangeFailed(MemorySaveStorage.FailureMessage, CloudSaveSync.DefaultRetrySeconds));
            sync.Tick(_time);
            sync.Tick(_time + 1d);

            Assert.That(sync.IsSignedIn, Is.False);
            Assert.That(backend.Calls, Is.Empty);
            Assert.That(local.Text(CloudSaveSync.StampsSlot), Is.EqualTo(unowned));
            Assert.That(local.Text(CloudSaveSync.StampsSlot), Does.Not.Contain("account"));

            local.FailWriteSlot = null;
            _time += CloudSaveSync.DefaultRetrySeconds;
            Drain(sync);

            Assert.That(sync.IsSignedIn, Is.True);
            Assert.That(local.Text(CloudSaveSync.StampsSlot), Does.Contain("\"account\":\"" + AccountKey + "\""));
        }

        [Test]
        public void TheAccountKeyHidesTheAccountIdAndIsStable()
        {
            Assert.That(CloudSaveSync.AccountKey(Account), Is.EqualTo(AccountKey));
            Assert.That(CloudSaveSync.AccountKey(OtherAccount), Is.EqualTo(OtherAccountKey));
            Assert.That(CloudSaveSync.AccountStampsSlot(AccountKey), Is.EqualTo("cloud-account-" + AccountKey));
            Assert.That(CloudSaveSync.AccountSaveSlot(AccountKey, ProfileSlot), Is.EqualTo("cloud-account-" + AccountKey + "-profile"));
            Assert.That(CloudSaveBootstrap.Syncs(CloudSaveSync.AccountSaveSlot(AccountKey, ProfileSlot)), Is.False);
            Assert.Throws<ArgumentException>(() => CloudSaveSync.AccountKey(null));
            Assert.Throws<ArgumentException>(() => CloudSaveSync.AccountKey(""));
            Assert.Throws<ArgumentException>(() => CloudSaveSync.AccountStampsSlot(""));
            Assert.Throws<ArgumentException>(() => CloudSaveSync.AccountSaveSlot(AccountKey, ""));
        }

        [Test]
        public void TheStampsOutliveTheSync()
        {
            var local = new MemorySaveStorage();
            Create(local, null).Write(ProfileSlot, OldProfile);

            var restarted = Create(local, null);

            Assert.That(restarted.TryGetStamp(ProfileSlot, out var stamp), Is.True);
            Assert.That(stamp, Is.EqualTo(new CloudSaveStamp(1000, false)));
        }

        [Test]
        public void BrokenStampsAreLoggedAndEverySlotCountsAsOlderThanTheCloud()
        {
            var local = new MemorySaveStorage();
            local.Write(CloudSaveSync.StampsSlot, "{");

            LogAssert.Expect(LogType.Warning, new Regex(@"^\[CloudSave\] the save times could not be read; "));
            var sync = Create(local, null);

            Assert.That(sync.TryGetStamp(ProfileSlot, out _), Is.False);
        }

        [Test]
        public void ALocalWriteFailureIsLetThroughAndNothingIsStamped()
        {
            var local = new MemorySaveStorage { FailWrites = true };
            var sync = Create(local, new FakeCloudSaveBackend { SignedInAccount = Account });

            Assert.Throws<System.IO.IOException>(() => sync.Write(ProfileSlot, OldProfile));
            Assert.That(sync.TryGetStamp(ProfileSlot, out _), Is.False);
        }

        [Test]
        public void OnlyRegisteredSlotsAreSyncedAndTheProfileIsOneOfThem()
        {
            CloudSaveBootstrap.ClearRegisteredSlots();

            Assert.That(CloudSaveBootstrap.Syncs(ProfileSlot), Is.False, "the kit itself knows no save, not even the profile");
            Assert.That(CloudSaveBootstrap.Syncs(LegacySlot), Is.False);
            Assert.That(CloudSaveBootstrap.StartupSlots, Is.Empty);
            Assert.That(CloudSaveBootstrap.CanRead(ProfileSlot, OldProfile), Is.False);

            RegisterProfile();

            Assert.That(CloudSaveBootstrap.Syncs(ProfileSlot), Is.True);
            Assert.That(CloudSaveBootstrap.Syncs(LegacySlot), Is.False, "nothing but the profile is synced before the campaign registers");
            Assert.That(CloudSaveBootstrap.Syncs("campaign-Campaign01"), Is.False);
            Assert.That(CloudSaveBootstrap.StartupSlots, Is.EqualTo(new[] { ProfileSlot }));

            CloudSaveBootstrap.RegisterSlots(OwnsCampaignSlot, new[] { LegacySlot }, text => true);

            Assert.That(CloudSaveBootstrap.Syncs(ProfileSlot), Is.True);
            Assert.That(CloudSaveBootstrap.Syncs(LegacySlot), Is.True);
            Assert.That(CloudSaveBootstrap.Syncs("campaign-Campaign01"), Is.True);
            Assert.That(CloudSaveBootstrap.Syncs(RejectedProfileSlot), Is.False);
            Assert.That(CloudSaveBootstrap.Syncs(CloudSaveSync.StampsSlot), Is.False);
            Assert.That(CloudSaveBootstrap.Syncs(CloudSaveSync.ReplacedPrefix + ProfileSlot), Is.False);
            Assert.That(CloudSaveBootstrap.Syncs(null), Is.False);
            Assert.That(CloudSaveBootstrap.StartupSlots, Is.EqualTo(new[] { ProfileSlot, LegacySlot }));
        }

        [Test]
        public void RegisteringTheSameSlotsAgainReplacesTheEarlierRegistration()
        {
            CloudSaveBootstrap.RegisterSlots(OwnsCampaignSlot, new[] { LegacySlot }, text => false);
            CloudSaveBootstrap.RegisterSlots(OwnsNotes, new[] { "notes" }, text => true);

            Assert.That(CloudSaveBootstrap.StartupSlots, Is.EqualTo(new[] { ProfileSlot, LegacySlot, "notes" }));
            Assert.That(CloudSaveBootstrap.CanRead(LegacySlot, "{}"), Is.False, "the later judgement replaced the earlier one");
            Assert.That(CloudSaveBootstrap.CanRead("notes", "{}"), Is.True);

            RegisterProfile();

            Assert.That(CloudSaveBootstrap.StartupSlots, Is.EqualTo(new[] { ProfileSlot, LegacySlot, "notes" }), "the profile is listed once");
        }

        [Test]
        public void TheRegisteredJudgementDecidesWhetherACloudSaveCanBeRead()
        {
            CloudSaveBootstrap.RegisterSlots(OwnsNotes, new string[0], text => text == "ok");
            CloudSaveBootstrap.RegisterSlots(OwnsStrict, new string[0], text => throw new FormatException("not a save"));

            Assert.That(CloudSaveBootstrap.CanRead("notes", "ok"), Is.True);
            Assert.That(CloudSaveBootstrap.CanRead("notes", "bad"), Is.False);
            Assert.That(CloudSaveBootstrap.CanRead("strict", "{}"), Is.False, "a FormatException means the content cannot be read");
            Assert.That(CloudSaveBootstrap.CanRead("nobody-owns-this", "{}"), Is.False, "a slot nobody registered is never taken");
            Assert.That(CloudSaveBootstrap.CanRead(ProfileSlot, OldProfile), Is.True);
            Assert.That(CloudSaveBootstrap.CanRead(ProfileSlot, "{\"version\":999}"), Is.False);
        }

        [Test]
        public void ARegisteredSlotIsSyncedAndItsUnreadableCloudCopyIsLeftAlone()
        {
            CloudSaveBootstrap.ClearRegisteredSlots();
            CloudSaveBootstrap.RegisterSlots(OwnsNotes, new[] { "notes" }, text => text != "unreadable");
            var local = new MemorySaveStorage();
            var backend = new FakeCloudSaveBackend { SignedInAccount = Account };
            backend.Files["notes.json"] = CloudSaveEnvelope.Saved("notes", 3000, "unreadable").ToJson();
            backend.Files["campaign-Mission01.json"] = CloudSaveEnvelope.Saved("campaign-Mission01", 3000, "{}").ToJson();
            var sync = CloudSaveBootstrap.Create(local, backend);

            LogAssert.Expect(LogType.Warning, CloudSaveLog.CloudRejected("notes", "the saved content cannot be read by this version"));
            Drain(sync);

            Assert.That(backend.Calls, Does.Contain("read notes.json"), "the registered startup slot is checked");
            Assert.That(local.Has("notes"), Is.False);
            Assert.That(backend.Calls, Does.Not.Contain("read campaign-Mission01.json"), "slots nobody registered are not synced");
            Assert.That(backend.Calls, Does.Not.Contain("read " + ProfileFile), "the profile is not synced while it is not registered");
            Assert.That(local.Has("campaign-Mission01"), Is.False);

            sync.Write("notes", "mine");
            Assert.That(sync.TryGetStamp("notes", out var stamp), Is.True, "a registered slot is stamped when written");
            Assert.That(stamp.Deleted, Is.False);
        }

        [Test]
        public void RegisterSlotsRejectsMissingAndForeignArguments()
        {
            CloudSaveBootstrap.ClearRegisteredSlots();
            Func<string, bool> always = _ => true;

            Assert.Throws<ArgumentNullException>(() => CloudSaveBootstrap.RegisterSlots(null, new string[0], always));
            Assert.Throws<ArgumentNullException>(() => CloudSaveBootstrap.RegisterSlots(OwnsNotes, null, always));
            Assert.Throws<ArgumentNullException>(() => CloudSaveBootstrap.RegisterSlots(OwnsNotes, new string[0], null));
            Assert.Throws<ArgumentException>(() => CloudSaveBootstrap.RegisterSlots(OwnsNotes, new[] { "" }, always));
            Assert.Throws<ArgumentException>(
                () => CloudSaveBootstrap.RegisterSlots(OwnsNotes, new[] { LegacySlot }, always),
                "a startup slot must be one the registration owns");
            Assert.That(CloudSaveBootstrap.Syncs("notes"), Is.False, "a rejected registration is not kept");
        }

        [Test]
        public void FileNamesMapBackToSlots()
        {
            Assert.That(CloudSaveSync.FileNameOf(ProfileSlot), Is.EqualTo(ProfileFile));
            Assert.That(CloudSaveSync.SlotOf(ProfileFile), Is.EqualTo(ProfileSlot));
            Assert.That(CloudSaveSync.SlotOf(".json"), Is.Null);
            Assert.That(CloudSaveSync.SlotOf("profile.bin"), Is.Null);
            Assert.That(CloudSaveSync.SlotOf(null), Is.Null);
        }

        [Test]
        public void RejectsMissingArguments()
        {
            var local = new MemorySaveStorage();
            Func<long> clock = () => 0;
            Assert.Throws<ArgumentNullException>(() => new CloudSaveSync(null, null, CloudSaveBootstrap.Syncs, new string[0], clock));
            Assert.Throws<ArgumentNullException>(() => new CloudSaveSync(local, null, null, new string[0], clock));
            Assert.Throws<ArgumentNullException>(() => new CloudSaveSync(local, null, CloudSaveBootstrap.Syncs, null, clock));
            Assert.Throws<ArgumentNullException>(() => new CloudSaveSync(local, null, CloudSaveBootstrap.Syncs, new string[0], null));
        }

        // 同じ判定のデリゲートで登録し直すと前の登録を置き換えるので、メソッドを渡して 1 つの登録に保つ。
        private static void RegisterProfile()
        {
            CloudSaveBootstrap.RegisterSlots(OwnsProfile, new[] { ProfileSlot }, CanReadProfile);
        }

        private static bool OwnsProfile(string slot)
        {
            return slot == ProfileSlot;
        }

        // テストのプロフィール ({"xp":…}) だけを読めるものとし、新しい版で書いた中身 ({"version":999}) は読めないものとする。
        private static bool CanReadProfile(string text)
        {
            return text != null && text.StartsWith("{\"xp\":", StringComparison.Ordinal);
        }

        private static bool OwnsCampaignSlot(string slot)
        {
            return slot == LegacySlot || (slot != null && slot.StartsWith(SlotPrefix, StringComparison.Ordinal) && slot.Length > SlotPrefix.Length);
        }

        private static bool OwnsNotes(string slot)
        {
            return slot == "notes";
        }

        private static bool OwnsStrict(string slot)
        {
            return slot == "strict";
        }

        private CloudSaveSync Create(MemorySaveStorage local, FakeCloudSaveBackend backend)
        {
            return new CloudSaveSync(local, backend, CloudSaveBootstrap.Syncs, CloudSaveBootstrap.StartupSlots, () => _clock);
        }

        private void Drain(CloudSaveSync sync)
        {
            for (var i = 0; i < 50; i++)
            {
                sync.Tick(_time);
                if (sync.IsSignedIn && !sync.IsBusy && sync.PendingCount == 0)
                {
                    return;
                }
            }

            Assert.Fail("the sync did not settle");
        }
    }
}

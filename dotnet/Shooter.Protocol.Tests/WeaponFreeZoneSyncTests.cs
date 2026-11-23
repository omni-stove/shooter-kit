using System;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Protocol.Tests
{
    // 武器を出してはいけない場所の状態は持ち主の部分に載り、変わったときだけ差分に載ることを確かめる
    public sealed class WeaponFreeZoneSyncTests
    {
        // 試合の部分が空の差分の、持ち主の部分の頭 (Id・版・tick・基準の tick・試合の部分のビット・持ち主の有無・持ち主の部分のビット・最後に処理した入力の tick)
        private const int _ownerBodyAt = 2 + 1 + 4 + 4 + 2 + 1 + 2 + 4;

        private static readonly WeaponFreeZoneStatus _warning = new WeaponFreeZoneStatus(true, WeaponFreeZoneReaction.WarnOnly, true, 0f);

        [Test]
        public void TheZoneIsTheNextOwnerPart()
        {
            Assert.That((int)OwnerFields.WeaponFreeZone, Is.EqualTo(1 << 13));
            Assert.That(OwnerFields.All.HasFlag(OwnerFields.WeaponFreeZone), Is.True);
        }

        [TestCase(WeaponFreeZoneReaction.WarnOnly, true)]
        [TestCase(WeaponFreeZoneReaction.WarnOnly, false)]
        [TestCase(WeaponFreeZoneReaction.AutoHolster, false)]
        [TestCase(WeaponFreeZoneReaction.BlockDraw, false)]
        [TestCase(WeaponFreeZoneReaction.WarnThenHostile, false)]
        public void AStatusInsideAZoneRoundTrips(WeaponFreeZoneReaction reaction, bool warning)
        {
            var sent = Snapshot(10);
            var status = new WeaponFreeZoneStatus(true, reaction, warning, 0f);
            sent.Owner.WeaponFreeZone = status;

            var got = Send(sent, null);

            Assert.That(got.IsComplete, Is.True);
            Assert.That(got.Owner.WeaponFreeZone, Is.EqualTo(status));
        }

        [Test]
        public void TheSecondsToHostileRoundTripOnlyWhileWarningOfHostility()
        {
            var sent = Snapshot(10);
            sent.Owner.WeaponFreeZone = new WeaponFreeZoneStatus(true, WeaponFreeZoneReaction.WarnThenHostile, true, 3.25f);

            var got = Send(sent, null);

            Assert.That(got.Owner.WeaponFreeZone.Warning, Is.True);
            Assert.That(got.Owner.WeaponFreeZone.Reaction, Is.EqualTo(WeaponFreeZoneReaction.WarnThenHostile));
            Assert.That(got.Owner.WeaponFreeZone.SecondsToHostile, Is.EqualTo(3.25f));
        }

        [Test]
        public void OutsideEveryZoneRoundTripsAsOutside()
        {
            Assert.That(Send(Snapshot(10), null).Owner.WeaponFreeZone, Is.EqualTo(WeaponFreeZoneStatus.Outside));
        }

        [Test]
        public void AnUnchangedZoneIsNotSentAgainAndAChangedOneResolves()
        {
            var baseline = Snapshot(10);
            baseline.Owner.WeaponFreeZone = _warning;
            var unchanged = Snapshot(11);
            unchanged.Owner.CopyFrom(baseline.Owner);
            var outside = Snapshot(11);
            outside.Owner.CopyFrom(baseline.Owner);
            outside.Owner.WeaponFreeZone = WeaponFreeZoneStatus.Outside;
            var quiet = Snapshot(11);
            quiet.Owner.CopyFrom(baseline.Owner);
            quiet.Owner.WeaponFreeZone = new WeaponFreeZoneStatus(true, WeaponFreeZoneReaction.WarnOnly, false, 0f);
            var hostile = Snapshot(11);
            hostile.Owner.CopyFrom(baseline.Owner);
            hostile.Owner.WeaponFreeZone = new WeaponFreeZoneStatus(true, WeaponFreeZoneReaction.WarnThenHostile, true, 2f);

            // 外 (1) だけ
            Assert.That(Size(outside, baseline) - Size(unchanged, baseline), Is.EqualTo(1));

            // 反応 (1)・警告 (1)
            Assert.That(Size(quiet, baseline) - Size(unchanged, baseline), Is.EqualTo(1 + 1));

            // 反応 (1)・警告 (1)・残り秒数 (4)
            Assert.That(Size(hostile, baseline) - Size(unchanged, baseline), Is.EqualTo(1 + 1 + 4));

            var got = Send(unchanged, baseline);
            Assert.That(got.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(got.Owner.WeaponFreeZone, Is.EqualTo(_warning), "the unchanged zone comes from the baseline");
            var gotOutside = Send(outside, baseline);
            Assert.That(gotOutside.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(gotOutside.Owner.WeaponFreeZone, Is.EqualTo(WeaponFreeZoneStatus.Outside));
        }

        [Test]
        public void ClearingAndCopyingASnapshotHandleTheZone()
        {
            var sent = Snapshot(10);
            sent.Owner.WeaponFreeZone = _warning;
            var copy = new SnapshotMessage();

            copy.CopyFrom(sent);
            sent.Clear();

            Assert.That(copy.Owner.WeaponFreeZone, Is.EqualTo(_warning));
            Assert.That(sent.Owner.WeaponFreeZone, Is.EqualTo(WeaponFreeZoneStatus.Outside));
        }

        [Test]
        public void AnUnknownReactionIsUnreadable()
        {
            var baseline = Snapshot(10);
            var current = Snapshot(11);
            current.Owner.WeaponFreeZone = _warning;
            var bytes = Bytes(current, baseline);

            // 外なら 0、中なら反応の番号 + 1
            Assert.That(bytes[_ownerBodyAt], Is.EqualTo((byte)(WeaponFreeZoneReaction.WarnOnly + 1)));
            bytes[_ownerBodyAt] = (byte)(WeaponFreeZoneReaction.BlockDraw + 2);

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(new ArraySegment<byte>(bytes)));
        }

        private static SnapshotMessage Snapshot(int tick)
        {
            var snapshot = new SnapshotMessage();
            snapshot.Clear();
            snapshot.Tick = tick;
            snapshot.HasOwner = true;
            snapshot.Owner.LastProcessedInputTick = 9;
            snapshot.Match.Rules = MatchRulesSync.From(GameRules.Default);
            snapshot.Players.Add().PlayerId = 1;
            return snapshot;
        }

        private static SnapshotMessage Send(SnapshotMessage message, SnapshotMessage baseline)
        {
            var copy = new SnapshotMessage();
            copy.CopyFrom((SnapshotMessage)MatchMessages.CreateClientTable().Read(new ArraySegment<byte>(Bytes(message, baseline))));
            return copy;
        }

        private static int Size(SnapshotMessage message, SnapshotMessage baseline)
        {
            return Bytes(message, baseline).Length;
        }

        private static byte[] Bytes(SnapshotMessage message, SnapshotMessage baseline)
        {
            message.Baseline = baseline;
            var writer = new MessageWriter();
            writer.WriteMessage(message);
            message.Baseline = null;
            return writer.ToArray();
        }
    }
}

using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    // ウェポンライトはサーバーが持つので、相手の体の姿はスナップショットの点灯をそのまま運び、銃を隠している間は光を見せない
    public sealed class ClientWeaponLightTests
    {
        private const int _playerId = 3;
        private const string _rifleId = "6d3c9e2f-1b5a-4d7f-8c2b-8e3f6a9d1c24";

        [Test]
        public void ThePoseCarriesTheLightOfTheRow()
        {
            var lit = RemotePose.From(Row(true, HolsterPhase.Drawn, 0f));
            var dark = RemotePose.From(Row(false, HolsterPhase.Drawn, 0f));

            Assert.That(lit.WeaponLightOn, Is.True);
            Assert.That(lit.ShowsWeaponLight, Is.True);
            Assert.That(dark.WeaponLightOn, Is.False);
            Assert.That(dark.ShowsWeaponLight, Is.False);
        }

        [Test]
        public void AnOlderPoseHasTheLightOff()
        {
            var pose = new RemotePose(
                Vector3.Zero, 0f, PlayerStance.Standing, _rifleId, 0f, false, Shooter.Core.WallRunSide.None, false, Shooter.Core.ParkourMove.None, 0f);

            Assert.That(pose.WeaponLightOn, Is.False);
            Assert.That(pose.ShowsWeaponLight, Is.False);
        }

        [Test]
        public void AHiddenWeaponShowsNoLight()
        {
            var holstered = RemotePose.From(Row(true, HolsterPhase.Holstered, 1f));

            Assert.That(holstered.WeaponLightOn, Is.True, "the received value is kept");
            Assert.That(holstered.ShowsWeaponLight, Is.False);
        }

        [Test]
        public void APoseWithoutAWeaponShowsNoLight()
        {
            var pose = new RemotePose(
                Vector3.Zero, 0f, PlayerStance.Standing, null, 0f, false, Shooter.Core.WallRunSide.None, false, Shooter.Core.ParkourMove.None, 0f, true);

            Assert.That(pose.ShowsWeaponLight, Is.False);
        }

        // 点灯は量ではないので、二つの記録の間では前の記録の値を使う
        [Test]
        public void TheTrackKeepsTheLightOfTheEarlierSnapshotBetweenTwo()
        {
            var track = new RemoteTrack();
            track.Record(10, RemotePose.From(Row(true, HolsterPhase.Drawn, 0f)));
            track.Record(12, RemotePose.From(Row(false, HolsterPhase.Drawn, 0f)));

            Assert.That(track.TrySample(11f, out var between), Is.True);
            Assert.That(track.TrySample(12f, out var latest), Is.True);

            Assert.That(between.WeaponLightOn, Is.True);
            Assert.That(latest.WeaponLightOn, Is.False);
        }

        private static PlayerSnapshot Row(bool lightOn, HolsterPhase phase, float lowering)
        {
            return new PlayerSnapshot
            {
                PlayerId = _playerId,
                Health = 100,
                WeaponId = _rifleId,
                HolsterPhase = phase,
                HolsterLowering = lowering,
                WeaponLightOn = lightOn,
            };
        }
    }
}

using System;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // 鳴った音が次の Advance で聞こえる音になり、その次で消えること、聞く人に届く音のうち大きく聞こえるものを選ぶことを確かめる
    public sealed class ServerNoisesTests
    {
        private static readonly Func<int, bool> _everyoneHostile = _ => true;

        [Test]
        public void ANoiseIsHeardOnlyAfterTheNextAdvanceAndOnlyForOneTick()
        {
            var noises = new ServerNoises();
            noises.Emit(1, Clatter(Vector3.Zero, 10f));

            Assert.That(noises.TryHear(Vector3.Zero, _everyoneHostile, out _), Is.False, "not heard before the bots think again");
            Assert.That(noises.PendingCount, Is.EqualTo(1));

            noises.Advance();
            Assert.That(noises.Current.Count, Is.EqualTo(1));
            Assert.That(noises.PendingCount, Is.EqualTo(0));
            Assert.That(noises.TryHear(new Vector3(3f, 0f, 0f), _everyoneHostile, out var heard), Is.True);
            Assert.That(heard.SourceId, Is.EqualTo(1));
            Assert.That(heard.Sound.Kind, Is.EqualTo(SoundCueKind.Clatter));

            noises.Advance();
            Assert.That(noises.Current.Count, Is.EqualTo(0));
            Assert.That(noises.TryHear(Vector3.Zero, _everyoneHostile, out _), Is.False);
        }

        [Test]
        public void ANoiseWithoutARadiusIsNotKept()
        {
            var noises = new ServerNoises();
            noises.Emit(1, Clatter(Vector3.Zero, 0f));

            Assert.That(noises.PendingCount, Is.EqualTo(0));
        }

        [Test]
        public void ANoiseBeyondItsRadiusIsNotHeard()
        {
            var noises = Advanced(new ServerNoises.Noise(1, Clatter(Vector3.Zero, 10f)));

            Assert.That(noises.TryHear(new Vector3(10.5f, 0f, 0f), _everyoneHostile, out _), Is.False);
            Assert.That(noises.TryHear(new Vector3(9.5f, 0f, 0f), _everyoneHostile, out _), Is.True);
        }

        [Test]
        public void TheLoudestNoiseIsHeard()
        {
            // 聞く人は原点。蹴り (半径 25 の 10 m 先) は 0.6、爆薬 (半径 60 の 20 m 先) は 0.67 の大きさに聞こえる
            var noises = Advanced(
                new ServerNoises.Noise(1, Clatter(new Vector3(10f, 0f, 0f), 25f)),
                new ServerNoises.Noise(2, new SoundStimulus(SoundCueKind.Explosion, new Vector3(0f, 0f, 20f), 60f, false)));

            Assert.That(noises.TryHear(Vector3.Zero, _everyoneHostile, out var heard), Is.True);
            Assert.That(heard.SourceId, Is.EqualTo(2));
            Assert.That(heard.Sound.Kind, Is.EqualTo(SoundCueKind.Explosion));
        }

        [Test]
        public void ANoiseOfSomeoneNotHostileIsIgnoredButAnUnknownSourceIsHeard()
        {
            var friendly = Advanced(new ServerNoises.Noise(1, Clatter(Vector3.Zero, 10f)));
            Assert.That(friendly.TryHear(Vector3.Zero, id => id != 1, out _), Is.False);

            var unknown = Advanced(new ServerNoises.Noise(PlayerIds.None, Clatter(Vector3.Zero, 10f)));
            Assert.That(unknown.TryHear(Vector3.Zero, _ => false, out var heard), Is.True);
            Assert.That(heard.SourceId, Is.EqualTo(PlayerIds.None));
        }

        [Test]
        public void PendingNoisesStopAtTheCapAndKeepTheNewest()
        {
            var noises = new ServerNoises();
            for (var i = 0; i < ServerNoises.MaxPending + 3; i++)
            {
                noises.Emit(i, Clatter(Vector3.Zero, 10f));
            }

            Assert.That(noises.PendingCount, Is.EqualTo(ServerNoises.MaxPending));
            noises.Advance();
            Assert.That(noises.Current[0].SourceId, Is.EqualTo(3));
            Assert.That(noises.Current[ServerNoises.MaxPending - 1].SourceId, Is.EqualTo(ServerNoises.MaxPending + 2));
        }

        private static SoundStimulus Clatter(Vector3 at, float radius)
        {
            return new SoundStimulus(SoundCueKind.Clatter, at, radius, false);
        }

        private static ServerNoises Advanced(params ServerNoises.Noise[] emitted)
        {
            var noises = new ServerNoises();
            foreach (var noise in emitted)
            {
                noises.Emit(noise.SourceId, noise.Sound);
            }

            noises.Advance();
            return noises;
        }
    }
}

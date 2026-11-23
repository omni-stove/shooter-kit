using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class HolsterNetLogTests
    {
        [Test]
        public void TheNetLinesNameTheDirectionAndTheReason()
        {
            Assert.That(HolsterLog.NetRequested(HolsterPhase.Holstering), Is.EqualTo("[Holster] net requested=Holstering"));
            Assert.That(HolsterLog.NetRequested(HolsterPhase.Drawing), Is.EqualTo("[Holster] net requested=Drawing"));
            Assert.That(HolsterLog.NetNotSent(HolsterPhase.Holstering, "Riding"), Is.EqualTo("[Holster] net not-sent=Holstering reason=Riding"));
            Assert.That(HolsterLog.NetNotSent(HolsterPhase.Drawing, null), Is.EqualTo("[Holster] net not-sent=Drawing reason=-"));
            Assert.That(
                HolsterLog.NetChanged(HolsterPhase.Holstering, HolsterPhase.Holstered), Is.EqualTo("[Holster] net phase=Holstering->Holstered"));
            Assert.That(HolsterLog.NoInput("Player/Holster"), Is.EqualTo("[Holster] input action 'Player/Holster' is missing; the holster key is not read"));
        }
    }
}

using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Server.Tests
{
    // 何日も動かしたサーバーの時刻から始めても、ルール・乗り物・置ける物のモジュールの時計が 1 tick ずつ進むことを確かめる
    // (float の時計は 524288 秒を超えると 1/60 秒を足しても変わらなかった)
    public sealed partial class ServerDeployablesTests
    {
        private const double _longUptime = 1_000_000d;

        [Test]
        public void TheRulesVehiclesAndDeployablesClocksKeepEveryTickAfterALongUptime()
        {
            Start(Explosives(), vehicles: new NetMapVehicles(new[] { Car() }));
            Join(1);
            Join(2);
            Step(0.2f);
            _rules.ClockSeconds = _longUptime;
            _vehicles.ClockSeconds = _longUptime;
            _deployables.ClockSeconds = _longUptime;

            Step(_tickSeconds);

            var oneTick = _longUptime + _tickSeconds;
            Assert.That(_rules.ClockSeconds, Is.EqualTo(oneTick).Within(1e-9d), _log.ToString());
            Assert.That(_vehicles.ClockSeconds, Is.EqualTo(oneTick).Within(1e-9d));
            Assert.That(_deployables.ClockSeconds, Is.EqualTo(oneTick).Within(1e-9d));

            for (var i = 0; i < 59; i++)
            {
                Step(_tickSeconds);
            }

            Assert.That(_rules.ClockSeconds, Is.EqualTo(_longUptime + 1d).Within(1e-6d), "sixty ticks are one second");
            Assert.That(_vehicles.ClockSeconds, Is.EqualTo(_longUptime + 1d).Within(1e-6d));
            Assert.That(_deployables.ClockSeconds, Is.EqualTo(_longUptime + 1d).Within(1e-6d));
        }
    }
}

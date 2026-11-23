using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Server.Tests
{
    // 何日も動かしたサーバーの時刻 (float の刻みが 1 tick より粗くなる大きさ) から始めても、モジュールの時計が 1 tick ずつ進むことを確かめる。
    // 始めていないモジュールの Tick は時計を進めるだけなので、ホストを立てずに呼ぶ
    public sealed class ServerModuleClockTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;

        // 約 11.6 日。float では刻みが 0.0625 秒になり、1/60 秒を足しても変わらない
        private const double _longUptime = 1_000_000d;

        private MatchRulesModule _rules;
        private ServerMovement _movement;
        private ServerCombat _combat;
        private ServerInventories _inventories;

        [SetUp]
        public void SetUp()
        {
            _rules = new MatchRulesModule();
            _movement = new ServerMovement(ServerMotorSettings.Default);
            _combat = new ServerCombat(_rules, _movement);
            _inventories = new ServerInventories(_rules, _combat);
        }

        [Test]
        public void TheMovementClockKeepsEveryTickAfterALongUptime()
        {
            _movement.ClockSeconds = _longUptime;

            _movement.Tick(1, _tickSeconds);
            Assert.That(_movement.ClockSeconds, Is.EqualTo(_longUptime + _tickSeconds).Within(1e-9d));

            Advance(_movement, 59);
            Assert.That(_movement.ClockSeconds, Is.EqualTo(_longUptime + 1d).Within(1e-6d), "sixty ticks are one second");
        }

        [Test]
        public void TheInventoriesClockKeepsEveryTickAfterALongUptime()
        {
            _inventories.ClockSeconds = _longUptime;

            _inventories.Tick(1, _tickSeconds);
            Assert.That(_inventories.ClockSeconds, Is.EqualTo(_longUptime + _tickSeconds).Within(1e-9d));

            Advance(_inventories, 59);
            Assert.That(_inventories.ClockSeconds, Is.EqualTo(_longUptime + 1d).Within(1e-6d));
        }

        [Test]
        public void TheLootClockKeepsEveryTickAfterALongUptime()
        {
            var loot = new ServerLoot(_rules, _combat, _inventories, 1);
            loot.ClockSeconds = _longUptime;

            loot.Tick(1, _tickSeconds);
            Assert.That(loot.ClockSeconds, Is.EqualTo(_longUptime + _tickSeconds).Within(1e-9d));

            Advance(loot, 59);
            Assert.That(loot.ClockSeconds, Is.EqualTo(_longUptime + 1d).Within(1e-6d));
        }

        [Test]
        public void ThePickupsClockKeepsEveryTickAfterALongUptime()
        {
            var pickups = new ServerPickups(_rules, _combat, _inventories);
            pickups.ClockSeconds = _longUptime;

            pickups.Tick(1, _tickSeconds);
            Assert.That(pickups.ClockSeconds, Is.EqualTo(_longUptime + _tickSeconds).Within(1e-9d));

            Advance(pickups, 59);
            Assert.That(pickups.ClockSeconds, Is.EqualTo(_longUptime + 1d).Within(1e-6d));
        }

        [Test]
        public void TheStreaksClockKeepsEveryTickAfterALongUptime()
        {
            var streaks = new ServerStreaks(_rules, _combat);
            streaks.ClockSeconds = _longUptime;

            streaks.Tick(1, _tickSeconds);
            Assert.That(streaks.ClockSeconds, Is.EqualTo(_longUptime + _tickSeconds).Within(1e-9d));

            Advance(streaks, 59);
            Assert.That(streaks.ClockSeconds, Is.EqualTo(_longUptime + 1d).Within(1e-6d));
        }

        private static void Advance(IServerModule module, int ticks)
        {
            for (var i = 0; i < ticks; i++)
            {
                module.Tick(2 + i, _tickSeconds);
            }
        }
    }
}

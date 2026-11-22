using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // 床の上面は y = 0.5 で、立った体の中心は 1.5。10 の高さからは 8.5 落ち、着地の速さは約 18.4 (安全な 12 と死ぬ 25 の間)。
    public sealed class ServerLandingRollTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private const float _floorTop = 0.5f;
        private const float _dropY = 10f;
        private const int _maxTicks = 120;

        // 立った高さの 1 上。落ちる速さで 0.1 秒ほどで着地するので、既定の窓 0.3 秒の中に入る
        private const float _pressBelowY = _floorTop + 1f + 1f;

        private static readonly DateTime _time = new DateTime(2026, 11, 22, 9, 0, 0, DateTimeKind.Utc);
        private static readonly Vector3 _spawn = new Vector3(0f, 1.7f, 0f);
        private static readonly ConnectionId _connection = new ConnectionId(1);
        private static readonly ParkourTuning _rollOn = new ParkourTuning(
            WallKickTuning.Default, new LandingRollTuning(true), WallClimbTuning.Default, LedgeHangTuning.Default);

        private readonly MessageTable _clientTable = MatchMessages.CreateClientTable();

        private StringWriter _log;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerHost _host;
        private ServerMovement _movement;
        private int _nextInputTick;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _nextInputTick = 0;
        }

        [TearDown]
        public void TearDown()
        {
            _transport?.Dispose();
            _log.Dispose();
        }

        [Test]
        public void RollingOnLandingHalvesTheFallSpeedAndTheFallDamageGoesDown()
        {
            Start(_rollOn);
            var plain = DropAndLand(pressCrouch: false);
            var rolled = DropAndLand(pressCrouch: true);

            var expected = MathF.Sqrt(2f * MotorSpec.DefaultGravity * (_dropY - (_floorTop + 1f)));
            Assert.That(plain, Is.EqualTo(expected).Within(0.5f), "not pressing crouch counts the whole fall");
            Assert.That(rolled, Is.EqualTo(expected * LandingRollTuning.DefaultFallSpeedScale).Within(0.5f));

            var plainDamage = _movement.FallDamage(plain, 100);
            var rolledDamage = _movement.FallDamage(rolled, 100);
            Assert.That(plainDamage, Is.GreaterThan(0), "18.4 is over the safe 12");
            Assert.That(rolledDamage, Is.Zero, "9.2 is under the safe 12");
            Assert.That(rolledDamage, Is.LessThan(plainDamage));
        }

        [Test]
        public void TheRollIsKeptInTheStateAndSentInTheSnapshot()
        {
            Start(_rollOn);
            DropAndLand(pressCrouch: true);

            _movement.TryGetState(1, out var landed);
            Assert.That(landed.Parkour.Move, Is.EqualTo(Shooter.Core.ParkourMove.LandingRoll));
            Assert.That(landed.Parkour.FallSpeedScale, Is.EqualTo(LandingRollTuning.DefaultFallSpeedScale));

            Send(Vector2.Zero, InputButtons.None);
            _movement.TryGetState(1, out var rolling);
            Assert.That(rolling.Parkour.Move, Is.EqualTo(Shooter.Core.ParkourMove.LandingRoll), "the roll goes on without holding crouch");
            Assert.That(rolling.Position.Z, Is.GreaterThan(landed.Position.Z), "it rolls forward");

            _transport.Sent.Clear();
            _host.Tick(_tickSeconds);
            Assert.That(OnlySnapshot().Players[0].ParkourMove, Is.EqualTo(Shooter.Protocol.ParkourMove.LandingRoll));
        }

        [Test]
        public void TheRollEndsAfterTheTunedSeconds()
        {
            Start(_rollOn);
            DropAndLand(pressCrouch: true);

            var rollingTicks = 1;
            for (var i = 0; i < _maxTicks; i++)
            {
                Send(new Vector2(0f, 1f), InputButtons.None);
                _movement.TryGetState(1, out var state);
                if (state.Parkour.Move != Shooter.Core.ParkourMove.LandingRoll)
                {
                    Assert.That(state.Parkour, Is.EqualTo(CharacterParkourState.None));
                    Assert.That(state.Parkour.FallSpeedScale, Is.EqualTo(1f));
                    break;
                }

                rollingTicks++;
            }

            Assert.That(rollingTicks * _tickSeconds, Is.EqualTo(LandingRollTuning.DefaultRollSeconds).Within(_tickSeconds * 1.5f));
        }

        [Test]
        public void WithTheDefaultTuningCrouchingBeforeLandingDoesNotSoftenIt()
        {
            Start(ParkourTuning.Default);
            var plain = DropAndLand(pressCrouch: false);
            var crouched = DropAndLand(pressCrouch: true);

            _movement.TryGetState(1, out var state);
            Assert.That(state.Parkour, Is.EqualTo(CharacterParkourState.None));
            Assert.That(crouched, Is.EqualTo(plain).Within(0.5f));
        }

        // 前へ歩きながら落とし、着地を知らせた速さを返す。pressCrouch のときは着地の少し前からしゃがみを押す
        private float DropAndLand(bool pressCrouch)
        {
            var landings = new List<float>();
            Action<ServerPlayer, int, float> record = (player, tick, speed) => landings.Add(speed);
            _movement.Landed += record;
            try
            {
                Assert.That(_movement.Teleport(1, new Vector3(0f, _dropY, 0f)), Is.True);
                for (var i = 0; i < _maxTicks && landings.Count == 0; i++)
                {
                    _movement.TryGetState(1, out var state);
                    var crouch = pressCrouch && state.Position.Y < _pressBelowY;
                    Send(new Vector2(0f, 1f), crouch ? InputButtons.Crouch : InputButtons.None);
                }
            }
            finally
            {
                _movement.Landed -= record;
            }

            Assert.That(landings, Has.Count.EqualTo(1));
            return landings[0];
        }

        private void Start(ParkourTuning parkour)
        {
            _transport?.Dispose();
            _transport = new RecordingTransport();
            _match = new ServerMatch(5f, _log, new ServerEventWriter(false, _log, _log), () => _time);
            var defaults = ServerMotorSettings.Default;
            var settings = new ServerMotorSettings(
                defaults.Motor,
                defaults.Stance,
                defaults.Mover,
                defaults.Prone,
                defaults.Swim,
                defaults.WallRun,
                defaults.Parachute,
                defaults.Slide,
                defaults.Zipline,
                parkour,
                GameRules.Default.WithFallDamage(FallDamage.On),
                defaults.KillPlaneY,
                _spawn);
            _movement = new ServerMovement(settings, signals: new ServerCheatSignals());
            var snapshots = new ServerSnapshots(settings.Rules, new ISnapshotContributor[] { _movement }, TestAntiCheat.Off);
            _host = new ServerHost();
            _host.Register(_movement);
            _host.Register(snapshots);
            var options = new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort);
            var map = new ServerMapData("NetTest", TestMaps.World(), TestMaps.NavMesh(), "memory");
            var context = new ServerContext(options, "0.2.0", _log, map, _match, new ServerMessenger(_transport), new StubControl());
            _host.Start(context);
            _match.Join(_connection, "key");
            _host.OnConnected(_connection);
            _nextInputTick = 0;
        }

        // 1 tick ずつ送り、そのたびに 1 tick 進めて入力の予算を戻す (クライアントが tick の速さで送るのと同じ)
        private void Send(Vector2 move, InputButtons buttons)
        {
            var message = new InputMessage { AckedSnapshotTick = -1 };
            message.Add(new InputFrame(_nextInputTick, move, 0f, buttons));
            _movement.OnMessage(_connection, message);
            _nextInputTick++;
            _movement.Tick(_nextInputTick, _tickSeconds);
        }

        private SnapshotMessage OnlySnapshot()
        {
            Assert.That(_transport.Sent, Has.Count.EqualTo(1));
            var snapshot = (SnapshotMessage)_clientTable.Read(new ArraySegment<byte>(_transport.Sent[0].Payload));
            Assert.That(snapshot.Players.Count, Is.EqualTo(1));
            return snapshot;
        }
    }
}

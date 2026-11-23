using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.NetV2;
using Shooter.Protocol;
using Shooter.Runtime;

namespace Shooter.Net.Tests
{
    // しまう・構えるの押しを向きのある要求にして送り、サーバーが配る自分の状態を GameSignals.Holster に出すことを確かめる
    public sealed class NetV2HolsterPartTests
    {
        // 承認される前の MatchClient の自分の Id
        private const int _localId = 0;

        private readonly List<string> _logs = new List<string>();
        private readonly List<HolsterAction> _sent = new List<HolsterAction>();
        private MatchClient _client;
        private NetV2HolsterPart _part;
        private bool _holsterPressed;
        private bool _firePressed;
        private bool _sendResult;

        [SetUp]
        public void SetUp()
        {
            GameSignals.Holster.Clear();
            _logs.Clear();
            _sent.Clear();
            _holsterPressed = false;
            _firePressed = false;
            _sendResult = true;
            _client = new MatchClient(new SilentTransport(), "0.2.0", RejoinKey.NewDeviceKey(), "tester", _logs.Add);
            _part = new NetV2HolsterPart(_client, Send, HolsterSpec.Default, () => _holsterPressed, () => _firePressed, _logs.Add);
        }

        [TearDown]
        public void TearDown()
        {
            _part.Dispose();
            _client.Dispose();
            GameSignals.Holster.Clear();
        }

        [Test]
        public void PressingTheKeyWhileDrawnAsksToHolster()
        {
            _part.Apply(Snapshot(HolsterPhase.Drawn, 0f));

            Press();

            Assert.That(_sent, Is.EqualTo(new[] { HolsterAction.Holster }));
            Assert.That(_part.IsPending, Is.True);
            Assert.That(_logs, Has.Some.EqualTo("[Holster] net requested=Holstering"));
        }

        [Test]
        public void PressingAgainBeforeTheServerAnswersAsksToDraw()
        {
            _part.Apply(Snapshot(HolsterPhase.Drawn, 0f));
            Press();

            // サーバーがまだ受けていないスナップショットが続けて届いても、送った向きを覚えている
            _part.Apply(Snapshot(HolsterPhase.Drawn, 0f));
            Press();

            Assert.That(_sent, Is.EqualTo(new[] { HolsterAction.Holster, HolsterAction.Draw }));
        }

        [TestCase(HolsterPhase.Holstering, 0.5f)]
        [TestCase(HolsterPhase.Holstered, 1f)]
        public void PressingTheKeyWhileLoweredAsksToDraw(HolsterPhase phase, float lowering)
        {
            _part.Apply(Snapshot(phase, lowering));

            Press();

            Assert.That(_sent, Is.EqualTo(new[] { HolsterAction.Draw }));
            Assert.That(_logs, Has.Some.EqualTo("[Holster] net requested=Drawing"));
        }

        [Test]
        public void PressingTheKeyWhileDrawingAsksToHolsterAgain()
        {
            _part.Apply(Snapshot(HolsterPhase.Drawing, 0.5f));

            Press();

            Assert.That(_sent, Is.EqualTo(new[] { HolsterAction.Holster }));
        }

        [Test]
        public void FiringWhileHolsteredOnlyDraws()
        {
            _part.Apply(Snapshot(HolsterPhase.Holstered, 1f));
            _firePressed = true;

            _part.Tick(0.016f);

            Assert.That(_sent, Is.EqualTo(new[] { HolsterAction.Draw }));
        }

        [Test]
        public void FiringWhileDrawnSendsNothing()
        {
            _part.Apply(Snapshot(HolsterPhase.Drawn, 0f));
            _firePressed = true;

            _part.Tick(0.016f);

            Assert.That(_sent, Is.Empty);
        }

        [Test]
        public void TheServerPhaseEndsTheWaitAndIsFollowed()
        {
            _part.Apply(Snapshot(HolsterPhase.Drawn, 0f));
            Press();

            _part.Apply(Snapshot(HolsterPhase.Holstering, 0.2f));

            Assert.That(_part.IsPending, Is.False);
            Assert.That(_part.Phase, Is.EqualTo(HolsterPhase.Holstering));
            Assert.That(_logs, Has.Some.EqualTo("[Holster] net phase=Drawn->Holstering"));

            Press();
            Assert.That(_sent, Is.EqualTo(new[] { HolsterAction.Holster, HolsterAction.Draw }));
        }

        // 断られた要求の向きを覚え続けると、次の押しが逆向きになり続けるので、待つ時間を過ぎたらサーバーの値に戻す
        [Test]
        public void AnUnansweredRequestStopsBeingWaitedForAfterTheTimeout()
        {
            _part.Apply(Snapshot(HolsterPhase.Drawn, 0f));
            Press();

            _part.Tick(NetV2HolsterPart.PendingTimeoutSeconds + 0.1f);
            Press();

            Assert.That(_part.IsPending, Is.True, "the second press is waited for again");
            Assert.That(_sent, Is.EqualTo(new[] { HolsterAction.Holster, HolsterAction.Holster }));
        }

        [Test]
        public void TheSnapshotStateIsPublishedWithWhetherItCountsAsArmed()
        {
            _part.Apply(Snapshot(HolsterPhase.Holstering, 0.25f));

            Assert.That(GameSignals.Holster.Value, Is.EqualTo(new HolsterStatus(HolsterPhase.Holstering, 0.25f, false)));
            Assert.That(_part.Lowering, Is.EqualTo(0.25f));

            _part.Apply(Snapshot(HolsterPhase.Drawing, 0.75f));

            Assert.That(GameSignals.Holster.Value, Is.EqualTo(new HolsterStatus(HolsterPhase.Drawing, 0.75f, true)));
        }

        [Test]
        public void TheArmedPhasesComeFromTheSpec()
        {
            _part.Dispose();
            var everyPhase = new HolsterSpec(armedPhases: new[] { HolsterPhase.Drawn, HolsterPhase.Holstering, HolsterPhase.Holstered, HolsterPhase.Drawing });
            _part = new NetV2HolsterPart(_client, Send, everyPhase, () => _holsterPressed, () => _firePressed, _logs.Add);

            _part.Apply(Snapshot(HolsterPhase.Holstered, 1f));

            Assert.That(GameSignals.Holster.Value.Value.IsArmed, Is.True);
        }

        [Test]
        public void WithoutTheOwnRowTheSignalIsClearedAndNothingIsSent()
        {
            _part.Apply(Snapshot(HolsterPhase.Holstered, 1f));

            var empty = new SnapshotMessage();
            empty.Match.State = GameModeState.Playing;
            _part.Apply(empty);
            Press();

            Assert.That(GameSignals.Holster.HasValue, Is.False);
            Assert.That(_part.CanAct, Is.False);
            Assert.That(_sent, Is.Empty);
        }

        [Test]
        public void ADeadOrDownedPlayerSendsNothing()
        {
            _part.Apply(Snapshot(HolsterPhase.Drawn, 0f, health: 0));
            Press();
            _part.Apply(Snapshot(HolsterPhase.Drawn, 0f, downed: true));
            Press();

            Assert.That(_sent, Is.Empty);
        }

        [Test]
        public void OutsideAPlayingMatchNothingIsSent()
        {
            var snapshot = Snapshot(HolsterPhase.Drawn, 0f);
            snapshot.Match.State = GameModeState.Waiting;
            _part.Apply(snapshot);

            Press();

            Assert.That(_sent, Is.Empty);
        }

        [Test]
        public void ARiderDoesNotHolsterButCanDraw()
        {
            var riding = Snapshot(HolsterPhase.Drawn, 0f);
            riding.Players[0].Vehicle = new VehicleSeatSync(2, 0);
            _part.Apply(riding);
            Press();

            Assert.That(_sent, Is.Empty);
            Assert.That(_logs, Has.Some.EqualTo("[Holster] net not-sent=Holstering reason=Riding"));

            var holstered = Snapshot(HolsterPhase.Holstered, 1f);
            holstered.Players[0].Vehicle = new VehicleSeatSync(2, 0);
            _part.Apply(holstered);
            Press();

            Assert.That(_sent, Is.EqualTo(new[] { HolsterAction.Draw }));
        }

        [Test]
        public void HolsteringDuringAWeaponSwitchIsNotSent()
        {
            var switching = Snapshot(HolsterPhase.Drawn, 0f);
            switching.Owner.Weapon = new WeaponSync { WeaponCount = 2, IsSwitching = true };
            _part.Apply(switching);

            Press();

            Assert.That(_sent, Is.Empty);
            Assert.That(_logs, Has.Some.EqualTo("[Holster] net not-sent=Holstering reason=Switching"));
        }

        [Test]
        public void ARequestThatCannotBeSentIsNotWaitedFor()
        {
            _sendResult = false;
            _part.Apply(Snapshot(HolsterPhase.Drawn, 0f));

            Press();

            Assert.That(_part.IsPending, Is.False);
            Assert.That(_logs, Has.Some.EqualTo("[Holster] net not-sent=Holstering reason=NotConnected"));
        }

        [Test]
        public void DisposingClearsTheSignalAndStopsReading()
        {
            _part.Apply(Snapshot(HolsterPhase.Holstered, 1f));

            _part.Dispose();
            Press();
            _part.Apply(Snapshot(HolsterPhase.Holstering, 0.5f));

            Assert.That(GameSignals.Holster.HasValue, Is.False);
            Assert.That(_sent, Is.Empty);
        }

        [Test]
        public void TheMessagesFromTheServerAreLeftToOtherParts()
        {
            Assert.That(_part.Handle(new EmoteStateMessage { PlayerId = 1, Playing = false }), Is.False);
        }

        [Test]
        public void TheKeyIsTheHolsterActionAndTheFireIsTheAttackAction()
        {
            Assert.That(NetV2HolsterPart.HolsterActionPath, Is.EqualTo("Player/Holster"));
            Assert.That(NetV2HolsterPart.FireActionPath, Is.EqualTo("Player/Attack"));
        }

        [Test]
        public void ThePartNeedsTheClientAndTheSender()
        {
            Assert.Throws<ArgumentNullException>(() => new NetV2HolsterPart(null, Send));
            Assert.Throws<ArgumentNullException>(() => new NetV2HolsterPart(_client, null));
            Assert.Throws<ArgumentNullException>(() => NetV2HolsterPart.Create(null));
        }

        private void Press()
        {
            _holsterPressed = true;
            _part.Tick(0.016f);
            _holsterPressed = false;
        }

        private static SnapshotMessage Snapshot(HolsterPhase phase, float lowering, int health = 100, bool downed = false)
        {
            var snapshot = new SnapshotMessage { HasOwner = true };
            snapshot.Match.State = GameModeState.Playing;
            ref var own = ref snapshot.Players.Add();
            own.PlayerId = _localId;
            own.Team = Team.Alpha;
            own.Health = health;
            own.Downed = new DownedSync(downed, downed ? 10 : 0, 0);
            own.WeaponId = "rifle";
            own.HolsterPhase = phase;
            own.HolsterLowering = lowering;
            return snapshot;
        }

        private bool Send(IMessage message)
        {
            _sent.Add(((HolsterRequestMessage)message).Action);
            return _sendResult;
        }

        private sealed class SilentTransport : IMatchTransport
        {
            public bool IsActive => false;

            public void Listen(string address, ushort port)
            {
                throw new InvalidOperationException("a client transport does not listen");
            }

            public void Connect(string address, ushort port)
            {
            }

            public void Disconnect(ConnectionId connection, string reason)
            {
            }

            public void Send(ConnectionId connection, TransportChannel channel, ArraySegment<byte> payload)
            {
            }

            public int GetMaxPayloadSize(TransportChannel channel)
            {
                return MessageWriter.DefaultMaxLength;
            }

            public bool TryGetRoundTripTime(ConnectionId connection, out int milliseconds)
            {
                milliseconds = 0;
                return false;
            }

            public bool TryReceive(out TransportEvent transportEvent)
            {
                transportEvent = default;
                return false;
            }

            public void Shutdown(string reason)
            {
            }

            public void Dispose()
            {
            }
        }
    }
}

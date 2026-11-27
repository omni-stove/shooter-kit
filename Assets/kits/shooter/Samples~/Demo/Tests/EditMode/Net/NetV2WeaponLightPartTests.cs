using System;
using System.Collections.Generic;
using GameKit.Net;
using NUnit.Framework;
using Shooter.Core;
using Shooter.NetV2;
using Shooter.Protocol;
using Shooter.Runtime;

namespace Shooter.Net.Tests
{
    // ウェポンライトの押しを点けたい向きの要求にして送り、サーバーが配る自分の点灯を GameSignals.WeaponLight に出すことを確かめる
    public sealed class NetV2WeaponLightPartTests
    {
        // 承認される前の MatchClient の自分の Id
        private const int _localId = 0;
        private const string _rifleId = "rifle";
        private const string _pistolId = "pistol";
        private const string _rifleItemId = "6c2d4e5f-7a8b-4c9d-8e0f-1a2b3c4d5e6f";
        private const string _pistolItemId = "7d3e5f6a-8b9c-4d0e-9f1a-2b3c4d5e6f7a";
        private const string _lightItemId = "8e4f6a7b-9c0d-4e1f-8a2b-3c4d5e6f7a8b";

        private static readonly string[] _light = { _lightItemId };

        private readonly List<string> _logs = new List<string>();
        private readonly List<bool> _sent = new List<bool>();
        private readonly Dictionary<string, IReadOnlyList<string>> _attached = new Dictionary<string, IReadOnlyList<string>>();
        private MatchClient _client;
        private NetV2WeaponLightPart _part;
        private bool _lightPressed;
        private bool _sendResult;

        [SetUp]
        public void SetUp()
        {
            GameSignals.WeaponLight.Clear();
            _logs.Clear();
            _sent.Clear();
            _attached.Clear();
            _attached[_rifleId] = _light;
            _lightPressed = false;
            _sendResult = true;
            _client = new MatchClient(new SilentTransport(), "0.2.0", RejoinKey.NewDeviceKey(), "tester", _logs.Add);
            _part = new NetV2WeaponLightPart(_client, Send, AttachmentsOf, Items(), HolsterSpec.Default, () => _lightPressed, _logs.Add);
        }

        [TearDown]
        public void TearDown()
        {
            _part.Dispose();
            _client.Dispose();
            GameSignals.WeaponLight.Clear();
        }

        [Test]
        public void PressingTheKeyWithALightAsksToTurnItOn()
        {
            _part.Apply(Snapshot());

            Press();

            Assert.That(_sent, Is.EqualTo(new[] { true }));
            Assert.That(_part.IsPending, Is.True);
            Assert.That(_logs, Has.Some.EqualTo("[Combat] weaponLight net requested on=true"));
        }

        [Test]
        public void PressingAgainBeforeTheServerAnswersAsksToTurnItOff()
        {
            _part.Apply(Snapshot());
            Press();

            // サーバーがまだ受けていないスナップショットが続けて届いても、送った向きを覚えている
            _part.Apply(Snapshot());
            Press();

            Assert.That(_sent, Is.EqualTo(new[] { true, false }));
        }

        [Test]
        public void PressingTheKeyWhileLitAsksToTurnItOff()
        {
            _part.Apply(Snapshot(lit: true));

            Press();

            Assert.That(_sent, Is.EqualTo(new[] { false }));
            Assert.That(_logs, Has.Some.EqualTo("[Combat] weaponLight net requested on=false"));
        }

        [Test]
        public void AWeaponWithoutALightSendsNothing()
        {
            _part.Apply(Snapshot(weaponId: _pistolId));

            Press();

            Assert.That(_sent, Is.Empty);
            Assert.That(_part.HasLight, Is.False);
            Assert.That(_logs, Has.Some.EqualTo("[Combat] weaponLight net not-sent on=true reason=no-light"));
        }

        [Test]
        public void TakingTheLightOffTheWeaponSendsNothing()
        {
            _attached.Clear();
            _part.Apply(Snapshot());

            Press();

            Assert.That(_sent, Is.Empty);
            Assert.That(_logs, Has.Some.EqualTo("[Combat] weaponLight net not-sent on=true reason=no-light"));
        }

        [TestCase(HolsterPhase.Holstering, 0.5f)]
        [TestCase(HolsterPhase.Holstered, 1f)]
        public void AHolsteredWeaponSendsNothing(HolsterPhase phase, float lowering)
        {
            var snapshot = Snapshot();
            snapshot.Players[0].HolsterPhase = phase;
            snapshot.Players[0].HolsterLowering = lowering;
            _part.Apply(snapshot);

            Press();

            Assert.That(_sent, Is.Empty);
            Assert.That(_logs, Has.Some.EqualTo("[Combat] weaponLight net not-sent on=true reason=holstered"));
        }

        [Test]
        public void NoWeaponSendsNothing()
        {
            _part.Apply(Snapshot(weaponId: ""));

            Press();

            Assert.That(_sent, Is.Empty);
            Assert.That(_logs, Has.Some.EqualTo("[Combat] weaponLight net not-sent on=true reason=unarmed"));
        }

        [Test]
        public void TheServerStateEndsTheWaitAndIsFollowed()
        {
            _part.Apply(Snapshot());
            Press();

            _part.Apply(Snapshot(lit: true));

            Assert.That(_part.IsPending, Is.False);
            Assert.That(_part.IsOn, Is.True);
            Assert.That(_logs, Has.Some.EqualTo("[Combat] weaponLight net on=false->true"));

            Press();
            Assert.That(_sent, Is.EqualTo(new[] { true, false }));
        }

        // 断られた要求の向きを覚え続けると、次の押しが逆向きになり続けるので、待つ時間を過ぎたらサーバーの値に戻す
        [Test]
        public void AnUnansweredRequestStopsBeingWaitedForAfterTheTimeout()
        {
            _part.Apply(Snapshot());
            Press();

            _part.Tick(NetV2WeaponLightPart.PendingTimeoutSeconds + 0.1f);
            Press();

            Assert.That(_part.IsPending, Is.True, "the second press is waited for again");
            Assert.That(_sent, Is.EqualTo(new[] { true, true }));
        }

        [Test]
        public void TheSnapshotStateIsPublishedWithWhetherTheWeaponHasALight()
        {
            _part.Apply(Snapshot());

            Assert.That(GameSignals.WeaponLight.Value, Is.EqualTo((WeaponLightStatus?)new WeaponLightStatus(true, false)));

            _part.Apply(Snapshot(lit: true));

            Assert.That(GameSignals.WeaponLight.Value, Is.EqualTo((WeaponLightStatus?)new WeaponLightStatus(true, true)));

            _part.Apply(Snapshot(weaponId: _pistolId));

            Assert.That(GameSignals.WeaponLight.Value, Is.EqualTo((WeaponLightStatus?)WeaponLightStatus.None));
        }

        // 付けた物の知らせより先にスナップショットが届いても、サーバーが点けているならライトがあると出す
        [Test]
        public void ALightTheServerTurnedOnCountsAsALightBeforeTheAttachmentsArrive()
        {
            _attached.Clear();

            _part.Apply(Snapshot(lit: true));

            Assert.That(GameSignals.WeaponLight.Value, Is.EqualTo((WeaponLightStatus?)new WeaponLightStatus(true, true)));
        }

        [Test]
        public void WithoutTheOwnRowTheSignalIsClearedAndNothingIsSent()
        {
            _part.Apply(Snapshot(lit: true));

            var empty = new SnapshotMessage();
            empty.Match.State = GameModeState.Playing;
            _part.Apply(empty);
            Press();

            Assert.That(GameSignals.WeaponLight.HasValue, Is.False);
            Assert.That(_part.CanAct, Is.False);
            Assert.That(_sent, Is.Empty);
        }

        [Test]
        public void ADeadOrDownedPlayerSendsNothing()
        {
            _part.Apply(Snapshot(health: 0));
            Press();
            _part.Apply(Snapshot(downed: true));
            Press();

            Assert.That(_sent, Is.Empty);
        }

        [Test]
        public void OutsideAPlayingMatchNothingIsSent()
        {
            var snapshot = Snapshot();
            snapshot.Match.State = GameModeState.Waiting;
            _part.Apply(snapshot);

            Press();

            Assert.That(_sent, Is.Empty);
        }

        // holster.json の既定では、乗り物が手を預かっている間は武装と数えない
        [Test]
        public void ARiderDoesNotTurnTheLightOn()
        {
            var riding = Snapshot();
            riding.Players[0].Vehicle = new VehicleSeatSync(2, 0);
            _part.Apply(riding);

            Press();

            Assert.That(_sent, Is.Empty);
            Assert.That(_logs, Has.Some.EqualTo("[Combat] weaponLight net not-sent on=true reason=holstered"));
        }

        [Test]
        public void ARequestThatCannotBeSentIsNotWaitedFor()
        {
            _sendResult = false;
            _part.Apply(Snapshot());

            Press();

            Assert.That(_part.IsPending, Is.False);
            Assert.That(_logs, Has.Some.EqualTo("[Combat] weaponLight net not-sent on=true reason=not-connected"));
        }

        [Test]
        public void DisposingClearsTheSignalAndStopsReading()
        {
            _part.Apply(Snapshot(lit: true));

            _part.Dispose();
            Press();
            _part.Apply(Snapshot());

            Assert.That(GameSignals.WeaponLight.HasValue, Is.False);
            Assert.That(_sent, Is.Empty);
        }

        [Test]
        public void TheMessagesFromTheServerAreLeftToOtherParts()
        {
            Assert.That(_part.Handle(new EmoteStateMessage { PlayerId = 1, Playing = false }), Is.False);
        }

        [Test]
        public void TheKeyIsTheWeaponLightAction()
        {
            Assert.That(NetV2WeaponLightPart.LightActionPath, Is.EqualTo("Player/WeaponLight"));
        }

        // 再生では要求を受ける相手がいないので押しを読まず、記録された自分の点灯だけを出す
        [Test]
        public void AReplayPartNeverSends()
        {
            var sent = 0;
            var context = new NetV2ClientPartsContext(_client, _ =>
            {
                sent++;
                return true;
            }, _logs.Add)
            {
                Replay = true,
            };
            var replay = (NetV2WeaponLightPart)NetV2WeaponLightPart.Create(context);
            try
            {
                replay.Apply(Snapshot(lit: true));
                replay.Tick(0.016f);

                Assert.That(sent, Is.Zero);
                Assert.That(replay.IsOn, Is.True);
            }
            finally
            {
                replay.Dispose();
            }
        }

        [Test]
        public void ThePartNeedsTheClientAndTheSender()
        {
            Assert.Throws<ArgumentNullException>(() => new NetV2WeaponLightPart(null, Send));
            Assert.Throws<ArgumentNullException>(() => new NetV2WeaponLightPart(_client, null));
            Assert.Throws<ArgumentNullException>(() => NetV2WeaponLightPart.Create(null));
        }

        private void Press()
        {
            _lightPressed = true;
            _part.Tick(0.016f);
            _lightPressed = false;
        }

        private IReadOnlyList<string> AttachmentsOf(string weaponId)
        {
            return _attached.TryGetValue(weaponId, out var attached) ? attached : null;
        }

        // 小銃にだけ light の取り付け口があり、ライトが付く。拳銃には付かない
        private static ItemCatalog Items()
        {
            return new ItemCatalog(
                new[]
                {
                    new ItemSpec(_rifleItemId, "weapon.rifle", weaponId: _rifleId, attachmentSlots: new[] { "light" }),
                    new ItemSpec(_pistolItemId, "weapon.handgun", weaponId: _pistolId),
                    new ItemSpec(_lightItemId, "item.weaponLight", attachment: new AttachmentSpec("light", light: WeaponLightSpec.Default)),
                },
                InventoryRule.Default);
        }

        private static SnapshotMessage Snapshot(bool lit = false, string weaponId = _rifleId, int health = 100, bool downed = false)
        {
            var snapshot = new SnapshotMessage { HasOwner = true };
            snapshot.Match.State = GameModeState.Playing;
            ref var own = ref snapshot.Players.Add();
            own.PlayerId = _localId;
            own.Team = Team.Alpha;
            own.Health = health;
            own.Downed = new DownedSync(downed, downed ? 10 : 0, 0);
            own.WeaponId = weaponId;
            own.WeaponLightOn = lit;
            return snapshot;
        }

        private bool Send(IMessage message)
        {
            _sent.Add(((WeaponLightRequestMessage)message).On);
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

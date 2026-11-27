using System.Numerics;
using GameKit.World;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    public sealed class LedgeHangPredictionTests
    {
        private const int _playerId = 3;
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;

        // 上端が高さ 2.9、手前の面が z = 1、幅が x = -2 から 2 までの台
        private const float _ledgeTop = 2.9f;

        private static readonly Vector3 _wallNormal = new Vector3(0f, 0f, -1f);
        private static readonly Vector3 _ledgePoint = new Vector3(0f, _ledgeTop, 1f);
        private static readonly ParkourTuning _ledgeOn = new ParkourTuning(ledgeHang: new LedgeHangTuning(true));

        [Test]
        public void ThePredictionGrabsShimmiesAndClimbsExactlyLikeTheServer()
        {
            var server = NewSimulation();
            var client = new OwnerPrediction(NewSimulation());
            var state = Falling();
            client.Start(-1, state);

            var seen = new System.Collections.Generic.HashSet<Shooter.Core.ParkourMove>();
            for (var tick = 0; tick < 120; tick++)
            {
                var frame = Frame(tick, ClimbingCourse(tick));
                client.Predict(frame, Input(frame));
                state = server.Step(state, Input(frame));
                seen.Add(state.Parkour.Move);

                Assert.That(client.Reconcile(tick, state).Outcome, Is.EqualTo(ReconcileOutcome.Matched), "tick " + tick);
            }

            Assert.That(seen, Is.SupersetOf(new[]
            {
                Shooter.Core.ParkourMove.LedgeHang,
                Shooter.Core.ParkourMove.LedgeShimmy,
                Shooter.Core.ParkourMove.LedgeClimbUp,
            }));
            Assert.That(client.Current, Is.EqualTo(state));
            Assert.That(state.Parkour, Is.EqualTo(CharacterParkourState.None), "it has climbed up");
            Assert.That(state.IsGrounded, Is.True);
            Assert.That(state.Position.Z, Is.GreaterThan(1f), "it stands on the top, past the face");
        }

        [Test]
        public void CrouchingDropsTheSameWayOnBothSides()
        {
            var server = NewSimulation();
            var client = new OwnerPrediction(NewSimulation());
            var state = Falling();
            client.Start(-1, state);

            var droppedAt = -1;
            for (var tick = 0; tick < 60; tick++)
            {
                var buttons = tick == 5 ? InputButtons.Crouch : InputButtons.None;
                var frame = new InputFrame(tick, Vector2.Zero, 0f, buttons);
                client.Predict(frame, Input(frame));
                var before = state;
                state = server.Step(state, Input(frame));
                if (before.IsHangingOnLedge && !state.IsHangingOnLedge)
                {
                    droppedAt = tick;
                    Assert.That(server.LastLedgeHang.LetGoReason, Is.EqualTo(LedgeLetGoReason.Dropped));
                }

                Assert.That(client.Reconcile(tick, state).Outcome, Is.EqualTo(ReconcileOutcome.Matched), "tick " + tick);
            }

            Assert.That(droppedAt, Is.EqualTo(5));
            Assert.That(state.IsGrounded, Is.True, "it fell to the floor without grabbing again");
            Assert.That(state.IsHangingOnLedge, Is.False);
        }

        // スナップショットはぶら下がりの種類しか運ばないが、予測と種類が同じなら予測が持つふちで続けるので巻き戻さない。
        [Test]
        public void ASnapshotWithOnlyTheMoveMatchesTheHangingPrediction()
        {
            var server = NewSimulation();
            var client = new OwnerPrediction(NewSimulation());
            var state = Falling();
            client.Start(-1, state);

            for (var tick = 0; tick < 10; tick++)
            {
                var frame = Frame(tick, Vector2.Zero);
                client.Predict(frame, Input(frame));
                state = server.Step(state, Input(frame));
            }

            var row = new PlayerSnapshot
            {
                PlayerId = _playerId,
                Position = state.Position,
                Velocity = state.Velocity,
                ParkourMove = Shooter.Protocol.ParkourMove.LedgeHang,
            };

            Assert.That(client.Reconcile(9, SnapshotMotion.ToState(row)).Outcome, Is.EqualTo(ReconcileOutcome.Matched));
            Assert.That(client.Current.Parkour.LedgePoint, Is.EqualTo(state.Parkour.LedgePoint));
        }

        // 掴んだのを予測し損ねると、スナップショットから戻した状態にはふちが無い。目の前のふちを測り直してぶら下がり続ける。
        [Test]
        public void StartingFromASnapshotHangFindsTheLedgeAgainAndKeepsHanging()
        {
            var client = new OwnerPrediction(NewSimulation());
            var hang = LedgeHangStep.HangPosition(_ledgePoint, _wallNormal, NetMotorSettings.Default.Stance);
            var row = new PlayerSnapshot { PlayerId = _playerId, Position = hang, ParkourMove = Shooter.Protocol.ParkourMove.LedgeHang };
            client.Start(9, SnapshotMotion.ToState(row));

            var frame = Frame(10, Vector2.Zero);
            var predicted = client.Predict(frame, Input(frame));

            Assert.That(predicted.Parkour.Move, Is.EqualTo(Shooter.Core.ParkourMove.LedgeHang));
            Assert.That(Vector3.Distance(predicted.Parkour.LedgePoint, _ledgePoint), Is.LessThan(1e-3f));
            Assert.That(Vector3.Distance(predicted.Parkour.WallNormal, _wallNormal), Is.LessThan(1e-3f));
            Assert.That(Vector3.Distance(predicted.Position, hang), Is.LessThan(1e-3f), "it does not fall");
        }

        [Test]
        public void TheClientHoldsItsFireWhileHanging()
        {
            var snapshot = new SnapshotMessage { Tick = 10, HasOwner = true };
            ref var row = ref snapshot.Players.Add();
            row.PlayerId = _playerId;
            row.Health = 100;
            row.WeaponId = "pistol";
            snapshot.Owner.EquippedWeaponIds.Add("pistol");
            snapshot.Owner.Weapon = new WeaponSync { AmmoInMagazine = 10, WeaponCount = 1, EquippedIndex = 0 };
            var hang = LedgeHangStep.HangPosition(_ledgePoint, _wallNormal, NetMotorSettings.Default.Stance);
            var free = new CharacterMotorState(hang, Vector3.Zero, false, false);

            Assert.That(ClientFire.Decide(snapshot, _playerId, free), Is.EqualTo(FireDecision.Fire));
            foreach (var move in new[] { Shooter.Core.ParkourMove.LedgeHang, Shooter.Core.ParkourMove.LedgeShimmy, Shooter.Core.ParkourMove.LedgeClimbUp })
            {
                var hanging = free.WithParkour(new CharacterParkourState(move, 0.1f, _wallNormal, _ledgePoint));
                Assert.That(ClientFire.Decide(snapshot, _playerId, hanging), Is.EqualTo(FireDecision.Hold), move.ToString());
                Assert.That(hanging.CanAimDownSights, Is.False, move.ToString());
            }
        }

        [Test]
        public void OtherPlayersSeeTheHangFromTheRow()
        {
            var row = new PlayerSnapshot { PlayerId = _playerId, Position = new Vector3(0f, 1.9f, 0.4f), ParkourMove = Shooter.Protocol.ParkourMove.LedgeShimmy };

            Assert.That(RemotePose.From(row).Parkour, Is.EqualTo(Shooter.Core.ParkourMove.LedgeShimmy));
            Assert.That(SnapshotMotion.ToState(row).IsHangingOnLedge, Is.True);
        }

        // 0〜4 tick は掴んでぶら下がり、5〜14 tick は右へ動き、15 tick にジャンプで登る
        private static InputFrame Frame(int tick, Vector2 move)
        {
            return new InputFrame(tick, move, 0f, tick == 15 ? InputButtons.Jump : InputButtons.None);
        }

        private static Vector2 ClimbingCourse(int tick)
        {
            return tick >= 5 && tick < 15 ? new Vector2(1f, 0f) : Vector2.Zero;
        }

        private static CharacterMotorInput Input(InputFrame frame)
        {
            // 既定のルールは跳べる (MatchRulesSync.Jump) ので、ジャンプで登れる
            return SnapshotMotion.ToInput(frame, false, default);
        }

        // 手がふちの 0.3 m 上、体の表面が面の 0.1 m 手前に来る所から落とし始める
        private static CharacterMotorState Falling()
        {
            var stance = NetMotorSettings.Default.Stance;
            var start = new Vector3(0f, _ledgeTop + 0.3f - stance.Standing.TopY, 1f - stance.Radius - VaultProbe.WallGap);
            return new CharacterMotorState(start, new Vector3(0f, -1f, 0f), false, false);
        }

        private static CharacterMotorSimulation NewSimulation()
        {
            var world = new WorldMap(new[]
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, new Vector3(0f, -0.5f, 0f), new Vector3(50f, 0.5f, 50f), Quaternion.Identity),
                WorldCollider.Box(2, 0, WorldCollider.StaticGroup, new Vector3(0f, _ledgeTop * 0.5f, 1.5f), new Vector3(2f, _ledgeTop * 0.5f, 0.5f), Quaternion.Identity),
            });
            var settings = NetMotorSettings.Default;
            var body = new CharacterBody(new BvhWorldCollision(world), settings.Mover, settings.Stance);
            return new CharacterMotorSimulation(body, new PlayerMotor(settings.Motor), _tickSeconds, -50f) { Parkour = _ledgeOn };
        }
    }
}

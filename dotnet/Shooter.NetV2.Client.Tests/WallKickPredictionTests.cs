using System;
using System.Collections.Generic;
using System.Numerics;
using GameKit.World;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    // 床の上面は y = 0。体の原点はカプセルの中心で、足元はそれより 1 m 下。
    // 右 (+x) の壁の面は x = 0.7、左 (-x) の壁の面は x = -1.2 で、間を蹴って行き来する。
    // 右を蹴ってから手を離していると空中の操作で横の速さが毎 tick 1 割落ち、9 tick 後に x ≈ -0.65 で左の壁だけが届く。
    public sealed class WallKickPredictionTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private const int _firstKickTick = 3;
        private const int _secondKickTick = 12;
        private const int _lastTick = 150;
        private const int _lag = 6;

        private static readonly Vector3 _beside = new Vector3(0f, 4f, 0f);
        private static readonly ParkourTuning _kickOn = new ParkourTuning(new WallKickTuning(true));
        private static readonly MatchRulesSync _rules = new MatchRulesSync { Jump = Shooter.Core.Jump.On };

        [Test]
        public void ThePredictionAndTheServerKickToTheSamePlacesWithTheSameInputs()
        {
            var serverJumps = new List<int>();
            var clientJumps = new List<int>();
            var server = NewSimulation(_kickOn, serverJumps);
            var client = new OwnerPrediction(NewSimulation(_kickOn, clientJumps));
            var state = new CharacterMotorState(_beside, Vector3.Zero, false, false);
            client.Start(-1, state);

            var serverStates = new CharacterMotorState[_lastTick + 1];
            for (var tick = 0; tick <= _lastTick; tick++)
            {
                var frame = Frame(tick);
                var predicted = client.Predict(frame, SnapshotMotion.ToInput(frame, false, _rules));
                state = server.Step(state, SnapshotMotion.ToInput(frame, false, _rules));
                serverStates[tick] = state;

                Assert.That(predicted, Is.EqualTo(state), "tick " + tick);

                // サーバーの答えは数 tick 遅れ、壁の法線と秒数を持たない行で届く
                if (tick >= _lag)
                {
                    var authoritative = SnapshotMotion.ToState(Row(serverStates[tick - _lag]));
                    Assert.That(client.Reconcile(tick - _lag, authoritative).Outcome, Is.EqualTo(ReconcileOutcome.Matched), "tick " + tick);
                }
            }

            Assert.That(serverStates[_firstKickTick - 1].Parkour.Move, Is.EqualTo(Shooter.Core.ParkourMove.None));
            Assert.That(serverStates[_firstKickTick].Parkour.Move, Is.EqualTo(Shooter.Core.ParkourMove.WallKick), "the first jump kicks the right wall");
            Assert.That(serverStates[_firstKickTick].Parkour.WallNormal.X, Is.EqualTo(-1f).Within(1e-4f));
            Assert.That(serverStates[_firstKickTick].Velocity.X, Is.EqualTo(-WallKickTuning.DefaultAwaySpeed).Within(1e-4f));
            Assert.That(serverStates[_secondKickTick].Parkour.Elapsed, Is.Zero, "the second jump kicks the other wall");
            Assert.That(serverStates[_secondKickTick].Parkour.WallNormal.X, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(serverStates[_secondKickTick].Velocity.X, Is.EqualTo(WallKickTuning.DefaultAwaySpeed).Within(1e-4f));
            Assert.That(serverStates[_lastTick].IsGrounded, Is.True, "it has landed by the end");
            Assert.That(serverStates[_lastTick].Parkour, Is.EqualTo(CharacterParkourState.None), "landing ends the kick");
            Assert.That(serverJumps, Is.EqualTo(new[] { PlayerMotor.GroundJumpNumber, PlayerMotor.GroundJumpNumber }));
            Assert.That(clientJumps, Is.EqualTo(serverJumps), "the prediction tells of the same two jumps and replays none");
            Assert.That(client.Current, Is.EqualTo(state));
        }

        [Test]
        public void AKickOnlyTheServerMadeIsCorrectedOnceAndThenPredicted()
        {
            var server = NewSimulation(_kickOn, null);
            var client = new OwnerPrediction(NewSimulation(_kickOn, null));
            var state = new CharacterMotorState(_beside, Vector3.Zero, false, false);
            client.Start(-1, state);

            // 答えを待たずに予測を溜めるので、PredictionBuffer.DefaultCapacity (128) に収まる長さにする。蹴った 2 回の後、tick 70 ほどで着地する
            const int lastTick = 100;

            // 落ちた入力のように、跳んだ tick の入力をクライアントだけが押さずに予測する
            var serverStates = new CharacterMotorState[lastTick + 1];
            for (var tick = 0; tick <= lastTick; tick++)
            {
                var frame = Frame(tick);
                var predictedFrame = tick == _firstKickTick ? new InputFrame(tick, frame.Move, frame.Yaw, InputButtons.None) : frame;
                client.Predict(predictedFrame, SnapshotMotion.ToInput(predictedFrame, false, _rules));
                state = server.Step(state, SnapshotMotion.ToInput(frame, false, _rules));
                serverStates[tick] = state;
            }

            Assert.That(client.Reconcile(_firstKickTick - 1, serverStates[_firstKickTick - 1]).Outcome, Is.EqualTo(ReconcileOutcome.Matched));
            Assert.That(
                client.Reconcile(_firstKickTick, SnapshotMotion.ToState(Row(serverStates[_firstKickTick]))).Outcome,
                Is.EqualTo(ReconcileOutcome.Corrected),
                "the client did not kick");
            Assert.That(client.Current.Position, Is.EqualTo(serverStates[lastTick].Position), "the replay kicks the other wall the same way");
            Assert.That(serverStates[_secondKickTick].Parkour.WallNormal.X, Is.EqualTo(1f).Within(1e-4f));
            for (var tick = _firstKickTick + 1; tick <= lastTick; tick++)
            {
                Assert.That(
                    client.Reconcile(tick, SnapshotMotion.ToState(Row(serverStates[tick]))).Outcome, Is.EqualTo(ReconcileOutcome.Matched), "tick " + tick);
            }
        }

        // 手を離して落ちながら 2 回だけ押す。間の tick は押さないので、同じ押下で蹴り直さない
        private static InputFrame Frame(int tick)
        {
            var buttons = tick == _firstKickTick || tick == _secondKickTick ? InputButtons.Jump : InputButtons.None;
            return new InputFrame(tick, Vector2.Zero, 0f, buttons);
        }

        private static PlayerSnapshot Row(CharacterMotorState state)
        {
            return new PlayerSnapshot
            {
                Position = state.Position,
                Velocity = state.Velocity,
                IsGrounded = state.IsGrounded,
                IsCrouched = state.IsCrouched,
                IsProne = state.IsProne,
                Dodge = state.Dodge,
                Prone = state.Prone,
                Ladder = state.Ladder,
                Lean = state.Lean.Amount,
                Swimming = state.Swim.IsSwimming,
                WallRunSide = (Shooter.Protocol.WallRunSide)(byte)state.WallRun.Side,
                AirJumpsUsed = (byte)state.AirJumpsUsed,
                JumpGraceLeft = state.JumpGraceLeft,
                ParkourMove = (Shooter.Protocol.ParkourMove)(byte)state.Parkour.Move,
                IsParachuting = state.IsParachuting,
            };
        }

        private static WorldMap World()
        {
            return new WorldMap(new[]
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, new Vector3(0f, -0.5f, 0f), new Vector3(80f, 0.5f, 80f), Quaternion.Identity),
                WorldCollider.Box(2, 0, WorldCollider.StaticGroup, new Vector3(0.95f, 5f, 0f), new Vector3(0.25f, 5f, 20f), Quaternion.Identity),
                WorldCollider.Box(3, 0, WorldCollider.StaticGroup, new Vector3(-1.45f, 5f, 0f), new Vector3(0.25f, 5f, 20f), Quaternion.Identity),
            });
        }

        private static CharacterMotorSimulation NewSimulation(ParkourTuning parkour, List<int> jumps)
        {
            var settings = NetMotorSettings.Default;
            var body = new CharacterBody(new BvhWorldCollision(World()), settings.Mover, settings.Stance);
            var motor = new PlayerMotor(settings.Motor);
            if (jumps != null)
            {
                motor.Jumped += jumps.Add;
            }

            return new CharacterMotorSimulation(body, motor, _tickSeconds, -50f) { Parkour = parkour ?? throw new ArgumentNullException(nameof(parkour)) };
        }
    }
}

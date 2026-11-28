using System.Numerics;
using GameKit.Core;
using GameKit.World;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    // 床の上面は y = 0。走り込む人の正面に、上端 3 m・奥行き 3 m の台を置く。
    public sealed class WallClimbPredictionTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private const float _wallTop = 3f;
        private const float _wallDepth = 3f;

        private static readonly ParkourTuning _climbOn = new ParkourTuning(wallClimb: new WallClimbTuning(true));

        [Test]
        public void TheOwnersPredictionClimbsAndMantlesLikeTheServer()
        {
            var server = NewSimulation(_climbOn);
            var client = new OwnerPrediction(NewSimulation(_climbOn));
            var state = RunningAtTheWall(server);
            client.Start(-1, state);

            var climbed = false;
            var mantled = false;
            for (var tick = 0; tick < 120; tick++)
            {
                var input = Input(tick, tick == 0);
                client.Predict(Frame(tick, tick == 0), input);
                state = server.Step(state, input);
                climbed |= state.Parkour.Move == Shooter.Core.ParkourMove.WallClimb;
                mantled |= state.IsVaulting;

                Assert.That(client.Reconcile(tick, AsSent(state)).Outcome, Is.EqualTo(ReconcileOutcome.Matched), "tick " + tick);
            }

            Assert.That(climbed, Is.True);
            Assert.That(mantled, Is.True, "the mantle at the ledge needs no mantle rule, which the client does not know");
            Assert.That(client.Current, Is.EqualTo(state));
            Assert.That(state.IsGrounded, Is.True);
            Assert.That(server.Body.FeetY(state.Position), Is.EqualTo(_wallTop + CapsuleMoverSpec.DefaultSkinWidth).Within(0.05f), "standing on the top, resting a skin width above it");
        }

        [Test]
        public void WithMantleAtLedgeOffThePredictionFallsBackLikeTheServer()
        {
            var noMantle = new ParkourTuning(wallClimb: new WallClimbTuning(true, mantleAtLedge: false));
            var server = NewSimulation(noMantle);
            var client = new OwnerPrediction(NewSimulation(noMantle));
            var state = RunningAtTheWall(server);
            client.Start(-1, state);

            var climbed = false;
            var mantled = false;
            for (var tick = 0; tick < 120; tick++)
            {
                var input = Input(tick, tick == 0);
                client.Predict(Frame(tick, tick == 0), input);
                state = server.Step(state, input);
                climbed |= state.Parkour.Move == Shooter.Core.ParkourMove.WallClimb;
                mantled |= state.IsVaulting;

                Assert.That(client.Reconcile(tick, AsSent(state)).Outcome, Is.EqualTo(ReconcileOutcome.Matched), "tick " + tick);
            }

            Assert.That(climbed, Is.True);
            Assert.That(mantled, Is.False);
            Assert.That(client.Current, Is.EqualTo(state));
            Assert.That(state.IsGrounded, Is.True);
            Assert.That(server.Body.FeetY(state.Position), Is.LessThan(0.1f), "back on the floor, not on the top");
        }

        [Test]
        public void AClientThatStartsInTheMiddleOfTheMantleFollowsTheSamePath()
        {
            var server = NewSimulation(_climbOn);
            var state = server.Step(RunningAtTheWall(server), Input(0, true));
            var tick = 1;
            for (; tick < 60 && !state.IsVaulting; tick++)
            {
                state = server.Step(state, Input(tick, false));
            }

            state = server.Step(state, Input(tick, false));
            Assert.That(state.IsVaulting, Is.True, "the climb should have reached the ledge");

            // 途中から始めた予測は、サーバーが始めに作った軌跡を持たず、スナップショットの頂点から作り直す
            var client = new OwnerPrediction(NewSimulation(_climbOn));
            client.Start(tick, AsSent(state));
            for (var next = tick + 1; next < tick + 60; next++)
            {
                client.Predict(Frame(next, false), Input(next, false));
                state = server.Step(state, Input(next, false));
                Assert.That(client.Reconcile(next, AsSent(state)).Outcome, Is.EqualTo(ReconcileOutcome.Matched), "tick " + next);
            }

            Assert.That(state.IsVaulting, Is.False, "the mantle has ended");
            Assert.That(client.Current.Position, Is.EqualTo(state.Position));
        }

        [Test]
        public void ClimbingIsOffUnlessTheServerSendsTheTuning()
        {
            var server = NewSimulation(ParkourTuning.Default);
            var state = server.Step(RunningAtTheWall(server), Input(0, true));

            Assert.That(state.Parkour, Is.EqualTo(CharacterParkourState.None));
            Assert.That(state.Velocity.Y, Is.GreaterThan(6f), "a plain jump");
        }

        // スナップショットはパルクールの動きだけを運ぶので、秒数と壁の向きを落とした状態をサーバーの答えにする
        private static CharacterMotorState AsSent(CharacterMotorState state)
        {
            return state.WithParkour(new CharacterParkourState(state.Parkour.Move, 0f, Vector3.Zero, Vector3.Zero));
        }

        // 床に下ろしてから、壁へ走り込んでいる速さを持たせる
        private static CharacterMotorState RunningAtTheWall(CharacterMotorSimulation simulation)
        {
            var state = new CharacterMotorState(new Vector3(0f, 2.5f, 0f), Vector3.Zero, false, false);
            for (var i = 0; i < 60; i++)
            {
                state = simulation.Step(state, Input(0, false));
            }

            Assert.That(state.IsGrounded, Is.True, "the runner should stand on the floor");
            return new CharacterMotorState(state.Position, new Vector3(0f, 0f, 6f), true, false);
        }

        private static InputFrame Frame(int tick, bool jump)
        {
            return new InputFrame(tick, Vector2.Zero, 0f, jump ? InputButtons.Jump : InputButtons.None);
        }

        private static CharacterMotorInput Input(int tick, bool jump)
        {
            return SnapshotMotion.ToInput(Frame(tick, jump), false, default);
        }

        private static CharacterMotorSimulation NewSimulation(ParkourTuning parkour)
        {
            var settings = NetMotorSettings.Default;
            var faceZ = settings.Stance.Radius + 0.2f;
            var world = new WorldMap(new[]
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, new Vector3(0f, -0.5f, 0f), new Vector3(50f, 0.5f, 50f), Quaternion.Identity),
                WorldCollider.Box(
                    2,
                    0,
                    WorldCollider.StaticGroup,
                    new Vector3(0f, _wallTop * 0.5f, faceZ + (_wallDepth * 0.5f)),
                    new Vector3(5f, _wallTop * 0.5f, _wallDepth * 0.5f),
                    Quaternion.Identity),
            });
            var body = new CharacterBody(new BvhWorldCollision(world), settings.Mover, settings.Stance);
            return new CharacterMotorSimulation(body, new PlayerMotor(settings.Motor), _tickSeconds, -50f) { Parkour = parkour };
        }
    }
}

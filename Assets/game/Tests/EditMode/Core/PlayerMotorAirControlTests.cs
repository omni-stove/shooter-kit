using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class PlayerMotorAirControlTests
    {
        private const float _airSeconds = 0.5f;
        private const float _startSpeed = 3f;

        // 0.9^30。AirControl 0.1 で基準の 60Hz を 0.5 秒 (30 tick) 進めた後に残る割合
        private const float _keptAfterHalfSecond = 0.042391f;

        private static readonly Vector2 _forward = new Vector2(0f, 1f);

        [Test]
        public void AtTheReferenceTickAnAirStepBlendsByExactlyAirControl()
        {
            var spec = MotorSpec.Default;
            var tick = new TickClock().TickSeconds;
            var motor = new PlayerMotor(spec);
            motor.SetVelocity(new Vector3(_startSpeed, 0f, -2f));
            var desired = GroundVelocity(spec, _forward, 0.7f, tick);
            var expected = new Vector2(_startSpeed, -2f);

            for (var i = 0; i < 30; i++)
            {
                var displacement = motor.Step(_forward, 0.7f, false, false, false, false, tick);
                expected = Vector2.Lerp(expected, desired, spec.AirControl);

                // 許容幅なしで比べる。固定 tick のサーバーと予測は、直す前と 1 ビットも違わない結果でなければならない
                Assert.That(motor.Velocity.X, Is.EqualTo(expected.X), $"tick {i}");
                Assert.That(motor.Velocity.Z, Is.EqualTo(expected.Y), $"tick {i}");
                Assert.That(displacement.X, Is.EqualTo(expected.X * tick), $"tick {i}");
                Assert.That(displacement.Z, Is.EqualTo(expected.Y * tick), $"tick {i}");
            }
        }

        [Test]
        public void TheDefaultTickRateIsTheReferenceTheTuningWasMadeFor()
        {
            Assert.That(TickClock.DefaultTickRate, Is.EqualTo(60));
            Assert.That(MotorSpec.DefaultAirControl, Is.EqualTo(0.1f));
        }

        [TestCase(15)]
        [TestCase(30)]
        [TestCase(72)]
        [TestCase(120)]
        public void LettingGoInTheAirLosesTheSameSpeedAtAnyFrameRate(int steps)
        {
            var motor = new PlayerMotor(MotorSpec.Default);
            motor.SetVelocity(new Vector3(_startSpeed, 0f, 0f));

            Fly(motor, Vector2.Zero, steps);

            Assert.That(motor.Velocity.X, Is.EqualTo(_startSpeed * _keptAfterHalfSecond).Within(1e-4f));
            Assert.That(motor.Velocity.Z, Is.EqualTo(0f).Within(1e-6f));
        }

        [TestCase(15)]
        [TestCase(30)]
        [TestCase(72)]
        [TestCase(120)]
        public void SteeringInTheAirGainsTheSameSpeedAtAnyFrameRate(int steps)
        {
            var spec = MotorSpec.Default;
            var motor = new PlayerMotor(spec);
            var desired = GroundVelocity(spec, _forward, 0f, _airSeconds / steps);

            Fly(motor, _forward, steps);

            Assert.That(desired.Y, Is.GreaterThan(0f));
            Assert.That(motor.Velocity.Z, Is.EqualTo(desired.Y * (1f - _keptAfterHalfSecond)).Within(1e-3f));
            Assert.That(motor.Velocity.X, Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void OneStepAtHalfTheRateEqualsTwoReferenceTicks()
        {
            var motor = new PlayerMotor(MotorSpec.Default);
            motor.SetVelocity(new Vector3(_startSpeed, 0f, 0f));

            motor.Step(Vector2.Zero, 0f, false, false, false, false, new TickClock(30).TickSeconds);

            // 3 × 0.9 × 0.9
            Assert.That(motor.Velocity.X, Is.EqualTo(2.43f).Within(1e-5f));
        }

        [Test]
        public void AStepOfNoTimeKeepsTheSpeed()
        {
            var motor = new PlayerMotor(Spec(1f));
            motor.SetVelocity(new Vector3(_startSpeed, 0f, 0f));

            motor.Step(_forward, 0f, false, false, false, false, 0f);

            Assert.That(motor.Velocity.X, Is.EqualTo(_startSpeed));
            Assert.That(motor.Velocity.Z, Is.EqualTo(0f));
        }

        [TestCase(1f / 30f)]
        [TestCase(1f / 60f)]
        [TestCase(1f / 144f)]
        public void FullAirControlReachesTheWishedSpeedInOneStep(float deltaSeconds)
        {
            var spec = Spec(1f);
            var motor = new PlayerMotor(spec);
            motor.SetVelocity(new Vector3(_startSpeed, 0f, 0f));
            var desired = GroundVelocity(spec, _forward, 0f, deltaSeconds);

            motor.Step(_forward, 0f, false, false, false, false, deltaSeconds);

            Assert.That(motor.Velocity.X, Is.EqualTo(desired.X));
            Assert.That(motor.Velocity.Z, Is.EqualTo(desired.Y));
        }

        [TestCase(1f / 30f)]
        [TestCase(1f / 60f)]
        [TestCase(1f / 144f)]
        public void NoAirControlKeepsTheSpeed(float deltaSeconds)
        {
            var motor = new PlayerMotor(Spec(0f));
            motor.SetVelocity(new Vector3(_startSpeed, 0f, 0f));

            motor.Step(_forward, 0f, false, false, false, false, deltaSeconds);

            Assert.That(motor.Velocity.X, Is.EqualTo(_startSpeed));
            Assert.That(motor.Velocity.Z, Is.EqualTo(0f));
        }

        private static void Fly(PlayerMotor motor, Vector2 move, int steps)
        {
            var deltaSeconds = _airSeconds / steps;
            for (var i = 0; i < steps; i++)
            {
                motor.Step(move, 0f, false, false, false, false, deltaSeconds);
            }
        }

        // 地上の 1 歩は横の速さを目標の速さそのものにするので、空中で寄せる先をそこから読む
        private static Vector2 GroundVelocity(MotorSpec spec, Vector2 move, float yaw, float deltaSeconds)
        {
            var grounded = new PlayerMotor(spec);
            grounded.Step(move, yaw, false, false, false, true, deltaSeconds);
            return new Vector2(grounded.Velocity.X, grounded.Velocity.Z);
        }

        private static MotorSpec Spec(float airControl)
        {
            return new MotorSpec(
                MotorSpec.DefaultWalkSpeed,
                MotorSpec.DefaultSprintSpeed,
                MotorSpec.DefaultJumpHeight,
                MotorSpec.DefaultGravity,
                airControl,
                MotorSpec.DefaultCrouchSpeed);
        }
    }
}

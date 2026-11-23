using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    // 体は高さ 2・中心 0 で原点の上 1 に置くので、足元は y = 0、+Z を向いて探す。
    public sealed class CharacterCoverSensorTests
    {
        private const float _tolerance = 0.01f;
        private const float _faceZ = 1f;
        private const float _thickness = 0.4f;

        private static readonly StanceGeometry _stance = new StanceGeometry(2f, 0f, 0.6f, 1.2f, 0.5f);
        private static readonly Vector3 _position = new Vector3(0f, 1f, 0f);
        private static readonly Vector3 _forward = new Vector3(0f, 0f, 1f);

        private readonly List<WorldCollider> _colliders = new List<WorldCollider>();

        [SetUp]
        public void SetUp()
        {
            _colliders.Clear();
        }

        [Test]
        public void AWallStandingOnTheGroundIsFoundByTheFootRay()
        {
            AddBlock(0f, 1f);

            var result = NewSensor().Probe(_position, _forward, _forward, MotorSpec.Default, out var topY);

            Assert.That(result.IsWall, Is.True);
            Assert.That(result.Height, Is.EqualTo(CoverHeight.Low));
            Assert.That(topY, Is.EqualTo(1f).Within(_tolerance));
        }

        [Test]
        public void ABodyRaisedAboveTheFootRayLikeACarIsStillCover()
        {
            // 車体の底 (0.6m) が足元の Ray (0.5m) より高く、下を抜けても上の Ray (0.9m) で当たる。
            AddBlock(0.6f, 1.4f);

            var result = NewSensor().Probe(_position, _forward, _forward, MotorSpec.Default, out var topY);

            Assert.That(result.IsWall, Is.True);
            Assert.That(result.Height, Is.EqualTo(CoverHeight.High));
            Assert.That(result.Distance, Is.EqualTo(_faceZ).Within(_tolerance));
            Assert.That(result.IsAtLeftEdge, Is.False, "the edge rays run at the height that hit");
            Assert.That(result.IsAtRightEdge, Is.False);
            Assert.That(topY, Is.EqualTo(1.4f).Within(_tolerance));
        }

        [Test]
        public void SomethingHangingAboveBothRaysIsNotCover()
        {
            AddBlock(MotorSpec.DefaultCoverUpperProbeHeight + 0.1f, 2.5f);

            var result = NewSensor().Probe(_position, _forward, _forward, MotorSpec.Default, out var topY);

            Assert.That(result.IsWall, Is.False);
            Assert.That(topY, Is.EqualTo(0f));
        }

        // 壁の上端は足元から 0.4 m で、既定の 2 本の Ray (0.5 m・0.9 m) はどちらも上を越える。
        [TestCase(MotorSpec.DefaultCoverProbeHeight, false, 0f)]
        [TestCase(0.3f, true, 0.4f)]
        public void AWallLowerThanTheFootRayIsFoundOnlyWithALowerConfiguredRay(float probeHeight, bool isWall, float expectedTopY)
        {
            AddBlock(0f, 0.4f);

            var result = NewSensor().Probe(_position, _forward, _forward, SpecWith(coverProbeHeight: probeHeight), out var topY);

            Assert.That(result.IsWall, Is.EqualTo(isWall));
            Assert.That(topY, Is.EqualTo(expectedTopY).Within(_tolerance));
        }

        // 車体の底は足元から 0.6 m で、足元の Ray (0.5 m) は下を抜ける。
        [TestCase(MotorSpec.DefaultCoverUpperProbeHeight, true)]
        [TestCase(0.55f, false)]
        public void ARaisedBodyIsFoundOnlyWhenTheConfiguredUpperRayReachesIt(float upperProbeHeight, bool isWall)
        {
            AddBlock(0.6f, 1.4f);

            var result = NewSensor().Probe(
                _position, _forward, _forward, SpecWith(coverUpperProbeHeight: upperProbeHeight), out _);

            Assert.That(result.IsWall, Is.EqualTo(isWall));
        }

        // 壁の上端は足元から 3.5 m。上端を測る Ray の始点より高い壁は、始点の高さを上端とする。
        [TestCase(MotorSpec.DefaultCoverTopProbeHeight, 3f)]
        [TestCase(4f, 3.5f)]
        public void TheTopOfATallWallIsMeasuredUpToTheConfiguredTopRay(float topProbeHeight, float expectedTopY)
        {
            AddBlock(0f, 3.5f);

            var result = NewSensor().Probe(_position, _forward, _forward, SpecWith(coverTopProbeHeight: topProbeHeight), out var topY);

            Assert.That(result.IsWall, Is.True);
            Assert.That(result.TopHeight, Is.EqualTo(expectedTopY).Within(_tolerance));
            Assert.That(topY, Is.EqualTo(expectedTopY).Within(_tolerance));
        }

        private static MotorSpec SpecWith(
            float coverProbeHeight = MotorSpec.DefaultCoverProbeHeight,
            float coverUpperProbeHeight = MotorSpec.DefaultCoverUpperProbeHeight,
            float coverTopProbeHeight = MotorSpec.DefaultCoverTopProbeHeight)
        {
            return new MotorSpec(
                MotorSpec.DefaultWalkSpeed,
                MotorSpec.DefaultSprintSpeed,
                MotorSpec.DefaultJumpHeight,
                MotorSpec.DefaultGravity,
                MotorSpec.DefaultAirControl,
                MotorSpec.DefaultCrouchSpeed,
                coverProbeHeight: coverProbeHeight,
                coverUpperProbeHeight: coverUpperProbeHeight,
                coverTopProbeHeight: coverTopProbeHeight);
        }

        private CharacterCoverSensor NewSensor()
        {
            var world = new BvhWorldCollision(new WorldMap(_colliders));
            var body = new CharacterBody(world, CapsuleMoverSpec.Default, _stance);
            body.Place(_position, false);
            return new CharacterCoverSensor(body);
        }

        private void AddBlock(float bottomY, float topY)
        {
            var halfHeight = (topY - bottomY) * 0.5f;
            _colliders.Add(WorldCollider.Box(
                _colliders.Count + 1,
                0,
                WorldCollider.StaticGroup,
                new Vector3(0f, bottomY + halfHeight, _faceZ + (_thickness * 0.5f)),
                new Vector3(2f, halfHeight, _thickness * 0.5f),
                Quaternion.Identity));
        }
    }
}

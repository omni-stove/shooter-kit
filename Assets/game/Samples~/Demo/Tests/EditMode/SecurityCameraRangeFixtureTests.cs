using System;
using System.Linq;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class SecurityCameraRangeFixtureTests
    {
        private const float Epsilon = 1e-4f;

        private Scene _scene;
        private WeaponDefinition _rifle;

        [SetUp]
        public void SetUp()
        {
            _scene = TestScenes.NewAdditive();
            _rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            _rifle.name = "Rifle";
            WeaponAssetBuilder.Configure(_rifle, DemoTestWeapons.CarbineSpec());
        }

        [TearDown]
        public void TearDown()
        {
            if (_scene.IsValid())
            {
                EditorSceneManager.CloseScene(_scene, true);
            }

            Object.DestroyImmediate(_rifle);
        }

        [Test]
        public void TheTpsRangeAndThePlaygroundFindTheYardAndItsSmokeStep()
        {
            Assert.That(TpsRangeSceneBuilder.Fixtures().OfType<SecurityCameraRangeFixture>().Single().StartsAwake, Is.False);
            Assert.That(TpsRangeSmokeRun.ExtraSteps().OfType<SecurityCameraSmokeStep>().Count(), Is.EqualTo(1));

            var station = FeatureSceneBuilder.PlaygroundStations().Single(s => s.Fixture.Name == SecurityCameraRangeFixture.RootName);
            Assert.That(station.Offset, Is.EqualTo(Vector3.zero));
            Assert.That(((SecurityCameraRangeFixture)station.Fixture).StartsAwake, Is.True);
        }

        [Test]
        public void TheSmokeStepChecksFiveThingsInOrder()
        {
            Assert.That(Enum.GetValues(typeof(SecurityCameraSmokeCheck)).Cast<SecurityCameraSmokeCheck>(), Is.EqualTo(new[]
            {
                SecurityCameraSmokeCheck.Spotted,
                SecurityCameraSmokeCheck.AlarmRaised,
                SecurityCameraSmokeCheck.Investigated,
                SecurityCameraSmokeCheck.Destroyed,
                SecurityCameraSmokeCheck.StayedQuiet,
            }));

            var step = new SecurityCameraSmokeStep();
            Assert.That(step.Name, Is.EqualTo("security-camera"));
            Assert.That(step.Order, Is.EqualTo(580));
            Assert.That(step.TimeoutSeconds, Is.EqualTo(60f));
        }

        [Test]
        public void TheSmokeLinesStartWithTheStealthTag()
        {
            Assert.That(SecurityCameraSmokeLog.Spotted("yard-camera-east", 2.345f), Is.EqualTo("[Stealth] smoke=security-camera check=Spotted camera=yard-camera-east seconds=2.35"));
            Assert.That(SecurityCameraSmokeLog.AlarmRaised("yard-camera-east", 30f), Is.EqualTo("[Stealth] smoke=security-camera check=AlarmRaised camera=yard-camera-east radius=30.0"));
            Assert.That(
                SecurityCameraSmokeLog.Investigated("yard-guard-near", "yard-guard-far", 2.5f),
                Is.EqualTo("[Stealth] smoke=security-camera check=Investigated enemy=yard-guard-near approached=2.50 ignored=yard-guard-far"));
            Assert.That(SecurityCameraSmokeLog.Destroyed("yard-camera-east", 2), Is.EqualTo("[Stealth] smoke=security-camera check=Destroyed camera=yard-camera-east shots=2"));
            Assert.That(
                SecurityCameraSmokeLog.StayedQuiet("yard-camera-west", 1.5f, 4f),
                Is.EqualTo("[Stealth] smoke=security-camera check=StayedQuiet camera=yard-camera-west held=1.50 watched=4.00"));
            Assert.That(SecurityCameraSmokeLog.Destroyed(null, 0), Is.EqualTo("[Stealth] smoke=security-camera check=Destroyed camera=- shots=0"));
        }

        [Test]
        public void TheYardRunsAlongTheNorthEdgeNextToTheStealthArea()
        {
            var footprint = new SecurityCameraRangeFixture().Footprint;

            AssertNear(footprint.min, new Vector3(-38f, 0f, 30f));
            AssertNear(footprint.max, new Vector3(-1.5f, 3.5f, 37.5f));
            Assert.That(footprint.min.z, Is.GreaterThan(new StealthRangeFixture().Footprint.max.z));
            Assert.That(footprint.max.x, Is.LessThan(new RangeWeaponRackFixture().Footprint.min.x));
            Assert.That(footprint.max.z, Is.LessThanOrEqualTo(TpsRangeSceneBuilder.FloorHalfExtent));

            AssertNear(SecurityCameraRangeFixture.EastStand, new Vector3(-4f, 0f, 33.3f));
            AssertNear(SecurityCameraRangeFixture.WestStand, new Vector3(-16f, 0f, 33.3f));
            AssertNear(SecurityCameraRangeFixture.WestBeside, new Vector3(-17.2f, 0f, 36.45f));
            foreach (var point in new[]
            {
                SecurityCameraRangeFixture.EastStand,
                SecurityCameraRangeFixture.WestStand,
                SecurityCameraRangeFixture.WestBeside,
                SecurityCameraRangeFixture.NearGuardGround,
                SecurityCameraRangeFixture.FarGuardGround,
                SecurityCameraRangeFixture.EastCameraMount,
                SecurityCameraRangeFixture.WestCameraMount,
            })
            {
                Assert.That(point.x, Is.InRange(footprint.min.x, footprint.max.x), point.ToString());
                Assert.That(point.z, Is.InRange(footprint.min.z, footprint.max.z), point.ToString());
            }

            // 仕切りの東に東のカメラと近い見張り、西に西のカメラと遠い見張りを置く。東のカメラの前に立つ間、西のカメラから見えないように。
            var dividerWest = SecurityCameraRangeFixture.DividerCenter.x - (SecurityCameraRangeFixture.DividerSize.x / 2f);
            var dividerEast = SecurityCameraRangeFixture.DividerCenter.x + (SecurityCameraRangeFixture.DividerSize.x / 2f);
            foreach (var east in new[] { SecurityCameraRangeFixture.EastCameraMount, SecurityCameraRangeFixture.EastStand, SecurityCameraRangeFixture.NearGuardGround })
            {
                Assert.That(east.x, Is.GreaterThan(dividerEast), east.ToString());
            }

            foreach (var west in new[] { SecurityCameraRangeFixture.WestCameraMount, SecurityCameraRangeFixture.WestStand, SecurityCameraRangeFixture.WestBeside, SecurityCameraRangeFixture.FarGuardGround })
            {
                Assert.That(west.x, Is.LessThan(dividerWest), west.ToString());
            }
        }

        [Test]
        public void TheAlarmReachesOnlyTheNearGuardFromTheEastCamerasStand()
        {
            var stand = SecurityCameraRangeFixture.EastStand;
            var near = Flat(stand, SecurityCameraRangeFixture.NearGuardGround);
            var far = Flat(stand, SecurityCameraRangeFixture.FarGuardGround);

            Assert.That(near, Is.EqualTo(5.32f).Within(0.01f));
            Assert.That(far, Is.EqualTo(32.82f).Within(0.01f));
            foreach (var radius in new[] { SecurityAlarmSpec.DefaultRadius, StealthActionInstaller.Tuning.Alarm.Radius })
            {
                Assert.That(near, Is.LessThanOrEqualTo(radius));
                Assert.That(far, Is.GreaterThan(radius));
            }

            // 見つかったプレイヤーは出現位置へ戻るので、調べに来た見張りからも遠い見張りからも見えない。
            foreach (var guard in new[] { SecurityCameraRangeFixture.NearGuardGround, SecurityCameraRangeFixture.FarGuardGround })
            {
                Assert.That(Vector3.Distance(GameBootstrap.PlayerSpawn, guard), Is.GreaterThan(StealthRangeFixture.SightRange), guard.ToString());
            }

            // 遠い見張りは西の外壁を向くので、西のカメラの横と前に立つプレイヤーは背中の側にいる。
            foreach (var point in new[] { SecurityCameraRangeFixture.WestBeside, SecurityCameraRangeFixture.WestStand })
            {
                var toPlayer = point - SecurityCameraRangeFixture.FarGuardGround;
                Assert.That(Vector3.Dot(SecurityCameraRangeFixture.GuardFacing * Vector3.forward, toPlayer), Is.LessThan(0f), point.ToString());
            }
        }

        [Test]
        public void TheGuardsAndTheStandStayOnTheBakedSlabs()
        {
            var radius = SecurityCameraRangeFixture.GuardBody.Radius;
            AssertOnSlab(SecurityCameraRangeFixture.NearGuardGround, SecurityCameraRangeFixture.NearSlabCenter, SecurityCameraRangeFixture.NearSlabSize, radius);
            AssertOnSlab(SecurityCameraRangeFixture.EastStand, SecurityCameraRangeFixture.NearSlabCenter, SecurityCameraRangeFixture.NearSlabSize, radius);
            AssertOnSlab(SecurityCameraRangeFixture.FarGuardGround, SecurityCameraRangeFixture.FarSlabCenter, SecurityCameraRangeFixture.FarSlabSize, radius);
        }

        [Test]
        public void StandingBesideTheWestCameraIsOutOfItsSightButWithinItsReach()
        {
            var spec = StealthActionInstaller.Tuning.Camera;
            var mount = SecurityCameraRangeFixture.WestCameraMount;
            var eye = mount + (SecurityCameraRangeFixture.CameraFacing * SecurityCamera.EyeLocalPosition);
            var beside = SecurityCameraRangeFixture.WestBeside;
            var flat = new Vector3(beside.x - eye.x, 0f, beside.z - eye.z);
            var bearing = Vector3.Angle(SecurityCameraRangeFixture.CameraFacing * Vector3.forward, flat);

            Assert.That(bearing, Is.GreaterThan((spec.SweepDegrees / 2f) + (spec.FieldOfViewDegrees / 2f)));
            Assert.That(flat.magnitude, Is.LessThan(spec.DisableRadius));

            // 前に立つ所は東のカメラが見つけた所と同じ距離にする。止まっていなければ同じように見つかる。
            Assert.That(
                Flat(SecurityCameraRangeFixture.WestStand, mount),
                Is.EqualTo(Flat(SecurityCameraRangeFixture.EastStand, SecurityCameraRangeFixture.EastCameraMount)).Within(Epsilon));
            Assert.That(Flat(SecurityCameraRangeFixture.EastStand, SecurityCameraRangeFixture.EastCameraMount), Is.LessThan(spec.ViewDistance));
        }

        [Test]
        public void TheTpsRangeYardWaitsAsleepForItsSmokeStep()
        {
            var root = PlaceWithBootstrap(new SecurityCameraRangeFixture());

            Assert.That(root.name, Is.EqualTo(SecurityCameraRangeFixture.RootName));
            Assert.That(root.scene, Is.EqualTo(_scene));
            Assert.That(root.transform.parent, Is.Null);

            var cameras = root.GetComponentsInChildren<SecurityCamera>(true);
            Assert.That(cameras.Select(c => c.Id), Is.EquivalentTo(new[] { SecurityCameraRangeFixture.EastCameraId, SecurityCameraRangeFixture.WestCameraId }));
            foreach (var camera in cameras)
            {
                Assert.That(camera.gameObject.activeSelf, Is.False, camera.Id + " waits for the smoke step");
                Assert.That(camera.GetComponent<Damageable>(), Is.Not.Null, camera.Id + " can be shot");
                Assert.That(Quaternion.Angle(camera.transform.rotation, SecurityCameraRangeFixture.CameraFacing), Is.LessThan(0.01f));
            }

            AssertNear(cameras.Single(c => c.Id == SecurityCameraRangeFixture.EastCameraId).transform.position, SecurityCameraRangeFixture.EastCameraMount);
            AssertNear(cameras.Single(c => c.Id == SecurityCameraRangeFixture.WestCameraId).transform.position, SecurityCameraRangeFixture.WestCameraMount);

            var alarm = root.GetComponentsInChildren<SecurityAlarm>(true).Single();
            Assert.That(alarm.name, Is.EqualTo(SecurityCameraRangeFixture.AlarmName));
            Assert.That(alarm.gameObject.activeSelf, Is.True, "the alarm listens from the start; only the cameras raise it");

            var guards = root.GetComponentsInChildren<EnemyAgent>(true);
            Assert.That(guards.Select(g => g.Id), Is.EquivalentTo(new[] { SecurityCameraRangeFixture.NearGuardId, SecurityCameraRangeFixture.FarGuardId }));
            foreach (var guard in guards)
            {
                Assert.That(guard.gameObject.activeSelf, Is.False, guard.Id + " waits for the smoke step");
                Assert.That(guard.GetComponent<EnemyStealth>(), Is.Not.Null, guard.Id + " hears the alarm");
                Assert.That(Quaternion.Angle(guard.transform.rotation, SecurityCameraRangeFixture.GuardFacing), Is.LessThan(0.01f));
            }

            var near = guards.Single(g => g.Id == SecurityCameraRangeFixture.NearGuardId);
            Assert.That(near.name, Is.EqualTo(SecurityCameraRangeFixture.NearGuardName));
            Assert.That(Flat(near.transform.position, SecurityCameraRangeFixture.NearGuardGround), Is.LessThan(Epsilon));
            var far = guards.Single(g => g.Id == SecurityCameraRangeFixture.FarGuardId);
            Assert.That(far.name, Is.EqualTo(SecurityCameraRangeFixture.FarGuardName));
            Assert.That(Flat(far.transform.position, SecurityCameraRangeFixture.FarGuardGround), Is.LessThan(Epsilon));

            var bake = root.GetComponent<NavMeshBakeActivator>();
            Assert.That(bake, Is.Not.Null);
            Assert.That(bake.Source.name, Is.EqualTo(SecurityCameraRangeFixture.NavFloorName));
            Assert.That(bake.Source.Find(SecurityCameraRangeFixture.NearSlabName), Is.Not.Null);
            Assert.That(bake.Source.Find(SecurityCameraRangeFixture.FarSlabName), Is.Not.Null);
            Assert.That(bake.ActivateAfterBake, Is.Empty, "the bake must not wake the guards");

            Assert.That(root.transform.Find(SecurityCameraRangeFixture.BackWallName), Is.Not.Null);
            Assert.That(root.transform.Find(SecurityCameraRangeFixture.DividerName), Is.Not.Null);
        }

        [Test]
        public void ThePlaygroundYardRunsTheCamerasAndWakesTheGuardsAfterTheBake()
        {
            var root = PlaceWithBootstrap(new SecurityCameraRangeFixture(startsAwake: true));

            foreach (var camera in root.GetComponentsInChildren<SecurityCamera>(true))
            {
                Assert.That(camera.gameObject.activeSelf, Is.True, camera.Id);
            }

            var guards = root.GetComponentsInChildren<EnemyAgent>(true);
            foreach (var guard in guards)
            {
                Assert.That(guard.gameObject.activeSelf, Is.False, guard.Id + " waits for the NavMesh");
            }

            Assert.That(root.GetComponent<NavMeshBakeActivator>().ActivateAfterBake, Is.EquivalentTo(guards.Select(g => g.gameObject)));
        }

        [Test]
        public void WithoutAWeaponTheYardKeepsItsCamerasAndAlarm()
        {
            var root = new SecurityCameraRangeFixture().Place(_scene, GameRules.Default);

            Assert.That(root.GetComponentsInChildren<SecurityCamera>(true), Has.Length.EqualTo(2));
            Assert.That(root.GetComponentsInChildren<SecurityAlarm>(true), Has.Length.EqualTo(1));
            Assert.That(root.GetComponentsInChildren<EnemyAgent>(true), Is.Empty);
        }

        private GameObject PlaceWithBootstrap(SecurityCameraRangeFixture fixture)
        {
            var bootstrap = new GameObject("GameBootstrap");
            SceneManager.MoveGameObjectToScene(bootstrap, _scene);
            bootstrap.AddComponent<GameBootstrap>().WeaponDefinitions = new[] { _rifle };
            return fixture.Place(_scene, GameRules.Default);
        }

        private static void AssertOnSlab(Vector3 point, Vector3 center, Vector2 size, float inset)
        {
            Assert.That(point.x, Is.InRange(center.x - (size.x / 2f) + inset, center.x + (size.x / 2f) - inset), point.ToString());
            Assert.That(point.z, Is.InRange(center.z - (size.y / 2f) + inset, center.z + (size.y / 2f) - inset), point.ToString());
        }

        private static void AssertNear(Vector3 actual, Vector3 expected)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(Epsilon), $"{actual} vs {expected}");
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            return new Vector2(a.x - b.x, a.z - b.z).magnitude;
        }
    }
}

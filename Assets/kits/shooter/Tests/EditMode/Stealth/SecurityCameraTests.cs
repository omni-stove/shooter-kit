using System.Collections.Generic;
using System.Text.RegularExpressions;
using GameKit.Core;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Edit Mode では Update が走らないので、SecurityCamera.Tick を 0.25 秒ずつ手で進める。
    // カメラは (0, 1, 0) に +Z 向きで置き、目は (0, 1, 0.325)。プレイヤーは正面 (0, 1, 2.5) に立つ。
    // 目から体の中心まで 2.175 m なので、気づきは最大の速さ (立ち止まって 0.5 / 秒) でたまり、止められる距離 (2.5 m) にも入る。
    public sealed class SecurityCameraTests
    {
        private const float Step = 0.25f;
        private const string CameraId = "cam-test";

        private static readonly Vector3 CameraPosition = new Vector3(0f, 1f, 0f);
        private static readonly Vector3 PlayerPosition = new Vector3(0f, 1f, 2.5f);

        private readonly List<Object> _created = new List<Object>();
        private readonly List<(Vector3 At, string Id)> _alarms = new List<(Vector3, string)>();

        [SetUp]
        public void SetUp()
        {
            SecurityAlarmBus.ResetOnPlay();
            SecurityAlarmBus.Raised += OnAlarm;
            GameSignals.InteractHint.Clear();
            KeySharing.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            SecurityAlarmBus.ResetOnPlay();
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
            _alarms.Clear();
            GameSignals.InteractHint.Clear();
            KeySharing.Reset();
        }

        private void OnAlarm(Vector3 at, string id)
        {
            _alarms.Add((at, id));
        }

        private SecurityCamera Place(SecurityCameraSpec spec = null, SecurityCameraCatalog catalog = null)
        {
            var root = new GameObject("SecurityCamera");
            _created.Add(root);
            root.transform.SetPositionAndRotation(CameraPosition, Quaternion.identity);
            var camera = root.AddComponent<SecurityCamera>();
            camera.Initialize(CameraId, spec ?? SecurityCameraSpec.Default, catalog);
            Physics.SyncTransforms();
            return camera;
        }

        private StealthTarget PlacePlayer(Stealth rule = Stealth.On, Vector3? at = null)
        {
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            _created.Add(player);
            player.transform.position = at ?? PlayerPosition;
            var damageable = player.AddComponent<Damageable>();
            damageable.Initialize("Player", Damageable.DefaultMaxHealth);
            damageable.IsLocalPlayer = true;
            var target = player.AddComponent<StealthTarget>();
            target.Rule = rule;
            Physics.SyncTransforms();
            return target;
        }

        private static void Run(SecurityCamera camera, float seconds, bool held = false)
        {
            var ticks = Mathf.RoundToInt(seconds / Step);
            for (var i = 0; i < ticks; i++)
            {
                camera.Tick(Step, held);
            }
        }

        private static SecurityCameraHint ShownHint(SecurityCamera camera)
        {
            var shown = GameSignals.InteractHint.Value;
            Assert.That(shown.HasValue, Is.True, "a hint is shown");
            Assert.That(shown.Value.Source, Is.SameAs(camera));
            Assert.That(shown.Value.Detail, Is.TypeOf<SecurityCameraHint>());
            return (SecurityCameraHint)shown.Value.Detail;
        }

        [Test]
        public void WithNobodyThereTheHeadSweepsAtTheTunedSpeed()
        {
            var camera = Place();

            Run(camera, 1f);
            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Sweeping));
            Assert.That(camera.Sweep.YawDegrees, Is.EqualTo(20f), "1 s at 20 deg/s");
            Assert.That(camera.Head.localEulerAngles.y, Is.EqualTo(20f).Within(1e-3f), "the head turns with the sweep");

            Run(camera, 1.25f);
            Assert.That(camera.Sweep.YawDegrees, Is.EqualTo(45f), "the edge of the 90 deg sweep");
            Assert.That(camera.Head.localEulerAngles.y, Is.EqualTo(45f).Within(1e-3f));

            Run(camera, 3f);
            Assert.That(camera.Sweep.YawDegrees, Is.EqualTo(25f), "2 s at the edge, then 1 s back");
            Assert.That(camera.Head.localEulerAngles.y, Is.EqualTo(25f).Within(1e-3f));
            Assert.That(GameSignals.InteractHint.HasValue, Is.False, "nobody to show a hint to");
            Assert.That(_alarms, Is.Empty);
        }

        [Test]
        public void TheHeadSweepsAtTheSpeedFromTheSpec()
        {
            var camera = Place(new SecurityCameraSpec(sweepDegreesPerSecond: 40f));

            Run(camera, 0.5f);

            Assert.That(camera.Sweep.YawDegrees, Is.EqualTo(20f));
            Assert.That(camera.Head.localEulerAngles.y, Is.EqualTo(20f).Within(1e-3f));
        }

        [Test]
        public void APlayerInFrontIsSpottedAndRaisesTheAlarmOnce()
        {
            var camera = Place();
            PlacePlayer();
            LogAssert.Expect(LogType.Log, new Regex(@"^\[SecurityCamera\] camera=cam-test phase=Tracking from=Sweeping yaw=-?0\.00 level=0\.1[23]$"));
            LogAssert.Expect(LogType.Log, new Regex(@"^\[SecurityCamera\] camera=cam-test phase=Spotted from=Tracking at=\(-?0\.00, -?0\.00, 2\.50\) level=1\.00$"));

            Run(camera, 1.75f);
            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Tracking));
            Assert.That(camera.Sweep.Level, Is.EqualTo(0.875f), "0.5 per second for a player standing still");
            Assert.That(_alarms, Is.Empty);

            Run(camera, 0.25f);
            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Spotted), "spotted at 2 s");
            Assert.That(_alarms, Has.Count.EqualTo(1));
            Assert.That(_alarms[0].Id, Is.EqualTo(CameraId));
            Assert.That(_alarms[0].At.x, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(_alarms[0].At.y, Is.EqualTo(0f).Within(1e-4f), "the feet of the capsule");
            Assert.That(_alarms[0].At.z, Is.EqualTo(2.5f).Within(1e-4f));

            Run(camera, 5f);
            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Spotted));
            Assert.That(_alarms, Has.Count.EqualTo(1), "staying in view does not raise it again");
        }

        [Test]
        public void APlayerBehindAWallIsNotSeen()
        {
            var camera = Place();
            PlacePlayer();
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(wall);
            wall.name = "Wall";
            wall.transform.position = new Vector3(0f, 1f, 1.25f);
            wall.transform.localScale = new Vector3(3f, 3f, 0.2f);
            Physics.SyncTransforms();

            Run(camera, 5f);

            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Sweeping));
            Assert.That(camera.Sweep.Level, Is.Zero);
            Assert.That(_alarms, Is.Empty);
            Assert.That(GameSignals.InteractHint.HasValue, Is.False, "cannot be disabled through the wall");
        }

        [Test]
        public void APlayerWithoutTheStealthRuleIsNotWatched()
        {
            var camera = Place();
            PlacePlayer(Stealth.Off);

            Run(camera, 5f);

            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Sweeping));
            Assert.That(_alarms, Is.Empty);
        }

        [Test]
        public void TheCameraTakesTheSameCuesAsTheGuards()
        {
            var camera = Place();
            var player = PlacePlayer();

            Assert.That(camera.TryDescribe(player, out var standing), Is.True);
            Assert.That(standing.HasLineOfSight, Is.True);
            Assert.That(standing.Distance, Is.EqualTo(2.175f).Within(1e-3f));
            Assert.That(standing.BearingDegrees, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(standing.Posture, Is.EqualTo(AwarenessPosture.Standing));
            Assert.That(standing.LightFactor, Is.EqualTo(1f));
            Assert.That(standing.IsArmed, Is.True, "no holster means the weapon is out");
            Assert.That(standing.IsLit, Is.False, "no weapon light");

            player.IsProne = true;
            player.LightFactor = 0.5f;
            Assert.That(camera.TryDescribe(player, out var prone), Is.True);
            Assert.That(prone.Posture, Is.EqualTo(AwarenessPosture.Prone));
            Assert.That(prone.LightFactor, Is.EqualTo(0.5f));

            player.LightFactor = 0f;
            Assert.That(camera.TryDescribe(player, out _), Is.False, "nothing fills in the dark");

            player.LightFactor = 1f;
            player.transform.position = new Vector3(2.5f, 1f, 0.325f);
            Physics.SyncTransforms();
            Assert.That(camera.TryDescribe(player, out _), Is.False, "90 deg to the side is outside the 60 deg view at yaw 0");
        }

        [Test]
        public void ShootingTheCameraBreaksItAndItNeverRaisesTheAlarmAgain()
        {
            var camera = Place();
            PlacePlayer();
            Run(camera, 2f);
            Assert.That(_alarms, Has.Count.EqualTo(1));

            var hitbox = camera.Head.Find(SecurityCamera.BodyName).GetComponent<Hitbox>();
            Assert.That(hitbox.Owner, Is.SameAs(camera.Damageable));
            hitbox.ApplyResolvedHit(25, null, null, DamageKind.Bullet);
            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Spotted), "50 health takes two rifle hits");

            LogAssert.Expect(LogType.Log, new Regex(@"^\[SecurityCamera\] camera=cam-test phase=Destroyed from=Spotted$"));
            hitbox.ApplyResolvedHit(25, null, null, DamageKind.Bullet);

            Assert.That(camera.Damageable.IsDead, Is.True);
            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Destroyed));
            Assert.That(camera.gameObject.activeSelf, Is.False, "hidden until Destroy removes it at the end of the frame");
            Assert.That(GameSignals.InteractHint.HasValue, Is.False, "a broken camera has nothing to disable");

            _alarms.Clear();
            Run(camera, 10f, held: true);
            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Destroyed));
            Assert.That(_alarms, Is.Empty, "no more alarms after it breaks");
        }

        [Test]
        public void TheHealthComesFromTheSpecAndIsNotAKill()
        {
            var camera = Place(new SecurityCameraSpec(maxHealth: 80));

            Assert.That(camera.Damageable.MaxHealth, Is.EqualTo(80));
            Assert.That(camera.Damageable.Health.Current, Is.EqualTo(80));
            Assert.That(camera.Damageable.Id, Is.EqualTo(CameraId));
            Assert.That(camera.Damageable.ReportsKills, Is.False, "not in the kill feed");
        }

        [Test]
        public void HoldingInteractStopsTheCameraForTheDisabledSeconds()
        {
            var camera = Place();
            PlacePlayer();

            camera.Tick(Step, true);
            var first = ShownHint(camera);
            Assert.That(first, Is.EqualTo(new SecurityCameraHint(1.5f, 0f, true, SecurityCamera.InteractActionPath)), "the hold starts once its own hint is shown");

            Run(camera, 1.25f, held: true);
            Assert.That(camera.HeldSeconds, Is.EqualTo(1.25f));
            Assert.That(ShownHint(camera).Progress, Is.EqualTo(1.25f / 1.5f).Within(1e-5f));
            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Tracking), "it has been watching for 1.5 s");
            Assert.That(KeySharing.IsClaimed(SecurityCamera.InteractActionPath), Is.True);

            LogAssert.Expect(LogType.Log, new Regex(@"^\[SecurityCamera\] camera=cam-test phase=Disabled from=Tracking seconds=30\.00$"));
            camera.Tick(Step, true);
            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Disabled), "the 1.5 s hold ends");
            Assert.That(camera.HeldSeconds, Is.Zero);
            Assert.That(ShownHint(camera), Is.EqualTo(new SecurityCameraHint(1.5f, 0f, false, SecurityCamera.InteractActionPath)));
            Assert.That(KeySharing.IsClaimed(SecurityCamera.InteractActionPath), Is.False, "holding does nothing on a stopped camera");

            Run(camera, 29.75f);
            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Disabled));
            Assert.That(camera.Sweep.DisabledSecondsLeft, Is.EqualTo(0.25f));
            Assert.That(_alarms, Is.Empty, "the player stood in front for 30 s while it was off");

            camera.Tick(Step, false);
            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Tracking), "back on at 30 s with the meter where it stopped");
            Assert.That(_alarms, Is.Empty);

            camera.Tick(Step, false);
            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Spotted));
            Assert.That(_alarms, Has.Count.EqualTo(1));
            Assert.That(ShownHint(camera).CanDisable, Is.True);
        }

        [Test]
        public void ReleasingTheHoldStartsItOver()
        {
            var camera = Place(new SecurityCameraSpec(disableHoldSeconds: 1f));
            PlacePlayer(Stealth.Off);

            camera.Tick(Step, true);
            Run(camera, 0.75f, held: true);
            Assert.That(camera.HeldSeconds, Is.EqualTo(0.75f));

            camera.Tick(Step, false);
            Assert.That(camera.HeldSeconds, Is.Zero);
            Assert.That(ShownHint(camera).Progress, Is.Zero);

            Run(camera, 0.75f, held: true);
            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Sweeping), "0.75 s is short of the 1 s hold");

            camera.Tick(Step, true);
            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Disabled));
            Assert.That(camera.Sweep.DisabledSecondsLeft, Is.EqualTo(SecurityCameraSpec.DefaultDisabledSeconds));
        }

        [Test]
        public void KeepingTheKeyDownDoesNotStopItAgainWhenItComesBack()
        {
            var camera = Place(new SecurityCameraSpec(disableHoldSeconds: 0.5f, disabledSeconds: 1f));
            PlacePlayer(Stealth.Off);

            Run(camera, 0.75f, held: true);
            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Disabled));

            Run(camera, 3f, held: true);
            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Sweeping), "back on after 1 s and not stopped again");
            Assert.That(camera.HeldSeconds, Is.Zero);

            camera.Tick(Step, false);
            Run(camera, 0.5f, held: true);
            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Disabled), "a fresh hold stops it again");
        }

        [Test]
        public void TheHoldOnlyCountsWithinReach()
        {
            var camera = Place();
            PlacePlayer(Stealth.Off, new Vector3(0f, 1f, 3f));

            Run(camera, 3f, held: true);

            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Sweeping));
            Assert.That(camera.HeldSeconds, Is.Zero);
            Assert.That(GameSignals.InteractHint.HasValue, Is.False, "2.675 m is beyond the 2.5 m reach");
        }

        [Test]
        public void TheHoldDoesNotCountWhileAnotherHintIsShown()
        {
            var camera = Place();
            PlacePlayer(Stealth.Off);
            camera.Tick(Step, false);
            Assert.That(ShownHint(camera).CanDisable, Is.True);

            // 弾薬箱の案内をカメラの後に出すのは、先に出すとカメラが最初の Tick で自分の案内に出し直してしまうため。
            var other = new object();
            GameSignals.InteractHint.Publish(new InteractHint(other, new AmmoCrateHint(true, SecurityCamera.InteractActionPath)));

            Run(camera, 3f, held: true);

            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Sweeping), "the key belongs to the other hint");
            Assert.That(camera.HeldSeconds, Is.Zero);
            Assert.That(GameSignals.InteractHint.Value.Value.Source, Is.SameAs(other));
        }

        [Test]
        public void DisableWithoutHoldingUsesTheTunedSeconds()
        {
            var camera = Place(new SecurityCameraSpec(disabledSeconds: 12f));

            Assert.That(camera.Disable(), Is.True);

            Assert.That(camera.Phase, Is.EqualTo(SecurityCameraPhase.Disabled));
            Assert.That(camera.Sweep.DisabledSecondsLeft, Is.EqualTo(12f));
        }

        [Test]
        public void WithAnEmptyCatalogTheCameraIsABoxAndATube()
        {
            var empty = ScriptableObject.CreateInstance<SecurityCameraCatalog>();
            _created.Add(empty);
            var camera = Place(catalog: empty);

            var head = camera.transform.Find(SecurityCamera.HeadName);
            Assert.That(head, Is.Not.Null);
            Assert.That(camera.Head, Is.SameAs(head));

            var body = head.Find(SecurityCamera.BodyName);
            Assert.That(body, Is.Not.Null);
            Assert.That(body.GetComponent<MeshFilter>().sharedMesh.name, Does.StartWith("Cube"));
            Assert.That(body.GetComponent<MeshRenderer>(), Is.Not.Null);
            Assert.That(body.localScale, Is.EqualTo(SecurityCamera.BodySize));
            Assert.That(body.GetComponent<BoxCollider>(), Is.Not.Null);
            Assert.That(body.GetComponent<Hitbox>().Owner, Is.SameAs(camera.Damageable));

            var lens = head.Find(SecurityCamera.LensName);
            Assert.That(lens, Is.Not.Null);
            Assert.That(lens.GetComponent<MeshFilter>().sharedMesh.name, Does.StartWith("Cylinder"));
            Assert.That(lens.localPosition, Is.EqualTo(SecurityCamera.LensLocalPosition));
            Assert.That(lens.localScale, Is.EqualTo(SecurityCamera.LensScale));
            Assert.That(lens.GetComponent<Collider>(), Is.Null, "only the box is shot");
            Assert.That(Vector3.Dot(lens.up, camera.transform.forward), Is.EqualTo(1f).Within(1e-4f), "the tube points forward");

            Assert.That(head.Find(SecurityCamera.VisualName), Is.Null);
            Assert.That(Vector3.Distance(camera.Eye, new Vector3(0f, 1f, 0.325f)), Is.LessThan(1e-5f), "the eye is just past the tube");
        }

        [Test]
        public void WithoutACatalogTheCameraIsABoxAndATube()
        {
            var camera = Place();

            Assert.That(camera.Head.Find(SecurityCamera.BodyName).GetComponent<MeshRenderer>(), Is.Not.Null);
            Assert.That(camera.Head.Find(SecurityCamera.LensName), Is.Not.Null);
        }

        [Test]
        public void TheCatalogPrefabReplacesTheShapeButKeepsTheBoxToShoot()
        {
            var holder = new GameObject("ArtHolder");
            holder.transform.position = new Vector3(0f, -100f, 0f);
            _created.Add(holder);
            var art = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            art.name = "CameraArt";
            art.transform.SetParent(holder.transform, false);
            var catalog = ScriptableObject.CreateInstance<SecurityCameraCatalog>();
            _created.Add(catalog);
            catalog.CameraPrefab = art;

            var camera = Place(catalog: catalog);

            var visual = camera.Head.Find(SecurityCamera.VisualName);
            Assert.That(visual, Is.Not.Null);
            Assert.That(visual.GetComponentsInChildren<Collider>(true), Is.Empty, "the art does not stop bullets");
            Assert.That(camera.Head.Find(SecurityCamera.LensName), Is.Null);

            var body = camera.Head.Find(SecurityCamera.BodyName);
            Assert.That(body, Is.Not.Null);
            Assert.That(body.GetComponent<Renderer>(), Is.Null, "the box is not drawn over the art");
            Assert.That(body.GetComponent<BoxCollider>().size, Is.EqualTo(SecurityCamera.BodySize));
            Assert.That(body.GetComponent<Hitbox>().Owner, Is.SameAs(camera.Damageable));
        }

        [Test]
        public void InvalidUseIsRejected()
        {
            var camera = Place();

            camera.Initialize("Rebuilt", SecurityCameraSpec.Default, null);
            Assert.That(camera.Id, Is.EqualTo("Rebuilt"), "a second Initialize rebuilds with the new id");
            Assert.That(camera.transform.Find(SecurityCamera.HeadName).Find(SecurityCamera.BodyName), Is.Not.Null);
            Assert.That(camera.transform.Find(SecurityCamera.HeadName).Find(SecurityCamera.LensName), Is.Not.Null);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => camera.Tick(-1f, false));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => camera.Tick(float.NaN, false));

            var bare = new GameObject("Bare");
            _created.Add(bare);
            var uninitialized = bare.AddComponent<SecurityCamera>();
            Assert.Throws<System.InvalidOperationException>(() => uninitialized.Tick(Step, false));
            Assert.Throws<System.ArgumentNullException>(() => uninitialized.Initialize(CameraId, null, null));
            Assert.That(uninitialized.Id, Is.EqualTo("Bare"), "an empty id falls back to the object name");
        }
    }
}

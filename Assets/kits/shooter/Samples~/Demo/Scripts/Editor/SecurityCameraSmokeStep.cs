using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.Editor
{
    // batchmode には入力が無いので、長押しは SecurityCamera を止めて Update の代わりに Tick を押したままで呼び、撃つのは Hitscan.Fire で撃つ。
    // 見つかったらプレイヤーを出現位置へ戻す。カメラの前に残ると、調べに来た見張りに見つかって撃たれる。
    public sealed class SecurityCameraSmokeStep : ITpsActionSmokeStep
    {
        // 調べに来たとみなすのに、見張りが見つかった場所へ近づく距離。着く前に気づきが下がって止まることがあるので、着くまでは待たない。
        public const float InvestigateProgress = 2f;

        // 遠い見張りが立ち位置から動いたとみなす距離。NavMesh に寄せる分の揺れを見込む。
        public const float FarGuardDriftLimit = 0.5f;

        // 撃つ所はカメラの前の、首の箱に外れずに当たる近さにする。
        public const float ShotDistance = 2f;
        public const int MaxShots = 10;

        // 止める秒数 (SecurityCameraSpec.DisableHoldSeconds) を過ぎても止まらないときに見切るまでの猶予。案内を出す 1 フレームの遅れを見込む。
        public const float HoldGraceSeconds = 1f;

        // 止めたカメラの前に立つ秒数は、東のカメラが見つけるまでにかかった秒数のこの倍にする。止まっていなければ確実に見つかる長さ。
        public const float WatchFactor = 2f;
        public const float MinWatchSeconds = 3f;

        private enum Phase
        {
            Setup,
            WakeUp,
            Spotting,
            Alarm,
            Investigating,
            Shooting,
            Holding,
            Watching,
            Done,
        }

        private readonly List<string> _lines = new List<string>();
        private Phase _phase;
        private int _mark;
        private GameObject _root;
        private SecurityCamera _east;
        private SecurityCamera _west;
        private SecurityAlarm _alarm;
        private EnemyAgent _near;
        private EnemyAgent _far;
        private EnemyStealth _nearStealth;
        private EnemyStealth _farStealth;
        private Vector3 _spot;
        private float _nearStart;
        private float _spottingSince;
        private float _spotSeconds;
        private int _shots;
        private float _lastTickAt;
        private float _heldSeconds;
        private float _watchSince;
        private float _watchSeconds;

        public int Order => SecurityCameraRangeFixture.FixtureOrder;

        public string Name => "security-camera";

        public float TimeoutSeconds => 60f;

        public void Reset()
        {
            _lines.Clear();
            _phase = Phase.Setup;
            _mark = 0;
            _root = null;
            _east = null;
            _west = null;
            _alarm = null;
            _near = null;
            _far = null;
            _nearStealth = null;
            _farStealth = null;
            _spot = Vector3.zero;
            _nearStart = 0f;
            _spottingSince = 0f;
            _spotSeconds = 0f;
            _shots = 0;
            _lastTickAt = 0f;
            _heldSeconds = 0f;
            _watchSince = 0f;
            _watchSeconds = 0f;
        }

        public void OnLog(string condition)
        {
            if (condition == null)
            {
                return;
            }

            if (condition.StartsWith(SecurityCameraLog.Tag, StringComparison.Ordinal)
                || condition.StartsWith(SecurityAlarmLog.Tag, StringComparison.Ordinal)
                || condition.StartsWith(SecurityAlarmLog.ReceivedPrefix(SecurityCameraRangeFixture.NearGuardId), StringComparison.Ordinal)
                || condition.StartsWith(SecurityAlarmLog.ReceivedPrefix(SecurityCameraRangeFixture.FarGuardId), StringComparison.Ordinal)
                || condition.StartsWith(EnemyStealth.StageLogPrefix(SecurityCameraRangeFixture.NearGuardId), StringComparison.Ordinal)
                || condition.StartsWith(EnemyStealth.StageLogPrefix(SecurityCameraRangeFixture.FarGuardId), StringComparison.Ordinal))
            {
                _lines.Add(condition);
            }
        }

        public bool Advance(BootstrapWorld world, out string problem)
        {
            problem = null;
            switch (_phase)
            {
                case Phase.Setup:
                    problem = Setup(world);
                    return false;
                case Phase.WakeUp:
                    problem = WakeUp(world);
                    return false;
                case Phase.Spotting:
                    problem = Spotting(world);
                    return false;
                case Phase.Alarm:
                    problem = Alarm();
                    return false;
                case Phase.Investigating:
                    problem = Investigating();
                    return false;
                case Phase.Shooting:
                    problem = Shooting(world);
                    return false;
                case Phase.Holding:
                    problem = Holding(world);
                    return false;
                case Phase.Watching:
                    problem = Watching();
                    return _phase == Phase.Done;
                default:
                    return true;
            }
        }

        public void Cleanup()
        {
            // 残すと、この後の手順で見張りがプレイヤーを撃ち、カメラが警報を鳴らす。
            foreach (var guard in new[] { _near, _far })
            {
                if (guard != null)
                {
                    guard.gameObject.SetActive(false);
                }
            }

            foreach (var camera in new[] { _east, _west })
            {
                if (camera != null)
                {
                    camera.enabled = true;
                    camera.gameObject.SetActive(false);
                }
            }

            if (_alarm != null)
            {
                _alarm.Silence();
            }
        }

        private string Setup(BootstrapWorld world)
        {
            _root = GameObject.Find(SecurityCameraRangeFixture.RootName);
            if (_root == null)
            {
                return $"the scene has no '{SecurityCameraRangeFixture.RootName}'; rerun {TpsRangeSceneBuilder.MenuPath}";
            }

            var navMesh = _root.GetComponent<NavMeshBakeActivator>();
            if (navMesh == null || !navMesh.IsBaked)
            {
                return $"'{SecurityCameraRangeFixture.RootName}' did not bake its NavMesh";
            }

            _east = FindCamera(SecurityCameraRangeFixture.EastCameraId);
            _west = FindCamera(SecurityCameraRangeFixture.WestCameraId);
            _alarm = _root.GetComponentInChildren<SecurityAlarm>(true);
            _near = FindGuard(SecurityCameraRangeFixture.NearGuardId);
            _far = FindGuard(SecurityCameraRangeFixture.FarGuardId);
            if (_east == null || _west == null || _alarm == null || _near == null || _far == null)
            {
                return $"'{SecurityCameraRangeFixture.RootName}' is missing its cameras, alarm or guards; rerun {TpsRangeSceneBuilder.MenuPath}";
            }

            if (_east.gameObject.activeSelf || _west.gameObject.activeSelf || _near.gameObject.activeSelf || _far.gameObject.activeSelf)
            {
                return "the security camera yard was awake before its step; the cameras and the guards must wait inactive";
            }

            if (!_alarm.isActiveAndEnabled || !_alarm.IsListening)
            {
                return $"'{SecurityCameraRangeFixture.AlarmName}' is not listening for the cameras";
            }

            var stealth = world.Player.GetComponent<StealthTarget>();
            if (stealth == null || stealth.Rule != Stealth.On)
            {
                return $"the player has no StealthTarget with stealth On; the TPS range rules must turn stealth on (rerun {GameRulesAssetBuilder.MenuPath}; got {PlayerActions.FormatLog(world.Rules.Actions)})";
            }

            // 半径は警報ごとに持たず隠密の調整値から読むので、置き場所の前提 (近い見張りは内、遠い見張りは外) が今の値でも成り立つかを先に確かめる。
            var radius = StealthActionInstaller.Tuning.Alarm.Radius;
            var stand = SecurityCameraRangeFixture.EastStand;
            if (!(Flat(stand, SecurityCameraRangeFixture.NearGuardGround) <= radius) || !(Flat(stand, SecurityCameraRangeFixture.FarGuardGround) > radius))
            {
                return $"the alarm radius {Number(radius)} m does not reach '{SecurityCameraRangeFixture.NearGuardId}' while leaving out '{SecurityCameraRangeFixture.FarGuardId}' from the east camera's stand";
            }

            // 前の手順の立ち位置 (武器の棚など) は東のカメラの視野に入るので、起こす前に離す。先に見つかると、鳴り始めの行が数える前に出てしまう。
            Teleport(world.Controller, GameBootstrap.PlayerSpawn);
            _alarm.Silence();
            foreach (var guard in new[] { _near, _far })
            {
                guard.gameObject.SetActive(true);
                if (!guard.IsInitialized)
                {
                    return $"'{guard.name}' did not initialize when it woke up";
                }

                guard.enabled = true;
            }

            _east.gameObject.SetActive(true);
            _west.gameObject.SetActive(true);
            _nearStealth = _near.GetComponent<EnemyStealth>();
            _farStealth = _far.GetComponent<EnemyStealth>();
            if (_nearStealth == null || _farStealth == null)
            {
                return "the yard's guards have no EnemyStealth to hear the alarm with";
            }

            _phase = Phase.WakeUp;
            return null;
        }

        // 見張りは自分の Update で相手のルールを読むので、On になってから見つからせる。Off のままだと警報は調べに行かせても段階の行が出ない。
        private string WakeUp(BootstrapWorld world)
        {
            if (!_east.IsInitialized || !_west.IsInitialized)
            {
                return null;
            }

            if (_nearStealth.Awareness == null || _farStealth.Awareness == null
                || _nearStealth.Rule != Stealth.On || _farStealth.Rule != Stealth.On)
            {
                return null;
            }

            if (_nearStealth.Stage != AwarenessStage.Unaware || _farStealth.Stage != AwarenessStage.Unaware)
            {
                return $"the yard's guards were not unaware when they woke up ({_nearStealth.Stage}, {_farStealth.Stage})";
            }

            _mark = _lines.Count;
            Teleport(world.Controller, SecurityCameraRangeFixture.EastStand + new Vector3(0f, GameBootstrap.PlayerSpawn.y, 0f));
            FaceTowards(world, _east.transform.position);
            _spottingSince = Time.time;
            _phase = Phase.Spotting;
            return null;
        }

        private string Spotting(BootstrapWorld world)
        {
            var quiet = WestProblem();
            if (quiet != null)
            {
                return quiet;
            }

            var spotted = IndexSince(_mark, line => line.StartsWith(PhasePrefix(SecurityCameraRangeFixture.EastCameraId, SecurityCameraPhase.Spotted), StringComparison.Ordinal));
            if (spotted < 0)
            {
                return null;
            }

            _spotSeconds = Time.time - _spottingSince;
            Teleport(world.Controller, GameBootstrap.PlayerSpawn);
            Debug.Log(SecurityCameraSmokeLog.Spotted(SecurityCameraRangeFixture.EastCameraId, _spotSeconds));
            _mark = spotted;
            _phase = Phase.Alarm;
            return null;
        }

        private string Alarm()
        {
            var far = FarProblem();
            if (far != null)
            {
                return far;
            }

            var raised = SecurityAlarmLog.Prefix(SecurityCameraRangeFixture.EastCameraId) + " raised ";
            if (IndexSince(_mark, line => line.StartsWith(raised, StringComparison.Ordinal)) < 0 || !_alarm.Ringing)
            {
                return null;
            }

            var received = SecurityAlarmLog.ReceivedPrefix(SecurityCameraRangeFixture.NearGuardId) + $"camera={SecurityCameraRangeFixture.EastCameraId} ";
            var at = IndexSince(_mark, line => line.StartsWith(received, StringComparison.Ordinal));
            if (at < 0)
            {
                return null;
            }

            if (!_lines[at].Contains($" outcome={HearingOutcome.Investigating} "))
            {
                return $"'{SecurityCameraRangeFixture.NearGuardId}' heard the alarm but did not go to investigate ('{_lines[at]}')";
            }

            _spot = _alarm.State.SpottedAt.ToUnity();
            _nearStart = Flat(_near.GroundPosition, _spot);
            Debug.Log(SecurityCameraSmokeLog.AlarmRaised(SecurityCameraRangeFixture.EastCameraId, _alarm.State.Spec.Radius));
            _phase = Phase.Investigating;
            return null;
        }

        private string Investigating()
        {
            var far = FarProblem();
            if (far != null)
            {
                return far;
            }

            if (_nearStealth.Stage == AwarenessStage.Engaged)
            {
                return $"'{SecurityCameraRangeFixture.NearGuardId}' engaged instead of investigating the alarm (lines: {string.Join(" | ", Since(_mark))})";
            }

            var remaining = Flat(_near.GroundPosition, _spot);
            var approached = _nearStart - remaining;
            if (approached < InvestigateProgress && remaining > _near.Brain.Spec.ArrivalDistance)
            {
                return null;
            }

            Debug.Log(SecurityCameraSmokeLog.Investigated(SecurityCameraRangeFixture.NearGuardId, SecurityCameraRangeFixture.FarGuardId, approached));

            // 調べ終えて戻る見張りが、この後で西のカメラへ向かうプレイヤーを見つけないよう眠らせる。
            _near.gameObject.SetActive(false);
            _mark = _lines.Count;
            _phase = Phase.Shooting;
            return null;
        }

        private string Shooting(BootstrapWorld world)
        {
            var destroyed = IndexSince(_mark, line => line.StartsWith(PhasePrefix(SecurityCameraRangeFixture.EastCameraId, SecurityCameraPhase.Destroyed), StringComparison.Ordinal));
            if (destroyed >= 0)
            {
                Debug.Log(SecurityCameraSmokeLog.Destroyed(SecurityCameraRangeFixture.EastCameraId, _shots));
                _east = null;
                return BeginHold(world);
            }

            // 耐久が 0 になった弾でカメラは隠れるので、行が出るまでは撃ち足さない。
            if (_east == null || !_east.gameObject.activeInHierarchy)
            {
                return null;
            }

            if (_shots >= MaxShots)
            {
                return $"'{SecurityCameraRangeFixture.EastCameraId}' was not destroyed after {_shots.ToString(CultureInfo.InvariantCulture)} shots (hp {DescribeHealth(_east.Damageable)})";
            }

            var spec = world.Weapon != null && world.Weapon.Weapon != null ? world.Weapon.Weapon.Spec : null;
            if (spec == null || spec.Damage <= 0)
            {
                return "the player has no weapon that deals damage to shoot the camera with";
            }

            var target = _east.Head.position;
            var origin = target + (_east.transform.forward * ShotDistance);
            var result = Hitscan.Fire(
                origin,
                target - origin,
                Mathf.Max(spec.Range, ShotDistance * 2f),
                spec.Damage,
                spec.Falloff,
                world.Player.transform,
                0f,
                spec.Id);
            _shots++;
            var collider = result.RaycastHit.collider;
            if (!result.Hit || collider == null || collider.GetComponentInParent<SecurityCamera>() != _east)
            {
                return $"the shot from {origin} did not hit '{SecurityCameraRangeFixture.EastCameraId}'";
            }

            return null;
        }

        private string BeginHold(BootstrapWorld world)
        {
            var quiet = WestProblem();
            if (quiet != null)
            {
                return quiet;
            }

            Teleport(world.Controller, SecurityCameraRangeFixture.WestBeside + new Vector3(0f, GameBootstrap.PlayerSpawn.y, 0f));

            // 止めている間は Update に押していないと数えさせないよう、コンポーネントを止めて Tick を押したままで呼ぶ。
            _west.enabled = false;
            _lastTickAt = Time.time;
            _heldSeconds = 0f;
            _phase = Phase.Holding;
            return null;
        }

        private string Holding(BootstrapWorld world)
        {
            var quiet = WestProblem();
            if (quiet != null)
            {
                return quiet;
            }

            var now = Time.time;
            var delta = now - _lastTickAt;
            if (!(delta > 0f))
            {
                return null;
            }

            _lastTickAt = now;
            _west.Tick(delta, true);
            _heldSeconds += delta;

            var hold = _west.Spec.DisableHoldSeconds;
            if (_west.Phase != SecurityCameraPhase.Disabled)
            {
                if (_heldSeconds > hold + HoldGraceSeconds)
                {
                    var reach = Vector3.Distance(_west.Eye, world.PlayerDamageable.AimPoint);
                    return $"holding interact for {Number(_heldSeconds)}s did not disable '{SecurityCameraRangeFixture.WestCameraId}' (reach {Number(reach)} m of {Number(_west.Spec.DisableRadius)} m, held {Number(_west.HeldSeconds)}s)";
                }

                return null;
            }

            if (IndexSince(_mark, line => line.StartsWith(PhasePrefix(SecurityCameraRangeFixture.WestCameraId, SecurityCameraPhase.Disabled), StringComparison.Ordinal)) < 0)
            {
                return $"'{SecurityCameraRangeFixture.WestCameraId}' was disabled without logging it";
            }

            if (_heldSeconds < hold)
            {
                return $"'{SecurityCameraRangeFixture.WestCameraId}' was disabled after {Number(_heldSeconds)}s of holding (needs {Number(hold)}s)";
            }

            _watchSeconds = Mathf.Max(MinWatchSeconds, _spotSeconds * WatchFactor);
            if (!(_watchSeconds < _west.Spec.DisabledSeconds))
            {
                return $"the disabled camera wakes after {Number(_west.Spec.DisabledSeconds)}s, before the {Number(_watchSeconds)}s the step stands in front of it";
            }

            _west.enabled = true;
            Teleport(world.Controller, SecurityCameraRangeFixture.WestStand + new Vector3(0f, GameBootstrap.PlayerSpawn.y, 0f));
            FaceTowards(world, _west.transform.position);
            _watchSince = Time.time;
            _phase = Phase.Watching;
            return null;
        }

        private string Watching()
        {
            var quiet = WestProblem();
            if (quiet != null)
            {
                return quiet;
            }

            if (_west.Phase != SecurityCameraPhase.Disabled)
            {
                return $"'{SecurityCameraRangeFixture.WestCameraId}' left Disabled ({_west.Phase}) while the player stood in front of it";
            }

            var watched = Time.time - _watchSince;
            if (watched < _watchSeconds)
            {
                return null;
            }

            Debug.Log(SecurityCameraSmokeLog.StayedQuiet(SecurityCameraRangeFixture.WestCameraId, _heldSeconds, watched));
            _far.gameObject.SetActive(false);
            _phase = Phase.Done;
            return null;
        }

        // 西のカメラは仕切りの向こうか、横か、止まった後にしかプレイヤーがいないので、追い始めることも鳴らすこともない。
        private string WestProblem()
        {
            var id = SecurityCameraRangeFixture.WestCameraId;
            var tracking = PhasePrefix(id, SecurityCameraPhase.Tracking);
            var spotted = PhasePrefix(id, SecurityCameraPhase.Spotted);
            var alarm = SecurityAlarmLog.Prefix(id) + " ";
            var at = IndexSince(0, line => line.StartsWith(tracking, StringComparison.Ordinal)
                || line.StartsWith(spotted, StringComparison.Ordinal)
                || line.StartsWith(alarm, StringComparison.Ordinal));
            return at < 0 ? null : $"'{id}' saw the player or raised the alarm ('{_lines[at]}')";
        }

        private string FarProblem()
        {
            var id = SecurityCameraRangeFixture.FarGuardId;
            var heard = IndexSince(0, line => line.StartsWith(SecurityAlarmLog.ReceivedPrefix(id), StringComparison.Ordinal));
            if (heard >= 0)
            {
                return $"'{id}' outside the alarm radius heard it ('{_lines[heard]}')";
            }

            if (_farStealth != null && _farStealth.Stage != AwarenessStage.Unaware)
            {
                return $"'{id}' outside the alarm radius became {_farStealth.Stage}";
            }

            var drift = Flat(_far.GroundPosition, SecurityCameraRangeFixture.FarGuardGround);
            return drift > FarGuardDriftLimit ? $"'{id}' outside the alarm radius moved {Number(drift)} m" : null;
        }

        private SecurityCamera FindCamera(string id)
        {
            foreach (var camera in _root.GetComponentsInChildren<SecurityCamera>(true))
            {
                if (string.Equals(camera.Id, id, StringComparison.Ordinal))
                {
                    return camera;
                }
            }

            return null;
        }

        private EnemyAgent FindGuard(string id)
        {
            foreach (var agent in _root.GetComponentsInChildren<EnemyAgent>(true))
            {
                if (string.Equals(agent.Id, id, StringComparison.Ordinal))
                {
                    return agent;
                }
            }

            return null;
        }

        // SecurityCameraLog は段階ごとの行の頭を公開しないので、Changed と同じ並び (camera・phase・from) の頭で探す。
        private static string PhasePrefix(string cameraId, SecurityCameraPhase phase)
        {
            return $"{SecurityCameraLog.Tag} camera={cameraId} phase={phase} from=";
        }

        private int IndexSince(int mark, Predicate<string> match)
        {
            for (var i = Math.Max(0, mark); i < _lines.Count; i++)
            {
                if (match(_lines[i]))
                {
                    return i;
                }
            }

            return -1;
        }

        private IEnumerable<string> Since(int mark)
        {
            for (var i = Math.Max(0, mark); i < _lines.Count; i++)
            {
                yield return _lines[i];
            }
        }

        private static string DescribeHealth(Damageable damageable)
        {
            return damageable != null && damageable.Health != null ? damageable.Health.Current.ToString(CultureInfo.InvariantCulture) : "?";
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            var delta = a - b;
            delta.y = 0f;
            return delta.magnitude;
        }

        private static string Number(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        // CharacterController は有効なままだと Transform の書き換えを次の移動で上書きするので、移す間だけ切る。
        private static void Teleport(CharacterController controller, Vector3 position)
        {
            var wasEnabled = controller.enabled;
            controller.enabled = false;
            controller.transform.position = position;
            controller.enabled = wasEnabled;
            Physics.SyncTransforms();
        }

        // FirstPersonLook が自分の角度を持っているので、Transform を直接回すと次の視点操作で戻る。
        private static void FaceTowards(BootstrapWorld world, Vector3 point)
        {
            var direction = point - world.Player.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f)
            {
                return;
            }

            var yaw = Quaternion.LookRotation(direction, Vector3.up).eulerAngles.y;
            var look = world.Look;
            if (look != null && look.IsInitialized)
            {
                look.ApplyLookDelta(new Vector2(Mathf.DeltaAngle(look.Yaw, yaw), 0f));
            }
            else
            {
                world.Player.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
        }
    }
}

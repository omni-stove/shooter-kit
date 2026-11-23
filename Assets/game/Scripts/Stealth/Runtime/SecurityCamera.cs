using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Runtime
{
    // 首振り・見つけるまでのゲージ・止める・壊れるは SecurityCameraSweep が決め、ここは見え方の材料を集め、首を回し、警報と案内を出すだけを持つ。
    // 根の正面 (transform.forward) が首振りの真ん中で、根の上向きを軸に首 (Head) を回す。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable))]
    public sealed class SecurityCamera : MonoBehaviour
    {
        public const string LogTag = SecurityCameraLog.Tag;

        public const string InteractActionPath = InputActionPaths.Interact;

        public const string HeadName = "Head";
        public const string BodyName = "Body";
        public const string LensName = "Lens";
        public const string VisualName = "Visual";

        // 見る相手の候補を集めるのにシーンの StealthTarget をすべて見るので、毎フレームではなくこの間隔で集め直す。
        public const float SearchIntervalSeconds = 0.25f;

        // 首の箱の大きさ。プレハブを使うときも撃たれる当たり判定はこの箱にして、見た目を替えても壊しやすさが変わらないようにする。
        public static readonly Vector3 BodySize = new Vector3(0.25f, 0.25f, 0.45f);

        // 筒の素の形は高さ 2 m・直径 1 m。前へ向けるので、高さ (y) が筒の長さになる。
        public static readonly Vector3 LensScale = new Vector3(0.14f, 0.05f, 0.14f);
        public static readonly Vector3 LensLocalPosition = new Vector3(0f, 0f, (BodySize.z / 2f) + 0.05f);

        // 目をレンズの先に置くのは、首の箱の中から視線を引くと、首の向きによっては自分の取り付け先の壁に当たるため。
        public static readonly Vector3 EyeLocalPosition = new Vector3(0f, 0f, (BodySize.z / 2f) + 0.1f);

        public static readonly Color DefaultBodyColor = new Color(0.82f, 0.82f, 0.8f);
        public static readonly Color DefaultLensColor = new Color(0.08f, 0.08f, 0.1f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField, Tooltip("ログと警報に出す識別子。空なら GameObject の名前")]
        private string _id = string.Empty;

        private readonly List<StealthTarget> _candidates = new List<StealthTarget>();
        private SecurityCameraSpec _spec;
        private SecurityCameraSweep _sweep;
        private Damageable _damageable;
        private Transform _head;
        private InteractHintSlot _hint;
        private float _searchIn;
        private float _heldSeconds;
        private bool _waitForRelease;

        // シーンに置いたまま再コンパイルすると、Unity が private の値も保存し直し、null の InputAction を空のアクションとして戻してしまう。
        [NonSerialized]
        private InputAction _interactAction;
        private bool _enabledInteractAction;

        public string Id => string.IsNullOrWhiteSpace(_id) ? name : _id.Trim();

        public bool IsInitialized => _sweep != null;

        public SecurityCameraSpec Spec => _spec;

        public SecurityCameraSweep Sweep => _sweep;

        public SecurityCameraPhase Phase => _sweep != null ? _sweep.Phase : SecurityCameraPhase.Sweeping;

        public Damageable Damageable => _damageable;

        public Transform Head => _head != null ? _head : transform;

        public Vector3 Eye => Head.TransformPoint(EyeLocalPosition);

        // 押し始めてからの秒数。止め終えるか、離すか、離れると 0 に戻る。
        public float HeldSeconds => _heldSeconds;

        public void Initialize(string id, SecurityCameraSpec spec, SecurityCameraCatalog catalog)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            // Awake が既定値で先に組み立てるので、直後にゲームが渡す Id・Spec・カタログで作り直せるようにする (TargetDummy と同じ)。
            if (_sweep != null)
            {
                _sweep.PhaseChanged -= OnPhaseChanged;
                if (_damageable != null)
                {
                    _damageable.Died -= OnDied;
                }

                ResetHold();
                _hint?.Clear();

                var oldVisual = _head != null ? _head.Find(VisualName) : null;
                if (oldVisual != null)
                {
                    oldVisual.SetParent(null, false);
                    if (Application.isPlaying)
                    {
                        Destroy(oldVisual.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(oldVisual.gameObject);
                    }
                }
            }

            if (id != null)
            {
                _id = id;
            }

            _spec = spec;
            _sweep = new SecurityCameraSweep(spec);
            _sweep.PhaseChanged += OnPhaseChanged;

            _damageable = GetComponent<Damageable>();
            if (_damageable == null)
            {
                _damageable = gameObject.AddComponent<Damageable>();
            }

            // カメラは倒す相手ではなく仕掛けなので、置いた盾 (Deployable) と同じくキルフィードや得点に載せない。
            _damageable.ReportsKills = false;
            _damageable.Initialize(Id, spec.MaxHealth);
            _damageable.Died += OnDied;

            Build(catalog != null ? catalog.CameraPrefab : null);
            _searchIn = 0f;
            ApplyYaw();
        }

        private void Awake()
        {
            if (!IsInitialized)
            {
                Initialize(null, StealthActionInstaller.Tuning.Camera, SecurityCameraCatalog.Load());
            }
        }

        // GameBootstrap は Awake で組み立てるので、シーンのルール (部位の倍率) は Start まで待って読む。
        private void Start()
        {
            if (_damageable != null)
            {
                _damageable.ApplySceneRules();
            }
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                AcquireInteractAction();
            }
        }

        private void OnDisable()
        {
            ReleaseInteractAction();
            ResetHold();
            _hint?.Clear();
        }

        private void OnDestroy()
        {
            if (_sweep != null)
            {
                _sweep.PhaseChanged -= OnPhaseChanged;
            }

            if (_damageable != null)
            {
                _damageable.Died -= OnDied;
            }

            _hint?.Clear();
        }

        private void Update()
        {
            if (IsInitialized)
            {
                Tick(Time.deltaTime, _interactAction != null && _interactAction.IsPressed());
            }
        }

        // Edit Mode では Update が走らないので、テストは押しているかを渡して手で進める。
        public void Tick(float deltaSeconds, bool interactHeld)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be non-negative and finite");
            }

            if (!IsInitialized)
            {
                throw new InvalidOperationException($"security camera '{name}' is not initialized");
            }

            if (_sweep.Phase == SecurityCameraPhase.Destroyed)
            {
                return;
            }

            _searchIn -= deltaSeconds;
            if (_searchIn <= 0f)
            {
                _searchIn = SearchIntervalSeconds;
                CollectCandidates();
            }

            _sweep.Tick(deltaSeconds, ChooseTarget());
            if (_sweep.Phase == SecurityCameraPhase.Destroyed)
            {
                return;
            }

            ApplyYaw();
            UpdateDisableHold(deltaSeconds, interactHeld);
        }

        // 長押しを通さずに止める口。警報の仕掛けやモードがカメラを止めるときに使う。
        public bool Disable()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException($"security camera '{name}' is not initialized");
            }

            ResetHold();
            return _sweep.Disable();
        }

        // Off の相手は見ない。発見ゲージを使わないモードでカメラが警報を鳴らし、敵を呼び寄せないように。
        public bool TryDescribe(StealthTarget candidate, out SecurityCameraTarget target)
        {
            target = SecurityCameraTarget.None;
            if (!IsInitialized || candidate == null || !candidate.isActiveAndEnabled || candidate.Rule != Stealth.On)
            {
                return false;
            }

            var body = candidate.GetComponentInParent<Damageable>();
            if (body == null || body.IsDead || body.IsDowned || body == _damageable)
            {
                return false;
            }

            var eye = Eye;
            var toTarget = body.AimPoint - eye;
            var distance = toTarget.magnitude;
            if (distance > _spec.ViewDistance)
            {
                return false;
            }

            var up = transform.up;
            var flat = Vector3.ProjectOnPlane(toTarget, up);
            var forward = Vector3.ProjectOnPlane(transform.forward, up);
            var bearing = flat.sqrMagnitude < 1e-8f || forward.sqrMagnitude < 1e-8f ? 0f : Vector3.SignedAngle(forward, flat, up);

            // Raycast を引く前に、視線が通るとしてたまる量を測る。0 なら遮るものを確かめなくても見えないので、相手ごとの Raycast を省ける。
            var seen = Describe(true, distance, bearing, body, candidate);
            if (!(_sweep.GainPerSecond(seen) > 0f))
            {
                return false;
            }

            if (!Hitscan.HasLineOfSight(eye, body, _spec.ViewDistance + 0.01f, transform))
            {
                return false;
            }

            target = seen;
            return true;
        }

        private static SecurityCameraTarget Describe(bool hasLineOfSight, float distance, float bearing, Damageable body, StealthTarget stealth)
        {
            return new SecurityCameraTarget(
                hasLineOfSight,
                distance,
                bearing,
                body.GroundPoint.ToNumerics(),
                stealth.Posture,
                Mathf.Clamp01(stealth.LightFactor),
                stealth.Speed,
                stealth.IsArmed,
                stealth.IsLit);
        }

        private SecurityCameraTarget ChooseTarget()
        {
            var best = SecurityCameraTarget.None;
            var bestGain = 0f;
            foreach (var candidate in _candidates)
            {
                if (!TryDescribe(candidate, out var target))
                {
                    continue;
                }

                var gain = _sweep.GainPerSecond(target);
                if (gain > bestGain)
                {
                    best = target;
                    bestGain = gain;
                }
            }

            return best;
        }

        private void CollectCandidates()
        {
            _candidates.Clear();
            _candidates.AddRange(FindObjectsByType<StealthTarget>(FindObjectsInactive.Exclude));
        }

        private void UpdateDisableHold(float deltaSeconds, bool interactHeld)
        {
            if (!interactHeld)
            {
                _waitForRelease = false;
            }

            var player = LocalPlayerInReach();
            if (player == null)
            {
                ResetHold();
                _hint?.Clear();
                return;
            }

            // 案内が他の物 (拾える物など) のものになっている間は数えない。押した 1 回で両方が動かないように。
            var canDisable = _sweep.IsActive;
            if (canDisable && interactHeld && !_waitForRelease && ShowsOwnHint())
            {
                _heldSeconds += deltaSeconds;
                if (_heldSeconds >= _spec.DisableHoldSeconds)
                {
                    // 押したままにしていても、明けた後にすぐ止め直さないよう、一度離すまで数え直さない。
                    ResetHold();
                    _waitForRelease = true;
                    _sweep.Disable();
                    canDisable = _sweep.IsActive;
                }
            }
            else
            {
                _heldSeconds = 0f;
            }

            var hold = _spec.DisableHoldSeconds;
            var hint = new SecurityCameraHint(hold, Mathf.Clamp01(_heldSeconds / hold), canDisable, InteractActionPath);
            (_hint ?? (_hint = new InteractHintSlot(this))).Show(hint, canDisable);
        }

        private Damageable LocalPlayerInReach()
        {
            var eye = Eye;
            var reach = _spec.DisableRadius;
            foreach (var candidate in _candidates)
            {
                if (candidate == null || !candidate.isActiveAndEnabled)
                {
                    continue;
                }

                var body = candidate.GetComponentInParent<Damageable>();
                if (body == null || !body.IsLocalPlayer || body.IsDead || body.IsDowned)
                {
                    continue;
                }

                // 壁越しに止められないよう、届く距離の中でも目から体が見えるときだけにする。
                if (Vector3.Distance(eye, body.AimPoint) <= reach && Hitscan.HasLineOfSight(eye, body, reach + 0.01f, transform))
                {
                    return body;
                }
            }

            return null;
        }

        private bool ShowsOwnHint()
        {
            var shown = GameSignals.InteractHint.Value;
            return shown.HasValue && ReferenceEquals(shown.Value.Source, this);
        }

        private void ResetHold()
        {
            _heldSeconds = 0f;
        }

        private void ApplyYaw()
        {
            if (_head != null && _sweep != null)
            {
                _head.localRotation = Quaternion.Euler(0f, _sweep.YawDegrees, 0f);
            }
        }

        private void OnPhaseChanged(SecurityCameraPhase from, SecurityCameraPhase to)
        {
            Debug.Log(SecurityCameraLog.Changed(Id, _sweep, from), this);
            if (to == SecurityCameraPhase.Spotted)
            {
                SecurityAlarmBus.Raise(_sweep.SpottedAt.ToUnity(), Id);
            }
        }

        private void OnDied(Damageable self)
        {
            self.Died -= OnDied;
            ResetHold();
            _hint?.Clear();
            _sweep.Destroy();

            // Damageable は Died を知らせた後もこの GameObject を使うので、すぐには消さない。再生中の Destroy はフレームの終わりまで待つ。
            // その間の弾や視線が壊れたカメラに当たらないよう、先に隠す。Edit Mode では Destroy を呼べないので、隠すだけにする。
            gameObject.SetActive(false);
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
        }

        private void Build(GameObject prefab)
        {
            var head = transform.Find(HeadName);
            if (head == null)
            {
                head = new GameObject(HeadName).transform;
                head.SetParent(transform, false);
            }

            _head = head;
            if (prefab != null)
            {
                // 撃たれるのは下の箱の当たり判定で受けるので、見た目の当たり判定は外す (残すと部位の分からない所で弾が止まる)。
                if (head.Find(VisualName) == null)
                {
                    VisualPrefab.Instantiate(prefab, head, VisualName, stripColliders: true);
                }

                EnsureBody(head, visible: false);
                return;
            }

            EnsureBody(head, visible: true);
            if (head.Find(LensName) == null)
            {
                var lens = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                lens.name = LensName;
                lens.transform.SetParent(head, false);
                lens.transform.localPosition = LensLocalPosition;
                lens.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                lens.transform.localScale = LensScale;

                // 当たり判定を箱だけにして、プレハブを使うときと壊しやすさをそろえる。Destroy は次のフレームまで残り、その間の弾が当たるのですぐ消す。
                DestroyImmediate(lens.GetComponent<Collider>());
                Paint(lens.GetComponent<Renderer>(), DefaultLensColor);
            }
        }

        private void EnsureBody(Transform head, bool visible)
        {
            var body = head.Find(BodyName);
            if (body == null)
            {
                GameObject part;
                if (visible)
                {
                    part = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    part.transform.localScale = BodySize;
                }
                else
                {
                    part = new GameObject();
                    part.AddComponent<BoxCollider>().size = BodySize;
                }

                part.name = BodyName;
                part.transform.SetParent(head, false);
                body = part.transform;
            }

            var hitbox = body.GetComponent<Hitbox>();
            if (hitbox == null)
            {
                hitbox = body.gameObject.AddComponent<Hitbox>();
            }

            hitbox.Initialize(HitZone.Body, _damageable);
            if (visible)
            {
                Paint(body.GetComponent<Renderer>(), DefaultBodyColor);
            }
        }

        // 共有マテリアルを塗り替えると他のプリミティブまで色が変わるので、この形だけの上書きで色を付ける。
        private static void Paint(Renderer renderer, Color color)
        {
            if (renderer == null)
            {
                return;
            }

            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(block);
        }

        private void AcquireInteractAction()
        {
            if (_interactAction != null)
            {
                return;
            }

            var actions = InputSystem.actions;
            var action = actions != null ? actions.FindAction(InteractActionPath) : null;
            if (action == null)
            {
                Debug.LogError(SecurityCameraLog.NoInteractAction(Id, InteractActionPath), this);
                return;
            }

            _interactAction = action;

            // プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたときだけ後で無効に戻す。
            if (!_interactAction.enabled)
            {
                _interactAction.Enable();
                _enabledInteractAction = true;
            }
        }

        private void ReleaseInteractAction()
        {
            if (_interactAction == null)
            {
                return;
            }

            if (_enabledInteractAction)
            {
                _interactAction.Disable();
            }

            _enabledInteractAction = false;
            _interactAction = null;
        }
    }
}

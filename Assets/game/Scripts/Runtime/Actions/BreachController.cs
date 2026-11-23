using System;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Runtime
{
    // ドアの Update (既定の順) が仕掛けの秒数を進め終えてから、同じフレームのうちに取りやめと進み具合を見る。
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(110)]
    public sealed class BreachController : MonoBehaviour
    {
        public const string LogTag = BreachLog.Tag;

        public const string InteractActionPath = InputActionPaths.Interact;

        public const string TimeScaleReason = "breach";

        public const string RefusedNoCharge = "no-charge";

        public const string CancelledLeft = "left";

        public const string CancelledStaggered = "staggered";

        public const string CancelledDead = "dead";

        public const string CancelledVehicle = "vehicle";

        public const string CancelledDisabled = "disabled";

        // 正面のドアを見つけるための線の長さ。届くかはドアごとの BreachSpec.Reach で決めるので、それより長ければよい。
        public const float ProbeDistance = 8f;

        private SlowMotion _slowMotion = SlowMotion.Off;
        private bool _configured;
        private bool _subscribed;
        private Damageable _self;
        private WeaponController _weapon;
        private PlayerInventory _inventory;
        private PlayerStagger _stagger;
        private PlayerVehicleRider _rider;
        private ViewRig _rig;
        private InteractHintSlot _hint;

        private SquadOrderButton _button;
        private float _buttonHoldSeconds;
        private float _clock;
        private bool _wasDown;
        private bool _ownsPress;

        private BreachDoor _target;
        private BreachDoor _kickDoor;
        private float _kickRemaining;
        private BreachDoor _plantDoor;
        private Collider[] _plantColliders = Array.Empty<Collider>();
        private string _spentItemId;
        private BreachDoor _armedDoor;
        private bool _holstered;
        private BreachStatus? _published;

        // シーンに置いたまま再コンパイルすると、Unity が private の値も保存し直し、null の InputAction を空のアクションとして戻してしまう。
        [NonSerialized]
        private InputAction _interactAction;
        private bool _enabledInteractAction;

        public bool IsConfigured => _configured;

        public SlowMotion SlowMotion
        {
            get => _slowMotion;
            set => _slowMotion = RequireRule(value);
        }

        public BreachDoor Target => _target;

        public BreachDoor KickingDoor => _kickDoor;

        public BreachDoor PlantingDoor => _plantDoor;

        public BreachDoor ArmedDoor => _armedDoor;

        public bool IsKicking => _kickDoor != null;

        public bool IsPlanting => _plantDoor != null;

        public bool IsHolstering => _holstered;

        // 取り付けの後で体の部品が付け替わっても拾えるよう、Configure のたびに探し直す。
        public void Configure(SlowMotion slowMotion)
        {
            _slowMotion = RequireRule(slowMotion);
            _self = GetComponent<Damageable>();
            _weapon = GetComponentInChildren<WeaponController>(true);
            _inventory = GetComponent<PlayerInventory>();
            _stagger = GetComponent<PlayerStagger>();
            _rider = GetComponent<PlayerVehicleRider>();
            _rig = GetComponent<ViewRig>();
            _hint = _hint ?? new InteractHintSlot(this);
            _configured = true;
            Subscribe();
        }

        // Edit Mode では OnDisable が走らないので、テストからも後片付けできるよう分けておく。
        public void Shutdown()
        {
            Disengage(CancelledDisabled);
            Unsubscribe();
        }

        public void Tick(bool interactDown, float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            if (!_configured)
            {
                Configure(_slowMotion);
            }

            _clock += deltaSeconds;
            var blocked = BlockReason();
            TickPlant(blocked);
            TickArmed();
            TickKick(blocked, deltaSeconds);

            var busy = _kickDoor != null || _plantDoor != null;
            _target = busy || blocked != null ? null : Probe();
            ShowHint();
            ReadGesture(interactDown);
            PublishStatus();
        }

        public static float DistanceTo(Vector3 point, Collider[] colliders, Vector3 fallback)
        {
            var nearest = float.PositiveInfinity;
            if (colliders != null)
            {
                foreach (var collider in colliders)
                {
                    if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy)
                    {
                        nearest = Mathf.Min(nearest, Vector3.Distance(point, collider.ClosestPoint(point)));
                    }
                }
            }

            return float.IsPositiveInfinity(nearest) ? Vector3.Distance(point, fallback) : nearest;
        }

        private void OnEnable()
        {
            if (_configured)
            {
                Subscribe();
            }

            if (Application.isPlaying)
            {
                AcquireInteractAction();
            }
        }

        private void OnDisable()
        {
            Shutdown();
            ReleaseInteractAction();
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        private void LateUpdate()
        {
            if (!_configured)
            {
                return;
            }

            // ポーズ画面のボタンを押したつもりで蹴らないよう、開いている間は押していないことにする。
            var down = !GameUi.IsPauseOpen && KeySharing.IsPressed(_interactAction);
            Tick(down, Time.deltaTime);
        }

        private void TickPlant(string blocked)
        {
            if (_plantDoor == null)
            {
                return;
            }

            if (!_plantDoor.isActiveAndEnabled)
            {
                CancelPlant(CancelledDisabled);
                return;
            }

            if (_plantDoor.Phase != BreachDoorPhase.Planting)
            {
                // 仕掛け終えた爆薬は取り外せないので、ここからは武器を戻して離れてよい。
                _armedDoor = _plantDoor.Phase == BreachDoorPhase.Armed ? _plantDoor : null;
                FinishPlant();
                return;
            }

            var reason = blocked;
            if (reason == null && DistanceTo(EyeOrigin(), _plantColliders, _plantDoor.Visuals.PanelCenter()) > _plantDoor.Spec.Reach)
            {
                reason = CancelledLeft;
            }

            if (reason != null)
            {
                CancelPlant(reason);
            }
        }

        private void TickArmed()
        {
            if (_armedDoor != null && (!_armedDoor.isActiveAndEnabled || _armedDoor.Phase != BreachDoorPhase.Armed))
            {
                _armedDoor = null;
            }
        }

        private void TickKick(string blocked, float deltaSeconds)
        {
            if (_kickDoor == null)
            {
                return;
            }

            var door = _kickDoor;
            var reason = blocked;
            if (reason == null && (!door.isActiveAndEnabled || !InReach(door)))
            {
                reason = CancelledLeft;
            }

            if (reason != null)
            {
                _kickDoor = null;
                Debug.Log(BreachLog.KickCancelled(door.Id, ActorId, reason), this);
                return;
            }

            _kickRemaining -= deltaSeconds;
            if (_kickRemaining > 0f)
            {
                return;
            }

            _kickDoor = null;
            door.Kick(gameObject);
        }

        private void ReadGesture(bool down)
        {
            var wasDown = _wasDown;
            _wasDown = down;

            // 押している途中で怯んだりドアが外れたりしたら、離したときに蹴らないよう、その押下は離すまで使わない。
            if (_target == null)
            {
                DropPress();
                return;
            }

            // 押し始めたときに出ていた案内が自分のものでなければ、その押下は案内を出している物 (拾う物など) のものにする。
            if (down && !wasDown)
            {
                _ownsPress = HintIsMine();
            }

            if (!_ownsPress)
            {
                return;
            }

            var button = ButtonFor(_target.Spec);
            var gesture = button.Tick(down, _clock);
            if (!down)
            {
                _ownsPress = false;
            }

            switch (gesture)
            {
                case SquadOrderGesture.Tap:
                    BeginKick(_target);
                    break;
                case SquadOrderGesture.Hold:
                    DropPress();
                    TryBeginPlant(_target);
                    break;
            }
        }

        private void DropPress()
        {
            _ownsPress = false;
            _button?.Reset();
        }

        private SquadOrderButton ButtonFor(BreachSpec spec)
        {
            if (_button == null || !_buttonHoldSeconds.Equals(spec.HoldSeconds))
            {
                _button = new SquadOrderButton(spec.HoldSeconds);
                _buttonHoldSeconds = spec.HoldSeconds;
            }

            return _button;
        }

        private void BeginKick(BreachDoor door)
        {
            var seconds = door.Spec.KickSeconds;
            Debug.Log(BreachLog.KickBegan(door.Id, ActorId, seconds), this);
            if (seconds <= 0f)
            {
                door.Kick(gameObject);
                return;
            }

            _kickDoor = door;
            _kickRemaining = seconds;
        }

        private bool TryBeginPlant(BreachDoor door)
        {
            var spec = door.Spec;
            var itemId = spec.ChargeItemId;
            if (spec.NeedsChargeItem && ChargeCount(itemId) < 1)
            {
                Debug.Log(BreachLog.Refused(door.Id, ActorId, BreachMethod.Charge, door.Phase, RefusedNoCharge), this);
                return false;
            }

            if (!door.BeginCharge(gameObject))
            {
                return false;
            }

            _spentItemId = null;
            if (spec.NeedsChargeItem)
            {
                var inventory = _inventory.Inventory;
                inventory.Remove(itemId, 1);
                _spentItemId = itemId;
                Debug.Log(BreachLog.ChargeSpent(door.Id, ActorId, itemId, inventory.CountOf(itemId)), this);
            }

            switch (door.Phase)
            {
                case BreachDoorPhase.Planting:
                    _plantDoor = door;
                    _plantColliders = door.Visuals.PanelColliders();
                    Holster();
                    break;
                case BreachDoorPhase.Armed:
                    _armedDoor = door;
                    _spentItemId = null;
                    break;
                default:
                    _spentItemId = null;
                    break;
            }

            return true;
        }

        private void CancelPlant(string reason)
        {
            var door = _plantDoor;
            var itemId = _spentItemId;
            if (door != null && door.CancelCharge(reason) && itemId != null)
            {
                var returned = _inventory != null && _inventory.Inventory.Add(itemId, 1) > 0;
                Debug.Log(BreachLog.ChargeReturned(door.Id, ActorId, itemId, returned), this);
            }

            FinishPlant();
        }

        private void FinishPlant()
        {
            _plantDoor = null;
            _plantColliders = Array.Empty<Collider>();
            _spentItemId = null;
            Unholster();
        }

        private void Disengage(string reason)
        {
            if (_plantDoor != null)
            {
                CancelPlant(reason);
            }

            if (_kickDoor != null)
            {
                Debug.Log(BreachLog.KickCancelled(_kickDoor.Id, ActorId, reason), this);
                _kickDoor = null;
            }

            _armedDoor = null;
            _target = null;
            _ownsPress = false;
            _wasDown = false;
            _button?.Reset();
            _hint?.Clear();
            PublishStatus();
        }

        private void Holster()
        {
            if (_weapon != null && _weapon.Holster.Claim(this))
            {
                _holstered = true;
            }
        }

        private void Unholster()
        {
            if (_holstered && _weapon != null)
            {
                _weapon.Holster.Release(this);
            }

            _holstered = false;
        }

        private string BlockReason()
        {
            if (_self != null && (_self.IsDead || _self.IsDowned))
            {
                return CancelledDead;
            }

            if (_rider != null && _rider.IsRiding)
            {
                return CancelledVehicle;
            }

            if (_stagger != null && _stagger.IsStaggered)
            {
                return CancelledStaggered;
            }

            return null;
        }

        private BreachDoor Probe()
        {
            if (!Hitscan.TryFirstHit(EyeOrigin(), AimForward(), ProbeDistance, transform, out var hit))
            {
                return null;
            }

            var door = hit.collider.GetComponentInParent<BreachDoor>();
            if (door == null || !door.isActiveAndEnabled)
            {
                return null;
            }

            if (!door.IsBound)
            {
                door.Bind();
            }

            return door.Phase == BreachDoorPhase.Closed && hit.distance <= door.Spec.Reach ? door : null;
        }

        private bool InReach(BreachDoor door)
        {
            return DistanceTo(EyeOrigin(), door.Visuals.PanelColliders(), door.Visuals.PanelCenter()) <= door.Spec.Reach;
        }

        private void ShowHint()
        {
            if (_target == null)
            {
                _hint.Clear();
                return;
            }

            var spec = _target.Spec;
            var canPlant = !spec.NeedsChargeItem || ChargeCount(spec.ChargeItemId) > 0;
            _hint.Show(new BreachHint(true, canPlant, spec.ChargeItemId, InteractActionPath));
        }

        private bool HintIsMine()
        {
            var shown = GameSignals.InteractHint.Value;
            return shown.HasValue && ReferenceEquals(shown.Value.Source, this);
        }

        private int ChargeCount(string itemId)
        {
            return _inventory != null ? _inventory.Inventory.CountOf(itemId) : 0;
        }

        private void PublishStatus()
        {
            var door = _plantDoor != null ? _plantDoor : _armedDoor;
            if (door == null)
            {
                Unpublish();
                return;
            }

            var status = door.Status;
            if (_published == status && GameSignals.Breach.HasValue)
            {
                return;
            }

            _published = status;
            GameSignals.Breach.Publish(status);
        }

        // 後から別の物が出した進み具合までは消さない。
        private void Unpublish()
        {
            if (_published.HasValue)
            {
                GameSignals.Breach.ClearIfCurrent(_published);
                _published = null;
            }
        }

        private void OnDoorBreached(DoorBreach breach)
        {
            if (breach.Breacher != gameObject || _slowMotion != SlowMotion.On)
            {
                return;
            }

            var door = breach.Door != null ? breach.Door.GetComponent<BreachDoor>() : null;
            var spec = door != null && door.IsBound ? door.Spec : KitTuning.Breach;
            if (!spec.SlowsMotion)
            {
                return;
            }

            TimeScaleArbiter.Request(TimeScaleReason, spec.SlowMotionScale, spec.SlowMotionSeconds);
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            CombatEvents.DoorBreached += OnDoorBreached;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            CombatEvents.DoorBreached -= OnDoorBreached;
            _subscribed = false;
        }

        // 三人称ではカメラが体の後ろにあるので、線の起点はカメラでなく目 (ViewRig が無ければ体) にする。MeleeController と同じ。
        private Vector3 EyeOrigin()
        {
            return _rig != null && _rig.IsInitialized ? _rig.Eye.position : transform.position;
        }

        // 足元のドアを見下ろしていても正面のドアに届くよう、狙っている向きを水平にして使う。
        private Vector3 AimForward()
        {
            var aim = _weapon != null ? _weapon.Aim : null;
            var forward = aim != null ? aim.forward : transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f)
            {
                forward = transform.forward;
                forward.y = 0f;
            }

            return forward.sqrMagnitude < 1e-6f ? Vector3.forward : forward.normalized;
        }

        private string ActorId => _self != null ? _self.Id : string.Empty;

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
                Debug.LogError($"{LogTag} '{InteractActionPath}' が Input Actions に無いため、'{name}' はドアを破れません (Project Settings > Input System Package)。", this);
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

        private static SlowMotion RequireRule(SlowMotion rule)
        {
            if (rule != SlowMotion.Off && rule != SlowMotion.On)
            {
                throw new ArgumentOutOfRangeException(nameof(rule), rule, "unknown slow motion rule");
            }

            return rule;
        }
    }
}

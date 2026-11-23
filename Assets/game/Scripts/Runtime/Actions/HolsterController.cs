using System;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Runtime
{
    // オフラインの体だけで動く。ネット対戦ではサーバーが状態と時間を持つ。
    // WeaponController (実行順 0) の Update より先に回すのは、同じフレームの射撃とリロードを、このフレームで変えた状態で止めるため。
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10)]
    public sealed class HolsterController : MonoBehaviour, IWeaponModifier
    {
        public const string LogTag = HolsterLog.Tag;

        public const string HolsterActionPath = InputActionPaths.Holster;

        public const string DiedReason = "died";

        public const string DisabledReason = "disabled";

        public const string UnarmedReason = "unarmed";

        private const float NeutralMoveSpeedMultiplier = 1f;

        private readonly HolsterState _state = new HolsterState();

        private HolsterSpec _spec;
        private bool _specApplied;
        private WeaponController _weapon;
        private Damageable _self;
        private Damageable _diedSource;
        private bool _hooked;
        private bool _claimed;
        private int _lastIndex = -1;
        private bool _waitForAttackRelease;
        private HolsterStatus? _published;

        [NonSerialized]
        private InputAction _holsterAction;
        private bool _enabledHolsterAction;
        private bool _resolvedAction;

        public HolsterSpec Spec
        {
            get => _spec ?? HolsterSpec.Default;
            set
            {
                _spec = value ?? throw new ArgumentNullException(nameof(value));
                _specApplied = true;
                Publish();
            }
        }

        public WeaponController Weapon => _weapon;

        public bool IsInitialized => _weapon != null;

        public HolsterPhase Phase => _state.Phase;

        public float Lowering => _state.Lowering;

        public float RemainingSeconds => _state.RemainingSeconds;

        public bool IsHolstered => _state.Phase == HolsterPhase.Holstered;

        // bool でなく名乗る形にするのは、重なった場所の片方から出たときに、もう片方の中で構えられるようにならないため。
        public ControlHandoff BlockDraw { get; } = new ControlHandoff();

        public bool IsDrawBlocked => BlockDraw.IsHandedOff;

        // 構える秒数が 0 の武器や押したまま構え終えたときに、構え直しの押下で撃ってしまわないよう、離すまで止める。
        public bool IsWaitingForAttackRelease => _waitForAttackRelease;

        public bool IsHeldByOthers => _weapon != null && _weapon.Holster.IsHandedOff && !_weapon.Holster.IsHeldBy(this);

        public HolsterStatus Status => _state.Status(Spec, IsHeldByOthers);

        public InputAction HolsterAction
        {
            get => _holsterAction;
            set
            {
                ReleaseHolsterAction();
                _holsterAction = value;
                _resolvedAction = true;
                AcquireHolsterAction();
            }
        }

        public bool BlocksFire => _hooked && (_state.Phase != HolsterPhase.Drawn || _waitForAttackRelease);

        public bool BlocksReload => _hooked && _state.Phase != HolsterPhase.Drawn;

        public float SpreadMultiplier => 1f;

        public float ModifyConeDegrees(WeaponSpec spec, float coneDegrees)
        {
            return coneDegrees;
        }

        public float ModifySpreadFraction(float spreadFraction)
        {
            return spreadFraction;
        }

        private string HolderId => gameObject.name;

        private bool IsDead => _self != null && _self.IsDead;

        public void Configure(HolsterSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            Debug.Log(HolsterLog.Loaded(spec), this);
        }

        public void Initialize(WeaponController weapon, Damageable self)
        {
            if (weapon == null)
            {
                throw new ArgumentNullException(nameof(weapon));
            }

            if (_weapon != null && _weapon != weapon)
            {
                ResetToDrawn(null);
                Unhook();
            }

            if (!_specApplied)
            {
                _spec = KitTuning.Holster;
                _specApplied = true;
            }

            _weapon = weapon;
            _self = self;
            _lastIndex = weapon.CurrentIndex;
            SyncHooks();
            Apply();
        }

        public bool Holster()
        {
            if (!_hooked || IsDead)
            {
                return false;
            }

            var loadout = _weapon.Loadout;
            if (loadout == null)
            {
                return false;
            }

            var index = loadout.CurrentIndex;
            var seconds = loadout.HolsterSecondsOf(index);
            if (seconds <= 0f)
            {
                seconds = loadout.DrawSecondsOf(index);
            }

            if (!_state.BeginHolster(seconds))
            {
                return false;
            }

            Debug.Log(HolsterLog.Began(HolderId, HolsterPhase.Holstering, seconds), this);
            SettleIfDone();
            Apply();
            return true;
        }

        public bool Draw()
        {
            if (!_hooked || IsDrawBlocked)
            {
                return false;
            }

            var loadout = _weapon.Loadout;
            var seconds = loadout != null ? loadout.DrawSecondsOf(loadout.CurrentIndex) : 0f;
            if (!_state.BeginDraw(seconds))
            {
                return false;
            }

            ReleaseClaim();
            Debug.Log(HolsterLog.Began(HolderId, HolsterPhase.Drawing, seconds), this);
            SettleIfDone();
            Apply();
            return true;
        }

        public bool Toggle()
        {
            return _state.Phase == HolsterPhase.Drawn || _state.Phase == HolsterPhase.Drawing ? Holster() : Draw();
        }

        // 入力を読む Update から分けるのは、Edit Mode のテストがボタンと時間を渡して進められるようにするため。
        public void Tick(float deltaSeconds, bool holsterPressed, bool attackPressed, bool attackHeld)
        {
            if (!_hooked)
            {
                return;
            }

            if (!attackHeld)
            {
                _waitForAttackRelease = false;
            }

            FollowWeaponSwitch();

            if (!IsDead)
            {
                if (holsterPressed)
                {
                    Toggle();
                }
                else if (attackPressed && _state.Phase != HolsterPhase.Drawn)
                {
                    _waitForAttackRelease = true;
                    if (_state.Phase == HolsterPhase.Holstering || _state.Phase == HolsterPhase.Holstered)
                    {
                        Draw();
                    }
                }
            }

            if (_state.Tick(deltaSeconds))
            {
                OnSettled(_state.Phase);
            }

            Apply();
        }

        private void Update()
        {
            if (!_hooked)
            {
                return;
            }

            ReadInput(out var holsterPressed, out var attackPressed, out var attackHeld);
            Tick(Time.deltaTime, holsterPressed, attackPressed, attackHeld);
        }

        // 乗り物などで入力を手放している間・ポーズ中・死んでいる間は、押していないことにする。
        private void ReadInput(out bool holsterPressed, out bool attackPressed, out bool attackHeld)
        {
            holsterPressed = false;
            attackPressed = false;
            attackHeld = false;
            if (!_resolvedAction)
            {
                ResolveHolsterAction();
            }

            if (_weapon.IsInputHandedOff || GameUi.IsPauseOpen || IsDead)
            {
                return;
            }

            holsterPressed = _holsterAction != null && _holsterAction.enabled && _holsterAction.WasPressedThisFrame();
            var attack = _weapon.AttackAction;
            if (attack != null && attack.enabled)
            {
                attackPressed = attack.WasPressedThisFrame();
                attackHeld = attack.IsPressed();
            }
        }

        // 持ち替えは WeaponController が Loadout で進めるので、ここでは CurrentIndex の変化を見て構え直す。
        private void FollowWeaponSwitch()
        {
            var index = _weapon.CurrentIndex;
            if (index == _lastIndex)
            {
                return;
            }

            var previous = _lastIndex;
            _lastIndex = index;
            if (index < 0)
            {
                ResetToDrawn(UnarmedReason);
                return;
            }

            if (previous >= 0 && (_state.Phase == HolsterPhase.Holstering || _state.Phase == HolsterPhase.Holstered))
            {
                Draw();
            }
        }

        private void SettleIfDone()
        {
            if (!_state.IsMoving)
            {
                OnSettled(_state.Phase);
            }
        }

        private void OnSettled(HolsterPhase phase)
        {
            if (phase == HolsterPhase.Holstered)
            {
                _claimed |= _weapon.Holster.Claim(this);
            }

            Debug.Log(HolsterLog.Settled(HolderId, phase), this);
            CombatEvents.RaiseHolsterChanged(gameObject, phase);
        }

        private void ResetToDrawn(string reason)
        {
            var was = _state.Phase;
            _state.ResetToDrawn();
            _waitForAttackRelease = false;
            ReleaseClaim();
            if (was == HolsterPhase.Drawn)
            {
                return;
            }

            if (reason != null)
            {
                Debug.Log(HolsterLog.Reset(HolderId, reason), this);
            }

            CombatEvents.RaiseHolsterChanged(gameObject, HolsterPhase.Drawn);
        }

        private void ReleaseClaim()
        {
            if (!_claimed)
            {
                return;
            }

            _claimed = false;
            if (!ReferenceEquals(_weapon, null))
            {
                _weapon.Holster.Release(this);
            }
        }

        private void Apply()
        {
            if (_weapon == null)
            {
                return;
            }

            _weapon.HolsterLowering = _state.Lowering;
            _weapon.HolsterMoveSpeedMultiplier = _state.Phase == HolsterPhase.Holstered ? Spec.MoveSpeedMultiplier : NeutralMoveSpeedMultiplier;
            Publish();
        }

        // 変わらないフレームは出し直さない。毎フレーム購読側を起こさないため。
        private void Publish()
        {
            if (!_hooked)
            {
                return;
            }

            var status = Status;
            if (_published.HasValue && _published.Value == status)
            {
                return;
            }

            _published = status;
            GameSignals.Holster.Publish(status);
        }

        private void Unpublish()
        {
            if (_published.HasValue)
            {
                GameSignals.Holster.ClearIfCurrent(_published);
                _published = null;
            }
        }

        private void OnEnable()
        {
            SyncHooks();
            Apply();
        }

        private void OnDisable()
        {
            ResetToDrawn(DisabledReason);
            Unhook();
        }

        private void OnDestroy()
        {
            ResetToDrawn(DisabledReason);
            Unhook();
        }

        // Edit Mode では OnEnable が呼ばれないので、Initialize でもつなぐ。
        private void SyncHooks()
        {
            if (enabled && _weapon != null)
            {
                Hook();
            }
            else
            {
                Unhook();
            }
        }

        private void Hook()
        {
            if (!ReferenceEquals(_diedSource, _self))
            {
                UnlistenDied();
                if (_self != null)
                {
                    _self.Died += OnDied;
                    _diedSource = _self;
                }
            }

            if (_hooked)
            {
                return;
            }

            _weapon.AddModifier(this);
            AcquireHolsterAction();
            _hooked = true;
        }

        private void Unhook()
        {
            UnlistenDied();
            if (!_hooked)
            {
                return;
            }

            _hooked = false;
            if (!ReferenceEquals(_weapon, null))
            {
                _weapon.RemoveModifier(this);
            }

            // 破棄済みの WeaponController へは書けないので、生きているときだけ下げ具合と倍率を戻す。
            if (_weapon != null)
            {
                _weapon.HolsterLowering = 0f;
                _weapon.HolsterMoveSpeedMultiplier = NeutralMoveSpeedMultiplier;
            }

            Unpublish();
            ReleaseHolsterAction();
        }

        private void UnlistenDied()
        {
            // 破棄済みでも C# のオブジェクトは残っているので、購読は外せる。
            if (!ReferenceEquals(_diedSource, null))
            {
                _diedSource.Died -= OnDied;
            }

            _diedSource = null;
        }

        private void OnDied(Damageable _)
        {
            ResetToDrawn(DiedReason);
            Apply();
        }

        private void ResolveHolsterAction()
        {
            _resolvedAction = true;
            AppliedInputBindings.EnsureLoaded();
            var actions = InputSystem.actions;
            var action = actions != null ? actions.FindAction(HolsterActionPath) : null;
            if (action == null)
            {
                Debug.LogWarning(HolsterLog.NoInput(HolsterActionPath), this);
                return;
            }

            _holsterAction = action;
            AcquireHolsterAction();
        }

        // プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたものだけ後で無効に戻す。
        private void AcquireHolsterAction()
        {
            if (_holsterAction != null && !_enabledHolsterAction && !_holsterAction.enabled && Application.isPlaying && isActiveAndEnabled)
            {
                _holsterAction.Enable();
                _enabledHolsterAction = true;
            }
        }

        private void ReleaseHolsterAction()
        {
            if (_enabledHolsterAction && _holsterAction != null)
            {
                _holsterAction.Disable();
            }

            _enabledHolsterAction = false;
        }
    }
}

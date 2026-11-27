using System;
using GameKit.Runtime;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Runtime
{
    // オフラインの体だけで動く。ネット対戦ではサーバーが点灯を持ち、見た目は WeaponLightBeam だけを使う。
    [DisallowMultipleComponent]
    public sealed class WeaponLightController : MonoBehaviour, IWeaponLightStatusSource
    {
        public const string LightActionPath = InputActionPaths.WeaponLight;

        public const string DiedReason = "died";

        public const string UnarmedReason = "unarmed";

        public const string HolsteredReason = "holstered";

        public const string NoLightReason = "no-light";

        private readonly WeaponLightState _state = new WeaponLightState();

        private WeaponController _weapon;
        private Damageable _self;
        private HolsterController _holster;
        private WeaponController _listenedWeapon;
        private Damageable _diedSource;
        private bool _hooked;
        private WeaponLightStatus? _published;
        private WeaponLightBeam _beam;

        private WeaponLightCatalog _catalog;
        private bool _catalogResolved;
        private WeaponPresentation _presentation;
        private bool _presentationResolved;

        private bool _lightKnown;
        private string _lightWeaponId;
        private WeaponLightSpec _light;

        [NonSerialized]
        private InputAction _lightAction;
        private bool _enabledLightAction;
        private bool _resolvedAction;

        public WeaponController Weapon => _weapon;

        public HolsterController Holster => _holster;

        public bool IsInitialized => _weapon != null;

        public bool IsOn => _state.IsOn;

        public string LitWeaponId => _state.WeaponId;

        public WeaponLightSpec CurrentLight
        {
            get
            {
                var weapon = _weapon != null ? _weapon.Weapon : null;
                var weaponId = weapon != null ? weapon.Spec.Id : null;
                if (!_lightKnown || !string.Equals(weaponId, _lightWeaponId, StringComparison.Ordinal))
                {
                    _light = weapon != null ? FindLight(_weapon) : null;
                    _lightWeaponId = weaponId;
                    _lightKnown = true;
                }

                return _light;
            }
        }

        public WeaponLightStatus Status => _state.Status(CurrentLight != null);

        public WeaponLightStatus WeaponLightStatus => Status;

        public WeaponLightBeam Beam => _beam != null ? _beam : null;

        public WeaponLightCatalog Catalog
        {
            get
            {
                if (!_catalogResolved)
                {
                    _catalog = WeaponLightCatalog.Load();
                    _catalogResolved = true;
                }

                return _catalog;
            }
            set
            {
                _catalog = value;
                _catalogResolved = true;
                Apply();
            }
        }

        public WeaponPresentation Presentation
        {
            get
            {
                if (!_presentationResolved)
                {
                    _presentation = GetComponent<WeaponPresentation>();
                    _presentationResolved = true;
                }

                return _presentation;
            }
            set
            {
                _presentation = value;
                _presentationResolved = true;
                Apply();
            }
        }

        public InputAction LightAction
        {
            get => _lightAction;
            set
            {
                ReleaseLightAction();
                _lightAction = value;
                _resolvedAction = true;
                AcquireLightAction();
            }
        }

        private string HolderId => gameObject.name;

        private bool IsDead => _self != null && _self.IsDead;

        private string CurrentWeaponId
        {
            get
            {
                var weapon = _weapon != null ? _weapon.Weapon : null;
                return weapon != null ? weapon.Spec.Id : null;
            }
        }

        // WeaponController.IsHolstered でなく HolsterStatus を先に見るのは、しまい始めた時点で消し、梯子などほかの部品が預けたときの扱いも holster.json の調整値に合わせるため。
        private bool IsArmed =>
            _weapon != null
            && !_weapon.IsUnarmed
            && !IsDead
            && (_holster != null ? _holster.Status.IsArmed : !_weapon.IsHolstered);

        public void Initialize(WeaponController weapon, Damageable self, HolsterController holster = null)
        {
            if (weapon == null)
            {
                throw new ArgumentNullException(nameof(weapon));
            }

            if (_weapon != null && _weapon != weapon)
            {
                TurnOff(WeaponLightOffCause.Switched);
                Unhook();
            }

            _weapon = weapon;
            _self = self;
            _holster = holster != null ? holster : GetComponent<HolsterController>();
            _lightKnown = false;
            SyncHooks();
            Follow();
            Apply();
        }

        public bool Toggle()
        {
            if (!_hooked)
            {
                return false;
            }

            Follow();
            var litWeaponId = _state.WeaponId;
            var weaponId = CurrentWeaponId;
            var light = CurrentLight;
            var armed = IsArmed;
            if (!_state.Toggle(weaponId, light != null, armed))
            {
                Debug.Log(WeaponLightLog.NotLit(HolderId, weaponId, NotLitReason(weaponId, light, armed)), this);
                Apply();
                return false;
            }

            Debug.Log(WeaponLightLog.Toggled(HolderId, _state.IsOn ? weaponId : litWeaponId, _state.IsOn), this);
            Apply();
            return true;
        }

        // 入力を読む Update から分けるのは、Edit Mode のテストがボタンを渡して進められるようにするため。
        public void Tick(bool lightPressed)
        {
            if (!_hooked)
            {
                return;
            }

            Follow();
            if (lightPressed)
            {
                Toggle();
                return;
            }

            Apply();
        }

        private void Update()
        {
            if (!_hooked)
            {
                return;
            }

            Tick(ReadLightPressed());
        }

        // 乗り物などで入力を手放している間・ポーズ中・死んでいる間は、押していないことにする。
        private bool ReadLightPressed()
        {
            if (!_resolvedAction)
            {
                ResolveLightAction();
            }

            if (_weapon.IsInputHandedOff || GameUi.IsPauseOpen || IsDead)
            {
                return false;
            }

            return _lightAction != null && _lightAction.enabled && _lightAction.WasPressedThisFrame();
        }

        private static WeaponLightSpec FindLight(WeaponController weapon)
        {
            foreach (var attachment in weapon.EquippedAttachmentSpecs)
            {
                if (attachment.IsLight)
                {
                    return attachment.Light;
                }
            }

            return null;
        }

        private string NotLitReason(string weaponId, WeaponLightSpec light, bool armed)
        {
            if (IsDead)
            {
                return DiedReason;
            }

            if (string.IsNullOrEmpty(weaponId))
            {
                return UnarmedReason;
            }

            if (!armed)
            {
                return HolsteredReason;
            }

            return light == null ? NoLightReason : null;
        }

        private void Follow()
        {
            if (!_state.IsOn)
            {
                return;
            }

            if (IsDead)
            {
                TurnOff(WeaponLightOffCause.Died);
                return;
            }

            var litWeaponId = _state.WeaponId;
            var cause = _state.Follow(CurrentWeaponId, CurrentLight != null, IsArmed);
            if (cause.HasValue)
            {
                Debug.Log(WeaponLightLog.TurnedOff(HolderId, litWeaponId, cause.Value), this);
            }
        }

        private bool TurnOff(WeaponLightOffCause cause)
        {
            var litWeaponId = _state.WeaponId;
            if (!_state.TurnOff())
            {
                return false;
            }

            Debug.Log(WeaponLightLog.TurnedOff(HolderId, litWeaponId, cause), this);
            Apply();
            return true;
        }

        private void Apply()
        {
            ShowBeam();
            Publish();
        }

        // 消している間も光の部品を残すのは、点けるたびにプレハブを出し直さないため。武器のモデルを作り直すと子の光も消えるので、無ければ作り直す。
        private void ShowBeam()
        {
            var light = _hooked && _state.IsOn ? CurrentLight : null;
            if (light == null)
            {
                if (_beam != null)
                {
                    _beam.SetOn(false);
                }

                return;
            }

            var sockets = Presentation != null ? Presentation.Sockets : WeaponSockets.None;
            var fallback = _weapon.Aim != null ? _weapon.Aim : _weapon.transform;
            if (_beam == null)
            {
                _beam = WeaponLightBeam.Create(sockets, fallback, light, Catalog);
            }
            else
            {
                _beam.Place(sockets, fallback);
                _beam.Configure(light, light.VisualId, Catalog);
            }

            _beam.SetOn(true);
        }

        private void RemoveBeam()
        {
            if (_beam != null)
            {
                _beam.Remove();
            }

            _beam = null;
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
            GameSignals.WeaponLight.Publish(status);
        }

        private void Unpublish()
        {
            if (_published.HasValue)
            {
                GameSignals.WeaponLight.ClearIfCurrent(_published);
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
            TurnOff(WeaponLightOffCause.Disabled);
            Unhook();
        }

        private void OnDestroy()
        {
            TurnOff(WeaponLightOffCause.Disabled);
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

            _weapon.WeaponChanged += OnWeaponChanged;
            _weapon.AttachmentsChanged += OnAttachmentsChanged;
            _listenedWeapon = _weapon;
            AcquireLightAction();
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
            // 破棄済みでも C# のオブジェクトは残っているので、購読は外せる。
            if (!ReferenceEquals(_listenedWeapon, null))
            {
                _listenedWeapon.WeaponChanged -= OnWeaponChanged;
                _listenedWeapon.AttachmentsChanged -= OnAttachmentsChanged;
            }

            _listenedWeapon = null;
            RemoveBeam();
            Unpublish();
            ReleaseLightAction();
        }

        private void UnlistenDied()
        {
            if (!ReferenceEquals(_diedSource, null))
            {
                _diedSource.Died -= OnDied;
            }

            _diedSource = null;
        }

        // 持ち替えと付け外しは次の Update を待たずに追うのは、光が付いていない武器を同じフレームに照らさないため。
        private void OnWeaponChanged(Weapon _)
        {
            _lightKnown = false;
            Follow();
            Apply();
        }

        private void OnAttachmentsChanged(string _)
        {
            _lightKnown = false;
            Follow();
            Apply();
        }

        private void OnDied(Damageable _)
        {
            TurnOff(WeaponLightOffCause.Died);
            Apply();
        }

        private void ResolveLightAction()
        {
            _resolvedAction = true;
            AppliedInputBindings.EnsureLoaded();
            var actions = InputSystem.actions;
            var action = actions != null ? actions.FindAction(LightActionPath) : null;
            if (action == null)
            {
                Debug.LogWarning(WeaponLightLog.NoInput(LightActionPath), this);
                return;
            }

            _lightAction = action;
            AcquireLightAction();
        }

        // プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたものだけ後で無効に戻す。
        private void AcquireLightAction()
        {
            if (_lightAction != null && !_enabledLightAction && !_lightAction.enabled && Application.isPlaying && isActiveAndEnabled)
            {
                _lightAction.Enable();
                _enabledLightAction = true;
            }
        }

        private void ReleaseLightAction()
        {
            if (_enabledLightAction && _lightAction != null)
            {
                _lightAction.Disable();
            }

            _enabledLightAction = false;
        }
    }
}

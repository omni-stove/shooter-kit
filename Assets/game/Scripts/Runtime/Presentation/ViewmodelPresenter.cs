using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ViewmodelMotion))]
    public sealed class ViewmodelPresenter : MonoBehaviour
    {
        public const string LogTag = "[Viewmodel]";
        public const string SpawnedLogPrefix = LogTag + " spawned";
        public const string SwappedLogPrefix = LogTag + " swapped";
        public const string RemovedLogPrefix = LogTag + " removed";
        public const string AttachedLogPrefix = LogTag + " attachments";

        public const string ReasonThirdPerson = "third-person";
        public const string ReasonNotLocalView = "not-local-view";
        public const string ReasonDead = "dead";
        public const string ReasonUnarmed = "unarmed";
        public const string ReasonNoViewmodel = "no-viewmodel";
        public const string ReasonDisabled = "disabled";
        public const string ReasonHolstered = "holstered";
        public const string ReasonDowned = "downed";
        public const string ReasonSeated = "seated";

        private static readonly HashSet<string> _warnedWeaponIds = new HashSet<string>(StringComparer.Ordinal);
        private static bool _warnedMissingCatalog;

        private ViewmodelCatalog _catalog;
        private bool _catalogResolved;
        private bool _resolved;
        private ViewRig _rig;
        private WeaponController _weapon;
        private Damageable _damageable;
        private WeaponPresentation _presentation;
        private ViewmodelMotion _motion;
        private AimDownSights _ads;

        private GameObject _instance;
        private Vector3 _hipPosition;
        private Vector3 _adsPosition;
        private string _weaponId;
        private WeaponSockets _sockets = WeaponSockets.None;
        private ViewmodelEntry _entry;
        private IReadOnlyList<string> _attachments = WeaponAttachments.None;
        private AttachmentModelCatalog _attachmentModels;
        private bool _attachmentModelsResolved;
        private bool _hiddenBySight;
        private Material _paint;

        private GameObject _leftInstance;
        private Vector3 _leftHipPosition;
        private bool _leftMirrored;
        private WeaponSockets _leftSockets = WeaponSockets.None;

        // 歩行の揺れと追従遅れは軸 (ViewmodelMotion) で両手そろって揺らすので、手ごとには跳ね上がりだけを持つ。
        private readonly ViewmodelSway _rightKick = new ViewmodelSway();
        private readonly ViewmodelSway _leftKick = new ViewmodelSway();
        private WeaponController _hooked;

        public ViewmodelCatalog Catalog
        {
            get
            {
                if (!_catalogResolved)
                {
                    _catalog = ViewmodelCatalog.Load();
                    _catalogResolved = true;
                }

                return _catalog;
            }
            set
            {
                _catalog = value;
                _catalogResolved = true;
            }
        }

        // ネット対戦の本体のように WeaponController を持たない本体が、持っている武器の Id を渡すための差し込み口。
        public Func<string> WeaponIdSource { get; set; }

        public Func<string, IReadOnlyList<string>> AttachmentsSource { get; set; }

        // 死んだ・しまったを Damageable と WeaponController から読めない本体 (NetV2 の自分の体) は、隠す理由 (Reason*) をここから渡す。
        public Func<string> HiddenReasonSource { get; set; }

        public AttachmentModelCatalog AttachmentModels
        {
            get
            {
                if (!_attachmentModelsResolved)
                {
                    _attachmentModels = AttachmentModelCatalog.Load();
                    _attachmentModelsResolved = true;
                }

                return _attachmentModels;
            }
            set
            {
                _attachmentModels = value;
                _attachmentModelsResolved = true;
            }
        }

        // 迷彩の材質を渡す差し込み口。空なら着けている迷彩 (GameSignals.Cosmetics) を引く。
        public Func<Material> PaintSource { get; set; }

        public Material CurrentPaint => _instance != null ? _paint : null;

        public IReadOnlyList<string> CurrentAttachments => _instance != null ? _attachments : WeaponAttachments.None;

        public GameObject Instance => _instance;

        public string CurrentWeaponId => _instance != null ? _weaponId : null;

        public WeaponSockets Sockets => _instance != null ? _sockets : WeaponSockets.None;

        public bool IsHiddenBySight => _instance != null && _hiddenBySight;

        public GameObject LeftInstance => _instance != null && _leftInstance != null ? _leftInstance : null;

        public bool IsLeftMirrored => LeftInstance != null && _leftMirrored;

        public WeaponSockets LeftSockets => LeftInstance != null ? _leftSockets : WeaponSockets.None;

        public float HandKicks(AkimboHand hand)
        {
            return KickOf(hand).Kicks;
        }

        public Func<SightView> SightSource { get; set; }

        public ViewmodelMotion Motion
        {
            get
            {
                Resolve();
                return _motion;
            }
        }

        private void OnEnable()
        {
            if (NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
            {
                enabled = false;
                return;
            }

            if (_resolved)
            {
                HookFiredHand();
            }
        }

        private void OnDisable()
        {
            UnhookFiredHand();

            // 親ごと無効にされている最中は子の SetActive が許されないので、隠さずに壊すだけにする。
            Remove(ReasonDisabled, false);
        }

        private void LateUpdate()
        {
            StepHands(Time.deltaTime);
            Refresh();
        }

        public void StepHands(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            Resolve();
            var ads = Mathf.Clamp01(_motion.AdsProgress());
            _rightKick.Tick(deltaSeconds, 0f, 0f, 0f, ads);
            _leftKick.Tick(deltaSeconds, 0f, 0f, 0f, ads);
        }

        public void Refresh()
        {
            Resolve();
            var reason = HiddenReason();
            if (reason != null)
            {
                Remove(reason);
                return;
            }

            var wanted = WantedWeaponId();
            if (string.IsNullOrEmpty(wanted))
            {
                Remove(ReasonUnarmed);
                return;
            }

            var akimbo = HeldHands(wanted) != null;
            if (_instance != null && string.Equals(wanted, _weaponId, StringComparison.Ordinal) && akimbo == (_leftInstance != null))
            {
                var attachments = WantedAttachments(wanted);
                if (!WeaponAttachments.SameAs(attachments, _attachments))
                {
                    Attach(attachments);
                    HideForSight(_hiddenBySight, true);
                    Debug.Log($"{AttachedLogPrefix} weapon={wanted} attached={WeaponAttachments.Describe(attachments)} sight={_sockets.Sight != null}", this);
                }
                else
                {
                    var paint = WantedPaint();
                    if (paint != _paint)
                    {
                        ApplyPaint(paint);
                    }
                }

                PlaceForAim();
                return;
            }

            var entry = FindEntry(wanted);
            if (entry == null)
            {
                Remove(ReasonNoViewmodel);
                return;
            }

            Spawn(wanted, entry, akimbo);
            PlaceForAim();
        }

        // 持ち替えでしまい切ったときに、構えた位置から下げる距離 (m)。
        public const float LoweredDrop = 0.5f;

        public static Vector3 LoweredPosition(Vector3 held, float lowering)
        {
            return held + (Vector3.down * (LoweredDrop * Mathf.Clamp01(lowering)));
        }

        public static Vector3 AimedRootPosition(Vector3 adsOffset, Vector3 sightInModel)
        {
            return adsOffset - sightInModel;
        }

        private void PlaceForAim()
        {
            if (_instance == null)
            {
                return;
            }

            var progress = Mathf.Clamp01(_motion.AdsProgress());
            var lowering = _weapon != null ? _weapon.SwitchLowering : 0f;
            var right = Vector3.Lerp(_hipPosition, _adsPosition, progress);
            var hands = _leftInstance != null ? HeldHands(_weaponId) : null;
            if (hands == null)
            {
                _instance.transform.localPosition = LoweredPosition(right, lowering);
            }
            else
            {
                PlaceHand(_instance.transform, LoweredPosition(right, Mathf.Max(lowering, HandReloadLowering(hands, AkimboHand.Right))), _rightKick);
                PlaceHand(_leftInstance.transform, LoweredPosition(_leftHipPosition, Mathf.Max(lowering, HandReloadLowering(hands, AkimboHand.Left))), _leftKick);
            }

            HideForSight(CurrentSightView().HidesViewmodel, false);
        }

        // 込め始めと込め終わりで構えた位置に戻るよう、進みの半ばで一番深く下げる。
        public static float AkimboReloadLowering(bool reloading, float progress, float depth)
        {
            if (!reloading || float.IsNaN(progress) || float.IsNaN(depth))
            {
                return 0f;
            }

            return Mathf.Clamp01(depth) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(progress));
        }

        private float HandReloadLowering(AkimboHands hands, AkimboHand hand)
        {
            return AkimboReloadLowering(hands.IsHandReloading(hand), hands.ReloadProgress(hand), _entry != null ? _entry.AkimboReloadLowering : 0f);
        }

        private static void PlaceHand(Transform model, Vector3 position, ViewmodelSway kick)
        {
            model.localPosition = position + kick.Position.ToUnity();
            model.localRotation = Quaternion.Euler(-kick.PitchUpDegrees, 0f, 0f);
        }

        private ViewmodelSway KickOf(AkimboHand hand)
        {
            switch (hand)
            {
                case AkimboHand.Right:
                    return _rightKick;
                case AkimboHand.Left:
                    return _leftKick;
                default:
                    throw new ArgumentOutOfRangeException(nameof(hand), hand, "hand is not a defined AkimboHand");
            }
        }

        // WeaponIdSource で別の武器を見せている間は、持っている武器の手の組を当てない。
        private AkimboHands HeldHands(string weaponId)
        {
            var hands = _weapon != null && _weapon.IsInitialized ? _weapon.Akimbo : null;
            return hands != null && string.Equals(hands.Spec.Id, weaponId, StringComparison.Ordinal) ? hands : null;
        }

        private void HandleFiredHand(Weapon weapon, AkimboHand hand)
        {
            if (_leftInstance == null)
            {
                return;
            }

            KickOf(hand).Kick();
        }

        private void HookFiredHand()
        {
            if (ReferenceEquals(_hooked, _weapon))
            {
                return;
            }

            UnhookFiredHand();
            if (_weapon == null)
            {
                return;
            }

            _weapon.FiredHand += HandleFiredHand;
            _hooked = _weapon;
        }

        private void UnhookFiredHand()
        {
            if (!ReferenceEquals(_hooked, null))
            {
                _hooked.FiredHand -= HandleFiredHand;
            }

            _hooked = null;
        }

        private SightView CurrentSightView()
        {
            if (SightSource != null)
            {
                return SightSource();
            }

            // 組み立てる側が AimDownSights をこれより後に付けるので、見つかるまで探し直す。
            if (_ads == null)
            {
                _ads = GetComponent<AimDownSights>();
            }

            return _ads != null && _ads.IsInitialized ? _ads.SightView : SightView.None;
        }

        // 消して出し直すと付けた物とソケットを作り直すことになり、銃口の炎の出る位置も失うので、描くのだけを止める。
        private void HideForSight(bool hide, bool force)
        {
            if (_instance == null || (!force && hide == _hiddenBySight))
            {
                return;
            }

            _hiddenBySight = hide;
            SetRenderersEnabled(_instance, !hide);
            if (_leftInstance != null)
            {
                SetRenderersEnabled(_leftInstance, !hide);
            }

            if (!force)
            {
                Debug.Log($"{LogTag} sight hidden={(hide ? "true" : "false")} weapon={_weaponId}", this);
            }
        }

        private static void SetRenderersEnabled(GameObject model, bool enabled)
        {
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = enabled;
            }
        }

        public static string FormatLeftHandLog(string weaponId, string prefabName, bool mirrored, bool hasMuzzle)
        {
            return $"{LogTag} akimbo left weapon={weaponId} prefab={prefabName} mirrored={(mirrored ? "true" : "false")} muzzle={hasMuzzle}";
        }

        public static string FormatSpawnLog(string weaponId, string prefabName, bool hasMuzzle)
        {
            return $"{SpawnedLogPrefix} weapon={weaponId} prefab={prefabName} muzzle={hasMuzzle}";
        }

        public static string FormatSwapLog(string fromWeaponId, string toWeaponId, string prefabName, bool hasMuzzle)
        {
            return $"{SwappedLogPrefix} weapon={fromWeaponId} -> {toWeaponId} prefab={prefabName} muzzle={hasMuzzle}";
        }

        public static string FormatRemoveLog(string weaponId, string reason)
        {
            return $"{RemovedLogPrefix} weapon={weaponId} reason={reason}";
        }

        private string HiddenReason()
        {
            if (_rig == null || !_rig.IsInitialized || !_rig.enabled || !_rig.IsLocalView)
            {
                return ReasonNotLocalView;
            }

            // 三人称でも目へ寄せている間は体を隠すので、一人称の銃まで消すと手元に何も描かれなくなる。
            if (_rig.Mode != ViewMode.FirstPerson && !CurrentSightView().HidesBody)
            {
                return ReasonThirdPerson;
            }

            if (_damageable != null && _damageable.IsDead)
            {
                return ReasonDead;
            }

            if (_weapon != null && _weapon.IsHolstered)
            {
                return ReasonHolstered;
            }

            return HiddenReasonSource != null ? HiddenReasonSource() : null;
        }

        private string WantedWeaponId()
        {
            if (WeaponIdSource != null)
            {
                return WeaponIdSource();
            }

            var weapon = _weapon != null ? _weapon.Weapon : null;
            return weapon != null ? weapon.Spec.Id : null;
        }

        private IReadOnlyList<string> WantedAttachments(string weaponId)
        {
            if (AttachmentsSource != null)
            {
                return AttachmentsSource(weaponId) ?? WeaponAttachments.None;
            }

            return _weapon != null && _weapon.IsInitialized ? _weapon.AttachmentsOf(weaponId) : WeaponAttachments.None;
        }

        private void Attach(IReadOnlyList<string> attachments)
        {
            var catalog = _weapon != null ? _weapon.AttachmentCatalog : KitTuning.Items;
            WeaponAttachmentModels.Apply(_instance.transform, attachments, catalog, AttachmentModels);
            if (_leftInstance != null)
            {
                WeaponAttachmentModels.Apply(_leftInstance.transform, attachments, catalog, AttachmentModels);
                _leftSockets = new WeaponSockets(_leftInstance.transform);
            }

            _attachments = attachments;
            ApplyPaint(WantedPaint());
            _sockets = new WeaponSockets(_instance.transform);
            var pivot = _instance.transform.parent;
            var sight = _sockets.Sight != null && pivot != null ? pivot.InverseTransformPoint(_sockets.Sight.position) - _instance.transform.localPosition : Vector3.zero;
            _adsPosition = AimedRootPosition(_entry.AdsOffset, sight);
            if (_presentation != null)
            {
                _presentation.WeaponModel = _instance.transform;
            }
        }

        private ViewmodelEntry FindEntry(string weaponId)
        {
            var catalog = Catalog;
            if (catalog == null)
            {
                WarnMissingCatalog();
                return null;
            }

            if (!catalog.TryGetWeapon(weaponId, out var entry) || entry.Prefab == null)
            {
                if (_warnedWeaponIds.Add(weaponId))
                {
                    Debug.LogWarning($"{LogTag} weapon '{weaponId}' has no viewmodel prefab in the viewmodel catalog; no viewmodel is shown", this);
                }

                return null;
            }

            return entry;
        }

        private void Spawn(string weaponId, ViewmodelEntry entry, bool akimbo)
        {
            var pivot = _motion.EnsurePivot();
            if (pivot == null)
            {
                return;
            }

            var previousId = _instance != null ? _weaponId : null;
            DestroyInstance();

            var instance = Instantiate(entry.Prefab, pivot, false);
            instance.name = entry.Prefab.name;
            instance.transform.localPosition = entry.Offset;
            instance.transform.localRotation = Quaternion.identity;
            PrepareForView(instance);
            _hipPosition = entry.Offset;

            _instance = instance;
            _weaponId = weaponId;
            _entry = entry;
            _attachments = WantedAttachments(weaponId);
            if (_attachments.Count > 0)
            {
                WeaponAttachmentModels.Apply(instance.transform, _attachments, _weapon != null ? _weapon.AttachmentCatalog : KitTuning.Items, AttachmentModels);
            }

            if (akimbo)
            {
                SpawnLeft(pivot, entry);
            }

            _paint = null;
            ApplyPaint(WantedPaint());
            _sockets = new WeaponSockets(instance.transform);
            var sight = _sockets.Sight != null ? pivot.InverseTransformPoint(_sockets.Sight.position) - entry.Offset : Vector3.zero;
            _adsPosition = AimedRootPosition(entry.AdsOffset, sight);
            if (_presentation != null)
            {
                _presentation.WeaponModel = instance.transform;
            }

            var sway = SwaySettingsFor(entry);
            _motion.Settings = sway;
            _motion.ResetMotion();
            _rightKick.Settings = sway;
            _leftKick.Settings = sway;
            _rightKick.Reset();
            _leftKick.Reset();
            _hiddenBySight = false;

            var hasMuzzle = _sockets.Muzzle != null;
            Debug.Log(
                previousId == null
                    ? FormatSpawnLog(weaponId, instance.name, hasMuzzle)
                    : FormatSwapLog(previousId, weaponId, instance.name, hasMuzzle),
                this);
            if (_leftInstance != null)
            {
                Debug.Log(FormatLeftHandLog(weaponId, _leftInstance.name, _leftMirrored, _leftSockets.Muzzle != null), this);
            }
        }

        // 左手のモデルが無い武器は、形をコードで作らず右手のモデルを鏡に映して使う。
        private void SpawnLeft(Transform pivot, ViewmodelEntry entry)
        {
            var mirrored = entry.LeftPrefab == null;
            var prefab = mirrored ? entry.Prefab : entry.LeftPrefab;
            var left = Instantiate(prefab, pivot, false);
            left.name = prefab.name;
            left.transform.localPosition = entry.LeftOffset;
            left.transform.localRotation = Quaternion.identity;
            if (mirrored)
            {
                var scale = left.transform.localScale;
                left.transform.localScale = new Vector3(-scale.x, scale.y, scale.z);
            }

            PrepareForView(left);
            if (_attachments.Count > 0)
            {
                WeaponAttachmentModels.Apply(left.transform, _attachments, _weapon != null ? _weapon.AttachmentCatalog : KitTuning.Items, AttachmentModels);
            }

            _leftInstance = left;
            _leftHipPosition = entry.LeftOffset;
            _leftMirrored = mirrored;
            _leftSockets = new WeaponSockets(left.transform);
        }

        private void Remove(string reason, bool hideFirst = true)
        {
            if (_instance == null)
            {
                // 外から壊されていても、次に同じ武器を出すときに作り直させる。
                _weaponId = null;
                _sockets = WeaponSockets.None;
                DestroyLeft(hideFirst);
                return;
            }

            var weaponId = _weaponId;
            DestroyInstance(hideFirst);
            Debug.Log(FormatRemoveLog(weaponId, reason), this);
        }

        private void DestroyInstance(bool hideFirst = true)
        {
            DestroyLeft(hideFirst);
            if (_instance == null)
            {
                return;
            }

            if (_presentation != null && _presentation.WeaponModel == _instance.transform)
            {
                _presentation.WeaponModel = null;
            }

            // Destroy はフレームの終わりまで残り、同じフレームに出した次のモデルと重なって見えるので、先に隠す。
            if (hideFirst)
            {
                _instance.SetActive(false);
            }

            DestroySafely(_instance);
            _instance = null;
            _weaponId = null;
            _paint = null;
            _sockets = WeaponSockets.None;
        }

        private void DestroyLeft(bool hideFirst)
        {
            if (_leftInstance != null)
            {
                if (hideFirst)
                {
                    _leftInstance.SetActive(false);
                }

                DestroySafely(_leftInstance);
            }

            _leftInstance = null;
            _leftMirrored = false;
            _leftSockets = WeaponSockets.None;
        }

        private Material WantedPaint()
        {
            return PaintSource != null ? PaintSource() : CosmeticLooks.WornPaint();
        }

        private void ApplyPaint(Material paint)
        {
            if (_instance == null)
            {
                return;
            }

            var changed = paint != _paint;
            CamoPaint.Apply(_instance, paint);
            if (_leftInstance != null)
            {
                CamoPaint.Apply(_leftInstance, paint);
            }

            _paint = paint;
            if (changed)
            {
                Debug.Log($"{LogTag} paint weapon={_weaponId} paint={(paint != null ? paint.name : "-")}", this);
            }
        }

        private ViewmodelSwaySettings SwaySettingsFor(ViewmodelEntry entry)
        {
            if (entry.TryGetSwaySettings(out var settings, out var error))
            {
                return settings;
            }

            if (_warnedWeaponIds.Add("sway:" + entry.WeaponId))
            {
                Debug.LogWarning($"{LogTag} weapon '{entry.WeaponId}' has invalid sway values ({error}); using the defaults", this);
            }

            return ViewmodelSwaySettings.Default;
        }

        private static void PrepareForView(GameObject instance)
        {
            // 体の当たり判定と射撃の Ray に混ざらないよう、モデルの当たりは捨てる。
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
                DestroySafely(collider);
            }

            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        private static void DestroySafely(UnityEngine.Object target)
        {
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private void Resolve()
        {
            if (_resolved)
            {
                return;
            }

            _resolved = true;
            _rig = GetComponent<ViewRig>();
            _weapon = GetComponentInChildren<WeaponController>(true);
            _damageable = GetComponent<Damageable>();
            _presentation = GetComponent<WeaponPresentation>();
            _motion = GetComponent<ViewmodelMotion>();
            if (_motion == null)
            {
                _motion = gameObject.AddComponent<ViewmodelMotion>();
            }

            _motion.HandsKickSeparately = () => _leftInstance != null;
            HookFiredHand();
        }

        private void WarnMissingCatalog()
        {
            if (_warnedMissingCatalog)
            {
                return;
            }

            _warnedMissingCatalog = true;
            Debug.LogWarning(
                $"{LogTag} no ViewmodelCatalog at Resources/{ViewmodelCatalog.ResourcePath}; first-person viewmodels are off. "
                + "Run Shooter/Build All Presentation Assets (Shooter.Editor.PresentationAssetSteps.BuildAll)",
                this);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetOnPlay()
        {
            _warnedWeaponIds.Clear();
            _warnedMissingCatalog = false;
        }
    }
}

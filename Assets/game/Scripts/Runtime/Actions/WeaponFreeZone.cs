using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // オフラインの体だけを見る。ネット対戦のゾーンはサーバーが判定する。
    [DisallowMultipleComponent]
    public sealed class WeaponFreeZone : MonoBehaviour
    {
        public const string LogTag = WeaponFreeZoneLog.Tag;

        public const string VisualName = "Visual";

        // 床に描く印なので、足が埋まらない薄さにする。
        public const float PlaceholderThickness = 0.02f;

        public static readonly Vector3 DefaultSize = new Vector3(8f, 3f, 8f);

        public static readonly Color DefaultColor = new Color(0.95f, 0.65f, 0.15f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField, Tooltip("ゾーンの Id。中立の警備は Id か参照でこのゾーンに結び付く。ログにも出す")]
        private string _zoneId = string.Empty;

        [SerializeField, Tooltip("武器を出したまま入ったときの反応")]
        private WeaponFreeZoneReaction _reaction = WeaponFreeZoneSpec.DefaultReaction;

        [SerializeField, Min(0f), Tooltip("警告してから敵対するまでの秒数。警告してしばらくで敵対する反応だけが使う")]
        private float _hostileAfterSeconds = WeaponFreeZoneSpec.DefaultHostileAfterSeconds;

        [SerializeField, Tooltip("プレイヤーとみなす Collider のタグ")]
        private string _playerTag = GameBootstrap.PlayerTag;

        [SerializeField, Tooltip("Collider が無いときに作る範囲の大きさ (m)")]
        private Vector3 _size = DefaultSize;

        [SerializeField, Tooltip("ゾーンの印。原点がトリガーの床の中心。空なら床に橙の板を描く")]
        private GameObject _visualPrefab;

        private readonly HashSet<Collider> _inside = new HashSet<Collider>();

        private WeaponFreeZoneSpec _spec;
        private WeaponFreeZoneState _state;
        private HolsterController _holster;
        private Damageable _body;
        private HolsterController _blocking;
        private bool _warnedNoHolster;
        private WeaponFreeZoneStatus? _published;

        public string ZoneId
        {
            get => string.IsNullOrEmpty(_zoneId) ? name : _zoneId;
            set => _zoneId = value ?? string.Empty;
        }

        public WeaponFreeZoneSpec Spec => _spec ??= CreateSpec();

        public string PlayerTag
        {
            get => _playerTag;
            set => _playerTag = value;
        }

        public Vector3 Size
        {
            get => _size;
            set => _size = value;
        }

        public GameObject VisualPrefab
        {
            get => _visualPrefab;
            set => _visualPrefab = value;
        }

        public WeaponFreeZoneState State => _state ??= new WeaponFreeZoneState(Spec);

        public WeaponFreeZoneStatus Status => State.Status;

        public bool IsPlayerInside => State.Inside;

        public bool IsWarning => State.Warning;

        public bool IsHostile => State.IsHostile;

        public HolsterController Holster => _holster;

        public Damageable Player => _body;

        public static WeaponFreeZone FindById(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId))
            {
                return null;
            }

            foreach (var zone in FindObjectsByType<WeaponFreeZone>(FindObjectsInactive.Exclude))
            {
                if (string.Equals(zone.ZoneId, zoneId, StringComparison.Ordinal))
                {
                    return zone;
                }
            }

            return null;
        }

        // 反応を替えると数えていた警告と敵対の記録は意味を失うので、中にいる人の扱いを片付けてから作り直す。
        public void Configure(WeaponFreeZoneSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            ReleaseBlock();
            Unpublish();
            _reaction = spec.Reaction;
            _hostileAfterSeconds = spec.HostileAfterSeconds;
            _spec = spec;
            _state = new WeaponFreeZoneState(spec);
            Debug.Log(WeaponFreeZoneLog.Loaded(ZoneId, spec), this);
        }

        private void Awake()
        {
            Build();
        }

        // Awake でも呼ぶが、Edit Mode で置いたときにシーンへ範囲と印を残すには置いた側が呼ぶので公開する。
        public void Build()
        {
            var trigger = EnsureTrigger();
            BuildVisual(trigger);
        }

        // 置いた Collider があればそれを使い、無いときだけ箱を作る。どちらも通り抜けられるよう isTrigger にする。
        public Collider EnsureTrigger()
        {
            var collider = GetComponent<Collider>();
            if (collider == null)
            {
                var box = gameObject.AddComponent<BoxCollider>();
                box.size = _size;
                box.center = new Vector3(0f, _size.y * 0.5f, 0f);
                collider = box;
            }

            collider.isTrigger = true;
            return collider;
        }

        private void OnTriggerEnter(Collider other)
        {
            // 無効なコンポーネントにもトリガーの通知は届くので、ここで止める。
            if (enabled)
            {
                HandleEnter(other);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            HandleExit(other);
        }

        public bool HandleEnter(Collider other)
        {
            if (!IsPlayer(other))
            {
                return false;
            }

            if (_inside.Count == 0 || _body == null)
            {
                _body = other.GetComponentInParent<Damageable>();
                _holster = other.GetComponentInParent<HolsterController>();
                _warnedNoHolster = false;
            }

            return _inside.Add(other);
        }

        public bool HandleExit(Collider other)
        {
            return other != null && _inside.Remove(other);
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        // 入力を読む Update から分けるのは、Edit Mode のテストが時間を渡して進められるようにするため。
        public void Tick(float deltaSeconds)
        {
            PruneInside();
            var state = State;
            var inside = _inside.Count > 0 && (_body == null || !_body.IsDead);
            var armed = inside && IsArmed();
            var wasInside = state.Inside;
            var wasWarning = state.Warning;
            var turnedHostile = state.Tick(deltaSeconds, inside, armed);

            if (inside && !wasInside)
            {
                Debug.Log(WeaponFreeZoneLog.Entered(ZoneId, HolderId, armed), this);
                WarnIfNoHolster();
            }
            else if (!inside && wasInside)
            {
                Debug.Log(WeaponFreeZoneLog.Exited(ZoneId, HolderId), this);
            }

            if (state.ShouldHolster && _holster != null)
            {
                Debug.Log(WeaponFreeZoneLog.AutoHolstered(ZoneId, HolderId, _holster.Holster()), this);
            }

            if (state.Warning && !wasWarning)
            {
                Debug.Log(WeaponFreeZoneLog.WarningBegan(ZoneId, Spec.Reaction, state.SecondsToHostile), this);
            }
            else if (!state.Warning && wasWarning && !turnedHostile)
            {
                Debug.Log(WeaponFreeZoneLog.WarningCleared(ZoneId, inside ? WeaponFreeZoneLog.UnarmedReason : WeaponFreeZoneLog.ExitedReason), this);
            }

            if (turnedHostile)
            {
                Debug.Log(WeaponFreeZoneLog.Hostile(ZoneId), this);
            }

            SyncBlock(state.BlocksDraw);
            Publish(inside);

            if (!inside && _inside.Count == 0)
            {
                _body = null;
                _holster = null;
            }
        }

        // 出入りの通知を待たずに片付ける。ゾーンを無効にしたときや、プレイヤーを入れ替えたときに使う。
        public void ClearPlayer()
        {
            _inside.Clear();
            Tick(0f);
        }

        private void OnDisable()
        {
            ClearPlayer();
        }

        private void OnDestroy()
        {
            ReleaseBlock();
            Unpublish();
        }

        private string HolderId => _body != null ? _body.Id : _holster != null ? _holster.name : null;

        // しまう状態を持たない体は常に構えていると数える。Demo の警告表示と同じ決まりにするため。
        private bool IsArmed()
        {
            return _holster == null || _holster.Status.IsArmed;
        }

        private void WarnIfNoHolster()
        {
            var needsHolster = Spec.Reaction == WeaponFreeZoneReaction.AutoHolster || Spec.Reaction == WeaponFreeZoneReaction.BlockDraw;
            if (_holster != null || !needsHolster || _warnedNoHolster)
            {
                return;
            }

            _warnedNoHolster = true;
            Debug.LogWarning(WeaponFreeZoneLog.NoHolster(ZoneId, HolderId, Spec.Reaction), this);
        }

        // 消えた・無効になった Collider からは出る通知が来ないので、毎回数え直す。
        private void PruneInside()
        {
            if (_inside.Count == 0)
            {
                return;
            }

            _inside.RemoveWhere(collider => collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy);
        }

        private void SyncBlock(bool block)
        {
            if (block && _holster != null)
            {
                if (_blocking != null && _blocking != _holster)
                {
                    ReleaseBlock();
                }

                if (_holster.BlockDraw.Claim(this))
                {
                    _blocking = _holster;
                    Debug.Log(WeaponFreeZoneLog.DrawBlocked(ZoneId, HolderId, true), this);
                }

                return;
            }

            ReleaseBlock();
        }

        private void ReleaseBlock()
        {
            var blocking = _blocking;
            _blocking = null;
            // 破棄済みでも C# のオブジェクトは残っているので、宣言は外せる。
            if (!ReferenceEquals(blocking, null) && blocking.BlockDraw.Release(this))
            {
                Debug.Log(WeaponFreeZoneLog.DrawBlocked(ZoneId, HolderId, false), this);
            }
        }

        // 外にいる間は値を出さない。重なった別のゾーンが出した値を、外にいるこちらが上書きしないため。
        private void Publish(bool inside)
        {
            if (!inside)
            {
                Unpublish();
                return;
            }

            var status = State.Status;
            if (_published.HasValue && _published.Value == status)
            {
                return;
            }

            _published = status;
            GameSignals.WeaponFreeZone.Publish(status);
        }

        private void Unpublish()
        {
            if (_published.HasValue)
            {
                GameSignals.WeaponFreeZone.ClearIfCurrent(_published);
                _published = null;
            }
        }

        private bool IsPlayer(Collider other)
        {
            return other != null && !string.IsNullOrEmpty(_playerTag) && other.CompareTag(_playerTag);
        }

        private WeaponFreeZoneSpec CreateSpec()
        {
            try
            {
                return new WeaponFreeZoneSpec(_reaction, _hostileAfterSeconds);
            }
            catch (ArgumentException e)
            {
                Debug.LogError($"{LogTag} zone={ZoneId} has an invalid setting: {e.Message}", this);
                return WeaponFreeZoneSpec.Default;
            }
        }

        private void BuildVisual(Collider trigger)
        {
            if (transform.Find(VisualName) != null)
            {
                return;
            }

            var bounds = LocalFloor(trigger, out var floorCenter);
            if (_visualPrefab != null)
            {
                // 印の中を歩いて通れるよう、見た目の当たり判定は外す。
                var placed = Shooter.Runtime.VisualPrefab.Instantiate(_visualPrefab, transform, VisualName, stripColliders: true);
                placed.transform.localPosition = floorCenter;
                return;
            }

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = VisualName;
            visual.transform.SetParent(transform, false);
            visual.transform.localPosition = floorCenter + new Vector3(0f, PlaceholderThickness * 0.5f, 0f);
            visual.transform.localScale = new Vector3(bounds.x, PlaceholderThickness, bounds.z);
            var collider = visual.GetComponent<Collider>();
            if (collider != null)
            {
                // Destroy は次のフレームまで残り、その間にトリガーの出入りや移動の判定に当たるので、すぐ消す。
                DestroyImmediate(collider);
            }

            Paint(visual.GetComponent<Renderer>(), DefaultColor);
        }

        // 箱でない Collider は形を推し量れないので、Size の箱の床に描く。
        private Vector3 LocalFloor(Collider trigger, out Vector3 floorCenter)
        {
            if (trigger is BoxCollider box)
            {
                floorCenter = box.center - new Vector3(0f, box.size.y * 0.5f, 0f);
                return box.size;
            }

            floorCenter = Vector3.zero;
            return _size;
        }

        private static void Paint(Renderer renderer, Color color)
        {
            if (renderer == null)
            {
                return;
            }

            // 共有マテリアルを塗り替えると他のプリミティブまで色が変わるので、この印だけの上書きで色を付ける。
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(block);
        }
    }
}

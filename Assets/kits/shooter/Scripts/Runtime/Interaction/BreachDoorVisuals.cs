using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 板はドアの当たりそのものなので、見る人がいない専用サーバーでも作る。破片・爆薬・音だけを見る人がいるときに出す。
    [DisallowMultipleComponent]
    public sealed class BreachDoorVisuals : MonoBehaviour
    {
        public const string PanelObjectName = "BreachPanel";
        public const string ChargeObjectName = "BreachCharge";
        public const string DebrisObjectName = "BreachDebris";

        public static readonly Vector3 DefaultPanelSize = new Vector3(1f, 2.1f, 0.08f);

        public const float DefaultSwingDegrees = 95f;

        public const float DefaultSwingSeconds = 0.15f;

        public const float DefaultDebrisSeconds = 6f;

        public const int DefaultDebrisCount = 10;

        public const float DefaultDebrisSpeed = 6f;

        // 板の面から少し浮かせ、板の見た目に埋もれないようにする。
        private const float _chargeLift = 0.06f;

        private static readonly Vector3 _chargeSize = new Vector3(0.3f, 0.2f, 0.08f);
        private static readonly Color _panelColor = new Color(0.45f, 0.3f, 0.18f, 1f);
        private static readonly Color _chargeColor = new Color(0.8f, 0.1f, 0.05f, 1f);
        private static Material _panelMaterial;
        private static Material _chargeMaterial;

        [SerializeField, Tooltip("ドアの板。蹴ると根元を軸に回すので、軸を原点に置き +X へ板を伸ばす。当たりは板の子の Collider。空なら箱を作る")]
        private GameObject _panelPrefab;

        [SerializeField, Tooltip("爆薬で壊れた板の破片。空なら小さな箱を飛ばす")]
        private GameObject _debrisPrefab;

        [SerializeField, Tooltip("仕掛けた爆薬。板の面に貼る。空なら小さな箱を作る")]
        private GameObject _chargePrefab;

        [SerializeField, Tooltip("蹴り開けた音。空なら鳴らさない")]
        private AudioClip _kickSound;

        [SerializeField, Tooltip("爆薬の爆発の音。空なら PresentationCatalog の爆発の音")]
        private AudioClip _explosionSound;

        [SerializeField, Tooltip("板の箱を作るときの幅・高さ・厚み (m)。板の参照があっても、厚みは爆薬を板の面に貼る位置に使う")]
        private Vector3 _panelSize = DefaultPanelSize;

        [SerializeField, Range(0f, 180f), Tooltip("蹴り開けたときに板を回す角度")]
        private float _swingDegrees = DefaultSwingDegrees;

        [SerializeField, Min(0f), Tooltip("蹴り開けた板が回り切るまでの秒数。0 ならすぐ")]
        private float _swingSeconds = DefaultSwingSeconds;

        [SerializeField, Min(0f), Tooltip("破片を残しておく秒数")]
        private float _debrisSeconds = DefaultDebrisSeconds;

        [SerializeField, Min(0), Tooltip("破片の箱を作るときの数")]
        private int _debrisCount = DefaultDebrisCount;

        [SerializeField, Min(0f), Tooltip("破片の箱を飛ばす速さ (m/s)")]
        private float _debrisSpeed = DefaultDebrisSpeed;

        // シーンの生成で作った板をシーンに残し、オンラインのマップの書き出しが板の当たりをドアの組にできるよう、保存する。
        // 保存しないと、Play の Bind が 2 枚目の板を作る。
        [SerializeField, HideInInspector]
        private Transform _panel;

        [NonSerialized]
        private bool _hasClosedRotation;

        private GameObject _charge;
        private GameObject _debris;
        private readonly List<Piece> _pieces = new List<Piece>();
        private float _debrisElapsed;
        private Quaternion _closedRotation;
        private Quaternion _swingTarget;
        private float _swingElapsed;
        private bool _swinging;

        public GameObject PanelPrefab
        {
            get => _panelPrefab;
            set => _panelPrefab = value;
        }

        public GameObject DebrisPrefab
        {
            get => _debrisPrefab;
            set => _debrisPrefab = value;
        }

        public GameObject ChargePrefab
        {
            get => _chargePrefab;
            set => _chargePrefab = value;
        }

        public AudioClip KickSound
        {
            get => _kickSound;
            set => _kickSound = value;
        }

        public AudioClip ExplosionSound
        {
            get => _explosionSound;
            set => _explosionSound = value;
        }

        // 板の箱を作るときの幅・高さ・厚み (m)。作った後に変えても、今ある板は作り直さない。
        public Vector3 PanelSize
        {
            get => _panelSize;
            set => _panelSize = value;
        }

        public Vector3 WorldPanelSize
        {
            get
            {
                var scale = transform.lossyScale;
                return Vector3.Scale(ValidPanelSize(), new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            }
        }

        public Transform Panel => _panel;

        public GameObject Charge => _charge;

        public GameObject Debris => _debris;

        public bool IsSwinging => _swinging;

        public static BreachDoorVisuals For(GameObject door)
        {
            if (door == null)
            {
                throw new ArgumentNullException(nameof(door));
            }

            var visuals = door.GetComponent<BreachDoorVisuals>();
            return visuals != null ? visuals : door.AddComponent<BreachDoorVisuals>();
        }

        // 2 度呼んでも板は 1 枚だけにする。ドアが Bind をやり直すことがあるため。
        public Transform EnsurePanel()
        {
            if (_panel == null)
            {
                if (_panelPrefab != null)
                {
                    var instance = Instantiate(_panelPrefab, transform, false);
                    instance.name = PanelObjectName;
                    _panel = instance.transform;
                }
                else
                {
                    _panel = BuildPanel(transform, ValidPanelSize());
                }

                _hasClosedRotation = false;
            }

            // シーンに保存した板は閉じた向きのまま読み込まれるので、作ったときと同じく最初に見た向きを閉じた向きにする。
            if (!_hasClosedRotation)
            {
                _closedRotation = _panel.localRotation;
                _hasClosedRotation = true;
            }

            return _panel;
        }

        public Collider[] PanelColliders()
        {
            return _panel != null ? _panel.GetComponentsInChildren<Collider>(true) : Array.Empty<Collider>();
        }

        // 板の見た目の真ん中。爆薬を貼る所と破片を出す所に使う。見た目が無ければ箱の寸法から出す。
        public Vector3 PanelCenter()
        {
            if (_panel != null && TryPanelBounds(out var bounds))
            {
                return bounds.center;
            }

            var size = ValidPanelSize();
            return transform.TransformPoint(new Vector3(0f, size.y * 0.5f, 0f));
        }

        // nearSign はドアの前 (+Z) に対して爆薬を貼る側。+1 なら前の面に貼る。
        public Vector3 ChargePoint(float nearSign)
        {
            var half = HalfThickness();
            return PanelCenter() + (transform.forward * (Mathf.Sign(nearSign) * (half + _chargeLift)));
        }

        // 板の根元を軸に、ドアの前 (+Z) に対して farSign の側へ回す。根元から +X に伸びる板を -Y 回りに回すと +Z へ動く。
        public void SwingOpen(float farSign)
        {
            if (_panel == null)
            {
                return;
            }

            _swingTarget = _closedRotation * Quaternion.Euler(0f, -Mathf.Sign(farSign) * _swingDegrees, 0f);
            _swingElapsed = 0f;
            _swinging = _swingSeconds > 0f;
            if (!_swinging)
            {
                _panel.localRotation = _swingTarget;
            }
        }

        public void ShowCharge(float nearSign)
        {
            HideCharge();
            if (NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
            {
                return;
            }

            var point = ChargePoint(nearSign);
            var rotation = transform.rotation * Quaternion.LookRotation(Vector3.forward * Mathf.Sign(nearSign));
            if (_chargePrefab != null)
            {
                _charge = Instantiate(_chargePrefab, point, rotation, transform);
            }
            else
            {
                _charge = BuildBox(_chargeSize, ChargeMaterial);
                _charge.transform.SetParent(transform, false);
                _charge.transform.SetPositionAndRotation(point, rotation);
            }

            _charge.name = ChargeObjectName;
        }

        public void HideCharge()
        {
            DestroySafely(_charge);
            _charge = null;
        }

        // 板を消し、破片を farSign の側へ飛ばす。当たりは板ごと消えるので、破片には当たりを付けない。
        public void Shatter(float farSign)
        {
            HideCharge();
            var center = PanelCenter();
            if (_panel != null)
            {
                _swinging = false;
                _panel.gameObject.SetActive(false);
            }

            if (NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
            {
                return;
            }

            DestroySafely(_debris);
            _pieces.Clear();
            _debrisElapsed = 0f;
            if (_debrisPrefab != null)
            {
                _debris = Instantiate(_debrisPrefab, center, transform.rotation, transform);
            }
            else
            {
                _debris = BuildDebris(center, farSign);
            }

            _debris.name = DebrisObjectName;
        }

        public void PlayKick(Vector3 point, float audibleDistance)
        {
            if (_kickSound == null || !Application.isPlaying || NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
            {
                return;
            }

            AudioSource.PlayClipAtPoint(_kickSound, point);
            SubtitleFeed.Raise(SoundCueKind.Clatter, point, audibleDistance);
        }

        public void PlayExplosion(Vector3 center)
        {
            Explosion.PlayEffects(center, _explosionSound);
        }

        public void Advance(float deltaSeconds)
        {
            var dt = Mathf.Max(0f, deltaSeconds);
            AdvanceSwing(dt);
            AdvanceDebris(dt);
        }

        private void Update()
        {
            Advance(Time.deltaTime);
        }

        private void AdvanceSwing(float dt)
        {
            if (!_swinging || _panel == null)
            {
                return;
            }

            _swingElapsed += dt;
            var t = Mathf.Clamp01(_swingElapsed / _swingSeconds);
            _panel.localRotation = Quaternion.Slerp(_closedRotation, _swingTarget, t);
            if (t >= 1f)
            {
                _swinging = false;
            }
        }

        private void AdvanceDebris(float dt)
        {
            if (_debris == null)
            {
                return;
            }

            _debrisElapsed += dt;
            if (_debrisElapsed >= _debrisSeconds)
            {
                DestroySafely(_debris);
                _debris = null;
                _pieces.Clear();
                return;
            }

            // 当たりを付けないので、床はドアの足元の高さとみなして止める。
            var floor = transform.position.y;
            foreach (var piece in _pieces)
            {
                if (piece.Transform == null || piece.Resting)
                {
                    continue;
                }

                piece.Velocity += Physics.gravity * dt;
                var position = piece.Transform.position + (piece.Velocity * dt);
                if (position.y <= floor + piece.HalfHeight)
                {
                    position.y = floor + piece.HalfHeight;
                    piece.Resting = true;
                }

                piece.Transform.position = position;
                piece.Transform.Rotate(piece.Spin * dt, Space.Self);
            }
        }

        private GameObject BuildDebris(Vector3 center, float farSign)
        {
            var root = new GameObject(DebrisObjectName);
            root.transform.SetParent(transform, false);
            root.transform.position = center;
            var size = ValidPanelSize();
            var away = transform.forward * Mathf.Sign(farSign);
            for (var i = 0; i < _debrisCount; i++)
            {
                var scale = new Vector3(size.x * UnityEngine.Random.Range(0.15f, 0.3f), size.y * UnityEngine.Random.Range(0.08f, 0.18f), size.z);
                var piece = BuildBox(scale, PanelMaterial);
                piece.name = DebrisObjectName + i;
                piece.transform.SetParent(root.transform, false);
                piece.transform.position = center + transform.rotation * new Vector3(
                    UnityEngine.Random.Range(-0.5f, 0.5f) * size.x,
                    UnityEngine.Random.Range(-0.5f, 0.5f) * size.y,
                    0f);
                var velocity = (away + (UnityEngine.Random.insideUnitSphere * 0.6f)).normalized * (_debrisSpeed * UnityEngine.Random.Range(0.6f, 1f));
                var spin = UnityEngine.Random.insideUnitSphere * 720f;
                _pieces.Add(new Piece(piece.transform, velocity, spin, Mathf.Max(scale.x, scale.y) * 0.5f));
            }

            return root;
        }

        private static Transform BuildPanel(Transform door, Vector3 size)
        {
            // 板を根元で回せるよう、根元に軸を置いて板をその子にする。
            var hinge = new GameObject(PanelObjectName).transform;
            hinge.SetParent(door, false);
            hinge.localPosition = new Vector3(-size.x * 0.5f, 0f, 0f);

            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = PanelObjectName + "Board";
            var renderer = board.GetComponent<Renderer>();
            renderer.sharedMaterial = PanelMaterial(renderer);
            board.transform.SetParent(hinge, false);
            board.transform.localPosition = new Vector3(size.x * 0.5f, size.y * 0.5f, 0f);
            board.transform.localScale = size;
            return hinge;
        }

        private static GameObject BuildBox(Vector3 size, Func<Renderer, Material> material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);

            // 弾や人が飾りの箱に当たらないよう、すぐ消す。
            DestroyImmediate(box.GetComponent<Collider>());
            var renderer = box.GetComponent<Renderer>();
            renderer.sharedMaterial = material(renderer);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            box.transform.localScale = size;
            return box;
        }

        private static Material PanelMaterial(Renderer template)
        {
            if (_panelMaterial == null)
            {
                _panelMaterial = new Material(template.sharedMaterial) { name = PanelObjectName, color = _panelColor };
            }

            return _panelMaterial;
        }

        private static Material ChargeMaterial(Renderer template)
        {
            if (_chargeMaterial == null)
            {
                _chargeMaterial = new Material(template.sharedMaterial) { name = ChargeObjectName, color = _chargeColor };
                _chargeMaterial.EnableKeyword("_EMISSION");
                _chargeMaterial.SetColor("_EmissionColor", _chargeColor);
            }

            return _chargeMaterial;
        }

        private bool TryPanelBounds(out Bounds bounds)
        {
            var found = false;
            bounds = default;
            foreach (var renderer in _panel.GetComponentsInChildren<Renderer>(true))
            {
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return found && bounds.size.sqrMagnitude > 0f;
        }

        private float HalfThickness()
        {
            return Mathf.Max(0f, ValidPanelSize().z) * 0.5f;
        }

        private Vector3 ValidPanelSize()
        {
            return new Vector3(
                Mathf.Max(0.01f, _panelSize.x),
                Mathf.Max(0.01f, _panelSize.y),
                Mathf.Max(0.01f, _panelSize.z));
        }

        private static void DestroySafely(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private sealed class Piece
        {
            public Piece(Transform transform, Vector3 velocity, Vector3 spin, float halfHeight)
            {
                Transform = transform;
                Velocity = velocity;
                Spin = spin;
                HalfHeight = halfHeight;
            }

            public Transform Transform { get; }

            public Vector3 Velocity { get; set; }

            public Vector3 Spin { get; }

            public float HalfHeight { get; }

            public bool Resting { get; set; }
        }
    }
}

using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.NetV2
{
    // プレハブの見た目は立ったときの高さで作ってある前提なので、しゃがみと伏せは見た目を縦に縮めて下げて表す。
    [DisallowMultipleComponent]
    public sealed class NetV2RemotePlayer : MonoBehaviour
    {
        // プレハブはルートの原点を足元ではなく本体の位置 (移動の位置) に合わせて作る前提で、そのまま本体に重ねる。
        public const string BodyResourcePath = "NetV2/RemotePlayerBody";

        // 武器に体用の見た目 (WeaponExtrasCatalog の置いたときの見た目) が無いときに持たせる銃。中心が原点、前が +Z。無ければ仮の箱を組む
        public const string BodyGunResourcePath = "NetV2/RemotePlayerGun";

        public const string BodyGunName = "BodyGun";
        public const string RightHandGunName = "Gun";

        private const string _bodyName = "Body";

        private StanceGeometry _geometry;
        private float _leanAngleDegrees;
        private Transform _body;
        private Vector3 _standingScale;
        private float _standingY;
        private PlayerStance _stance = PlayerStance.Standing;
        private Quaternion _tilt = Quaternion.identity;
        private float _drop;
        private bool _snapTilt = true;
        private WeaponLightBeam _weaponLight;
        private WeaponLightSpec _weaponLightSpec;
        private WeaponLightCatalog _weaponLightCatalog;
        private bool _weaponLightCatalogResolved;
        private Transform _bodyGun;
        private Transform _rightHandGun;
        private Transform _leftHandGun;
        private BodyWeaponModels _bodyGuns;
        private readonly List<Renderer> _bodyGunRenderers = new List<Renderer>();
        private Material _gunMaterial;
        private WeaponExtrasCatalog _weaponModels;
        private bool _weaponModelsResolved;
        private Func<string, WeaponSpec> _weaponSpecSource;

        public int PlayerId { get; private set; }

        public Quaternion Tilt => _tilt;

        public float Drop => _drop;

        public bool IsParachuting { get; private set; }

        public bool IsConcealed { get; private set; }

        // 体に銃を持たせる見た目 (ゲームの体のプレハブなど) が読む口。0 が構えた状態、1 がしまい切った状態
        public float WeaponLowering { get; private set; }

        public bool IsWeaponHidden { get; private set; }

        public bool IsWeaponLightOn { get; private set; }

        // 一度点けるまで光の部品を作らない。消している間も残すのは、点けるたびにプレハブを出し直さないため (WeaponLightController と同じ)
        public WeaponLightBeam WeaponLight => _weaponLight != null ? _weaponLight : null;

        // 相手の武器に何を付けたかはスナップショットに載らないので、相手の光はこの値で照らす。ゲームが差し替えられるよう外に開ける
        public WeaponLightSpec WeaponLightSpec
        {
            get => _weaponLightSpec ?? WeaponLightSpec.Default;
            set
            {
                _weaponLightSpec = value;
                ConfigureWeaponLight();
            }
        }

        // 決めなければオフラインと同じ Resources の WeaponLightCatalog を引く。null を決めるとコードで作るスポットライトで照らす
        public WeaponLightCatalog WeaponLightCatalog
        {
            get
            {
                if (!_weaponLightCatalogResolved)
                {
                    _weaponLightCatalog = WeaponLightCatalog.Load();
                    _weaponLightCatalogResolved = true;
                }

                return _weaponLightCatalog;
            }
            set
            {
                _weaponLightCatalog = value;
                _weaponLightCatalogResolved = true;
                ConfigureWeaponLight();
            }
        }

        public Transform Body => _body;

        // 体の銃の置き場。体 (Body) の子にしないのは、しゃがみと伏せで体を縦に縮めると銃まで潰れ、見た目の差し替えで体ごと作り直されるため
        public Transform BodyGun => _bodyGun;

        public Transform RightHandGun => _rightHandGun;

        public Transform LeftHandGun => _leftHandGun;

        public Transform BodyGunModel => _bodyGuns.Model;

        public Transform LeftHandGunModel => _bodyGuns.LeftHandModel;

        public string BodyWeaponId => _bodyGuns.WeaponId;

        // 二丁持ちかはスナップショットに載らないので、武器 Id から Spec を引く口。NetV2Client が NetV2Weapons.SpecFor をつなぐ。空なら右手の銃だけを持たせる
        public Func<string, WeaponSpec> WeaponSpecSource
        {
            get => _weaponSpecSource;
            set
            {
                _weaponSpecSource = value;
                _bodyGuns.Forget();
            }
        }

        // 決めなければオフラインの体 (PlayerMockBody) と同じ Resources の WeaponExtrasCatalog を引く
        public WeaponExtrasCatalog WeaponModels
        {
            get
            {
                if (!_weaponModelsResolved)
                {
                    _weaponModels = WeaponExtrasCatalog.Load();
                    _weaponModelsResolved = true;
                }

                return _weaponModels;
            }
            set
            {
                _weaponModels = value;
                _weaponModelsResolved = true;
                _bodyGuns.Forget();
            }
        }

        // スナップショットが載せてきた見た目の Id。体を替えられなかった (カタログに無い・絵が無い) ときも Id は残し、同じ Id で引き直さない。
        public string CosmeticId { get; private set; } = string.Empty;

        // 引く口を差し替えられるのは、テストと、見た目の引き方を変えたいゲームのため。
        public Func<string, GameObject> SkinSource { get; set; }

        // 傾ける角度はオフラインと同じ移動の値 (MotorSpec.LeanAngleDegrees) を、サーバーのルールから受け取る。
        public static NetV2RemotePlayer Create(int playerId, StanceGeometry geometry, MotorSpec motor, Transform parent)
        {
            if (geometry == null)
            {
                throw new ArgumentNullException(nameof(geometry));
            }

            if (motor == null)
            {
                throw new ArgumentNullException(nameof(motor));
            }

            var root = new GameObject("NetV2Remote" + playerId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            root.transform.SetParent(parent, false);
            var remote = root.AddComponent<NetV2RemotePlayer>();
            remote.PlayerId = playerId;
            remote._geometry = geometry;
            remote._leanAngleDegrees = Mathf.Clamp(motor.LeanAngleDegrees, 0f, 90f);
            remote.CreateBodyGun();
            remote.SetBody(CreateBody(null, geometry, root.transform));
            NetV2LocalPlayer.AttachPresentation(root, false);
            return remote;
        }

        public void ShowCosmetic(string cosmeticId)
        {
            var id = cosmeticId ?? string.Empty;
            if (string.Equals(id, CosmeticId, StringComparison.Ordinal))
            {
                return;
            }

            CosmeticId = id;
            var source = SkinSource ?? (Func<string, GameObject>)CosmeticLooks.BodyOf;
            var skin = id.Length == 0 ? null : source(id);
            var previous = _body;
            SetBody(CreateBody(skin, _geometry, transform));

            // 光は体の子なので、古い体と一緒に消されないよう先に新しい体へ移す
            PlaceWeaponLight();

            // Destroy はフレームの終わりまで残り、新しい体と重なって見えるので先に隠す
            previous.gameObject.SetActive(false);
            if (Application.isPlaying)
            {
                Destroy(previous.gameObject);
            }
            else
            {
                DestroyImmediate(previous.gameObject);
            }

            Debug.Log(CosmeticLog.RemoteWorn(PlayerId, id, skin != null ? skin.name : null), this);
        }

        // ルートではなく体だけを止めるのは、隠れている間も射撃や状態の演出 (WeaponPresentation など) がルートで動き続けるため
        public void SetConcealed(bool concealed)
        {
            if (IsConcealed == concealed)
            {
                return;
            }

            IsConcealed = concealed;
            _body.gameObject.SetActive(!concealed);
            _bodyGun.gameObject.SetActive(!concealed);

            // 隠れる前の傾きから寄せると、離れた場所で見え直した体が前の姿から倒れ込んで見えるので、見え直したらそのまま置く
            _snapTilt = true;
        }

        private void SetBody(Transform body)
        {
            _body = body;
            body.gameObject.SetActive(!IsConcealed);
            _standingScale = body.localScale;
            _standingY = body.localPosition.y;
            if (_stance != PlayerStance.Standing)
            {
                ApplyStance(_stance);
            }
            else
            {
                PlaceBody();
            }
        }

        public void Show(RemotePose pose)
        {
            transform.SetPositionAndRotation(NetV2Math.ToUnity(pose.Position), NetV2Math.YawRotation(pose.Yaw));
            if (pose.Stance != _stance)
            {
                ApplyStance(pose.Stance);
            }

            // 泳ぎと壁走りは補間されず 1 tick で切り替わるので、オフラインの CharacterAnimator と同じ速さで寄せる。
            // 出てきた直後は前の傾きが無いので、起き上がった姿から倒れ込まないよう、そのまま置く。
            // パラシュートはオフラインの ParachuteMovement と同じく移動の横取りなので、リーンや壁走りの傾きに替えて立ったまま吊る。
            // 開いた tick にリーンがまだ戻りきっていなくても傾けないよう、届いたリーンの量は読まない。
            IsParachuting = pose.Parachuting;
            WeaponLowering = pose.HolsterLowering;
            IsWeaponHidden = pose.IsWeaponHidden;
            ShowBodyWeapon(pose.ShownWeaponId);
            ShowWeaponLight(pose.ShowsWeaponLight);
            var target = pose.Parachuting
                ? Quaternion.identity
                : PlayerMockPose.TraversalLean(pose.Lean, pose.Swimming, pose.WallRun, pose.Parkour, _leanAngleDegrees);
            var targetDrop = pose.Parachuting ? 0f : PlayerMockPose.ParkourDrop(pose.Parkour);
            var blend = PlayerMockPose.BlendFactor(Time.deltaTime);
            _tilt = _snapTilt ? target : Quaternion.Slerp(_tilt, target, blend);
            _drop = _snapTilt ? targetDrop : Mathf.Lerp(_drop, targetDrop, blend);
            _snapTilt = false;
            PlaceBody();
        }

        // 持ち替えたときだけ引き直す。毎 tick 置き直すと、プレハブの複製が積もる
        private void ShowBodyWeapon(string weaponId)
        {
            var id = string.IsNullOrEmpty(weaponId) ? null : weaponId;
            var armed = id != null;
            if (_rightHandGun.gameObject.activeSelf != armed)
            {
                _rightHandGun.gameObject.SetActive(armed);
                _leftHandGun.gameObject.SetActive(armed);
            }

            if (string.Equals(id, _bodyGuns.WeaponId, StringComparison.Ordinal))
            {
                return;
            }

            var spec = id != null && _weaponSpecSource != null ? _weaponSpecSource(id) : null;

            // 届いた Id は信用できないので、Id の違う Spec が返っても体の銃 (BodyWeaponModels.Show) で投げさせず、二丁持ちと分からない武器として見せる
            if (spec != null && !string.Equals(spec.Id, id, StringComparison.Ordinal))
            {
                spec = null;
            }

            _bodyGuns.Show(id, spec, id != null ? WeaponModels : null);
        }

        private void CreateBodyGun()
        {
            _bodyGun = CreateChild(BodyGunName, transform);
            _rightHandGun = CreateChild(RightHandGunName, _bodyGun);
            _leftHandGun = CreateChild(PlayerMockBody.LeftHandGunName, _bodyGun);
            _bodyGuns = new BodyWeaponModels(_rightHandGun, _leftHandGun, CreateDefaultGun, _bodyGunRenderers);
            _rightHandGun.gameObject.SetActive(false);
            _leftHandGun.gameObject.SetActive(false);
        }

        private static Transform CreateChild(string childName, Transform parent)
        {
            var child = new GameObject(childName).transform;
            child.SetParent(parent, false);
            child.gameObject.layer = parent.gameObject.layer;
            return child;
        }

        private Transform CreateDefaultGun(Transform hand)
        {
            var prefab = Resources.Load<GameObject>(BodyGunResourcePath);
            if (prefab != null)
            {
                var instance = VisualPrefab.Instantiate(prefab, hand, prefab.name, stripColliders: true);
                VisualPrefab.SetLayer(instance, hand.gameObject.layer);
                _bodyGunRenderers.AddRange(instance.GetComponentsInChildren<Renderer>(true));
                return instance.transform;
            }

            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "GunBox";
            // Destroy は次のフレームまで残り、その間に射撃の Ray に当たるので、すぐ消す
            DestroyImmediate(box.GetComponent<Collider>());
            box.layer = hand.gameObject.layer;
            box.transform.SetParent(hand, false);
            box.transform.localScale = PlayerMockPose.GunSize;
            var boxRenderer = box.GetComponent<Renderer>();
            if (_gunMaterial == null && boxRenderer.sharedMaterial != null)
            {
                // プリミティブに付いてくる材質を元にすると、今の描画パイプラインのシェーダーをそのまま使える
                _gunMaterial = new Material(boxRenderer.sharedMaterial) { name = "RemoteGun", color = PlayerMockBody.GunColor };
            }

            if (_gunMaterial != null)
            {
                boxRenderer.sharedMaterial = _gunMaterial;
            }

            _bodyGunRenderers.Add(boxRenderer);
            return box.transform;
        }

        private void OnDestroy()
        {
            if (_gunMaterial == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_gunMaterial);
            }
            else
            {
                DestroyImmediate(_gunMaterial);
            }
        }

        // オフラインの体と同じく足元を軸に傾けるので、置き場を足元に置いて体と同じ傾きで回し、銃の構えは足元からの位置で決める
        private void PlaceBodyGun()
        {
            if (_bodyGun == null)
            {
                return;
            }

            var standing = _geometry.Standing;
            var shape = _geometry.Shape(_stance);
            _bodyGun.localPosition = new Vector3(0f, shape.BottomY, 0f) + (Vector3.down * _drop);
            _bodyGun.localRotation = _tilt;
            var right = PlayerMockPose.HeldBodyGun(WeaponLowering, shape.Height / standing.Height);
            var left = PlayerMockPose.LeftHandGun(right);
            _rightHandGun.localPosition = right.Position;
            _rightHandGun.localRotation = right.Rotation;
            _leftHandGun.localPosition = left.Position;
            _leftHandGun.localRotation = left.Rotation;
        }

        // 光を体の子に付けるのは、隠れている間 (SetConcealed) に体と一緒に止めるため。銃口のソケットがあればそこから照らす
        private void ShowWeaponLight(bool on)
        {
            IsWeaponLightOn = on;
            if (!on)
            {
                if (_weaponLight != null)
                {
                    _weaponLight.SetOn(false);
                }

                return;
            }

            if (_weaponLight == null)
            {
                _weaponLight = WeaponLightBeam.Create(Sockets(), _body, WeaponLightSpec, WeaponLightCatalog);
            }
            else
            {
                PlaceWeaponLight();
            }

            _weaponLight.SetOn(true);
        }

        private void PlaceWeaponLight()
        {
            if (_weaponLight != null)
            {
                _weaponLight.Place(Sockets(), _body);
            }
        }

        private void ConfigureWeaponLight()
        {
            if (_weaponLight != null)
            {
                var spec = WeaponLightSpec;
                _weaponLight.Configure(spec, spec.VisualId, WeaponLightCatalog);
            }
        }

        private WeaponSockets Sockets()
        {
            var presentation = GetComponent<WeaponPresentation>();
            return presentation != null ? presentation.Sockets : WeaponSockets.None;
        }

        private static Transform CreateBody(GameObject skin, StanceGeometry geometry, Transform parent)
        {
            var prefab = skin != null ? skin : Resources.Load<GameObject>(BodyResourcePath);
            GameObject body;
            if (prefab != null)
            {
                body = Instantiate(prefab, parent, false);
                if (skin != null)
                {
                    // スキンは他のゲームの体を流用して当たりが付いていることがあり、自分の体と射撃の Ray に混ざるので捨てる
                    foreach (var collider in body.GetComponentsInChildren<Collider>(true))
                    {
                        collider.enabled = false;
                        DestroyCollider(collider);
                    }
                }
            }
            else
            {
                body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.transform.SetParent(parent, false);

                // 当たりは .fpsmap とサーバーが取るので、見た目に Unity の物理を持たせない
                DestroyCollider(body.GetComponent<Collider>());
                var standing = geometry.Standing;
                body.transform.localScale = new Vector3(geometry.Radius * 2f, standing.Height * 0.5f, geometry.Radius * 2f);
                body.transform.localPosition = new Vector3(0f, standing.CenterY, 0f);
            }

            body.name = _bodyName;
            return body.transform;
        }

        private static void DestroyCollider(Collider collider)
        {
            if (collider == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(collider);
            }
            else
            {
                DestroyImmediate(collider);
            }
        }

        private void ApplyStance(PlayerStance stance)
        {
            _stance = stance;
            var standing = _geometry.Standing;
            var shape = _geometry.Shape(stance);
            var scale = _standingScale;
            scale.y *= shape.Height / standing.Height;
            _body.localScale = scale;
            PlaceBody();
        }

        // オフラインの体と同じく足元を軸に回すため、見た目の位置も傾きで回す。見た目だけを回すと体の中心を軸に回り、足が地面から浮く。
        private void PlaceBody()
        {
            var standing = _geometry.Standing;
            var shape = _geometry.Shape(_stance);
            var upright = new Vector3(0f, _standingY + shape.CenterY - standing.CenterY, 0f);
            var feet = new Vector3(0f, shape.BottomY, 0f);
            _body.localPosition = feet + (_tilt * (upright - feet)) + (Vector3.down * _drop);
            _body.localRotation = _tilt;
            PlaceBodyGun();
        }
    }
}

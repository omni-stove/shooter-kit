using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 三人称の体の両手の銃の見た目。オフラインの自機 (PlayerMockBody) とオンラインのほかの人 (NetV2RemotePlayer) が同じ置き方で持つよう、体の作りから切り離す。
    // 置き場の姿勢は持ち主が決め、ここは置き場の下の見た目だけを替える。
    public sealed class BodyWeaponModels
    {
        public const string LeftHandGunModelName = "LeftHandGunModel";

        private readonly Transform _rightHand;
        private readonly Transform _leftHand;
        private readonly Transform _defaultGun;
        private readonly Func<Transform, Transform> _createDefaultGun;
        private readonly List<Renderer> _renderers;
        private Transform _model;
        private Transform _leftHandModel;

        // 仮の銃の作り方を受け取るのは、持ち主ごとに仮の銃のプレハブと材質の持ち方が違うため。作った描画物は持ち主が renderers に足す。
        public BodyWeaponModels(Transform rightHand, Transform leftHand, Func<Transform, Transform> createDefaultGun, List<Renderer> renderers)
        {
            _rightHand = rightHand != null ? rightHand : throw new ArgumentNullException(nameof(rightHand));
            _leftHand = leftHand != null ? leftHand : throw new ArgumentNullException(nameof(leftHand));
            _createDefaultGun = createDefaultGun ?? throw new ArgumentNullException(nameof(createDefaultGun));
            _renderers = renderers ?? throw new ArgumentNullException(nameof(renderers));
            _defaultGun = _createDefaultGun(_rightHand);
        }

        public Transform Model => _model;

        public Transform LeftHandModel => _leftHandModel;

        public string WeaponId { get; private set; }

        public void SetModel(GameObject model)
        {
            Remove(ref _model);
            if (model != null)
            {
                _model = Place(model, _rightHand);
            }

            if (_defaultGun != null)
            {
                _defaultGun.gameObject.SetActive(_model == null);
            }
        }

        // 二丁持ちかを Id から引かず spec で受け取るのは、オフラインは WeaponController の武器の Spec を、オンラインのほかの人はスナップショットの武器 Id から引いた Spec を渡し、引き方が違うため。
        public void Show(string weaponId, WeaponSpec spec, WeaponExtrasCatalog catalog)
        {
            var id = RequireSpecOf(weaponId, spec);
            WeaponId = id;
            WeaponExtrasAssets assets = null;
            if (id != null && catalog != null)
            {
                catalog.TryGetWeapon(id, out assets);
            }

            SetModel(assets != null ? assets.PickupModel : null);
            SetLeftHand(spec != null && spec.IsAkimbo, assets);
        }

        internal static string RequireSpecOf(string weaponId, WeaponSpec spec)
        {
            var id = string.IsNullOrEmpty(weaponId) ? null : weaponId;
            if (spec != null && !string.Equals(spec.Id, id, StringComparison.Ordinal))
            {
                throw new ArgumentException($"spec '{spec.Id}' is not the spec of weapon '{id}'", nameof(spec));
            }

            return id;
        }

        public void Forget()
        {
            WeaponId = null;
        }

        // 左右反転は左手のプレハブが無いときだけにする。左手用に作られたプレハブを反転すると右手の向きに戻ってしまう。
        private void SetLeftHand(bool akimbo, WeaponExtrasAssets assets)
        {
            Remove(ref _leftHandModel);
            if (!akimbo)
            {
                return;
            }

            var holder = new GameObject(LeftHandGunModelName).transform;
            holder.SetParent(_leftHand, false);
            holder.gameObject.layer = _leftHand.gameObject.layer;
            holder.localPosition = assets != null ? assets.LeftHandBodyOffset : Vector3.zero;
            var leftPrefab = assets != null ? assets.LeftHandBodyModel : null;
            if (leftPrefab != null)
            {
                Place(leftPrefab, holder);
            }
            else
            {
                holder.localScale = new Vector3(-1f, 1f, 1f);
                var rightModel = assets != null ? assets.PickupModel : null;
                if (rightModel != null)
                {
                    Place(rightModel, holder);
                }
                else
                {
                    _createDefaultGun(holder);
                }
            }

            _leftHandModel = holder;
        }

        private Transform Place(GameObject prefab, Transform parent)
        {
            var instance = VisualPrefab.Instantiate(prefab, parent, prefab.name, stripColliders: true);
            VisualPrefab.SetLayer(instance, parent.gameObject.layer);
            _renderers.AddRange(instance.GetComponentsInChildren<Renderer>(true));
            return instance.transform;
        }

        private void Remove(ref Transform model)
        {
            if (model == null)
            {
                return;
            }

            var old = model;
            _renderers.RemoveAll(r => r == null || r.transform.IsChildOf(old));
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(old.gameObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(old.gameObject);
            }

            model = null;
        }
    }
}

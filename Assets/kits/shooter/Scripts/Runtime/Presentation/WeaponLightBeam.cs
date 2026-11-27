using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 点ける条件 (持ち替え・しまう・死亡) を持たず点灯と消灯だけを受けるのは、ネット越しに受け取ったほかの人の体の光にも同じ部品を使うため。
    [DisallowMultipleComponent]
    public sealed class WeaponLightBeam : MonoBehaviour
    {
        public const string ObjectName = "WeaponLightBeam";

        public const string BuiltLightName = "WeaponLightSpot";

        private WeaponLightSpec _spec;
        private string _visualId = string.Empty;
        private WeaponLightCatalog _catalog;
        private GameObject _visual;
        private Light _builtLight;
        private bool _isOn;

        public WeaponLightSpec Spec => _spec ?? WeaponLightSpec.Default;

        public string VisualId => _visualId;

        public bool IsOn => _isOn;

        public GameObject Visual => _visual;

        public Light BuiltLight => _builtLight;

        public bool UsesPrefab => _visual != null && _builtLight == null;

        public static WeaponLightBeam Create(WeaponSockets sockets, Transform fallback, WeaponLightSpec spec, WeaponLightCatalog catalog)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            return Create(sockets, fallback, spec, spec.VisualId, catalog);
        }

        public static WeaponLightBeam Create(WeaponSockets sockets, Transform fallback, WeaponLightSpec spec, string visualId, WeaponLightCatalog catalog)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var root = new GameObject(ObjectName);
            var beam = root.AddComponent<WeaponLightBeam>();
            beam.Place(sockets, fallback);
            beam.Configure(spec, visualId, catalog);
            return beam;
        }

        // 銃口のソケットが無い武器で狙いの真ん中から照らさないのは、銃口の炎と同じ位置 (WeaponSockets.DefaultMuzzleOffset) から出して光の出所をそろえるため。
        public bool Place(WeaponSockets sockets, Transform fallback)
        {
            var muzzle = sockets != null ? sockets.Muzzle : null;
            var parent = muzzle != null ? muzzle : fallback;
            if (parent == null)
            {
                return false;
            }

            if (transform.parent != parent)
            {
                transform.SetParent(parent, false);
            }

            transform.localPosition = muzzle != null ? Vector3.zero : WeaponSockets.DefaultMuzzleOffset;
            transform.localRotation = Quaternion.identity;
            return true;
        }

        public void Configure(WeaponLightSpec spec, string visualId, WeaponLightCatalog catalog)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var id = visualId ?? string.Empty;
            if (_visual != null
                && ReferenceEquals(spec, _spec)
                && ReferenceEquals(catalog, _catalog)
                && string.Equals(id, _visualId, StringComparison.Ordinal))
            {
                return;
            }

            DestroyVisual();
            _spec = spec;
            _visualId = id;
            _catalog = catalog;

            var prefab = WeaponLightCatalog.PrefabFor(catalog, id);
            if (prefab != null)
            {
                _visual = Instantiate(prefab, transform, false);
                _visual.name = prefab.name;
                _visual.transform.localPosition = Vector3.zero;
                _visual.transform.localRotation = Quaternion.identity;
            }
            else
            {
                _builtLight = BuildSpot(transform, spec);
                _visual = _builtLight.gameObject;
            }

            _visual.SetActive(_isOn);
        }

        public bool SetOn(bool on)
        {
            if (_visual != null && _visual.activeSelf != on)
            {
                _visual.SetActive(on);
            }

            if (_isOn == on)
            {
                return false;
            }

            _isOn = on;
            return true;
        }

        public void Remove()
        {
            DestroySafely(gameObject);
        }

        public static Light BuildSpot(Transform parent, WeaponLightSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var spot = new GameObject(BuiltLightName);
            spot.transform.SetParent(parent, false);
            var light = spot.AddComponent<Light>();
            light.type = LightType.Spot;
            light.range = spec.Range;
            light.spotAngle = spec.SpotAngle;
            light.intensity = spec.Intensity;
            return light;
        }

        private void DestroyVisual()
        {
            if (_visual != null)
            {
                // Play 中の Destroy はフレームの終わりまで残るので、差し替えた光と二重に照らさないよう先に消す。
                _visual.SetActive(false);
                DestroySafely(_visual);
            }

            _visual = null;
            _builtLight = null;
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
    }
}

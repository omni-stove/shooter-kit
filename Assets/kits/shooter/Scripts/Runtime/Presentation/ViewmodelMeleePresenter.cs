using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 振りの見た目と、振り始め・当てたときの音を受け持つ。当たりの手応え (画面の揺れ・止め) はゲームが自分の IMeleePresenter に差し替えて足す。
    // 音を鳴らすのは、振りが出たか・当たったかをビューモデルの小さな動きだけでは見分けにくいため。
    [DisallowMultipleComponent]
    public sealed class ViewmodelMeleePresenter : MonoBehaviour, IMeleePresenter
    {
        public const string LogTag = "[MeleeSound]";

        // 同じ振りの音の繰り返しに聞こえないよう、毎回高さを揺らす。
        public const float PitchJitter = 0.08f;

        [SerializeField, Tooltip("振りを見せるビューモデルの動き。空なら同じ物の ViewmodelMotion")]
        private ViewmodelMotion _motion;

        [SerializeField, Tooltip("振りと当たりの音を引く設定。空なら Resources の PresentationCatalog を探す")]
        private PresentationCatalog _catalog;

        [SerializeField, Range(0f, 1f), Tooltip("振りと当たりの音量")]
        private float _volume = 1f;

        private bool _catalogResolved;
        private AudioSource _source;

        public ViewmodelMotion Motion
        {
            get => _motion != null ? _motion : (_motion = GetComponent<ViewmodelMotion>());
            set => _motion = value;
        }

        public PresentationCatalog Catalog
        {
            get
            {
                if (_catalog == null && !_catalogResolved)
                {
                    _catalog = PresentationCatalog.Load();
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

        public AudioSource Source => _source;

        public void OnSwingStarted(GameObject attacker, MeleeSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            Play(Catalog != null ? Catalog.MeleeSwingSound : null, "swing");
            var motion = Motion;
            if (motion == null || !motion.enabled)
            {
                return;
            }

            motion.Swing(spec.SwingSeconds, spec.ImpactSeconds);
        }

        public void OnHit(GameObject attacker, Damageable target, Vector3 point, Vector3 direction, int damage)
        {
            Play(Catalog != null ? Catalog.MeleeHitSound : null, "hit");
        }

        public void OnMissed(GameObject attacker)
        {
        }

        private void Play(AudioClip clip, string kind)
        {
            if (clip == null || NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
            {
                return;
            }

            if (_source == null)
            {
                _source = gameObject.AddComponent<AudioSource>();
                _source.playOnAwake = false;
                _source.rolloffMode = AudioRolloffMode.Logarithmic;
                _source.minDistance = CharacterAudio.DefaultMinDistance;
                _source.maxDistance = CharacterAudio.DefaultMaxDistance;
                _source.dopplerLevel = 0f;
            }

            // ネット対戦の他人の振りは位置から聞こえるようにし、自分の振りは耳元で鳴らす (DryFireSound と同じ)。
            var presentation = GetComponent<WeaponPresentation>();
            _source.spatialBlend = presentation == null || presentation.IsLocal ? 0f : 1f;
            _source.pitch = 1f + UnityEngine.Random.Range(-PitchJitter, PitchJitter);
            _source.PlayOneShot(clip, _volume);
            Debug.Log($"{LogTag} {kind} sound={clip.name}", this);
        }
    }
}

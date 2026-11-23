using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // ドアの前は +Z。板は XY 面に立ち、足元が transform の位置。破った人のいない側の箱 (奥行き・幅・高さは BreachSpec) の中の敵を怯ませる。
    // プレイヤーの入力はここで読まず、Kick / BeginCharge / CancelCharge を外から呼ぶ。
    [DisallowMultipleComponent]
    public sealed class BreachDoor : MonoBehaviour
    {
        public const string LogTag = BreachLog.Tag;

        public const string StaggerSource = "breach";

        public const string RefusedBreached = "breached";

        public const string RefusedCharging = "charging";

        public const string CancelledByCaller = "cancelled";

        [SerializeField, Tooltip("ログと出来事でドアを見分ける Id")]
        private string _id = string.Empty;

        [SerializeField, Tooltip("このドアだけの breach JSON。空なら KitTuning.Breach (Tuning/breach)")]
        private TextAsset _tuning;

        [SerializeField, Tooltip("板・破片・爆薬の見た目と音。空なら同じ GameObject のものを使い、無ければ足す")]
        private BreachDoorVisuals _visuals;

        private readonly BreachDoorState _state = new BreachDoorState();
        private readonly HashSet<StaggerReaction> _seen = new HashSet<StaggerReaction>();
        private readonly List<StaggerReaction> _targets = new List<StaggerReaction>();
        private BreachSpec _spec;
        private Collider[] _panelColliders = Array.Empty<Collider>();
        private GameObject _planter;
        private float _chargeFarSign = 1f;

        public string Id
        {
            get => _id;
            set => _id = value ?? string.Empty;
        }

        // 差し替えたら次に Bind するまで読み直さない。仕掛けている途中に秒数が変わらないようにするため。
        public TextAsset Tuning
        {
            get => _tuning;
            set => _tuning = value;
        }

        public BreachDoorVisuals Visuals
        {
            get => _visuals;
            set => _visuals = value;
        }

        public BreachSpec Spec => _spec;

        public bool IsBound => _spec != null;

        public BreachDoorPhase Phase => _state.Phase;

        public BreachStatus Status => _state.Status();

        public bool IsBreached => _state.IsBreached;

        public GameObject Planter => _planter;

        // 破ったうえで板の当たりが 1 つも残っていなければ通れる。
        public bool IsPassable
        {
            get
            {
                if (!_state.IsBreached)
                {
                    return false;
                }

                foreach (var collider in _panelColliders)
                {
                    if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        // Edit Mode では Awake が走らないので、テストやシーン生成からも呼べるように分けておく。
        public bool Bind()
        {
            if (_visuals == null)
            {
                _visuals = BreachDoorVisuals.For(gameObject);
            }

            _spec = _tuning != null ? KitTuning.Read(_tuning.text, _tuning.name, BreachJson.Parse, KitTuning.Breach) : KitTuning.Breach;
            _visuals.EnsurePanel();
            _panelColliders = _visuals.PanelColliders();
            if (_panelColliders.Length == 0)
            {
                Debug.LogWarning($"{LogTag} door={LogId} の板に当たりがありません。閉じていても通れます。", this);
            }

            return true;
        }

        public bool Kick(GameObject breacher)
        {
            EnsureBound();
            if (!_state.Kick())
            {
                Refuse(breacher, BreachMethod.Kick);
                return false;
            }

            var farSign = FarSignFrom(transform, breacher);
            var point = _visuals.PanelCenter();
            ClearPanel();
            Debug.Log(BreachLog.Kicked(LogId, ActorId(breacher)), this);
            _visuals.SwingOpen(farSign);
            _visuals.PlayKick(point, _spec.KickSoundRadius);
            Finish(BreachMethod.Kick, breacher, farSign, point);
            return true;
        }

        public bool BeginCharge(GameObject breacher)
        {
            EnsureBound();
            var plantSeconds = _spec.PlantSeconds;
            var fuseSeconds = _spec.FuseSeconds;
            if (!_state.IsClosed)
            {
                Refuse(breacher, BreachMethod.Charge);
                return false;
            }

            _planter = breacher;
            _chargeFarSign = FarSignFrom(transform, breacher);
            Debug.Log(BreachLog.PlantBegan(LogId, ActorId(breacher), plantSeconds, fuseSeconds), this);
            _visuals.ShowCharge(-_chargeFarSign);
            _state.BeginPlant(plantSeconds, fuseSeconds);
            Settle(BreachDoorPhase.Planting);
            return true;
        }

        // 仕掛け終わった爆薬は取り外せないので、取りやめられるのは仕掛けている途中だけ。
        public bool CancelCharge(string reason = CancelledByCaller)
        {
            if (!_state.Cancel())
            {
                return false;
            }

            Debug.Log(BreachLog.PlantCancelled(LogId, ActorId(_planter), reason), this);
            _visuals.HideCharge();
            _planter = null;
            return true;
        }

        public void Tick(float deltaSeconds)
        {
            if (!_state.IsTiming)
            {
                return;
            }

            var before = _state.Phase;
            if (_state.Tick(deltaSeconds))
            {
                Settle(before);
            }
        }

        // 破った人のいない側。ドアの前 (+Z) にいれば奥 (-1)、後ろか真横か人がいなければ前 (+1)。
        public static float FarSignFrom(Transform door, GameObject breacher)
        {
            if (door == null)
            {
                throw new ArgumentNullException(nameof(door));
            }

            if (breacher == null)
            {
                return 1f;
            }

            return Vector3.Dot(breacher.transform.position - door.position, door.forward) > 0f ? -1f : 1f;
        }

        public static void RoomBox(Transform door, float farSign, BreachSpec spec, out Vector3 center, out Vector3 halfExtents)
        {
            if (door == null)
            {
                throw new ArgumentNullException(nameof(door));
            }

            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            halfExtents = new Vector3(spec.RoomWidth * 0.5f, spec.RoomHeight * 0.5f, spec.RoomDepth * 0.5f);
            center = door.position + (door.rotation * new Vector3(0f, halfExtents.y, Mathf.Sign(farSign) * halfExtents.z));
        }

        private void Awake()
        {
            Bind();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void Settle(BreachDoorPhase before)
        {
            if (before == BreachDoorPhase.Planting && _state.Phase != BreachDoorPhase.Planting)
            {
                Debug.Log(BreachLog.Armed(LogId, ActorId(_planter), _spec.FuseSeconds), this);
            }

            if (_state.Phase == BreachDoorPhase.Destroyed)
            {
                Detonate();
            }
        }

        private void Detonate()
        {
            var breacher = _planter;
            _planter = null;
            var center = _visuals.ChargePoint(-_chargeFarSign);

            // 板の当たりが残ると、向こう側の相手への線が板で遮られ爆発が届かないので、先に外す。
            ClearPanel();
            _visuals.Shatter(_chargeFarSign);
            if (_spec.ChargeRadius > 0f)
            {
                var explosion = new ExplosionSpec(new ExplosionDamage(_spec.ChargeDamage, _spec.ChargeRadius));
                Explosion.Detonate(center, explosion, breacher, _spec.ChargeItemId, playEffects: false);
            }

            _visuals.PlayExplosion(center);
            Debug.Log(BreachLog.Detonated(LogId, ActorId(breacher), _spec.ChargeDamage, _spec.ChargeRadius), this);
            Finish(BreachMethod.Charge, breacher, _chargeFarSign, center);
        }

        private void Finish(BreachMethod method, GameObject breacher, float farSign, Vector3 soundPoint)
        {
            var kind = BreachSpec.StaggerKindFor(method);
            var seconds = _spec.StaggerSecondsFor(method);
            var staggered = 0;
            foreach (var target in FindRoomTargets(farSign, breacher))
            {
                if (target.Impose(kind, seconds, StaggerSource).Staggered)
                {
                    staggered++;
                }
            }

            Debug.Log(BreachLog.Staggered(LogId, method, kind, seconds, staggered), this);

            var soundKind = method == BreachMethod.Kick ? SoundCueKind.Clatter : SoundCueKind.Explosion;
            SoundStimuli.Emit(
                breacher != null ? breacher : gameObject,
                new SoundStimulus(soundKind, soundPoint.ToNumerics(), _spec.SoundRadiusFor(method), false));
            CombatEvents.RaiseDoorBreached(gameObject, method, breacher);
        }

        private List<StaggerReaction> FindRoomTargets(float farSign, GameObject breacher)
        {
            _seen.Clear();
            _targets.Clear();
            RoomBox(transform, farSign, _spec, out var center, out var halfExtents);
            var colliders = Physics.OverlapBox(center, halfExtents, transform.rotation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            foreach (var collider in colliders)
            {
                var reaction = ReactionOf(collider);
                if (reaction == null || !reaction.IsBound || !reaction.enabled || !reaction.gameObject.activeInHierarchy || !_seen.Add(reaction))
                {
                    continue;
                }

                if (breacher != null && reaction.transform.IsChildOf(breacher.transform))
                {
                    continue;
                }

                _targets.Add(reaction);
            }

            return _targets;
        }

        private static StaggerReaction ReactionOf(Collider collider)
        {
            var hitbox = Hitbox.FindFor(collider);
            if (hitbox != null && hitbox.Owner != null)
            {
                var owned = hitbox.Owner.GetComponent<StaggerReaction>();
                if (owned != null)
                {
                    return owned;
                }
            }

            return collider.GetComponentInParent<StaggerReaction>();
        }

        private void ClearPanel()
        {
            foreach (var collider in _panelColliders)
            {
                if (collider != null)
                {
                    collider.enabled = false;
                }
            }
        }

        private void Refuse(GameObject actor, BreachMethod method)
        {
            var reason = _state.IsBreached ? RefusedBreached : RefusedCharging;
            Debug.Log(BreachLog.Refused(LogId, ActorId(actor), method, _state.Phase, reason), this);
        }

        private void EnsureBound()
        {
            if (!IsBound)
            {
                Bind();
            }
        }

        [ContextMenu("Kick")]
        private void KickFromMenu()
        {
            Kick(LocalPlayer());
        }

        [ContextMenu("Begin Charge")]
        private void BeginChargeFromMenu()
        {
            BeginCharge(LocalPlayer());
        }

        [ContextMenu("Cancel Charge")]
        private void CancelChargeFromMenu()
        {
            CancelCharge();
        }

        // メニューから破るときは、手元のプレイヤーを破った人にする。いなければ前 (+Z) の向こう側を怯ませる。
        private static GameObject LocalPlayer()
        {
            foreach (var damageable in FindObjectsByType<Damageable>(FindObjectsInactive.Exclude))
            {
                if (damageable.IsLocalPlayer)
                {
                    return damageable.gameObject;
                }
            }

            return null;
        }

        private string LogId => _id;

        private static string ActorId(GameObject actor)
        {
            if (actor == null)
            {
                return string.Empty;
            }

            var damageable = actor.GetComponentInParent<Damageable>();
            return damageable != null ? damageable.Id : string.Empty;
        }
    }
}

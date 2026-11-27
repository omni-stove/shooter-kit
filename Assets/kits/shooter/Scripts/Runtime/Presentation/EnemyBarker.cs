using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Shooter.Runtime
{
    // EnemyAgent・EnemyBrain に掛け声を書き足さないのは、交戦の挙動の作業と同じファイルを触らず、掛け声を使わないゲームでは付けないだけで済むようにするため。
    // そのため公開された状態 (State・Weapon.IsReloading) と出来事 (Damageable.Damaged・Died) と ThrownGrenade.Live だけを外から見る。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyAgent))]
    public sealed class EnemyBarker : MonoBehaviour
    {
        private static readonly Func<int, int> RandomBelow = count => Random.Range(0, count);

        [SerializeField, Tooltip("掛け声の声。空なら入口 (GameContent) か Resources の BarkCatalog を使う。それも無いか種類の声が空なら声は鳴らさない (掛け声の出来事とログは出す)")]
        private BarkCatalog _catalog;

        [SerializeField, Tooltip("声を鳴らす AudioSource。空なら初めて鳴らすときにこの GameObject へ足し、聞こえる距離 (barks.json の hearingDistance) まで届くようにする")]
        private AudioSource _voice;

        private readonly HashSet<Damageable> _watched = new HashSet<Damageable>();
        private readonly List<Damageable> _gone = new List<Damageable>();
        private Damageable[] _watchedFrom;
        private EnemyAgent _agent;
        private Damageable _self;
        private BarkCatalog _loadedCatalog;
        private bool _catalogLoaded;
        private BarkObservation _previous;
        private bool _hasPrevious;
        private bool _wasHit;
        private bool _allyWentDown;
        private string _spokenAs;

        public EnemyAgent Agent => _agent != null ? _agent : (_agent = GetComponent<EnemyAgent>());

        public BarkCatalog Catalog
        {
            get
            {
                if (_catalog != null)
                {
                    return _catalog;
                }

                // 鳴らすたびに Resources を引かないよう、最初に引いた結果を覚える。
                if (!_catalogLoaded)
                {
                    _loadedCatalog = BarkCatalog.Load();
                    _catalogLoaded = true;
                }

                return _loadedCatalog;
            }

            set => _catalog = value;
        }

        public AudioSource Voice
        {
            get => _voice;
            set => _voice = value;
        }

        public AudioClip LastClip { get; private set; }

        private void OnDisable()
        {
            // 無効の間に起きた変化を、有効に戻った最初のフレームで掛け声にしないよう、比べる元から取り直す。
            ForgetObservation();
        }

        private void OnDestroy()
        {
            Unbind();
            foreach (var body in _watched)
            {
                // 破棄された体も C# の購読は残っているので、Unity の == null ではなく参照で見て外す。
                if (!ReferenceEquals(body, null))
                {
                    body.Died -= OnBodyDied;
                }
            }

            _watched.Clear();
            if (!string.IsNullOrEmpty(_spokenAs))
            {
                BarkBoard.Forget(_spokenAs);
            }
        }

        private void Update()
        {
            Tick(Time.time);
        }

        // Update から呼ぶだけにせず公開するのは、Edit Mode のテストが時刻を渡して 1 フレームずつ進められるようにするため。
        public BarkCue? Tick(float time)
        {
            var agent = Agent;
            if (agent == null || !agent.IsInitialized)
            {
                return null;
            }

            Bind(agent);
            WatchAllies(time);

            var position = agent.EyePosition;
            if (!StuckMonitor.IsFinite(position.ToNumerics()))
            {
                return null;
            }

            var current = Observe(agent);
            _wasHit = false;
            _allyWentDown = false;

            // 最初の観測は比べる元にするだけにするのは、交戦の途中で付けたときに Spotted などを言わせないため。
            if (!_hasPrevious)
            {
                _previous = current;
                _hasPrevious = true;
                return null;
            }

            var previous = _previous;
            _previous = current;

            var selector = BarkBoard.Selector;
            var kinds = BarkTriggers.Pick(previous, current, selector.Spec);
            if (kinds.Count == 0)
            {
                return null;
            }

            var speakerId = agent.Id;
            if (string.IsNullOrEmpty(speakerId))
            {
                return null;
            }

            foreach (var kind in kinds)
            {
                if (selector.TryBark(speakerId, kind, time, position.ToNumerics()) != BarkVerdict.Allowed)
                {
                    continue;
                }

                var cue = new BarkCue(speakerId, agent.Archetype, kind, position, selector.Spec.HearingDistance);
                Speak(cue);
                return cue;
            }

            return null;
        }

        // 陣営が分かれば陣営で分け、分からなければ敵どうしを味方とみなす。EnemyAgent が陣営の無いときにプレイヤーだけを狙うのと同じ分け方。
        public static bool IsHostile(GameObject self, GameObject other)
        {
            if (self == null || other == null || IsSameBody(self, other))
            {
                return false;
            }

            var mine = SideMember.Of(self);
            var theirs = SideMember.Of(other);
            if (mine != null && theirs != null)
            {
                return !mine.IsAllyOf(theirs);
            }

            return other.GetComponentInParent<EnemyAgent>() == null;
        }

        public static bool IsAlly(GameObject self, GameObject other)
        {
            if (self == null || other == null || IsSameBody(self, other))
            {
                return false;
            }

            var mine = SideMember.Of(self);
            var theirs = SideMember.Of(other);
            if (mine != null && theirs != null)
            {
                return mine.IsAllyOf(theirs);
            }

            return other.GetComponentInParent<EnemyAgent>() != null;
        }

        // 欠けた声を飛ばして選ぶのは、カタログの検証 (Validate) が知らせるまでの間も、残った声で鳴らせるようにするため。
        public static AudioClip PickClip(IReadOnlyList<AudioClip> clips, Func<int, int> randomBelow)
        {
            if (randomBelow == null)
            {
                throw new ArgumentNullException(nameof(randomBelow));
            }

            if (clips == null)
            {
                return null;
            }

            var count = 0;
            foreach (var clip in clips)
            {
                if (clip != null)
                {
                    count++;
                }
            }

            if (count == 0)
            {
                return null;
            }

            var pick = Mathf.Clamp(randomBelow(count), 0, count - 1);
            foreach (var clip in clips)
            {
                if (clip == null)
                {
                    continue;
                }

                if (pick == 0)
                {
                    return clip;
                }

                pick--;
            }

            return null;
        }

        private BarkObservation Observe(EnemyAgent agent)
        {
            var weapon = agent.Weapon;
            var dead = _self != null && _self.IsDead;
            return new BarkObservation(
                agent.State,
                weapon != null && weapon.IsReloading,
                _wasHit && !dead,
                NearestHostileGrenadeDistance(agent.GroundPosition),
                _allyWentDown,
                dead);
        }

        private float? NearestHostileGrenadeDistance(Vector3 from)
        {
            float? nearest = null;
            foreach (var grenade in ThrownGrenade.Live)
            {
                if (grenade == null || grenade.HasExploded || !IsHostile(gameObject, grenade.Thrower))
                {
                    continue;
                }

                var distance = Vector3.Distance(from, grenade.transform.position);
                if (float.IsNaN(distance) || float.IsInfinity(distance))
                {
                    continue;
                }

                if (!nearest.HasValue || distance < nearest.Value)
                {
                    nearest = distance;
                }
            }

            return nearest;
        }

        private void Speak(BarkCue cue)
        {
            _spokenAs = cue.SpeakerId;
            var clip = PickClip(BarkCatalog.ClipsFor(Catalog, cue.Kind), RandomBelow);
            LastClip = clip;
            if (clip != null)
            {
                EnsureVoice(cue.HearingDistance).PlayOneShot(clip);
            }

            Debug.Log(BarkLog.Spoken(cue.SpeakerId, cue.Archetype, cue.Kind, cue.HearingDistance), this);
            BarkEvents.Raise(cue);
        }

        // 渡された AudioSource の設定は変えないのは、ゲームが距離の減り方や音量を自分で決められるようにするため。
        private AudioSource EnsureVoice(float hearingDistance)
        {
            if (_voice != null)
            {
                return _voice;
            }

            _voice = gameObject.AddComponent<AudioSource>();
            _voice.playOnAwake = false;
            _voice.loop = false;
            _voice.spatialBlend = 1f;
            _voice.dopplerLevel = 0f;
            _voice.rolloffMode = AudioRolloffMode.Linear;
            _voice.minDistance = Mathf.Min(1f, hearingDistance);
            _voice.maxDistance = hearingDistance;
            return _voice;
        }

        private void Bind(EnemyAgent agent)
        {
            var self = agent.GetComponent<Damageable>();
            if (self == _self)
            {
                return;
            }

            Unbind();
            _self = self;
            if (_self != null)
            {
                _self.Damaged += OnSelfDamaged;
                _self.Died += OnSelfDied;
            }
        }

        private void Unbind()
        {
            if (!ReferenceEquals(_self, null))
            {
                _self.Damaged -= OnSelfDamaged;
                _self.Died -= OnSelfDied;
            }

            _self = null;
        }

        // 倒れた体を一覧から外すのは、一覧を取り直したときだけにする。毎フレーム外すと話し手の数だけ一覧をなめ直すことになるため。
        private void WatchAllies(float time)
        {
            var bodies = BarkBoard.Bodies(time);
            if (ReferenceEquals(bodies, _watchedFrom))
            {
                return;
            }

            _watchedFrom = bodies;
            foreach (var watched in _watched)
            {
                if (watched == null)
                {
                    _gone.Add(watched);
                }
            }

            foreach (var gone in _gone)
            {
                if (!ReferenceEquals(gone, null))
                {
                    gone.Died -= OnBodyDied;
                }

                _watched.Remove(gone);
            }

            _gone.Clear();

            // 味方かどうかは倒れたときに見るので、ここでは自分以外のすべての体を見張る。陣営は試合の途中で変わることがあるため。
            foreach (var body in bodies)
            {
                if (body == null || body == _self || !_watched.Add(body))
                {
                    continue;
                }

                body.Died += OnBodyDied;
            }
        }

        private void OnSelfDamaged(Damageable self, DamageReport report)
        {
            if (self != null && !self.IsDead)
            {
                _wasHit = true;
            }
        }

        // 倒れた話し手を忘れさせないと、BarkSelector の話し手ごとの記録が試合の間たまり続けるため。
        private void OnSelfDied(Damageable self)
        {
            if (!string.IsNullOrEmpty(_spokenAs))
            {
                BarkBoard.Forget(_spokenAs);
                _spokenAs = null;
            }
        }

        private void OnBodyDied(Damageable body)
        {
            var agent = Agent;
            if (body == null || agent == null || !IsAlly(gameObject, body.gameObject))
            {
                return;
            }

            var hearing = BarkBoard.Spec.HearingDistance;
            if ((body.GroundPoint - agent.GroundPosition).sqrMagnitude <= hearing * hearing)
            {
                _allyWentDown = true;
            }
        }

        private void ForgetObservation()
        {
            _hasPrevious = false;
            _wasHit = false;
            _allyWentDown = false;
        }

        private static bool IsSameBody(GameObject self, GameObject other)
        {
            return other == self || other.transform.IsChildOf(self.transform) || self.transform.IsChildOf(other.transform);
        }
    }
}

using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 陣営はシーンに置いたもの (1 人ずつ別の陣営) をそのまま使う。Juggernaut は FreeForAll の並びなので、Infected のように陣営を替えない。
    // ジャガーノートは持っている武器のままにする (武器を配り替えない)。強めるのは体力の上限と bot の移動の速さだけ。
    // 自分がジャガーノートのときは自分の頭上に目印を出さない。自分には HUD で知らせるため (GuardianRunner と同じ)。
    public sealed class JuggernautRunner : ModeRunnerBase
    {
        public const int DefaultJuggernautMaxHealth = JuggernautSpec.DefaultJuggernautMaxHealth;
        public const float DefaultJuggernautSpeedMultiplier = JuggernautSpec.DefaultJuggernautSpeedMultiplier;
        public const int DefaultScorePerKillAsJuggernaut = JuggernautSpec.DefaultScorePerKillAsJuggernaut;
        public const int DefaultScorePerJuggernautKill = JuggernautSpec.DefaultScorePerJuggernautKill;
        public const int DefaultScoreLimit = JuggernautSpec.DefaultScoreLimit;
        public const float DefaultTimeLimitSeconds = JuggernautSpec.DefaultTimeLimitSeconds;
        public const float DefaultStartDelaySeconds = JuggernautSpec.DefaultStartDelaySeconds;
        public const float DefaultRespawnSeconds = FreeForAllMode.DefaultRespawnSeconds;
        public const string MarkerRootName = "JuggernautMarkers";

        [SerializeField, Tooltip("このモードの調整値のアセット。空なら調整値の JSON (GameContent の JuggernautTuning か Resources/Tuning/juggernaut) と下のリスポーンの秒数を読む")]
        private JuggernautDefinition _definition;

        [SerializeField, Min(0f), Tooltip("定義アセットが空のとき、倒れてから出現点に戻るまでの秒数")]
        private float _respawnSeconds = DefaultRespawnSeconds;

        [SerializeField, Tooltip("ジャガーノートの頭上に目印を出すか")]
        private bool _showMarker = true;

        [SerializeField, Tooltip("ジャガーノートの頭上の目印の見た目。原点が目印の下端で、縦の軸で回す。当たり判定は外し、材質は陣営の色に塗る。空なら仮の菱形を作る")]
        private GameObject _markerPrefab;

        private readonly Dictionary<SideMember, OriginalBody> _originals = new Dictionary<SideMember, OriginalBody>();
        private readonly List<JuggernautTargetPerception> _perceptions = new List<JuggernautTargetPerception>();
        private Juggernaut _juggernaut;
        private Func<int, int> _juggernautPicker;
        private GuardianLeaderMarker _marker;
        private Transform _markerRoot;

        public Juggernaut Juggernaut => _juggernaut;

        public JuggernautDefinition Definition
        {
            get => _definition;
            set => _definition = value;
        }

        public float RespawnSeconds
        {
            get => _respawnSeconds;
            set => _respawnSeconds = value;
        }

        public bool ShowsMarker
        {
            get => _showMarker;
            set => _showMarker = value;
        }

        public GameObject MarkerPrefab
        {
            get => _markerPrefab;
            set => _markerPrefab = value;
        }

        // 0 から count 未満の添字を返す。空なら Juggernaut が乱数で選ぶ。テストでジャガーノートを決まった人にするために入れられるようにしておく。
        public Func<int, int> JuggernautPicker
        {
            get => _juggernautPicker;
            set => _juggernautPicker = value;
        }

        public GuardianLeaderMarker Marker => _marker;

        public IReadOnlyList<JuggernautTargetPerception> TargetPerceptions => _perceptions;

        public SideMember JuggernautMember
        {
            get
            {
                var id = _juggernaut != null ? _juggernaut.JuggernautId : null;
                return id.HasValue ? MemberOf(id.Value) : null;
            }
        }

        public float EffectiveRespawnSeconds => _definition != null ? _definition.RespawnSeconds : _respawnSeconds;

        public JuggernautSpec CreateSpec()
        {
            return _definition != null ? _definition.ToSpec() : KitTuning.Juggernaut;
        }

        public Juggernaut CreateJuggernaut()
        {
            var spec = CreateSpec();
            var respawnSeconds = EffectiveRespawnSeconds;
            try
            {
                return new Juggernaut(spec, respawnSeconds, _juggernautPicker);
            }
            catch (ArgumentException e)
            {
                var source = _definition != null ? $"juggernaut definition '{_definition.name}'" : $"{nameof(JuggernautRunner)} '{name}'";
                throw new InvalidOperationException($"{source} has an invalid respawn time: {e.Message}", e);
            }
        }

        public void Begin(
            Juggernaut juggernaut,
            IReadOnlyList<SideMember> members,
            IReadOnlyList<SideSpawnPoint> spawnPoints,
            SideMember localPlayer = null)
        {
            if (juggernaut == null)
            {
                throw new ArgumentNullException(nameof(juggernaut));
            }

            if (members == null)
            {
                throw new ArgumentNullException(nameof(members));
            }

            if (IsStarted)
            {
                throw new InvalidOperationException($"{nameof(JuggernautRunner)} '{name}' has already started a mode");
            }

            Attach(juggernaut);
            try
            {
                Begin(juggernaut.Session, members, spawnPoints, localPlayer);
            }
            catch
            {
                Detach();
                throw;
            }
        }

        public bool IsJuggernaut(SideMember member)
        {
            var id = IdOf(member);
            return id >= 0 && _juggernaut != null && _juggernaut.JuggernautId == id;
        }

        protected override ModeSession CreateSession()
        {
            var juggernaut = CreateJuggernaut();
            Attach(juggernaut);
            return juggernaut.Session;
        }

        protected override void OnBegan()
        {
            foreach (var member in Members)
            {
                var agent = member != null ? member.GetComponent<EnemyAgent>() : null;
                if (agent == null)
                {
                    continue;
                }

                var perception = new JuggernautTargetPerception(agent, this);
                if (agent.AddPerceptionContributor(perception))
                {
                    _perceptions.Add(perception);
                }
            }
        }

        protected override void OnKill(SideMember killer, SideMember victim)
        {
            if (_juggernaut == null || victim == null)
            {
                return;
            }

            var victimId = IdOf(victim);
            if (victimId < 0)
            {
                return;
            }

            var killerId = IdOf(killer);
            _juggernaut.ReportFall(victimId, killerId >= 0 ? killerId : (int?)null);
        }

        protected override void OnEnded(Side? winner)
        {
            if (_juggernaut == null)
            {
                return;
            }

            var winnerId = _juggernaut.WinnerId;
            Debug.Log(JuggernautLog.Ended(winnerId, winnerId.HasValue ? _juggernaut.GetScore(winnerId.Value) : 0), this);
        }

        protected override object HudDetail
        {
            get
            {
                if (_juggernaut == null)
                {
                    return null;
                }

                var id = IdOf(LocalPlayer);
                return JuggernautStatus.Of(_juggernaut, id >= 0 ? id : (int?)null);
            }
        }

        protected override void OnStopping()
        {
            Detach();
        }

        private void Attach(Juggernaut juggernaut)
        {
            Detach();
            _juggernaut = juggernaut;
            _juggernaut.Changed += OnChanged;
        }

        // 止めたときに強めたままの体を残すと、同じシーンで次に始めたモードや bot にジャガーノートの体力と速さが残る。
        private void Detach()
        {
            if (_juggernaut != null)
            {
                _juggernaut.Changed -= OnChanged;
            }

            _juggernaut = null;
            foreach (var perception in _perceptions)
            {
                if (perception.Agent != null)
                {
                    perception.Agent.RemovePerceptionContributor(perception);
                }
            }

            _perceptions.Clear();
            foreach (var member in new List<SideMember>(_originals.Keys))
            {
                Restore(member, logged: false);
            }

            _originals.Clear();
            RemoveMarker();
        }

        private void OnChanged(JuggernautChange change)
        {
            var member = MemberOf(change.JuggernautId);
            Debug.Log(JuggernautLog.Changed(change), member != null ? member : (UnityEngine.Object)this);
            if (change.PreviousId.HasValue && change.PreviousId.Value != change.JuggernautId)
            {
                Restore(MemberOf(change.PreviousId.Value), logged: true);
            }

            if (member != null)
            {
                Strengthen(member, change.JuggernautId);
            }

            PlaceMarker(member);
            RefreshHud();
        }

        // 立っている人は新しい上限まで満たす。上限だけ上げると、撃ち合いで削れた体力のままジャガーノートを受け継ぎ、強い役になった意味が薄れる。
        // 倒れている (リスポーンを待つ) 人は満たせないので、戻るとき (SideMember.Respawn) に新しい上限で満ちる。
        private void Strengthen(SideMember member, int playerId)
        {
            var spec = _juggernaut.Spec;
            var damageable = member.Damageable;
            var agent = member.GetComponent<EnemyAgent>();
            if (!_originals.ContainsKey(member))
            {
                _originals[member] = new OriginalBody(
                    damageable != null ? damageable.MaxHealth : spec.JuggernautMaxHealth,
                    agent != null ? agent.MoveSpeedMultiplier : (float?)null);
            }

            if (damageable != null)
            {
                damageable.MaxHealth = spec.JuggernautMaxHealth;
                var session = Session;
                var standing = !damageable.IsDead && (session == null || !session.HasPlayer(playerId) || !session.IsDown(playerId));
                if (standing)
                {
                    damageable.ResetHealth();
                }
            }

            if (agent != null)
            {
                agent.MoveSpeedMultiplier = spec.JuggernautSpeedMultiplier;
            }

            Debug.Log(JuggernautLog.Body(playerId, spec.JuggernautMaxHealth, agent != null ? spec.JuggernautSpeedMultiplier : (float?)null), member);
        }

        // 前のジャガーノートはふつう倒れたところなので、上限を戻すだけで戻るときに元の体力で満ちる。立ったまま替わった (抜けた人の代わりなど) なら元の上限まで削る。
        private void Restore(SideMember member, bool logged)
        {
            if (member == null || !_originals.TryGetValue(member, out var original))
            {
                return;
            }

            _originals.Remove(member);
            var damageable = member.Damageable;
            if (damageable != null)
            {
                damageable.MaxHealth = original.MaxHealth;
                if (!damageable.IsDead && damageable.Health.Current > original.MaxHealth)
                {
                    damageable.RestoreHealth(original.MaxHealth);
                }
            }

            var agent = member.GetComponent<EnemyAgent>();
            if (agent != null && original.SpeedMultiplier.HasValue)
            {
                agent.MoveSpeedMultiplier = original.SpeedMultiplier.Value;
            }

            var id = IdOf(member);
            if (logged && id >= 0)
            {
                Debug.Log(JuggernautLog.Restored(id, original.MaxHealth, agent != null ? original.SpeedMultiplier : null), member);
            }
        }

        private void PlaceMarker(SideMember member)
        {
            RemoveMarker();
            if (!_showMarker || member == null || member == LocalPlayer)
            {
                return;
            }

            if (_markerRoot == null)
            {
                _markerRoot = new GameObject(MarkerRootName).transform;
                _markerRoot.SetParent(transform, false);
            }

            _marker = GuardianLeaderMarker.Create(member, _markerPrefab);
            _marker.transform.SetParent(_markerRoot, true);
        }

        private void RemoveMarker()
        {
            if (_marker != null)
            {
                _marker.Remove();
            }

            _marker = null;
        }

        private readonly struct OriginalBody
        {
            public OriginalBody(int maxHealth, float? speedMultiplier)
            {
                MaxHealth = maxHealth;
                SpeedMultiplier = speedMultiplier;
            }

            public int MaxHealth { get; }

            // EnemyAgent を持たない人 (操作しているプレイヤー) には速さを掛けないので null。
            public float? SpeedMultiplier { get; }
        }
    }
}

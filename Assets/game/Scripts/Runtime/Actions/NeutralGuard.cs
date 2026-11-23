using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // EnemyBrain の判断は変えず、敵対するまで知覚から相手を消すことで撃たせない。外すと普通の敵に戻る。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyAgent))]
    public sealed class NeutralGuard : MonoBehaviour, IPerceptionContributor
    {
        public const string LogTag = WeaponFreeZoneLog.Tag;

        [SerializeField, Tooltip("見張るゾーン。空なら Zone Id で探す")]
        private WeaponFreeZone _zone;

        [SerializeField, Tooltip("見張るゾーンの Id。Zone が空のときだけ使う")]
        private string _zoneId = string.Empty;

        private readonly NeutralGuardState _state = new NeutralGuardState();

        private EnemyAgent _agent;
        private Damageable _self;
        private Damageable _damagedSource;
        private bool _attached;
        private bool _searchedZone;

        public event Action<NeutralGuard, NeutralGuardPhase> PhaseChanged;

        public EnemyAgent Agent => _agent != null ? _agent : (_agent = GetComponent<EnemyAgent>());

        public WeaponFreeZone Zone
        {
            get => ResolveZone();
            set
            {
                _zone = value;
                _searchedZone = false;
            }
        }

        public string ZoneId
        {
            get => _zone != null ? _zone.ZoneId : _zoneId ?? string.Empty;
            set
            {
                _zoneId = value ?? string.Empty;
                _zone = null;
                _searchedZone = false;
            }
        }

        public NeutralGuardPhase Phase => _state.Phase;

        public bool IsHostile => _state.IsHostile;

        public bool IsAttached => _attached;

        private string GuardId => Agent != null ? Agent.Id : name;

        private void OnEnable()
        {
            Attach();
        }

        private void Start()
        {
            // 敵の Awake より先に付けられたときも、ここで撃たれた知らせを受け直す。
            Attach();
        }

        private void OnDisable()
        {
            Detach();
        }

        private void OnDestroy()
        {
            Detach();
        }

        private void Update()
        {
            Refresh();
        }

        // Edit Mode では OnEnable が呼ばれないので、置いた側やテストが呼べるよう公開する。
        public void Attach()
        {
            var agent = Agent;
            if (agent == null)
            {
                return;
            }

            agent.AddPerceptionContributor(this);
            _attached = true;

            var self = _self != null ? _self : (_self = GetComponent<Damageable>());
            if (!ReferenceEquals(_damagedSource, self))
            {
                Unlisten();
                if (self != null)
                {
                    self.Damaged += OnDamaged;
                    _damagedSource = self;
                }
            }
        }

        public void Detach()
        {
            Unlisten();
            if (!_attached)
            {
                return;
            }

            _attached = false;
            if (!ReferenceEquals(_agent, null))
            {
                _agent.RemovePerceptionContributor(this);
            }
        }

        // ゾーンの警告と敵対を写す。敵対した後はゾーンを見ない。
        public void Refresh()
        {
            if (_state.IsHostile)
            {
                return;
            }

            var zone = ResolveZone();
            if (zone == null)
            {
                return;
            }

            if (_state.Observe(zone.IsWarning, zone.IsHostile))
            {
                OnPhaseChanged(WeaponFreeZoneLog.ZoneReason);
            }
        }

        public bool Provoke()
        {
            if (!_state.Provoke())
            {
                return false;
            }

            OnPhaseChanged(WeaponFreeZoneLog.ShotReason);
            return true;
        }

        public EnemyPerception Contribute(EnemyPerception perception)
        {
            Refresh();
            if (_state.IsHostile)
            {
                return perception;
            }

            // 撃たれたことは OnDamaged で敵対に変えているので、ここまで来る WasHit は残しても相手を見つけない。
            return new EnemyPerception(
                perception.Self,
                null,
                false,
                false,
                null,
                perception.WasHit,
                perception.HitSource,
                perception.HealthFraction,
                0f,
                perception.IsSuppressed,
                perception.SuppressionLevel);
        }

        private void OnDamaged(Damageable self, DamageReport report)
        {
            Provoke();
        }

        private void OnPhaseChanged(string reason)
        {
            var phase = _state.Phase;
            Debug.Log(WeaponFreeZoneLog.GuardPhase(GuardId, ZoneId, phase, reason), this);

            // ゾーンで敵対したとき、見えていない相手のほうへも向かうよう、今の目標の位置を物音として渡す。
            if (phase == NeutralGuardPhase.Hostile && reason != WeaponFreeZoneLog.ShotReason)
            {
                var agent = Agent;
                var target = agent != null ? agent.Target : null;
                if (target != null && !target.IsDead)
                {
                    agent.Alert(target.GroundPoint);
                }
            }

            var handlers = PhaseChanged;
            if (handlers == null)
            {
                return;
            }

            foreach (var handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<NeutralGuard, NeutralGuardPhase>)handler)(this, phase);
                }
                catch (Exception e)
                {
                    Debug.LogException(e, this);
                }
            }
        }

        // シーン全体を探すのは重いので、Id で見つからなければ Zone か Zone Id を替えるまで探し直さない。
        private WeaponFreeZone ResolveZone()
        {
            if (_zone != null || _searchedZone)
            {
                return _zone;
            }

            _searchedZone = true;
            _zone = WeaponFreeZone.FindById(_zoneId);
            if (_zone == null)
            {
                Debug.LogWarning(WeaponFreeZoneLog.GuardWithoutZone(GuardId, _zoneId), this);
            }

            return _zone;
        }

        private void Unlisten()
        {
            // 破棄済みでも C# のオブジェクトは残っているので、購読は外せる。
            if (!ReferenceEquals(_damagedSource, null))
            {
                _damagedSource.Damaged -= OnDamaged;
            }

            _damagedSource = null;
        }
    }
}

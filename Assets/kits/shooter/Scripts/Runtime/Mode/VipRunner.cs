using System;
using System.Collections.Generic;
using GameKit.Core;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 陣営はシーンに置いたもの (Alpha と Bravo) のまま、攻守だけをラウンドごとに入れ替える。出現点の陣営を入れ替えるのは、攻める側がいつも攻める側の
    // 出現点から出るようにするため (人の陣営を替えると、味方の色やチームの記録まで入れ替わる)。
    // VIP は持っている武器のままにし、強めるのは体力の上限だけにする。自分が VIP のときは自分の頭上に目印を出さない。自分には HUD で知らせるため。
    // ラウンドの終わりの区切りは Vip が出すので、ModeSession の終わりを試合の終わりにしない (SearchAndDestroyRunner と同じ)。
    public sealed class VipRunner : ModeRunnerBase
    {
        public const float DefaultEscortRadius = 3f;
        public const float DefaultEscortRerouteDistance = 2f;
        public const string MarkerRootName = "VipMarkers";

        [SerializeField, Tooltip("このモードの調整値のアセット。空なら調整値の JSON (GameContent の VipTuning か Resources/Tuning/vip) を読む")]
        private VipDefinition _definition;

        [SerializeField, Tooltip("VIP が向かう脱出地点。空ならシーンの VipExitPoint を使う。1 つも無ければ警告を出して始めない")]
        private VipExitPoint[] _exitPoints = Array.Empty<VipExitPoint>();

        [SerializeField, Tooltip("VIP の頭上に目印を出すか")]
        private bool _showMarker = true;

        [SerializeField, Tooltip("VIP の頭上の目印の見た目。原点が目印の下端で、縦の軸で回す。当たり判定は外し、材質は陣営の色に塗る。空なら仮の菱形を作る")]
        private GameObject _markerPrefab;

        [SerializeField, Min(0.1f), Tooltip("守る側の bot が VIP の周りを回る半径 (m)")]
        private float _escortRadius = DefaultEscortRadius;

        [SerializeField, Min(0f), Tooltip("VIP がこの距離 (m) だけ動いたら、守る側の bot の行き先を引き直す")]
        private float _escortRerouteDistance = DefaultEscortRerouteDistance;

        private readonly List<VipExitPoint> _exits = new List<VipExitPoint>();
        private readonly Dictionary<SideMember, int> _originalMaxHealth = new Dictionary<SideMember, int>();
        private readonly Dictionary<SideSpawnPoint, Side> _originalSpawnSides = new Dictionary<SideSpawnPoint, Side>();
        private readonly Dictionary<EnemyAgent, Vector3[]> _homeRoutes = new Dictionary<EnemyAgent, Vector3[]>();
        private readonly List<VipTargetPerception> _perceptions = new List<VipTargetPerception>();
        private Vip _vip;
        private Func<int, int> _vipPicker;
        private GuardianLeaderMarker _marker;
        private Transform _markerRoot;
        private Vector3? _escortRoutedAt;
        private int? _escortRoutedVip;

        public Vip Vip => _vip;

        public VipDefinition Definition
        {
            get => _definition;
            set => _definition = value;
        }

        public IReadOnlyList<VipExitPoint> ConfiguredExitPoints => _exitPoints ?? Array.Empty<VipExitPoint>();

        public IReadOnlyList<VipExitPoint> ExitPoints => _exits;

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

        public float EscortRadius
        {
            get => _escortRadius;
            set => _escortRadius = value;
        }

        public float EscortRerouteDistance
        {
            get => _escortRerouteDistance;
            set => _escortRerouteDistance = value;
        }

        // 0 から count 未満の添字を返す。空なら Vip が乱数で選ぶ。テストで VIP を決まった人にするために入れられるようにしておく。
        public Func<int, int> VipPicker
        {
            get => _vipPicker;
            set => _vipPicker = value;
        }

        public GuardianLeaderMarker Marker => _marker;

        public IReadOnlyList<VipTargetPerception> TargetPerceptions => _perceptions;

        public SideMember VipMember
        {
            get
            {
                var id = _vip != null ? _vip.VipId : null;
                return id.HasValue ? MemberOf(id.Value) : null;
            }
        }

        public bool IsRoundLive => _vip != null && _vip.Phase == RoundPhase.Live && _vip.Mode.State == GameModeState.Playing;

        protected override bool EndsMatchWithSession => false;

        public void SetExitPoints(IReadOnlyList<VipExitPoint> exits)
        {
            var copy = new VipExitPoint[exits != null ? exits.Count : 0];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = exits[i];
            }

            _exitPoints = copy;
        }

        public VipSpec CreateSpec()
        {
            return _definition != null ? _definition.ToSpec() : KitTuning.Vip;
        }

        public Vip CreateVip()
        {
            return new Vip(CreateSpec(), _vipPicker);
        }

        // 並びはログの exit= の番号になるので、Play ごとに変わらないよう位置で並べる。
        public List<VipExitPoint> FindExitPoints()
        {
            var configured = ConfiguredExitPoints;
            IReadOnlyList<VipExitPoint> source = configured.Count > 0 ? configured : VipExitPoint.FindAll();
            return SortExits(source);
        }

        public static List<VipExitPoint> SortExits(IReadOnlyList<VipExitPoint> exits)
        {
            var sorted = new List<VipExitPoint>();
            if (exits == null)
            {
                return sorted;
            }

            foreach (var exit in exits)
            {
                if (exit != null && !sorted.Contains(exit))
                {
                    sorted.Add(exit);
                }
            }

            sorted.Sort((a, b) => ComparePositions(a.Position, b.Position));
            return sorted;
        }

        public int NearestExitIndex(Vector3 from)
        {
            var nearest = -1;
            var nearestDistance = float.PositiveInfinity;
            for (var i = 0; i < _exits.Count; i++)
            {
                var exit = _exits[i];
                if (exit == null)
                {
                    continue;
                }

                var distance = exit.HorizontalDistanceTo(from);
                if (distance < nearestDistance)
                {
                    nearest = i;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        public bool IsAtExit(Vector3 ground)
        {
            foreach (var exit in _exits)
            {
                if (exit != null && exit.Contains(ground))
                {
                    return true;
                }
            }

            return false;
        }

        public void Begin(
            Vip vip,
            IReadOnlyList<SideMember> members,
            IReadOnlyList<SideSpawnPoint> spawnPoints,
            IReadOnlyList<VipExitPoint> exits,
            SideMember localPlayer = null)
        {
            if (vip == null)
            {
                throw new ArgumentNullException(nameof(vip));
            }

            if (members == null)
            {
                throw new ArgumentNullException(nameof(members));
            }

            if (IsStarted)
            {
                throw new InvalidOperationException($"{nameof(VipRunner)} '{name}' has already started a mode");
            }

            var sorted = SortExits(exits);
            if (sorted.Count == 0)
            {
                throw new ArgumentException("there is no VipExitPoint for the VIP to reach", nameof(exits));
            }

            ValidateEscort();
            Attach(vip, sorted);
            try
            {
                Begin(vip.Mode, members, spawnPoints, localPlayer);
            }
            catch
            {
                Detach();
                throw;
            }
        }

        public bool IsVip(SideMember member)
        {
            var id = IdOf(member);
            return id >= 0 && _vip != null && _vip.IsVip(id);
        }

        public bool IsAttacker(SideMember member)
        {
            var side = SideOf(member);
            return side.HasValue && side.Value == _vip.Attacker;
        }

        public bool IsDefender(SideMember member)
        {
            var side = SideOf(member);
            return side.HasValue && side.Value == _vip.Defender;
        }

        protected override bool CanBegin()
        {
            if (FindExitPoints().Count > 0)
            {
                return true;
            }

            Debug.LogWarning(VipLog.NoExitLog(name), this);
            return false;
        }

        protected override ModeSession CreateSession()
        {
            var exits = FindExitPoints();
            if (exits.Count == 0)
            {
                throw new InvalidOperationException("シーンに VipExitPoint がありません。VIP の脱出地点を置いてください。");
            }

            ValidateEscort();
            var vip = CreateVip();
            Attach(vip, exits);
            return vip.Mode;
        }

        protected override void StartSession(ModeSession session)
        {
            if (_vip == null || session != _vip.Mode)
            {
                throw new InvalidOperationException($"{nameof(VipRunner)} can only start the session made by its own CreateSession or Begin");
            }

            _homeRoutes.Clear();
            foreach (var member in Members)
            {
                var agent = member != null ? member.GetComponent<EnemyAgent>() : null;
                if (agent != null && !_homeRoutes.ContainsKey(agent))
                {
                    _homeRoutes.Add(agent, CopyRoute(agent.PatrolRoute));
                }
            }

            _originalSpawnSides.Clear();
            foreach (var point in SpawnPoints)
            {
                if (point != null && !_originalSpawnSides.ContainsKey(point))
                {
                    _originalSpawnSides.Add(point, point.Side);
                }
            }

            Debug.Log(VipLog.SettingsLog(_vip.Spec), this);
            _vip.Start();
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

                var perception = new VipTargetPerception(agent, this);
                if (agent.AddPerceptionContributor(perception))
                {
                    _perceptions.Add(perception);
                }
            }
        }

        protected override void TickSession(ModeSession session, float deltaSeconds)
        {
            if (_vip == null)
            {
                session.Tick(deltaSeconds);
                return;
            }

            UpdatePresence();
            _vip.Tick(deltaSeconds);

            // Tick の中で Stop されたら、外した bot を動かさない。
            if (_vip == null)
            {
                return;
            }

            RouteEscorts();
        }

        protected override string SessionEndedLog(Side? winner)
        {
            // ModeSession が終わるのはラウンドの終わり。試合の終わりは Vip の Ended から EndMatch で出す。
            return null;
        }

        protected override object HudDetail
        {
            get
            {
                if (_vip == null)
                {
                    return null;
                }

                var id = IdOf(LocalPlayer);
                return VipStatus.Of(_vip, id >= 0 ? id : (int?)null);
            }
        }

        protected override void OnStop()
        {
            Detach();
        }

        private void ValidateEscort()
        {
            if (!(_escortRadius > 0f) || float.IsInfinity(_escortRadius))
            {
                throw new InvalidOperationException($"{nameof(VipRunner)} '{name}' has an invalid escort radius {_escortRadius}; it must be positive and finite");
            }

            if (!(_escortRerouteDistance >= 0f) || float.IsInfinity(_escortRerouteDistance))
            {
                throw new InvalidOperationException(
                    $"{nameof(VipRunner)} '{name}' has an invalid escort reroute distance {_escortRerouteDistance}; it must not be negative and must be finite");
            }
        }

        private void Attach(Vip vip, IReadOnlyList<VipExitPoint> exits)
        {
            Detach();
            _vip = vip;
            _exits.AddRange(exits);
            _vip.VipChosen += OnVipChosen;
            _vip.RoundStarted += OnRoundStarted;
            _vip.VipEscaped += OnVipEscaped;
            _vip.RoundEnded += OnRoundEnded;
            _vip.SidesSwapped += OnSidesSwapped;
            _vip.Ended += OnGameEnded;
        }

        // 止めたときに強めたままの体や入れ替えたままの出現点を残すと、同じシーンで次に始めたモードにそれが残る。
        private void Detach()
        {
            if (_vip != null)
            {
                _vip.VipChosen -= OnVipChosen;
                _vip.RoundStarted -= OnRoundStarted;
                _vip.VipEscaped -= OnVipEscaped;
                _vip.RoundEnded -= OnRoundEnded;
                _vip.SidesSwapped -= OnSidesSwapped;
                _vip.Ended -= OnGameEnded;
            }

            _vip = null;
            foreach (var perception in _perceptions)
            {
                if (perception.Agent != null)
                {
                    perception.Agent.RemovePerceptionContributor(perception);
                }
            }

            _perceptions.Clear();
            RestoreBodies(logged: false);
            foreach (var pair in _originalSpawnSides)
            {
                if (pair.Key != null)
                {
                    pair.Key.Side = pair.Value;
                }
            }

            _originalSpawnSides.Clear();
            RestoreRoutes();
            _homeRoutes.Clear();
            _exits.Clear();
            _escortRoutedAt = null;
            _escortRoutedVip = null;
            RemoveMarker();
        }

        private void OnVipChosen(int playerId)
        {
            var member = MemberOf(playerId);
            Debug.Log(VipLog.ChosenLog(_vip.RoundNumber, playerId), member != null ? member : (UnityEngine.Object)this);

            // ラウンドの途中で VIP が抜けて替わったときは、前の VIP の体をここで戻す。
            foreach (var other in new List<SideMember>(_originalMaxHealth.Keys))
            {
                if (other != member)
                {
                    Restore(other, logged: true);
                }
            }

            if (member != null)
            {
                Strengthen(member, playerId);
            }

            PlaceMarker(member);
            _escortRoutedAt = null;
            if (IsRoundLive)
            {
                RestoreRoutes();
                RouteVip();
            }

            RefreshHud();
        }

        // ラウンドの始めに全員を出現点に並べ直す。VIP の体力の上限は選んだとき (準備の始め) に上げてあるので、ここで新しい上限まで満ちる。
        private void OnRoundStarted(int round)
        {
            Debug.Log(VipLog.RoundLog(round, _vip.Attacker, _vip.Defender), this);
            PlaceForRound();
            RestoreRoutes();
            RouteVip();
            _escortRoutedAt = null;
            RouteEscorts();
        }

        private void OnVipEscaped(int playerId)
        {
            var member = MemberOf(playerId);
            Debug.Log(VipLog.EscapedLog(playerId), member != null ? member : (UnityEngine.Object)this);
        }

        private void OnRoundEnded(Side winner, VipRoundEnd reason)
        {
            Debug.Log(
                VipLog.RoundEndLog(_vip.RoundNumber, winner, _vip.Defender, reason, _vip.GetRoundWins(Side.Alpha), _vip.GetRoundWins(Side.Bravo)),
                this);
            RestoreBodies(logged: true);
            RemoveMarker();
            RestoreRoutes();
            _escortRoutedAt = null;
            _escortRoutedVip = null;
        }

        private void OnSidesSwapped()
        {
            Debug.Log(VipLog.SwapLog(_vip.Attacker, _vip.Defender), this);
            var swapped = 0;
            foreach (var point in SpawnPoints)
            {
                if (point == null)
                {
                    continue;
                }

                if (point.Side == Side.Alpha)
                {
                    point.Side = Side.Bravo;
                    swapped++;
                }
                else if (point.Side == Side.Bravo)
                {
                    point.Side = Side.Alpha;
                    swapped++;
                }
            }

            Debug.Log(VipLog.SpawnSwapLog(swapped, _vip.Attacker, _vip.Defender), this);
        }

        private void OnGameEnded(Side winner)
        {
            Debug.Log(VipLog.EndedLog(winner), this);
            EndMatch(winner);
        }

        // 立っている人は新しい上限まで満たす。上限だけ上げると、前のラウンドや撃ち合いで削れた体力のまま VIP になり、強めた意味が薄れる。
        // 倒れている人は満たせないので、ラウンドの始めの並べ直し (SideMember.Respawn) で新しい上限まで満ちる。
        private void Strengthen(SideMember member, int playerId)
        {
            var damageable = member.Damageable;
            if (damageable == null)
            {
                return;
            }

            var maxHealth = _vip.Spec.VipMaxHealth;
            if (!_originalMaxHealth.ContainsKey(member))
            {
                _originalMaxHealth[member] = damageable.MaxHealth;
            }

            damageable.MaxHealth = maxHealth;
            var session = Session;
            var standing = !damageable.IsDead && (session == null || !session.HasPlayer(playerId) || !session.IsDown(playerId));
            if (standing)
            {
                damageable.ResetHealth();
            }

            Debug.Log(VipLog.BodyLog(playerId, maxHealth), member);
        }

        private void RestoreBodies(bool logged)
        {
            foreach (var member in new List<SideMember>(_originalMaxHealth.Keys))
            {
                Restore(member, logged);
            }

            _originalMaxHealth.Clear();
        }

        // 立ったまま戻すときは元の上限まで削る。倒れている人は上限を戻すだけで、戻るときに元の体力で満ちる。
        private void Restore(SideMember member, bool logged)
        {
            if (ReferenceEquals(member, null) || !_originalMaxHealth.TryGetValue(member, out var original))
            {
                return;
            }

            _originalMaxHealth.Remove(member);
            if (member == null)
            {
                return;
            }

            var damageable = member.Damageable;
            if (damageable == null)
            {
                return;
            }

            damageable.MaxHealth = original;
            if (!damageable.IsDead)
            {
                damageable.RestoreHealth(Math.Min(damageable.Health.Current, original));
            }

            var id = IdOf(member);
            if (logged && id >= 0)
            {
                Debug.Log(VipLog.RestoredLog(id, original), member);
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
            var viewer = LocalPlayer != null ? LocalPlayer.Side : PlayerSide;
            _marker.ViewerTeam = viewer.TryGetTeam(out var viewerTeam) ? viewerTeam : (Team?)null;
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

        private void UpdatePresence()
        {
            var session = Session;
            var members = Members;
            for (var id = 0; id < members.Count; id++)
            {
                var member = members[id];
                if (session == null || !session.HasPlayer(id))
                {
                    continue;
                }

                var present = member != null && member.IsAlive && !session.IsDown(id) && IsAtExit(member.GroundPosition);
                _vip.SetAtExit(id, present);
            }
        }

        // 陣営ごとの出現点に、同じ陣営の人を順に 1 人ずつ割り当てる。SideSpawnSelector で選ぶと、同じラウンドの始めに何人も同じ点に重なる。
        private void PlaceForRound()
        {
            var bySide = new Dictionary<Side, List<SideSpawnPoint>>();
            foreach (var point in SpawnPoints)
            {
                if (point == null)
                {
                    continue;
                }

                if (!bySide.TryGetValue(point.Side, out var list))
                {
                    list = new List<SideSpawnPoint>();
                    bySide.Add(point.Side, list);
                }

                list.Add(point);
            }

            foreach (var list in bySide.Values)
            {
                list.Sort((a, b) => ComparePositions(a.Position, b.Position));
            }

            var used = new Dictionary<Side, int>();
            foreach (var member in Members)
            {
                if (member == null)
                {
                    continue;
                }

                // Awake で初期化に失敗した bot は動かせない (自分で止まっている)。シーンに置かれた場所に立たせたままにする。
                var agent = member.GetComponent<EnemyAgent>();
                if (agent != null && !agent.IsInitialized)
                {
                    continue;
                }

                // 自陣営の出現点が無ければ、土台の Respawn が警告を出してその場で体力を満たす。
                if (!bySide.TryGetValue(member.Side, out var points))
                {
                    Respawn(member);
                    continue;
                }

                used.TryGetValue(member.Side, out var count);
                used[member.Side] = count + 1;
                RespawnAt(member, points[count % points.Count]);
            }
        }

        private void RouteVip()
        {
            var vip = VipMember;
            var agent = vip != null ? vip.GetComponent<EnemyAgent>() : null;
            if (agent == null)
            {
                return;
            }

            var index = NearestExitIndex(vip.GroundPosition);
            if (index < 0)
            {
                return;
            }

            agent.SetPatrolRoute(new[] { _exits[index].Position });
            Debug.Log(VipLog.BotExitLog(IdOf(vip), index), agent);
        }

        // 動く VIP へ毎フレーム巡回路を引き直すと NavMesh の経路を探し直し続けるので、VIP が決めた距離だけ動いたら引き直す (PayloadRunner と同じ)。
        private void RouteEscorts()
        {
            if (!IsRoundLive)
            {
                return;
            }

            var vip = VipMember;
            var vipId = _vip.VipId;
            if (vip == null || !vipId.HasValue || !vip.IsAlive)
            {
                return;
            }

            var center = vip.GroundPosition;
            if (_escortRoutedVip == vipId
                && _escortRoutedAt.HasValue
                && Vector3.Distance(_escortRoutedAt.Value, center) < _escortRerouteDistance)
            {
                return;
            }

            var firstForThisVip = _escortRoutedVip != vipId;
            var slot = 0;
            foreach (var member in Members)
            {
                if (member == null || member == vip || !IsDefender(member))
                {
                    continue;
                }

                var agent = member.GetComponent<EnemyAgent>();
                if (agent == null)
                {
                    continue;
                }

                agent.SetPatrolRoute(HardpointRunner.RouteAround(center, _escortRadius, slot));
                slot++;
                if (firstForThisVip)
                {
                    Debug.Log(VipLog.BotEscortLog(IdOf(member), vipId.Value), agent);
                }
            }

            _escortRoutedAt = center;
            _escortRoutedVip = vipId;
        }

        private void RestoreRoutes()
        {
            foreach (var pair in _homeRoutes)
            {
                if (pair.Key != null)
                {
                    pair.Key.SetPatrolRoute(pair.Value);
                }
            }
        }

        private Side? SideOf(SideMember member)
        {
            var id = IdOf(member);
            if (id < 0 || _vip == null || !_vip.HasPlayer(id))
            {
                return null;
            }

            return _vip.Mode.GetSide(id);
        }

        private static int ComparePositions(Vector3 a, Vector3 b)
        {
            var byX = a.x.CompareTo(b.x);
            if (byX != 0)
            {
                return byX;
            }

            var byZ = a.z.CompareTo(b.z);
            return byZ != 0 ? byZ : a.y.CompareTo(b.y);
        }

        private static Vector3[] CopyRoute(IReadOnlyList<Vector3> route)
        {
            var copy = new Vector3[route != null ? route.Count : 0];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = route[i];
            }

            return copy;
        }
    }
}

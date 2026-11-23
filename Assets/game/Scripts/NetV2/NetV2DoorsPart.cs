using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Shooter.NetV2
{
    // ドアの状態はサーバー (ServerDoors) が持つので、シーンの BreachDoor の状態機械は動かさず (Kick / BeginCharge を呼ばず)、
    // 届いた DoorStateMessage の段に合わせて板を開ける・壊す見た目と音を出し、板の当たりを外す。
    // 押す・長押しはオフラインの BreachController と同じ読み方で BreachRequestMessage にして送り、案内 (BreachHint) と仕掛けの進み具合 (GameSignals.Breach) もオフラインと同じに出す。
    // ドアの番号は書き出し (MapCollisionExporter.CollectDoors) と同じく、有効で動いているドアだけをシーンの並び順で数える。
    public sealed class NetV2DoorsPart : INetV2ClientPart
    {
        public const string InteractActionPath = InputActionPaths.Interact;

        // 正面のドアを見つける線の長さ。届くかは BreachSpec.Reach で決める (オフラインと同じ)
        public const float ProbeDistance = BreachController.ProbeDistance;

        // 仕掛ける要求を送ってから、そのドアが仕掛け中になったのを自分のものと見なす秒数。往復の遅れより長く、
        // ほかの人が後から同じドアに仕掛けたのを取り違えないくらいに短くする。回線の遅れに合わせる値で、遊びの手触りではないので調整値に出さない
        internal const float PendingTimeoutSeconds = 1f;

        private const float _settleSeconds = 3600f;

        private readonly Func<IMessage, bool> _send;
        private readonly Func<bool> _canAct;
        private readonly Func<ClientWorld> _world;
        private readonly Func<IReadOnlyList<BreachDoor>> _sceneDoors;
        private readonly Func<Ray?> _aim;
        private readonly Func<Transform> _shooterRoot;
        private readonly Func<bool> _interactDown;
        private readonly Func<string, int> _chargeCount;
        private readonly BreachSpec _spec;
        private readonly Action<string> _log;
        private readonly DoorStateMessage _state = new DoorStateMessage();
        private readonly BreachRequestMessage _request = new BreachRequestMessage();
        private readonly List<BreachDoor> _doors = new List<BreachDoor>();
        private readonly List<BreachDoorPhase> _shown = new List<BreachDoorPhase>();
        private readonly HashSet<int> _missingLogged = new HashSet<int>();
        private readonly InteractHintSlot _hint;
        private readonly SquadOrderButton _button;
        private ClientWorld _boundWorld;
        private bool _hasState;
        private int _target = -1;
        private float _clock;
        private bool _wasDown;
        private bool _ownsPress;
        private int _pendingCharge = -1;
        private float _pendingLeft;
        private int _ownCharge = -1;
        private float _ownElapsed;
        private BreachStatus? _published;
        private InputAction _interactAction;
        private bool _resolvedAction;
        private bool _disposed;

        // world は今のマップの当たりで、マップに入る前と出た後は null を返す。spec が null ならオフラインと同じ調整値 (KitTuning.Breach) を使う。
        // sceneDoors・aim・interactDown は、テストや別の入力から差し替えられるよう受け取り、null ならシーンのドア・Player/Interact を読む。
        // chargeCount が null なら品の数を知らないものとして仕掛ける要求を送り、確かめはサーバーに任せる
        public NetV2DoorsPart(
            Func<IMessage, bool> send,
            Func<bool> canAct,
            Func<ClientWorld> world,
            BreachSpec spec = null,
            Func<Ray?> aim = null,
            Func<Transform> shooterRoot = null,
            Func<bool> interactDown = null,
            Func<string, int> chargeCount = null,
            Func<IReadOnlyList<BreachDoor>> sceneDoors = null,
            Action<string> log = null)
        {
            _send = send ?? throw new ArgumentNullException(nameof(send));
            _canAct = canAct ?? throw new ArgumentNullException(nameof(canAct));
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _spec = spec ?? KitTuning.Breach;
            _aim = aim ?? (() => null);
            _shooterRoot = shooterRoot ?? (() => null);
            _interactDown = interactDown ?? ReadInteractDown;
            _chargeCount = chargeCount;
            _sceneDoors = sceneDoors ?? ActiveSceneDoors;
            _log = log ?? (line => Debug.Log(line));
            _hint = new InteractHintSlot(this);
            _button = new SquadOrderButton(_spec.HoldSeconds);
        }

        public BreachSpec Spec => _spec;

        public int SceneDoorCount => _doors.Count;

        public bool IsInMap => _boundWorld != null;

        // 正面にある、蹴れる・仕掛けられるドアの番号。無ければ -1
        public int Target => _target;

        // 自分が仕掛けているか仕掛け終えたドアの番号。無ければ -1
        public int OwnCharge => _ownCharge;

        // NetV2ClientParts.Registered に並べる口。再生では要求を受ける相手がいないので、見せるだけにする
        public static INetV2ClientPart Create(NetV2ClientPartsContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var client = context.Client;
            var localPlayer = context.LocalPlayer;
            Func<ClientWorld> world = context.World ?? (() => null);
            return new NetV2DoorsPart(
                context.Send,
                () => !context.Replay && NetV2ClientParts.CanAct(client),
                world,
                aim: localPlayer != null ? () => AimOf(localPlayer) : (Func<Ray?>)null,
                shooterRoot: localPlayer != null ? () => RootOf(localPlayer) : (Func<Transform>)null,
                interactDown: context.Replay ? () => false : (Func<bool>)null,
                log: context.Log);
        }

        // 番号はこのマップでの番号。シーンに無いドアは届いた状態の段を返し、何も届いていなければ閉じているとみなす
        public BreachDoorPhase PhaseOf(int door)
        {
            if (door >= 0 && door < _shown.Count)
            {
                return _shown[door];
            }

            return _hasState && door >= 0 && door < _state.DoorCount ? _state[door].Phase : BreachDoorPhase.Closed;
        }

        public bool Handle(IMessage message)
        {
            if (!(message is DoorStateMessage doors))
            {
                return false;
            }

            if (_disposed)
            {
                return true;
            }

            Follow();
            _state.Clear();
            for (var i = 0; i < doors.DoorCount; i++)
            {
                var door = doors[i];
                _state.Add(door.Phase, door.FarSideFront);
            }

            _hasState = true;
            _log(ClientDoorsLog.Received(_state.DoorCount, _boundWorld != null));
            if (_boundWorld != null)
            {
                Show(true);
            }

            return true;
        }

        public void Apply(SnapshotMessage snapshot)
        {
        }

        public void Tick(float deltaSeconds)
        {
            if (_disposed)
            {
                return;
            }

            var seconds = float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) ? 0f : Mathf.Max(0f, deltaSeconds);
            _clock += seconds;
            Follow();
            if (_pendingLeft > 0f)
            {
                _pendingLeft = Mathf.Max(0f, _pendingLeft - seconds);
                if (_pendingLeft <= 0f)
                {
                    _pendingCharge = -1;
                }
            }

            if (_ownCharge >= 0)
            {
                _ownElapsed += seconds;
            }

            // ポーズ画面のボタンを押したつもりで蹴らないよう、開いている間は押下そのものを捨てる (オフラインの BreachController と同じ)
            var paused = GameUi.IsPauseOpen;
            if (paused)
            {
                DiscardPress();
            }

            var canAct = _boundWorld != null && _canAct();
            var planting = _ownCharge >= 0 && PhaseOf(_ownCharge) == BreachDoorPhase.Planting;
            _target = canAct && !planting ? Probe() : -1;
            ShowHint();
            ReadGesture(!paused && canAct && _interactDown());
            PublishStatus();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Leave();
            _boundWorld = null;
            _state.Clear();
            _hasState = false;
        }

        // マップの当たりが替わったら (入った・出た・次のマップ)、シーンのドアを拾い直す。
        // 前のマップの状態を残すと、番号が同じ別のマップのドアに当たってしまうので、マップを出たら捨てる。
        // 入る前に届いた状態 (途中参加) は残し、入ったときにまとめて写す
        private void Follow()
        {
            var world = _world();
            if (ReferenceEquals(world, _boundWorld))
            {
                return;
            }

            if (_boundWorld != null)
            {
                Leave();
                _state.Clear();
                _hasState = false;
            }

            _boundWorld = world;
            if (world != null)
            {
                Enter(world);
            }
        }

        private void Enter(ClientWorld world)
        {
            _doors.Clear();
            _shown.Clear();
            _missingLogged.Clear();
            var found = _sceneDoors() ?? Array.Empty<BreachDoor>();
            foreach (var door in found)
            {
                if (door == null)
                {
                    continue;
                }

                if (!door.IsBound)
                {
                    door.Bind();
                }

                _doors.Add(door);
                _shown.Add(BreachDoorPhase.Closed);
            }

            _log(ClientDoorsLog.Entered(SceneManager.GetActiveScene().name, _doors.Count, world.DoorCount, _hasState ? _state.DoorCount : 0));
            if (_hasState)
            {
                Show(false);
            }
        }

        private void Leave()
        {
            _hint.Clear();
            Unpublish();
            _doors.Clear();
            _shown.Clear();
            _missingLogged.Clear();
            _target = -1;
            _pendingCharge = -1;
            _pendingLeft = 0f;
            _ownCharge = -1;
            _ownElapsed = 0f;
            DiscardPress();
        }

        // live は届いたその場で変わった (音と動きを出す) か、入ったときにまとめて写す (結果の見た目だけを出す) か
        private void Show(bool live)
        {
            if (!_boundWorld.Apply(_state, out var changed))
            {
                _log(ClientDoorsLog.CountMismatch(_state.DoorCount, _boundWorld.DoorCount));
            }

            if (changed > 0)
            {
                _log(ClientDoorsLog.CollisionChanged(changed, _boundWorld.OpenDoorCount));
            }

            for (var i = 0; i < _state.DoorCount; i++)
            {
                ShowDoor(i, _state[i], live);
            }
        }

        private void ShowDoor(int index, DoorStateMessage.Door state, bool live)
        {
            if (index >= _doors.Count || _doors[index] == null)
            {
                if (state.Phase != BreachDoorPhase.Closed && _missingLogged.Add(index))
                {
                    _log(ClientDoorsLog.NoSceneDoor(index, _doors.Count));
                }

                return;
            }

            var before = _shown[index];
            var after = state.Phase;
            if (before == after || IsBreached(before))
            {
                // 板を外した・壊した見た目を戻す手立てが無いので、破れたドアは閉じ直さない (BreachDoorState と同じ)
                return;
            }

            _shown[index] = after;
            _log(ClientDoorsLog.Changed(index, before, after, live));
            FollowOwnCharge(index, before, after);

            var door = _doors[index];
            var visuals = door.Visuals;
            var farSign = state.FarSideFront ? 1f : -1f;
            switch (after)
            {
                case BreachDoorPhase.Closed:
                    visuals.HideCharge();
                    break;
                case BreachDoorPhase.Planting:
                case BreachDoorPhase.Armed:
                    if (visuals.Charge == null)
                    {
                        visuals.ShowCharge(-farSign);
                    }

                    break;
                case BreachDoorPhase.Opened:
                    ClearPanel(visuals);
                    visuals.HideCharge();
                    visuals.SwingOpen(farSign);
                    if (live)
                    {
                        visuals.PlayKick(visuals.PanelCenter(), _spec.KickSoundRadius);
                        CombatEvents.RaiseDoorBreached(door.gameObject, BreachMethod.Kick, null);
                    }
                    else
                    {
                        // 途中から入った人には、回し終えた板を見せる。回す秒数は見た目の部品ごとに違うので、どれよりも長い秒数を進める
                        visuals.Advance(_settleSeconds);
                    }

                    break;
                case BreachDoorPhase.Destroyed:
                    ClearPanel(visuals);
                    if (live)
                    {
                        var center = visuals.ChargePoint(-farSign);
                        visuals.Shatter(farSign);

                        // 爆発に半径があればサーバーが爆発 (ExplosionMessage) も配り、NetV2Client がその場で爆発の見た目と音を出すので、二重に鳴らさない
                        if (!(_spec.ChargeRadius > 0f))
                        {
                            visuals.PlayExplosion(center);
                        }

                        CombatEvents.RaiseDoorBreached(door.gameObject, BreachMethod.Charge, null);
                    }
                    else
                    {
                        // 破片はもう散って消えたころなので、途中から入った人には板が無いことだけを見せる
                        visuals.HideCharge();
                        if (visuals.Panel != null)
                        {
                            visuals.Panel.gameObject.SetActive(false);
                        }
                    }

                    break;
            }
        }

        // 仕掛ける要求を送ったドアが、待つ間に閉じた状態から仕掛け中 (秒数 0 なら仕掛け終え) に変わったら自分のものとし、進み具合を出す
        private void FollowOwnCharge(int index, BreachDoorPhase before, BreachDoorPhase after)
        {
            if (index == _pendingCharge && _pendingLeft > 0f && before == BreachDoorPhase.Closed
                && (after == BreachDoorPhase.Planting || after == BreachDoorPhase.Armed))
            {
                _ownCharge = index;
                _ownElapsed = 0f;
                _pendingCharge = -1;
                _pendingLeft = 0f;
                return;
            }

            if (index != _ownCharge)
            {
                return;
            }

            if (after == BreachDoorPhase.Armed)
            {
                _ownElapsed = 0f;
            }
            else if (after != BreachDoorPhase.Planting)
            {
                _ownCharge = -1;
                _ownElapsed = 0f;
            }
        }

        private int Probe()
        {
            var aim = _aim();
            if (!aim.HasValue)
            {
                return -1;
            }

            // 足元のドアを見下ろしていても正面のドアに届くよう、狙っている向きを水平にして使う (オフラインの BreachController と同じ)
            var forward = aim.Value.direction;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f)
            {
                return -1;
            }

            if (!Hitscan.TryFirstHit(aim.Value.origin, forward.normalized, ProbeDistance, _shooterRoot(), out var hit) || hit.distance > _spec.Reach)
            {
                return -1;
            }

            var door = hit.collider.GetComponentInParent<BreachDoor>();
            var index = door != null ? _doors.IndexOf(door) : -1;
            return index >= 0 && _shown[index] == BreachDoorPhase.Closed ? index : -1;
        }

        private void ShowHint()
        {
            if (_target < 0)
            {
                _hint.Clear();
                return;
            }

            _hint.Show(new BreachHint(true, CanPlant(), _spec.ChargeItemId, InteractActionPath));
        }

        private bool CanPlant()
        {
            return !_spec.NeedsChargeItem || _chargeCount == null || _chargeCount(_spec.ChargeItemId) > 0;
        }

        private void ReadGesture(bool down)
        {
            var wasDown = _wasDown;
            _wasDown = down;

            // 押している途中でドアから外れたら、離したときに蹴らないよう、その押下は離すまで使わない
            if (_target < 0)
            {
                DropPress();
                return;
            }

            // 押し始めたときに出ていた案内が自分のものでなければ、その押下は案内を出している物 (拾う物など) のものにする
            if (down && !wasDown)
            {
                _ownsPress = HintIsMine();
            }

            if (!_ownsPress)
            {
                return;
            }

            var gesture = _button.Tick(down, _clock);
            if (!down)
            {
                _ownsPress = false;
            }

            switch (gesture)
            {
                case SquadOrderGesture.Tap:
                    Request(_target, BreachMethod.Kick);
                    break;
                case SquadOrderGesture.Hold:
                    DropPress();
                    if (!CanPlant())
                    {
                        _log(BreachLog.Refused(_doors[_target].Id, null, BreachMethod.Charge, BreachDoorPhase.Closed, BreachController.RefusedNoCharge));
                        break;
                    }

                    if (Request(_target, BreachMethod.Charge))
                    {
                        _pendingCharge = _target;
                        _pendingLeft = PendingTimeoutSeconds;
                    }

                    break;
            }
        }

        private bool Request(int door, BreachMethod method)
        {
            _request.DoorIndex = door;
            _request.Method = method;
            _request.Cancel = false;
            var sent = _send(_request);
            _log(ClientDoorsLog.Requested(door, method, sent));
            return sent;
        }

        private bool HintIsMine()
        {
            var shown = GameSignals.InteractHint.Value;
            return shown.HasValue && ReferenceEquals(shown.Value.Source, this);
        }

        private void DropPress()
        {
            _ownsPress = false;
            _button.Reset();
        }

        private void DiscardPress()
        {
            DropPress();
            _wasDown = false;
        }

        // サーバーは仕掛け中の秒数を送らないので、仕掛け中・仕掛け終えたと届いてからの秒数を手元で数え、調整値の秒数で割合にする
        private void PublishStatus()
        {
            if (_ownCharge < 0)
            {
                Unpublish();
                return;
            }

            var phase = PhaseOf(_ownCharge);
            var total = phase == BreachDoorPhase.Planting ? _spec.PlantSeconds : _spec.FuseSeconds;
            var progress = total > 0f ? Mathf.Clamp01(_ownElapsed / total) : 1f;
            var status = new BreachStatus(phase, progress, Mathf.Max(0f, total - _ownElapsed));
            if (_published == status && GameSignals.Breach.HasValue)
            {
                return;
            }

            _published = status;
            GameSignals.Breach.Publish(status);
        }

        // 後から別の物が出した進み具合までは消さない
        private void Unpublish()
        {
            if (_published.HasValue)
            {
                GameSignals.Breach.ClearIfCurrent(_published);
                _published = null;
            }
        }

        private static void ClearPanel(BreachDoorVisuals visuals)
        {
            foreach (var collider in visuals.PanelColliders())
            {
                if (collider != null)
                {
                    collider.enabled = false;
                }
            }
        }

        private static bool IsBreached(BreachDoorPhase phase)
        {
            return phase == BreachDoorPhase.Opened || phase == BreachDoorPhase.Destroyed;
        }

        // 書き出し (MapCollisionExporter.ExportedDoors) と同じく、有効で動いているドアだけをシーンの並び順で数える。番号がずれると、別のドアの板を開けてしまう
        public static List<BreachDoor> DoorsIn(IReadOnlyList<GameObject> roots)
        {
            if (roots == null)
            {
                throw new ArgumentNullException(nameof(roots));
            }

            var doors = new List<BreachDoor>();
            foreach (var root in roots)
            {
                if (root == null)
                {
                    continue;
                }

                foreach (var door in root.GetComponentsInChildren<BreachDoor>(true))
                {
                    if (door.enabled && door.gameObject.activeInHierarchy)
                    {
                        doors.Add(door);
                    }
                }
            }

            return doors;
        }

        private static IReadOnlyList<BreachDoor> ActiveSceneDoors()
        {
            var scene = SceneManager.GetActiveScene();
            return scene.IsValid() ? DoorsIn(scene.GetRootGameObjects()) : (IReadOnlyList<BreachDoor>)Array.Empty<BreachDoor>();
        }

        // 破棄済みの Unity の参照も無いものとして扱うため、?. ではなく != null で確かめる
        private static Ray? AimOf(Func<NetV2LocalPlayer> localPlayer)
        {
            var local = localPlayer();
            if (local == null || local.Camera == null)
            {
                return null;
            }

            return new Ray(local.EyePosition, local.AimDirection);
        }

        private static Transform RootOf(Func<NetV2LocalPlayer> localPlayer)
        {
            var local = localPlayer();
            return local != null ? local.transform : null;
        }

        private bool ReadInteractDown()
        {
            if (!_resolvedAction)
            {
                _resolvedAction = true;
                AppliedInputBindings.EnsureLoaded();
                var actions = InputSystem.actions;
                _interactAction = actions != null ? actions.FindAction(InteractActionPath) : null;
                if (_interactAction == null)
                {
                    _log(ClientDoorsLog.NoInput(InteractActionPath));
                }
            }

            return _interactAction != null && _interactAction.enabled && KeySharing.IsPressed(_interactAction);
        }
    }
}

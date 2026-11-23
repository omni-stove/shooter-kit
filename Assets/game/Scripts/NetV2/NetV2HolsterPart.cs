using System;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.NetV2
{
    // しまう・構えるの状態と秒数はサーバー (ServerHolster) が持つので、ここは Player/Holster と射撃のボタンを向きのある要求にして送り、
    // スナップショットの自分の行の状態を GameSignals.Holster に出すだけにする。
    // 押すたびに向きを決めるのは、オフラインの HolsterController と同じく「もう一度押すと構え直す」「しまっている間の射撃は構えるだけ」にするため。
    public sealed class NetV2HolsterPart : INetV2ClientPart
    {
        public const string HolsterActionPath = InputActionPaths.Holster;

        public const string FireActionPath = InputActionPaths.Attack;

        // 送った向きがスナップショットに出るまで待つ秒数。往復の遅れより長く、断られた要求で次の押しの向きが逆になり続けないくらいに短くする。
        // 調整値に出さないのは、遊びの手触りではなく回線の遅れに合わせる値で、ゲームごとに変える理由が無いため
        internal const float PendingTimeoutSeconds = 1f;

        private readonly MatchClient _client;
        private readonly Func<IMessage, bool> _send;
        private readonly HolsterSpec _spec;
        private readonly Func<bool> _holsterPressed;
        private readonly Func<bool> _firePressed;
        private readonly Action<string> _log;
        private readonly HolsterRequestMessage _request = new HolsterRequestMessage();
        private InputAction _holsterAction;
        private InputAction _fireAction;
        private bool _resolvedActions;
        private HolsterStatus? _published;
        private WeaponFreeZoneStatus? _publishedZone;
        private bool _canAct;
        private bool _riding;
        private bool _switching;
        private bool _pendingHolstered;
        private float _pendingLeft;
        private bool _disposed;

        // spec が null ならオフラインと同じ調整値 (KitTuning.Holster) で、どの状態を武装と数えるかを決める。
        // holsterPressed・firePressed はこのフレームに押したかで、null なら Player/Holster・Player/Attack を読む
        public NetV2HolsterPart(
            MatchClient client,
            Func<IMessage, bool> send,
            HolsterSpec spec = null,
            Func<bool> holsterPressed = null,
            Func<bool> firePressed = null,
            Action<string> log = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _send = send ?? throw new ArgumentNullException(nameof(send));
            _spec = spec ?? KitTuning.Holster;
            _holsterPressed = holsterPressed ?? ReadHolsterPressed;
            _firePressed = firePressed ?? ReadFirePressed;
            _log = log ?? (line => Debug.Log(line));
        }

        public HolsterPhase Phase { get; private set; } = HolsterPhase.Drawn;

        public float Lowering { get; private set; }

        public bool IsPending => _pendingLeft > 0f;

        public bool CanAct => !_disposed && _canAct;

        // スナップショットの自分の部分に載った、武器を出してはいけない場所の状態
        public WeaponFreeZoneStatus Zone { get; private set; }

        // NetV2ClientParts.Registered に並べる口。再生では要求を受ける相手がいないので押しを読まず、記録された自分の状態を出すだけにする
        public static INetV2ClientPart Create(NetV2ClientPartsContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            Func<bool> never = () => false;
            return new NetV2HolsterPart(
                context.Client,
                context.Send,
                holsterPressed: context.Replay ? never : null,
                firePressed: context.Replay ? never : null,
                log: context.Log);
        }

        public bool Handle(IMessage message)
        {
            return false;
        }

        // 送れるか (試合中・生きていてダウンしていない) は、サーバーが同じ値で判定するスナップショットの値で決める
        public void Apply(SnapshotMessage snapshot)
        {
            if (_disposed || snapshot == null)
            {
                return;
            }

            var own = snapshot.Players.IndexOf(_client.PlayerId);
            var phase = HolsterPhase.Drawn;
            var lowering = 0f;
            var canAct = false;
            _riding = false;
            if (own >= 0)
            {
                var row = snapshot.Players[own];
                phase = row.HolsterPhase;
                lowering = row.HolsterLowering;
                canAct = snapshot.Match.State == GameModeState.Playing && row.Health > 0 && !row.Downed.IsDowned;
                _riding = row.Vehicle.IsRiding;
            }

            _switching = snapshot.HasOwner && snapshot.Owner.Weapon.IsSwitching;
            _canAct = canAct;
            if (phase != Phase)
            {
                _log(HolsterLog.NetChanged(Phase, phase));

                // サーバーで状態が動いたら、送った向きを受けたか、倒れた・持ち替えたで戻したかのどちらかなので、待つのをやめてサーバーの値に従う
                _pendingLeft = 0f;
            }

            Phase = phase;
            Lowering = lowering;
            Publish(own >= 0 ? new HolsterStatus(phase, lowering, _spec.IsArmed(phase, false)) : (HolsterStatus?)null);

            // オフラインの WeaponFreeZone と同じく、どこかのゾーンの中にいる間だけ値を出す。判定はサーバー (ServerWeaponFreeZones) が持つ
            var zone = own >= 0 && snapshot.HasOwner ? snapshot.Owner.WeaponFreeZone : WeaponFreeZoneStatus.Outside;
            Zone = zone;
            PublishZone(zone.Inside ? zone : (WeaponFreeZoneStatus?)null);
        }

        public void Tick(float deltaSeconds)
        {
            if (_disposed)
            {
                return;
            }

            if (_pendingLeft > 0f)
            {
                var seconds = float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) ? 0f : Mathf.Max(0f, deltaSeconds);
                _pendingLeft = Mathf.Max(0f, _pendingLeft - seconds);
            }

            if (!_canAct || GameUi.IsPauseOpen)
            {
                return;
            }

            if (_holsterPressed())
            {
                Request(IsHeadingToHolstered() ? HolsterAction.Draw : HolsterAction.Holster);
            }
            else if (IsHeadingToHolstered() && _firePressed())
            {
                // オフラインと同じく、しまっている間の射撃は構えるだけで撃たない (撃つ要求はサーバーが Holstered で断る)
                Request(HolsterAction.Draw);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Publish(null);
            PublishZone(null);
            _pendingLeft = 0f;
        }

        // 送った向きがまだスナップショットに出ていない間は、その向きへ進んでいることにする。続けて押したときに同じ向きを送り直さないため
        private bool IsHeadingToHolstered()
        {
            if (_pendingLeft > 0f)
            {
                return _pendingHolstered;
            }

            return Phase == HolsterPhase.Holstering || Phase == HolsterPhase.Holstered;
        }

        // サーバーも同じ理由で断るが、断られると分かっている要求を送らず、理由を手元のログに残す
        private void Request(HolsterAction action)
        {
            var toward = action == HolsterAction.Holster ? HolsterPhase.Holstering : HolsterPhase.Drawing;
            if (action == HolsterAction.Holster && _riding)
            {
                _log(HolsterLog.NetNotSent(toward, "Riding"));
                return;
            }

            if (action == HolsterAction.Holster && _switching)
            {
                _log(HolsterLog.NetNotSent(toward, "Switching"));
                return;
            }

            if (action == HolsterAction.Draw && Zone.Inside && Zone.Reaction == WeaponFreeZoneReaction.BlockDraw)
            {
                _log(HolsterLog.NetNotSent(toward, "WeaponFreeZone"));
                return;
            }

            _request.Action = action;
            if (!_send(_request))
            {
                _log(HolsterLog.NetNotSent(toward, "NotConnected"));
                return;
            }

            _pendingHolstered = action == HolsterAction.Holster;
            _pendingLeft = PendingTimeoutSeconds;
            _log(HolsterLog.NetRequested(toward));
        }

        private void Publish(HolsterStatus? status)
        {
            if (_published == status)
            {
                return;
            }

            if (status.HasValue)
            {
                GameSignals.Holster.Publish(status);
            }
            else
            {
                GameSignals.Holster.ClearIfCurrent(_published);
            }

            _published = status;
        }

        private void PublishZone(WeaponFreeZoneStatus? status)
        {
            if (_publishedZone == status)
            {
                return;
            }

            if (status.HasValue)
            {
                GameSignals.WeaponFreeZone.Publish(status);
            }
            else
            {
                GameSignals.WeaponFreeZone.ClearIfCurrent(_publishedZone);
            }

            _publishedZone = status;
        }

        private void ResolveActions()
        {
            if (_resolvedActions)
            {
                return;
            }

            _resolvedActions = true;
            AppliedInputBindings.EnsureLoaded();
            var actions = InputSystem.actions;
            _holsterAction = actions != null ? actions.FindAction(HolsterActionPath) : null;
            _fireAction = actions != null ? actions.FindAction(FireActionPath) : null;
            if (_holsterAction == null)
            {
                _log(HolsterLog.NoInput(HolsterActionPath));
            }

            if (_fireAction == null)
            {
                _log(HolsterLog.NoInput(FireActionPath));
            }
        }

        private bool ReadHolsterPressed()
        {
            ResolveActions();
            return _holsterAction != null && _holsterAction.enabled && _holsterAction.WasPressedThisFrame();
        }

        private bool ReadFirePressed()
        {
            ResolveActions();
            return _fireAction != null && _fireAction.enabled && _fireAction.WasPressedThisFrame();
        }
    }
}

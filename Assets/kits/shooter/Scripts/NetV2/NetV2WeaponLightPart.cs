using System;
using System.Collections.Generic;
using GameKit.Net;
using GameKit.Runtime;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.NetV2
{
    // 点灯はサーバー (ServerWeaponLight) が持つので、ここは Player/WeaponLight の押しを点けたい向きの要求にして送り、
    // スナップショットの自分の行の点灯を GameSignals.WeaponLight に出すだけにする。相手の光の見た目は NetV2RemotePlayer がスナップショットの値で出す
    public sealed class NetV2WeaponLightPart : INetV2ClientPart
    {
        public const string LightActionPath = InputActionPaths.WeaponLight;

        public const string NotConnectedReason = "not-connected";

        // 送った向きがスナップショットに出るまで待つ秒数。NetV2HolsterPart と同じ理由で、往復の遅れより長く、断られた要求で次の押しが逆になり続けないくらいに短くする
        internal const float PendingTimeoutSeconds = 1f;

        private readonly MatchClient _client;
        private readonly Func<IMessage, bool> _send;
        private readonly Func<string, IReadOnlyList<string>> _attachmentsOf;
        private readonly ItemCatalog _items;
        private readonly HolsterSpec _holster;
        private readonly Func<bool> _lightPressed;
        private readonly Action<string> _log;
        private readonly WeaponLightRequestMessage _request = new WeaponLightRequestMessage();
        private InputAction _lightAction;
        private bool _resolvedAction;
        private WeaponLightStatus? _published;
        private string _weaponId;
        private bool _hasLight;
        private bool _armed;
        private bool _canAct;
        private bool _pendingOn;
        private float _pendingLeft;
        private bool _disposed;

        // attachmentsOf は自分の武器に付けた物の Id で、null なら何も付けていないとみなす。items が null ならオフラインと同じ調整値 (KitTuning.Items)、
        // holster が null なら KitTuning.Holster で、どの状態を武装と数えるかを決める。lightPressed はこのフレームに押したかで、null なら Player/WeaponLight を読む
        public NetV2WeaponLightPart(
            MatchClient client,
            Func<IMessage, bool> send,
            Func<string, IReadOnlyList<string>> attachmentsOf = null,
            ItemCatalog items = null,
            HolsterSpec holster = null,
            Func<bool> lightPressed = null,
            Action<string> log = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _send = send ?? throw new ArgumentNullException(nameof(send));
            _attachmentsOf = attachmentsOf;
            _items = items ?? KitTuning.Items;
            _holster = holster ?? KitTuning.Holster;
            _lightPressed = lightPressed ?? ReadLightPressed;
            _log = log ?? (line => Debug.Log(line));
        }

        public bool IsOn { get; private set; }

        public bool HasLight => _hasLight;

        public bool IsPending => _pendingLeft > 0f;

        public bool CanAct => !_disposed && _canAct;

        // NetV2ClientParts.Registered に並べる口。再生では要求を受ける相手がいないので押しを読まず、記録された自分の点灯を出すだけにする
        public static INetV2ClientPart Create(NetV2ClientPartsContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            Func<bool> never = () => false;
            return new NetV2WeaponLightPart(
                context.Client,
                context.Send,
                context.OwnAttachments,
                lightPressed: context.Replay ? never : null,
                log: context.Log);
        }

        public bool Handle(IMessage message)
        {
            return false;
        }

        public void Apply(SnapshotMessage snapshot)
        {
            if (_disposed || snapshot == null)
            {
                return;
            }

            var own = snapshot.Players.IndexOf(_client.PlayerId);
            var on = false;
            string weaponId = null;
            var canAct = false;
            var armed = false;
            if (own >= 0)
            {
                var row = snapshot.Players[own];
                on = row.WeaponLightOn;
                weaponId = string.IsNullOrEmpty(row.WeaponId) ? null : row.WeaponId;
                canAct = snapshot.Match.State == GameModeState.Playing && row.Health > 0 && !row.Downed.IsDowned;
                // サーバー (ServerHolster.IsArmed) と同じく、はしごや乗り物が手を預かっている間は holster.json の armedWhileHeldByOthers で数える
                armed = _holster.IsArmed(row.HolsterPhase, row.Vehicle.IsRiding || row.Ladder.IsClimbing);
            }

            _weaponId = weaponId;
            _hasLight = weaponId != null && WeaponAttachments.LightOf(_items, weaponId, _attachmentsOf?.Invoke(weaponId)) != null;
            _armed = armed;
            _canAct = canAct;
            if (on != IsOn)
            {
                _log(WeaponLightLog.NetChanged(IsOn, on));

                // サーバーで点灯が変わったら、送った向きを受けたか、持ち替え・しまう・倒れるで消したかのどちらかなので、待つのをやめてサーバーの値に従う
                _pendingLeft = 0f;
            }

            IsOn = on;

            // 付けた物の知らせがまだ届いていないと手元はライトが無いと数えるが、サーバーが点けているなら付いている
            Publish(own >= 0 ? new WeaponLightStatus(_hasLight || on, on) : (WeaponLightStatus?)null);
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

            if (_lightPressed())
            {
                Request(!IsHeadingOn());
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
            _pendingLeft = 0f;
        }

        // 送った向きがまだスナップショットに出ていない間は、その向きになったことにする。続けて押したときに同じ向きを送り直さないため
        private bool IsHeadingOn()
        {
            return _pendingLeft > 0f ? _pendingOn : IsOn;
        }

        // サーバーも同じ理由で断るが、断られると分かっている点ける要求は送らず、理由を手元のログに残す。消す要求はいつでも送る
        private void Request(bool on)
        {
            if (on)
            {
                var reason = NotLitReason();
                if (reason != null)
                {
                    _log(WeaponLightLog.NetNotSent(true, reason));
                    return;
                }
            }

            _request.On = on;
            if (!_send(_request))
            {
                _log(WeaponLightLog.NetNotSent(on, NotConnectedReason));
                return;
            }

            _pendingOn = on;
            _pendingLeft = PendingTimeoutSeconds;
            _log(WeaponLightLog.NetRequested(on));
        }

        // 理由の言葉はオフラインの WeaponLightController と同じにして、同じ grep で拾えるようにする
        private string NotLitReason()
        {
            if (_weaponId == null)
            {
                return WeaponLightController.UnarmedReason;
            }

            if (!_armed)
            {
                return WeaponLightController.HolsteredReason;
            }

            return _hasLight ? null : WeaponLightController.NoLightReason;
        }

        private void Publish(WeaponLightStatus? status)
        {
            if (_published == status)
            {
                return;
            }

            if (status.HasValue)
            {
                GameSignals.WeaponLight.Publish(status);
            }
            else
            {
                GameSignals.WeaponLight.ClearIfCurrent(_published);
            }

            _published = status;
        }

        private bool ReadLightPressed()
        {
            if (!_resolvedAction)
            {
                _resolvedAction = true;
                AppliedInputBindings.EnsureLoaded();
                var actions = InputSystem.actions;
                _lightAction = actions != null ? actions.FindAction(LightActionPath) : null;
                if (_lightAction == null)
                {
                    _log(WeaponLightLog.NoInput(LightActionPath));
                }
            }

            return _lightAction != null && _lightAction.enabled && _lightAction.WasPressedThisFrame();
        }
    }
}

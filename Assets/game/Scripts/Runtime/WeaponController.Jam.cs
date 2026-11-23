using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed partial class WeaponController
    {
        private readonly List<JamHooks> _jamHooks = new List<JamHooks>();
        private bool _jamPending;
        private WeaponJamStatus? _publishedJam;

        public Func<WeaponSpec, IJamRoll> JamRolls { get; set; }

        public WeaponJamStatus JamStatus => WeaponJamStatus.Of(Weapon);

        public bool TryStartClearJam()
        {
            EnsureInitialized();
            var weapon = Weapon;
            if (weapon == null || IsSwitching || IsReloadBlockedByModifier || !weapon.TryStartClearJam())
            {
                return false;
            }

            PublishJam();
            return true;
        }

        public bool CancelClearJam()
        {
            EnsureInitialized();
            var canceled = CancelClearJam(Weapon);
            PublishJam();
            return canceled;
        }

        private bool CancelClearJam(Weapon weapon)
        {
            if (weapon == null || !weapon.IsClearingJam)
            {
                return false;
            }

            var remaining = weapon.JamClearRemainingSeconds;
            if (!weapon.CancelClearJam())
            {
                return false;
            }

            Debug.Log(WeaponJamLog.ClearCanceled(weapon.Spec.Id, remaining), this);
            return true;
        }

        // 今の武器のほかは Tick で進まないので、持ち替えで手を離した武器の解除はここで取り消す。
        private void CancelClearJamOfHolstered()
        {
            var current = _unarmed ? null : _loadout.Current;
            foreach (var weapon in _loadout.Items)
            {
                if (weapon != current)
                {
                    CancelClearJam(weapon);
                }
            }
        }

        private void HookJam(Weapon weapon)
        {
            var hooks = new JamHooks(this, weapon);
            hooks.Attach();
            _jamHooks.Add(hooks);
        }

        private void UnhookJam()
        {
            foreach (var hooks in _jamHooks)
            {
                hooks.Detach();
            }

            _jamHooks.Clear();
            _jamPending = false;
        }

        // Weapon.Jammed は TryFire の中で鳴るので、弾を撃ち終えてから知らせる。先に知らせると詰まった行が撃った行より前に出る。
        private void ReportPendingJam(Weapon weapon, int sustainedShots, float chance)
        {
            if (!_jamPending)
            {
                return;
            }

            _jamPending = false;
            Debug.Log(WeaponJamLog.Jammed(weapon.Spec.Id, sustainedShots, chance), this);
            CombatEvents.RaiseWeaponJammed(JamHolder, weapon.Spec.Id);
            PublishJam();
        }

        private void OnJamClearStarted(Weapon weapon)
        {
            Debug.Log(WeaponJamLog.ClearStarted(weapon.Spec.Id, weapon.JamClearRemainingSeconds), this);
            CombatEvents.RaiseWeaponJamClearStarted(JamHolder, weapon.Spec.Id);
        }

        private void OnJamCleared(Weapon weapon)
        {
            Debug.Log(WeaponJamLog.Cleared(weapon.Spec.Id), this);
            CombatEvents.RaiseWeaponJamCleared(JamHolder, weapon.Spec.Id);

            // 最後の 1 発で詰まると撃ったときの自動リロードは詰まりで断られているので、解除し終えたここで始める (ServerWeapon と同じ)。
            if (weapon == Weapon)
            {
                ReloadIfAuto(weapon);
            }
        }

        private GameObject JamHolder => _aim != null ? _aim.root.gameObject : gameObject;

        // 変わらないフレームは出し直さない。毎フレーム購読側を起こさないため。
        private void PublishJam()
        {
            if (_loadout == null)
            {
                return;
            }

            var status = JamStatus;
            if (_publishedJam.HasValue && _publishedJam.Value == status)
            {
                return;
            }

            _publishedJam = status;
            GameSignals.WeaponJam.Publish(status);
        }

        private void UnpublishJam()
        {
            if (_publishedJam.HasValue)
            {
                GameSignals.WeaponJam.ClearIfCurrent(_publishedJam);
                _publishedJam = null;
            }
        }

        private sealed class JamHooks
        {
            private readonly WeaponController _owner;
            private readonly Weapon _weapon;
            private readonly Action _jammed;
            private readonly Action _clearStarted;
            private readonly Action _cleared;

            public JamHooks(WeaponController owner, Weapon weapon)
            {
                _owner = owner;
                _weapon = weapon;
                _jammed = () => _owner._jamPending = true;
                _clearStarted = () => _owner.OnJamClearStarted(_weapon);
                _cleared = () => _owner.OnJamCleared(_weapon);
            }

            public void Attach()
            {
                _weapon.Jammed += _jammed;
                _weapon.JamClearStarted += _clearStarted;
                _weapon.JamCleared += _cleared;
            }

            public void Detach()
            {
                _weapon.Jammed -= _jammed;
                _weapon.JamClearStarted -= _clearStarted;
                _weapon.JamCleared -= _cleared;
            }
        }
    }
}

using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed partial class EnemyAgent
    {
        private bool _jamPending;

        public Func<WeaponSpec, IJamRoll> JamRolls { get; set; }

        // 種に敵のばらつきの種を混ぜるのは、Weapon の既定 (武器の Id だけの種) では同じ武器の敵が皆同じ発数で詰まるため。
        private Weapon CreateWeapon(WeaponSpec spec, GameRules rules, WeaponAmmo ammo)
        {
            IJamRoll jamRoll = null;
            if (spec.Ammo?.Jam != null && rules.Actions.Jam == WeaponJam.On)
            {
                jamRoll = JamRolls?.Invoke(spec) ?? new JamRoll(JamRoll.SeedFor(spec.Id) ^ _spreadSeed);
            }

            return ammo != null
                ? new Weapon(spec, Math.Min(ammo.AmmoInMagazine, spec.MagazineSize), ammo.ReserveAmmo, rules, jamRoll)
                : new Weapon(spec, rules, jamRoll);
        }

        private void UseWeapon(Weapon weapon)
        {
            if (_weapon != null)
            {
                _weapon.Reloaded -= OnReloaded;
                _weapon.Jammed -= OnJammed;
                _weapon.JamClearStarted -= OnJamClearStarted;
                _weapon.JamCleared -= OnJamCleared;
            }

            _jamPending = false;
            _weapon = weapon;
            if (_weapon == null)
            {
                return;
            }

            _weapon.Reloaded += OnReloaded;
            _weapon.Jammed += OnJammed;
            _weapon.JamClearStarted += OnJamClearStarted;
            _weapon.JamCleared += OnJamCleared;
        }

        private void ClearJam()
        {
            _weapon.TryStartClearJam();
        }

        // Weapon.Jammed は TryFire の中で鳴るので、弾を撃ち終えてから知らせる。先に知らせると詰まった行が撃った行より前に出る。
        private void OnJammed()
        {
            _jamPending = true;
        }

        private void ReportJammed(int sustainedShots, float chance)
        {
            Debug.Log($"{LogTag} id={Id} {WeaponJamLog.Jammed(_weapon.Spec.Id, sustainedShots, chance)}", this);
            CombatEvents.RaiseWeaponJammed(gameObject, _weapon.Spec.Id);
        }

        private void OnJamClearStarted()
        {
            Debug.Log($"{LogTag} id={Id} {WeaponJamLog.ClearStarted(_weapon.Spec.Id, _weapon.JamClearRemainingSeconds)}", this);
            CombatEvents.RaiseWeaponJamClearStarted(gameObject, _weapon.Spec.Id);
        }

        private void OnJamCleared()
        {
            Debug.Log($"{LogTag} id={Id} {WeaponJamLog.Cleared(_weapon.Spec.Id)}", this);
            CombatEvents.RaiseWeaponJamCleared(gameObject, _weapon.Spec.Id);

            // 最後の 1 発で詰まると撃ったときのリロードは詰まりで断られているので、解除し終えたここで始める。
            ReloadIfEmpty();
        }
    }
}

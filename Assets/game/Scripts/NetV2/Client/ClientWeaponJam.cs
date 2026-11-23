using System;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    public enum ClientJamChange
    {
        None,
        Jammed,
        Cleared,
    }

    // 詰まりは持ち主の WeaponSync にしか載らないので、この端末の人の今の武器だけを追う。
    // 持ち替えで別の武器の値に替わったときは、詰まった・解除し終えたとは数えない (詰まりは詰まった武器に残るため)。
    public sealed class ClientWeaponJam
    {
        private string _weaponId;
        private bool _jammed;
        private bool _clearing;

        public ClientJamChange Observe(in WeaponSync weapon, string weaponId)
        {
            var jammed = weapon.WeaponCount > 0 && weapon.IsJammed;
            var clearing = jammed && weapon.JamClearEndsAt > 0d;
            var sameWeapon = _weaponId != null && string.Equals(_weaponId, weaponId, StringComparison.Ordinal);
            var change = ClientJamChange.None;
            if (sameWeapon && jammed && !_jammed)
            {
                change = ClientJamChange.Jammed;
            }
            else if (sameWeapon && !jammed && _clearing)
            {
                // 解除をやめたときは詰まったまま届くので、解除中から詰まりが消えたときだけを解除し終えたとする
                change = ClientJamChange.Cleared;
            }

            _weaponId = weaponId;
            _jammed = jammed;
            _clearing = clearing;
            return change;
        }

        public void Reset()
        {
            _weaponId = null;
            _jammed = false;
            _clearing = false;
        }
    }
}

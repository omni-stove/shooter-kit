using System;

namespace Shooter.Core
{
    // 点けた武器の Id を覚えるのは、持ち替えて戻ったときに点いたまま戻らないよう、別の武器へ移った時点で消すため。
    public sealed class WeaponLightState
    {
        private string _weaponId;

        public bool IsOn { get; private set; }

        public string WeaponId => IsOn ? _weaponId : null;

        public WeaponLightStatus Status(bool hasLight)
        {
            return new WeaponLightStatus(hasLight, hasLight && IsOn);
        }

        public bool Toggle(string weaponId, bool hasLight, bool armed)
        {
            if (IsOn)
            {
                TurnOff();
                return true;
            }

            if (string.IsNullOrEmpty(weaponId) || !hasLight || !armed)
            {
                return false;
            }

            IsOn = true;
            _weaponId = weaponId;
            return true;
        }

        public WeaponLightOffCause? Follow(string weaponId, bool hasLight, bool armed)
        {
            if (!IsOn)
            {
                return null;
            }

            WeaponLightOffCause cause;
            if (!string.Equals(weaponId, _weaponId, StringComparison.Ordinal))
            {
                cause = WeaponLightOffCause.Switched;
            }
            else if (!armed)
            {
                cause = WeaponLightOffCause.Holstered;
            }
            else if (!hasLight)
            {
                cause = WeaponLightOffCause.NoLight;
            }
            else
            {
                return null;
            }

            TurnOff();
            return cause;
        }

        public bool TurnOff()
        {
            if (!IsOn)
            {
                return false;
            }

            IsOn = false;
            _weaponId = null;
            return true;
        }
    }
}

using System;

namespace Shooter.Core
{
    public readonly struct WeaponLightStatus : IEquatable<WeaponLightStatus>
    {
        public static readonly WeaponLightStatus None = new WeaponLightStatus(false, false);

        public WeaponLightStatus(bool hasLight, bool isOn)
        {
            if (isOn && !hasLight)
            {
                throw new ArgumentException("a weapon without a light cannot have it on", nameof(isOn));
            }

            HasLight = hasLight;
            IsOn = isOn;
        }

        public bool HasLight { get; }

        public bool IsOn { get; }

        public bool Equals(WeaponLightStatus other)
        {
            return HasLight == other.HasLight && IsOn == other.IsOn;
        }

        public override bool Equals(object obj)
        {
            return obj is WeaponLightStatus other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(HasLight, IsOn);
        }

        public static bool operator ==(WeaponLightStatus left, WeaponLightStatus right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(WeaponLightStatus left, WeaponLightStatus right)
        {
            return !left.Equals(right);
        }
    }
}

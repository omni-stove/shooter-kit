using System;
using GameKit.Core;

namespace Shooter.Core
{
    public sealed class WeaponLightSpec
    {
        public const float DefaultRange = 25f;
        public const float DefaultSpotAngle = 45f;
        public const float DefaultIntensity = 3f;
        public const float MaxSpotAngle = 179f;

        public static readonly WeaponLightSpec Default = new WeaponLightSpec();

        public WeaponLightSpec(
            float range = DefaultRange,
            float spotAngle = DefaultSpotAngle,
            float intensity = DefaultIntensity,
            string visualId = null)
        {
            Range = SpecGuard.PositiveFinite(range, nameof(range));

            // スポットライトの円錐は 180 度で平らになり照らす向きが決まらないので、Unity の Light と同じく 179 度までにする。
            if (!(spotAngle > 0f && spotAngle <= MaxSpotAngle))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(spotAngle), spotAngle, nameof(spotAngle) + " must be greater than 0 and at most " + MaxSpotAngle);
            }

            SpotAngle = spotAngle;
            Intensity = SpecGuard.NonNegativeFinite(intensity, nameof(intensity));
            VisualId = SpecGuard.ReferenceId(visualId);
        }

        public float Range { get; }

        public float SpotAngle { get; }

        public float Intensity { get; }

        public string VisualId { get; }

        public bool HasVisual => VisualId.Length > 0;
    }
}

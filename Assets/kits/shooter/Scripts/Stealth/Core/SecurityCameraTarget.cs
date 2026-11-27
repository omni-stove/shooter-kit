using System;
using System.Numerics;

namespace Shooter.Core
{
    // AwarenessSighting をそのまま受け取らないのは、視野の中心からの角度がカメラの首の向きで決まり、首の向きはカメラ自身が tick の中で動かすため。
    public readonly struct SecurityCameraTarget
    {
        public static SecurityCameraTarget None => default;

        public SecurityCameraTarget(
            bool hasLineOfSight,
            float distance,
            float bearingDegrees,
            Vector3 position,
            AwarenessPosture posture,
            float lightFactor,
            float speed,
            bool isArmed = true,
            bool isLit = false)
        {
            if (!(distance >= 0f) || float.IsInfinity(distance))
            {
                throw new ArgumentOutOfRangeException(nameof(distance), distance, "distance must not be negative and must be finite");
            }

            if (float.IsNaN(bearingDegrees) || float.IsInfinity(bearingDegrees))
            {
                throw new ArgumentOutOfRangeException(nameof(bearingDegrees), bearingDegrees, "bearingDegrees must be finite");
            }

            if (!IsFinite(position.X) || !IsFinite(position.Y) || !IsFinite(position.Z))
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, "position must be finite");
            }

            if (!Enum.IsDefined(typeof(AwarenessPosture), posture))
            {
                throw new ArgumentOutOfRangeException(nameof(posture), posture, "unknown posture");
            }

            if (!(lightFactor >= 0f) || lightFactor > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(lightFactor), lightFactor, "lightFactor must be between 0 and 1");
            }

            if (!(speed >= 0f) || float.IsInfinity(speed))
            {
                throw new ArgumentOutOfRangeException(nameof(speed), speed, "speed must not be negative and must be finite");
            }

            HasLineOfSight = hasLineOfSight;
            Distance = distance;
            BearingDegrees = bearingDegrees;
            Position = position;
            Posture = posture;
            LightFactor = lightFactor;
            Speed = speed;
            IsArmed = isArmed;
            IsLit = isLit;
        }

        public bool HasLineOfSight { get; }

        public float Distance { get; }

        // カメラの据え付けの正面 (首振りの真ん中) から測った左右の角度。正の側は首振りが最初に向かう側。
        public float BearingDegrees { get; }

        public Vector3 Position { get; }

        public AwarenessPosture Posture { get; }

        public float LightFactor { get; }

        public float Speed { get; }

        public bool IsArmed { get; }

        public bool IsLit { get; }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}

using System;
using System.Numerics;

namespace Shooter.Core
{
    public readonly struct SecurityAlarmStatus : IEquatable<SecurityAlarmStatus>
    {
        private readonly string _cameraId;

        public static SecurityAlarmStatus Silent => default;

        // 鳴っていないときは残り秒数・場所・Id を空に揃える。意味の無い値の違いで変化の知らせが余計に出ないようにするため。
        public SecurityAlarmStatus(bool ringing, float secondsLeft, Vector3 spottedAt, string cameraId)
        {
            SpecGuard.NonNegativeFinite(secondsLeft, nameof(secondsLeft));
            if (!IsFinite(spottedAt.X) || !IsFinite(spottedAt.Y) || !IsFinite(spottedAt.Z))
            {
                throw new ArgumentOutOfRangeException(nameof(spottedAt), spottedAt, "spottedAt must be finite");
            }

            Ringing = ringing;
            SecondsLeft = ringing ? secondsLeft : 0f;
            SpottedAt = ringing ? spottedAt : Vector3.Zero;
            _cameraId = ringing ? SpecGuard.ReferenceId(cameraId) : string.Empty;
        }

        public bool Ringing { get; }

        public float SecondsLeft { get; }

        public Vector3 SpottedAt { get; }

        public string CameraId => _cameraId ?? string.Empty;

        public bool Equals(SecurityAlarmStatus other)
        {
            return Ringing == other.Ringing
                && SecondsLeft.Equals(other.SecondsLeft)
                && SpottedAt.Equals(other.SpottedAt)
                && string.Equals(CameraId, other.CameraId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is SecurityAlarmStatus other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Ringing, SecondsLeft, SpottedAt, CameraId);
        }

        public static bool operator ==(SecurityAlarmStatus left, SecurityAlarmStatus right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SecurityAlarmStatus left, SecurityAlarmStatus right)
        {
            return !left.Equals(right);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}

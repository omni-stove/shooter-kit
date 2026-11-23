using System;

namespace Shooter.Core
{
    public sealed class SecurityAlarmSpec
    {
        public const float DefaultDurationSeconds = 20f;

        // 見張りどうしの呼び声 (AwarenessSpec.DefaultAlarmRadius) より広く、建物 1 棟の見張りを集める。
        public const float DefaultRadius = 30f;

        public static SecurityAlarmSpec Default { get; } = new SecurityAlarmSpec();

        public SecurityAlarmSpec(float durationSeconds = DefaultDurationSeconds, float radius = DefaultRadius)
        {
            SpecGuard.PositiveFinite(durationSeconds, nameof(durationSeconds));
            SpecGuard.NonNegativeFinite(radius, nameof(radius));

            DurationSeconds = durationSeconds;
            Radius = radius;
        }

        public float DurationSeconds { get; }

        // 0 なら鳴っても誰も呼ばない。足音の半径と同じく、音を消す設定として受け付ける。
        public float Radius { get; }
    }
}

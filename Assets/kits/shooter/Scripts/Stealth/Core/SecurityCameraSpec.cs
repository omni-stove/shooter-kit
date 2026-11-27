using System;
using GameKit.Core;

namespace Shooter.Core
{
    public sealed class SecurityCameraSpec
    {
        // 正面から左右に 45 度ずつ。1 台で通路の端から端まで見られる。0 なら首を振らない据え置きのカメラ。
        public const float DefaultSweepDegrees = 90f;

        // 端から端まで 4.5 秒。見られずに抜けられる間がある速さ。
        public const float DefaultSweepDegreesPerSecond = 20f;

        public const float DefaultEdgePauseSeconds = 2f;

        public const float DefaultViewDistance = 15f;

        // 見張りの目 (AwarenessSpec.DefaultFieldOfViewDegrees) より狭くして、首振りの外を抜ける遊びを残す。
        public const float DefaultFieldOfViewDegrees = 60f;

        public const float DefaultDisableHoldSeconds = 1.5f;

        public const float DefaultDisabledSeconds = 30f;

        // 小銃の胴撃ち 2 発で壊れる量。1 発で壊れると撃てば済み、近づいて長押しで止める方を選ぶ理由が無くなる。
        public const int DefaultMaxHealth = 50;

        // 天井近くに付けたカメラの真下に立てば届く距離。目 (レンズ) からプレイヤーの体の中心までで測る。
        public const float DefaultDisableRadius = 2.5f;

        public static SecurityCameraSpec Default { get; } = new SecurityCameraSpec();

        // 気づきの視野を別に持たないのは、AwarenessMeter が視野の内外を Awareness.FieldOfViewDegrees で決めるので、2 つの値が食い違うとカメラの見える範囲が 1 つに決まらないため。
        public SecurityCameraSpec(
            float sweepDegrees = DefaultSweepDegrees,
            float sweepDegreesPerSecond = DefaultSweepDegreesPerSecond,
            float edgePauseSeconds = DefaultEdgePauseSeconds,
            float viewDistance = DefaultViewDistance,
            float fieldOfViewDegrees = DefaultFieldOfViewDegrees,
            AwarenessSpec awareness = null,
            float disableHoldSeconds = DefaultDisableHoldSeconds,
            float disabledSeconds = DefaultDisabledSeconds,
            int maxHealth = DefaultMaxHealth,
            float disableRadius = DefaultDisableRadius)
        {
            if (!(sweepDegrees >= 0f) || sweepDegrees > 360f)
            {
                throw new ArgumentOutOfRangeException(nameof(sweepDegrees), sweepDegrees, "sweepDegrees must be between 0 and 360");
            }

            SpecGuard.PositiveFinite(sweepDegreesPerSecond, nameof(sweepDegreesPerSecond));
            SpecGuard.NonNegativeFinite(edgePauseSeconds, nameof(edgePauseSeconds));
            SpecGuard.PositiveFinite(viewDistance, nameof(viewDistance));
            if (!(fieldOfViewDegrees > 0f) || fieldOfViewDegrees > 360f)
            {
                throw new ArgumentOutOfRangeException(nameof(fieldOfViewDegrees), fieldOfViewDegrees, "fieldOfViewDegrees must be greater than 0 and at most 360");
            }

            if (awareness != null && !awareness.FieldOfViewDegrees.Equals(fieldOfViewDegrees))
            {
                throw new ArgumentException(
                    "awareness.FieldOfViewDegrees (" + awareness.FieldOfViewDegrees + ") must equal fieldOfViewDegrees (" + fieldOfViewDegrees + ")",
                    nameof(awareness));
            }

            SpecGuard.PositiveFinite(disableHoldSeconds, nameof(disableHoldSeconds));
            SpecGuard.PositiveFinite(disabledSeconds, nameof(disabledSeconds));
            SpecGuard.AtLeast(maxHealth, 1, nameof(maxHealth));
            SpecGuard.PositiveFinite(disableRadius, nameof(disableRadius));

            SweepDegrees = sweepDegrees;
            SweepDegreesPerSecond = sweepDegreesPerSecond;
            EdgePauseSeconds = edgePauseSeconds;
            ViewDistance = viewDistance;
            FieldOfViewDegrees = fieldOfViewDegrees;
            Awareness = awareness ?? new AwarenessSpec(fieldOfViewDegrees: fieldOfViewDegrees);
            DisableHoldSeconds = disableHoldSeconds;
            DisabledSeconds = disabledSeconds;
            MaxHealth = maxHealth;
            DisableRadius = disableRadius;
        }

        public float SweepDegrees { get; }
        public float SweepDegreesPerSecond { get; }
        public float EdgePauseSeconds { get; }
        public float ViewDistance { get; }
        public float FieldOfViewDegrees { get; }
        public AwarenessSpec Awareness { get; }
        public float DisableHoldSeconds { get; }
        public float DisabledSeconds { get; }
        public int MaxHealth { get; }
        public float DisableRadius { get; }
    }
}

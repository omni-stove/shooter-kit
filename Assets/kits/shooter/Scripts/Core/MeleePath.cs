using System;
using GameKit.Core;

namespace Shooter.Core
{
    public sealed class MeleePath
    {
        public const float MaxYawDegrees = 180f;
        public const float MaxPitchDegrees = 90f;
        public const MeleePathStyle DefaultStyle = MeleePathStyle.Horizontal;
        public const float DefaultFromDegrees = 45f;
        public const float DefaultToDegrees = -45f;
        public const float DefaultWidthDegrees = 30f;
        public const float DefaultActiveSeconds = 0.1f;
        public const int DefaultMaxTargets = 1;

        // 角度は体の正面を 0 にし、横は右が +、縦は上が +。縦を照準の上下でなく水平から測るのは、サーバーの体が上下の向きを持たないため。
        public MeleePath(
            MeleePathStyle style,
            float fromDegrees,
            float toDegrees,
            float widthDegrees = DefaultWidthDegrees,
            float activeSeconds = DefaultActiveSeconds,
            int maxTargets = DefaultMaxTargets)
        {
            Style = SpecGuard.Defined(style, nameof(style));
            var limit = style == MeleePathStyle.Vertical ? MaxPitchDegrees : MaxYawDegrees;
            FromDegrees = Within(fromDegrees, limit, nameof(fromDegrees));
            ToDegrees = Within(toDegrees, limit, nameof(toDegrees));

            // 始めと終わりが同じだと刃が動かず、どの相手も「通った」と言えない。突きは距離で進むので角度を使わない。
            if (style != MeleePathStyle.Thrust && fromDegrees == toDegrees)
            {
                throw new ArgumentOutOfRangeException(nameof(toDegrees), toDegrees, "toDegrees must differ from fromDegrees");
            }

            if (!(widthDegrees > 0f && widthDegrees <= MeleeSpec.MaxArcDegrees))
            {
                throw new ArgumentOutOfRangeException(nameof(widthDegrees), widthDegrees, "widthDegrees must be greater than 0 and at most 360");
            }

            WidthDegrees = widthDegrees;
            ActiveSeconds = SpecGuard.NonNegativeFinite(activeSeconds, nameof(activeSeconds));
            MaxTargets = SpecGuard.AtLeast(maxTargets, 1, nameof(maxTargets));
        }

        public MeleePathStyle Style { get; }

        public float FromDegrees { get; }

        public float ToDegrees { get; }

        // 刃の通る向きと直角に測った全体の角度。横振りなら上下、縦振りなら左右、突きなら上下と左右の両方の幅。
        public float WidthDegrees { get; }

        // 当たりの瞬間 (MeleeSpec.ImpactSeconds) から刃が通り終えるまでの秒数。0 なら当たりの瞬間に全体を一度に通る。
        public float ActiveSeconds { get; }

        public int MaxTargets { get; }

        public float DegreesAt(float progress)
        {
            return FromDegrees + ((ToDegrees - FromDegrees) * progress);
        }

        // 型が違う軌道は混ぜられないので、溜めの半分を境に近いほうを使う。
        public static MeleePath Lerp(MeleePath from, MeleePath to, float t)
        {
            if (from == null || to == null || from.Style != to.Style)
            {
                return t < 0.5f ? from : to;
            }

            return new MeleePath(
                from.Style,
                from.FromDegrees + ((to.FromDegrees - from.FromDegrees) * t),
                from.ToDegrees + ((to.ToDegrees - from.ToDegrees) * t),
                from.WidthDegrees + ((to.WidthDegrees - from.WidthDegrees) * t),
                from.ActiveSeconds + ((to.ActiveSeconds - from.ActiveSeconds) * t),
                (int)Math.Round(from.MaxTargets + ((to.MaxTargets - from.MaxTargets) * t), MidpointRounding.AwayFromZero));
        }

        private static float Within(float degrees, float limit, string paramName)
        {
            if (!(degrees >= -limit && degrees <= limit))
            {
                throw new ArgumentOutOfRangeException(paramName, degrees, paramName + " must be between -" + limit + " and " + limit);
            }

            return degrees;
        }
    }
}

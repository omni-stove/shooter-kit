using System;
using System.Collections.Generic;
using System.Numerics;

namespace Shooter.Core
{
    // 座標は Y が上。前方の扇は水平面で測り、届く距離は高さの差も含めて測る (段の上の相手に下から届かないように)。
    public static class MeleeJudge
    {
        private const float _minLengthSquared = 1e-8f;

        // 刃の端 (0 と 1) ちょうどにいる相手を、浮動小数の丸めで取りこぼさないため。
        private const float _progressEpsilon = 1e-5f;

        public static bool IsInFan(Vector3 origin, Vector3 forward, MeleeSpec spec, Vector3 point, out float distance)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            distance = 0f;
            if (!IsFinite(origin) || !IsFinite(forward) || !IsFinite(point))
            {
                return false;
            }

            if (!TryFlatten(forward, out _))
            {
                return false;
            }

            distance = (point - origin).Length();
            return distance <= spec.Reach && IsWithinArc(origin, forward, spec.ArcDegrees, point);
        }

        // 距離は問わず、水平の向きだけを見る。前方が水平に決まらなければ false。
        public static bool IsWithinArc(Vector3 origin, Vector3 forward, float arcDegrees, Vector3 point)
        {
            if (!IsFinite(origin) || !IsFinite(forward) || !IsFinite(point) || !(arcDegrees > 0f))
            {
                return false;
            }

            if (!TryFlatten(forward, out var flatForward))
            {
                return false;
            }

            // 真上・真下や体に重なった相手は向きが決まらない。触れているほど近いので扇の中とみなす。
            if (!TryFlatten(point - origin, out var flatOffset))
            {
                return true;
            }

            var halfArc = Math.Min(arcDegrees, MeleeSpec.MaxArcDegrees) / 2.0;
            return Vector3.Dot(flatForward, flatOffset) >= Math.Cos(halfArc * Math.PI / 180.0) - 1e-6;
        }

        // 同じ距離なら先に並んでいるほうを選ぶ。呼ぶ側が並びを決めておけば、毎回同じ相手を選ぶ。
        public static int SelectNearest(Vector3 origin, Vector3 forward, MeleeSpec spec, IReadOnlyList<Vector3> candidates)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            var best = -1;
            var bestDistance = float.PositiveInfinity;
            for (var i = 0; i < candidates.Count; i++)
            {
                if (IsInFan(origin, forward, spec, candidates[i], out var distance) && distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            return best;
        }

        // 軌道 (MeleeSpec.Path) を持つ振りで、刃が progressFrom から progressTo まで進む間に相手の点を通ったか。通った進み (0〜1) を crossing に返す。
        // 刃の後ろへ回り込んだ相手は通っていないので当てない。一度通った相手を数えるのは呼ぶ側 (振りごとに覚える)。
        public static bool TryCross(Vector3 origin, Vector3 forward, MeleeSpec spec, Vector3 point, float progressFrom, float progressTo, out float crossing, out float distance)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var path = spec.Path ?? throw new ArgumentException("spec has no path", nameof(spec));
            crossing = 0f;
            distance = 0f;
            if (!IsFinite(origin) || !IsFinite(forward) || !IsFinite(point) || !TryFlatten(forward, out var flatForward))
            {
                return false;
            }

            var offset = point - origin;
            distance = offset.Length();
            if (distance > spec.Reach)
            {
                return false;
            }

            // 真上・真下や体に重なった相手は向きが決まらない。触れているほど近いので、刃が動き始めた時点で通ったとみなす。
            if (!TryFlatten(offset, out var flatOffset))
            {
                crossing = progressFrom;
                return true;
            }

            var right = new Vector3(flatForward.Z, 0f, -flatForward.X);
            var yaw = Degrees(Math.Atan2(Vector3.Dot(flatOffset, right), Vector3.Dot(flatOffset, flatForward)));
            var horizontal = Math.Sqrt((offset.X * offset.X) + (offset.Z * offset.Z));
            var pitch = Degrees(Math.Atan2(offset.Y, horizontal));
            var halfWidth = path.WidthDegrees / 2f;
            switch (path.Style)
            {
                case MeleePathStyle.Horizontal:
                    return Math.Abs(pitch) <= halfWidth && Crosses(path, yaw, progressFrom, progressTo, out crossing);
                case MeleePathStyle.Vertical:
                    return Math.Abs(yaw) <= halfWidth && Crosses(path, pitch, progressFrom, progressTo, out crossing);
                default:
                    if (Math.Abs(yaw) > halfWidth || Math.Abs(pitch) > halfWidth)
                    {
                        return false;
                    }

                    crossing = Math.Max(progressFrom, distance / spec.Reach);
                    return crossing <= progressTo + _progressEpsilon;
            }
        }

        // 刃が通った順に、通った候補の番号を crossed へ入れる。同じ瞬間なら近い順、それも同じなら候補の並び順にし、毎回同じ相手を選ぶ。
        public static void SelectCrossed(Vector3 origin, Vector3 forward, MeleeSpec spec, IReadOnlyList<Vector3> candidates, float progressFrom, float progressTo, List<int> crossed)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            if (crossed == null)
            {
                throw new ArgumentNullException(nameof(crossed));
            }

            crossed.Clear();
            var found = new List<(int Index, float Crossing, float Distance)>();
            for (var i = 0; i < candidates.Count; i++)
            {
                if (TryCross(origin, forward, spec, candidates[i], progressFrom, progressTo, out var crossing, out var distance))
                {
                    found.Add((i, crossing, distance));
                }
            }

            found.Sort((a, b) =>
            {
                var order = a.Crossing.CompareTo(b.Crossing);
                if (order == 0)
                {
                    order = a.Distance.CompareTo(b.Distance);
                }

                return order != 0 ? order : a.Index.CompareTo(b.Index);
            });
            foreach (var entry in found)
            {
                crossed.Add(entry.Index);
            }
        }

        private static bool Crosses(MeleePath path, float degrees, float progressFrom, float progressTo, out float crossing)
        {
            crossing = (degrees - path.FromDegrees) / (path.ToDegrees - path.FromDegrees);
            return crossing >= progressFrom - _progressEpsilon && crossing <= progressTo + _progressEpsilon;
        }

        private static float Degrees(double radians)
        {
            return (float)(radians * 180.0 / Math.PI);
        }

        // 怯みや吹き飛ばしが使う向き。上下に飛ばさないよう水平にし、決まらなければ振った向きにする。
        public static Vector3 HitDirection(Vector3 origin, Vector3 forward, Vector3 point)
        {
            if (IsFinite(origin) && IsFinite(point) && TryFlatten(point - origin, out var toTarget))
            {
                return toTarget;
            }

            return IsFinite(forward) && TryFlatten(forward, out var flatForward) ? flatForward : Vector3.Zero;
        }

        private static bool TryFlatten(Vector3 value, out Vector3 flat)
        {
            var horizontal = new Vector3(value.X, 0f, value.Z);
            var lengthSquared = horizontal.LengthSquared();
            if (!(lengthSquared >= _minLengthSquared) || float.IsInfinity(lengthSquared))
            {
                flat = Vector3.Zero;
                return false;
            }

            flat = horizontal / (float)Math.Sqrt(lengthSquared);
            return true;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.X) && IsFinite(value.Y) && IsFinite(value.Z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}

using System;
using System.Numerics;

namespace Shooter.Core
{
    public enum ParkourMove : byte
    {
        None = 0,
        WallKick = 1,
        LandingRoll = 2,
        WallClimb = 3,
        LedgeHang = 4,
        LedgeShimmy = 5,
        LedgeClimbUp = 6,
    }

    // 動きごとに状態を分けて持つと CharacterMotorState の引数と比べる所が動きを足すたびに増えるので、
    // 同時にはひとつしか行わない壁蹴り・受け身・壁登り・ぶら下がりを、ひとつの状態にまとめて持つ。
    public readonly struct CharacterParkourState : IEquatable<CharacterParkourState>
    {
        public const float NormalFallSpeedScale = 1f;

        // 割合だけを持つと既定値 (default) の状態で 0 になり落下の傷みを消してしまうので、作り方で割合を渡したかを別に持ち、渡していなければ 1 を返す。
        private readonly float _fallSpeedScale;
        private readonly bool _hasFallSpeedScale;

        public CharacterParkourState(ParkourMove move, float elapsed, Vector3 wallNormal, Vector3 ledgePoint)
            : this(move, elapsed, wallNormal, ledgePoint, NormalFallSpeedScale)
        {
        }

        // 何もしていない (None) 間も秒数と法線を残せるようにするのは、離してからの秒数や前に蹴った壁で、掴み直しと同じ壁の蹴り直しを断るため。
        public CharacterParkourState(ParkourMove move, float elapsed, Vector3 wallNormal, Vector3 ledgePoint, float fallSpeedScale)
        {
            Move = move;
            Elapsed = elapsed;
            WallNormal = wallNormal;
            LedgePoint = ledgePoint;
            _fallSpeedScale = fallSpeedScale;
            _hasFallSpeedScale = true;
        }

        public static CharacterParkourState None => default;

        public ParkourMove Move { get; }

        public float Elapsed { get; }

        public Vector3 WallNormal { get; }

        public Vector3 LedgePoint { get; }

        public float FallSpeedScale => _hasFallSpeedScale ? _fallSpeedScale : NormalFallSpeedScale;

        public bool IsActive => Move != ParkourMove.None;

        public bool IsHangingOnLedge => Move == ParkourMove.LedgeHang || Move == ParkourMove.LedgeShimmy || Move == ParkourMove.LedgeClimbUp;

        public bool IsValid => Move <= ParkourMove.LedgeClimbUp
            && IsFinite(Elapsed) && Elapsed >= 0f
            && IsFinite(WallNormal.X) && IsFinite(WallNormal.Y) && IsFinite(WallNormal.Z)
            && IsFinite(LedgePoint.X) && IsFinite(LedgePoint.Y) && IsFinite(LedgePoint.Z)
            && FallSpeedScale >= 0f && FallSpeedScale <= NormalFallSpeedScale;

        public bool Equals(CharacterParkourState other)
        {
            return Move == other.Move
                && Elapsed.Equals(other.Elapsed)
                && WallNormal.Equals(other.WallNormal)
                && LedgePoint.Equals(other.LedgePoint)
                && FallSpeedScale.Equals(other.FallSpeedScale);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterParkourState other && Equals(other);
        }

        public static bool operator ==(CharacterParkourState left, CharacterParkourState right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CharacterParkourState left, CharacterParkourState right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Move * 397) ^ Elapsed.GetHashCode() ^ (WallNormal.GetHashCode() * 31) ^ (LedgePoint.GetHashCode() * 17)
                    ^ FallSpeedScale.GetHashCode();
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}

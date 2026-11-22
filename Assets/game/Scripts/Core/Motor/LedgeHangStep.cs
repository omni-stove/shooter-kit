using System;
using System.Numerics;

namespace Shooter.Core
{
    public enum LedgeHangMotion : byte
    {
        None = 0,
        Grabbed = 1,
        Hanging = 2,
        Shimmying = 3,
        StoppedAtEdge = 4,
        ClimbingUp = 5,
        ClimbedUp = 6,
        LetGo = 7,
    }

    public enum LedgeLetGoReason : byte
    {
        None = 0,
        Dropped = 1,
        LostLedge = 2,
        Landed = 3,
    }

    public readonly struct LedgeHangResult
    {
        public LedgeHangResult(
            CharacterParkourState state, LedgeHangMotion motion, LedgeLetGoReason letGoReason, Vector3 position, Vector3 velocity)
        {
            State = state;
            Motion = motion;
            LetGoReason = motion == LedgeHangMotion.LetGo ? letGoReason : LedgeLetGoReason.None;
            Position = position;
            Velocity = velocity;
        }

        public CharacterParkourState State { get; }

        public LedgeHangMotion Motion { get; }

        public LedgeLetGoReason LetGoReason { get; }

        public Vector3 Position { get; }

        public Vector3 Velocity { get; }

        // 離した tick は速さを 0 にして落とし始めるので、位置と速さをふつうの移動へ渡すのは何もしなかった tick だけ。
        public bool HoldsBody => Motion != LedgeHangMotion.None && Motion != LedgeHangMotion.LetGo;

        public static LedgeHangResult Resting(CharacterParkourState state, Vector3 position, Vector3 velocity)
        {
            return new LedgeHangResult(state, LedgeHangMotion.None, LedgeLetGoReason.None, position, velocity);
        }
    }

    // ふちを呼び手が CharacterLedgeSensor で調べて渡すのは、WallRunStep と同じく世界を引かない計算にして、ふちの有無ごとの決まりを世界を組まずに確かめるため。
    // 登る軌跡は状態に持たず、ふちの位置と壁の法線から毎 tick 作り直す。スナップショットに頂点を載せずに、巻き戻した tick から同じ道をたどれるようにするため。
    public static class LedgeHangStep
    {
        public static LedgeHangResult Step(
            CharacterParkourState parkour,
            LedgeContact ledge,
            Vector3 position,
            Vector3 velocity,
            Vector2 move,
            bool jump,
            bool crouch,
            bool grounded,
            StanceGeometry geometry,
            LedgeHangTuning tuning,
            float tickSeconds,
            Func<VaultPath, bool> isClimbClear)
        {
            if (geometry == null)
            {
                throw new ArgumentNullException(nameof(geometry));
            }

            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }

            if (!(tickSeconds > 0f) || float.IsInfinity(tickSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(tickSeconds), tickSeconds, "tickSeconds must be positive and finite");
            }

            if (isClimbClear == null)
            {
                throw new ArgumentNullException(nameof(isClimbClear));
            }

            if (!tuning.Enabled)
            {
                var cleared = parkour.IsHangingOnLedge || IsWaitingToRegrab(parkour) ? CharacterParkourState.None : parkour;
                return LedgeHangResult.Resting(cleared, position, velocity);
            }

            switch (parkour.Move)
            {
                case ParkourMove.None:
                case ParkourMove.WallKick:
                    return TryGrab(parkour, ledge, position, velocity, grounded, crouch, geometry, tuning, tickSeconds);
                case ParkourMove.LedgeHang:
                case ParkourMove.LedgeShimmy:
                    return Hold(parkour, ledge, position, move, jump, crouch, grounded, geometry, tuning, tickSeconds, isClimbClear);
                case ParkourMove.LedgeClimbUp:
                    return ClimbUp(parkour, geometry, tuning, tickSeconds);
                default:
                    return LedgeHangResult.Resting(parkour, position, velocity);
            }
        }

        public static Vector3 HangPosition(Vector3 ledgePoint, Vector3 wallNormal, StanceGeometry geometry)
        {
            if (geometry == null)
            {
                throw new ArgumentNullException(nameof(geometry));
            }

            var away = geometry.Radius + VaultProbe.WallGap;
            return ledgePoint + (wallNormal * away) - (Vector3.UnitY * geometry.Standing.TopY);
        }

        // 乗り越えの軌跡 (PlanPath) でなく崖のぼりの軌跡にするのは、乗り越えは奥の面の向こうへ下りてしまい、上端に立たないため。
        public static VaultPath ClimbPath(Vector3 wallNormal, StanceGeometry geometry, LedgeHangTuning tuning)
        {
            if (geometry == null)
            {
                throw new ArgumentNullException(nameof(geometry));
            }

            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }

            return VaultProbe.PlanMantlePath(
                -wallNormal, geometry.Radius + VaultProbe.WallGap, geometry.Standing.Height, geometry.Radius, tuning.ClimbUpSpeed);
        }

        // 離した印を状態に別に持たず、何もしていない (None) のままふちの位置を残すことで表す。スナップショットと比べる所を増やさないため。
        // 原点ちょうどのふちだけは待たずに掴み直せるが、ふちが世界の原点に来ることはまず無いので受け入れる。
        public static bool IsWaitingToRegrab(CharacterParkourState parkour)
        {
            return parkour.Move == ParkourMove.None && parkour.LedgePoint != Vector3.Zero;
        }

        public static CharacterParkourState Released(CharacterParkourState hanging, float sinceRelease)
        {
            return new CharacterParkourState(ParkourMove.None, sinceRelease, hanging.WallNormal, hanging.LedgePoint);
        }

        private static LedgeHangResult TryGrab(
            CharacterParkourState parkour,
            LedgeContact ledge,
            Vector3 position,
            Vector3 velocity,
            bool grounded,
            bool crouch,
            StanceGeometry geometry,
            LedgeHangTuning tuning,
            float tickSeconds)
        {
            var state = parkour;
            if (IsWaitingToRegrab(parkour))
            {
                var sinceRelease = parkour.Elapsed + tickSeconds;
                if (sinceRelease < tuning.RegrabSeconds)
                {
                    return LedgeHangResult.Resting(Released(parkour, sinceRelease), position, velocity);
                }

                state = CharacterParkourState.None;
            }

            // しゃがみは離す入力なので、押したまま落ちる間は掴まない
            if (grounded || velocity.Y > 0f || crouch || !ledge.IsFound)
            {
                return LedgeHangResult.Resting(state, position, velocity);
            }

            var hanging = new CharacterParkourState(ParkourMove.LedgeHang, tickSeconds, ledge.Normal, ledge.Point);
            return new LedgeHangResult(
                hanging, LedgeHangMotion.Grabbed, LedgeLetGoReason.None, HangPosition(ledge.Point, ledge.Normal, geometry), Vector3.Zero);
        }

        private static LedgeHangResult Hold(
            CharacterParkourState parkour,
            LedgeContact ledge,
            Vector3 position,
            Vector2 move,
            bool jump,
            bool crouch,
            bool grounded,
            StanceGeometry geometry,
            LedgeHangTuning tuning,
            float tickSeconds,
            Func<VaultPath, bool> isClimbClear)
        {
            if (grounded)
            {
                return LetGo(parkour, position, LedgeLetGoReason.Landed);
            }

            if (crouch)
            {
                return LetGo(parkour, position, LedgeLetGoReason.Dropped);
            }

            if (!ledge.IsFound)
            {
                return LetGo(parkour, position, LedgeLetGoReason.LostLedge);
            }

            // 上がふさがっていれば跳んでも登らず、ぶら下がったままにする。離すとふさがった所の下へ落ちて、跳んだのに落ちたように見えるため。
            if (jump)
            {
                var path = ClimbPath(ledge.Normal, geometry, tuning);
                if (isClimbClear(path))
                {
                    var climbing = new CharacterParkourState(ParkourMove.LedgeClimbUp, 0f, ledge.Normal, ledge.Point);
                    return FollowClimb(climbing, path, HangPosition(ledge.Point, ledge.Normal, geometry), tickSeconds);
                }
            }

            var side = Math.Sign(move.X);
            if (side == 0)
            {
                return Hang(parkour, ledge, LedgeHangMotion.Hanging, geometry, tickSeconds);
            }

            var continues = side > 0 ? ledge.ContinuesRight : ledge.ContinuesLeft;
            if (!continues)
            {
                return Hang(parkour, ledge, LedgeHangMotion.StoppedAtEdge, geometry, tickSeconds);
            }

            var along = CharacterLedgeSensor.RightAlong(ledge.Normal) * (side * tuning.ShimmySpeed);
            var point = ledge.Point + (along * tickSeconds);
            var state = new CharacterParkourState(ParkourMove.LedgeShimmy, Next(parkour, ParkourMove.LedgeShimmy, tickSeconds), ledge.Normal, point);
            return new LedgeHangResult(state, LedgeHangMotion.Shimmying, LedgeLetGoReason.None, HangPosition(point, ledge.Normal, geometry), along);
        }

        private static LedgeHangResult Hang(
            CharacterParkourState parkour, LedgeContact ledge, LedgeHangMotion motion, StanceGeometry geometry, float tickSeconds)
        {
            var state = new CharacterParkourState(ParkourMove.LedgeHang, Next(parkour, ParkourMove.LedgeHang, tickSeconds), ledge.Normal, ledge.Point);
            return new LedgeHangResult(state, motion, LedgeLetGoReason.None, HangPosition(ledge.Point, ledge.Normal, geometry), Vector3.Zero);
        }

        private static LedgeHangResult ClimbUp(CharacterParkourState parkour, StanceGeometry geometry, LedgeHangTuning tuning, float tickSeconds)
        {
            var path = ClimbPath(parkour.WallNormal, geometry, tuning);
            return FollowClimb(parkour, path, HangPosition(parkour.LedgePoint, parkour.WallNormal, geometry), tickSeconds);
        }

        // 乗り越えと同じく時刻の差で動かし、tick の刻みによらず上端の同じ所へ着くようにする。
        private static LedgeHangResult FollowClimb(CharacterParkourState climbing, VaultPath path, Vector3 start, float tickSeconds)
        {
            var elapsed = climbing.Elapsed + tickSeconds;
            if (elapsed >= path.Duration)
            {
                return new LedgeHangResult(CharacterParkourState.None, LedgeHangMotion.ClimbedUp, LedgeLetGoReason.None, start + path.End, Vector3.Zero);
            }

            var state = new CharacterParkourState(ParkourMove.LedgeClimbUp, elapsed, climbing.WallNormal, climbing.LedgePoint);
            var velocity = path.Displacement(climbing.Elapsed, elapsed) / tickSeconds;
            return new LedgeHangResult(state, LedgeHangMotion.ClimbingUp, LedgeLetGoReason.None, start + path.PositionAt(elapsed), velocity);
        }

        private static LedgeHangResult LetGo(CharacterParkourState parkour, Vector3 position, LedgeLetGoReason reason)
        {
            return new LedgeHangResult(Released(parkour, 0f), LedgeHangMotion.LetGo, reason, position, Vector3.Zero);
        }

        private static float Next(CharacterParkourState parkour, ParkourMove move, float tickSeconds)
        {
            return parkour.Move == move ? parkour.Elapsed + tickSeconds : tickSeconds;
        }
    }
}

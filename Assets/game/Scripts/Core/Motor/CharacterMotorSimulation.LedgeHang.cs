using System;
using System.Numerics;

namespace Shooter.Core
{
    public sealed partial class CharacterMotorSimulation
    {
        private CharacterLedgeSensor _ledgeSensor;

        // 登れるかの判定に渡す。毎 tick ラムダを作らずに済むよう、この tick のぶら下がる位置を持たせて使い回す
        private Func<VaultPath, bool> _isClimbClear;
        private Vector3 _climbFrom;

        public CharacterLedgeSensor LedgeSensor => _ledgeSensor ??= new CharacterLedgeSensor(Body);

        // ふちを掴んだ・離したなどの行は、予測のやり直しでも呼ばれるシミュレーションでは出さず、呼び手がこれと LedgeHangLog.ForStep で出す。
        public LedgeHangResult LastLedgeHang { get; private set; }

        public CharacterParkourState LastLedgeHangBefore { get; private set; }

        // 掴めないとき (切ってある・接地・上がっている・しゃがんでいる・離したばかり) はふちを調べない (使わないゲームと地上の tick に Raycast を払わせない)。
        // 立っていない・カバー・倒れている (allowed が false) ときはしゃがみと同じく掴まず、掴んでいれば離す。登っている途中だけは止めずに上がり切らせる。
        private bool TryStepLedgeHang(
            LedgeHangTuning tuning,
            CharacterMotorState state,
            Vector2 move,
            float yaw,
            bool jump,
            bool crouch,
            bool allowed,
            ref CharacterParkourState parkour,
            out Vector3 velocity)
        {
            velocity = Vector3.Zero;
            var release = crouch || !allowed;
            var before = RecoverLedge(tuning, parkour, yaw);
            var position = Body.Position;
            var ledge = Measure(tuning, before, position, state.Velocity, yaw, state.IsGrounded, release);

            _climbFrom = ledge.IsFound ? LedgeHangStep.HangPosition(ledge.Point, ledge.Normal, Body.Geometry) : position;
            _isClimbClear ??= path => LedgeSensor.IsClimbClear(_climbFrom, path);
            var result = LedgeHangStep.Step(
                before, ledge, position, state.Velocity, move, jump, release, state.IsGrounded, Body.Geometry, tuning, TickSeconds, _isClimbClear);
            parkour = result.State;
            if (result.Motion != LedgeHangMotion.None)
            {
                LastLedgeHangBefore = before;
                LastLedgeHang = result;
            }

            if (!result.HoldsBody)
            {
                return false;
            }

            // 乗り越えと同じく当たりを通して動かし、掴んだ tick に遠くから寄せても壁や上端にめり込ませない
            Body.Move(result.Position - Body.Position);
            velocity = result.Velocity;
            return true;
        }

        // スナップショットから戻した状態は動きの種類だけで、ふちの位置と壁の法線を持たない。目の前のふちを測り直して掴まり続け、見つからなければ離した扱いにする。
        // 登っている途中は軌跡の始まりが分からないので、続けずに何もしていない状態からやり直す。
        private CharacterParkourState RecoverLedge(LedgeHangTuning tuning, CharacterParkourState parkour, float yaw)
        {
            if (!parkour.IsHangingOnLedge || parkour.WallNormal != Vector3.Zero)
            {
                return parkour;
            }

            if (!tuning.Enabled || parkour.Move == ParkourMove.LedgeClimbUp)
            {
                return CharacterParkourState.None;
            }

            var found = LedgeSensor.Probe(Body.Position, yaw, tuning.GrabReach);
            return found.IsFound
                ? new CharacterParkourState(ParkourMove.LedgeHang, parkour.Elapsed, found.Normal, found.Point)
                : CharacterParkourState.None;
        }

        private LedgeContact Measure(
            LedgeHangTuning tuning, CharacterParkourState parkour, Vector3 position, Vector3 velocity, float yaw, bool grounded, bool release)
        {
            if (!tuning.Enabled || grounded)
            {
                return LedgeContact.None;
            }

            switch (parkour.Move)
            {
                case ParkourMove.LedgeHang:
                case ParkourMove.LedgeShimmy:
                    return release ? LedgeContact.None : LedgeSensor.Follow(parkour.LedgePoint, parkour.WallNormal);
                case ParkourMove.None:
                case ParkourMove.WallKick:
                    var waiting = LedgeHangStep.IsWaitingToRegrab(parkour) && parkour.Elapsed + TickSeconds < tuning.RegrabSeconds;
                    return release || waiting || velocity.Y > 0f ? LedgeContact.None : LedgeSensor.Probe(position, yaw, tuning.GrabReach);
                default:
                    return LedgeContact.None;
            }
        }

        private void ClearLedgeHangReport()
        {
            LastLedgeHang = default;
            LastLedgeHangBefore = CharacterParkourState.None;
        }
    }
}

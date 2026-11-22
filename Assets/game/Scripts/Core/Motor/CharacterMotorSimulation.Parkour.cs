using System;
using System.Numerics;

namespace Shooter.Core
{
    // 壁蹴り・受け身・壁登り・ぶら下がりの入り口を 1 か所にまとめ、Step と Finish からはここだけを呼ぶ。
    // 動きごとの中身は CharacterMotorSimulation.<動き>.cs が持つ (動きを足すたびに Step を書き換えずに済むため)。
    public sealed partial class CharacterMotorSimulation
    {
        private ParkourTuning _parkour = ParkourTuning.Default;

        // この tick の頭で地面に立っていたか。Finish で着地した tick を見分けるため
        private bool _groundedBefore;

        // 予測とサーバーで同じ値を渡す。片方だけ壁蹴りやぶら下がりを入れると、動いたかと位置が毎 tick 食い違う。
        public ParkourTuning Parkour
        {
            get => _parkour;
            set => _parkour = value ?? throw new ArgumentNullException(nameof(value));
        }

        // ふつうの移動の道筋から、壁走りより先に呼ぶ。体を動かした tick は true を返し、壁走りとふつうの移動に回さない。
        // ぶら下がっている間に壁登りや壁蹴りを調べ直さないよう、続いている動き (転がる・掴まっている) を先に見る。
        private bool TryMoveParkour(
            CharacterMotorState state, Vector2 move, float yaw, bool jump, bool allowed, ref CharacterParkourState parkour, out Vector3 velocity)
        {
            if (TryMoveLandingRoll(_parkour.LandingRoll, state, ref parkour, out velocity))
            {
                return true;
            }

            if (TryStepLedgeHang(_parkour.LedgeHang, state, move, yaw, jump, allowed, ref parkour, out velocity))
            {
                return true;
            }

            if (TryStepWallClimb(_parkour.WallClimb, state, move, yaw, jump, allowed, ref parkour, out velocity))
            {
                return true;
            }

            return TryStepWallKick(_parkour.WallKick, state, move, yaw, jump, allowed, ref parkour, out velocity);
        }

        // どの道筋で終えた tick にも Finish から呼ぶ。着地した tick に、落下の傷みへ掛ける割合 (FallSpeedScale) を決めるため。
        private CharacterParkourState SettleParkour(CharacterParkourState parkour, CharacterMotorInput input, Vector3 velocity, bool grounded)
        {
            return StepLandingRoll(_parkour.LandingRoll, parkour, input, velocity, _groundedBefore, grounded);
        }
    }
}

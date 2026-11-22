using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 予測・サーバーの CharacterMotorSimulation.Parkour と同じく、壁蹴り・受け身・壁登り・ぶら下がりの入り口を 1 か所にまとめ、Tick と TryOverrideMove からはここだけを呼ぶ。
    // 動きごとの中身は PlayerController.<動き>.cs が持つ (動きを足すたびに Tick を書き換えずに済むため)。
    // 動きごとの入り口を部分メソッドで宣言だけ置くのは、中身を足すまで呼び出しごと消え、今までの動きに何も足さないため。
    public sealed partial class PlayerController
    {
        private CharacterParkourState _parkour;

        public CharacterParkourState Parkour => _parkour;

        // 受け身で転がった着地は、状態が持つ割合 (FallSpeedScale) だけ着地の速さを落とし、落下の傷みを減らす。サーバーの ServerMovement.LandingSpeed と同じ計算。
        public static float LandingSpeed(float verticalVelocity, CharacterParkourState parkour)
        {
            return Mathf.Max(0f, -verticalVelocity) * parkour.FallSpeedScale;
        }

        // 乗り越え・崖のぼりが始まらなかったときに、回避より先に呼ぶ。CharacterMotorSimulation.TryMoveParkour と同じく、掴まっている動きを先に見る。
        private void TryOverrideMoveForParkour(Vector2 move, bool jumpPressed, float deltaSeconds, ref bool overridden, ref Vector3 displacement)
        {
            TryOverrideMoveForLedgeHang(move, jumpPressed, deltaSeconds, ref overridden, ref displacement);
            if (!overridden)
            {
                TryOverrideMoveForWallClimb(move, jumpPressed, deltaSeconds, ref overridden, ref displacement);
            }

            if (!overridden)
            {
                TryOverrideMoveForWallKick(move, jumpPressed, deltaSeconds, ref overridden, ref displacement);
            }
        }

        // 着地の速さを読む ReportLanding より先に呼び、着地した tick の落下の割合を決める。
        private void FinishParkour(bool footing, bool crouch, float deltaSeconds)
        {
            StepLandingRoll(footing, crouch, deltaSeconds);
        }

        private void ResetParkour()
        {
            _parkour = CharacterParkourState.None;
        }
    }
}

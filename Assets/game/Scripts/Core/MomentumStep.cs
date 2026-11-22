using System;
using System.Numerics;

namespace Shooter.Core
{
    // 予測・サーバーの CharacterMotorSimulation とオフラインの SlideMovement・PlayerController で同じ答えを出すよう、勢いのつながりの計算をここに置く。
    public static class MomentumStep
    {
        // 滑りから跳んだときの水平の速さ。滑ってきた速さに SlideJumpBoost (m/s) を足す。
        public static float SlideJumpSpeed(float slideSpeed, MomentumTuning tuning)
        {
            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }

            if (!(slideSpeed >= 0f) || float.IsInfinity(slideSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(slideSpeed), slideSpeed, "slideSpeed must be finite and not negative");
            }

            return slideSpeed + tuning.SlideJumpBoost;
        }

        // たどり終えた乗り越えから持ち越す速度。越えた向きへ、軌跡をたどった速さに VaultExitSpeedKeep を掛けた水平の速さだけを残す。
        // 縦を 0 にするのは、降りる区間 (真下へ下りる) の速さを持ち越すと、着地で落下の傷みを受けたり跳ね返ったりするため。
        // 走ってきた速さでなく軌跡の速さにするのは、乗り越えの状態 (CharacterVaultState) に入る前の速さを持たず、巻き戻した tick からも同じ答えを出すため。
        public static Vector3 VaultExitVelocity(VaultPath path, MomentumTuning tuning)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }

            var across = new Vector3(path.End.X, 0f, path.End.Z);
            if (tuning.VaultExitSpeedKeep <= 0f || across.LengthSquared() < 1e-8f)
            {
                return Vector3.Zero;
            }

            return Vector3.Normalize(across) * (path.Speed * tuning.VaultExitSpeedKeep);
        }
    }
}

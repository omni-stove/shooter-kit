using System;

namespace Shooter.Core
{
    // 二丁持ちの手ごとのリロードの様子。手の組 (AkimboHands) を持たない体 (オンラインの自分) も、一人称の武器モデルへ同じ形で渡すために分ける。
    public readonly struct AkimboHandsView
    {
        private readonly bool _rightReloading;
        private readonly float _rightReloadProgress;
        private readonly bool _leftReloading;
        private readonly float _leftReloadProgress;

        public AkimboHandsView(bool rightReloading, float rightReloadProgress, bool leftReloading, float leftReloadProgress)
        {
            _rightReloading = rightReloading;
            _rightReloadProgress = Fraction(rightReloadProgress);
            _leftReloading = leftReloading;
            _leftReloadProgress = Fraction(leftReloadProgress);
        }

        public static AkimboHandsView Of(AkimboHands hands)
        {
            if (hands == null)
            {
                throw new ArgumentNullException(nameof(hands));
            }

            return new AkimboHandsView(
                hands.IsHandReloading(AkimboHand.Right),
                hands.ReloadProgress(AkimboHand.Right),
                hands.IsHandReloading(AkimboHand.Left),
                hands.ReloadProgress(AkimboHand.Left));
        }

        public bool IsHandReloading(AkimboHand hand)
        {
            switch (hand)
            {
                case AkimboHand.Right:
                    return _rightReloading;
                case AkimboHand.Left:
                    return _leftReloading;
                default:
                    throw new ArgumentOutOfRangeException(nameof(hand), hand, "hand is not a defined AkimboHand");
            }
        }

        public float ReloadProgress(AkimboHand hand)
        {
            switch (hand)
            {
                case AkimboHand.Right:
                    return _rightReloading ? _rightReloadProgress : 0f;
                case AkimboHand.Left:
                    return _leftReloading ? _leftReloadProgress : 0f;
                default:
                    throw new ArgumentOutOfRangeException(nameof(hand), hand, "hand is not a defined AkimboHand");
            }
        }

        private static float Fraction(float value)
        {
            return float.IsNaN(value) ? 0f : Math.Min(1f, Math.Max(0f, value));
        }
    }
}

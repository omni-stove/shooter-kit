using Shooter.Core;

namespace Shooter.Runtime
{
    // GameSignals.Holster は操作しているプレイヤー 1 人分しか持たないので、見られる側ごとに同じ値を読めるよう、プレイヤーに付く部品から出させる。
    public interface IHolsterStatusSource
    {
        HolsterStatus HolsterStatus { get; }
    }
}

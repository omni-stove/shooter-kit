using Shooter.Core;

namespace Shooter.Runtime
{
    // GameSignals.WeaponLight は操作しているプレイヤー 1 人分しか持たないので、ほかのプレイヤーや敵の光も同じ値で読めるよう、プレイヤーに付く部品から出させる。
    public interface IWeaponLightStatusSource
    {
        WeaponLightStatus WeaponLightStatus { get; }
    }
}

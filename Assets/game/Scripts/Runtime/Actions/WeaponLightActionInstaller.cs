using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 点けられるかは付けた物 (アタッチメント) にライトがあるかで決まるので、ルールでは切り替えず、撃つ武器を持つ体には付ける。
    public sealed class WeaponLightActionInstaller : IPlayerActionInstaller
    {
        // しまう動作 (HolsterActionInstaller) が付けた HolsterController を Initialize で拾うので、その後に呼ぶ。
        public const int InstallOrder = HolsterActionInstaller.InstallOrder + 20;

        public int Order => InstallOrder;

        public void Install(GameObject player, GameRules rules)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            // 移動と射撃が無い物 (標的など) には照らす武器が無いので付けない。
            var weapon = player.GetComponentInChildren<WeaponController>(true);
            if (player.GetComponent<PlayerController>() == null || weapon == null)
            {
                return;
            }

            var controller = PlayerActions.GetOrAdd<WeaponLightController>(player);
            controller.Initialize(weapon, player.GetComponent<Damageable>(), player.GetComponent<HolsterController>());
        }

        // 組み込みの並びに足さず自分で登録するのは、IPlayerActionInstaller の決まりどおり既存のファイルを変えずに足すため。
        // Edit Mode (テストやシーン生成) では Play の初期化が走らないので、エディターの読み込みでも登録する。
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            PlayerActions.Register(new WeaponLightActionInstaller());
        }
    }
}

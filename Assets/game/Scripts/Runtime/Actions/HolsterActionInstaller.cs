using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // しまう動作はどのゲームでも使えるので、ルールでは切り替えず、撃つ武器を持つ体には付ける。
    public sealed class HolsterActionInstaller : IPlayerActionInstaller
    {
        // ほかのアクションのコンポーネントは読まないので、組み込みの後に呼ぶ。
        public const int InstallOrder = 540;

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

            // 移動と射撃が無い物 (標的など) にはしまう武器が無いので付けない。
            var weapon = player.GetComponentInChildren<WeaponController>(true);
            if (player.GetComponent<PlayerController>() == null || weapon == null)
            {
                return;
            }

            // 実行時に付けるので Inspector の値は既定のままになる。ゲームの値は調整値の JSON から渡す。
            var controller = PlayerActions.GetOrAdd<HolsterController>(player);
            controller.Configure(KitTuning.Holster);
            controller.Initialize(weapon, player.GetComponent<Damageable>());
        }

        // 組み込みの並びに足さず自分で登録するのは、IPlayerActionInstaller の決まりどおり既存のファイルを変えずに足すため。
        // Edit Mode (テストやシーン生成) では Play の初期化が走らないので、エディターの読み込みでも登録する。
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            PlayerActions.Register(new HolsterActionInstaller());
        }
    }
}

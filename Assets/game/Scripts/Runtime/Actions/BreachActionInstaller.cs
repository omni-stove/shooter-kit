using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 突入はドアの置かれたシーンでしか起きないので、ルールでは切り替えず、移動と体力のあるプレイヤーには付ける。
    public sealed class BreachActionInstaller : IPlayerActionInstaller
    {
        // 怯み (StaggerActionInstaller) と武器のしまい (HolsterActionInstaller) の部品を Configure で拾うので、その後に呼ぶ。
        public const int InstallOrder = 550;

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

            // 標的など移動しない物はドアを破らないので付けない。
            if (player.GetComponent<PlayerController>() == null || player.GetComponent<Damageable>() == null)
            {
                return;
            }

            var controller = PlayerActions.GetOrAdd<BreachController>(player);
            controller.Configure(rules.Actions.SlowMotion);
            Debug.Log(BreachLog.Loaded(KitTuning.Breach), controller);
        }

        // 組み込みの並びに足さず自分で登録するのは、IPlayerActionInstaller の決まりどおり既存のファイルを変えずに足すため。
        // Edit Mode (テストやシーン生成) では Play の初期化が走らないので、エディターの読み込みでも登録する。
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            PlayerActions.Register(new BreachActionInstaller());
        }
    }
}

using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 場面を作るスクリプトやプレハブで EnemyBarker を付けないのは、波で後から出る敵や、ゲームが差し替えた敵のプレハブにも付け忘れなく付けるため。
    public static class EnemyBarkInstaller
    {
        public static bool IsInstalled { get; private set; }

        // サーバーの判定を引数で受けるのは、Edit Mode のテストが専用サーバーのときに付けないことを確かめられるようにするため。
        public static void Install(bool isDedicatedServer)
        {
            Uninstall();
            if (NetStartup.ShouldDisablePresentation(isDedicatedServer))
            {
                return;
            }

            EnemyAgent.Initialized += OnEnemyInitialized;
            IsInstalled = true;

            // 購読より前に初期化を終えた敵 (Play の途中で付け直したとき) にも付ける。
            foreach (var agent in Object.FindObjectsByType<EnemyAgent>(FindObjectsInactive.Include))
            {
                if (agent.IsInitialized)
                {
                    AttachTo(agent);
                }
            }
        }

        // もう付いている EnemyBarker は外さない。外すのは、これから出る敵に付けないことだけにする。
        public static void Uninstall()
        {
            EnemyAgent.Initialized -= OnEnemyInitialized;
            IsInstalled = false;
        }

        public static EnemyBarker AttachTo(EnemyAgent agent)
        {
            if (agent == null)
            {
                return null;
            }

            return PlayerActions.GetOrAdd<EnemyBarker>(agent.gameObject);
        }

        private static void OnEnemyInitialized(EnemyAgent agent)
        {
            AttachTo(agent);
        }

        // ドメインの再読み込みを切った Play でも、前の Play の購読を持ち越さない。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Uninstall();
        }

        // SubsystemRegistration で購読しないのは、同じ段で EnemyAgent が Initialized の購読を外すので、どちらが先に走るかで購読が消えるため。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallOnPlay()
        {
            Install(NetStartup.IsDedicatedServerBuild);
        }
    }
}

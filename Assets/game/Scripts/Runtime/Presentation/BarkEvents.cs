using System;

namespace Shooter.Runtime
{
    // CombatEvents に置かないのは、掛け声を使わないゲームの戦闘の出来事に掛け声の型を残さないため。
    public static class BarkEvents
    {
        // 静的な初期化で 1 度だけ登録するのは、ドメインの再読み込みを切った Play でも CombatEvents.ClearSubscribers がここの購読を外すようにするため。
        static BarkEvents()
        {
            CombatEvents.RegisterClear(ClearSubscribers);
        }

        public static event Action<BarkCue> Spoken;

        public static void Raise(BarkCue cue)
        {
            CombatEvents.Notify(Spoken, cue);
        }

        private static void ClearSubscribers()
        {
            Spoken = null;
        }
    }
}

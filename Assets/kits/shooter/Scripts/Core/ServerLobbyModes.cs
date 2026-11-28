using System;
using GameKit.Core;

namespace Shooter.Core
{
    // ロビーのモードのキーを ServerLobby (GameKit.Core) に置かないのは、キーがシューターの目的 (MatchObjective) ごとに決まり、core はモードの型を知らないため。
    public static class ServerLobbyModes
    {
        // 目的の無いチームの倒し合い (MatchObjective.None) のキー。目的を載せる前からこの値だったので、古いクライアントの一覧でも同じに見える。
        public const string TeamDeathmatchMode = ServerLobby.DefaultMode;

        // キーを列挙の名前から作らないのは、列挙の名前を替えても一覧の絞り込みと表示の鍵 (serverBrowser.mode.<key>) が変わらないようにするため。
        public static string ModeName(MatchObjective objective)
        {
            switch (objective)
            {
                case MatchObjective.None:
                    return TeamDeathmatchMode;
                case MatchObjective.Domination:
                    return "domination";
                case MatchObjective.Elimination:
                    return "elimination";
                case MatchObjective.CaptureTheFlag:
                    return "capture-the-flag";
                case MatchObjective.Hardpoint:
                    return "hardpoint";
                case MatchObjective.SearchAndDestroy:
                    return "search-and-destroy";
                case MatchObjective.FreeForAll:
                    return "free-for-all";
                case MatchObjective.KillConfirmed:
                    return "kill-confirmed";
                case MatchObjective.GunGame:
                    return "gun-game";
                case MatchObjective.Payload:
                    return "payload";
                case MatchObjective.BattleRoyale:
                    return "battle-royale";
                case MatchObjective.Extraction:
                    return "extraction";
                case MatchObjective.Horde:
                    return "horde";
                case MatchObjective.Infected:
                    return "infected";
                case MatchObjective.Guardian:
                    return "guardian";
                case MatchObjective.Execution:
                    return "execution";
                case MatchObjective.Juggernaut:
                    return "juggernaut";
                case MatchObjective.Vip:
                    return "vip";
                default:
                    throw new ArgumentOutOfRangeException(nameof(objective), objective, "objective is not a defined MatchObjective");
            }
        }
    }
}

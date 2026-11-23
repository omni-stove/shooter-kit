using System.Globalization;
using Shooter.Core;

namespace Shooter.NetV2
{
    // オフラインのドア (BreachLog) と同じ [Breach] で、オンラインの行だけを net で見分けられるようにする
    public static class ClientDoorsLog
    {
        public const string Tag = BreachLog.Tag;

        public static string Entered(string scene, int sceneDoors, int collisionDoors, int stateDoors)
        {
            return Tag + " net doors scene=" + Name(scene)
                + " sceneDoors=" + Count(sceneDoors)
                + " collisionDoors=" + Count(collisionDoors)
                + " stateDoors=" + Count(stateDoors);
        }

        public static string Received(int doors, bool shown)
        {
            return Tag + " net state doors=" + Count(doors) + " shown=" + (shown ? "yes" : "later");
        }

        public static string Changed(int door, BreachDoorPhase from, BreachDoorPhase to, bool live)
        {
            return Tag + " net door=" + Count(door) + " phase=" + from + "->" + to + " live=" + (live ? "yes" : "no");
        }

        public static string CollisionChanged(int changed, int open)
        {
            return Tag + " net collision changed=" + Count(changed) + " open=" + Count(open);
        }

        public static string CountMismatch(int stateDoors, int collisionDoors)
        {
            return Tag + " net the server sent " + Count(stateDoors) + " doors but the map collision has "
                + Count(collisionDoors) + "; export the map again with make export-server-data";
        }

        public static string NoSceneDoor(int door, int sceneDoors)
        {
            return Tag + " net the scene has no door " + Count(door) + " (sceneDoors=" + Count(sceneDoors) + ")";
        }

        public static string Requested(int door, BreachMethod method, bool sent)
        {
            return Tag + " net request door=" + Count(door) + " method=" + method + " sent=" + (sent ? "yes" : "no");
        }

        public static string NoInput(string actionPath)
        {
            return Tag + " net '" + Name(actionPath) + "' is not in the Input Actions; doors cannot be breached (Project Settings > Input System Package)";
        }

        private static string Count(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string Name(string value)
        {
            return string.IsNullOrEmpty(value) ? "-" : value;
        }
    }
}

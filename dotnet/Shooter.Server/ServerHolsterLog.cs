using System.Globalization;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // タグはオフラインと同じ HolsterLog.Tag にそろえ、同じ grep でオフラインとサーバーの行を拾えるようにする
    internal static class ServerHolsterLog
    {
        public static string Started()
        {
            return HolsterLog.Tag + " server ready";
        }

        public static string Began(int playerId, HolsterPhase phase, float seconds, string weaponId)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} server began player={1} phase={2} seconds={3:0.00} weapon={4}",
                HolsterLog.Tag, playerId, phase, seconds, Id(weaponId));
        }

        public static string Settled(int playerId, HolsterPhase phase)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} server settled player={1} phase={2}", HolsterLog.Tag, playerId, phase);
        }

        public static string Reset(int playerId, ServerHolster.ResetReason reason)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} server reset player={1} to=Drawn reason={2}", HolsterLog.Tag, playerId, reason);
        }

        public static string Refused(int playerId, HolsterAction action, ServerHolster.Refusal reason)
        {
            return string.Format(
                CultureInfo.InvariantCulture, "{0} server refused player={1} action={2} reason={3}", HolsterLog.Tag, playerId, action, reason);
        }

        private static string Id(string id)
        {
            return string.IsNullOrEmpty(id) ? "-" : id;
        }
    }
}

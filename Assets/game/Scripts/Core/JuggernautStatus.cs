using System;

namespace Shooter.Core
{
    // ModeStatus は出した後も UI が持つので、Juggernaut を指さずに出した時点の値を写して持つ。
    public sealed class JuggernautStatus
    {
        public JuggernautStatus(int? juggernautId, float secondsUntilChosen, bool? isLocalPlayerJuggernaut)
        {
            if (!(secondsUntilChosen >= 0f) || float.IsInfinity(secondsUntilChosen))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(secondsUntilChosen), secondsUntilChosen, "secondsUntilChosen must not be negative and must be finite");
            }

            JuggernautId = juggernautId;
            SecondsUntilChosen = juggernautId.HasValue ? 0f : secondsUntilChosen;
            IsLocalPlayerJuggernaut = isLocalPlayerJuggernaut;
        }

        public int? JuggernautId { get; }

        public float SecondsUntilChosen { get; }

        // 操作しているプレイヤーがいない (bot だけの) ときは null。
        public bool? IsLocalPlayerJuggernaut { get; }

        public static JuggernautStatus Of(Juggernaut juggernaut, int? localPlayerId)
        {
            if (juggernaut == null)
            {
                throw new ArgumentNullException(nameof(juggernaut));
            }

            bool? local = null;
            if (localPlayerId.HasValue && juggernaut.Session.HasPlayer(localPlayerId.Value))
            {
                local = juggernaut.IsJuggernaut(localPlayerId.Value);
            }

            return new JuggernautStatus(juggernaut.JuggernautId, juggernaut.SecondsUntilChosen, local);
        }
    }
}

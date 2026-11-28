namespace GameKit.Core
{
    public readonly struct HeldPlayer
    {
        public HeldPlayer(int playerId, Team team, int kills, int deaths, float remainingSeconds)
            : this(playerId, team, kills, deaths, 0, remainingSeconds)
        {
        }

        public HeldPlayer(int playerId, Team team, int kills, int deaths, int assists, float remainingSeconds)
        {
            PlayerId = playerId;
            Team = team;
            Kills = kills;
            Deaths = deaths;
            Assists = assists;
            RemainingSeconds = remainingSeconds;
        }

        public int PlayerId { get; }

        public Team Team { get; }

        public int Kills { get; }

        public int Deaths { get; }

        public int Assists { get; }

        public float RemainingSeconds { get; }

        // internal にしないのは、GameKit.Core は内部をキットのアセンブリに見せないので、抜けた人を預かるキットのセッション (Shooter.Core.TeamDeathmatchSession など) から呼べなくなるため
        public HeldPlayer WithRemaining(float remainingSeconds) => new HeldPlayer(PlayerId, Team, Kills, Deaths, Assists, remainingSeconds);

        public HeldPlayer WithoutStats() => new HeldPlayer(PlayerId, Team, 0, 0, 0, RemainingSeconds);
    }
}

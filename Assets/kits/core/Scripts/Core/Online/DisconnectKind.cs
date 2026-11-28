namespace GameKit.Core
{
    public enum DisconnectKind
    {
        ServerStopped,

        TimedOut,

        Rejected,

        VersionMismatch,

        IdleKicked,

        VoteKicked,
    }
}

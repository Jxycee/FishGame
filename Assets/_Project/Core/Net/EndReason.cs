namespace FishGame.Core
{
    // Why a connection attempt or session ended. Rejections send ToString() of this value as the NGO disconnect reason.
    public enum EndReason : byte
    {
        UserLeft = 0, HostLeft = 1, Kicked = 2, Timeout = 3, FullLobby = 4, JoinsClosed = 5,
        VersionMismatch = 6, InvalidCode = 7, ServicesUnavailable = 8, TransportFailure = 9,
        Unknown = 10, DuplicateIdentity = 11
    }
}

namespace FishGame.Core
{
    // Maps an EndReason to its UI string key (spec 11.2 table). T14's UiStrings holds the texts.
    // SceneFlow (T13) publishes a toast only when the key is not null.
    public static class EndReasonText
    {
        public static string ToastKey(EndReason reason)
        {
            switch (reason)
            {
                case EndReason.UserLeft: return null;
                case EndReason.HostLeft: return "err.host_left";
                case EndReason.Kicked: return "err.rejected";
                case EndReason.DuplicateIdentity: return "err.rejected";
                case EndReason.Timeout: return "err.timeout";
                case EndReason.FullLobby: return "err.session_full";
                case EndReason.JoinsClosed: return "err.joins_closed";
                case EndReason.VersionMismatch: return "err.version";
                case EndReason.InvalidCode: return "err.code_not_found";
                case EndReason.ServicesUnavailable: return "err.services";
                case EndReason.TransportFailure: return "err.network";
                default: return "err.generic"; // Unknown and any undefined value
            }
        }
    }
}

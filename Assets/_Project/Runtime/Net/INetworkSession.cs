using System;
using System.Threading;
using System.Threading.Tasks;
using FishGame.Core;

namespace FishGame.Runtime
{
    // Authority: none (interface). Menus and App talk only to this seam. Start tasks complete when State reaches Connected.
    // On failure they fault with NetworkSessionException(reason) and Ended(reason) fires once. A cancel ends with UserLeft.
    public interface INetworkSession : IDisposable
    {
        ConnState State { get; }
        SessionMode Mode { get; }                 // None while Offline
        bool IsHost { get; }
        event Action<ConnState, ConnState> StateChanged;
        event Action<EndReason> Ended;            // exactly once per attempt (Begin .. Offline); UserLeft for a normal leave or a cancel
        string JoinCode { get; }                  // null for solo and dev-direct
        Task<string> HostOnlineAsync(CancellationToken ct);
        Task JoinOnlineAsync(string code, CancellationToken ct);
        Task StartLocalAsync(bool allowRemote, CancellationToken ct); // false = Solo; true = DevDirect host (dev builds only)
        Task JoinDirectAsync(string ip, ushort port, CancellationToken ct);
        Task SetJoinableAsync(bool open);
        Task LeaveAsync();                        // idempotent; completes when Offline
    }
}

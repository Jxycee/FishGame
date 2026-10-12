using System;
using System.Threading;
using System.Threading.Tasks;
using FishGame.Core;
using FishGame.Runtime;

namespace FishGame.Networking
{
    // Authority: none (router). The single INetworkSession the UI and App see (D7). Each attempt routes to Local or to a
    // lazily created online backend. Solo and dev-direct never construct or touch the online backend (no UGS).
    public sealed class CompositeNetworkSession : INetworkSession
    {
        private readonly Func<INetworkSession> _onlineFactory;
        private INetworkSession _online;
        private INetworkSession _active;
        private bool _disposed;

        public CompositeNetworkSession(LocalNetworkSession local, Func<INetworkSession> onlineFactory)
        {
            Local = local ?? throw new ArgumentNullException(nameof(local));
            _onlineFactory = onlineFactory ?? throw new ArgumentNullException(nameof(onlineFactory));
            Local.StateChanged += OnBackendStateChanged;
            Local.Ended += OnBackendEnded;
        }

        public LocalNetworkSession Local { get; }

        // Backend of the current or last attempt; null before the first.
        public INetworkSession Active => _active;

        private INetworkSession Current => _active ?? Local;

        public ConnState State => Current.State;
        public SessionMode Mode => Current.Mode;
        public bool IsHost => Current.IsHost;
        public string JoinCode => Current.JoinCode;

        public event Action<ConnState, ConnState> StateChanged;
        public event Action<EndReason> Ended;

        public Task<string> HostOnlineAsync(CancellationToken ct) => Begin(true).HostOnlineAsync(ct);

        public Task JoinOnlineAsync(string code, CancellationToken ct) => Begin(true).JoinOnlineAsync(code, ct);

        public Task StartLocalAsync(bool allowRemote, CancellationToken ct) => Begin(false).StartLocalAsync(allowRemote, ct);

        public Task JoinDirectAsync(string ip, ushort port, CancellationToken ct) => Begin(false).JoinDirectAsync(ip, port, ct);

        public Task SetJoinableAsync(bool open) => Current.SetJoinableAsync(open);

        public Task LeaveAsync() => Current.LeaveAsync();

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            Local.StateChanged -= OnBackendStateChanged;
            Local.Ended -= OnBackendEnded;
            Local.Dispose();
            if (_online != null)
            {
                _online.StateChanged -= OnBackendStateChanged;
                _online.Ended -= OnBackendEnded;
                _online.Dispose();
            }
        }

        // Starting while any backend is not Offline throws InvalidOperationException (synchronously).
        private INetworkSession Begin(bool online)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(CompositeNetworkSession));
            }
            if (Local.State != ConnState.Offline || (_online != null && _online.State != ConnState.Offline))
            {
                throw new InvalidOperationException("A session is already active (" + Current.State + ").");
            }
            INetworkSession target = online ? GetOrCreateOnline() : Local;
            _active = target;
            return target;
        }

        private INetworkSession GetOrCreateOnline()
        {
            if (_online != null)
            {
                return _online;
            }
            INetworkSession created = _onlineFactory();
            if (created == null)
            {
                throw new InvalidOperationException("The online session factory returned null.");
            }
            _online = created;
            created.StateChanged += OnBackendStateChanged;
            created.Ended += OnBackendEnded;
            return created;
        }

        // Only one backend can be outside Offline at a time (Begin enforces it), so forwarding from either backend
        // is the same as forwarding from the active one, and an Ended raised just after a backend switch is never lost.
        private void OnBackendStateChanged(ConnState from, ConnState to)
        {
            EventIsolation.Invoke(StateChanged, from, to);
        }

        private void OnBackendEnded(EndReason reason)
        {
            EventIsolation.Invoke(Ended, reason);
        }
    }
}

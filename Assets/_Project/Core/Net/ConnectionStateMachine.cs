using System;

namespace FishGame.Core
{
    // Connection state machine (spec 6.4, contract 7.3 and D9). Pure logic: no Unity or NGO references.
    // Exactly 8 legal transitions; every other (state, event) pair throws InvalidTransitionException.
    public sealed class ConnectionStateMachine
    {
        public ConnState State { get; private set; } = ConnState.Offline;

        // Reset to Unknown on Begin; set to the supplied reason on Fail / UnexpectedStop; otherwise unchanged.
        public EndReason FailureReason { get; private set; } = EndReason.Unknown;

        // (from, to), raised after State is updated.
        public event Action<ConnState, ConnState> Changed;

        public void Fire(ConnEvent ev, EndReason reason = EndReason.Unknown)
        {
            ConnState from = State;
            ConnState to = Next(from, ev);
            Apply(from, to, ev, reason);
        }

        public bool TryFire(ConnEvent ev, EndReason reason = EndReason.Unknown)
        {
            ConnState from = State;
            if (!TryNext(from, ev, out ConnState to)) return false;
            Apply(from, to, ev, reason);
            return true;
        }

        public static ConnState Next(ConnState from, ConnEvent ev)
        {
            if (!TryNext(from, ev, out ConnState to)) throw new InvalidTransitionException(from, ev);
            return to;
        }

        public static bool TryNext(ConnState from, ConnEvent ev, out ConnState to)
        {
            to = from;
            switch (from)
            {
                case ConnState.Offline:
                    if (ev == ConnEvent.Begin) { to = ConnState.Connecting; return true; }
                    break;
                case ConnState.Connecting:
                    if (ev == ConnEvent.Established) { to = ConnState.Connected; return true; }
                    if (ev == ConnEvent.Fail) { to = ConnState.Failed; return true; }
                    break;
                case ConnState.Connected:
                    if (ev == ConnEvent.UnexpectedStop) { to = ConnState.Failed; return true; }
                    if (ev == ConnEvent.Leave) { to = ConnState.ShuttingDown; return true; }
                    break;
                case ConnState.Failed:
                    if (ev == ConnEvent.Leave) { to = ConnState.ShuttingDown; return true; }
                    if (ev == ConnEvent.ShutdownComplete) { to = ConnState.Offline; return true; }
                    break;
                case ConnState.ShuttingDown:
                    if (ev == ConnEvent.ShutdownComplete) { to = ConnState.Offline; return true; }
                    break;
            }
            return false;
        }

        private void Apply(ConnState from, ConnState to, ConnEvent ev, EndReason reason)
        {
            if (ev == ConnEvent.Begin)
            {
                FailureReason = EndReason.Unknown;
            }
            else if (ev == ConnEvent.Fail || ev == ConnEvent.UnexpectedStop)
            {
                FailureReason = reason;
            }
            State = to;
            Changed?.Invoke(from, to);
        }
    }
}

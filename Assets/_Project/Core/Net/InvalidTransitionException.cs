using System;

namespace FishGame.Core
{
    // Thrown by ConnectionStateMachine for any (state, event) pair outside the 8 legal transitions.
    public sealed class InvalidTransitionException : InvalidOperationException
    {
        public ConnState From { get; }
        public ConnEvent Event { get; }

        public InvalidTransitionException(ConnState from, ConnEvent ev)
            : base("Invalid connection transition: " + from + " --" + ev + "-->")
        {
            From = from;
            Event = ev;
        }
    }
}

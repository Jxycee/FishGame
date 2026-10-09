using System;
using FishGame.Core;

namespace FishGame.Runtime
{
    // Authority: none (exception type). Start tasks fault with it. The UI shows toasts from Ended only, never from this exception (D8).
    public sealed class NetworkSessionException : Exception
    {
        public EndReason Reason { get; }

        public NetworkSessionException(EndReason reason, string message = null, Exception inner = null)
            : base(message ?? ("Network session failed: " + reason), inner)
        {
            Reason = reason;
        }
    }
}

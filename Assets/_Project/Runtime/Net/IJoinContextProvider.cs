using System.Collections.Generic;
using FishGame.Core;

namespace FishGame.Runtime
{
    // Authority: host. Implemented by SessionState (T10). Read by ConnectionApprover on the host only.
    public interface IJoinContextProvider
    {
        bool JoinsOpen { get; }
        void GetRoster(List<RosterEntry> into);   // clears `into`, then adds every slot (connected or not)
        bool TryGetKey(ulong clientId, out PlayerKey key);
    }
}

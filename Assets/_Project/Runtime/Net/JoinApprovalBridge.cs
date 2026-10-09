using System.Collections.Generic;

namespace FishGame.Runtime
{
    // Authority: host. App-scope handoff from the approver (Networking) to SessionState (Gameplay). Main thread only.
    // Pending entries count as connected for the approver's capacity and duplicate checks (D4).
    public sealed class JoinApprovalBridge
    {
        private readonly Dictionary<ulong, ApprovedJoin> _pending = new Dictionary<ulong, ApprovedJoin>();

        // SessionState sets this on server spawn and clears it on despawn.
        public IJoinContextProvider Provider { get; set; }

        // Replaces any pending entry for the same ClientId.
        public void AddApproved(in ApprovedJoin join)
        {
            _pending[join.ClientId] = join;
        }

        // Removes the entry when found.
        public bool TryTakeApproved(ulong clientId, out ApprovedJoin join)
        {
            if (_pending.TryGetValue(clientId, out join))
            {
                _pending.Remove(clientId);
                return true;
            }
            join = default;
            return false;
        }

        // Clears `into` first.
        public void GetPending(List<ApprovedJoin> into)
        {
            into.Clear();
            foreach (KeyValuePair<ulong, ApprovedJoin> entry in _pending)
            {
                into.Add(entry.Value);
            }
        }

        public void RemovePending(ulong clientId)
        {
            _pending.Remove(clientId);
        }

        // Pending only. Provider is untouched.
        public void Clear()
        {
            _pending.Clear();
        }
    }
}

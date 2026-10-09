using System.Collections.Generic;

namespace FishGame.Core
{
    // One connection approval request, decoded from the client's ConnectPayload by ConnectionApprover (T8).
    public readonly struct JoinRequest
    {
        public readonly ulong ClientId;
        public readonly bool IsServerSelf;
        public readonly int ProtocolVersion;
        public readonly ulong ContentHash;
        public readonly PlayerKey Key;
        public readonly string Name;

        public JoinRequest(ulong clientId, bool isServerSelf, int protocolVersion, ulong contentHash, PlayerKey key, string name)
        {
            ClientId = clientId;
            IsServerSelf = isServerSelf;
            ProtocolVersion = protocolVersion;
            ContentHash = contentHash;
            Key = key;
            Name = name;
        }
    }

    // One roster slot as seen by approval: connected players, disconnected (reclaimable) slots, and pending approvals (as connected).
    public readonly struct RosterEntry
    {
        public readonly PlayerKey Key;
        public readonly byte Slot;
        public readonly bool Connected;

        public RosterEntry(PlayerKey key, byte slot, bool connected)
        {
            Key = key;
            Slot = slot;
            Connected = connected;
        }
    }

    // Host-side facts that approval is evaluated against.
    public readonly struct JoinContext
    {
        public readonly SessionMode Mode;
        public readonly int ProtocolVersion;
        public readonly ulong ContentHash;
        public readonly bool JoinsOpen;
        public readonly int MaxPlayers;
        public readonly IReadOnlyList<RosterEntry> Roster;

        public JoinContext(SessionMode mode, int protocolVersion, ulong contentHash, bool joinsOpen, int maxPlayers, IReadOnlyList<RosterEntry> roster)
        {
            Mode = mode;
            ProtocolVersion = protocolVersion;
            ContentHash = contentHash;
            JoinsOpen = joinsOpen;
            MaxPlayers = maxPlayers;
            Roster = roster;
        }
    }

    public readonly struct JoinVerdict
    {
        public const byte NoSlot = 255;

        public readonly bool Approved;
        public readonly EndReason Reason;
        public readonly byte Slot;
        public readonly bool Reclaimed;

        private JoinVerdict(bool approved, EndReason reason, byte slot, bool reclaimed)
        {
            Approved = approved;
            Reason = reason;
            Slot = slot;
            Reclaimed = reclaimed;
        }

        // Reason is UserLeft and unused when approved.
        public static JoinVerdict Approve(byte slot, bool reclaimed) => new JoinVerdict(true, EndReason.UserLeft, slot, reclaimed);

        public static JoinVerdict Reject(EndReason reason) => new JoinVerdict(false, reason, NoSlot, false);
    }

    // Pure connection-approval rules (spec 6.6, contract 7.3). Host-only caller: ConnectionApprover (T8),
    // which rejects undecodable payloads and invalid keys before calling Evaluate.
    public static class JoinRules
    {
        // In order (spec 6.6):
        // 1 IsServerSelf -> Approve(ChooseSlot)               (skips 2-7; NGO auto-approves a declined host anyway)
        // 2 Mode == Solo -> Reject(JoinsClosed)
        // 3 ProtocolVersion differs -> Reject(VersionMismatch)
        // 4 ContentHash differs -> Reject(VersionMismatch)
        // 5 !JoinsOpen -> Reject(JoinsClosed)
        // 6 ConnectedCount(Roster) >= MaxPlayers -> Reject(FullLobby)
        // 7 a Connected entry has Key == request.Key -> Reject(DuplicateIdentity)
        // 8 slot = ChooseSlot; NoSlot -> Reject(FullLobby); else Approve(slot, reclaimed)
        public static JoinVerdict Evaluate(in JoinRequest request, in JoinContext context)
        {
            if (request.IsServerSelf)
            {
                byte hostSlot = ChooseSlot(request.Key, context.Roster, context.MaxPlayers, out bool hostReclaimed);
                return JoinVerdict.Approve(hostSlot, hostReclaimed);
            }
            if (context.Mode == SessionMode.Solo)
            {
                return JoinVerdict.Reject(EndReason.JoinsClosed);
            }
            if (request.ProtocolVersion != context.ProtocolVersion)
            {
                return JoinVerdict.Reject(EndReason.VersionMismatch);
            }
            if (request.ContentHash != context.ContentHash)
            {
                return JoinVerdict.Reject(EndReason.VersionMismatch);
            }
            if (!context.JoinsOpen)
            {
                return JoinVerdict.Reject(EndReason.JoinsClosed);
            }
            if (ConnectedCount(context.Roster) >= context.MaxPlayers)
            {
                return JoinVerdict.Reject(EndReason.FullLobby);
            }
            if (IsConnected(context.Roster, request.Key))
            {
                return JoinVerdict.Reject(EndReason.DuplicateIdentity);
            }

            byte slot = ChooseSlot(request.Key, context.Roster, context.MaxPlayers, out bool reclaimed);
            return slot == JoinVerdict.NoSlot
                ? JoinVerdict.Reject(EndReason.FullLobby)
                : JoinVerdict.Approve(slot, reclaimed);
        }

        public static int ConnectedCount(IReadOnlyList<RosterEntry> roster)
        {
            if (roster == null) return 0;
            int count = 0;
            for (int i = 0; i < roster.Count; i++)
            {
                if (roster[i].Connected) count++;
            }
            return count;
        }

        // a) a disconnected entry with the same Key -> its Slot, reclaimed = true
        // b) else the lowest slot in [0, maxPlayers) held by no entry (connected or not)
        // c) else the lowest Slot among disconnected entries (taken over), reclaimed = false
        // d) else JoinVerdict.NoSlot
        public static byte ChooseSlot(PlayerKey key, IReadOnlyList<RosterEntry> roster, int maxPlayers, out bool reclaimed)
        {
            reclaimed = false;
            if (roster == null) roster = new RosterEntry[0];

            for (int i = 0; i < roster.Count; i++)
            {
                RosterEntry e = roster[i];
                // A pending takeover can leave a disconnected row and a connected row on one slot; never reissue that slot.
                if (!e.Connected && e.Key == key && !SlotConnected(roster, e.Slot))
                {
                    reclaimed = true;
                    return e.Slot;
                }
            }

            for (int s = 0; s < maxPlayers && s < JoinVerdict.NoSlot; s++)
            {
                if (!SlotHeld(roster, (byte)s)) return (byte)s;
            }

            bool found = false;
            byte takeover = JoinVerdict.NoSlot;
            for (int i = 0; i < roster.Count; i++)
            {
                RosterEntry e = roster[i];
                if (e.Connected || SlotConnected(roster, e.Slot)) continue;
                if (!found || e.Slot < takeover)
                {
                    takeover = e.Slot;
                    found = true;
                }
            }
            return found ? takeover : JoinVerdict.NoSlot;
        }

        private static bool IsConnected(IReadOnlyList<RosterEntry> roster, PlayerKey key)
        {
            if (roster == null) return false;
            for (int i = 0; i < roster.Count; i++)
            {
                if (roster[i].Connected && roster[i].Key == key) return true;
            }
            return false;
        }

        private static bool SlotConnected(IReadOnlyList<RosterEntry> roster, byte slot)
        {
            for (int i = 0; i < roster.Count; i++)
            {
                if (roster[i].Connected && roster[i].Slot == slot) return true;
            }
            return false;
        }

        private static bool SlotHeld(IReadOnlyList<RosterEntry> roster, byte slot)
        {
            for (int i = 0; i < roster.Count; i++)
            {
                if (roster[i].Slot == slot) return true;
            }
            return false;
        }
    }
}

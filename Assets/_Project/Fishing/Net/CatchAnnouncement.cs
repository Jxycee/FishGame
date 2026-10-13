// Authority: none (local event payload built on every peer from CatchAnnouncedRpc). The host's decision is the truth; this is its echo.
using FishGame.Core;

namespace FishGame.Fishing
{
    public readonly struct CatchAnnouncement
    {
        public readonly byte CatcherSlot;
        public readonly CatchResult Catch;
        public readonly AnnounceKind Kind;
        public readonly ulong CatcherClientId;
        public readonly bool CleanBonus;   // from CommitDecision.CleanBonus (host-accepted claim); drives toast.clean and the reveal sparkle

        public CatchAnnouncement(byte catcherSlot, in CatchResult c, AnnounceKind kind, ulong catcherClientId, bool cleanBonus)
        {
            CatcherSlot = catcherSlot;
            Catch = c;
            Kind = kind;
            CatcherClientId = catcherClientId;
            CleanBonus = cleanBonus;
        }

        // The EditMode test seam for the wire decode (ruling E-Q2). Only bit RodWireQuant.AnnounceFlagClean of flags is read.
        internal static CatchAnnouncement FromWire(byte slot, in CatchResultNet c, byte kind, byte flags, ulong clientId)
        {
            return new CatchAnnouncement(slot, c.ToCore(), (AnnounceKind)kind, clientId, (flags & RodWireQuant.AnnounceFlagClean) != 0);
        }
    }
}

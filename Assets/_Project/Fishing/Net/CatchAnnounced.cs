// Authority: none (App EventBus message, raised locally on every peer by FishingRodNetwork when CatchAnnouncedRpc arrives).
// Kinds Landed, Lost, ReleasedCreelFull carry a catch; PityCast (R65) is toast-only and its catch is default.
namespace FishGame.Fishing
{
    public readonly struct CatchAnnounced
    {
        public readonly CatchAnnouncement Announcement;
        public readonly bool IsLocalCatcher;

        public CatchAnnounced(in CatchAnnouncement a, bool isLocalCatcher)
        {
            Announcement = a;
            IsLocalCatcher = isLocalCatcher;
        }
    }
}

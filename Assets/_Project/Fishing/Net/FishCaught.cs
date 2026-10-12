// Authority: host only. App EventBus message published by FishingHost after the creel add, iff Outcome == Landed && AddToCreel
// (never for ReleasedCreelFull). No consumer in M2 (M3 journal, M4 run stats).
using FishGame.Core;

namespace FishGame.Fishing
{
    public readonly struct FishCaught
    {
        public readonly byte Slot;
        public readonly CatchResult Catch;

        public FishCaught(byte slot, in CatchResult c)
        {
            Slot = slot;
            Catch = c;
        }
    }
}

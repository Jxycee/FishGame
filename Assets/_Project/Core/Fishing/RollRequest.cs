// Core/Fishing/RollRequest.cs: input of PlaceholderRoller.Roll (C 7.6). Plain data.
// Note: default(RollRequest) has ForcedItemIndex 0 (which would force species 0); callers set -1 for "off".
namespace FishGame.Core
{
    public struct RollRequest
    {
        public int BaitIndex;
        public int LoadedCount;
        public float Depth;
        public float Luck;
        public float ForcedBiteWait;    // > 0: BiteWait = this, no floor, no nibbles (debug)
        public int ForcedItemIndex;     // -1 off; >= 0 forces that species index (debug)
        public bool ForceJunk;          // debug
        public bool Twitchy;            // debug: HookWindow = 1.0 for a fish
    }
}

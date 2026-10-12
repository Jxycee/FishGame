// Core/Fishing/RodCueAudio.cs: RodCue -> SFX id (spec 9.11 sounds mapped onto 11.7 ids, C M2-D10). Core, pure.
// Pitch-follows-charge and loop handling belong to FishingFeedback (T23).
// The catch announce plays Catch_Flop + Catch_Cheer on every peer; that is not a RodCue.
namespace FishGame.Core
{
    public static class RodCueAudio
    {
        // null = no sound (None, Calm, and any value outside the enum). Dive returns the loop id; the consumer starts a loop.
        public static string SfxFor(RodCue cue)
        {
            switch (cue)
            {
                case RodCue.Equip:
                case RodCue.Stow:
                case RodCue.InvalidRelease:
                case RodCue.Recall:
                    return SfxIds.RodThunk;
                case RodCue.Ready:
                case RodCue.HookSet:
                    return SfxIds.ReelClick;
                case RodCue.ChargeStart:
                case RodCue.CastRelease:
                    return SfxIds.CastWhoosh;
                case RodCue.Landing: return SfxIds.BitePlop;
                case RodCue.Nibble: return SfxIds.NibbleTick;
                case RodCue.Bite: return SfxIds.SplashBig;
                case RodCue.SideRun:
                case RodCue.Exhausted:
                case RodCue.Escaped:
                    return SfxIds.SplashSmall;
                case RodCue.Dive: return SfxIds.DiveThrum;
                case RodCue.Snapped: return SfxIds.Snap;
                case RodCue.LineLanded: return SfxIds.CatchFlop;
                default: return null;
            }
        }

        // Bite and Snapped duck Ambience / Music (consumer: T23 calls AudioBus.Duck).
        public static bool IsStinger(RodCue cue)
        {
            return cue == RodCue.Bite || cue == RodCue.Snapped;
        }
    }
}

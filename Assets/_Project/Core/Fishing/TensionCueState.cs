// Core/Fishing/TensionCueState.cs: the cue values TensionCues derives from tension (spec 9.11, C 7.7). Plain data.
namespace FishGame.Core
{
    public struct TensionCueState
    {
        public TensionBand Band;
        public float RodBend01, ReelClickRate, CreakVolume01, CreakPitch, LineWarm01;
        public bool LineWarn, LineDash, Shake, CrackNow;
        public float RumbleLow, RumbleHigh, TraumaPerSecond;
    }
}

using FishGame.Core;
using UnityEngine;

namespace FishGame.Runtime
{
    // Authority: none. Client-side presentation audio handle for one looping cue (AudioBus.StartLoop). Only AudioBus creates loops.
    public sealed class AudioLoop
    {
        internal readonly AudioSource Source;
        internal readonly Transform Follow;        // null = a 2D loop that stays at the bus
        internal readonly AudioCategory Category;
        internal readonly float BaseVolume;        // recipe base volume; cueBase = BaseVolume * Volume01
        internal float Volume01 = 1f;
        internal bool Stopped;

        internal AudioLoop(string sfxId, AudioSource source, Transform follow, AudioCategory category, float baseVolume)
        {
            SfxId = sfxId;
            Source = source;
            Follow = follow;
            Category = category;
            BaseVolume = baseVolume;
        }

        public string SfxId { get; }

        public bool IsPlaying => !Stopped && (object)Source != null && Source.isPlaying;
    }
}

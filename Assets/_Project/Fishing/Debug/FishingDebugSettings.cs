// Authority: host side (the host reads these statics when it rolls a cast). Local dev toggles: F6 forced bite, F7 twitchy hook (T26),
// the PlayMode fixture sets ForcedSpeciesId. Honoured only when Debug.isDebugBuild (checked at runtime, never #if DEBUG).
// Reset on SubsystemRegistration so a disabled domain reload cannot leak a stale override.
using System.Collections.Generic;
using FishGame.Core;
using FishGame.Data;
using UnityEngine;

namespace FishGame.Fishing
{
    public static class FishingDebugSettings
    {
        public static float ForcedBiteWait { get; set; }    // <= 0 off (F6 toggles 0.1)
        public static bool TwitchyHook { get; set; }        // F7
        public static string ForcedSpeciesId { get; set; }  // null off (PlayMode fixture)
        public static bool ForceJunk { get; set; }

        private static readonly HashSet<string> s_warned = new HashSet<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ForcedBiteWait = 0f;
            TwitchyHook = false;
            ForcedSpeciesId = null;
            ForceJunk = false;
            s_warned.Clear();
        }

        // Starts from RollOverrides.None (ForcedItemIndex -1): a default(RollOverrides) would force species 0 on every cast (ruling B-Q14).
        public static RollOverrides ToOverrides(ContentRegistry registry)
        {
            if (!Debug.isDebugBuild) return RollOverrides.None;

            RollOverrides o = RollOverrides.None;
            o.ForcedBiteWait = Mathf.Max(0f, ForcedBiteWait);
            o.Twitchy = TwitchyHook;
            o.ForceJunk = ForceJunk;
            if (ForcedSpeciesId != null && registry != null)
            {
                if (registry.TryGetSpecies(ForcedSpeciesId, out _))
                {
                    o.ForcedItemIndex = registry.SpeciesIndex(ForcedSpeciesId);
                }
                else
                {
                    LogOnce("FishingDebugSettings: unknown ForcedSpeciesId '" + ForcedSpeciesId + "'; no species is forced.", ForcedSpeciesId);
                }
            }
            return o;
        }

        private static void LogOnce(string message, string key)
        {
            if (s_warned.Add(key)) Debug.LogWarning(message);
        }
    }
}

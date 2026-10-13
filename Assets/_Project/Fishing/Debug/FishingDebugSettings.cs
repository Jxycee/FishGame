// Authority: host side (the host reads these statics when it rolls a cast). Local dev toggles: F6 forced bite, F7 twitchy hook (T26),
// F10 cycles ForcedSpeciesId (the PlayMode fixture also sets it). Honoured only when Debug.isDebugBuild (checked at runtime, never #if DEBUG).
// Reset on SubsystemRegistration so a disabled domain reload cannot leak a stale override.
using System.Collections.Generic;
using FishGame.Core;
using FishGame.Data;
using UnityEngine;

namespace FishGame.Fishing
{
    public enum ForcedSpeciesKeyResult : byte { Changed, HostOnly, Ignored, NoSpecies }

    public static class FishingDebugSettings
    {
        public static float ForcedBiteWait { get; set; }    // <= 0 off (F6 toggles 0.1)
        public static bool TwitchyHook { get; set; }        // F7
        public static string ForcedSpeciesId { get; set; }  // null off (F10 cycles it; the PlayMode fixture sets it)
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

        // F10 (host only): the next id in the cycle None -> species 0 -> ... -> species N-1 -> None. An id that is not in the list
        // (stale, or never set) counts as None, so the cycle restarts at the first species. Pure: ids are the registry's species in
        // index order (the overlay builds the list), so EditMode tests need no content registry.
        public static string NextForcedSpecies(string current, IReadOnlyList<string> ids)
        {
            if (ids == null || ids.Count == 0) return null;
            if (string.IsNullOrEmpty(current)) return ids[0];
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] == current) return i + 1 < ids.Count ? ids[i + 1] : null;
            }
            return ids[0];
        }

        // The whole F10 rule, apart from the key read and the text: dev builds only (runtime check), nothing while the solo game is
        // paused, host only (a client's roll is not the one that counts), then one step of the cycle.
        public static ForcedSpeciesKeyResult CycleForcedSpecies(bool isHost, bool paused, IReadOnlyList<string> ids)
        {
            if (!Debug.isDebugBuild) return ForcedSpeciesKeyResult.Ignored;
            if (paused) return ForcedSpeciesKeyResult.Ignored;
            if (!isHost) return ForcedSpeciesKeyResult.HostOnly;
            if (ids == null || ids.Count == 0) return ForcedSpeciesKeyResult.NoSpecies;
            ForcedSpeciesId = NextForcedSpecies(ForcedSpeciesId, ids);
            return ForcedSpeciesKeyResult.Changed;
        }

        private static void LogOnce(string message, string key)
        {
            if (s_warned.Add(key)) Debug.LogWarning(message);
        }
    }
}

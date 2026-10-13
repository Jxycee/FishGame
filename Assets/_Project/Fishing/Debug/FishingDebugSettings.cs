// Authority: host side (the host reads these statics when it rolls a cast). Local dev toggles: F6 forced bite, F7 twitchy hook (T26),
// F10 cycles ForcedSpeciesId (the PlayMode fixture also sets it). Honoured only when Debug.isDebugBuild (checked at runtime, never #if DEBUG).
// F6/F7 and Luck reset when the session ends (INetworkSession.Ended, hooked by FishingHost.OnEnable through ClearOnSessionEnd) and on
// SubsystemRegistration, never when a Spot scene unloads (the M3 dev map switch keeps them). ForcedSpeciesId (cycled by F10 since the M2
// final review) and ForceJunk reset only on SubsystemRegistration, so a disabled domain reload cannot leak a stale override.
using System.Collections.Generic;
using FishGame.Core;
using FishGame.Data;
using FishGame.Runtime;
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

        // M3 dev luck (T15, T22 panel): size skew only, clamped to [-1, 1], NaN -> 0. The host reads it into the cast context in debug builds
        // only. A session toggle like F6/F7: reset when the session ends and on SubsystemRegistration, never on a Spot change.
        public static float Luck
        {
            get => s_luck;
            set => s_luck = float.IsNaN(value) ? 0f : Mathf.Clamp(value, -1f, 1f);
        }

        private static float s_luck;
        private static readonly HashSet<string> s_warned = new HashSet<string>();
        private static INetworkSession s_session;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ForcedBiteWait = 0f;
            TwitchyHook = false;
            ForcedSpeciesId = null;
            ForceJunk = false;
            s_luck = 0f;
            s_warned.Clear();
            if (s_session != null) s_session.Ended -= OnSessionEnded;
            s_session = null;
        }

        // F6/F7 are SESSION toggles: cleared when the session ends (INetworkSession.Ended, once per attempt), never when a Spot scene unloads,
        // so the M3 dev map switch keeps them (C 7.1, CCR-A1). Idempotent; re-hooks when the app's session instance changes (a new App in tests).
        // A null session only unhooks the current one (tests; FishingHost never passes null).
        internal static void ClearOnSessionEnd(INetworkSession session)
        {
            if (ReferenceEquals(s_session, session)) return;
            if (s_session != null) s_session.Ended -= OnSessionEnded;
            s_session = session;
            if (session != null) session.Ended += OnSessionEnded;
        }

        private static void OnSessionEnded(EndReason reason)
        {
            ForcedBiteWait = 0f;
            TwitchyHook = false;
            s_luck = 0f;
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

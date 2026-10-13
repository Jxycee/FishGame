// Runtime/World/SeasonalVisibility.cs: per-season renderer switching for the scenery (user decision 2026-10-13 "Full seasons", spec 10.2).
// Authority: presentation only, local (every peer); follows the season the palette was pushed for and never writes gameplay state.
// Lives in Runtime (not Presentation) so builders, the LookDevCapture tool and the EditMode tests can reach it; PaletteGlobals (Presentation)
// is the runtime writer of the season: every successful palette push calls SetSeason with the pushed season, so a later season change at
// runtime (M3 T18: PaletteGlobals re-applies on a real world-season change) reaches every tree through the same call.
// Each entry is a renderer and a season mask (SeasonalLook bits). Hiding uses Renderer.forceRenderingOff, which is NOT serialized: switching
// in edit mode (capture tool, builds, validators) never dirties or saves a scene in a seasonal state, and colliders are never touched (a
// canopy's SphereCollider sits on the same GameObject as its renderer). Event-driven: no Update, no per-frame work or allocation.
using System;
using FishGame.Core;
using UnityEngine;

namespace FishGame.Runtime
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class SeasonalVisibility : MonoBehaviour
    {
        [Serializable]
        public struct Entry
        {
            public Renderer renderer;
            public byte seasons;   // SeasonalLook mask bits

            public Entry(Renderer renderer, byte seasons)
            {
                this.renderer = renderer;
                this.seasons = seasons;
            }
        }

        // ArtPaletteDef's default m2Season (Summer): the season every scene shows until a palette push says otherwise.
        public const int DefaultSeason = SeasonalLook.Summer;

        private static event Action<int> SeasonChanged;

        // The season of the last palette push (PaletteGlobals, or LookDevCapture in the editor).
        public static int CurrentSeason { get; private set; } = DefaultSeason;

        [SerializeField] private Entry[] entries = new Entry[0];

        public int EntryCount => entries == null ? 0 : entries.Length;

        // Sets the shown season for every enabled SeasonalVisibility. A season outside 0..3 is ignored (the palette push failed); the same
        // season again raises nothing.
        public static void SetSeason(int season)
        {
            if (!SeasonalLook.IsSeason(season) || season == CurrentSeason) return;
            CurrentSeason = season;
            SeasonChanged?.Invoke(season);
        }

        // A disabled domain reload must not carry a season from the last play session into the next one.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            CurrentSeason = DefaultSeason;
        }

        // Builders and tests. Copies the entries and shows the current season at once.
        public void Configure(Entry[] newEntries)
        {
            entries = newEntries == null ? new Entry[0] : (Entry[])newEntries.Clone();
            Apply(CurrentSeason);
        }

        public Entry GetEntry(int index) => entries[index];

        private void OnEnable()
        {
            SeasonChanged += Apply;
            Apply(CurrentSeason);
        }

        private void OnDisable()
        {
            SeasonChanged -= Apply;
        }

        private void Apply(int season)
        {
            if (entries == null) return;
            for (int i = 0; i < entries.Length; i++)
            {
                Renderer r = entries[i].renderer;
                if (r != null) r.forceRenderingOff = !SeasonalLook.IsVisible(entries[i].seasons, season);
            }
        }
    }
}

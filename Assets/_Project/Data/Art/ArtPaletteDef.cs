using System;
using System.Collections.Generic;
using FishGame.Core;
using UnityEngine;

namespace FishGame.Data
{
    // Content data only (not networked). The 16-slot art palette per season, day-phase ambience, skin tone and UI colours (spec 10.2, 10.3).
    // Player slot tints are NOT stored here: GetSlotTint reads SlotPalette (M2-D3). Owned by T13.
    public sealed class ArtPaletteDef : ScriptableObject
    {
        public const int SlotCount = 16;
        public const int SeasonCount = 4;

        private static readonly Vector4 Magenta = new Vector4(1f, 0f, 1f, 1f);
        private static readonly string[] NoStrings = new string[0];
        private static readonly PaletteSeason[] NoSeasons = new PaletteSeason[0];
        private static readonly DayPhaseAmbient[] NoPhases = new DayPhaseAmbient[0];
        private static readonly FishIconTile[] NoTiles = new FishIconTile[0];

        [SerializeField] private string[] slotNames;
        [SerializeField] private PaletteSeason[] seasons;
        [SerializeField] private float faceJitter;
        [SerializeField] private Color skinTone;
        [SerializeField] private DayPhaseAmbient[] phases;
        [SerializeField] private Color uiAccent;
        [SerializeField] private Color uiGood;
        [SerializeField] private Color uiWarn;
        [SerializeField] private Color uiBad;
        [SerializeField] private Color uiPanel;
        [SerializeField] private Color uiText;
        [SerializeField] private int m2Season = 1;   // Summer; the only initialiser (C 7.13)
        [SerializeField] private SunMoonSettings sunMoon;           // M3 (T9): set by ConfigureM3
        [SerializeField] private WeatherAmbient[] weatherLooks;     // M3 (T9): index Clear 0, Rain 1, Fog 2

        [SerializeField] private Texture2D fishIconAtlas;           // M3 (T21): the baked journal icons (Editor/Dev/FishIconBaker); null until baked
        [SerializeField] private FishIconTile[] fishIconTiles;      // M3 (T21): one tile per species id

        public Texture2D FishIconAtlas => fishIconAtlas;
        public IReadOnlyList<FishIconTile> FishIconTiles => fishIconTiles ?? NoTiles;

        // The tile of a species in FishIconAtlas; false for a null id, an unknown id or while no atlas is assigned.
        public bool TryGetFishIconTile(string id, out FishIconTile tile)
        {
            tile = default;
            if (fishIconAtlas == null || fishIconTiles == null || id == null) return false;
            for (int i = 0; i < fishIconTiles.Length; i++)
            {
                if (string.Equals(fishIconTiles[i].id, id, StringComparison.Ordinal))
                {
                    tile = fishIconTiles[i];
                    return true;
                }
            }
            return false;
        }

        // Used by the icon baker. The tile array is copied (null stays null). Configure and ConfigureM3 leave both fields alone.
        public void ConfigureFishIcons(Texture2D atlas, FishIconTile[] tiles)
        {
            fishIconAtlas = atlas;
            fishIconTiles = tiles == null ? null : (FishIconTile[])tiles.Clone();
        }

        public SunMoonSettings SunMoon => sunMoon;
        public IReadOnlyList<WeatherAmbient> WeatherLooks => weatherLooks ?? new WeatherAmbient[0];

        public IReadOnlyList<string> SlotNames => slotNames ?? NoStrings;
        public IReadOnlyList<PaletteSeason> Seasons => seasons ?? NoSeasons;
        public float FaceJitter => faceJitter;
        public Color SkinTone => skinTone;
        public IReadOnlyList<DayPhaseAmbient> Phases => phases ?? NoPhases;
        public Color UiAccent => uiAccent;
        public Color UiGood => uiGood;
        public Color UiWarn => uiWarn;
        public Color UiBad => uiBad;
        public Color UiPanel => uiPanel;
        public Color UiText => uiText;
        public int M2Season => m2Season;

        // Used by the content builder and by tests. Collections are copied (seasons deeply); null reads back as empty.
        public void Configure(IEnumerable<string> slotNames, IEnumerable<PaletteSeason> seasons, float faceJitter, Color skinTone,
            IEnumerable<DayPhaseAmbient> phases, Color uiAccent, Color uiGood, Color uiWarn, Color uiBad, Color uiPanel, Color uiText,
            int m2Season)
        {
            this.slotNames = slotNames == null ? null : new List<string>(slotNames).ToArray();
            this.seasons = CopySeasons(seasons);
            this.faceJitter = faceJitter;
            this.skinTone = skinTone;
            this.phases = phases == null ? null : new List<DayPhaseAmbient>(phases).ToArray();
            this.uiAccent = uiAccent;
            this.uiGood = uiGood;
            this.uiWarn = uiWarn;
            this.uiBad = uiBad;
            this.uiPanel = uiPanel;
            this.uiText = uiText;
            this.m2Season = m2Season;
        }

        // M3 (T9): the sun/moon settings and the three weather looks. The array is copied (null stays null). The M2 Configure leaves both alone.
        public void ConfigureM3(in SunMoonSettings sunMoon, WeatherAmbient[] weatherLooks)
        {
            this.sunMoon = sunMoon;
            this.weatherLooks = weatherLooks == null ? null : (WeatherAmbient[])weatherLooks.Clone();
        }

        private static PaletteSeason[] CopySeasons(IEnumerable<PaletteSeason> source)
        {
            if (source == null) return null;
            var list = new List<PaletteSeason>();
            foreach (PaletteSeason s in source)
            {
                list.Add(s == null ? null : new PaletteSeason { name = s.name, colors = s.colors == null ? null : (Color[])s.colors.Clone() });
            }
            return list.ToArray();
        }

        // The shader global _Palette for one season: always exactly 16 entries (r, g, b, a). A missing season colour is magenta, so
        // a short authoring mistake is visible instead of shortening the array. Throws ArgumentOutOfRangeException when `season`
        // is outside 0..3 or there are fewer than season + 1 seasons.
        public Vector4[] PaletteVectors(int season)
        {
            PaletteSeason[] all = seasons ?? NoSeasons;
            if (season < 0 || season >= SeasonCount || season >= all.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(season), season,
                    "Season must be in 0..3 and the palette must define it (seasons: " + all.Length + ").");
            }

            PaletteSeason s = all[season];
            var result = new Vector4[SlotCount];
            for (int i = 0; i < SlotCount; i++)
            {
                if (s == null || s.colors == null || i >= s.colors.Length)
                {
                    result[i] = Magenta;
                    continue;
                }
                Color c = s.colors[i];
                result[i] = new Vector4(c.r, c.g, c.b, c.a);
            }
            return result;
        }

        // Player slot body tint from SlotPalette (no stored copy). Throws ArgumentOutOfRangeException for a slot outside 0..3.
        public Color GetSlotTint(int slot)
        {
            SlotPalette.GetRgb(slot, out float r, out float g, out float b);
            return new Color(r, g, b, 1f);
        }

        public Color GetSkinTone()
        {
            return skinTone;
        }
    }
}

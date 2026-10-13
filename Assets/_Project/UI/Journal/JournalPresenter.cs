using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using FishGame.Core;
using FishGame.Data;
using FishGame.Fishing;
using FishGame.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

namespace FishGame.UI
{
    // Authority: local only. Reads the local save, the replicated world (IWorldContext) and the local rod; writes nothing (C 2.8 #17).
    // The journal (spec 12.10): one card per journal species in species-id order, hint levels 0-3, the "biting now" badge, spot and
    // biting-now filters and the completion count. UiFlowController owns the modal seam (MenuOpen, Esc/B, screen changes); this class owns
    // the content. Cards are built once per registry and updated in place; the 1 Hz biting refresh only toggles badges (no allocation).
    internal sealed class JournalPresenter
    {
        private const float BiteRefreshSeconds = 1f;
        private static readonly Color GlyphInk = new Color(0.82f, 0.92f, 1f, 1f);
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly LogOnce StaticLog = new LogOnce();

        private static readonly string[] BandKeys =
        {
            "journal.band_dawn", "journal.band_morning", "journal.band_midday", "journal.band_afternoon", "journal.band_dusk", "journal.band_night",
        };
        private static readonly string[] SeasonKeys = { "journal.season_spring", "journal.season_summer", "journal.season_autumn", "journal.season_winter" };
        private static readonly string[] WeatherKeys = { "journal.weather_clear", "journal.weather_rain", "journal.weather_fog" };
        private static readonly string[] QualityKeys = { "journal.quality_normal", "journal.quality_good", "journal.quality_trophy" };

        private sealed class SpeciesCard
        {
            public ushort Index;
            public string Id;
            public FishSpeciesDef Def;
            public VisualElement Root, Icon, Info, Glyphs;
            public readonly List<Label> Lines = new List<Label>(4);
            public Label Name, Badge;
            public SeasonMask Seasons;
            public WeatherMask Weather;
            public TimeBandMask Bands;
            public string HintSpots = string.Empty;        // level 0: "Lake Dock, Harbour Pier"
            public bool[] InSpot;                          // by spot chip: does the chip's zone have a rule for this species
            public int Level = -1;
            public string NameText, InfoText;
            public bool Biting, Shown = true;
        }

        private sealed class SpotChip
        {
            public SpotDef Spot;
            public int ZoneIndex;
            public Button Button;
        }

        private readonly VisualElement _modal;
        private readonly Label _completion;
        private readonly Button _close, _filterAll;
        private readonly VisualElement _spotFilters, _grid, _progressFill;
        private readonly Toggle _filterBiting;
        private readonly ScrollView _scroll;
        private readonly Label _empty;
        private readonly ServiceRegistry _app;
        private readonly IInputService _input;
        private readonly Action _requestClose;

        private readonly List<SpeciesCard> _cards = new List<SpeciesCard>();
        private readonly List<SpotChip> _chips = new List<SpotChip>();
        private readonly List<string> _speciesIds = new List<string>();
        private readonly List<int> _zoneScratch = new List<int>();
        private readonly StringBuilder _sb = new StringBuilder(256);
        private readonly LogOnce _log = new LogOnce();

        private ContentRegistry _registry;
        private ContentRegistry _builtRegistry;             // the registry the cards were built for
        private EcologyDatabase _eco;
        private FishingResolver _resolver;
        private SaveService _save;
        private AudioBus _audio;
        private IWorldContext _world;
        private bool _builtForRegistry;

        private bool _open, _inGame, _disposed;
        private bool _bitingAvailable;
        private int _biteZone;
        private int _openedFrame = -1;
        private float _biteTimer;
        private int _filterSpot = -1;                      // -1 = All spots; else an index into _chips
        private bool _bitingOnly;

        private FishingRodController _rodCached;           // the local rod's network component, re-read when the controller changes
        private FishingRodNetwork _netCached;

        public JournalPresenter(VisualElement root, ServiceRegistry app, IInputService input, Action requestClose)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            _app = app;
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _requestClose = requestClose ?? throw new ArgumentNullException(nameof(requestClose));

            _modal = UiQuery.Req<VisualElement>(root, "JournalModal");
            UiQuery.Req<Label>(root, "JournalTitle").text = UiStrings.Get("journal.title");
            _completion = UiQuery.Req<Label>(root, "JournalCompletion");
            _close = UiQuery.Req<Button>(root, "JournalClose");
            _filterAll = UiQuery.Req<Button>(root, "JournalFilterAll");
            _spotFilters = UiQuery.Req<VisualElement>(root, "JournalSpotFilters");
            _filterBiting = UiQuery.Req<Toggle>(root, "JournalFilterBiting");
            _scroll = UiQuery.Req<ScrollView>(root, "JournalScroll");
            _grid = UiQuery.Req<VisualElement>(root, "JournalGrid");
            _progressFill = UiQuery.Req<VisualElement>(root, "JournalProgressFill");
            _empty = UiQuery.Req<Label>(root, "JournalEmpty");

            _close.text = UiStrings.Get("journal.close");
            _filterAll.text = UiStrings.Get("journal.filter_all");
            _filterBiting.label = UiStrings.Get("journal.filter_biting");
            _empty.text = UiStrings.Get("journal.empty");
            _scroll.focusable = false;                      // the cards are the focus stops; the scroll view only scrolls

            _close.clicked += OnCloseClicked;
            _filterAll.clicked += OnFilterAllClicked;
            _filterBiting.RegisterValueChangedCallback(OnBitingToggled);
            _scroll.RegisterCallback<FocusInEvent>(OnFocusIn, TrickleDown.TrickleDown);
        }

        public bool IsOpen => _open;

        // Whether a journal may open right now: never over the local rod's bite or fight (M3-D27).
        internal bool CanOpen(bool inGame) => !inGame || !LocalBiteOrHooked();

        public void Open(bool inGame)
        {
            if (_disposed) return;
            if (!CanOpen(inGame))
            {
                _requestClose();
                return;
            }

            ResolveServices();
            EnsureBuilt();
            _inGame = inGame;
            _open = true;
            _openedFrame = Time.frameCount;
            _biteTimer = 0f;
            _bitingOnly = false;
            _filterBiting.SetValueWithoutNotify(false);
            _filterSpot = inGame ? DefaultSpotFilter() : -1;

            UiQuery.Show(_modal, true);
            if (_save != null) _save.Changed += OnSaveChanged;
            if (_world != null) _world.Changed += OnWorldChanged;

            RefreshBitingContext();
            RefreshCards();
            ApplyFilterVisuals();
            ApplyVisibility();
            _scroll.scrollOffset = Vector2.zero;
            _modal.schedule.Execute(FocusDefault);
        }

        // Called by UiFlowController's modal-closed callback only.
        public void Hide()
        {
            if (_save != null) _save.Changed -= OnSaveChanged;
            if (_world != null) _world.Changed -= OnWorldChanged;
            _open = false;
            UiQuery.Show(_modal, false);
        }

        public void Tick(float dt)
        {
            if (!_open || _disposed) return;
            if (_inGame && (LocalBiteOrHooked() || !ContextAllowsJournal(_input.GameplayContext)))
            {
                // The bite cue closes it (the owner is never left fighting behind a modal), and so does any context the journal may not open
                // in (swimming, climbing, a fight): spec 12.10 / M3-D27.
                _requestClose();
                return;
            }
            // The Journal key (J, Select, or whatever it is rebound to) closes it too. The input service reports the press in every context
            // (the Player map is off while a modal is open); the open frame is skipped so the press that opened it never closes it again,
            // and a held key does not repeat (wasPressedThisFrame).
            if (_input.JournalPressedAnyContext && Time.frameCount != _openedFrame)
            {
                _requestClose();
                return;
            }
            _biteTimer += dt;
            if (_biteTimer >= BiteRefreshSeconds)
            {
                _biteTimer = 0f;
                RefreshBiting();
                ApplyVisibility();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_save != null) _save.Changed -= OnSaveChanged;
            if (_world != null) _world.Changed -= OnWorldChanged;
            _close.clicked -= OnCloseClicked;
            _filterAll.clicked -= OnFilterAllClicked;
            _filterBiting.UnregisterValueChangedCallback(OnBitingToggled);
            _scroll.UnregisterCallback<FocusInEvent>(OnFocusIn, TrickleDown.TrickleDown);
            _open = false;
        }

        // Focus that fell off every control (a card hidden by a filter, a click on the backdrop) lands on the first visible card, else the
        // biting toggle, else Close. Any device: keyboard arrows and a gamepad both need a focus stop to navigate from.
        internal void RecoverFocus()
        {
            if (!_open) return;
            if (_modal.panel?.focusController?.focusedElement == null) FocusRecovered();
        }

        private static bool ContextAllowsJournal(InputContext context) =>
            context == InputContext.Free || context == InputContext.RodReady || context == InputContext.RodWaiting;

        // "Journal n/25" for the main menu; empty without a registry or save.
        internal static string SummaryText(ServiceRegistry app)
        {
            if (app == null || !app.TryGet(out ContentRegistry registry) || registry == null || !app.TryGet(out SaveService save)) return string.Empty;
            var ids = new List<string>();
            var indices = new List<ushort>();
            CollectSpecies(registry, TryGetEcology(registry), indices);
            for (int i = 0; i < indices.Count; i++) ids.Add(registry.SpeciesAt(indices[i]).Id);
            if (ids.Count == 0) return string.Empty;
            int caught = JournalRules.CompletionCount(save.Current, ids);
            return UiStrings.Format("menu.meta_journal", caught.ToString(Inv), ids.Count.ToString(Inv));
        }

        // ---- services and build -----------------------------------------------------------------------------------------------

        private void ResolveServices()
        {
            _registry = _app != null && _app.TryGet(out ContentRegistry registry) ? registry : null;
            _save = _app != null && _app.TryGet(out SaveService save) ? save : null;
            _audio = _app != null && _app.TryGet(out AudioBus audio) ? audio : null;
            ServiceRegistry game = ServiceRoots.Game;
            _world = game != null && game.TryGet(out IWorldContext world) ? world : null;
            if (_registry == null || _save == null)
            {
                if (_log.ShouldLog("no-services")) Debug.Log("FG JOURNAL no ContentRegistry or SaveService in App; the journal shows an empty grid.");
            }
        }

        // Builds the cards and the spot chips once per registry instance.
        private void EnsureBuilt()
        {
            if (_builtForRegistry && ReferenceEquals(_builtRegistry, _registry)) return;
            _grid.Clear();
            _spotFilters.Clear();
            _cards.Clear();
            _chips.Clear();
            _speciesIds.Clear();
            _builtRegistry = _registry;
            _builtForRegistry = true;
            _eco = null;
            _resolver = null;
            if (_registry == null) return;

            _eco = TryGetEcology(_registry);
            if (_eco != null) _resolver = new FishingResolver(_eco);

            BuildChips();
            var indices = new List<ushort>();
            CollectSpecies(_registry, _eco, indices);
            for (int i = 0; i < indices.Count; i++)
            {
                FishSpeciesDef def = _registry.SpeciesAt(indices[i]);
                if (def == null) continue;
                SpeciesCard card = BuildCard(indices[i], def);
                _cards.Add(card);
                _speciesIds.Add(card.Id);
                _grid.Add(card.Root);
            }
        }

        private void BuildChips()
        {
            if (_eco == null) return;
            var order = new List<ushort>();
            for (ushort i = 0; i < _registry.Spots.Count; i++) order.Add(i);
            order.Sort((a, b) =>
            {
                int c = _registry.SpotAt(a).MapOrder.CompareTo(_registry.SpotAt(b).MapOrder);
                return c != 0 ? c : string.CompareOrdinal(_registry.SpotAt(a).Id, _registry.SpotAt(b).Id);
            });
            for (int n = 0; n < order.Count; n++)
            {
                ushort spotIndex = order[n];
                if (spotIndex >= _eco.Spots.Count) continue;
                SpotDef spot = _registry.SpotAt(spotIndex);
                var chip = new SpotChip { Spot = spot, ZoneIndex = _eco.Spots[spotIndex].ZoneIndex };
                int chipIndex = _chips.Count;
                var button = new Button { name = "JournalFilter_" + spot.Id, text = spot.DisplayName };
                button.AddToClassList("journal-filter");
                button.clicked += () =>
                {
                    if (_audio != null) _audio.PlayUi(SfxIds.UiClick);   // runtime-created buttons play their own click (M2 carry rule)
                    SetSpotFilter(chipIndex);
                };
                chip.Button = button;
                _chips.Add(chip);
                _spotFilters.Add(button);
            }
        }

        private SpeciesCard BuildCard(ushort index, FishSpeciesDef def)
        {
            var card = new SpeciesCard { Index = index, Id = def.Id, Def = def, InSpot = new bool[_chips.Count] };

            if (_eco != null)
            {
                JournalHints.Summarize(_eco, index, out card.Seasons, out card.Weather, out card.Bands, _zoneScratch);
                _sb.Clear();
                for (int c = 0; c < _chips.Count; c++)
                {
                    card.InSpot[c] = _zoneScratch.Contains(_chips[c].ZoneIndex);
                    if (!card.InSpot[c]) continue;
                    if (_sb.Length > 0) _sb.Append(", ");
                    _sb.Append(_chips[c].Spot.DisplayName);
                }
                card.HintSpots = _sb.ToString();
            }

            var root = new VisualElement { name = "JournalCard_" + def.Id, focusable = true };
            root.AddToClassList("journal-card");

            var plate = new VisualElement();
            plate.AddToClassList("journal-plate");
            plate.pickingMode = PickingMode.Ignore;
            card.Icon = new VisualElement { pickingMode = PickingMode.Ignore };
            card.Icon.AddToClassList("journal-icon");
            plate.Add(card.Icon);
            card.Badge = new Label(UiStrings.Get("journal.biting")) { pickingMode = PickingMode.Ignore };
            card.Badge.AddToClassList("journal-badge");
            UiQuery.Show(card.Badge, false);
            root.Add(plate);
            root.Add(card.Badge);                 // a tab on the card's top edge, so it never covers the fish

            card.Name = new Label { pickingMode = PickingMode.Ignore };
            card.Name.AddToClassList("journal-name");
            root.Add(card.Name);
            card.Info = new VisualElement { pickingMode = PickingMode.Ignore };
            card.Info.AddToClassList("journal-info");
            root.Add(card.Info);

            card.Glyphs = BuildGlyphBlock(card);
            UiQuery.Show(card.Glyphs, false);
            root.Add(card.Glyphs);

            card.Root = root;
            return card;
        }

        // Level-2 block: season glyphs, weather glyphs, the six time-band cells with labels. Lit state is fixed per species, so it is set once.
        private VisualElement BuildGlyphBlock(SpeciesCard card)
        {
            var block = new VisualElement { pickingMode = PickingMode.Ignore };
            block.AddToClassList("journal-glyphs");

            var caption = new Label(UiStrings.Get("journal.bites_when")) { pickingMode = PickingMode.Ignore };
            caption.AddToClassList("journal-caption");
            block.Add(caption);

            // Legend in icon order, so the glyph names are readable without a tooltip (keyboard and gamepad have no hover).
            _sb.Clear();
            for (int i = 0; i < SeasonKeys.Length; i++) _sb.Append(i > 0 ? ", " : string.Empty).Append(UiStrings.Get(SeasonKeys[i]));
            _sb.Append('\n');                        // seasons on the first line, weather on the second: one tidy break instead of a ragged wrap
            for (int i = 0; i < WeatherKeys.Length; i++) _sb.Append(i > 0 ? ", " : string.Empty).Append(UiStrings.Get(WeatherKeys[i]));
            var legend = new Label(_sb.ToString()) { pickingMode = PickingMode.Ignore };
            legend.AddToClassList("journal-legend");
            block.Add(legend);

            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("journal-glyph-row");
            Season[] seasons = { Season.Spring, Season.Summer, Season.Autumn, Season.Winter };
            for (int i = 0; i < seasons.Length; i++)
            {
                row.Add(MakeGlyph(WorldGlyphs.Of(seasons[i]), (card.Seasons & EcologyMasks.Of(seasons[i])) != 0, SeasonKeys[i]));
            }
            var gap = new VisualElement { pickingMode = PickingMode.Ignore };
            gap.AddToClassList("journal-glyph-gap");
            row.Add(gap);
            WeatherMask[] weather = { WeatherMask.Clear, WeatherMask.Rain, WeatherMask.Fog };
            for (int i = 0; i < weather.Length; i++)
            {
                row.Add(MakeGlyph(WorldGlyphs.Of(weather[i]), (card.Weather & weather[i]) != 0, WeatherKeys[i]));
            }
            block.Add(row);

            var bar = new BandBarElement { Bands = card.Bands, Ink = GlyphInk };
            bar.AddToClassList("journal-band-bar");
            block.Add(bar);

            var labels = new VisualElement { pickingMode = PickingMode.Ignore };
            labels.AddToClassList("journal-band-labels");
            for (int i = 0; i < BandKeys.Length; i++)
            {
                var label = new Label(UiStrings.Get(BandKeys[i])) { pickingMode = PickingMode.Ignore };
                label.AddToClassList("journal-band-label");
                labels.Add(label);
            }
            block.Add(labels);
            return block;
        }

        private static GlyphElement MakeGlyph(Glyph glyph, bool lit, string nameKey)
        {
            var element = new GlyphElement { Glyph = glyph, Lit = lit, Ink = GlyphInk, tooltip = UiStrings.Get(nameKey) };
            element.AddToClassList("journal-glyph");
            return element;
        }

        // ---- refresh ---------------------------------------------------------------------------------------------------------------

        private void RefreshCards()
        {
            int caught = 0;
            MetaSave meta = _save != null ? _save.Current : null;
            for (int i = 0; i < _cards.Count; i++)
            {
                SpeciesCard card = _cards[i];
                JournalEntry entry = null;
                if (meta != null && meta.Journal != null) meta.Journal.TryGetValue(card.Id, out entry);
                if (entry != null && entry.Caught >= 1) caught++;
                UpdateCard(card, entry);
            }

            string completion = UiStrings.Format("journal.completion", caught.ToString(Inv), _cards.Count.ToString(Inv));
            if (_completion.text != completion) _completion.text = completion;
            float pct = _cards.Count == 0 ? 0f : 100f * caught / _cards.Count;
            _progressFill.style.width = Length.Percent(pct);
            RefreshBiting();
        }

        private void UpdateCard(SpeciesCard card, JournalEntry entry)
        {
            int level = JournalRules.HintLevel(entry);
            if (level != card.Level)
            {
                if (card.Level >= 0) card.Root.RemoveFromClassList("level-" + card.Level);
                card.Root.AddToClassList("level-" + level);
                card.Level = level;

                ArtPaletteDef palette = _registry != null ? _registry.ArtPalette : null;
                card.Icon.style.backgroundImage = IconFactory.TryGetBackground(card.Def, palette, out Background icon)
                    ? new StyleBackground(icon)
                    : new StyleBackground(StyleKeyword.None);
                card.Icon.style.unityBackgroundImageTintColor = level == 0 ? new StyleColor(IconFactory.UiTint) : new StyleColor(Color.white);
                UiQuery.Show(card.Glyphs, level >= 2);
            }

            string nameText = level == 0 ? UiStrings.Get("journal.unknown") : card.Def.DisplayName;
            if (card.NameText != nameText)
            {
                card.NameText = nameText;
                card.Name.text = nameText;
            }

            string info = BuildInfo(card, entry, level);
            if (card.InfoText != info)
            {
                card.InfoText = info;
                SetInfoLines(card, info);
            }
        }

        // One Label per line (seen at, caught, weight, best) so each reads as its own row; labels are reused and only hidden when unused.
        private static void SetInfoLines(SpeciesCard card, string info)
        {
            string[] parts = info.Length == 0 ? Array.Empty<string>() : info.Split('\n');
            while (card.Lines.Count < parts.Length)
            {
                var line = new Label { pickingMode = PickingMode.Ignore };
                line.AddToClassList("journal-line");
                card.Info.Add(line);
                card.Lines.Add(line);
            }
            for (int i = 0; i < card.Lines.Count; i++)
            {
                bool used = i < parts.Length;
                if (used) card.Lines[i].text = parts[i];
                UiQuery.Show(card.Lines[i], used);
            }
            UiQuery.Show(card.Info, parts.Length > 0);
        }

        private string BuildInfo(SpeciesCard card, JournalEntry entry, int level)
        {
            _sb.Clear();
            if (level == 0)
            {
                if (card.HintSpots.Length > 0) _sb.Append(UiStrings.Format("journal.hint_spot", card.HintSpots));
                return _sb.ToString();
            }

            bool any = false;
            if (entry.SeenInSpots != null && entry.SeenInSpots.Count > 0)
            {
                _sb.Append(UiStrings.Format("journal.seen_at", SeenAtText(card, entry)));
                any = true;
            }
            if (entry.Caught > 0)
            {
                if (any) _sb.Append('\n');
                _sb.Append(UiStrings.Format("journal.caught", entry.Caught.ToString(Inv)));
                any = true;
            }
            if (level >= 3)
            {
                if (any) _sb.Append('\n');
                _sb.Append(UiStrings.Format("journal.weight_range", Kg(card.Def.WeightMinG), Kg(card.Def.WeightMaxG)));
                if (entry.BestWeightG > 0)
                {
                    int q = Mathf.Clamp(entry.BestQuality, 0, QualityKeys.Length - 1);
                    _sb.Append('\n');
                    _sb.Append(UiStrings.Format("journal.best", Kg(entry.BestWeightG), entry.BestLengthMm.ToString(Inv), UiStrings.Get(QualityKeys[q])));
                }
            }
            return _sb.ToString();
        }

        // "Lake Dock (Reeds, Open Water), Harbour Pier (...)": each seen spot with the species' sub-areas in that spot's zone
        // (open question 1: level 1 lists sub-areas per seen spot, from the rules; the save stores spots only).
        private string SeenAtText(SpeciesCard card, JournalEntry entry)
        {
            var text = new StringBuilder(96);
            for (int i = 0; i < entry.SeenInSpots.Count; i++)
            {
                string id = entry.SeenInSpots[i];
                if (string.IsNullOrEmpty(id)) continue;
                if (text.Length > 0) text.Append(", ");
                SpotDef spot = null;
                bool known = _registry != null && _registry.TryGetSpot(id, out spot) && spot != null;
                text.Append(known ? spot.DisplayName : id);
                if (!known || spot.Zone == null) continue;
                int mark = text.Length;
                text.Append(" (");
                int before = text.Length;
                AppendSubAreas(text, spot.Zone, card.Def);
                if (text.Length == before) text.Length = mark;      // no rule of this species in that zone: just the spot name
                else text.Append(')');
            }
            return text.ToString();
        }

        private static void AppendSubAreas(StringBuilder text, FishingZoneDef zone, FishSpeciesDef def)
        {
            bool first = true;
            for (int b = 0; b < zone.SubAreas.Count; b++)
            {
                string bandId = zone.SubAreas[b].id;
                if (!BandNamedByRule(zone, def, bandId)) continue;
                if (!first) text.Append(", ");
                first = false;
                AppendTitleCase(text, bandId);
            }
        }

        private static bool BandNamedByRule(FishingZoneDef zone, FishSpeciesDef def, string bandId)
        {
            for (int r = 0; r < zone.Rules.Count; r++)
            {
                SpawnRule rule = zone.Rules[r];
                if (rule == null || rule.fish == null || !string.Equals(rule.fish.Id, def.Id, StringComparison.Ordinal)) continue;
                if (rule.subAreaIds == null || rule.subAreaIds.Length == 0) return true;      // empty = every band
                for (int k = 0; k < rule.subAreaIds.Length; k++)
                {
                    if (string.Equals(rule.subAreaIds[k], bandId, StringComparison.Ordinal)) return true;
                }
            }
            return false;
        }

        // "open_water" -> "Open Water"
        private static void AppendTitleCase(StringBuilder text, string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            bool upper = true;
            for (int i = 0; i < id.Length; i++)
            {
                char ch = id[i];
                if (ch == '_') { text.Append(' '); upper = true; continue; }
                text.Append(upper ? char.ToUpperInvariant(ch) : ch);
                upper = false;
            }
        }

        private static string Kg(int grams) => (grams / 1000f).ToString("0.0", Inv);

        // ---- biting now (M3-D26) ---------------------------------------------------------------------------------------------------

        // Resolves the zone and whether badges can show at all: in game, with replicated state, at a spot the registry knows.
        private void RefreshBitingContext()
        {
            _bitingAvailable = false;
            _biteZone = 0;
            if (!_inGame || _world == null || _registry == null || _eco == null || _resolver == null) return;
            if (!_world.HasState) return;
            string spotId = _world.SpotId;
            if (string.IsNullOrEmpty(spotId) || !_registry.TryGetSpot(spotId, out SpotDef spot) || spot == null) return;
            ushort spotIndex = _registry.SpotIndex(spot.Id);
            if (spotIndex >= _eco.Spots.Count) return;
            int zone = _eco.Spots[spotIndex].ZoneIndex;
            if ((uint)zone >= (uint)_eco.Zones.Count) return;
            _biteZone = zone;
            _bitingAvailable = true;
        }

        private void RefreshBiting()
        {
            if (_bitingAvailable && _world != null && !_world.HasState) _bitingAvailable = false;
            _filterBiting.SetEnabled(_bitingAvailable);
            if (!_bitingAvailable && _bitingOnly)
            {
                _bitingOnly = false;
                _filterBiting.SetValueWithoutNotify(false);
            }

            FishingContext ctx = default;
            ZoneData zone = null;
            if (_bitingAvailable)
            {
                ctx = BuildContext();
                zone = _eco.Zones[_biteZone];
            }

            for (int i = 0; i < _cards.Count; i++)
            {
                SpeciesCard card = _cards[i];
                bool biting = zone != null && card.Level >= 2 && BitingNow.IsBiting(_resolver, zone, card.Index, in ctx);
                if (biting == card.Biting) continue;
                card.Biting = biting;
                UiQuery.Show(card.Badge, biting);
            }
        }

        // The local player's view of the world: its rod tier and selected bait (never the host's ledger), the replicated clock, season and
        // weather. Events, skills and luck are neutral; BitingNow itself opens the sub-area and depth gates.
        private FishingContext BuildContext()
        {
            int tier = 0;
            BaitMods bait = BaitMods.Neutral(0);
            FishingRodNetwork net = LocalRodNetwork();
            if (net != null && _registry != null)
            {
                ushort rodIndex = net.NetRodIndex.Value;
                if (rodIndex < _registry.Rods.Count) tier = _registry.RodAt(rodIndex).Tier;
                byte baitIndex = net.NetSelectedBait.Value;
                if (baitIndex < _registry.Baits.Count) bait = _registry.BaitAt(baitIndex).ToCore().Mods(1);
            }
            int players = FishingRodNetwork.Active != null ? FishingRodNetwork.Active.Count : 0;
            return new FishingContext
            {
                ZoneIndex = _biteZone,
                SubAreaIndex = EcologyConstants.AnySubArea,
                Depth = float.PositiveInfinity,
                Minute = _world.Minute,
                Season = _world.Season,
                Weather = _world.Weather,
                RodTier = tier,
                Bait = bait,
                InHotspot = false,
                Luck = 0f,
                Events = EventMods.Neutral,
                Skills = SkillMods.Neutral,
                PlayersInSession = players < 1 ? 1 : players,
            };
        }

        private FishingRodNetwork LocalRodNetwork()
        {
            FishingRodController rod = FishingRodController.Local;
            if (rod == null) return null;
            if (!ReferenceEquals(rod, _rodCached))
            {
                _rodCached = rod;
                _netCached = rod.GetComponent<FishingRodNetwork>();
            }
            return _netCached;
        }

        private static bool LocalBiteOrHooked()
        {
            FishingRodController rod = FishingRodController.Local;
            if (rod == null || rod.Machine == null) return false;
            RodState state = rod.Machine.State;
            return state == RodState.Bite || state == RodState.Hooked;
        }

        // ---- filters ---------------------------------------------------------------------------------------------------------------

        private int DefaultSpotFilter()
        {
            if (_world == null) return -1;
            string spotId = _world.SpotId;
            if (string.IsNullOrEmpty(spotId)) return -1;
            for (int i = 0; i < _chips.Count; i++)
            {
                if (string.Equals(_chips[i].Spot.Id, spotId, StringComparison.Ordinal)) return i;
            }
            return -1;
        }

        private void SetSpotFilter(int chip)
        {
            _filterSpot = chip;
            ApplyFilterVisuals();
            ApplyVisibility();
        }

        private void ApplyFilterVisuals()
        {
            _filterAll.EnableInClassList("selected", _filterSpot < 0);
            for (int i = 0; i < _chips.Count; i++) _chips[i].Button.EnableInClassList("selected", i == _filterSpot);
        }

        // Filtering toggles display only; the cards are never rebuilt.
        private void ApplyVisibility()
        {
            int shown = 0;
            for (int i = 0; i < _cards.Count; i++)
            {
                SpeciesCard card = _cards[i];
                bool visible = (_filterSpot < 0 || (_filterSpot < card.InSpot.Length && card.InSpot[_filterSpot])) && (!_bitingOnly || card.Biting);
                if (visible) shown++;
                if (visible == card.Shown) continue;
                card.Shown = visible;
                UiQuery.Show(card.Root, visible);
            }
            UiQuery.Show(_empty, shown == 0 && _cards.Count > 0);
        }

        // ---- events ----------------------------------------------------------------------------------------------------------------

        private void OnCloseClicked() => _requestClose();

        private void OnFilterAllClicked() => SetSpotFilter(-1);

        private void OnBitingToggled(ChangeEvent<bool> evt)
        {
            _bitingOnly = evt.newValue && _bitingAvailable;
            ApplyVisibility();
        }

        private void OnSaveChanged(MetaSave _)
        {
            if (!_open) return;
            RefreshCards();
            ApplyVisibility();
        }

        private void OnWorldChanged()
        {
            if (!_open) return;
            RefreshBitingContext();
            RefreshBiting();
            ApplyVisibility();
        }

        // Keeps the focused card visible while navigating with the keyboard or a gamepad.
        private void OnFocusIn(FocusInEvent evt)
        {
            if (evt.target is VisualElement element && element != _scroll && _scroll.contentContainer.Contains(element))
                _scroll.ScrollTo(element);
        }

        private void FocusDefault()
        {
            if (_open) _close.Focus();
        }

        // Recovery lands on the first visible card (so the arrow keys and the stick restart inside the grid), else the biting toggle,
        // else Close.
        private void FocusRecovered()
        {
            if (!_open) return;
            for (int i = 0; i < _cards.Count; i++)
            {
                if (_cards[i].Shown)
                {
                    _cards[i].Root.Focus();
                    return;
                }
            }
            if (_filterBiting.enabledSelf) _filterBiting.Focus();
            else _close.Focus();
        }

        // ---- species list ----------------------------------------------------------------------------------------------------------

        private static EcologyDatabase TryGetEcology(ContentRegistry registry)
        {
            try
            {
                return registry.Ecology;
            }
            catch (InvalidOperationException e)
            {
                if (StaticLog.ShouldLog("ecology")) Debug.LogWarning("FG JOURNAL the ecology database could not be built; the journal lists every species: " + e.Message);
                return null;
            }
        }

        // M3-D50: every species with a rule in some zone, plus every special, sorted by id (ordinal). Without an ecology database: all species.
        private static void CollectSpecies(ContentRegistry registry, EcologyDatabase eco, List<ushort> into)
        {
            into.Clear();
            int count = registry.Species.Count;
            var ruled = new bool[count];
            if (eco != null)
            {
                for (int z = 0; z < eco.Zones.Count; z++)
                {
                    var rules = eco.Zones[z].Rules;
                    for (int r = 0; r < rules.Count; r++)
                    {
                        if (rules[r].SpeciesIndex < count) ruled[rules[r].SpeciesIndex] = true;
                    }
                }
            }
            for (ushort i = 0; i < count; i++)
            {
                FishSpeciesDef def = registry.SpeciesAt(i);
                if (def == null) continue;
                if (eco == null || ruled[i] || def.IsSpecial) into.Add(i);
            }
            into.Sort((a, b) => string.CompareOrdinal(registry.SpeciesAt(a).Id, registry.SpeciesAt(b).Id));
        }
    }
}

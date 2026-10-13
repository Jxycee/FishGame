using System;
using System.Collections.Generic;

namespace FishGame.UI
{
    // Single English string table for all UI text (spec 11.1). Unknown keys return the key itself.
    public static class UiStrings
    {
        private static readonly Dictionary<string, string> Table = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["menu.title"] = "FishGame",
            ["menu.host"] = "Host",
            ["menu.join"] = "Join",
            ["menu.join_code"] = "Join code",
            ["menu.solo"] = "Solo",
            ["menu.settings"] = "Settings",
            ["menu.quit"] = "Quit",
            ["menu.name"] = "Display name",
            ["menu.dev_host"] = "Host Direct (dev)",
            ["menu.dev_join"] = "Join 127.0.0.1 (dev)",
            ["busy.creating"] = "Creating session...",
            ["busy.joining"] = "Joining session...",
            ["busy.loading"] = "Loading...",
            ["busy.cancel"] = "Cancel",
            ["busy.cancelling"] = "Cancelling...",
            ["toast.join_code"] = "Join code: {0}",
            ["pause.title"] = "Paused",
            ["pause.resume"] = "Resume",
            ["pause.settings"] = "Settings",
            ["pause.leave"] = "Leave",
            ["pause.code_label"] = "Join code",
            ["pause.copy"] = "Copy",
            ["pause.copied"] = "Join code copied.",
            ["pause.reveal"] = "Hold to reveal",
            ["pause.masked"] = "••••••",
            ["pause.joins_close"] = "Close joins (dev)",
            ["pause.joins_open"] = "Open joins (dev)",
            ["settings.title"] = "Settings",
            ["settings.back"] = "Back",
            ["settings.section_display"] = "Display",
            ["settings.section_audio"] = "Audio",
            ["settings.section_gameplay"] = "Gameplay",
            ["settings.section_controls"] = "Controls",
            ["settings.master"] = "Master volume",
            ["settings.sfx"] = "Effects volume",
            ["settings.ambience"] = "Ambience volume",
            ["settings.ui"] = "Interface volume",
            ["settings.name"] = "Display name",
            ["settings.mouse"] = "Mouse sensitivity",
            ["settings.pad_look"] = "Gamepad look speed",
            ["settings.invert_y"] = "Invert Y",
            ["settings.fov"] = "Field of view",
            ["settings.cam_motion"] = "Camera motion",
            ["settings.sprint_toggle"] = "Sprint toggle",
            ["settings.aim_toggle"] = "Aim toggle",
            ["settings.streamer"] = "Streamer mode",
            ["settings.ui_scale"] = "UI scale %",
            ["settings.rebind_hint"] = "Select a control, then press the new key or button. Escape cancels; waiting stops after 6 seconds.",
            ["settings.rebind_wait"] = "Press a key or button...",
            ["settings.rebind_reset"] = "Reset controls",
            ["settings.action_jump"] = "Jump",
            ["settings.action_sprint"] = "Sprint",
            ["settings.action_interact"] = "Interact",
            ["settings.scheme_kbm"] = "Keyboard",
            ["settings.scheme_pad"] = "Gamepad",
            ["hud.key"] = "[{0}]",
            ["hud.strip_interact"] = "[{0}] Interact",
            ["hud.strip_pause"] = "[{0}] Pause",
            ["cap.bite"] = "Bite!",
            ["cap.straining"] = "Line straining",
            ["cap.snapped"] = "Snapped",
            ["cap.escaped"] = "Got away",
            ["hud.click"] = "Click!",
            ["hud.creel"] = "{0}/{1}  {2} kg",
            ["hud.creel_full"] = "Full - sell!",
            ["toast.catch"] = "{0} caught {1} kg {2} (${3})",
            ["toast.lost"] = "It got away!",
            ["toast.creel_full"] = "Creel full! {0} released",
            ["toast.clean"] = "Clean catch!",
            ["toast.raw"] = "{0}",
            ["toast.junk"] = "{0} fished up {1} (${2})",
            ["toast.pity"] = "{0} has a lucky line!",
            ["toast.journal_new"] = "New in the journal: {0}!",
            ["toast.journal_best"] = "Personal best! {0}",
            ["toast.journal_unlock"] = "Journal: new info on {0}",
            ["settings.line_gauge"] = "Line gauge",
            ["settings.captions"] = "Captions",
            ["settings.assist"] = "Fishing assist",
            ["settings.gauge_off"] = "Off",
            ["settings.gauge_arc"] = "Arc",
            ["settings.gauge_bar"] = "Bar",
            ["dev.host_only"] = "Host only",
            ["dev.refused_cast"] = "Refused: cast active",
            ["dev.no_rod"] = "No local rod",
            ["dev.no_water"] = "No water under crosshair",
            ["err.host_left"] = "Host ended the session.",
            ["err.rejected"] = "The host did not accept this connection.",
            ["err.joins_closed"] = "Run in progress. Join when the crew is in the Hub, at the Briefing or at Night Camp.",
            ["err.timeout"] = "Connection timed out. Try again.",
            ["err.session_full"] = "That session is full.",
            ["err.version"] = "Different game version — update your build (or the host's build differs).",
            ["err.code_not_found"] = "Join code not found. Check it and try again.",
            ["err.services"] = "Online services are unavailable. You can play Solo instead.",
            ["err.network"] = "Network connection failed. Try again.",
            ["err.generic"] = "Something went wrong. Try again.",
        };

        public static string Get(string key)
        {
            if (key == null) return string.Empty;
            return Table.TryGetValue(key, out var text) ? text : key;
        }

        public static string Format(string key, string arg) => string.Format(Get(key), arg ?? string.Empty);

        // Several placeholders (T25). Never pass a literal null as the only argument after the key: it is ambiguous with the overload above.
        public static string Format(string key, params string[] args) =>
            string.Format(Get(key), (object[])(args ?? Array.Empty<string>()));
    }
}

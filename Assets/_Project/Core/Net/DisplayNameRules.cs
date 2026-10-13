using System.Text;

namespace FishGame.Core
{
    // Display-name rules shared by the settings store (T5), the connection approver (T8) and SubmitNameRpc (T11).
    public static class DisplayNameRules
    {
        public const int MaxLength = 16;

        // Removes char.IsControl characters, trims, truncates to MaxLength chars (never splitting a surrogate pair),
        // trims the end again. An empty result (or null input) becomes Fallback(slot).
        public static string Sanitize(string raw, int slot)
        {
            if (raw == null) return Fallback(slot);

            var sb = new StringBuilder(raw.Length);
            foreach (char c in raw)
            {
                if (!char.IsControl(c)) sb.Append(c);
            }

            string s = sb.ToString().Trim();
            if (s.Length > MaxLength) s = Truncate(s, MaxLength).TrimEnd();

            return s.Length == 0 ? Fallback(slot) : s;
        }

        // The single truncation primitive (M3 T2): cut to at most maxChars UTF-16 chars, never between a high and a low surrogate
        // (the cut moves one char left instead). Does not clean: callers pass text that already went through their own cleaning.
        // Sanitize uses it directly; SessionText.FitBytes (and through SessionText.Name, NameWire) calls it once per byte-budget step.
        public static string Truncate(string clean, int maxChars)
        {
            if (maxChars < 0) throw new System.ArgumentOutOfRangeException(nameof(maxChars));
            if (string.IsNullOrEmpty(clean) || maxChars == 0) return string.Empty;
            if (clean.Length <= maxChars) return clean;
            int cut = maxChars;
            if (char.IsHighSurrogate(clean[cut - 1]) && char.IsLowSurrogate(clean[cut])) cut--;
            return clean.Substring(0, cut);
        }

        // "Fisher " + (slot + 1): slot 0 -> "Fisher 1".
        public static string Fallback(int slot)
        {
            return "Fisher " + (slot + 1);
        }
    }
}

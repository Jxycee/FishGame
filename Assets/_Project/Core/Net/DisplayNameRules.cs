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
            if (s.Length > MaxLength)
            {
                int cut = MaxLength;
                if (char.IsHighSurrogate(s[cut - 1]) && char.IsLowSurrogate(s[cut])) cut--;
                s = s.Substring(0, cut).TrimEnd();
            }

            return s.Length == 0 ? Fallback(slot) : s;
        }

        // "Fisher " + (slot + 1): slot 0 -> "Fisher 1".
        public static string Fallback(int slot)
        {
            return "Fisher " + (slot + 1);
        }
    }
}

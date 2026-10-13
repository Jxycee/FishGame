// Session-internal text helpers. Fixed-string capacities are UTF-8 bytes (FixedString32Bytes = 29, FixedString64Bytes = 61),
// and the string constructors throw in the editor when the text is too long, so text is truncated by bytes first.
// M3 T2: this is the one place that fits names to the wire (NameWire.ToFixed delegates to Name). The cut itself is
// DisplayNameRules.Truncate, called once per byte-budget step. Behaviour change recorded: Name now drops lone surrogates, as NameWire always did
// (before, a lone surrogate passed through here and encoded as U+FFFD, 3 bytes).
using System.Text;
using FishGame.Core;
using Unity.Collections;

namespace FishGame.Gameplay
{
    internal static class SessionText
    {
        public const int NameCapacityBytes = 29; // FixedString32Bytes
        public const int IdCapacityBytes = 61;   // FixedString64Bytes

        public static string FitBytes(string text, int maxBytes)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var s = text;
            while (s.Length > 0 && Encoding.UTF8.GetByteCount(s) > maxBytes)
            {
                s = DisplayNameRules.Truncate(s, s.Length - 1);   // never splits a surrogate pair
            }
            return s;
        }

        public static FixedString32Bytes Name(string text)
        {
            return new FixedString32Bytes(FitBytes(DropLoneSurrogates(text), NameCapacityBytes));
        }

        // A lone high or low surrogate is dropped; a valid pair is kept.
        private static string DropLoneSurrogates(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var clean = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsHighSurrogate(c))
                {
                    if (i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                    {
                        clean.Append(c);
                        clean.Append(text[i + 1]);
                        i++;
                    }

                    continue;
                }

                if (char.IsLowSurrogate(c)) continue;
                clean.Append(c);
            }

            return clean.ToString();
        }

        public static FixedString64Bytes Id(string text)
        {
            return new FixedString64Bytes(FitBytes(text, IdCapacityBytes));
        }
    }
}

// Session-internal text helpers. Fixed-string capacities are UTF-8 bytes (FixedString32Bytes = 29, FixedString64Bytes = 61),
// and the string constructors throw in the editor when the text is too long, so text is truncated by bytes first.
using System.Text;
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
                int cut = s.Length - 1;
                // Never split a surrogate pair.
                if (cut > 0 && char.IsLowSurrogate(s[cut]) && char.IsHighSurrogate(s[cut - 1])) cut--;
                s = s.Substring(0, cut);
            }
            return s;
        }

        public static FixedString32Bytes Name(string text)
        {
            return new FixedString32Bytes(FitBytes(text, NameCapacityBytes));
        }

        public static FixedString64Bytes Id(string text)
        {
            return new FixedString64Bytes(FitBytes(text, IdCapacityBytes));
        }
    }
}

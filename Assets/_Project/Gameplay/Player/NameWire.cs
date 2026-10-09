using System.Text;
using Unity.Collections;

namespace FishGame.Gameplay
{
    // Authority: none (pure helper). Converts a display name to FixedString32Bytes without ever throwing.
    // DisplayNameRules.Sanitize caps at 16 chars, which can exceed the 29 usable UTF-8 bytes for non-ASCII text, and
    // new FixedString32Bytes(string) throws on truncation. Lone UTF-16 surrogates are dropped; the rest is copied with
    // CopyFromTruncated, which cuts at a code-point boundary.
    internal static class NameWire
    {
        public static FixedString32Bytes ToFixed(string text)
        {
            FixedString32Bytes result = default;
            if (string.IsNullOrEmpty(text))
            {
                return result;
            }

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

                if (char.IsLowSurrogate(c))
                {
                    continue;
                }

                clean.Append(c);
            }

            result.CopyFromTruncated(clean.ToString());
            return result;
        }
    }
}

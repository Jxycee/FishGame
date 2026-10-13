using Unity.Collections;

namespace FishGame.Gameplay
{
    // Authority: none (pure helper). Converts a display name to FixedString32Bytes without ever throwing.
    // DisplayNameRules.Sanitize caps at 16 chars, which can exceed the 29 usable UTF-8 bytes for non-ASCII text, and
    // new FixedString32Bytes(string) throws on truncation. M3 T2: SessionText is the one place that fits names to the wire
    // (lone UTF-16 surrogates dropped, then the UTF-8 byte budget through DisplayNameRules.Truncate); this just delegates.
    internal static class NameWire
    {
        public static FixedString32Bytes ToFixed(string text)
        {
            return SessionText.Name(text);
        }
    }
}

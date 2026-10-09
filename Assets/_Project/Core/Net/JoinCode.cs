// Core/Net/JoinCode.cs: join-code normalisation and plausibility. Pure logic, no network calls.
// If runtime check V3 shows Sessions codes are case-sensitive, change only Normalize (contract D20).
namespace FishGame.Core
{
    public static class JoinCode
    {
        public const int MaxLength = 16;

        // null -> ""; trims surrounding whitespace; upper-cases with the invariant culture.
        public static string Normalize(string raw)
        {
            if (raw == null) return "";
            return raw.Trim().ToUpperInvariant();
        }

        // 1..MaxLength characters, each A-Z or 0-9. Expects an already normalised code.
        public static bool IsPlausible(string normalized)
        {
            if (normalized == null || normalized.Length < 1 || normalized.Length > MaxLength) return false;
            for (int i = 0; i < normalized.Length; i++)
            {
                char c = normalized[i];
                bool upper = c >= 'A' && c <= 'Z';
                bool digit = c >= '0' && c <= '9';
                if (!upper && !digit) return false;
            }
            return true;
        }
    }
}

using System;

namespace FishGame.Core
{
    // Maps a Multiplayer Services session error (numeric SessionError + message) to an EndReason.
    // Pure logic, EditMode-tested (T9). The SDK turns Lobby "full" (16004) and "invalid join code" (16010) into
    // SessionError.Unknown and keeps only the message text (F3), so those cases are decided from the message.
    public static class UgsErrorMap
    {
        // Mirror the Multiplayer Services 2.3.3 SessionError enum (read from its source: Unknown = 1,
        // SessionNotFound = 6, InvalidParameter = 12). UgsNetworkSession has a compile-time guard against drift.
        public const int SessionErrorUnknown = 1;
        public const int SessionErrorSessionNotFound = 6;
        public const int SessionErrorInvalidParameter = 12;

        // Rule order, first match wins (all text checks are ordinal, ignore case):
        // 1 message contains "full" not followed by a letter ("Lobby is full", "LobbyFull"; not "fully", "successfully") -> FullLobby
        // 2 code == SessionNotFound -> InvalidCode
        // 2b message contains "locked" -> JoinsClosed (Lobby LobbyLocked arrives as SessionError.Unknown with text only)
        // 3 message contains "relay" -> ServicesUnavailable (the SDK's "Failed to fetch relay join code" is a Relay outage, not a bad code)
        // 4 message contains "not found", "join code" or "invalid code" -> InvalidCode
        // 5 code == InvalidParameter -> InvalidCode
        // 6 else -> ServicesUnavailable
        public static EndReason Map(int sessionErrorCode, string message)
        {
            var text = message ?? string.Empty;

            if (ContainsFullWord(text))
            {
                return EndReason.FullLobby;
            }

            if (sessionErrorCode == SessionErrorSessionNotFound)
            {
                return EndReason.InvalidCode;
            }

            if (Contains(text, "locked"))
            {
                return EndReason.JoinsClosed;
            }

            if (Contains(text, "relay"))
            {
                return EndReason.ServicesUnavailable;
            }

            if (Contains(text, "not found") || Contains(text, "join code") || Contains(text, "invalid code"))
            {
                return EndReason.InvalidCode;
            }

            if (sessionErrorCode == SessionErrorInvalidParameter)
            {
                return EndReason.InvalidCode;
            }

            return EndReason.ServicesUnavailable;
        }

        private static bool Contains(string text, string part)
        {
            return text.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // "full" (any case) whose next character is not a letter. A letter before it is allowed, so "LobbyFull" matches.
        private static bool ContainsFullWord(string text)
        {
            int index = text.IndexOf("full", StringComparison.OrdinalIgnoreCase);
            while (index >= 0)
            {
                int after = index + 4;
                if (after >= text.Length || !char.IsLetter(text[after]))
                {
                    return true;
                }

                index = text.IndexOf("full", after, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }
    }
}

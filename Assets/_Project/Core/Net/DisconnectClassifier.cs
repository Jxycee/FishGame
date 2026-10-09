using System;

namespace FishGame.Core
{
    // Turns an NGO client stop into an EndReason (spec 6.9, contract 7.3). Called by LocalNetworkSession (T8) and UgsNetworkSession (T9).
    // The host's approver sends EndReason.ToString() as the disconnect reason; NGO's own reason text (for example
    // "[Disconnect Event] ...") is not an EndReason name and falls through to the timing rules.
    public static class DisconnectClassifier
    {
        // 1 userInitiated -> UserLeft
        // 2 disconnectReason is exactly one defined EndReason name (case-sensitive, first char a letter, no comma) -> that value
        // 3 wasConnected -> HostLeft
        // 4 secondsSinceStart < NetworkConstants.VersionMismatchWindowSeconds -> VersionMismatch
        // 5 else Timeout
        public static EndReason Classify(bool userInitiated, bool wasConnected, string disconnectReason, double secondsSinceStart)
        {
            if (userInitiated) return EndReason.UserLeft;
            if (TryParseReason(disconnectReason, out EndReason parsed)) return parsed;
            if (wasConnected) return EndReason.HostLeft;
            if (secondsSinceStart < NetworkConstants.VersionMismatchWindowSeconds) return EndReason.VersionMismatch;
            return EndReason.Timeout;
        }

        private static bool TryParseReason(string text, out EndReason reason)
        {
            reason = EndReason.Unknown;
            if (string.IsNullOrEmpty(text) || !char.IsLetter(text[0])) return false;
            // Enum.TryParse accepts "A, B" and returns the OR of both values, which can collide with a real member.
            if (text.IndexOf(',') >= 0) return false;
            if (!Enum.TryParse(text, false, out EndReason value)) return false;
            if (!Enum.IsDefined(typeof(EndReason), value)) return false;
            reason = value;
            return true;
        }
    }
}

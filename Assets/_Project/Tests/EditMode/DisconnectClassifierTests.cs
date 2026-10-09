// Tests for FishGame.Core.DisconnectClassifier (spec 6.9, contract 7.3).
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class DisconnectClassifierTests
    {
        [Test]
        public void DisconnectClassifier_ReasonString_ParsesEndReason()
        {
            Assert.AreEqual(EndReason.FullLobby, DisconnectClassifier.Classify(false, false, "FullLobby", 0.5));
            Assert.AreEqual(EndReason.JoinsClosed, DisconnectClassifier.Classify(false, true, "JoinsClosed", 100.0));
            Assert.AreEqual(EndReason.VersionMismatch, DisconnectClassifier.Classify(false, false, "VersionMismatch", 30.0));
            Assert.AreEqual(EndReason.DuplicateIdentity, DisconnectClassifier.Classify(false, false, EndReason.DuplicateIdentity.ToString(), 1.0));
            Assert.AreEqual(EndReason.Kicked, DisconnectClassifier.Classify(false, true, "Kicked", 5.0));
        }

        [Test]
        public void DisconnectClassifier_EmptyWhileConnected_IsHostLeft()
        {
            Assert.AreEqual(EndReason.HostLeft, DisconnectClassifier.Classify(false, true, "", 100.0));
            Assert.AreEqual(EndReason.HostLeft, DisconnectClassifier.Classify(false, true, null, 1.0));
        }

        [Test]
        public void DisconnectClassifier_EmptyEarlyWhileConnecting_IsVersionMismatch()
        {
            Assert.AreEqual(EndReason.VersionMismatch, DisconnectClassifier.Classify(false, false, null, 2.0));
            Assert.AreEqual(EndReason.VersionMismatch, DisconnectClassifier.Classify(false, false, "", 7.99));
        }

        [Test]
        public void DisconnectClassifier_EmptyLateWhileConnecting_IsTimeout()
        {
            Assert.AreEqual(EndReason.Timeout, DisconnectClassifier.Classify(false, false, "", 9.0));
            Assert.AreEqual(EndReason.Timeout, DisconnectClassifier.Classify(false, false, null, NetworkConstants.VersionMismatchWindowSeconds));
        }

        [Test]
        public void DisconnectClassifier_UserInitiated_IsUserLeft()
        {
            Assert.AreEqual(EndReason.UserLeft, DisconnectClassifier.Classify(true, true, "FullLobby", 0.0));
            Assert.AreEqual(EndReason.UserLeft, DisconnectClassifier.Classify(true, false, null, 100.0));
        }

        // Review Focus 1: reason text that is not exactly one EndReason name falls through to the timing rules.
        [Test]
        public void DisconnectClassifier_NonExactReasonText_FallsThrough()
        {
            // NGO's own generated text.
            Assert.AreEqual(EndReason.HostLeft, DisconnectClassifier.Classify(false, true, "[Disconnect Event] Disconnected by the server.", 50.0));
            Assert.AreEqual(EndReason.Timeout, DisconnectClassifier.Classify(false, false, "[Disconnect Event] Transport failure", 20.0));
            // A comma list would otherwise parse as FullLobby | Kicked = 6 = VersionMismatch.
            Assert.AreEqual(EndReason.HostLeft, DisconnectClassifier.Classify(false, true, "FullLobby, Kicked", 50.0));
            // Wrong case, digits, sentences and out-of-range values are not reasons.
            Assert.AreEqual(EndReason.HostLeft, DisconnectClassifier.Classify(false, true, "fulllobby", 50.0));
            Assert.AreEqual(EndReason.HostLeft, DisconnectClassifier.Classify(false, true, "4", 50.0));
            Assert.AreEqual(EndReason.HostLeft, DisconnectClassifier.Classify(false, true, "Disconnected due to host shutting down.", 50.0));
            Assert.AreEqual(EndReason.VersionMismatch, DisconnectClassifier.Classify(false, false, " FullLobby", 1.0));
        }
    }
}

using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class UgsErrorMapTests
    {
        [Test]
        public void UgsErrorMap_FullMessage_IsFullLobby()
        {
            Assert.AreEqual(EndReason.FullLobby, UgsErrorMap.Map(UgsErrorMap.SessionErrorUnknown, "Lobby is full"));
            Assert.AreEqual(EndReason.FullLobby, UgsErrorMap.Map(UgsErrorMap.SessionErrorUnknown, "LOBBY FULL"));
        }

        [Test]
        public void UgsErrorMap_SessionNotFound_IsInvalidCode()
        {
            Assert.AreEqual(EndReason.InvalidCode, UgsErrorMap.Map(UgsErrorMap.SessionErrorSessionNotFound, "Lobby not found"));
            Assert.AreEqual(EndReason.InvalidCode, UgsErrorMap.Map(UgsErrorMap.SessionErrorSessionNotFound, null));
        }

        [Test]
        public void UgsErrorMap_Other_IsServicesUnavailable()
        {
            Assert.AreEqual(EndReason.ServicesUnavailable, UgsErrorMap.Map(UgsErrorMap.SessionErrorUnknown, "Authentication failed"));
            Assert.AreEqual(EndReason.ServicesUnavailable, UgsErrorMap.Map(UgsErrorMap.SessionErrorUnknown, null));
            Assert.AreEqual(EndReason.ServicesUnavailable, UgsErrorMap.Map(27, "Failed to start NetworkManager component."));
        }

        [Test]
        public void UgsErrorMap_FullTextBeatsSessionNotFoundCode_IsFullLobby()
        {
            Assert.AreEqual(EndReason.FullLobby, UgsErrorMap.Map(UgsErrorMap.SessionErrorSessionNotFound, "Lobby is full"));
        }

        [Test]
        public void UgsErrorMap_CodeTextWithUnknownCode_IsInvalidCode()
        {
            Assert.AreEqual(EndReason.InvalidCode, UgsErrorMap.Map(UgsErrorMap.SessionErrorUnknown, "Invalid join code"));
            Assert.AreEqual(EndReason.InvalidCode, UgsErrorMap.Map(UgsErrorMap.SessionErrorUnknown, "invalid code supplied"));
            Assert.AreEqual(EndReason.InvalidCode, UgsErrorMap.Map(UgsErrorMap.SessionErrorUnknown, "lobby NOT FOUND"));
        }

        [Test]
        public void UgsErrorMap_InvalidParameter_IsInvalidCode()
        {
            Assert.AreEqual(EndReason.InvalidCode, UgsErrorMap.Map(UgsErrorMap.SessionErrorInvalidParameter, "Bad request"));
            Assert.AreEqual(EndReason.InvalidCode, UgsErrorMap.Map(UgsErrorMap.SessionErrorInvalidParameter, null));
        }

        // Review Focus 4: "full" inside a longer word is not a full lobby.
        [Test]
        public void UgsErrorMap_FullInsideLongerWord_IsNotFullLobby()
        {
            Assert.AreEqual(EndReason.ServicesUnavailable, UgsErrorMap.Map(UgsErrorMap.SessionErrorUnknown, "Session created successfully but the network start failed"));
            Assert.AreEqual(EndReason.ServicesUnavailable, UgsErrorMap.Map(UgsErrorMap.SessionErrorUnknown, "Lobby is not fully initialized"));
            Assert.AreEqual(EndReason.FullLobby, UgsErrorMap.Map(UgsErrorMap.SessionErrorUnknown, "LobbyFull"));
            Assert.AreEqual(EndReason.FullLobby, UgsErrorMap.Map(UgsErrorMap.SessionErrorUnknown, "Lobby is full."));
        }

        // Review Focus 5: a Relay failure is a service outage, not a bad join code.
        [Test]
        public void UgsErrorMap_RelayJoinCodeFailure_IsServicesUnavailable()
        {
            Assert.AreEqual(EndReason.ServicesUnavailable, UgsErrorMap.Map(UgsErrorMap.SessionErrorUnknown, "Failed to fetch relay join code"));
            Assert.AreEqual(EndReason.ServicesUnavailable, UgsErrorMap.Map(UgsErrorMap.SessionErrorInvalidParameter, "Relay allocation not found"));
            Assert.AreEqual(EndReason.InvalidCode, UgsErrorMap.Map(UgsErrorMap.SessionErrorSessionNotFound, "Relay join code not found"));
        }

        // A locked lobby (mid-run join) arrives as SessionError.Unknown with text only.
        [Test]
        public void UgsErrorMap_LockedMessage_IsJoinsClosed()
        {
            Assert.AreEqual(EndReason.JoinsClosed, UgsErrorMap.Map(UgsErrorMap.SessionErrorUnknown, "Lobby is locked"));
            Assert.AreEqual(EndReason.JoinsClosed, UgsErrorMap.Map(UgsErrorMap.SessionErrorUnknown, "LOBBY LOCKED"));
            Assert.AreEqual(EndReason.InvalidCode, UgsErrorMap.Map(UgsErrorMap.SessionErrorSessionNotFound, "Lobby locked"));
        }
    }
}

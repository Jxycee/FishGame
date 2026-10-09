// Tests for FishGame.Core.EndReasonText (spec 11.2 table, contract 7.3).
using System;
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class EndReasonTextTests
    {
        [Test]
        public void EndReasonText_EveryReason_HasKeyExceptUserLeft()
        {
            foreach (EndReason reason in (EndReason[])Enum.GetValues(typeof(EndReason)))
            {
                string key = EndReasonText.ToastKey(reason);
                if (reason == EndReason.UserLeft)
                {
                    Assert.IsNull(key, "UserLeft shows no toast");
                    continue;
                }
                Assert.IsNotNull(key, reason.ToString());
                StringAssert.StartsWith("err.", key, reason.ToString());
            }

            Assert.AreEqual("err.host_left", EndReasonText.ToastKey(EndReason.HostLeft));
            Assert.AreEqual("err.rejected", EndReasonText.ToastKey(EndReason.Kicked));
            Assert.AreEqual("err.rejected", EndReasonText.ToastKey(EndReason.DuplicateIdentity));
            Assert.AreEqual("err.timeout", EndReasonText.ToastKey(EndReason.Timeout));
            Assert.AreEqual("err.session_full", EndReasonText.ToastKey(EndReason.FullLobby));
            Assert.AreEqual("err.joins_closed", EndReasonText.ToastKey(EndReason.JoinsClosed));
            Assert.AreEqual("err.version", EndReasonText.ToastKey(EndReason.VersionMismatch));
            Assert.AreEqual("err.code_not_found", EndReasonText.ToastKey(EndReason.InvalidCode));
            Assert.AreEqual("err.services", EndReasonText.ToastKey(EndReason.ServicesUnavailable));
            Assert.AreEqual("err.network", EndReasonText.ToastKey(EndReason.TransportFailure));
            Assert.AreEqual("err.generic", EndReasonText.ToastKey(EndReason.Unknown));
            Assert.AreEqual("err.generic", EndReasonText.ToastKey((EndReason)200), "undefined values fall back");
        }
    }
}

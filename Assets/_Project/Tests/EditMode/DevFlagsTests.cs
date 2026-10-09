// Tests for FishGame.Core.DevFlags (contract 7.3).
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class DevFlagsTests
    {
        [Test]
        public void DevFlags_Parse_AllFlags()
        {
            DevFlags f = DevFlags.Parse(new[]
            {
                "Unity.exe", "-batchmode", "-dev-solo", "-dev-host-direct", "-dev-join-direct", "192.168.1.20:7800",
                "-dev-protocol-offset", "1", "-dev-long-timeout", "-logFile", "x.log"
            });
            Assert.IsTrue(f.Solo);
            Assert.IsTrue(f.HostDirect);
            Assert.IsTrue(f.HasJoinDirect);
            Assert.AreEqual("192.168.1.20", f.JoinDirectAddress);
            Assert.AreEqual((ushort)7800, f.JoinDirectPort);
            Assert.AreEqual(1, f.ProtocolOffset);
            Assert.IsTrue(f.LongTimeout);

            Assert.AreEqual(-2, DevFlags.Parse(new[] { "-dev-protocol-offset", "-2" }).ProtocolOffset);
        }

        [Test]
        public void DevFlags_Parse_JoinDirectWithoutPort_UsesDefault()
        {
            DevFlags f = DevFlags.Parse(new[] { "-dev-join-direct", "127.0.0.1" });
            Assert.IsTrue(f.HasJoinDirect);
            Assert.AreEqual("127.0.0.1", f.JoinDirectAddress);
            Assert.AreEqual(NetworkConstants.DefaultPort, f.JoinDirectPort);
        }

        [Test]
        public void DevFlags_Parse_MalformedOffset_IsZero()
        {
            Assert.AreEqual(0, DevFlags.Parse(new[] { "-dev-protocol-offset", "abc" }).ProtocolOffset);
            Assert.AreEqual(0, DevFlags.Parse(new[] { "-dev-protocol-offset" }).ProtocolOffset);
            Assert.AreEqual(0, DevFlags.Parse(new[] { "-dev-protocol-offset", "1.5" }).ProtocolOffset);
        }

        [Test]
        public void DevFlags_None_AndNullArgs_AreDefaults()
        {
            foreach (DevFlags f in new[] { DevFlags.None, DevFlags.Parse(null), DevFlags.Parse(new string[0]) })
            {
                Assert.IsFalse(f.Solo);
                Assert.IsFalse(f.HostDirect);
                Assert.IsFalse(f.HasJoinDirect);
                Assert.AreEqual(NetworkConstants.LoopbackAddress, f.JoinDirectAddress);
                Assert.AreEqual(NetworkConstants.DefaultPort, f.JoinDirectPort);
                Assert.AreEqual(0, f.ProtocolOffset);
                Assert.IsFalse(f.LongTimeout);
            }
        }

        // Review Focus 3: MPPM tag tokens in the wrong shape.
        [Test]
        public void DevFlags_Parse_MalformedJoinDirect_IsIgnoredAndNextFlagKept()
        {
            // A missing value must not swallow the next flag.
            DevFlags missing = DevFlags.Parse(new[] { "-dev-join-direct", "-dev-solo" });
            Assert.IsFalse(missing.HasJoinDirect);
            Assert.IsTrue(missing.Solo);

            string[] bad = { "not-an-ip", "127.0.0.1:", "127.0.0.1:0", "127.0.0.1:65536", "127.0.0.1:77x", "256.0.0.1", "127.0.0", "localhost:7777" };
            foreach (string value in bad)
            {
                DevFlags f = DevFlags.Parse(new[] { "-dev-join-direct", value });
                Assert.IsFalse(f.HasJoinDirect, value);
                Assert.AreEqual(NetworkConstants.LoopbackAddress, f.JoinDirectAddress, value);
                Assert.AreEqual(NetworkConstants.DefaultPort, f.JoinDirectPort, value);
            }

            // Flag names are case-insensitive; the highest valid port is accepted.
            DevFlags upper = DevFlags.Parse(new[] { "-DEV-JOIN-DIRECT", "10.0.0.2:65535", "-Dev-Long-Timeout" });
            Assert.IsTrue(upper.HasJoinDirect);
            Assert.AreEqual("10.0.0.2", upper.JoinDirectAddress);
            Assert.AreEqual((ushort)65535, upper.JoinDirectPort);
            Assert.IsTrue(upper.LongTimeout);
        }
    }
}

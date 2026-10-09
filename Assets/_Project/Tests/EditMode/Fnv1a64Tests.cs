using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class Fnv1a64Tests
    {
        [Test]
        public void Fnv1a64_KnownVectors_Match()
        {
            Assert.That(Fnv1a64.Hash(""), Is.EqualTo(0xcbf29ce484222325UL));
            Assert.That(Fnv1a64.Hash("a"), Is.EqualTo(0xaf63dc4c8601ec8cUL));
            Assert.That(Fnv1a64.Hash("foobar"), Is.EqualTo(0x85944171f73967e8UL));
            Assert.That(Fnv1a64.Hash((string)null), Is.EqualTo(Fnv1a64.OffsetBasis));
        }

        [Test]
        public void Fnv1a64_IncrementalAppend_EqualsOneShotHash()
        {
            var h = Fnv1a64.Create();
            h.Append("foo");
            h.Append("bar");
            Assert.That(h.Value, Is.EqualTo(Fnv1a64.Hash("foobar")));
            Assert.That(Fnv1a64.Hash(new byte[] { 0x61 }), Is.EqualTo(Fnv1a64.Hash("a")));
        }

        [Test]
        public void Fnv1a64_AppendInt32LE_EqualsLittleEndianBytes()
        {
            var viaInt = Fnv1a64.Create();
            viaInt.AppendInt32LE(0x01020304);
            Assert.That(viaInt.Value, Is.EqualTo(Fnv1a64.Hash(new byte[] { 0x04, 0x03, 0x02, 0x01 })));

            var negative = Fnv1a64.Create();
            negative.AppendInt32LE(-1);
            Assert.That(negative.Value, Is.EqualTo(Fnv1a64.Hash(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF })));
        }
    }
}
